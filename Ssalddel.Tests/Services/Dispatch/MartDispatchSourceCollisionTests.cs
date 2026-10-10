using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch;

public sealed class MartDispatchSourceCollisionTests
{
    private const string RequestId = "MART-COLLISION-SAMPLE";

    [Theory]
    [InlineData("existing-source-type")]
    [InlineData("incoming-source-type")]
    [InlineData("source-id")]
    [InlineData("source-id-case")]
    [InlineData("shipper")]
    [InlineData("shipper-case")]
    [InlineData("existing-work-type")]
    [InlineData("incoming-work-type")]
    public async Task 마트원천충돌은_기존확정업무와주소를보존하고투영전에거절한다(string conflict)
    {
        await using var db = CreateContext();
        var original = Existing();
        if (conflict == "existing-source-type") original.원본의뢰유형 = 운송의뢰배차원천유형.음식주문;
        if (conflict == "source-id") original.원본의뢰Id = "OTHER-SOURCE";
        if (conflict == "source-id-case") original.원본의뢰Id = RequestId.ToLowerInvariant();
        if (conflict == "shipper") original.화주Id = "OTHER-SHIPPER";
        if (conflict == "shipper-case") original.화주Id = "seller-sample";
        if (conflict == "existing-work-type") original.배차업무유형 = 상태값.배차업무유형.용달운송;
        db.운송원장.Add(original);
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(original);
        var bridge = new MustNotProject();
        var food = new InMemory음식배달권실행공간Store();
        var cargo = new InMemory국내화물배달권실행공간Store();
        var service = new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(), bridge, food, cargo);
        var options = new 운송의뢰배차대기생성옵션
        {
            의뢰Id = RequestId, 화주Id = "SELLER-SAMPLE", 원본의뢰Id = RequestId,
            원본의뢰유형 = conflict == "incoming-source-type" ? 운송의뢰배차원천유형.음식주문 : 운송의뢰배차원천유형.살뜰마트포장완료주문,
            배차업무유형 = conflict == "incoming-work-type" ? 상태값.배차업무유형.용달운송 : 상태값.배차업무유형.음식배달
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.생성또는조회Async(Target(), options));

        Assert.Equal("마트 배차 원천 결속이 일치하지 않아 기존 업무를 변경하지 않았습니다.", error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(original));
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(0, bridge.Calls);
        Assert.Single(await db.운송원장.ToArrayAsync());
        Assert.Empty(await db.원장배달권투영.ToArrayAsync());
        Assert.Empty(await food.SnapshotAsync());
        Assert.Empty(await cargo.SnapshotAsync());
    }

    [Fact]
    public async Task 신규마트원천의다른업무유형도_저장이나투영전에거절한다()
    {
        await using var db = CreateContext();
        var bridge = new MustNotProject();
        var service = new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(), bridge,
            new InMemory음식배달권실행공간Store(), new InMemory국내화물배달권실행공간Store());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.생성또는조회Async(Target(), new()
        {
            원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트포장완료주문,
            배차업무유형 = 상태값.배차업무유형.용달운송
        }));

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(await db.운송원장.ToArrayAsync());
        Assert.Equal(0, bridge.Calls);
    }

    [Fact]
    public async Task 같은마트원천의재조회는_확정기사와완료상태를되돌리지않는다()
    {
        await using var db = CreateContext();
        var original = Existing();
        db.운송원장.Add(original);
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(original);
        var food = new InMemory음식배달권실행공간Store();
        var cargo = new InMemory국내화물배달권실행공간Store();
        var service = new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(),
            new 운송원장배달권연결Service(new 원장배달권투영Service(db)), food, cargo);
        var options = new 운송의뢰배차대기생성옵션
        {
            원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트포장완료주문,
            원본의뢰Id = RequestId, 화주Id = "SELLER-SAMPLE", 배차업무유형 = 상태값.배차업무유형.음식배달
        };

        var result = await service.생성또는조회Async(Target(), options);
        await db.SaveChangesAsync();
        var replay = await service.생성또는조회Async(Target(), options);

        Assert.Same(original, result);
        Assert.Same(result, replay);
        Assert.Equal(before, JsonSerializer.Serialize(original));
        Assert.Single(await db.운송원장.ToArrayAsync());
        Assert.Empty(await food.SnapshotAsync());
        Assert.Empty(await cargo.SnapshotAsync());
    }

    private static 출고예정운송대상 Target() => new()
    {
        원천참조번호 = RequestId, 운송의뢰Id = RequestId, 판매자UserId = "SELLER-SAMPLE",
        상차주소 = "새 요청 주소", 하차주소 = "새 요청 전달지"
    };

    private static 운송원장 Existing() => new()
    {
        의뢰Id = RequestId, 운송번호 = RequestId,
        원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트포장완료주문, 원본의뢰Id = RequestId,
        화주Id = "SELLER-SAMPLE", 배차업무유형 = 상태값.배차업무유형.음식배달,
        확정기사Id = "DRIVER-SAMPLE", 상태 = "완료", 배차큐단계 = 상태값.배차큐단계.종료,
        픽업_도로명주소 = "기존 픽업 주소", 하차_도로명주소 = "기존 전달 주소",
        CreatedAt = new(2026, 10, 10, 1, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new(2026, 10, 10, 2, 0, 0, DateTimeKind.Utc)
    };

    private static SsalddelContext CreateContext() => new(
        new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase($"mart-collision-{Guid.NewGuid():N}").Options,
        new PassthroughEncryption());

    private sealed class PassthroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class MustNotProject : I운송원장배달권연결Service
    {
        public int Calls { get; private set; }
        public Task<운송원장배달권연결결과> 투영추적Async(운송원장 운송원장, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("충돌 업무의 배달권 투영에 도달하면 안 됩니다.");
        }
    }
}
