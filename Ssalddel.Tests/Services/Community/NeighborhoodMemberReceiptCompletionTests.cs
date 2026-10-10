using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Community;

/// <summary>실제 SQLite 상태/receipt/사진과 Mongo CAS 계약 대역을 대조한다. 실DB HTTP 증거와 구분한다.</summary>
public sealed class NeighborhoodMemberReceiptCompletionTests
{
    [Theory]
    [InlineData("owner", "requester")]
    [InlineData("requester", "owner")]
    public async Task 제공과필요글모두실제반대수령자가확인하고수금은확정하지않는다(string provider, string receiver)
    {
        await using var f = await Fixture.Ready(provider); f.Base.Actor.UserId = receiver;
        var command = f.Command(); var result = await f.Collaboration().변경Async("collaboration-1", command);
        Assert.True(result.IsSuccess, string.Join(";", result.Errors));
        Assert.Equal(NeighborhoodCollaborationStates.Completed, result.Value.StatusCode);
        f.Base.Db.ChangeTracker.Clear();
        var source = await f.Base.Db.화주운송의뢰.SingleAsync();
        Assert.Equal(상태값.배차상태.인수완료, source.배차상태); Assert.NotNull(source.인수증등록일시);
        Assert.Equal(상태값.결제상태.결제대기, source.결제상태); Assert.Equal(상태값.결제상태.결제대기, source.정산상태);
        Assert.Single(await f.Base.Db.Set<NeighborhoodMemberReceipt>().Where(x => x.Kind == "ReceiptConfirmation").ToListAsync());
        Assert.Single(await f.Base.Db.운송이벤트.Where(x => x.이벤트타입 == "NeighborhoodMemberReceiptConfirmed").ToListAsync());
        f.Base.Actor.UserId = "member-a"; var work = (await f.Member().WorkAsync("request-1"))!;
        Assert.Equal("Delivered", work.StateCode); Assert.Equal(source.인수증등록일시, work.ReceiptConfirmedAtUtc);
        Assert.Empty(work.EvidenceStageCode); Assert.Null(work.Pickup); Assert.Null(work.Dropoff);
        f.Base.Actor.UserId = receiver;
        Assert.True((await f.Collaboration().변경Async("collaboration-1", command)).Value.IdempotentReplay);
        Assert.Equal(1, await f.Base.Db.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMemberReceiptConfirmed"));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("member-a")]
    [InlineData("operator")]
    public async Task 제공자수행자운영자는수령자를대신할수없다(string actor)
    {
        await using var f = await Fixture.Ready(); f.Base.Actor.UserId = actor;
        Assert.True((await f.Collaboration().변경Async("collaboration-1", f.Command())).IsFailed);
        Assert.Empty(await f.Base.Db.Set<NeighborhoodMemberReceipt>().Where(x => x.Kind == "ReceiptConfirmation").ToListAsync());
        Assert.Null(f.Store.Item!.ReceiptConfirmationIntent);
    }

    [Theory]
    [InlineData("photo")]
    [InlineData("hash")]
    [InlineData("terms")]
    [InlineData("source")]
    [InlineData("consent")]
    [InlineData("expired")]
    public async Task 실제전달증빙현재조건출처동의기한이없으면수령을열지않는다(string invalid)
    {
        await using var f = await Fixture.Ready(); f.Base.Actor.UserId = "requester";
        if (invalid == "photo") await f.Base.Db.Set<NeighborhoodMemberPhoto>().Where(x => x.StageCode == "Delivery").ExecuteUpdateAsync(s => s.SetProperty(x => x.StateCode, "Uploaded"));
        if (invalid == "hash") f.Base.Storage.CorruptDownload = true;
        if (invalid == "terms") await f.Base.Db.운송원장.ExecuteUpdateAsync(s => s.SetProperty(x => x.생활배송조건판본, 9));
        if (invalid == "source") await f.Base.Db.화주운송의뢰.ExecuteUpdateAsync(s => s.SetProperty(x => x.정산메모, "neighborhood-delivery:v1:x:source-post:2:member-delivery:v1:"));
        if (invalid == "consent") f.Base.Consent.Valid = false;
        if (invalid == "expired") f.Base.Clock.Now = f.Base.Clock.Now.AddHours(73);
        var result = await f.Collaboration().변경Async("collaboration-1", f.Command());
        Assert.True(result.IsFailed); Assert.Null(f.Store.Item!.ReceiptConfirmationIntent);
        Assert.Equal(0, await f.Base.Db.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "ReceiptConfirmation"));
    }

    [Fact]
    public async Task SQL확정뒤Mongo실패는동일요청과새UseCase에서회복되고다른결정은잠긴다()
    {
        await using var f = await Fixture.Ready(); f.Base.Actor.UserId = "requester"; var command = f.Command();
        f.Store.RejectCompletion = true;
        var first = await f.Collaboration().변경Async("collaboration-1", command);
        Assert.True(first.IsFailed); Assert.Equal("pending", f.Store.Item!.ReceiptConfirmationIntent!.StateCode);
        Assert.Equal(1, await f.Base.Db.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "ReceiptConfirmation"));
        var cancel = f.Command(); cancel.ExpectedRevision = f.Store.Item.Revision; cancel.Action = NeighborhoodCollaborationActions.Cancel;
        Assert.Contains((await f.Collaboration().변경Async("collaboration-1", cancel)).Errors, x => (string)x.Metadata["ErrorCode"] == "ReceiptConfirmationPending");
        await using var fresh = f.Base.OpenDb(); f.Store.RejectCompletion = false;
        var recovered = await f.Collaboration(fresh).변경Async("collaboration-1", command);
        Assert.True(recovered.IsSuccess); Assert.Equal(NeighborhoodCollaborationStates.Completed, recovered.Value.StatusCode);
        Assert.Equal(1, await fresh.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "ReceiptConfirmation"));
        Assert.Equal(1, await fresh.운송이벤트.CountAsync(x => x.이벤트타입 == "NeighborhoodMemberReceiptConfirmed"));
        command.ExpectedRevision++; Assert.True((await f.Collaboration(fresh).변경Async("collaboration-1", command)).IsFailed);
    }

    [Fact]
    public async Task 투영실패는완료로위장하지않고같은키로원장반영을재시도한다()
    {
        await using var f = await Fixture.Ready(); f.Base.Actor.UserId = "requester"; var command = f.Command();
        f.Projection.Fail = true;
        var first = await f.Collaboration().변경Async("collaboration-1", command);
        Assert.Contains(first.Errors, x => (string)x.Metadata["ErrorCode"] == "MemberReceiptProjectionPending");
        Assert.Equal(NeighborhoodCollaborationStates.CompletionProposed, f.Store.Item!.StatusCode);
        Assert.Null((await f.Collaboration().요청결과Async(command.ClientRequestId, "collaboration-1")));
        f.Projection.Fail = false;
        Assert.True((await f.Collaboration().변경Async("collaboration-1", command)).IsSuccess);
        Assert.Equal(2, f.Projection.Calls);
        Assert.Equal(1, await f.Base.Db.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "ReceiptConfirmation"));
    }

    [Fact]
    public async Task 같은키가초기조회뒤먼저끝나면전달판본변경거절대신원완료를복구한다()
    {
        await using var f = await Fixture.Ready(); f.Base.Actor.UserId = "requester"; var command = f.Command();
        f.Store.AfterRead = async () =>
        {
            f.Store.AfterRead = null;
            Assert.True((await f.Collaboration().변경Async("collaboration-1", command)).IsSuccess);
        };
        var result = await f.Collaboration().변경Async("collaboration-1", command);
        Assert.True(result.IsSuccess); Assert.True(result.Value.IdempotentReplay);
        Assert.Equal(1, await f.Base.Db.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "ReceiptConfirmation"));
    }

    [Fact]
    public async Task 같은키의빠른요청이이미끝났으면느린intentCAS도완료결과를복구한다()
    {
        await using var f = await Fixture.Ready(); f.Base.Actor.UserId = "requester"; var command = f.Command();
        f.Store.BeforeReplace = async record =>
        {
            if (record.ReceiptConfirmationIntent?.StateCode != "pending") return;
            f.Store.BeforeReplace = null;
            Assert.True((await f.Collaboration().변경Async("collaboration-1", command)).IsSuccess);
        };
        var result = await f.Collaboration().변경Async("collaboration-1", command);
        Assert.True(result.IsSuccess); Assert.True(result.Value.IdempotentReplay);
        Assert.Equal(1, await f.Base.Db.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "ReceiptConfirmation"));
    }

    [Fact]
    public async Task 사진없는배정과픽업뒤DTO는촬영을열지만완료권한은닫는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var offer = await f.Ready();
        var work = await f.Service().AcceptAsync(offer.OfferId, f.Accept(offer));
        Assert.Equal("Pickup", work.EvidenceStageCode); Assert.False(work.CanPickup);
        work = await f.Service().EvidenceAsync(work.RequestId, f.Evidence(work, "Pickup"));
        Assert.Equal("Pickup", work.EvidenceStageCode); Assert.True(work.CanPickup);
        work = await f.Service().PickupAsync(work.RequestId, f.Command(work.Revision));
        Assert.Equal("Delivery", work.EvidenceStageCode); Assert.False(work.CanDeliver);
        f.Consent.Valid = false;
        Assert.Empty((await f.Service().WorkAsync(work.RequestId))!.EvidenceStageCode);
    }

    private sealed class Fixture(NeighborhoodMemberDeliveryTests.Fixture basis) : IAsyncDisposable
    {
        public NeighborhoodMemberDeliveryTests.Fixture Base { get; } = basis;
        public Store Store { get; } = new(); public Projection Projection { get; } = new();
        public NeighborhoodMemberDeliveryUseCase Member(SsalddelContext? db = null) => new(db ?? Base.Db, Base.Actor,
            Options.Create(new NeighborhoodMemberDeliveryOptions { Enabled = true }),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            Base.Clock, new NeighborhoodMemberDeliveryTests.Encryption(), Base.Consent, Base.Routes, Base.Storage,
            ledgerSync: Projection, memberCollaborations: Store);
        public 생활협업UseCase Collaboration(SsalddelContext? db = null) => new(Store, db ?? Base.Db, Base.Actor, Base.Clock, memberDeliveries: Member(db));
        public NeighborhoodCollaborationCommandRequest Command() => new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 10, Action = NeighborhoodCollaborationActions.ConfirmCompletion };
        public static async Task<Fixture> Ready(string provider = "owner")
        {
            var basis = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var f = new Fixture(basis);
            var offer = await basis.Ready(); var work = await basis.Service().AcceptAsync(offer.OfferId, basis.Accept(offer));
            work = await basis.Service().EvidenceAsync(work.RequestId, basis.Evidence(work, "Pickup"));
            work = await basis.Service().PickupAsync(work.RequestId, basis.Command(work.Revision));
            work = await basis.Service().EvidenceAsync(work.RequestId, basis.Evidence(work, "Delivery"));
            await basis.Service().DeliverAsync(work.RequestId, basis.Command(work.Revision));
            await basis.Db.운송원장.ExecuteUpdateAsync(s => s.SetProperty(x => x.생활배송협업Id, "collaboration-1").SetProperty(x => x.생활배송조건판본, 1));
            await basis.Db.화주운송의뢰.ExecuteUpdateAsync(s => s.SetProperty(x => x.정산메모, "neighborhood-delivery:v1:x:source-post:1:member-delivery:v1:"));
            f.Store.Item = new() { StableId = "collaboration-1", Revision = 10, TermsRevision = 1, OwnerUserId = "owner", RequesterUserId = "requester",
                Kind = NeighborhoodCollaborationKinds.Goods, StatusCode = NeighborhoodCollaborationStates.CompletionProposed,
                ProviderRoleCode = provider, CompletionProposerUserId = provider == "owner" ? "owner" : "requester", OwnerAgreed = true, RequesterAgreed = true,
                OwnerPrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version, RequesterPrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version,
                SourcePostId = 1, LinkedDeliveryRequestId = "request-1", Terms = new() { TransferMethod = NeighborhoodTransferMethods.DriverDelivery },
                DeliveryIntent = new() { ActorUserId = "owner", TermsRevision = 1, StateCode = "confirmed" } };
            return f;
        }
        public ValueTask DisposeAsync() => Base.DisposeAsync();
    }
    private sealed class Store : I생활협업Store
    {
        public 생활협업Record? Item; public bool RejectCompletion; public Func<생활협업Record, Task>? BeforeReplace; public Func<Task>? AfterRead;
        private static 생활협업Record Copy(생활협업Record item) => JsonSerializer.Deserialize<생활협업Record>(JsonSerializer.Serialize(item))!;
        public async Task<생활협업Record?> 조회Async(string id, CancellationToken cancellationToken = default)
        {
            var result = Item is null || Item.StableId != id ? null : Copy(Item);
            if (AfterRead is not null) await AfterRead();
            return result;
        }
        public Task<bool> 생성Async(생활협업Record record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async Task<bool> 교체Async(생활협업Record record, long expectedRevision, CancellationToken cancellationToken = default)
        {
            if (BeforeReplace is not null) await BeforeReplace(record);
            if (Item?.Revision != expectedRevision || RejectCompletion && record.StatusCode == NeighborhoodCollaborationStates.Completed) return false;
            Assert.Equal(expectedRevision + 1, record.Revision); Item = Copy(record); return true;
        }
        public Task<IReadOnlyList<생활협업Record>> 목록Async(생활협업StoreQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<생활협업Record>>(Item is null ? [] : [Copy(Item)]);
    }
    private sealed class Projection : I운송원장Mongo동기화Service
    {
        public bool Fail; public int Calls;
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 row, string actor, CancellationToken ct = default)
        { Calls++; return Task.FromResult<커뮤니티원장Dto?>(Fail ? null : new()); }
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(살뜰.도메인.화주.화주운송의뢰 source, string actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
