using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

/// <summary>관계형 SQL 재시도·불명확 commit과 기존 요청 증적을 검증한다. 외부 서비스·실운영 증거는 아니다.</summary>
public sealed class NeighborhoodMemberDeliveryCommandRetryTests
{
    private static readonly ServiceProvider RetryingProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().AddScoped<IExecutionStrategyFactory, RetryStrategyFactory>()
        .BuildServiceProvider(validateScopes: true);

    private static SsalddelContext Open(NeighborhoodMemberDeliveryTests.Fixture f, IInterceptor interceptor)
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(f.Db.Database.GetConnectionString()!)
            .UseInternalServiceProvider(RetryingProvider).AddInterceptors(interceptor).Options,
            new NeighborhoodMemberDeliveryTests.Encryption());

    private static NeighborhoodMemberDeliveryUseCase Service(NeighborhoodMemberDeliveryTests.Fixture f,
        SsalddelContext db, I배차추천경로Service? routes = null, I생활협업Store? collaborations = null,
        I운송원장Mongo동기화Service? projection = null)
        => new(db, f.Actor, Options.Create(new NeighborhoodMemberDeliveryOptions { Enabled = true }),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            f.Clock, new NeighborhoodMemberDeliveryTests.Encryption(), f.Consent, routes ?? f.Routes, f.Storage,
            ledgerSync: projection, memberCollaborations: collaborations);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 거절은재시도전략안에서같은증적과판본을한번만변경한다(bool commitLost)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var offer = await f.Ready();
        var request = f.Command(offer.Revision); var fault = Fault(commitLost, typeof(NeighborhoodMemberOffer));
        await using var db = Open(f, fault);
        var result = await Service(f, db).DeclineAsync(offer.OfferId, request);
        Assert.Equal("Declined", result.StateCode); Assert.Equal(offer.Revision + 1, result.Revision); Assert.Equal(1, fault.Failures);
        await using var current = f.OpenDb();
        Assert.Equal(1, await current.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "Decline"));
        Assert.Equal(result.Revision, (await f.Service(current).DeclineAsync(offer.OfferId, request)).Revision);
        Assert.Equal("Pending", (await current.Set<NeighborhoodMemberDelivery>().SingleAsync()).StateCode);
        await FinancialStateUnchanged(current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수락은외부견적을재전송하지않고현재SQL만회복한다(bool commitLost)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var offer = await f.Ready();
        var request = f.Accept(offer); var fault = Fault(commitLost, typeof(NeighborhoodMemberDelivery));
        await using var db = Open(f, fault); var routes = new RouteProbe(f.Routes); var projection = new Projection(db);
        var result = await Service(f, db, routes, projection: projection).AcceptAsync(offer.OfferId, request);
        Assert.Equal("Assigned", result.StateCode); Assert.Equal(2, routes.Calls); Assert.Equal(1, projection.Calls); Assert.Equal(1, fault.Failures);
        await using var current = f.OpenDb();
        Assert.Equal(result.Revision, (await f.Service(current).AcceptAsync(offer.OfferId, request)).Revision);
        Assert.Equal(1, await current.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "Accept"));
        Assert.Equal(1, await current.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMemberAssigned"));
        Assert.Equal("request-1", (await current.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        Assert.Equal("member-a", (await current.운송원장.SingleAsync()).일반회원수행자UserId);
        Assert.Null((await current.운송원장.SingleAsync()).확정기사Id);
        await FinancialStateUnchanged(current);
    }

    [Fact]
    public async Task 재시도사이에동의가철회되면이전조회로수락하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var offer = await f.Ready();
        var fault = new SaveFault(typeof(NeighborhoodMemberDelivery), () => f.Consent.Valid = false);
        await using var db = Open(f, fault); var routes = new RouteProbe(f.Routes);
        var error = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => Service(f, db, routes).AcceptAsync(offer.OfferId, f.Accept(offer)));
        Assert.Equal("PrivacyConsentRequired", error.Code); Assert.Equal(2, routes.Calls);
        await using var current = f.OpenDb();
        Assert.Equal("Pending", (await current.Set<NeighborhoodMemberDelivery>().SingleAsync()).StateCode);
        Assert.Null((await current.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        Assert.Empty(await current.Set<NeighborhoodMemberReceipt>().Where(x => x.Kind == "Accept").ToListAsync());
        Assert.Empty(await current.운송이벤트.Where(x => x.이벤트타입 == "NeighborhoodMemberAssigned").ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 픽업전취소는재실행시슬롯과취소증적을중복하지않는다(bool commitLost)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var offer = await f.Ready();
        var work = await f.Service().AcceptAsync(offer.OfferId, f.Accept(offer)); f.Actor.UserId = "owner";
        var request = f.Command(work.Revision); var fault = Fault(commitLost, typeof(NeighborhoodMemberDelivery));
        await using var db = Open(f, fault); var projection = new Projection(db);
        await Service(f, db, projection: projection).CancelAsync(work.RequestId, request);
        Assert.Equal(1, fault.Failures); Assert.Equal(1, projection.Calls);
        await using var current = f.OpenDb(); await f.Service(current).CancelAsync(work.RequestId, request);
        Assert.Equal("Cancelled", (await current.Set<NeighborhoodMemberDelivery>().SingleAsync()).StateCode);
        Assert.Null((await current.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        Assert.Equal(상태값.배차큐단계.종료, (await current.운송원장.SingleAsync()).배차큐단계);
        Assert.Equal(1, await current.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "Cancel"));
        Assert.Equal(1, await current.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMemberCancelled"));
        await FinancialStateUnchanged(current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 지원복구는종결사건과같은명령으로회복하고지급을만들지않는다(bool commitLost)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var (work, cases, request) = await RecoveryReady(f);
        var fault = Fault(commitLost, typeof(NeighborhoodMemberDelivery)); await using var db = Open(f, fault);
        var result = await Service(f, db).RecoverAsync(work.RequestId, request, cases, default);
        Assert.Equal("Cancelled", result.StateCode); Assert.Equal(1, fault.Failures);
        await using var current = f.OpenDb();
        Assert.Equal(result.Revision, (await f.Service(current).RecoverAsync(work.RequestId, request, cases, default)).Revision);
        Assert.Equal(1, await current.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "Recovery"));
        Assert.Equal(1, await current.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMemberRecovered"));
        Assert.Equal(1, await current.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMemberCancelled"));
        Assert.Null((await current.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        Assert.Single(cases.Rows); await FinancialStateUnchanged(current);
    }

    [Fact]
    public async Task 재시도사이재개방된지원사건은이전종결근거로복구하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var (work, cases, request) = await RecoveryReady(f);
        var fault = new SaveFault(typeof(NeighborhoodMemberDelivery), () => cases.Rows[request.SupportCaseId].StatusCode = "received");
        await using var db = Open(f, fault);
        var error = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => Service(f, db).RecoverAsync(work.RequestId, request, cases, default));
        Assert.Equal("MemberRecoveryEvidenceRequired", error.Code);
        await using var current = f.OpenDb();
        Assert.Equal("Issue", (await current.Set<NeighborhoodMemberDelivery>().SingleAsync()).StateCode);
        Assert.NotNull((await current.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        Assert.Empty(await current.Set<NeighborhoodMemberReceipt>().Where(x => x.Kind == "Recovery").ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수령확인은현재Mongo의도를검사하고SQL증적을한번만확정한다(bool commitLost)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var store = await ReceiptReady(f);
        var fault = Fault(commitLost, typeof(NeighborhoodMemberReceipt)); await using var db = Open(f, fault);
        var when = await Service(f, db, collaborations: store).ConfirmReceiptAsync(store.Item);
        Assert.Equal(f.Clock.Now, when); Assert.Equal(1, fault.Failures);
        Assert.Equal(when, await Service(f, db, collaborations: store).ConfirmReceiptAsync(store.Item));
        await using var current = f.OpenDb();
        Assert.Equal(1, await current.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "ReceiptConfirmation"));
        Assert.Equal(1, await current.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMemberReceiptConfirmed"));
        Assert.Equal("Delivered", (await current.Set<NeighborhoodMemberDelivery>().SingleAsync()).StateCode);
        Assert.Equal(상태값.배차상태.인수완료, (await current.화주운송의뢰.SingleAsync()).배차상태);
        Assert.Equal("pending", store.Item.ReceiptConfirmationIntent!.StateCode);
        await FinancialStateUnchanged(current);
    }

    [Fact]
    public async Task 재시도사이Mongo의도가바뀌면과거조건으로수령을확정하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var store = await ReceiptReady(f);
        var frozen = store.Copy(); var fault = new SaveFault(typeof(NeighborhoodMemberReceipt), () => store.Item.Revision++);
        await using var db = Open(f, fault);
        var error = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => Service(f, db, collaborations: store).ConfirmReceiptAsync(frozen));
        Assert.Equal("MemberReceiptIntentChanged", error.Code);
        await using var current = f.OpenDb();
        Assert.Equal(상태값.배차상태.하차완료, (await current.화주운송의뢰.SingleAsync()).배차상태);
        Assert.Empty(await current.Set<NeighborhoodMemberReceipt>().Where(x => x.Kind == "ReceiptConfirmation").ToListAsync());
        Assert.Empty(await current.운송이벤트.Where(x => x.이벤트타입 == "NeighborhoodMemberReceiptConfirmed").ToListAsync());
    }

    private static async Task FinancialStateUnchanged(SsalddelContext db)
    {
        var source = await db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal(상태값.결제상태.결제대기, source.결제상태); Assert.Equal(상태값.결제상태.결제대기, source.정산상태);
        Assert.Empty(await db.기사운송대금지급요청.ToListAsync());
    }

    private static async Task<(NeighborhoodMemberWorkResponse, NeighborhoodMemberDeliveryRecoveryTests.Cases, NeighborhoodMemberRecoveryRequest)> RecoveryReady(NeighborhoodMemberDeliveryTests.Fixture f)
    {
        var offer = await f.Ready(); var work = await f.Service().AcceptAsync(offer.OfferId, f.Accept(offer));
        var row = await f.Db.Set<NeighborhoodMemberDelivery>().SingleAsync();
        row.StateCode = "Issue"; row.IssueCode = "BeforePickupReviewRequired"; row.SupportCaseId = "case-1"; row.Revision++;
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear(); work = (await f.Service().WorkAsync(work.RequestId))!;
        var cases = new NeighborhoodMemberDeliveryRecoveryTests.Cases();
        cases.Rows.Add("case-1", new 보호지원Record { CaseId = "case-1", SourceKind = 보호지원출처Codes.생활배송, SourceId = work.RequestId,
            StatusCode = "closed", ClosedAtUtc = f.Clock.Now, PartyUserIds = ["owner", "member-a"],
            Notifications = [new("result", "owner", "member", "local-owner-notice", f.Clock.Now, f.Clock.Now),
                new("result", "member-a", "member", "local-member-notice", f.Clock.Now, f.Clock.Now)] });
        f.Actor.UserId = "admin"; f.Actor.Role = "서버관리자";
        return (work, cases, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision, SupportCaseId = "case-1", ResolutionCode = "CancelBeforePickup" });
    }

    private static async Task<ReceiptStore> ReceiptReady(NeighborhoodMemberDeliveryTests.Fixture f)
    {
        var offer = await f.Ready(); var work = await f.Service().AcceptAsync(offer.OfferId, f.Accept(offer));
        work = await f.Service().EvidenceAsync(work.RequestId, f.Evidence(work, "Pickup"));
        work = await f.Service().PickupAsync(work.RequestId, f.Command(work.Revision));
        work = await f.Service().EvidenceAsync(work.RequestId, f.Evidence(work, "Delivery"));
        await f.Service().DeliverAsync(work.RequestId, f.Command(work.Revision));
        await f.Db.운송원장.ExecuteUpdateAsync(s => s.SetProperty(x => x.생활배송협업Id, "collaboration-1").SetProperty(x => x.생활배송조건판본, 1));
        await f.Db.화주운송의뢰.ExecuteUpdateAsync(s => s.SetProperty(x => x.정산메모, "neighborhood-delivery:v1:x:source-post:1:member-delivery:v1:"));
        var store = new ReceiptStore { Item = new() { StableId = "collaboration-1", Revision = 10, TermsRevision = 1, OwnerUserId = "owner", RequesterUserId = "requester",
            Kind = NeighborhoodCollaborationKinds.Goods, StatusCode = NeighborhoodCollaborationStates.CompletionProposed, ProviderRoleCode = "owner",
            CompletionProposerUserId = "owner", OwnerAgreed = true, RequesterAgreed = true,
            OwnerPrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version, RequesterPrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version,
            SourcePostId = 1, LinkedDeliveryRequestId = "request-1", Terms = new() { TransferMethod = NeighborhoodTransferMethods.DriverDelivery },
            DeliveryIntent = new() { ActorUserId = "owner", TermsRevision = 1, StateCode = "confirmed" } } };
        f.Actor.UserId = "requester";
        store.Item.ReceiptConfirmationIntent = await Service(f, f.Db, collaborations: store)
            .PrepareReceiptAsync(store.Item, "requester", Guid.NewGuid(), "same-receipt-fingerprint");
        return store;
    }

    private sealed class ReceiptStore : I생활협업Store
    {
        public 생활협업Record Item = new();
        public 생활협업Record Copy() => JsonSerializer.Deserialize<생활협업Record>(JsonSerializer.Serialize(Item))!;
        public Task<생활협업Record?> 조회Async(string id, CancellationToken ct = default) => Task.FromResult<생활협업Record?>(Item.StableId == id ? Copy() : null);
        public Task<bool> 생성Async(생활협업Record record, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(생활협업Record record, long revision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<생활협업Record>> 목록Async(생활협업StoreQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Projection(SsalddelContext db) : I운송원장Mongo동기화Service
    {
        public int Calls;
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 row, string actor, CancellationToken ct = default)
        { Assert.Null(db.Database.CurrentTransaction); Calls++; return Task.FromResult<커뮤니티원장Dto?>(new()); }
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 source, string actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RouteProbe(NeighborhoodMemberDeliveryTests.Route inner) : I배차추천경로Service
    {
        public int Calls;
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string option, CancellationToken ct)
        { Calls++; return inner.EstimateRouteAsync(origin, destination, option, ct); }
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination) => throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => throw new NotSupportedException();
        public decimal? CalculateDistanceKm(배차경로좌표 origin, 배차경로좌표 destination) => throw new NotSupportedException();
    }

    private static IFault Fault(bool commitLost, Type entityType) => commitLost ? new CommitFault() : new SaveFault(entityType);
    private interface IFault : IInterceptor { int Failures { get; } }
    private sealed class RetryableFailure : Exception { }
    private sealed class RetryStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    { public IExecutionStrategy Create() => new RetryStrategy(dependencies); }
    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    { protected override bool ShouldRetryOn(Exception exception) => exception is RetryableFailure; }
    private sealed class SaveFault(Type entityType, Action? onFailure = null) : SaveChangesInterceptor, IFault
    {
        public int Failures { get; private set; }
        private bool matches;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        { matches = eventData.Context!.ChangeTracker.Entries().Any(x => x.Entity.GetType() == entityType && x.State is EntityState.Added or EntityState.Modified); return ValueTask.FromResult(result); }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        { if (matches && Failures == 0) { Failures++; onFailure?.Invoke(); throw new RetryableFailure(); } return ValueTask.FromResult(result); }
    }
    private sealed class CommitFault : DbTransactionInterceptor, IFault
    {
        public int Failures { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken ct = default)
        { if (Failures == 0) { Failures++; throw new RetryableFailure(); } return Task.CompletedTask; }
    }
}
