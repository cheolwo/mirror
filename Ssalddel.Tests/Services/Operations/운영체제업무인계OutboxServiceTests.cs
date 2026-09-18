using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Operations;
using 살뜰.도메인.운영;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 운영체제업무인계OutboxServiceTests
{
    [Fact]
    public void 자동전달Worker는_명시적으로활성화하기전까지꺼져있다()
    {
        var options = new 운영체제업무인계OutboxOptions();

        Assert.False(options.Enabled);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(5, options.IntervalSeconds);
    }

    private static readonly DateTimeOffset 기준시각 =
        new(2026, 9, 17, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 대기항목은_내부Event로한번전달하고_완료한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var publisher = new RecordingPublisher();
        var service = CreateService(db, publisher, new MutableTimeProvider(기준시각));

        var first = await service.대기항목처리Async();
        var second = await service.대기항목처리Async();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var delivered = Assert.Single(publisher.Events);
        Assert.Equal("handoff:100", delivered.인계StableId);
        Assert.Equal("handoff-outbox-key", delivered.멱등Key);
        Assert.Equal("WarehouseCommerceFulfillmentOS", delivered.출발운영체제Id);
        Assert.Equal("DomesticCargoTransportOS", delivered.도착운영체제Id);
        var outbox = await db.운영체제업무인계Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(운영체제업무인계Outbox상태Codes.완료, outbox.처리상태Code);
        Assert.Equal(1, outbox.처리시도수);
        Assert.Null(outbox.다음처리시각Utc);
    }

    [Fact]
    public async Task 전달실패는_지연후재시도하고_같은멱등키로완료한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var publisher = new RecordingPublisher { FailuresRemaining = 1 };
        var time = new MutableTimeProvider(기준시각);
        var service = CreateService(db, publisher, time);

        Assert.Equal(1, await service.대기항목처리Async());
        var waiting = await db.운영체제업무인계Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(운영체제업무인계Outbox상태Codes.재시도대기, waiting.처리상태Code);
        Assert.Equal(1, waiting.처리시도수);
        Assert.Equal(기준시각.UtcDateTime + OutboxProcessingPolicy.RetryDelay, waiting.다음처리시각Utc);
        Assert.Equal(0, await service.대기항목처리Async());

        time.Now = 기준시각 + OutboxProcessingPolicy.RetryDelay + TimeSpan.FromSeconds(1);
        Assert.Equal(1, await service.대기항목처리Async());

        var completed = await db.운영체제업무인계Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(운영체제업무인계Outbox상태Codes.완료, completed.처리상태Code);
        Assert.Equal(2, completed.처리시도수);
        Assert.Equal("handoff-outbox-key", Assert.Single(publisher.Events).멱등Key);
    }

    [Fact]
    public async Task 자동재시도한도를넘으면_실패로보호하고_운영자가다시예약할수있다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, attemptCount: OutboxProcessingPolicy.MaximumAttempts - 1);
        var publisher = new RecordingPublisher { FailuresRemaining = 1 };
        var time = new MutableTimeProvider(기준시각);
        var service = CreateService(db, publisher, time);

        Assert.Equal(1, await service.대기항목처리Async());
        var failed = await db.운영체제업무인계Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(운영체제업무인계Outbox상태Codes.실패, failed.처리상태Code);
        Assert.Equal(OutboxProcessingPolicy.MaximumAttempts, failed.처리시도수);
        Assert.Null(failed.다음처리시각Utc);

        Assert.True(await service.실패항목재시도예약Async(failed.Id, failed.처리시도수));
        var rescheduled = await db.운영체제업무인계Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(운영체제업무인계Outbox상태Codes.재시도대기, rescheduled.처리상태Code);
        Assert.Equal(기준시각.UtcDateTime, rescheduled.다음처리시각Utc);

        Assert.Equal(1, await service.대기항목처리Async());
        var completed = await db.운영체제업무인계Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(운영체제업무인계Outbox상태Codes.완료, completed.처리상태Code);
        Assert.Equal(OutboxProcessingPolicy.MaximumAttempts + 1, completed.처리시도수);
    }

    [Fact]
    public async Task 인계원장이이미수락되어도_요청과수락Event의각판본을보존한다()
    {
        await using var db = CreateContext();
        var time = new MutableTimeProvider(기준시각);
        var coordinator = new 운영체제업무인계Coordinator(db, time);
        var created = await coordinator.요청Async(new 운영체제업무인계생성요청
        {
            클라이언트요청Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            출발운영체제Id = OperatingSystemIds.WarehouseCommerceFulfillment,
            도착운영체제Id = OperatingSystemIds.DomesticCargoTransport,
            출발업무유형Code = "WarehouseOutboundPlan",
            출발업무StableId = "warehouse-outbound:100",
            출발업무Revision = 1,
            인계계약Code = OperatingSystemInteractionContractCodes.WarehouseOutboundToCargoTransport,
            인계계약Revision = OperatingSystemInteractionContractRevisions.WarehouseOutboundToCargoTransport,
            최소상태사본Json = "{}",
            만료시각Utc = 기준시각.AddHours(1).UtcDateTime
        });
        await coordinator.결정Async(created.인계StableId, new 운영체제업무인계결정요청
        {
            클라이언트요청Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            예상Revision = created.Revision,
            응답운영체제Id = OperatingSystemIds.DomesticCargoTransport,
            결정Code = 운영체제업무인계결정Codes.수락,
            도착업무StableId = "cargo-request:100"
        });
        var publisher = new RecordingPublisher();

        Assert.Equal(2, await CreateService(db, publisher, time).대기항목처리Async());

        Assert.Collection(
            publisher.Events.OrderBy(item => item.OutboxId),
            requested =>
            {
                Assert.Equal("OperatingSystemHandoffRequested", requested.이벤트Type);
                Assert.Equal(1, requested.인계Revision);
                Assert.Equal(OperatingSystemIds.WarehouseCommerceFulfillment, requested.현재책임운영체제Id);
            },
            accepted =>
            {
                Assert.Equal("OperatingSystemHandoffAccepted", accepted.이벤트Type);
                Assert.Equal(2, accepted.인계Revision);
                Assert.Equal(OperatingSystemIds.DomesticCargoTransport, accepted.현재책임운영체제Id);
            });
    }

    private static 운영체제업무인계OutboxService CreateService(
        SsalddelContext db,
        I운영체제업무인계OutboxPublisher publisher,
        TimeProvider timeProvider)
        => new(
            db,
            publisher,
            timeProvider,
            NullLogger<운영체제업무인계OutboxService>.Instance);

    private static async Task SeedAsync(SsalddelContext db, int attemptCount = 0)
    {
        db.운영체제업무인계.Add(new 운영체제업무인계
        {
            인계StableId = "handoff:100",
            생성멱등Key = "handoff-key",
            출발운영체제Id = "WarehouseCommerceFulfillmentOS",
            도착운영체제Id = "DomesticCargoTransportOS",
            현재책임운영체제Id = "DomesticCargoTransportOS",
            출발업무유형Code = "WarehouseOutboundPlan",
            출발업무StableId = "warehouse-outbound:100",
            인계계약Code = "WarehouseOutboundToCargoTransport",
            인계계약Revision = "v1",
            최소상태사본Json = "{}",
            공개범위Code = "MinimumRequiredBusinessData",
            상태Code = "Accepted",
            Revision = 2,
            요청시각Utc = 기준시각.AddMinutes(-1).UtcDateTime,
            만료시각Utc = 기준시각.AddHours(1).UtcDateTime,
            CreatedAt = 기준시각.AddMinutes(-1).UtcDateTime,
            UpdatedAt = 기준시각.AddMinutes(-1).UtcDateTime
        });
        db.운영체제업무인계Outbox.Add(new 운영체제업무인계Outbox
        {
            멱등Key = "handoff-outbox-key",
            인계StableId = "handoff:100",
            이벤트Type = "OperatingSystemHandoffAccepted",
            PayloadJson = "{\"인계StableId\":\"handoff:100\",\"출발운영체제Id\":\"WarehouseCommerceFulfillmentOS\",\"도착운영체제Id\":\"DomesticCargoTransportOS\",\"현재책임운영체제Id\":\"DomesticCargoTransportOS\",\"Revision\":2}",
            처리상태Code = 운영체제업무인계Outbox상태Codes.대기,
            처리시도수 = attemptCount,
            CreatedAt = 기준시각.AddMinutes(-1).UtcDateTime,
            UpdatedAt = 기준시각.AddMinutes(-1).UtcDateTime
        });
        await db.SaveChangesAsync();
    }

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"operating-system-handoff-outbox-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryption());

    private sealed class RecordingPublisher : I운영체제업무인계OutboxPublisher
    {
        public int FailuresRemaining { get; set; }
        public List<운영체제업무인계Outbox전달됨Event> Events { get; } = [];

        public Task 발행Async(
            운영체제업무인계Outbox전달됨Event notification,
            CancellationToken cancellationToken = default)
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("simulated publish failure");
            }

            Events.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
