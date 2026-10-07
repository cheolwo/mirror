using System.Text.Json;
using FluentResults;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
namespace Ssalddel.Tests.Services.Community;

public sealed partial class NeighborhoodCollaborationTests
{
    [Fact]
    public async Task 합의한수량과다른배송은_새조건합의없이_접수할수없다()
    {
        await using var f = await Fixture.Create(); var work = await GoodsAgreement(f, NeighborhoodTransferMethods.DriverDelivery);
        var guard = new 생활배송협업Guard(f.Store, f.Clock);
        var result = await guard.획득Async(new() { ClientRequestId = Guid.NewGuid(), CollaborationId = work.StableId,
            ExpectedTermsRevision = work.TermsRevision, SourcePostId = work.SourcePostId, Quantity = 2, DispatchMode = NeighborhoodDispatchModes.Hybrid }, "requester", "different-quantity", default);
        Assert.True(result.IsFailed); Assert.Null(f.Store.Records[work.StableId].DeliveryIntent);
    }
    [Theory]
    [InlineData(NeighborhoodExchange.Offer, NeighborhoodTransferMethods.RecipientPickup, "owner")]
    [InlineData(NeighborhoodExchange.Offer, NeighborhoodTransferMethods.ProviderDelivery, "owner")]
    [InlineData(NeighborhoodExchange.Need, NeighborhoodTransferMethods.RecipientPickup, "requester")]
    [InlineData(NeighborhoodExchange.Need, NeighborhoodTransferMethods.ProviderDelivery, "requester")]
    public async Task 직접전달은_제공필요글의_역할과_주소동의_양측수령확인으로_배차없이완주한다(string intent, string method, string provider)
    {
        await using var f = await Fixture.Create(); f.Post.RoleTag = intent; await f.Db.SaveChangesAsync();
        var input = f.Request(); input.Terms.TransferMethod = method; input.Terms.HandoverPlace = "동의한 인계 장소";
        var work = Success(await f.Service("requester").신청Async(input)); Assert.Equal(provider, work.ProviderRoleCode);
        var ownerView = (await f.Service("owner").상세Async(work.StableId))!;
        Assert.Null(ownerView.Terms!.HandoverPlace); Assert.DoesNotContain("agree", ownerView.AllowedActions);
        work = await GoodsConsent(f, work, "owner"); work = await GoodsConsent(f, work, "requester");
        Assert.Equal("동의한 인계 장소", work.Terms!.HandoverPlace);
        foreach (var actor in new[] { "owner", "requester" })
            work = Success(await f.Service(actor).변경Async(work.StableId, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision, Action = "agree", PrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version }));
        work = Success(await f.Command(work, "owner", "start")); Assert.DoesNotContain("link-delivery", work.AllowedActions);
        var recipient = provider == "owner" ? "requester" : "owner";
        AssertCode("ActionNotAllowed", await f.Command(work, recipient, "propose-completion"));
        work = Success(await f.Command(work, provider, "propose-completion"));
        work = Success(await f.Command(work, recipient, "confirm-completion"));
        Assert.Equal("completed", work.StatusCode); Assert.Empty(f.Db.운송원장); Assert.Empty(f.Db.화주운송의뢰);
        Assert.Null(await f.Service("stranger").상세Async(work.StableId));
    }
    [Fact]
    public async Task 인계정보동의철회는_상세주소를숨기고_새합의를요구한다()
    {
        await using var f = await Fixture.Create(); var work = await GoodsAgreement(f, NeighborhoodTransferMethods.RecipientPickup);
        work = Success(await f.Command(work, "requester", NeighborhoodCollaborationActions.WithdrawHandoverConsent));
        Assert.Equal("requested", work.StatusCode); Assert.False(work.OwnerAgreed); Assert.False(work.RequesterAgreed);
        Assert.Null((await f.Service("owner").상세Async(work.StableId))!.Terms!.HandoverPlace);
        Assert.DoesNotContain("start", work.AllowedActions);
    }
    [Fact]
    public async Task 기사전달의_배송접수는_합의가드로만연결한다()
    {
        await using var f = await Fixture.Create(); var work = await GoodsAgreement(f, NeighborhoodTransferMethods.DriverDelivery);
        Assert.True(work.CanRequestDelivery); Assert.DoesNotContain("link-delivery", work.AllowedActions);
        AssertCode("ActionNotAllowed", await f.Command(work, "requester", "link-delivery"));
    }
    [Fact]
    public void 배차선택근거를덧붙여도_출처번호는_정확히한번읽는다()
    {
        var memo = "neighborhood-delivery:v1:fixture:source-post:17" + 생활배송배차Policy.신청메모(new() { DispatchMode = "hybrid" });
        Assert.Equal(17, 생활배송출처Evidence.Read(memo));
        Assert.Null(생활배송출처Evidence.Read(memo + ":source-post:18"));
        Assert.Null(생활배송출처Evidence.Read("neighborhood-delivery:v1:fixture:source-post:17:unknown"));
    }
    [Fact]
    public void 동의증적의_Guid는_Mongo표준형식으로왕복한다()
    {
        var record = new Ssalddel.Services.Privacy.신청개인정보동의증적Record { Id = Guid.NewGuid() };
        var bson = MongoDB.Bson.BsonExtensionMethods.ToBson(record);
        var read = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Ssalddel.Services.Privacy.신청개인정보동의증적Record>(bson);
        Assert.Equal(record.Id, read.Id);
    }
    [Fact]
    public async Task 기사전달은_배송연결과_실제인수완료없이_협업완료할수없다()
    {
        await using var f = await Fixture.Create(); var work = await GoodsAgreement(f, NeighborhoodTransferMethods.DriverDelivery);
        work = Success(await f.Command(work, "owner", "start")); work = Success(await f.Command(work, "owner", "propose-completion"));
        AssertCode("DeliveryCompletionRequired", await f.Command(work, "requester", "confirm-completion"));
    }
    [Fact]
    public async Task 조건수정은_이전인계정보동의와_양측합의를_무효화한다()
    {
        await using var f = await Fixture.Create(); var work = await GoodsAgreement(f, NeighborhoodTransferMethods.RecipientPickup);
        var terms = JsonSerializer.Deserialize<NeighborhoodCollaborationTerms>(JsonSerializer.Serialize(work.Terms))!;
        terms.TransferMethod = NeighborhoodTransferMethods.ProviderDelivery;
        work = Success(await f.Command(work, "owner", "update-terms", terms));
        Assert.Equal("requested", work.StatusCode); Assert.False(work.OwnerAgreed); Assert.False(work.RequesterAgreed);
        Assert.Null((await f.Service("requester").상세Async(work.StableId))!.Terms!.HandoverPlace);
    }
    [Fact]
    public async Task 배송접수intent는_판본과멱등키를고정하고_연결완료전에_전달완료를만들지않는다()
    {
        await using var f = await Fixture.Create(); var work = await GoodsAgreement(f, NeighborhoodTransferMethods.DriverDelivery);
        var request = new NeighborhoodDeliveryRequest { ClientRequestId = Guid.NewGuid(), CollaborationId = work.StableId,
            ExpectedTermsRevision = work.TermsRevision, SourcePostId = work.SourcePostId, DispatchMode = NeighborhoodDispatchModes.Hybrid };
        var guard = new 생활배송협업Guard(f.Store, f.Clock);
        Assert.True((await guard.획득Async(request, "requester", "payload", default)).IsSuccess);
        Assert.True((await guard.획득Async(request, "requester", "payload", default)).IsSuccess);
        Assert.True((await guard.획득Async(request, "requester", "different", default)).IsFailed);
        var latest = (await f.Service("requester").상세Async(work.StableId))!;
        Assert.True(latest.DeliveryRegistrationPending); Assert.NotNull(latest.MyPendingDeliveryRequest);
        Assert.Null((await f.Service("owner").상세Async(work.StableId))!.MyPendingDeliveryRequest);
        Assert.True((await guard.연결확정Async(request, "requester", "delivery-1", default)).IsSuccess);
        Assert.True((await guard.연결확정Async(request, "requester", "delivery-1", default)).IsSuccess);
        Assert.True((await guard.연결확정Async(request, "requester", "delivery-2", default)).IsFailed);
    }
    [Fact]
    public async Task 취소된intent의_지연접수는_확정이나새조건배차로_부활하지않는다()
    {
        await using var f = await Fixture.Create(); var work = await GoodsAgreement(f, NeighborhoodTransferMethods.DriverDelivery);
        var request = new NeighborhoodDeliveryRequest { ClientRequestId = Guid.NewGuid(), CollaborationId = work.StableId,
            ExpectedTermsRevision = work.TermsRevision, SourcePostId = work.SourcePostId, DispatchMode = NeighborhoodDispatchModes.Hybrid };
        var guard = new 생활배송협업Guard(f.Store, f.Clock); Assert.True((await guard.획득Async(request, "requester", "payload", default)).IsSuccess);
        f.Store.Records[work.StableId].DeliveryIntent!.StateCode = "cancelled";
        Assert.True((await guard.연결확정Async(request, "requester", "delivery-1", default)).IsFailed);
        Assert.True((await guard.획득Async(request, "requester", "payload", default)).IsFailed);
    }
    private static async Task<NeighborhoodCollaborationResponse> GoodsConsent(Fixture f, NeighborhoodCollaborationResponse work, string actor)
        => Success(await f.Service(actor).변경Async(work.StableId, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision,
            Action = NeighborhoodCollaborationActions.HandoverInfoConsent, Consented = true, PrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version }));
    private static async Task<NeighborhoodCollaborationResponse> GoodsAgreement(Fixture f, string method)
    {
        var request = f.Request(); request.Terms.TransferMethod = method; request.Terms.HandoverPlace = "사가정 시험 인계 장소";
        var work = Success(await f.Service("requester").신청Async(request));
        foreach (var actor in new[] { "owner", "requester" }) work = await GoodsConsent(f, work, actor);
        foreach (var actor in new[] { "owner", "requester" }) work = Success(await f.Service(actor).변경Async(work.StableId,
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = work.Revision, Action = "agree", PrivacyNoticeVersion = NeighborhoodGoodsHandoverNotice.Version }));
        return work;
    }
}
