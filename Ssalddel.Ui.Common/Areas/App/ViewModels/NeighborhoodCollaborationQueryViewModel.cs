using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodCollaborationQueryViewModel(INeighborhoodCollaborationClient client,
    NeighborhoodCollaborationDraftSession pending, ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    public string Scope { get; private set; } = "requested";
    public int Page { get; private set; } = 1;
    public bool HasMore { get; private set; }
    public bool Loaded { get; private set; }
    public IReadOnlyList<NeighborhoodCollaborationResponse> Items { get; private set; } = [];
    public NeighborhoodCollaborationResponse? Detail { get; private set; }
    public bool PublicRecordConsent { get; set; }
    public bool HandoverPrivacyConfirmed { get; set; }
    public 거래보호확인Request? CommerceProtection { get; set; }
    public bool HasPending => pending.PendingCommand is not null || pending.PendingCreate is not null;
    public string? PendingTarget => pending.PendingTarget;
    public string? PrimaryAction => NeighborhoodCollaborationPresentation.Primary(Detail);
    protected override void ResetForOwner() { pending.Bind(Owner); Items = []; Detail = null; Loaded = false; PublicRecordConsent = false; HandoverPrivacyConfirmed = false; CommerceProtection = null; }
    public Task LoadMineAsync(string? scope = null, int page = 1) => LoadAsync(null, scope ?? Scope, page);
    public Task LoadDetailAsync(string id) => LoadAsync(id, Scope, 1);
    private async Task LoadAsync(string? id, string scope, int page)
    {
        if (IsSending || !CheckOwner()) return;
        var operation = Begin(); IsLoading = true; Loaded = false; Error = null; Notice = null; Items = []; Detail = null;
        Scope = scope == "undertaken" ? "undertaken" : "requested"; Page = Math.Clamp(page, 1, 10000); Changed();
        try
        {
            if (id is null)
            {
                var result = await client.MineAsync(Scope, Page, Token);
                if (!Current(operation)) return;
                Items = result.Items; HasMore = result.HasMore;
            }
            else
            {
                var value = await client.ReadAsync(id, Token);
                if (!Current(operation)) return;
                if (value is null || value.StableId != id) { Error = "내가 참여하는 협업을 찾을 수 없습니다."; return; }
                SetDetail(value);
            }
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task ExecuteAsync(string action, string? applicantId = null, bool? consented = null)
    {
        if (IsLoading || IsSending || !CheckOwner() || Detail is not { } detail) return;
        if (!detail.AllowedActions.Contains(action)) { Error = "현재 단계에서 할 수 없는 행동입니다. 새로고침으로 확인해 주세요."; Changed(); return; }
        if (action == NeighborhoodCollaborationActions.HandoverInfoConsent && !HandoverPrivacyConfirmed)
        { Error = "인계 정보 제공 안내를 확인하고 선택해 주세요."; Changed(); return; }
        if (action == NeighborhoodCollaborationActions.Agree && detail.Terms?.StorageSpaceId is not null && !HandoverPrivacyConfirmed)
        { Error = "보관 인계 정보 제공 안내를 확인하고 선택해 주세요."; Changed(); return; }
        if (pending.PendingCreate is not null || (pending.PendingCommand is not null && pending.PendingTarget != detail.StableId))
        { Error = "이전 처리 결과를 먼저 확인해 주세요."; Changed(); return; }
        var command = new NeighborhoodCollaborationCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = detail.Revision,
            CommerceProtection = action == NeighborhoodCollaborationActions.Agree ? CommerceProtection is null ? null : new() { NoticeVersion = CommerceProtection.NoticeVersion, NoticeAccepted = CommerceProtection.NoticeAccepted, SellerRevision = CommerceProtection.SellerRevision } : null,
            Action = action, ApplicantUserId = applicantId, Consented = action == NeighborhoodCollaborationActions.HandoverInfoConsent ? true : consented,
            PrivacyNoticeVersion = action == NeighborhoodCollaborationActions.Agree && detail.Terms?.StorageSpaceId is not null
                ? NeighborhoodCollaborationAgreementNotice.Version
                : action == NeighborhoodCollaborationActions.HandoverInfoConsent || action == NeighborhoodCollaborationActions.Agree && detail.Terms?.TransferMethod is not null ? NeighborhoodGoodsHandoverNotice.Version : null };
        if (pending.PendingCommand is { } previous)
        {
            if (previous.Action != action || previous.ApplicantUserId != applicantId || previous.Consented != consented)
            { Error = "확인 중인 이전 요청의 결과를 먼저 조회해 주세요."; Changed(); return; }
            command = previous;
        }
        else { pending.PendingCommand = command; pending.PendingTarget = detail.StableId; }
        await SendPendingAsync();
    }
    public async Task SendPendingAsync()
    {
        if (IsSending || !CheckOwner() || pending.PendingCommand is not { } command || pending.PendingTarget is not { } id) return;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.CommandAsync(id, JsonSerializer.Deserialize<NeighborhoodCollaborationCommandRequest>(JsonSerializer.Serialize(command))!, Token);
            if (!Current(operation)) return;
            ApplyResult(result, id);
        }
        catch (Exception ex)
        {
            if (Current(operation))
            {
                if (DefinitiveRejection(ex)) { pending.PendingCommand = null; pending.PendingTarget = null; }
                ApplyError(ex, true);
            }
        }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    public async Task<string?> RecoverAsync()
    {
        if (IsSending || !CheckOwner()) return null;
        var id = pending.PendingCreate?.ClientRequestId ?? pending.PendingCommand?.ClientRequestId;
        if (id is null) return null;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            var result = await client.ReceiptAsync(id.Value, pending.PendingTarget, Token);
            if (!Current(operation)) return null;
            if (result is null) { Notice = "처리 기록이 아직 확인되지 않았습니다. 같은 요청으로 직접 다시 시도할 수 있습니다."; return null; }
            return ApplyResult(result, pending.PendingTarget);
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); return null; }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    private string? ApplyResult(NeighborhoodCollaborationResponse? result, string? expectedId)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.StableId) || (expectedId is not null && expectedId != result.StableId))
        { Error = "처리 결과를 확인하지 못했습니다. 같은 요청 번호로 결과를 조회해 주세요."; return null; }
        pending.PendingCommand = null; pending.PendingCreate = null; pending.PendingTarget = null;
        SetDetail(result); Notice = "현재 협업 기록을 확인했습니다."; return result.StableId;
    }
    private void SetDetail(NeighborhoodCollaborationResponse result)
    {
        Detail = result; PublicRecordConsent = result.MyPublicHistoryConsented; HandoverPrivacyConfirmed = false; CommerceProtection = null;
    }
}
