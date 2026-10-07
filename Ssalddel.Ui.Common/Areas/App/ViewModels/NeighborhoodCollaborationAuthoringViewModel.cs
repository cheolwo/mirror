using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodCollaborationAuthoringViewModel(
    INeighborhoodCollaborationClient client, INeighborhoodExchangeClient posts, INeighborhoodStorageClient spaces,
    NeighborhoodCollaborationDraftSession draft, ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    public NeighborhoodCollaborationDraftSession Form => draft;
    public string SourceTitle { get; private set; } = "";
    public bool Loaded { get; private set; }
    public bool NeedsReview => draft.PendingCreate is not null || draft.PendingCommand is not null;
    public bool IsEditing => draft.EditId is not null;
    protected override void ResetForOwner() { draft.Bind(Owner); SourceTitle = ""; Loaded = false; }

    public async Task InitializeAsync(long? postId, string? spaceId, string? editId = null)
    {
        SynchronizeOwner();
        if (Disposed || IsSending) return;
        if (NeedsReview)
        {
            Notice = "이전 처리 결과를 먼저 확인한 뒤 새로운 조건을 입력해 주세요.";
            Changed(); return;
        }
        if (Loaded && draft.PostId == postId && draft.SpaceId == spaceId && draft.EditId == editId) return;
        var sameDraft = draft.PostId == postId && draft.SpaceId == spaceId && draft.EditId == editId;
        if (!sameDraft) draft.ResetDraft();
        draft.PostId = postId; draft.SpaceId = spaceId; draft.EditId = editId;
        var operation = Begin(); IsLoading = true; Loaded = false; Error = null; Notice = null; SourceTitle = ""; Changed();
        try
        {
            if (editId is not null)
            {
                if (!CheckOwner()) return;
                var detail = await client.ReadAsync(editId, Token);
                if (!Current(operation)) return;
                if (detail?.Terms is null || !detail.AllowedActions.Contains(NeighborhoodCollaborationActions.UpdateTerms))
                { Error = "현재 협업 조건은 수정할 수 없습니다."; return; }
                SourceTitle = detail.SourceTitle; draft.Revision = detail.Revision;
                draft.Draft.Kind = detail.Kind; draft.Draft.Terms = Clone(detail.Terms);
                draft.From = detail.Terms.FromUtc.ToLocalTime(); draft.Until = detail.Terms.UntilUtc.ToLocalTime();
            }
            else if (!string.IsNullOrWhiteSpace(spaceId))
            {
                var space = await spaces.PublicAsync(spaceId, Token);
                if (!Current(operation)) return;
                if (space is null) { Error = "현재 제공되는 보관공간을 찾을 수 없습니다."; return; }
                SourceTitle = space.PublicTitle; draft.Draft.StorageSpaceId = space.SpaceId;
                draft.Draft.ExpectedStorageRevision = space.OfferRevision; draft.Draft.Kind = NeighborhoodCollaborationKinds.Storage;
                draft.Draft.Terms.StorageSpaceId = space.SpaceId; draft.Draft.Terms.Unit = space.CapacityUnit;
                draft.Draft.Terms.StorageQuantity = draft.Draft.Terms.Quantity; draft.Draft.Terms.StorageUnit = space.CapacityUnit;
                if (!sameDraft) { draft.From = space.AvailableFromUtc.LocalDateTime; draft.Until = space.AvailableUntilUtc.LocalDateTime; }
            }
            else if (postId is > 0)
            {
                var post = await posts.ReadAsync(postId.Value, Token);
                if (!Current(operation)) return;
                if (post is null || !NeighborhoodExchange.IsExchange(post)) { Error = "현재 생활 교류 글을 찾을 수 없습니다."; return; }
                if (post.CanDelete && !post.DeleteRequiresPassword && IsAuthenticated) { Error = "내 글의 신청은 상대방이 등록하면 내 할 일에서 확인할 수 있습니다."; return; }
                SourceTitle = post.Title; draft.Draft.SourcePostId = post.Id; draft.Draft.ExpectedSourceUpdatedAtUtc = post.UpdatedAtUtc;
            }
            else { Error = "생활 교류 글이나 보관공간에서 신청을 시작해 주세요."; return; }
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }

    public async Task<string?> SubmitAsync()
    {
        if (IsSending || !CheckOwner()) return null;
        if (draft.PendingCommand is not null && draft.PendingTarget != draft.EditId)
        { Error = "이전 협업의 처리 결과를 먼저 확인해 주세요."; Changed(); return null; }
        if (!NeedsReview && (!Loaded || !Validate())) return null;
        if (draft.EditId is { } editId)
        {
            draft.PendingCommand ??= new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = draft.Revision,
                Action = NeighborhoodCollaborationActions.UpdateTerms, Terms = BuildTerms(), CommerceProtection = draft.Draft.CommerceProtection };
            draft.PendingTarget = editId;
        }
        else draft.PendingCreate ??= BuildCreate();
        var operation = Begin(); Error = null; IsSending = true; Changed();
        try
        {
            var result = draft.PendingCreate is { } create ? await client.CreateAsync(Clone(create), Token)
                : await client.CommandAsync(draft.PendingTarget!, Clone(draft.PendingCommand!), Token);
            if (!Current(operation)) return null;
            return Complete(result);
        }
        catch (Exception ex)
        {
            if (Current(operation))
            {
                if (DefinitiveRejection(ex)) { draft.PendingCreate = null; draft.PendingCommand = null; draft.PendingTarget = null; }
                ApplyError(ex, true);
            }
            return null;
        }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    public async Task<string?> RecoverAsync()
    {
        if (IsSending || !CheckOwner()) return null;
        var requestId = draft.PendingCreate?.ClientRequestId ?? draft.PendingCommand?.ClientRequestId;
        if (requestId is null) return null;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.ReceiptAsync(requestId.Value, draft.PendingTarget, Token);
            if (!Current(operation)) return null;
            if (result is null) { Notice = "처리 기록이 아직 확인되지 않았습니다. 같은 요청을 다시 보내려면 직접 버튼을 눌러 주세요."; return null; }
            return Complete(result);
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); return null; }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    private string? Complete(NeighborhoodCollaborationResponse? result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.StableId)
            || (draft.PendingTarget is not null && result.StableId != draft.PendingTarget))
        { Error = "처리 결과를 확인하지 못했습니다. 요청 번호를 유지한 채 결과를 조회해 주세요."; return null; }
        draft.PendingCreate = null; draft.PendingCommand = null; draft.PendingTarget = null; draft.ResetDraft(); Loaded = false;
        return result.StableId;
    }
    private bool Validate()
    {
        var terms = draft.Draft.Terms;
        Error = !draft.ConditionsConfirmed ? "신청 조건을 확인하고 선택해 주세요."
            : string.IsNullOrWhiteSpace(terms.Summary) || terms.Summary.Length > 120 ? "할 일의 요약을 120자 이내로 입력해 주세요."
            : terms.Quantity < 1 || string.IsNullOrWhiteSpace(terms.Unit) ? "수량과 단위를 확인해 주세요."
            : draft.Until <= draft.From ? "종료 시각은 시작 시각보다 뒤여야 합니다."
            : terms.TransferMethod is not null && (!NeighborhoodTransferMethods.IsKnown(terms.TransferMethod) || string.IsNullOrWhiteSpace(terms.HandoverPlace)) ? "전달 방식과 인계 장소를 확인해 주세요."
            : terms.AgreedCostKrw < 0 || terms.Notes?.Length > 1000 ? "부담 금액과 조건 설명을 확인해 주세요." : null;
        if (Error is not null) Changed(); return Error is null;
    }
    private NeighborhoodCollaborationTerms BuildTerms()
    {
        var terms = Clone(draft.Draft.Terms);
        terms.FromUtc = DateTime.SpecifyKind(draft.From, DateTimeKind.Local).ToUniversalTime();
        terms.UntilUtc = DateTime.SpecifyKind(draft.Until, DateTimeKind.Local).ToUniversalTime();
        if (draft.Draft.Kind != NeighborhoodCollaborationKinds.Goods) { terms.TransferMethod = null; terms.HandoverPlace = null; }
        if (draft.Draft.Kind == NeighborhoodCollaborationKinds.Storage)
        {
            terms.StorageQuantity = terms.Quantity; terms.StorageUnit = terms.Unit;
            terms.StorageFromUtc = terms.FromUtc; terms.StorageUntilUtc = terms.UntilUtc;
        }
        return terms;
    }
    private NeighborhoodCollaborationCreateRequest BuildCreate()
    {
        var request = Clone(draft.Draft); request.Terms = BuildTerms(); return request;
    }
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
