using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>원글의 비식별 공개 정보만 조회하며 참여 의사 표시는 별도 명령입니다.</summary>
public sealed class NeighborhoodCollaborationSourceViewModel(INeighborhoodCollaborationClient client,
    INeighborhoodExchangeMapClient map, NeighborhoodCollaborationDraftSession pending, ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    public long PostId { get; private set; }
    public bool Loaded { get; private set; }
    public IReadOnlyList<NeighborhoodCollaborationOpportunityResponse> Opportunities { get; private set; } = [];
    public IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse> PublicHistory { get; private set; } = [];
    public IReadOnlyList<NeighborhoodPublicRegionDto> Regions { get; private set; } = [];
    public string RegionLabel(string? key) => Regions.FirstOrDefault(item => item.RegionKey == key)?.DisplayName ?? "동네 미지정";
    public bool HasPending => pending.PendingCommand is not null || pending.PendingCreate is not null;
    public bool CanRetryParticipation => pending.PendingCommand?.Action == NeighborhoodCollaborationActions.RequestParticipation;
    protected override void ResetForOwner() { pending.Bind(Owner); Opportunities = []; PublicHistory = []; Regions = []; Loaded = false; }
    public async Task LoadAsync(long postId)
    {
        SynchronizeOwner(); if (Disposed || IsSending || postId < 1) return;
        var operation = Begin(); PostId = postId; IsLoading = true; Error = null; Loaded = false; Opportunities = []; PublicHistory = []; Changed();
        try
        {
            var opportunities = await client.OpportunitiesAsync(postId, Token);
            if (!Current(operation)) return;
            Opportunities = opportunities.Where(value => value.SourcePostId == postId && !string.IsNullOrWhiteSpace(value.StableId)).ToArray();
            var history = await client.PublicHistoryAsync(postId, Token);
            if (!Current(operation)) return;
            PublicHistory = history;
            try { var regions = await map.RegionsAsync(Token); if (!Current(operation)) return; Regions = regions.Items; }
            catch (Exception) { if (!Current(operation)) return; Regions = []; }
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task<string?> ParticipateAsync(string id)
    {
        if (IsLoading || IsSending || !CheckOwner()) return null;
        if (HasPending) { Error = "이전 처리 결과를 먼저 확인해 주세요."; Changed(); return null; }
        var opportunity = Opportunities.FirstOrDefault(item => item.StableId == id);
        if (opportunity is null) return null;
        pending.PendingTarget = id;
        pending.PendingCommand = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = opportunity.Revision, Action = NeighborhoodCollaborationActions.RequestParticipation };
        return await RetryParticipationAsync();
    }
    public async Task<string?> RetryParticipationAsync()
    {
        if (IsSending || !CheckOwner() || !CanRetryParticipation || pending.PendingTarget is not { } id) return null;
        var command = pending.PendingCommand!;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.CommandAsync(id, new() { ClientRequestId = command.ClientRequestId, ExpectedRevision = command.ExpectedRevision, Action = command.Action }, Token);
            if (!Current(operation)) return null;
            return Complete(result, id);
        }
        catch (Exception ex) { if (Current(operation)) { if (DefinitiveRejection(ex)) { pending.PendingCommand = null; pending.PendingTarget = null; } ApplyError(ex, true); } return null; }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    public async Task<string?> RecoverAsync()
    {
        if (IsSending || !CheckOwner()) return null;
        var requestId = pending.PendingCreate?.ClientRequestId ?? pending.PendingCommand?.ClientRequestId;
        if (requestId is null) return null;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.ReceiptAsync(requestId.Value, pending.PendingTarget, Token);
            if (!Current(operation)) return null;
            if (result is null) { Notice = "접수 기록이 아직 확인되지 않았습니다. 자동으로 다시 보내지 않습니다."; return null; }
            return Complete(result, pending.PendingTarget);
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); return null; }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    private string? Complete(NeighborhoodCollaborationResponse? result, string? expectedId)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.StableId) || (expectedId is not null && result.StableId != expectedId))
        { Error = "참여 결과를 확인하지 못했습니다. 같은 요청 번호로 결과를 조회해 주세요."; return null; }
        pending.PendingCommand = null; pending.PendingCreate = null; pending.PendingTarget = null; return result.StableId;
    }
}
