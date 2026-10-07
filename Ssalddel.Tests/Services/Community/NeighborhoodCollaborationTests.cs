using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Services.Community;

public sealed partial class NeighborhoodCollaborationTests
{
    [Fact]
    public async Task 신청은_인증_작성자_공개글에만_가능하고_익명글을_임의_claim하지않는다()
    {
        await using var fixture = await Fixture.Create();
        AssertCode("AuthenticationRequired", await fixture.Service(null).신청Async(fixture.Request()));
        AssertCode("SelfCollaborationNotAllowed", await fixture.Service("owner").신청Async(fixture.Request()));
        fixture.Post.AuthorUserId = null; await fixture.Db.SaveChangesAsync();
        AssertCode("AuthenticatedAuthorRequired", await fixture.Service("requester").신청Async(fixture.Request()));
        Assert.Empty(fixture.Store.Records);
    }

    [Fact]
    public async Task 요청번호와_전체payload를_영속대조해_재송신은_한건만_생성하고_다른내용은거절한다()
    {
        await using var fixture = await Fixture.Create(); var service = fixture.Service("requester"); var request = fixture.Request();
        var first = Success(await service.신청Async(request)); var replay = Success(await service.신청Async(request));
        Assert.Equal(first.StableId, replay.StableId); Assert.True(replay.IdempotentReplay); Assert.Single(fixture.Store.Records);
        request.Terms.Quantity++;
        AssertCode("IdempotencyConflict", await service.신청Async(request));
        Assert.Null(await fixture.Service("stranger").상세Async(first.StableId));
        Assert.Null(await fixture.Service("stranger").요청결과Async(request.ClientRequestId, first.StableId));
        Assert.NotNull(await service.요청결과Async(request.ClientRequestId));
    }

