using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using System.Security.Cryptography;
using System.Text.Json;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>사진 저장 증빙과 단계 완료는 별도이며, 완료 뒤 같은 배송 원장을 재조회합니다.</summary>
public sealed class NeighborhoodMemberWorkViewModel(INeighborhoodMemberDeliveryClient client, ISsalddel현재사용자Context user,
    NeighborhoodMemberDeliverySession session, I업무사진촬영Adapter capture) : NeighborhoodMemberPageViewModel(user, session)
{
    public sealed record EvidencePending(string StageCode, string ContentType, string ContentSha256);
    private NeighborhoodMemberEvidenceRequest? _upload;
    private string? _photoStage;
    public IReadOnlyList<NeighborhoodMemberWorkResponse> Items { get; private set; } = [];
    public NeighborhoodMemberWorkResponse? Work { get; private set; }
    public string? RequestId { get; private set; }
    public bool Loaded { get; private set; }
    public 업무촬영사진? Photo { get; private set; }
    public bool CaptureSupported => capture.IsSupported;
    public bool PhotoUploadPending => _upload is not null || Pending?.Kind == "evidence";
    public string Stage => Work?.CanPickup == true ? "Pickup" : Work?.CanDeliver == true ? "Delivery" : "";
    public string PrimaryLabel => Stage == "Pickup" ? "사진 보내고 픽업 완료" : Stage == "Delivery" ? "사진 보내고 전달 완료" : "현재 상태 확인";
    public string? IssueReason { get; set; }
    public bool CanComplete => Work is not null && Stage.Length > 0 && !IsLoading && !IsSending
        && (Pending is null || Session.Matches(Stage == "Pickup" ? "pickup" : "deliver", Work.RequestId) || Session.Matches("evidence", Work.RequestId) && _upload is not null)
        && (Photo is not null || PhotoUploadPending || (Stage == "Pickup" ? Work.PickupEvidenceId : Work.DeliveryEvidenceId) is not null);
    protected override void ResetForOwner() { Work = null; Items = []; Loaded = false; Photo = null; _upload = null; _photoStage = null; IssueReason = null; }
    public async Task LoadAsync(string? requestId = null)
    {
        if (IsSending || !await PrepareAsync()) return;
        if (RequestId != requestId) { Work = null; Photo = null; _upload = null; _photoStage = null; }
        RequestId = requestId; var operation = Begin(); IsLoading = true; Error = null; Loaded = false; Changed();
        try
        {
            if (requestId is null) { var list = await client.WorkAsync(Token); if (!Current(operation)) return; Items = list; }
            else
            {
                var value = await client.WorkAsync(requestId, Token); if (!Current(operation)) return;
                if (value?.RequestId != requestId) throw new InvalidOperationException("업무 조회 결과가 없습니다.");
                if (Work?.StateCode != value.StateCode) { Photo = null; _upload = null; _photoStage = null; }
                Work = value;
                if (Session.Matches("evidence", requestId))
                {
                    var photoPending = Pending!;
                    var metadata = JsonSerializer.Deserialize<EvidencePending>(photoPending.PayloadJson) ?? throw new InvalidOperationException("사진 요청 정보 미확인");
                    var receipt = await client.EvidenceReceiptAsync(requestId, photoPending.ClientRequestId, Token); if (!Current(operation)) return;
                    if (receipt?.Committed == true)
                    {
                        if (receipt.RequestId != requestId || receipt.ClientRequestId != photoPending.ClientRequestId || receipt.StageCode != metadata.StageCode || string.IsNullOrWhiteSpace(receipt.EvidenceId)) throw new InvalidOperationException("사진 전송 결과 미확인");
                        var confirmed = await client.WorkAsync(requestId, Token); if (!Current(operation)) return;
                        if (confirmed?.RequestId != requestId || confirmed.Revision < receipt.WorkRevision) throw new InvalidOperationException("사진 증빙 원장 미확인");
                        Work = value = confirmed;
                        await Session.ClearAsync(Owner!, Token, photoPending.ClientRequestId); if (!Current(operation)) return;
                        _upload = null; Photo = null; _photoStage = null; Notice = "이 단계의 사진 저장을 확인했습니다. 완료 버튼을 눌러 주세요.";
                    }
                    else if (_upload is null)
                    {
                        await Session.ClearAsync(Owner!, Token, photoPending.ClientRequestId); if (!Current(operation)) return;
                        Photo = null; _photoStage = null; Notice = "사진 저장이 완료되지 않았습니다. 사진을 다시 촬영해 주세요.";
                    }
                }
                if ((Stage == "Pickup" ? value.PickupEvidenceId : value.DeliveryEvidenceId) is not null)
                { _upload = null; Photo = null; _photoStage = null; }
                if (Session.Matches("issue", requestId)) IssueReason = System.Text.Json.JsonSerializer.Deserialize<NeighborhoodMemberIssueRequest>(Pending!.PayloadJson)?.Reason;
                if (Pending is { Target: var pendingTarget } pending && pendingTarget == requestId && value.Revision > pending.ExpectedRevision
                    && (pending.Kind == "pickup" && value.StateCode is "PickedUp" or "Delivered" or "Issue"
                        || pending.Kind == "deliver" && value.StateCode == "Delivered" || pending.Kind == "issue" && value.StateCode == "Issue" && !string.IsNullOrWhiteSpace(value.SupportCaseId)))
                    await Session.ClearAsync(Owner!, Token);
                if (!Current(operation)) return;
            }
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) { Work = null; Items = []; Photo = null; _upload = null; ApplyError(ex); } }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task CaptureAsync()
    {
        if (InputsLocked || PhotoUploadPending || !CaptureSupported || Stage.Length == 0 || !await PrepareAsync()) return;
        var operation = Begin(); var stage = Stage; IsLoading = true; Error = null; Changed();
        try
        {
            var photo = await capture.CaptureAsync(stage == "Pickup" ? "픽업 사진" : "전달 사진", Token);
            if (!Current(operation) || stage != Stage) return;
            if (photo is null) return;
            if (photo.Bytes.Length is 0 or > 5 * 1024 * 1024 || photo.ContentType is not ("image/jpeg" or "image/png"))
            { Error = "사진은 JPEG 또는 PNG로 5MB 이내여야 합니다."; return; }
            Photo = photo; _photoStage = stage;
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public void Retake() { if (!InputsLocked && !PhotoUploadPending) { Photo = null; _photoStage = null; Changed(); } }
    public async Task CompleteAsync()
    {
        if (!await PrepareAsync() || !CanComplete || Owner is null || Work is null) return;
        var operation = Begin(); var target = Work.RequestId; var stage = Stage; IsSending = true; Error = Notice = null; Changed();
        try
        {
            // 서버에 이미 저장된 사진이 있으면 재촬영·재전송 없이 현재 증빙을 사용합니다.
            var evidence = stage == "Pickup" ? Work.PickupEvidenceId : Work.DeliveryEvidenceId;
            if (evidence is null)
            {
                if (_upload is null)
                {
                    if (Photo is null || _photoStage != stage) throw new InvalidOperationException("현재 단계의 사진이 필요합니다.");
                    var requestId = Guid.NewGuid();
                    var metadata = new EvidencePending(stage, Photo.ContentType, Convert.ToHexString(SHA256.HashData(Photo.Bytes)));
                    await PrepareCommandAsync(Owner!, "evidence", target, requestId, Work.Revision, metadata, Token);
                    if (!Current(operation)) return;
                    _upload = new() { ClientRequestId = requestId, ExpectedRevision = Work.Revision,
                        StageCode = stage, ContentType = Photo.ContentType, ContentBase64 = Convert.ToBase64String(Photo.Bytes) };
                }
                await client.EvidenceAsync(target, _upload, Token); if (!Current(operation)) return;
                var stored = await client.WorkAsync(target, Token); if (!Current(operation)) return;
                if (stored?.RequestId != target || (stage == "Pickup" ? stored.PickupEvidenceId : stored.DeliveryEvidenceId) is null)
                    throw new InvalidOperationException("사진 저장 증빙을 확인하지 못했습니다.");
                await Session.ClearAsync(Owner!, Token, _upload.ClientRequestId); if (!Current(operation)) return;
                Work = stored; _upload = null; Photo = null; _photoStage = null;
            }
            var kind = stage == "Pickup" ? "pickup" : "deliver";
            var body = new NeighborhoodMemberCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Work.Revision };
            body = await PrepareCommandAsync(Owner!, kind, target, body.ClientRequestId, body.ExpectedRevision, body, Token);
            if (!Current(operation)) return;
            if (stage == "Pickup") await client.PickupAsync(target, body, Token); else await client.DeliverAsync(target, body, Token);
            if (!Current(operation)) return;
            var latest = await client.WorkAsync(target, Token); if (!Current(operation)) return;
            if (latest?.RequestId != target || latest.Revision <= body.ExpectedRevision || latest.StateCode != (stage == "Pickup" ? "PickedUp" : "Delivered"))
                throw new InvalidOperationException("완료 원장 재조회 필요");
            await Session.ClearAsync(Owner!, Token, body.ClientRequestId); if (!Current(operation)) return;
            Work = latest; Notice = stage == "Pickup" ? "물품을 픽업했습니다." : "전달을 완료했습니다. 수령자의 확인과 지급 확인은 별도입니다.";
        }
        catch (Exception ex) { await RejectPendingAsync(ex, operation); if (Current(operation) && DefinitiveRejection(ex)) { _upload = null; Photo = null; _photoStage = null; } }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    public async Task IssueAsync()
    {
        if (IsSending || IsLoading || !await PrepareAsync() || Work is null || Owner is null
            || Pending is not null && !Session.Matches("issue", Work.RequestId) || PhotoUploadPending
            || Work.StateCode is not ("Assigned" or "PickedUp") || string.IsNullOrWhiteSpace(IssueReason) || IssueReason.Length > 1000) return;
        var operation = Begin(); var target = Work.RequestId; IsSending = true; Error = Notice = null; Changed();
        try
        {
            var body = new NeighborhoodMemberIssueRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Work.Revision, Reason = IssueReason.Trim() };
            body = await PrepareCommandAsync(Owner, "issue", target, body.ClientRequestId, body.ExpectedRevision, body, Token);
            if (!Current(operation)) return;
            await client.IssueAsync(target, body, Token); if (!Current(operation)) return;
            var latest = await client.WorkAsync(target, Token); if (!Current(operation)) return;
            if (latest?.RequestId != target || latest.StateCode != "Issue" || string.IsNullOrWhiteSpace(latest.SupportCaseId)) throw new InvalidOperationException("문제 접수 원장 미확인");
            await Session.ClearAsync(Owner!, Token, body.ClientRequestId); if (!Current(operation)) return;
            Work = latest; Photo = null; _upload = null; Notice = "배송 문제를 접수했습니다. 운영자의 안내를 기다려 주세요.";
        }
        catch (Exception ex) { await RejectPendingAsync(ex, operation); }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
}
