using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Operations;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 화주운송의뢰화물운송인계ServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 확정된화주의뢰는_최소상태사본으로화물운송Os에멱등인계된다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        await db.SaveChangesAsync();
        var timeProvider = new FixedTimeProvider(Now);
        var coordinator = new 운영체제업무인계Coordinator(db, timeProvider);
        var service = new 화주운송의뢰화물운송인계Service(db, coordinator, timeProvider);

        var result = await service.인계Async("request-101");
        var replay = await service.인계Async("request-101");

        Assert.Equal(운영체제업무인계상태Codes.수락됨, result.상태Code);
        Assert.Equal(OperatingSystemIds.ShipperTransportManagement, result.출발운영체제Id);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, result.도착운영체제Id);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, result.현재책임운영체제Id);
        Assert.Equal("shipper-transport-request:request-101", result.출발업무StableId);
        Assert.Equal("cargo-request:request-101", result.도착업무StableId);
        Assert.Equal(result.인계StableId, replay.인계StableId);
        Assert.Equal(result.Revision, replay.Revision);
        Assert.Single(await db.운영체제업무인계.ToListAsync());
        Assert.Equal(2, await db.운영체제업무인계Outbox.CountAsync());
        Assert.Equal(
            상태값.배차상태.미시작,
            (await db.화주운송의뢰.AsNoTracking().SingleAsync()).배차상태);

        var stored = await db.운영체제업무인계.AsNoTracking().SingleAsync();
        using var snapshot = JsonDocument.Parse(stored.최소상태사본Json);
        Assert.Equal("냉장식품", snapshot.RootElement.GetProperty("cargoType").GetString());
        Assert.Contains(OperatingSystemLifecycleStageIds.ShipperTransportHandoff, stored.최소상태사본Json, StringComparison.Ordinal);
        Assert.DoesNotContain("서울 중랑구", stored.최소상태사본Json, StringComparison.Ordinal);
        Assert.DoesNotContain("010-", stored.최소상태사본Json, StringComparison.Ordinal);
        Assert.DoesNotContain("shipper-101", stored.최소상태사본Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 저장되지않은화주의뢰는_인계원장을만들지않는다()
    {
        await using var db = CreateContext();
        var timeProvider = new FixedTimeProvider(Now);
        var coordinator = new 운영체제업무인계Coordinator(db, timeProvider);
        var service = new 화주운송의뢰화물운송인계Service(db, coordinator, timeProvider);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.인계Async("missing-request"));

        Assert.Empty(await db.운영체제업무인계.ToListAsync());
        Assert.Empty(await db.운영체제업무인계Outbox.ToListAsync());
    }

    private static 화주운송의뢰 CreateRequest()
        => new()
        {
            의뢰Id = "request-101",
            화주Id = "shipper-101",
            주문자UserId = "shipper-101",
            화물종류 = "냉장식품",
            화물수량 = 12,
            화물중량Kg = 240m,
            화물부피Cbm = 1.3m,
            화물팔레트개수 = 1,
            화물온도조건 = "냉장",
            운송방식 = "단독",
            차량종류 = "1톤 냉장탑차",
            픽업_도로명주소 = "서울 중랑구 출발로 1",
            픽업_연락처_전화번호 = "010-1111-2222",
            픽업_시간창_시작일시 = Now.AddHours(1).UtcDateTime,
            픽업_시간창_종료일시 = Now.AddHours(2).UtcDateTime,
            하차_도로명주소 = "서울 중랑구 도착로 2",
            하차_연락처_전화번호 = "010-3333-4444",
            하차_시간창_시작일시 = Now.AddHours(3).UtcDateTime,
            하차_시간창_종료일시 = Now.AddHours(4).UtcDateTime,
            최종운임 = 45_000m,
            상태 = 상태값.의뢰상태.생성됨,
            배차상태 = 상태값.배차상태.미시작,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"shipper-cargo-handoff-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryption());

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