    [Fact]
    public async Task 서로조건을동의한뒤_한쪽완료제안과_상대확인이_있어야만완료되며_종결은되돌릴수없다()
    {
        await using var fixture = await Fixture.Create(); var item = Success(await fixture.Service("requester").신청Async(fixture.Request()));
        AssertCode("ActionNotAllowed", await fixture.Command(item, "owner", NeighborhoodCollaborationActions.Start));
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.Agree)); Assert.Equal("requested", item.StatusCode);
        item = Success(await fixture.Command(item, "requester", NeighborhoodCollaborationActions.Agree)); Assert.Equal("agreed", item.StatusCode);
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.Start));
        item = Success(await fixture.Command(item, "requester", NeighborhoodCollaborationActions.ProposeCompletion));
        AssertCode("ActionNotAllowed", await fixture.Command(item, "requester", NeighborhoodCollaborationActions.ConfirmCompletion));
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.ConfirmCompletion)); Assert.Equal("completed", item.StatusCode);
        AssertCode("ActionNotAllowed", await fixture.Command(item, "owner", NeighborhoodCollaborationActions.Cancel));
        Assert.Equal(new[] { "create", "agree", "agree", "start", "propose-completion", "confirm-completion" }, item.History.Select(x => x.Action));
        Assert.Equal(6, (await fixture.Service("requester").상세Async(item.StableId))!.Revision);
    }

    [Theory]
    [InlineData(NeighborhoodCollaborationActions.Cancel, "requester", "cancelled")]
    [InlineData(NeighborhoodCollaborationActions.Reject, "owner", "rejected")]
    public async Task 취소와거절도_불가역원장이다(string action, string actor, string expected)
    {
        await using var fixture = await Fixture.Create(); var item = Success(await fixture.Service("requester").신청Async(fixture.Request()));
        item = Success(await fixture.Command(item, actor, action)); Assert.Equal(expected, item.StatusCode);
        Assert.Empty(item.AllowedActions);
        AssertCode("ActionNotAllowed", await fixture.Command(item, actor, NeighborhoodCollaborationActions.UpdateTerms, fixture.Terms()));
    }

    [Fact]
    public async Task 조건변경은_양측동의와_공개동의와_참여자수락을_초기화하고_시작후에는변경할수없다()
    {
        await using var fixture = await Fixture.Create(); var item = await fixture.Agreed();
        item = Success(await fixture.Command(item, "helper", NeighborhoodCollaborationActions.RequestParticipation));
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.AcceptParticipation, applicant: "helper"));
        item = Success(await fixture.Command(item, "requester", NeighborhoodCollaborationActions.AcceptParticipation, applicant: "helper"));
        item = (await fixture.Service("owner").상세Async(item.StableId))!;
        var updated = fixture.Terms(); updated.Quantity = 3;
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.UpdateTerms, updated));
        Assert.False(item.OwnerAgreed); Assert.False(item.RequesterAgreed); Assert.Equal(2, item.TermsRevision);
        Assert.Equal("requested", Assert.Single(item.Participants).StatusCode);
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.Agree));
        item = Success(await fixture.Command(item, "requester", NeighborhoodCollaborationActions.Agree));
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.Start));
        AssertCode("ActionNotAllowed", await fixture.Command(item, "owner", NeighborhoodCollaborationActions.UpdateTerms, fixture.Terms()));
    }

    [Fact]
    public async Task 출처_수정과삭제를_다시검사하며_최신조건_갱신전_동의를_확정하지않는다()
    {
        await using var fixture = await Fixture.Create(); var request = fixture.Request();
        fixture.Post.UpdatedAtUtc = fixture.Clock.Now.AddMinutes(1); await fixture.Db.SaveChangesAsync();
        AssertCode("SourceChanged", await fixture.Service("requester").신청Async(request));
        var item = Success(await fixture.Service("requester").신청Async(fixture.Request()));
        fixture.Post.UpdatedAtUtc = fixture.Clock.Now.AddMinutes(2); fixture.Post.Body = "협업 제공 조건이 바뀌었습니다."; await fixture.Db.SaveChangesAsync();
        AssertCode("SourceChanged", await fixture.Command(item, "owner", NeighborhoodCollaborationActions.Agree));
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.UpdateTerms, fixture.Terms()));
        item = Success(await fixture.Command(item, "owner", NeighborhoodCollaborationActions.Agree));
        fixture.Post.IsDeleted = true; await fixture.Db.SaveChangesAsync();
        AssertCode("SourcePostUnavailable", await fixture.Command(item, "requester", NeighborhoodCollaborationActions.Agree));
    }

    [Fact]
    public async Task 댓글과추천_수정시각은_협업조건을바꾸지않고_공개조건지문이_같으면동의를유지한다()
    {
        await using var fixture = await Fixture.Create(); var item = Success(await fixture.Service("requester").신청Async(fixture.Request()));
        fixture.Post.CommentCount++; fixture.Post.RecommendationCount++; fixture.Post.UpdatedAtUtc = fixture.Clock.Now.AddMinutes(1);
        await fixture.Db.SaveChangesAsync();
        item = Success(await fixture.Command(item, "owner", "agree"));
        item = Success(await fixture.Command(item, "requester", "agree"));
        Assert.Equal("agreed", item.StatusCode);
        item = Success(await fixture.Command(item, "owner", "start")); Assert.Equal("in-progress", item.StatusCode);
    }

    [Fact]
    public async Task stale판본과_동시CAS패배는_저장되지않고_저장된_명령만_정확히재조회된다()
    {
        await using var fixture = await Fixture.Create(); var item = Success(await fixture.Service("requester").신청Async(fixture.Request()));
        var command = new NeighborhoodCollaborationCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = item.Revision, Action = "agree" };
        item = Success(await fixture.Service("owner").변경Async(item.StableId, command));
        Assert.True(Success(await fixture.Service("owner").변경Async(item.StableId, command)).IdempotentReplay);
        Assert.NotNull(await fixture.Service("owner").요청결과Async(command.ClientRequestId, item.StableId));
        command.Action = "reject"; AssertCode("IdempotencyConflict", await fixture.Service("owner").변경Async(item.StableId, command));
        AssertCode("RevisionConflict", await fixture.Service("requester").변경Async(item.StableId, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1, Action = "agree" }));
        fixture.Store.RejectNextWrite = true;
        AssertCode("RevisionConflict", await fixture.Command(item, "requester", "agree"));
        Assert.False((await fixture.Service("requester").상세Async(item.StableId))!.RequesterAgreed);
    }

    [Fact]
    public async Task 만료는_서버가_영속저장하며_읽기실패를_가짜상태로숨기지않는다()
    {
        await using var fixture = await Fixture.Create(); var item = Success(await fixture.Service("requester").신청Async(fixture.Request()));
        fixture.Clock.Now = item.ExpiresAtUtc.AddSeconds(1);
        var expired = (await fixture.Service("owner").상세Async(item.StableId))!;
        Assert.Equal("expired", expired.StatusCode); Assert.Empty(expired.AllowedActions); Assert.Equal("expire", expired.History.Last().Action);
        Assert.Equal("expired", fixture.Store.Records[item.StableId].StatusCode);
        fixture.Store.ThrowOnRead = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.Service("owner").상세Async(item.StableId));
    }

    [Fact]
    public async Task 참여는_양측수락이필요하고_공개기회나_신청자에게_사적조건을반환하지않는다()
    {
        await using var fixture = await Fixture.Create(); var item = await fixture.Agreed();
        var opportunities = await fixture.Service(null).참여기회Async(fixture.Post.Id);
        Assert.Single(opportunities); Assert.DoesNotContain("010-1234", JsonSerializer.Serialize(opportunities));
        item = Success(await fixture.Command(item, "helper", "request-participation"));
        Assert.Null(item.Terms); Assert.Empty(item.History); Assert.Null(item.LinkedDeliveryRequestId);
        item = Success(await fixture.Command(item, "owner", "accept-participation", applicant: "helper"));
        Assert.Equal("requested", Assert.Single(item.Participants).StatusCode);
        item = Success(await fixture.Command(item, "requester", "accept-participation", applicant: "helper"));
        Assert.Equal("accepted", Assert.Single(item.Participants).StatusCode);
        Assert.Single((await fixture.Service("helper").내목록Async("undertaken")).Items);
        Assert.Empty((await fixture.Service("stranger").내목록Async()).Items);
        Assert.Null((await fixture.Service("helper").상세Async(item.StableId))!.Terms);
        AssertCode("ActionNotAllowed", await fixture.Command(item, "helper", "start"));
    }

    [Fact]
    public async Task 공개기록은_완료와_모든당사자동의가필요하며_조건과신원과금액은노출하지않는다()
    {
        await using var fixture = await Fixture.Create(); var item = await fixture.Agreed();
        item = Success(await fixture.Command(item, "helper", "request-participation"));
        item = Success(await fixture.Command(item, "owner", "accept-participation", applicant: "helper"));
        item = Success(await fixture.Command(item, "requester", "accept-participation", applicant: "helper"));
        item = Success(await fixture.Command(item, "owner", "start"));
        item = Success(await fixture.Command(item, "owner", "propose-completion"));
        item = Success(await fixture.Command(item, "requester", "confirm-completion"));
        item = Success(await fixture.Command(item, "owner", "public-history-consent", consent: true));
        item = Success(await fixture.Command(item, "requester", "public-history-consent", consent: true));
        Assert.Empty(await fixture.Service(null).공개이력Async(fixture.Post.Id));
        item = Success(await fixture.Command(item, "helper", "public-history-consent", consent: true));
        var history = Assert.Single(await fixture.Service(null).공개이력Async(fixture.Post.Id));
        var serialized = JsonSerializer.Serialize(history);
        Assert.DoesNotContain("010-1234", serialized); Assert.DoesNotContain("비공개", serialized);
        Assert.DoesNotContain("owner", serialized); Assert.DoesNotContain("requester", serialized); Assert.DoesNotContain("2500", serialized);
        item = Success(await fixture.Command(item, "helper", "public-history-consent", consent: false));
        Assert.Empty(await fixture.Service(null).공개이력Async(fixture.Post.Id));
        Assert.Equal("completed", item.StatusCode);
    }

    [Fact]
    public async Task 배송을_연결할때_본인_기존생활배송과_출처를_검사하고_기존원장완료전_협업완료를막는다()
    {
        await using var fixture = await Fixture.Create(); var item = await fixture.Agreed("transport");
        fixture.Delivery.Item = new("delivery-1", "stranger", fixture.Post.Id, true, false, false);
        AssertCode("DeliveryLinkNotAllowed", await fixture.Command(item, "owner", "link-delivery", deliveryId: "delivery-1"));
        fixture.Delivery.Item = new("delivery-1", "owner", fixture.Post.Id, true, false, false);
        item = Success(await fixture.Command(item, "owner", "link-delivery", deliveryId: "delivery-1"));
        item = Success(await fixture.Command(item, "owner", "start")); item = Success(await fixture.Command(item, "owner", "propose-completion"));
        AssertCode("DeliveryCompletionRequired", await fixture.Command(item, "requester", "confirm-completion"));
        fixture.Delivery.Item = fixture.Delivery.Item with { Completed = true };
        item = Success(await fixture.Command(item, "requester", "confirm-completion")); Assert.Equal("completed", item.StatusCode);
    }

    [Fact]
    public async Task 공개글없이_공간에서직접신청하고_보관반환양측확인전_완료를막는다()
    {
        await using var fixture = await Fixture.Create(); var request = fixture.Request("storage"); request.SourcePostId = null;
        request.ExpectedSourceUpdatedAtUtc = null; request.StorageSpaceId = "space-1"; request.ExpectedStorageRevision = 2;
        var item = Success(await fixture.Service("requester").신청Async(request)); Assert.Null(item.SourcePostId);
        item = Success(await fixture.Command(item, "owner", "agree")); item = Success(await fixture.Command(item, "requester", "agree"));
        item = Success(await fixture.Command(item, "owner", "start")); item = Success(await fixture.Command(item, "owner", "propose-completion"));
        AssertCode("StorageReturnRequired", await fixture.Command(item, "requester", "confirm-completion"));
        fixture.Storage.Returned = true;
        item = Success(await fixture.Command(item, "requester", "confirm-completion")); Assert.Equal("completed", item.StatusCode);
        Assert.Equal(item.TermsRevision, fixture.Storage.LastTermsRevision);
    }

    [Fact]
    public async Task 음식협업은_일반배송링크나_조리명령으로_승격하지않는다()
    {
        await using var fixture = await Fixture.Create(); var item = await fixture.Agreed("food");
        Assert.DoesNotContain("link-delivery", item.AllowedActions);
        AssertCode("ActionNotAllowed", await fixture.Command(item, "owner", "start-cooking"));
    }

    [Fact]
    public async Task 보관정보공유는_양측의_최신안내동의증적이있어야하며_구형bool합의만으로승격하지않는다()
    {
        await using var fixture = await Fixture.Create(); var item = Success(await fixture.Service("requester").신청Async(fixture.Request("storage")));
        AssertCode("StorageDisclosureNoticeRequired", await fixture.Service("owner").변경Async(item.StableId,
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = item.Revision, Action = "agree" }));
        item = Success(await fixture.Command(item, "owner", "agree"));
        var context = await ((I생활협업연결Query)fixture.Service("owner")).조회Async(item.StableId); Assert.False(context!.AcceptedCurrentTerms);
        item = Success(await fixture.Command(item, "requester", "agree"));
        context = await ((I생활협업연결Query)fixture.Service("owner")).조회Async(item.StableId); Assert.True(context!.AcceptedCurrentTerms);
        fixture.Store.Records[item.StableId].RequesterPrivacyNoticeVersion = "old-version";
        context = await ((I생활협업연결Query)fixture.Service("owner")).조회Async(item.StableId); Assert.False(context!.AcceptedCurrentTerms);
    }

    [Fact]
    public async Task 진행보관예약은_조건변경과_협업취소와_자동만료로_고아예약을만들지않는다()
    {
        await using var fixture = await Fixture.Create(); var item = await fixture.Agreed("storage");
        fixture.Storage.Reservation = new(item.TermsRevision, "reserved");
        AssertCode("StorageReservationOpen", await fixture.Command(item, "owner", "update-terms", fixture.Terms("storage")));
        AssertCode("StorageReservationOpen", await fixture.Command(item, "owner", "cancel"));
        fixture.Clock.Now = item.ExpiresAtUtc.AddSeconds(1);
        Assert.Equal("agreed", (await fixture.Service("owner").상세Async(item.StableId))!.StatusCode);
        fixture.Storage.Reservation = new(item.TermsRevision, "cancelled");
        Assert.Equal("expired", (await fixture.Service("owner").상세Async(item.StableId))!.StatusCode);
    }

    [Fact]
    public async Task 예약intent_협업CAS가_먼저성공하면_이미읽은취소CAS도패배하고_공간예약을_고아로닫지않는다()
    {
        await using var fixture = await Fixture.Create(); var item = await fixture.Agreed("storage");
        var cancelRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueCancel = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeReplace = async (record, _) =>
        {
            if (record.StatusCode == "cancelled") { cancelRead.TrySetResult(); await continueCancel.Task; }
        };
        var cancel = fixture.Command(item, "owner", "cancel"); await cancelRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var guard = new 생활협업보관예약Guard(fixture.Store, fixture.Clock); var requestId = Guid.NewGuid();
        var intent = await guard.획득Async(item.StableId, item.TermsRevision, "space-1", "requester", requestId, 5, new string('a', 64));
        Assert.True(intent.IsSuccess); Assert.Equal("pending", intent.Value.StateCode);
        // 두 번째 원장(공간)은 협업 intent 획득 후에만 예약 CAS를 실행합니다.
        fixture.Storage.Reservation = new(item.TermsRevision, "reserved");
        Assert.True((await guard.확정Async(item.StableId, requestId, "requester")).IsSuccess);
        continueCancel.TrySetResult(); AssertCode("RevisionConflict", await cancel);
        var fresh = (await fixture.Service("owner").상세Async(item.StableId))!;
        Assert.Equal("agreed", fresh.StatusCode); Assert.DoesNotContain("cancel", fresh.AllowedActions);
        Assert.Equal("reserved", fixture.Storage.Reservation.StatusCode);
        Assert.Equal("confirmed", (await guard.조회Async(item.StableId, requestId, "requester"))!.StateCode);
    }

    [Fact]
    public async Task intent는_자동해제하지않고_같은payload로복구하며_다른payload와_해제후재확정은거절한다()
    {
        await using var fixture = await Fixture.Create(); var item = await fixture.Agreed("storage");
        var guard = new 생활협업보관예약Guard(fixture.Store, fixture.Clock); var requestId = Guid.NewGuid(); var fingerprint = new string('b', 64);
        Assert.True((await guard.획득Async(item.StableId, item.TermsRevision, "space-1", "requester", requestId, 5, fingerprint)).IsSuccess);
        var before = fixture.Store.Records[item.StableId].Revision;
        Assert.True((await guard.획득Async(item.StableId, item.TermsRevision, "space-1", "requester", requestId, 5, fingerprint)).IsSuccess);
        Assert.Equal(before, fixture.Store.Records[item.StableId].Revision);
        Assert.True((await guard.획득Async(item.StableId, item.TermsRevision, "space-1", "requester", requestId, 6, fingerprint)).IsFailed);
        fixture.Clock.Now = item.ExpiresAtUtc.AddDays(1);
        var waiting = (await fixture.Service("requester").상세Async(item.StableId))!;
        Assert.True(waiting.StorageReservationPending); Assert.Equal("agreed", waiting.StatusCode);
        Assert.Equal(requestId, waiting.MyPendingStorageRequestId); Assert.Equal("space-1", waiting.MyPendingStorageSpaceId);
        var ownerView = (await fixture.Service("owner").상세Async(item.StableId))!;
        Assert.True(ownerView.StorageReservationPending); Assert.Null(ownerView.MyPendingStorageRequestId); Assert.Null(ownerView.MyPendingStorageSpaceId);
        Assert.DoesNotContain("update-terms", waiting.AllowedActions);
        // 실제 공간 서비스는 이 지점 전에 지연 쓰기를 막는 abort fence를 영속 저장해야 합니다.
        Assert.True((await guard.해제Async(item.StableId, requestId, "requester")).IsSuccess);
        Assert.True((await guard.확정Async(item.StableId, requestId, "requester")).IsFailed);
        Assert.Equal("expired", (await fixture.Service("requester").상세Async(item.StableId))!.StatusCode);
    }

    private static void AssertCode(string expected, Result<NeighborhoodCollaborationResponse> result)
    { Assert.True(result.IsFailed); Assert.Equal(expected, Assert.Single(result.Errors).Metadata["ErrorCode"]); }
    private static NeighborhoodCollaborationResponse Success(Result<NeighborhoodCollaborationResponse> result)
    { Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(x => x.Message))); return result.Value; }

    private sealed class Fixture : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = new(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase("collaboration-" + Guid.NewGuid()).Options, new Encryption());
        public Store Store { get; } = new(); public Clock Clock { get; } = new(); public Spaces Space { get; } = new(); public Storage Storage { get; } = new(); public Delivery Delivery { get; } = new();
        public PlatformCommunityPost Post { get; } = new() { AppKey = "platform", Category = PlatformCommunityPostCategories.General, WorkflowTag = NeighborhoodExchange.WorkflowTag,
            RoleTag = NeighborhoodExchange.Offer, Title = "책 한 상자", AuthorUserId = "owner", PublicNeighborhoodRegionKey = "region:kr:hjd:1126057500", UpdatedAtUtc = new(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc) };
        public static async Task<Fixture> Create() { var f = new Fixture(); f.Db.PlatformCommunityPosts.Add(f.Post); await f.Db.SaveChangesAsync(); return f; }
        public 생활협업UseCase Service(string? actor) => new(Store, Db, new Current(actor), Clock, Space, Storage, Delivery);
        public NeighborhoodCollaborationTerms Terms(string kind = "goods") => new() { Summary = "책 한 상자 전달", Quantity = 1, Unit = "상자", FromUtc = Clock.Now.AddHours(1), UntilUtc = Clock.Now.AddDays(1),
            AgreedCostKrw = 2500, Notes = "비공개 담당자 010-1234",
            StorageSpaceId = kind == "storage" ? "space-1" : null, StorageQuantity = kind == "storage" ? 1 : null, StorageUnit = kind == "storage" ? "상자" : null,
            StorageFromUtc = kind == "storage" ? Clock.Now.AddHours(1) : null, StorageUntilUtc = kind == "storage" ? Clock.Now.AddDays(1) : null };
        public NeighborhoodCollaborationCreateRequest Request(string kind = "goods") => new() { ClientRequestId = Guid.NewGuid(), SourcePostId = Post.Id, ExpectedSourceUpdatedAtUtc = Post.UpdatedAtUtc,
            Kind = kind, Terms = Terms(kind), ExpectedStorageRevision = kind == "storage" ? 2 : null };
        public Task<Result<NeighborhoodCollaborationResponse>> Command(NeighborhoodCollaborationResponse item, string actor, string action,
            NeighborhoodCollaborationTerms? terms = null, string? applicant = null, bool? consent = null, string? deliveryId = null)
            => Service(actor).변경Async(item.StableId, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = item.Revision, Action = action, Terms = terms,
                ApplicantUserId = applicant, Consented = consent, LinkedDeliveryRequestId = deliveryId,
                PrivacyNoticeVersion = action == "agree" && item.Kind == "storage" ? NeighborhoodCollaborationAgreementNotice.Version : null });
        public async Task<NeighborhoodCollaborationResponse> Agreed(string kind = "goods")
        { var item = Success(await Service("requester").신청Async(Request(kind))); item = Success(await Command(item, "owner", "agree")); return Success(await Command(item, "requester", "agree")); }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed record Current(string? UserId) : ICurrentUserAccessor { public string? Role => "일반회원"; }
    private sealed class Clock : TimeProvider { public DateTime Now { get; set; } = new(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc); public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Spaces : I생활협업공간Source
    { public Task<생활협업공간SourceSnapshot?> 조회Async(string stableId, CancellationToken cancellationToken = default) => Task.FromResult<생활협업공간SourceSnapshot?>(stableId == "space-1" ? new(stableId, 2, "owner", "작은 보관 공간", "region:kr:hjd:1126057500", true) : null); }
    private sealed class Storage : I생활협업보관상태Source
    { public bool Returned { get; set; } public 생활협업보관예약Snapshot? Reservation { get; set; } public long LastTermsRevision { get; private set; }
        public Task<bool> 반환확인Async(string collaborationId, long termsRevision, CancellationToken cancellationToken = default) { LastTermsRevision = termsRevision; return Task.FromResult(Returned); }
        public Task<생활협업보관예약Snapshot?> 예약조회Async(string collaborationId, CancellationToken cancellationToken = default) => Task.FromResult(Reservation); }
    private sealed class Delivery : I생활협업배송Source
    { public 생활협업배송Snapshot? Item { get; set; } public Task<생활협업배송Snapshot?> 조회Async(string requestId, CancellationToken cancellationToken = default) => Task.FromResult(Item?.RequestId == requestId ? Item : null); }
    private sealed class Store : I생활협업Store
    {
        public Dictionary<string, 생활협업Record> Records { get; } = new(); private readonly object gate = new(); public bool RejectNextWrite { get; set; } public bool ThrowOnRead { get; set; }
        public Func<생활협업Record, long, Task>? BeforeReplace { get; set; }
        private static 생활협업Record Copy(생활협업Record value) => JsonSerializer.Deserialize<생활협업Record>(JsonSerializer.Serialize(value))!;
        public Task<생활협업Record?> 조회Async(string id, CancellationToken cancellationToken = default)
        { if (ThrowOnRead) throw new IOException("저장소 읽기 실패"); lock (gate) return Task.FromResult(Records.TryGetValue(id, out var item) ? Copy(item) : null); }
        public Task<bool> 생성Async(생활협업Record item, CancellationToken cancellationToken = default)
        { lock (gate) { if (Records.ContainsKey(item.StableId)) return Task.FromResult(false); Records.Add(item.StableId, Copy(item)); return Task.FromResult(true); } }
        public async Task<bool> 교체Async(생활협업Record item, long revision, CancellationToken cancellationToken = default)
        { if (BeforeReplace is not null) await BeforeReplace(item, revision); lock (gate) { if (RejectNextWrite) { RejectNextWrite = false; return false; } if (!Records.TryGetValue(item.StableId, out var existing) || existing.Revision != revision) return false; Records[item.StableId] = Copy(item); return true; } }
        public Task<IReadOnlyList<생활협업Record>> 목록Async(생활협업StoreQuery query, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                var items = Records.Values.Where(x => query.SourcePostId is null || x.SourcePostId == query.SourcePostId);
                if (query.UserId is not null) items = items.Where(x => query.Scope == "requested" ? x.RequesterUserId == query.UserId
                    : query.Scope == "undertaken" ? x.OwnerUserId == query.UserId || x.Participants.Any(p => p.UserId == query.UserId && p.StatusCode == "accepted")
                    : x.OwnerUserId == query.UserId || x.RequesterUserId == query.UserId || x.Participants.Any(p => p.UserId == query.UserId));
                return Task.FromResult<IReadOnlyList<생활협업Record>>(items.OrderByDescending(x => x.UpdatedAtUtc).Skip(query.Skip).Take(query.Take).Select(Copy).ToArray());
            }
        }
    }
}
