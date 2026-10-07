using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodStorageAuthoringViewModel(INeighborhoodStorageClient client,
    INeighborhoodExchangeMapClient map, NeighborhoodStorageDraftSession draft,
    ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    public NeighborhoodStorageDraftSession Form => draft;
    public IReadOnlyList<NeighborhoodPublicRegionDto> Regions { get; private set; } = [];
    public bool Loaded { get; private set; }
    public bool NeedsReview => draft.PendingSave is not null;
    protected override void ResetForOwner() { draft.Bind(Owner); Loaded = false; Regions = []; }

    public async Task InitializeAsync(string? spaceId)
    {
        SynchronizeOwner();
        if (Disposed || IsSending || !IsAuthenticated) return;
        if (draft.PendingRequestId is not null)
        { Notice = "이전 저장이나 인계 결과를 먼저 확인해 주세요."; Changed(); return; }
        if (Loaded && draft.SpaceId == spaceId) return;
        var sameDraft = draft.SpaceId == spaceId;
        if (!sameDraft) draft.ResetDraft();
        draft.SpaceId = spaceId;
        var operation = Begin(); Error = null; IsLoading = true; Loaded = false; Changed();
        try
        {
            var regions = await map.RegionsAsync(Token);
            if (!Current(operation)) return;
            Regions = regions.Items.Where(region => region.PrecisionCode == "neighborhood").ToArray();
            if (spaceId is not null)
            {
                var detail = await client.PrivateAsync(spaceId, null, Token);
                if (!Current(operation)) return;
                if (detail is null || !detail.IsOwner || detail.Status == NeighborhoodStorageStatus.Closed)
                { Error = "본인이 제공 중인 공간만 수정할 수 있습니다."; return; }
                draft.Revision = detail.Revision;
                draft.Title = detail.Public.PublicTitle; draft.Description = detail.Public.PublicDescription;
                draft.RegionKey = detail.Public.Region.RegionKey; draft.GoodsKind = detail.Public.GoodsKind;
                draft.Capacity = detail.Public.CapacityQuantity; draft.From = detail.Public.AvailableFromUtc.LocalDateTime;
                draft.Until = detail.Public.AvailableUntilUtc.LocalDateTime;
                draft.Address = detail.PrivateAddress; draft.Contact = detail.PrivateContact; draft.Instructions = detail.PrivateHandoverInstructions;
                draft.Publish = false;
            }
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task<string?> SaveAsync()
    {
        if (IsSending || !CheckOwner()) return null;
        if (draft.PendingRequestId is not null && draft.PendingSave is null)
        { Error = "이전 인계 결과를 먼저 확인해 주세요."; Changed(); return null; }
        if (!NeedsReview && (!Loaded || !Validate())) return null;
        draft.PendingSave ??= BuildRequest(); draft.PendingRequestId = draft.PendingSave.RequestId;
        draft.PendingTarget = draft.SpaceId;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.SaveAsync(draft.PendingTarget, draft.PendingSave, Token);
            if (!Current(operation)) return null;
            return Complete(result);
        }
        catch (Exception ex)
        {
            if (Current(operation)) { if (DefinitiveRejection(ex)) draft.ClearPending(); ApplyError(ex, true); }
            return null;
        }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    public async Task<string?> RecoverAsync()
    {
        if (IsSending || !CheckOwner() || draft.PendingRequestId is not { } requestId) return null;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.ReceiptAsync(requestId, draft.PendingTarget, Token);
            if (!Current(operation)) return null;
            if (result is null) { Notice = "저장 기록이 아직 확인되지 않았습니다. 같은 요청을 다시 보내려면 직접 버튼을 눌러 주세요."; return null; }
            return Complete(result);
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); return null; }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    private string? Complete(NeighborhoodStorageSpaceDto? result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.SpaceId)
            || (draft.PendingTarget is not null && result.SpaceId != draft.PendingTarget))
        { Error = "저장 결과를 확인하지 못했습니다. 요청 번호를 유지한 채 결과를 조회해 주세요."; return null; }
        draft.ClearPending(); draft.ResetDraft(); Loaded = false; return result.SpaceId;
    }
    private bool Validate()
    {
        Error = !draft.PrivacyConfirmed ? "공개 정보와 비공개 인계 정보의 구분을 확인해 주세요."
            : string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Length > 120 ? "공간 이름은 120자 이내로 입력해 주세요."
            : !Regions.Any(region => region.RegionKey == draft.RegionKey) ? "목록에 있는 동네를 선택해 주세요."
            : draft.Capacity <= 0 || draft.Until <= draft.From ? "보관 가능한 상자 수와 기간을 확인해 주세요."
            : string.IsNullOrWhiteSpace(draft.GoodsKind) || draft.Description.Length > 1000 ? "보관할 물품과 공개 설명을 확인해 주세요."
            : string.IsNullOrWhiteSpace(draft.Address) || string.IsNullOrWhiteSpace(draft.Contact) ? "비공개 인계 장소와 연락 방법을 입력해 주세요." : null;
        if (Error is not null) Changed(); return Error is null;
    }
    private NeighborhoodStorageSpaceRequest BuildRequest() => new()
    {
        RequestId = draft.RequestId, ExpectedRevision = draft.Revision, PublicTitle = draft.Title.Trim(), PublicDescription = draft.Description.Trim(),
        PublicNeighborhoodRegionKey = draft.RegionKey, GoodsKind = draft.GoodsKind.Trim(), CapacityQuantity = draft.Capacity, CapacityUnit = "상자",
        AvailableFromUtc = new DateTimeOffset(DateTime.SpecifyKind(draft.From, DateTimeKind.Local)).ToUniversalTime(),
        AvailableUntilUtc = new DateTimeOffset(DateTime.SpecifyKind(draft.Until, DateTimeKind.Local)).ToUniversalTime(),
        PrivateAddress = draft.Address.Trim(), PrivateContact = draft.Contact.Trim(), PrivateHandoverInstructions = draft.Instructions.Trim(),
        PublishOnCreate = draft.SpaceId is null && draft.Publish
    };
}
