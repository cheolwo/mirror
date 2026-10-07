using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Migrations;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화주운송견적수정Tests
{
    [Fact]
    public async Task 기존요금수정은_서버재계산값과근거를저장하고_재호출에도한구성을유지한다()
    {
        await using var db = Context();
        await Seed(db);
        var sync = Sync();
        var handler = Handler(db, sync);
        var input = Command(new PricingDTO { 예상거리Km = 3.5m, 기본운임 = 1m, 최종운임 = 2m,
            거리계산방식 = "Directions5", 단가출처 = "client-forged-source" });
        var first = await handler.Handle(input, default);
        var replay = await handler.Handle(input, default);
        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        var entity = await db.화주운송의뢰.SingleAsync();
        var fare = await db.운임구성.SingleAsync();
        Assert.Equal(fare.Id, entity.운임구성Id);
        Assert.Equal(39550m, entity.최종운임);
        Assert.Equal(39550, entity.결제예정금액);
        Assert.Equal("결제대기", entity.결제상태);
        Assert.Equal("청구대기", entity.정산상태);
        Assert.Equal("미시작", entity.배차상태);
        Assert.Equal(3.5m, fare.예상거리Km);
        Assert.Equal(1300m, fare.Km당단가);
        Assert.Equal(35000m, fare.기본운임);
        Assert.Equal("입력거리", fare.거리계산방식);
        Assert.Equal("v1.0 기본단가", fare.단가출처);
        Assert.Equal(3.5m, first.Value.요금옵션?.예상거리Km);
        Assert.Equal("입력거리", replay.Value.요금옵션?.거리계산방식);
        Assert.Equal(2, sync.Calls.Count);
        Assert.All(sync.Calls, x => { Assert.Equal(39550m, x.Fare); Assert.Equal("shipper-a", x.User); });
    }

    [Theory]
    [InlineData("payment")]
    [InlineData("approval")]
    [InlineData("dispatch")]
    [InlineData("cancel")]
    [InlineData("driver")]
    [InlineData("unknown")]
    public async Task 승인결제배정종결뒤_견적수정은저장없이409이다(string change)
    {
        await using var db = Context();
        await Seed(db);
        var entity = await db.화주운송의뢰.SingleAsync();
        switch (change)
        {
            case "payment": entity.결제상태 = "결제완료"; break;
            case "approval": entity.정산상태 = "후불승인완료"; break;
            case "dispatch": entity.배차상태 = "매칭중"; break;
            case "cancel": entity.상태 = "취소"; break;
            case "unknown": entity.정산상태 = "새로운상태"; break;
            case "driver": db.운송원장.Add(new 운송원장 { 의뢰Id = "quote-request", 운송번호 = "quote-request", 상태 = "대기", 확정기사Id = "driver-a" }); break;
        }
        await db.SaveChangesAsync();
        var sync = Sync();
        var result = await Handler(db, sync).Handle(Command(new() { 예상거리Km = 3.5m }), default);
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, x => x.Metadata.TryGetValue("StatusCode", out var code) && Equals(code, 409));
        Assert.Empty(await db.운임구성.ToListAsync());
        Assert.Null(entity.최종운임);
        Assert.Empty(sync.Calls);
    }

    [Theory]
    [InlineData("distance")]
    [InlineData("surcharge")]
    [InlineData("clientFare")]
    [InlineData("payment")]
    public async Task 음수거리요금은_보정하거나저장하지않는다(string field)
    {
        await using var db = Context();
        await Seed(db);
        var pricing = new PricingDTO { 예상거리Km = 3.5m };
        if (field == "distance") pricing.예상거리Km = -1m;
        if (field == "surcharge") pricing.할증 = -1m;
        if (field == "clientFare") pricing.최종운임 = -1m;
        var sync = Sync();
        var result = await Handler(db, sync).Handle(Command(pricing) with { 결제예정금액 = field == "payment" ? -1 : null }, default);
        Assert.True(result.IsFailed);
        Assert.Empty(await db.운임구성.ToListAsync());
        Assert.Empty(sync.Calls);
    }

    [Fact]
    public async Task 거리근거없는금액단독입력은_추측견적으로저장하지않는다()
    {
        await using var db = Context();
        await Seed(db);
        var result = await Handler(db, Sync()).Handle(Command(new() { 최종운임 = 50000m }), default);
        Assert.True(result.IsFailed);
        Assert.Null((await db.화주운송의뢰.SingleAsync()).최종운임);
        Assert.Empty(await db.운임구성.ToListAsync());
    }

    [Fact]
    public async Task 다른화주는_요금수정권한과거리정보를획득하지못한다()
    {
        await using var db = Context();
        await Seed(db);
        var sync = Sync();
        var result = await Handler(db, sync, "shipper-other").Handle(Command(new() { 예상거리Km = 3.5m }), default);
        Assert.True(result.IsFailed);
        Assert.Empty(await db.운임구성.ToListAsync());
        Assert.Empty(sync.Calls);
    }

    [Fact]
    public async Task 결제예정액이서버견적보다낮으면_견적과예정액모두저장하지않는다()
    {
        await using var db = Context();
        await Seed(db);
        var result = await Handler(db, Sync()).Handle(Command(new() { 예상거리Km = 3.5m }) with { 결제예정금액 = 1 }, default);
        Assert.True(result.IsFailed);
        Assert.Empty(await db.운임구성.ToListAsync());
        Assert.Null((await db.화주운송의뢰.SingleAsync()).결제예정금액);
    }

    [Fact]
    public async Task 주소변경은_기존거리견적을재사용하지않는다()
    {
        await using var db = Context();
        await Seed(db);
        var handler = Handler(db, Sync());
        Assert.True((await handler.Handle(Command(new() { 예상거리Km = 3.5m }), default)).IsSuccess);
        var change = Command(null) with { 하차지 = new 위치정보입력값("다른 하차 주소", null, null, null, null, null, null, null) };
        Assert.True((await handler.Handle(change, default)).IsFailed);
        Assert.Equal(3.5m, (await db.운임구성.SingleAsync()).예상거리Km);
        Assert.Equal("합성 하차지", (await db.화주운송의뢰.SingleAsync()).하차_도로명주소);
    }

    [Fact]
    public async Task 새주소와입력거리저장은_이전주소좌표를제거한다()
    {
        await using var db = Context();
        await Seed(db);
        var entity = await db.화주운송의뢰.SingleAsync();
        entity.하차_위도 = 37.5m;
        entity.하차_경도 = 127m;
        await db.SaveChangesAsync();
        var change = Command(new() { 예상거리Km = 4m }) with
        {
            하차지 = new 위치정보입력값("새 합성 하차지", null, null, null, null, null, null, null)
        };
        Assert.True((await Handler(db, Sync()).Handle(change, default)).IsSuccess);
        Assert.Equal("새 합성 하차지", entity.하차_도로명주소);
        Assert.Null(entity.하차_위도);
        Assert.Null(entity.하차_경도);
        Assert.Equal(4m, (await db.운임구성.SingleAsync()).예상거리Km);
    }

    [Fact]
    public async Task 대기중운송도_같은트랜잭션에서_최신견적과주소를받는다()
    {
        await using var db = Context();
        await Seed(db);
        db.운송원장.Add(new() { 의뢰Id = "quote-request", 운송번호 = "quote-request", 상태 = "대기", 운임 = 1m });
        await db.SaveChangesAsync();
        var result = await Handler(db, Sync()).Handle(Command(new() { 예상거리Km = 3.5m }), default);
        Assert.True(result.IsSuccess);
        var queued = await db.운송원장.SingleAsync();
        Assert.Equal(39550m, queued.운임);
        Assert.Equal("합성 상차지", queued.픽업_도로명주소);
        Assert.Equal("합성 하차지", queued.하차_도로명주소);
        Assert.Equal("대기", queued.상태);
    }

    [Fact]
    public async Task 승인뒤_동일견적과정산조건을재전송한연락처수정은_승인을보존한다()
    {
        await using var db = Context();
        await Seed(db);
        var sync = Sync();
        var handler = Handler(db, sync);
        Assert.True((await handler.Handle(Command(new() { 예상거리Km = 3.5m }), default)).IsSuccess);
        var entity = await db.화주운송의뢰.SingleAsync();
        entity.정산상태 = "후불승인완료";
        entity.정산시점 = "월말정산";
        entity.증빙방식 = "없음";
        entity.수납주체 = "플랫폼";
        entity.정산메모 = string.Empty;
        await db.SaveChangesAsync();
        var unchanged = Command(new() { 예상거리Km = 3.5m, 최종운임 = 39550m, 기사지급예정운임 = 39550m }) with
        {
            결제예정금액 = 39550,
            픽업지 = new 위치정보입력값(null, null, null, null, "새 연락처", null, null, null),
            정산조건 = new 정산조건입력값(null, new() { 정산시점 = 정산시점.월말정산, 증빙방식 = 증빙방식.없음, 수납주체 = 수납주체.플랫폼 })
        };
        var result = await handler.Handle(unchanged, default);
        Assert.True(result.IsSuccess);
        Assert.Equal("새 연락처", entity.픽업_연락처_이름);
        Assert.Equal("후불승인완료", entity.정산상태);
        Assert.Equal(39550m, entity.최종운임);
        Assert.Single(await db.운임구성.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task 신규의뢰의0음수거리는_견적을우회하거나Command를생성하지않는다(int distance)
    {
        var sender = new RecordingSender();
        var useCase = new 화주운송의뢰UseCase(sender, null!, null!, null!, null!, null!, null!);
        var result = await useCase.의뢰생성Async(new() { 요금옵션 = new() { 예상거리Km = distance, 거리계산방식 = "forged" } });
        Assert.True(result.IsFailed);
        Assert.Null(sender.CreateCommand);
    }

    [Fact]
    public async Task 신규의뢰의거리미확인금액입력에는_위조계산근거가저장되지않는다()
    {
        var sender = new RecordingSender();
        var useCase = new 화주운송의뢰UseCase(sender, null!, null!, null!, new 화주운송요금정책검토Service(), null!, null!);
        var result = await useCase.의뢰생성Async(new() { 화물 = new() { 화물종류 = "일반화물" },
            요금옵션 = new() { 최종운임 = 40000m, 거리계산방식 = "Directions5", 단가출처 = "forged", Km당단가 = 1m, 최소운임 = 1m } });
        Assert.True(result.IsSuccess);
        Assert.NotNull(sender.CreateCommand);
        Assert.Null(sender.CreateCommand.예상거리Km);
        Assert.Null(sender.CreateCommand.거리계산방식);
        Assert.Null(sender.CreateCommand.단가출처);
        Assert.Null(sender.CreateCommand.Km당단가);
        Assert.Equal(40000m, sender.CreateCommand.최종운임);
    }

    [Fact]
    public async Task UseCase는_기존수정DTO의숫자요금과결제예정액을Command로전달한다()
    {
        var sender = new RecordingSender();
        var useCase = new 화주운송의뢰UseCase(sender, null!, null!, null!, null!, null!, null!);
        await useCase.의뢰수정Async("quote-request", new() { 요금옵션 = new() { 예상거리Km = 3.5m }, 결제예정금액 = 42000 });
        Assert.NotNull(sender.Command);
        Assert.Equal(3.5m, sender.Command.요금옵션?.예상거리Km);
        Assert.Equal(42000, sender.Command.결제예정금액);
    }

    [Fact]
    public void 마이그레이션은_기존행에숫자를생성하지않고_nullable근거다섯열만추가한다()
    {
        var migration = new AddCargoFareQuoteEvidence();
        var added = migration.UpOperations.Cast<AddColumnOperation>().ToArray();
        Assert.Equal(5, added.Length);
        Assert.All(added, x => { Assert.Equal("운임구성", x.Table); Assert.True(x.IsNullable); Assert.Null(x.DefaultValue); Assert.Null(x.DefaultValueSql); });
        Assert.Equal(added.Select(x => x.Name).OrderBy(x => x), migration.DownOperations.Cast<DropColumnOperation>().Select(x => x.Name).OrderBy(x => x));
    }

    [Theory]
    [InlineData("dispatch", "quantity")]
    [InlineData("dispatch", "pickupWindow")]
    [InlineData("dispatch", "dropoffWindow")]
    [InlineData("dispatch", "route")]
    [InlineData("dispatch", "transport")]
    [InlineData("approval", "weight")]
    [InlineData("approval", "settlement")]
    [InlineData("payment", "temperature")]
    [InlineData("driver", "quantity")]
    [InlineData("cancel", "quantity")]
    [InlineData("unknown", "quantity")]
    public async Task 숫자견적이없어도_확정후핵심조건변경은409이며_기존합의를덮어쓰지않는다(string lifecycle, string field)
    {
        await using var db = Context();
        await Seed(db);
        var entity = await db.화주운송의뢰.SingleAsync();
        entity.화물수량 = 2;
        entity.화물중량Kg = 10m;
        entity.화물온도조건 = "상온";
        switch (lifecycle)
        {
            case "dispatch": entity.배차상태 = "배차확정"; break;
            case "approval": entity.정산상태 = "후불승인완료"; break;
            case "payment": entity.결제상태 = "결제완료"; break;
            case "driver": db.운송원장.Add(new() { 의뢰Id = "quote-request", 운송번호 = "quote-request", 상태 = "대기", 확정기사Id = "driver-a" }); break;
            case "cancel": entity.상태 = "취소"; break;
            case "unknown": entity.정산상태 = "새로운상태"; break;
        }
        await db.SaveChangesAsync();
        var before = entity.UpdatedAt;
        var settlement = entity.정산상태;
        var paymentMethod = entity.결제수단;
        var transportMode = entity.운송방식;
        var pickupWindowStart = entity.픽업_시간창_시작일시;
        var pickupWindowEnd = entity.픽업_시간창_종료일시;
        var dropoffWindowStart = entity.하차_시간창_시작일시;
        var dropoffWindowEnd = entity.하차_시간창_종료일시;
        var sync = Sync();

        var result = await Handler(db, sync).Handle(CoreChange(field), default);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, error => error.Metadata.TryGetValue("StatusCode", out var status) && Equals(status, 409)
            && error.Metadata.TryGetValue("ErrorCode", out var code) && Equals(code, "FreightCoreConditionsLocked"));
        db.ChangeTracker.Clear();
        var stored = await db.화주운송의뢰.SingleAsync();
        Assert.Equal(2, stored.화물수량);
        Assert.Equal(10m, stored.화물중량Kg);
        Assert.Equal("상온", stored.화물온도조건);
        Assert.Equal("합성 하차지", stored.하차_도로명주소);
        Assert.Equal(pickupWindowStart, stored.픽업_시간창_시작일시);
        Assert.Equal(pickupWindowEnd, stored.픽업_시간창_종료일시);
        Assert.Equal(dropoffWindowStart, stored.하차_시간창_시작일시);
        Assert.Equal(dropoffWindowEnd, stored.하차_시간창_종료일시);
        Assert.Equal(paymentMethod, stored.결제수단);
        Assert.Equal(transportMode, stored.운송방식);
        Assert.Equal(settlement, stored.정산상태);
        Assert.Equal(before, stored.UpdatedAt);
        Assert.Empty(await db.운임구성.ToListAsync());
        Assert.Empty(sync.Calls);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("weight")]
    [InlineData("temperature")]
    [InlineData("pickupWindow")]
    [InlineData("dropoffWindow")]
    [InlineData("route")]
    [InlineData("transport")]
    [InlineData("settlement")]
    public async Task 숫자견적없는사전등록의뢰는_핵심조건을계속수정할수있다(string field)
    {
        await using var db = Context();
        await Seed(db);
        var result = await Handler(db, Sync()).Handle(CoreChange(field), default);

        Assert.True(result.IsSuccess);
        var stored = await db.화주운송의뢰.SingleAsync();
        switch (field)
        {
            case "quantity": Assert.Equal(7, stored.화물수량); break;
            case "weight": Assert.Equal(15m, stored.화물중량Kg); break;
            case "temperature": Assert.Equal("냉장", stored.화물온도조건); break;
            case "pickupWindow": Assert.Equal(new DateTime(2030, 1, 1, 9, 0, 0), stored.픽업_시간창_시작일시); break;
            case "dropoffWindow": Assert.Equal(new DateTime(2030, 1, 1, 11, 0, 0), stored.하차_시간창_시작일시); break;
            case "route": Assert.Equal("새 합성 하차지", stored.하차_도로명주소); break;
            case "transport": Assert.Equal("예약운송", stored.운송방식); break;
            case "settlement": Assert.Equal("현금", stored.결제수단); break;
        }
        Assert.Equal("미시작", stored.배차상태);
        Assert.Null(stored.최종운임);
    }

    [Fact]
    public async Task 현장지급예정의미배정초안도_화물수량수정을유지한다()
    {
        await using var db = Context();
        await Seed(db);
        var entity = await db.화주운송의뢰.SingleAsync();
        entity.정산상태 = "현장수금예정";
        db.운송원장.Add(new() { 의뢰Id = "quote-request", 운송번호 = "quote-request", 상태 = "대기" });
        await db.SaveChangesAsync();

        Assert.True((await Handler(db, Sync()).Handle(CoreChange("quantity"), default)).IsSuccess);
        Assert.Equal(7, entity.화물수량);
        Assert.Equal("현장수금예정", entity.정산상태);
        Assert.Equal("대기", (await db.운송원장.SingleAsync()).상태);
    }

    [Fact]
    public async Task 배차후같은핵심조건재전송과연락처수정은_다시합의한것으로오인하지않는다()
    {
        await using var db = Context();
        await Seed(db);
        var entity = await db.화주운송의뢰.SingleAsync();
        entity.배차상태 = "배차확정";
        entity.정산상태 = "후불승인완료";
        entity.화물수량 = 7;
        entity.화물중량Kg = 15m;
        entity.화물온도조건 = "냉장";
        entity.픽업_시간창_시작일시 = new DateTime(2030, 1, 1, 9, 0, 0);
        entity.픽업_시간창_종료일시 = new DateTime(2030, 1, 1, 10, 0, 0);
        await db.SaveChangesAsync();
        var request = Command(null) with
        {
            화물정보 = new(null, null, 7, 15m, null, null, "냉장"),
            픽업지 = new(null, null, null, null, "새 연락처", "010-1234-5678",
                entity.픽업_시간창_시작일시, entity.픽업_시간창_종료일시)
        };

        var result = await Handler(db, Sync()).Handle(request, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("새 연락처", entity.픽업_연락처_이름);
        Assert.Equal("배차확정", entity.배차상태);
        Assert.Equal("후불승인완료", entity.정산상태);
        Assert.Equal(7, entity.화물수량);
    }

    private static 의뢰수정Command CoreChange(string field)
        => field switch
        {
            "quantity" => Command(null) with { 화물정보 = new(null, null, 7, null, null, null, null) },
            "weight" => Command(null) with { 화물정보 = new(null, null, null, 15m, null, null, null) },
            "temperature" => Command(null) with { 화물정보 = new(null, null, null, null, null, null, "냉장") },
            "pickupWindow" => Command(null) with { 픽업지 = new(null, null, null, null, null, null, new DateTime(2030, 1, 1, 9, 0, 0), new DateTime(2030, 1, 1, 10, 0, 0)) },
            "dropoffWindow" => Command(null) with { 하차지 = new(null, null, null, null, null, null, new DateTime(2030, 1, 1, 11, 0, 0), new DateTime(2030, 1, 1, 12, 0, 0)) },
            "route" => Command(null) with { 하차지 = new("새 합성 하차지", null, null, null, null, null, null, null) },
            "transport" => Command(null) with { 운송조건 = new("예약운송", null, null) },
            "settlement" => Command(null) with { 정산조건 = new("현금", null) },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

    private static 의뢰수정Command Command(PricingDTO? pricing)
        => new("quote-request", new(null, null, null), new(null, null, null, null, null, null, null),
            new(null, null, null, null, null, null, null, null), new(null, null, null, null, null, null, null, null), new(null), null, pricing);
    private static 의뢰수정CommandHandler Handler(SsalddelContext db, RecordingSync sync, string user = "shipper-a")
        => new(db, new 화주운송업무담당자UseCase(db, new Current(user, "화주"), TimeProvider.System),
            new 화주운송기준운임Service(db, null!), sync, new Current(user, "화주"));
    private static RecordingSync Sync() => new();
    private sealed class RecordingSync : I운송원장Mongo동기화Service
    {
        public List<(decimal? Fare, string User)> Calls { get; } = [];
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 request, string user, CancellationToken cancellationToken = default)
        {
            Calls.Add((request.최종운임, user));
            return Task.FromResult<커뮤니티원장Dto?>(new());
        }
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 request, string user, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
    private sealed class RecordingSender : ISender
    {
        public 의뢰수정Command? Command { get; private set; }
        public 의뢰생성Command? CreateCommand { get; private set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is 의뢰생성Command create) CreateCommand = create;
            else Command = Assert.IsType<의뢰수정Command>(request);
            return Task.FromResult((TResponse)(object)Result.Ok(new 화주운송의뢰응답()));
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private static async Task Seed(SsalddelContext db)
    {
        db.화주운송의뢰.Add(new() { 의뢰Id = "quote-request", 화주Id = "shipper-a", 주문자UserId = "shipper-a",
            차량종류 = "1톤 카고", 상태 = "생성됨", 결제상태 = "결제대기", 배차상태 = "미시작", 정산상태 = "청구대기",
            픽업_도로명주소 = "합성 상차지", 하차_도로명주소 = "합성 하차지" });
        await db.SaveChangesAsync();
    }
    private static SsalddelContext Context()
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase($"cargo-quote-{Guid.NewGuid():N}").Options, new Encryption());
    private sealed record Current(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
