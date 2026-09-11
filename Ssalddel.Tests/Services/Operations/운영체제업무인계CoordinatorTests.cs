using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Operations;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 운영체제업무인계CoordinatorTests
{
    private static readonly DateTimeOffset 기준시각 =
        new(2026, 9, 11, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 인계요청은_수락전까지_출발OS책임과_Outbox를_유지한다()
    {
        await using var db = CreateContext();
        var service = new 운영체제업무인계Coordinator(db, new MutableTimeProvider(기준시각));
        var request = CreateRequest();

        var result = await service.요청Async(request);

        Assert.Equal(운영체제업무인계상태Codes.요청됨, result.상태Code);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, result.현재책임운영체제Id);
        Assert.Equal(1, result.Revision);
        var outbox = Assert.Single(await db.운영체제업무인계Outbox.AsNoTracking().ToListAsync());
        Assert.Equal("OperatingSystemHandoffRequested", outbox.이벤트Type);
        Assert.DoesNotContain("customerPhone", outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 같은_생성요청은_멱등하게_같은원장을_반환한다()
    {
        await using var db = CreateContext();
        var service = new 운영체제업무인계Coordinator(db, new MutableTimeProvider(기준시각));
        var request = CreateRequest();

        var first = await service.요청Async(request);
        var second = await service.요청Async(request);

        Assert.Equal(first.인계StableId, second.인계StableId);
        Assert.Single(await db.운영체제업무인계.ToListAsync());
        Assert.Single(await db.운영체제업무인계Outbox.ToListAsync());
    }

    [Fact]
    public async Task 도착OS가_수락해야만_책임이_도착OS로_이동한다()
    {
        await using var db = CreateContext();
        var service = new 운영체제업무인계Coordinator(db, new MutableTimeProvider(기준시각));
        var created = await service.요청Async(CreateRequest());

        var accepted = await service.결정Async(
            created.인계StableId,
            new 운영체제업무인계결정요청
            {
                클라이언트요청Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                예상Revision = created.Revision,
                응답운영체제Id = OperatingSystemIds.FoodDelivery,
                결정Code = 운영체제업무인계결정Codes.수락,
                도착업무StableId = "food-delivery:order:FOOD-42"
            });

        Assert.Equal(운영체제업무인계상태Codes.수락됨, accepted.상태Code);
        Assert.Equal(OperatingSystemIds.FoodDelivery, accepted.현재책임운영체제Id);
        Assert.Equal(2, accepted.Revision);
        Assert.Equal(2, await db.운영체제업무인계Outbox.CountAsync());
        Assert.Contains(await db.운영체제업무인계Outbox.ToListAsync(), item =>
            item.이벤트Type == "OperatingSystemHandoffAccepted");
    }

    [Theory]
    [InlineData(운영체제업무인계결정Codes.거절, 운영체제업무인계상태Codes.거절됨)]
    [InlineData(운영체제업무인계결정Codes.보류, 운영체제업무인계상태Codes.보류됨)]
    public async Task 거절과_보류는_출발OS책임을_바꾸지_않는다(string decision, string expectedStatus)
    {
        await using var db = CreateContext();
        var service = new 운영체제업무인계Coordinator(db, new MutableTimeProvider(기준시각));
        var created = await service.요청Async(CreateRequest());

        var result = await service.결정Async(
            created.인계StableId,
            new 운영체제업무인계결정요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = created.Revision,
                응답운영체제Id = OperatingSystemIds.FoodDelivery,
                결정Code = decision,
                사유Code = "TargetNotReady"
            });

        Assert.Equal(expectedStatus, result.상태Code);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, result.현재책임운영체제Id);
    }

    [Fact]
    public async Task 만료된_인계는_수락하지_않고_출발OS책임을_유지한다()
    {
        await using var db = CreateContext();
        var time = new MutableTimeProvider(기준시각);
        var service = new 운영체제업무인계Coordinator(db, time);
        var created = await service.요청Async(CreateRequest(expiresAt: 기준시각.AddMinutes(5).UtcDateTime));
        time.Now = 기준시각.AddMinutes(6);

        var result = await service.결정Async(
            created.인계StableId,
            new 운영체제업무인계결정요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = created.Revision,
                응답운영체제Id = OperatingSystemIds.FoodDelivery,
                결정Code = 운영체제업무인계결정Codes.수락,
                도착업무StableId = "food-delivery:order:FOOD-42"
            });

        Assert.Equal(운영체제업무인계상태Codes.만료됨, result.상태Code);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, result.현재책임운영체제Id);
        Assert.Contains(await db.운영체제업무인계Outbox.ToListAsync(), item =>
            item.이벤트Type == "OperatingSystemHandoffExpired");
    }

    [Fact]
    public async Task 같은_생성요청Id를_다른_내용으로_재사용하면_거부한다()
    {
        await using var db = CreateContext();
        var service = new 운영체제업무인계Coordinator(db, new MutableTimeProvider(기준시각));
        var request = CreateRequest();
        await service.요청Async(request);

        var changed = CreateRequest(sourceRevision: 8);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.요청Async(changed));
    }

    [Fact]
    public async Task 도착OS가_아닌_OS의_결정은_거부한다()
    {
        await using var db = CreateContext();
        var service = new 운영체제업무인계Coordinator(db, new MutableTimeProvider(기준시각));
        var created = await service.요청Async(CreateRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.결정Async(
            created.인계StableId,
            new 운영체제업무인계결정요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = created.Revision,
                응답운영체제Id = OperatingSystemIds.WarehouseCommerceFulfillment,
                결정Code = 운영체제업무인계결정Codes.수락,
                도착업무StableId = "food-delivery:order:FOOD-42"
            }));
    }

    private static 운영체제업무인계생성요청 CreateRequest(
        long sourceRevision = 7,
        DateTime? expiresAt = null)
        => new()
        {
            클라이언트요청Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            출발운영체제Id = OperatingSystemIds.DomesticCargoTransport,
            도착운영체제Id = OperatingSystemIds.FoodDelivery,
            출발업무유형Code = "CargoTransport",
            출발업무StableId = "cargo:transport:CARGO-42",
            출발업무Revision = sourceRevision,
            인계계약Code = "CargoToFoodDeliveryHandoff",
            인계계약Revision = "v1",
            최소상태사본Json = "{\"pickupZone\":\"SEOUL-JONGNO\",\"cargoId\":\"CARGO-42\"}",
            만료시각Utc = expiresAt ?? 기준시각.AddMinutes(10).UtcDateTime
        };

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"operating-system-handoff-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
