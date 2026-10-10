using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed partial class NeighborhoodDeliveryAuthoringViewModel
{
    private const string RecoveryScope = "neighborhood-delivery-create.v1";
    private readonly IWorkflowRecoveryStore _recoveryStore = recoveryStore ?? new VolatileWorkflowRecoveryStore();
    private bool _recoveryLoaded;
    private DateTime? _recoveryCreatedAtUtc;
    private Guid? _expiredRecoveryId;
    public bool RecoveryExpired => _expiredRecoveryId is not null;
    public bool RecoveryCheckedMissing { get; private set; }
    public bool RegistrationRecoveryPending { get; private set; }
    public Task<bool> CheckRecoveryAsync() { _recoveryLoaded = false; return RestoreAsync(); }

    /// <summary>재진입은 저장 결과만 읽습니다. 원 POST 재전송과 협업 연결은 사용자 행동으로 남깁니다.</summary>
    public async Task<bool> RestoreAsync()
    {
        if (!CheckOwner()) return false;
        if (_recoveryLoaded) return true;
        if (IsSending) return false;
        var owner = _owner!; var generation = ++_generation; IsSending = true; Changed();
        try
        {
            var record = await _recoveryStore.LoadAsync(owner, RecoveryScope, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return false;
            if (record is null) { _recoveryLoaded = true; return true; }
            if (record.Owner != owner || record.Scope != RecoveryScope) throw new InvalidDataException("이전 배송 요청의 계정을 확인하지 못했습니다.");
            _recoveryCreatedAtUtc = record.CreatedAtUtc;
            if (record.PayloadJson is null)
            {
                _expiredRecoveryId = record.RequestId;
                Draft = new() { ClientRequestId = record.RequestId };
            }
            else
            {
                var saved = JsonSerializer.Deserialize<AuthoringRecovery>(record.PayloadJson)
                    ?? throw new InvalidDataException("이전 배송 요청을 확인하지 못했습니다.");
                if (saved.Request.ClientRequestId != record.RequestId) throw new InvalidDataException("이전 배송 요청 번호가 다릅니다.");
                _recoveryRequest = saved.Request;
                Draft = JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(saved.Request))!;
                _submittedInput = Fingerprint(saved.Request); _collaborationId = saved.CollaborationId; _linkCommand = saved.LinkCommand;
                ConsentEvidenceId = Draft.PrivacyConsentEvidenceId; CollectionConsent = true; AgeConfirmed = true;
                DispatchRequested = true; DirectPaymentAgreed = Draft.DirectPaymentAgreed;
                if (Draft.Pickup.시간창 is { } pickup) { PickupStart = pickup.시작일시.ToLocalTime(); PickupEnd = pickup.종료일시.ToLocalTime(); }
                if (Draft.Dropoff.시간창 is { } dropoff) { DropoffStart = dropoff.시작일시.ToLocalTime(); DropoffEnd = dropoff.종료일시.ToLocalTime(); }
            }
            var result = await client.ReadSubmissionAsync(record.RequestId, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return false;
            if (result is not null && string.IsNullOrWhiteSpace(result.RequestId)) throw new InvalidDataException("원 배송 접수 결과를 확인하지 못했습니다.");
            RegistrationRecoveryPending = result?.ProposalStateCode == NeighborhoodDeliveryProposalStates.RegistrationPending;
            Submitted = RegistrationRecoveryPending ? null : result; RecoveryCheckedMissing = result is null;
            if (_collaborationId is not null && collaborations is not null)
            {
                _collaboration = await collaborations.ReadAsync(_collaborationId, _lifetime.Token);
                if (!OwnerMatches(generation, owner)) return false;
            }
            _recoveryLoaded = true;
            if (RegistrationRecoveryPending) Error = RegistrationPendingNotice;
            else if (Submitted is not null) Error = null;
            else Error = RecoveryExpired ? "이전 입력의 보관 기간이 지나 원문은 삭제했습니다. 원 요청 결과를 확인했으며, 새 작성은 아래에서 선택해 주세요."
                : "이전에 보낸 배송 요청을 복원했습니다. 접수되지 않은 경우 같은 번호와 입력으로 직접 다시 요청할 수 있습니다.";
            return true;
        }
        catch (Exception ex) { if (OwnerMatches(generation, owner)) ApplyError(ex); return false; }
        finally { if (OwnerMatches(generation, owner)) { IsSending = false; Changed(); } }
    }

    public async Task DiscardExpiredRecoveryAsync()
    {
        if (!RecoveryExpired || IsSending || !CheckOwner()) return;
        var owner = _owner!; var id = _expiredRecoveryId!.Value; var generation = ++_generation;
        IsSending = true; Changed();
        try
        {
            // 마지막 확인 후 접수된 결과도 새 작성 전에 다시 확인합니다.
            var result = await client.ReadSubmissionAsync(id, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return;
            if (result is not null)
            {
                RegistrationRecoveryPending = result.ProposalStateCode == NeighborhoodDeliveryProposalStates.RegistrationPending;
                Submitted = RegistrationRecoveryPending ? null : result; RecoveryCheckedMissing = false;
                Error = RegistrationRecoveryPending ? RegistrationPendingNotice : null; return;
            }
            await _recoveryStore.RemoveAsync(owner, RecoveryScope, id, _lifetime.Token);
            if (!OwnerMatches(generation, owner)) return;
            ClearPrivateInput(); _recoveryLoaded = true; Error = null;
        }
        catch (Exception ex) { if (OwnerMatches(generation, owner)) ApplyError(ex); }
        finally { if (OwnerMatches(generation, owner)) { IsSending = false; Changed(); } }
    }

    private Task PersistRecoveryAsync(string owner, CancellationToken ct)
    {
        if (_recoveryRequest is null) return Task.CompletedTask;
        _recoveryCreatedAtUtc ??= DateTime.UtcNow;
        return _recoveryStore.SaveAsync(new(owner, RecoveryScope, _recoveryRequest.ClientRequestId,
            JsonSerializer.Serialize(new AuthoringRecovery(_recoveryRequest, _collaborationId, _linkCommand)), _recoveryCreatedAtUtc.Value), ct);
    }
    private async Task RemoveRecoveryAsync(string owner, CancellationToken ct)
    {
        var id = _recoveryRequest?.ClientRequestId ?? _expiredRecoveryId;
        if (id is not null) await _recoveryStore.RemoveAsync(owner, RecoveryScope, id.Value, ct);
    }
    private sealed record AuthoringRecovery(NeighborhoodDeliveryRequest Request, string? CollaborationId,
        NeighborhoodCollaborationCommandRequest? LinkCommand);
    private string RegistrationPendingNotice => RecoveryExpired
        ? "배송 의뢰는 저장됐지만 배차 접수가 끝나지 않았습니다. 입력 원문의 보관 기간이 지나 다시 보낼 수 없으므로 내 배송 의뢰에서 지원을 요청해 주세요."
        : "배송 의뢰는 저장됐지만 배차 접수가 끝나지 않았습니다. 원 요청과 입력을 유지했습니다. 아래 버튼을 누르면 같은 요청의 배차 접수를 이어갑니다.";
}
