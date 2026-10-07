using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodStorageQueryViewModel(INeighborhoodStorageClient client,
    INeighborhoodCollaborationClient collaborations, NeighborhoodStorageDraftSession pending,
    ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    public IReadOnlyList<NeighborhoodStoragePublicDto> Items { get; private set; } = [];
    public IReadOnlyList<NeighborhoodStorageSpaceDto> Mine { get; private set; } = [];
    public NeighborhoodStoragePublicDto? Public { get; private set; }
    public NeighborhoodStorageSpaceDto? Private { get; private set; }
    public NeighborhoodCollaborationResponse? Collaboration { get; private set; }
    public string? CollaborationId { get; private set; }
    public bool MineOnly { get; private set; }
    public string? RegionKey { get; private set; }
    public int Page { get; private set; } = 1;
    public bool HasMore { get; private set; }
    public bool Loaded { get; private set; }
    public bool HasPending => pending.PendingRequestId is not null;
    public string? PendingTarget => pending.PendingTarget;
    public bool CanRetryMutation => pending.PendingMutation is not null;
    public bool IsPendingReservation => pending.PendingAction == "reserve";
    public string PendingAuthoringHref => NeighborhoodStorageRoutes.WritePage + (pending.PendingTarget is { } id ? "?spaceId=" + Uri.EscapeDataString(id) : "");
    public bool CanReserve => IsAuthenticated && !IsSending && !HasPending && Private?.IsOwner != true && Public is { } space
        && Collaboration is { OwnerAgreed: true, RequesterAgreed: true, Terms: { } terms } collaboration
        && terms.StorageSpaceId == space.SpaceId && collaboration.StatusCode is NeighborhoodCollaborationStates.Agreed or NeighborhoodCollaborationStates.InProgress
        && !((Private?.Reservations ?? []).Any(value => value.CollaborationId == collaboration.StableId));
    protected override void ResetForOwner()
    {
        pending.Bind(Owner); Mine = []; Private = null; Collaboration = null; Public = null; Items = []; Loaded = false;
    }
    public async Task LoadListAsync(bool mineOnly = false, string? region = null, int page = 1)
    {
        SynchronizeOwner();
        MineOnly = mineOnly; RegionKey = region; Page = Math.Clamp(page, 1, 10000);
        if (Disposed || IsSending || (mineOnly && !CheckOwner())) return;
        var operation = Begin();
        IsLoading = true; Loaded = false; Error = null; Items = []; Mine = []; Private = null; Public = null; Changed();
        try
        {
            if (mineOnly)
            {
                var items = await client.MineAsync(Page, Token);
                if (!Current(operation)) return;
                Mine = items; Items = items.Select(value => value.Public).ToArray(); HasMore = items.Count == 20;
            }
            else
            {
                var result = await client.ListAsync(region, Page, Token);
                if (!Current(operation)) return;
                Items = result.Items; HasMore = Page * 20 < result.TotalCount;
            }
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task LoadDetailAsync(string id, string? collaborationId)
    {
        SynchronizeOwner();
        if (Disposed || IsSending) return;
        var operation = Begin(); CollaborationId = collaborationId; IsLoading = true; Loaded = false; Error = null;
        Public = null; Private = null; Collaboration = null; Changed();
        try
        {
            var visible = await client.PublicAsync(id, Token);
            if (!Current(operation)) return;
            Public = visible;
            if (IsAuthenticated)
            {
                try
                {
                    var detail = await client.PrivateAsync(id, collaborationId, Token);
                    if (!Current(operation)) return;
                    if (detail?.SpaceId == id) { Private = detail; Public = detail.Public; }
                }
                catch (SsalddelApiException ex) when (ex.StatusCode is 403 or 404) { }
                if (!string.IsNullOrWhiteSpace(collaborationId))
                {
                    var collaboration = await collaborations.ReadAsync(collaborationId, Token);
                    if (!Current(operation)) return;
                    if (collaboration?.StableId == collaborationId)
                    {
                        Collaboration = collaboration;
                        if (!HasPending && collaboration.MyPendingStorageRequestId is { } originalId && collaboration.MyPendingStorageSpaceId == id)
                        {
                            pending.PendingRequestId = originalId; pending.PendingTarget = id; pending.PendingAction = "reserve";
                            pending.PendingCollaboration = collaborationId; pending.PendingMutation = null;
                            Notice = "이전 보관 예약 결과를 확인해 주세요. 새 예약을 자동으로 접수하지 않습니다.";
                        }
                    }
                }
            }
            if (Public is null) Error = "현재 제공되는 보관공간을 찾을 수 없습니다.";
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) { Private = null; Collaboration = null; ApplyError(ex); } }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task ChangeStateAsync(string action)
    {
        if (IsLoading || IsSending || !CheckOwner() || Private is not { IsOwner: true } detail || HasPending) return;
        var allowed = action == "pause" ? detail.Status == NeighborhoodStorageStatus.Published
            : action == "publish" ? detail.Status is NeighborhoodStorageStatus.Draft or NeighborhoodStorageStatus.Paused
            : action == "close" && detail.Status != NeighborhoodStorageStatus.Closed;
        if (!allowed) return;
        Prepare(detail.SpaceId, action, new NeighborhoodStorageMutationRequest { RequestId = Guid.NewGuid(), ExpectedRevision = detail.Revision });
        await SendPendingAsync();
    }
    public async Task ReserveAsync()
    {
        if (HasPending || !CheckOwner() || !CanReserve || Public is not { } space || Collaboration is not { } collaboration) return;
        Prepare(space.SpaceId, "reserve", new NeighborhoodStorageReservationRequest { RequestId = Guid.NewGuid(), ExpectedRevision = Private?.Revision ?? space.Revision,
            CollaborationId = collaboration.StableId }, collaboration.StableId);
        await SendPendingAsync();
    }
    public async Task HandoverAsync(string collaborationId, string action)
    {
        if (IsLoading || IsSending || HasPending || !CheckOwner() || Private is not { } detail) return;
        if (detail.Reservations.FirstOrDefault(value => value.CollaborationId == collaborationId) is not { } reservation || !reservation.AllowedActions.Contains(action)) return;
        Prepare(detail.SpaceId, "handover", new NeighborhoodStorageReservationActionRequest { RequestId = Guid.NewGuid(), ExpectedRevision = detail.Revision, Action = action }, collaborationId);
        await SendPendingAsync();
    }
    private void Prepare(string target, string action, object request, string? collaborationId = null)
    {
        pending.PendingTarget = target; pending.PendingAction = action; pending.PendingMutation = request; pending.PendingCollaboration = collaborationId;
        pending.PendingRequestId = request switch { NeighborhoodStorageMutationRequest value => value.RequestId, NeighborhoodStorageReservationRequest value => value.RequestId,
            NeighborhoodStorageReservationActionRequest value => value.RequestId, _ => throw new InvalidOperationException() };
    }
    public async Task SendPendingAsync()
    {
        if (IsSending || !CheckOwner() || pending.PendingTarget is not { } id || pending.PendingMutation is null) return;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = pending.PendingMutation switch
            {
                NeighborhoodStorageMutationRequest request => await client.StateAsync(id, pending.PendingAction!, request, Token),
                NeighborhoodStorageReservationRequest request => await client.ReserveAsync(id, request, Token),
                NeighborhoodStorageReservationActionRequest request => await client.HandoverAsync(id, pending.PendingCollaboration!, request, Token),
                _ => null
            };
            if (!Current(operation)) return;
            ApplyResult(result, id);
        }
        catch (Exception ex) { if (Current(operation)) { if (DefinitiveRejection(ex)) pending.ClearPending(); ApplyError(ex, true); } }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    public async Task<string?> RecoverAsync()
    {
        if (IsSending || !CheckOwner() || pending.PendingRequestId is not { } id) return null;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.ReceiptAsync(id, pending.PendingTarget, Token, pending.PendingAction == "reserve" ? pending.PendingCollaboration : null);
            if (!Current(operation)) return null;
            if (result is null) { Notice = "처리 기록이 아직 확인되지 않았습니다. 같은 요청으로 직접 다시 시도할 수 있습니다."; return null; }
            return ApplyResult(result, pending.PendingTarget);
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); return null; }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    private string? ApplyResult(NeighborhoodStorageSpaceDto? result, string? expectedId)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.SpaceId) || (expectedId is not null && expectedId != result.SpaceId))
        { Error = "처리 결과를 확인하지 못했습니다. 같은 요청 번호로 결과를 조회해 주세요."; return null; }
        pending.ClearPending(); Private = result; Public = result.Public;
        if (result.RequestOutcome == "aborted") { Notice = null; Error = "예약하지 못했습니다. 현재 조건과 상태를 확인해 주세요."; return null; }
        Notice = "현재 보관 기록을 확인했습니다."; return result.SpaceId;
    }
}
