using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class 생활권물류거점ServiceTests
{
    [Fact]
    public async Task 세가지승인뒤_SimulationPilot은_임시보관소를_한번만_만든다()
    {
        await using var db = CreateContext();
        var service = CreateService(db, "owner", SsalddelExecutionMode.Simulation);
        var hub = await service.신청Async(Request("manager"), default);
        hub = await service.소유자동의Async(hub.Id, new() { 동의 = true, ExpectedRevision = hub.Revision }, default);
        service = CreateService(db, "manager", SsalddelExecutionMode.Simulation);
        hub = await service.관리주체동의Async(hub.Id, new() { 동의 = true, ExpectedRevision = hub.Revision }, default);
        hub = await service.관리자검토Async(hub.Id, new() { 현장확인 = true, 플랫폼승인 = true, ExpectedRevision = hub.Revision }, "admin", default);
        hub = await service.상태변경Async(hub.Id, new() { 상태Code = "UnderReview", ExpectedRevision = hub.Revision }, "admin", default);
        hub = await service.상태변경Async(hub.Id, new() { 상태Code = "Pilot", ExpectedRevision = hub.Revision }, "admin", default);

        Assert.NotNull(hub.연결창고Id);
        var warehouse = await db.창고.SingleAsync();
        Assert.Equal("임시보관소", warehouse.창고유형);
        Assert.Equal("protected-place:1", warehouse.주소);
        Assert.False(hub.실운영허용);
    }

    [Fact]
    public async Task Operational모드는_준비완료여도_Pilot을_차단한다()
    {
        await using var db = CreateContext();
        var service = CreateService(db, "owner", SsalddelExecutionMode.Operational);
        var hub = await ReadyForReviewAsync(db, service);
        hub = await service.상태변경Async(hub.Id, new() { 상태Code = "UnderReview", ExpectedRevision = hub.Revision }, "admin", default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.상태변경Async(
            hub.Id, new() { 상태Code = "Pilot", ExpectedRevision = hub.Revision }, "admin", default));

        Assert.Contains("운영·법률·정산", exception.Message);
        Assert.Empty(db.창고);
    }

    [Fact]
    public async Task 동의철회는_신규배정을막기위해_가동거점을_Paused로_전환한다()
    {
        await using var db = CreateContext();
        var service = CreateService(db, "owner", SsalddelExecutionMode.Simulation);
        var hub = await ReadyPilotAsync(db, service);

        hub = await service.소유자동의Async(hub.Id, new() { 동의 = false, ExpectedRevision = hub.Revision }, default);

        Assert.Equal("Paused", hub.상태Code);
        Assert.Equal("ConsentWithdrawn", hub.상태사유);
    }

    [Fact]
    public async Task 예약과완료는_멱등하며_실제지급이아닌_모의보상만_기록한다()
    {
        await using var db = CreateContext();
        var service = CreateService(db, "owner", SsalddelExecutionMode.Simulation);
        var hub = await ReadyPilotAsync(db, service);
        var request = new 생활권물류거점예약Request { 업무StableId = "handoff:1", 멱등성Key = "reserve-1", 중량Kg = 2, 보관시간분 = 30 };

        var first = await service.예약Async(hub.Id, request, default);
        var duplicate = await service.예약Async(hub.Id, request, default);
        var completed = await service.인계완료Async(hub.Id, first.예약Id, "complete-1", default);
        var duplicateCompletion = await service.인계완료Async(hub.Id, first.예약Id, "complete-1", default);

        Assert.False(first.기존예약재사용);
        Assert.True(duplicate.기존예약재사용);
        Assert.Equal(700m, completed.모의보상금액);
        Assert.Equal(completed.모의보상금액, duplicateCompletion.모의보상금액);
        Assert.False(completed.실제지급대상);
        Assert.Single(db.생활권물류거점보상기록);
    }

    [Fact]
    public async Task 공개조회는_정확위치와_신청원장을_노출하지않는다()
    {
        await using var db = CreateContext();
        var service = CreateService(db, "owner", SsalddelExecutionMode.Simulation);
        var hub = await ReadyPilotAsync(db, service);

        var item = Assert.Single(await service.공개조회Async("bjd:1126010100", default));

        Assert.Equal(hub.StableId, item.StableId);
        Assert.Equal("사가정 생활권", item.대략위치Label);
        var publicProperties = typeof(생활권물류거점공개Response).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("정확위치보호참조", publicProperties);
        Assert.DoesNotContain("신청가원장Id", publicProperties);
    }

    [Fact]
    public async Task 신청은_실제물류대행가원장과_신청자관계를_검증한다()
    {
        await using var db = CreateContext();
        var missingLedgerService = CreateService(db, "owner", SsalddelExecutionMode.Simulation, []);
        await Assert.ThrowsAsync<InvalidOperationException>(() => missingLedgerService.신청Async(Request("manager"), default));

        var otherOwnerLedger = Ledger("mongo-map-ledger-1", "other-owner");
        var otherOwnerService = CreateService(db, "owner", SsalddelExecutionMode.Simulation, [otherOwnerLedger]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => otherOwnerService.신청Async(Request("manager"), default));

        var wrongTemplate = Ledger("mongo-map-ledger-1", "owner");
        wrongTemplate.원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport;
        var wrongTemplateService = CreateService(db, "owner", SsalddelExecutionMode.Simulation, [wrongTemplate]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => wrongTemplateService.신청Async(Request("manager"), default));
    }

    private static async Task<생활권물류거점Response> ReadyForReviewAsync(SsalddelContext db, 생활권물류거점Service service)
    {
        var hub = await service.신청Async(Request("manager"), default);
        hub = await service.소유자동의Async(hub.Id, new() { 동의 = true, ExpectedRevision = hub.Revision }, default);
        var manager = CreateService(db, "manager", SsalddelExecutionMode.Simulation);
        hub = await manager.관리주체동의Async(hub.Id, new() { 동의 = true, ExpectedRevision = hub.Revision }, default);
        return await service.관리자검토Async(hub.Id, new() { 현장확인 = true, 플랫폼승인 = true, ExpectedRevision = hub.Revision }, "admin", default);
    }

    private static async Task<생활권물류거점Response> ReadyPilotAsync(SsalddelContext db, 생활권물류거점Service service)
    {
        var hub = await ReadyForReviewAsync(db, service);
        hub = await service.상태변경Async(hub.Id, new() { 상태Code = "UnderReview", ExpectedRevision = hub.Revision }, "admin", default);
        return await service.상태변경Async(hub.Id, new() { 상태Code = "Pilot", ExpectedRevision = hub.Revision }, "admin", default);
    }

    private static 생활권물류거점신청Request Request(string manager) => new()
    {
        신청가원장Id = "mongo-map-ledger-1",
        공간StableId = "spatial-building:sample-1",
        관리담당자UserId = manager,
        생활권Key = "bjd:1126010100",
        대략위치Label = "사가정 생활권",
        정확위치보호참조 = "protected-place:1",
        최대동시보관건수 = 3,
        최대총중량Kg = 30,
        최대보관시간분 = 240,
        입고가능시간창 = "10:00-18:00",
        수령가능시간창 = "12:00-20:00",
        완료건당고정보상 = 700
    };

    private static 생활권물류거점Service CreateService(
        SsalddelContext db,
        string userId,
        SsalddelExecutionMode mode,
        IReadOnlyList<커뮤니티원장Dto>? ledgers = null)
        => new(
            db,
            new TestUser(userId, null),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })),
            new LedgerStore(ledgers ?? [Ledger("mongo-map-ledger-1", "owner")]));

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"neighborhood-hub-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyEncryption());
    }

    private sealed record TestUser(string? UserId, string? Role) : ICurrentUserAccessor;
    private static 커뮤니티원장Dto Ledger(string id, string owner) => new()
    {
        원장Id = id,
        원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseInbound,
        생성자UserId = owner,
        참여자목록 = []
    };

    private sealed class LedgerStore(IReadOnlyList<커뮤니티원장Dto> ledgers) : I커뮤니티원장저장소
    {
        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(ledgers.SingleOrDefault(x => x.원장Id == 원장Id));

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
            => Task.FromResult(ledgers);

        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
