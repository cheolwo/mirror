using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class WarehouseReconsignmentApprovalTests
{
    private static readonly ServiceProvider Provider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider(validateScopes: true);

    [Theory]
    [InlineData("dropoff", "WarehouseReconsignmentCoordinatesMissing")]
    [InlineData("pickup", "WarehouseReconsignmentCoordinatesMissing")]
    [InlineData("invalid-coordinate", "WarehouseReconsignmentCoordinatesMissing")]
    [InlineData("fare", "WarehouseReconsignmentFareMissing")]
    [InlineData("driver-pay", "WarehouseReconsignmentFareMissing")]
    [InlineData("distance", "WarehouseReconsignmentFareMissing")]
    [InlineData("payment", "WarehouseReconsignmentFareMissing")]
    [InlineData("fare-link", "WarehouseReconsignmentFareMissing")]
    [InlineData("fare-changed", "WarehouseReconsignmentFareChanged")]
    [InlineData("insufficient-payment", "WarehouseReconsignmentFareChanged")]
    public async Task 창고초안의필수입력미확정은승인전에차단하고미시작상태와재고연결을보존한다(string missing, string code)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Seed(db, ready: true);
        var source = await db.화주운송의뢰.SingleAsync(); var fare = await db.운임구성.SingleAsync();
        switch (missing)
        {
            case "dropoff": source.하차_위도 = null; source.하차_경도 = null; break;
            case "pickup": source.픽업_경도 = null; break;
            case "invalid-coordinate": source.하차_위도 = 100m; break;
            case "fare": source.최종운임 = null; break;
            case "driver-pay": fare.기사지급예정운임 = null; break;
            case "distance": fare.예상거리Km = null; break;
            case "payment": source.결제예정금액 = null; break;
            case "fare-link": source.운임구성Id = null; break;
            case "fare-changed": source.최종운임 = 11000m; break;
            case "insufficient-payment": source.결제예정금액 = 9000; break;
        }
        await db.SaveChangesAsync();
        var queue = new Queue(db); var sync = new Sync();
        var result = await Approval(db, queue, sync).Handle(new("warehouse-draft", "승인 전 검토"), default);
        Assert.True(result.IsFailed); Assert.Equal(code, result.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        await using var read = Db(connection);
        var stored = await read.화주운송의뢰.SingleAsync();
        Assert.Equal("청구대기", stored.정산상태); Assert.Equal("미시작", stored.배차상태);
        Assert.Equal("결제대기", stored.결제상태); Assert.Equal(0, queue.Calls); Assert.Equal(0, sync.Calls);
        Assert.Single(read.운송원장); Assert.Empty(read.결제); Assert.Empty(read.기사운송대금지급요청);
    }

    [Fact]
    public async Task 차단된창고초안은기존화주수정에서좌표규격과서버견적을확인한뒤승인할수있다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Seed(db, ready: false);
        var queue = new Queue(db); var sync = new Sync();
        Assert.True((await Approval(db, queue, sync).Handle(new("warehouse-draft", null), default)).IsFailed);
        var update = new 의뢰수정Command("warehouse-draft", new(null, null, null), new(null, null, null, 2m, 0.03m, null, null),
            new(null, null, null, null, null, null, null, null), new(null, null, 37.59m, 127.09m, null, null, null, null),
            new(null), null, new PricingDTO { 예상거리Km = 2.5m, 기사지급예정운임 = 4500m });
        var edited = await new 의뢰수정CommandHandler(db, Operators(db), new Quote(), sync, new Current())
            .Handle(update, default);
        Assert.True(edited.IsSuccess, string.Join(";", edited.Errors.Select(x => x.Message)));
        var source = await db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(37.588m, source.픽업_위도); Assert.Equal(127.085m, source.픽업_경도);
        Assert.Equal(37.59m, source.하차_위도); Assert.Equal(127.09m, source.하차_경도);
        Assert.Equal(2m, source.화물중량Kg); Assert.Equal(0.03m, source.화물부피Cbm);
        Assert.Equal(10000m, source.최종운임); Assert.Equal("청구대기", source.정산상태);
        var result = await Approval(db, queue, sync).Handle(new("warehouse-draft", "기존 입력·견적 검토 완료"), default);
        Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(x => x.Message)));
        await using var read = Db(connection);
        var stored = await read.화주운송의뢰.SingleAsync(); var projection = await read.운송원장.SingleAsync();
        Assert.Equal("후불승인완료", stored.정산상태); Assert.Equal("매칭중", stored.배차상태);
        Assert.Equal("결제대기", stored.결제상태); Assert.Equal(4500m, (await read.운임구성.SingleAsync()).기사지급예정운임);
        Assert.Equal(stored.하차_위도, projection.하차_위도); Assert.Equal(stored.최종운임, projection.운임);
        Assert.Equal(1, queue.Calls); Assert.Empty(read.결제); Assert.Empty(read.기사운송대금지급요청);
    }

    [Fact]
    public async Task 다른사용자에게초안존재나위치견적누락을노출하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); await Seed(db, ready: false);
        var queue = new Queue(db); var sync = new Sync(); var user = new Other();
        var result = await new 화주운송의뢰후불승인CommandHandler(db, user, queue, sync,
            new 화주운송업무담당자UseCase(db, user, TimeProvider.System)).Handle(new("warehouse-draft", null), default);
        Assert.True(result.IsFailed); Assert.Equal("의뢰를 찾을 수 없습니다.", result.Errors.Single().Message);
        Assert.Empty(result.Errors.Single().Metadata); Assert.Equal(0, queue.Calls);
    }

    [Fact]
    public void 일반화물과생활원천의기존후불승인조건은새창고준비정책으로강화하지않는다()
    {
        var source = new 화주운송의뢰();
        Assert.Null(창고출고운송승인준비Policy.확인(source, null, null));
        Assert.Null(창고출고운송승인준비Policy.확인(source, new() { 원본의뢰유형 = 운송의뢰배차원천유형.화주운송의뢰 }, null));
        Assert.Null(창고출고운송승인준비Policy.확인(source, new() { 원본의뢰유형 = 운송의뢰배차원천유형.주문자화물주문 }, null));
    }

    private static async Task Seed(SsalddelContext db, bool ready)
    {
        var source = new 화주운송의뢰 { 의뢰Id = "warehouse-draft", 화주Id = "owner", 주문자UserId = "owner", 차량종류 = "1톤 카고",
            정산시점 = "운송완료후정산", 정산상태 = "청구대기", 결제상태 = "결제대기", 배차상태 = "미시작",
            픽업_도로명주소 = "합성 창고 상차지", 픽업_위도 = 37.588m, 픽업_경도 = 127.085m,
            하차_도로명주소 = "합성 전달 지역", 하차_위도 = ready ? 37.59m : null, 하차_경도 = ready ? 127.09m : null,
            최종운임 = ready ? 10000m : null, 결제예정금액 = ready ? 10000 : null };
        db.Add(source); db.운송원장.Add(new() { 의뢰Id = source.의뢰Id, 운송번호 = source.의뢰Id,
            원본의뢰유형 = 운송의뢰배차원천유형.창고출고연계운송, 원본의뢰Id = "51", 상태 = 상태값.배차대기상태.대기 });
        await db.SaveChangesAsync();
        if (!ready) return;
        var fare = new 운임구성 { 의뢰Id = source.의뢰Id, 최종운임 = 10000m, 기사지급예정운임 = 4500m, 예상거리Km = 2.5m };
        db.Add(fare); await db.SaveChangesAsync(); source.운임구성Id = fare.Id; await db.SaveChangesAsync();
    }
    private static 화주운송업무담당자UseCase Operators(SsalddelContext db) => new(db, new Current(), TimeProvider.System);
    private static 화주운송의뢰후불승인CommandHandler Approval(SsalddelContext db, Queue queue, Sync sync)
        => new(db, new Current(), queue, sync, Operators(db));
    private static SsalddelContext Db(SqliteConnection connection) => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseSqlite(connection).UseInternalServiceProvider(Provider).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Current : ICurrentUserAccessor { public string? UserId => "owner"; public string? Role => "화주"; }
    private sealed class Other : ICurrentUserAccessor { public string? UserId => "another"; public string? Role => "화주"; }
    private sealed class Quote : I화주운송기준운임Service
    {
        public Task<Result<화주운송기준운임견적응답>> 견적Async(화주운송기준운임견적요청 request, CancellationToken ct = default)
            => Task.FromResult(Result.Ok(new 화주운송기준운임견적응답 { 차량종류 = request.차량종류, 예상거리Km = 2.5m,
                기본운임 = 7500m, 거리운임 = 2500m, Km당단가 = 1000m, 최종운임 = 10000m, 거리계산방식 = "합성시험거리", 단가출처 = "합성시험단가" }));
    }
    private sealed class Queue(SsalddelContext db) : I운송의뢰배차대기Service
    {
        public int Calls;
        public async Task<운송원장> 생성또는조회Async(출고예정운송대상 target, 운송의뢰배차대기생성옵션? options = null, CancellationToken ct = default)
        { Calls++; return await db.운송원장.SingleAsync(x => x.의뢰Id == target.운송의뢰Id, ct); }
    }
    private sealed class Sync : I운송원장Mongo동기화Service
    {
        public int Calls;
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 source, string user, CancellationToken ct = default)
        { Calls++; return Task.FromResult<커뮤니티원장Dto?>(new()); }
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 source, string user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
