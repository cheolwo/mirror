using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

public sealed class 운송원장RdbSnapshotProjectionTests
{
    [Fact]
    public void Snapshot_digest_survives_mongo_dictionary_and_state_normalization()
    {
        var save = 운송원장Mongo동기화Builder.저장요청생성(CreateRequest(35000m, "이전 주소"), null);
        save.원장Id = $" {save.원장Id} ";
        save.블록목록[0].BlockId = $" {save.블록목록[0].BlockId} ";
        save.블록목록[0].State = $" {save.블록목록[0].State} ";
        var data = save.블록목록[0].Data.ToDictionary(x => $" {x.Key} ", x => $" {x.Value} ");
        data["empty"] = " ";
        save.블록목록[0].Data = data;
        운송원장RdbSnapshotMarker.Attach(save, "REQ-MIRROR");
        var ledger = new 커뮤니티원장Dto
        {
            원장Id = save.원장Id!.Trim(), 원장템플릿Key = save.원장템플릿Key,
            블록목록 = save.블록목록.Select(x => new 커뮤니티원장블록Dto
            {
                BlockId = x.BlockId.Trim(), State = string.IsNullOrWhiteSpace(x.State) ? null : x.State.Trim(),
                Data = x.Data.Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value))
                    .ToDictionary(p => p.Key.Trim(), p => p.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            }).ToArray(),
            외부참조 = save.외부참조, 확장속성 = save.확장속성
        };

        Assert.True(운송원장RdbSnapshotMarker.Matches(ledger, "REQ-MIRROR"));
    }

    [Fact]
    public void Delayed_server_mirror_preserves_newer_price_approval_and_route()
    {
        var stale = CreateRequest(35000m, "이전 주소");
        var ledger = Mirror(stale);
        var snapshot = 운송원장업무투영Snapshot.생성(ledger)!;
        var current = CreateRequest(47000m, "변경 주소");
        current.정산상태 = "후불승인완료";
        current.결제상태 = 상태값.결제상태.결제완료;
        current.배차상태 = 상태값.배차상태.배차확정;
        var transport = new 운송원장
        {
            의뢰Id = current.의뢰Id, 운송번호 = current.의뢰Id, 운임 = current.최종운임,
            픽업_도로명주소 = current.픽업_도로명주소, 확정기사Id = "driver-current", 상태 = "상차완료"
        };

        Assert.True(snapshot.IsPersistedRdbSnapshot);
        Assert.True(운송원장업무투영Handler.ApplyTransportProjection(transport, snapshot, isNew: false));
        Assert.False(운송원장업무투영Handler.ApplyShipperRequest(current, snapshot));

        Assert.Equal(47000m, transport.운임);
        Assert.Equal("변경 주소", transport.픽업_도로명주소);
        Assert.Equal("driver-current", transport.확정기사Id);
        Assert.Equal("상차완료", transport.상태);
        Assert.Equal(ledger.원장Id, transport.커뮤니티원장Id);
        Assert.NotNull(transport.커뮤니티원장동기화시각Utc);
        Assert.Equal(47000m, current.최종운임);
        Assert.Equal(47000, current.결제예정금액);
        Assert.Equal("후불승인완료", current.정산상태);
        Assert.Equal(상태값.결제상태.결제완료, current.결제상태);
        Assert.Equal(상태값.배차상태.배차확정, current.배차상태);
        Assert.Equal("변경 주소", current.픽업_도로명주소);
    }

    [Fact]
    public async Task Delayed_mirror_initializes_missing_transport_from_current_saved_request()
    {
        await using var db = CreateContext();
        var current = CreateRequest(47000m, "변경 주소");
        current.정산상태 = "후불승인완료";
        db.화주운송의뢰.Add(current);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var handler = new 운송원장업무투영Handler(db, NullLogger<운송원장업무투영Handler>.Instance);

        await handler.동기화Async(Mirror(CreateRequest(35000m, "이전 주소")));

        var transport = await db.운송원장.AsNoTracking().SingleAsync();
        var request = await db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(47000m, transport.운임);
        Assert.Equal("변경 주소", transport.픽업_도로명주소);
        Assert.Equal(47000m, request.최종운임);
        Assert.Equal("후불승인완료", request.정산상태);
        Assert.Equal("transport:REQ-MIRROR", transport.커뮤니티원장Id);
    }

    [Fact]
    public async Task Delayed_mirror_cannot_recreate_a_deleted_request_or_transport()
    {
        await using var db = CreateContext();
        var handler = new 운송원장업무투영Handler(db, NullLogger<운송원장업무투영Handler>.Instance);

        await handler.동기화Async(Mirror(CreateRequest(35000m, "이전 주소")));

        Assert.Empty(await db.화주운송의뢰.ToListAsync());
        Assert.Empty(await db.운송원장.ToListAsync());
    }

    [Fact]
    public void Community_business_block_edit_with_copied_marker_retains_existing_projection()
    {
        var ledger = Mirror(CreateRequest(35000m, "이전 주소"));
        var settlement = ledger.블록목록.Single(x => x.BlockId == "settlement");
        var data = settlement.Data.ToDictionary(x => x.Key, x => x.Value);
        data["최종운임"] = "48000";
        settlement.Data = data;
        var snapshot = 운송원장업무투영Snapshot.생성(ledger)!;
        var current = CreateRequest(35000m, "이전 주소");

        Assert.False(snapshot.IsPersistedRdbSnapshot);
        Assert.True(운송원장업무투영Handler.ApplyShipperRequest(current, snapshot));
        Assert.Equal(48000m, current.최종운임);
    }

    private static 커뮤니티원장Dto Mirror(화주운송의뢰 request)
    {
        var save = 운송원장Mongo동기화Builder.저장요청생성(request, null);
        운송원장RdbSnapshotMarker.Attach(save, request.의뢰Id);
        return new 커뮤니티원장Dto
        {
            원장Id = save.원장Id!, 원장템플릿Key = save.원장템플릿Key,
            상태 = save.상태!, 현재단계Key = save.현재단계Key,
            블록목록 = save.블록목록, 참여자목록 = save.참여자목록,
            외부참조 = save.외부참조, 확장속성 = save.확장속성
        };
    }

    private static 화주운송의뢰 CreateRequest(decimal fare, string pickupAddress)
        => new()
        {
            의뢰Id = "REQ-MIRROR", 화주Id = "shipper-mirror", 주문자UserId = "shipper-mirror",
            화물종류 = "검증 화물", 차량종류 = "1톤 카고",
            픽업_도로명주소 = pickupAddress, 픽업_연락처_전화번호 = "010-0000-0001",
            픽업_시간창_시작일시 = new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc),
            픽업_시간창_종료일시 = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
            하차_도로명주소 = "검증 하차 주소", 최종운임 = fare, 결제예정금액 = decimal.ToInt32(fare),
            정산상태 = "후불승인대기", 결제상태 = 상태값.결제상태.결제대기,
            배차상태 = 상태값.배차상태.미시작, 상태 = 상태값.의뢰상태.생성됨
        };

    private static SsalddelContext CreateContext()
        => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new PassThroughEncryption());

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
