using System.Text.Json;
using Ssalddel.Contracts.Mart;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>수령 해결 한 건의 조회·명시 확인과 미확정 요청 복구만 담당합니다.</summary>
public sealed class 마트수령문제해결ViewModel(I마트수령문제해결Client client, IWorkflowRecoveryStore recovery,
    ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    private WorkflowRecoveryRecord? pending;
    public long OrderId { get; private set; }
    public 마트수령문제해결Dto? Value { get; private set; }
    public bool HasPending => pending is not null;
    public string? CurrentUserId => Owner;
    public bool CanAct => IsAuthenticated && !IsLoading && !IsSending && !HasPending && Value?.MartOrderId == OrderId;
    public string? ValidationError { get; private set; }
    private string Scope => "mart-receipt-resolution:" + OrderId;
    protected override void ResetForOwner() { pending = null; Value = null; OrderId = 0; ValidationError = null; }

    public async Task LoadAsync(long id)
    {
        SynchronizeOwner(); if (Disposed || IsSending || !CheckOwner() || id <= 0) return;
        OrderId = id; var operation = Begin(); Value = null; pending = null; IsLoading = true; Error = null; Notice = null; Changed();
        try
        {
            var stored = await recovery.LoadAsync(operation.Owner!, Scope, Token);
            if (!Current(operation)) return;
            if (stored is not null && (stored.Owner != operation.Owner || stored.Scope != Scope || stored.Target != id.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                throw new InvalidOperationException("이전 요청의 주문 연결을 확인하지 못했습니다.");
            pending = stored;
            var value = await client.ReadAsync(id, Token);
            if (!Current(operation)) return;
            Validate(value, id); Value = value;
            if (pending is not null) Notice = "이전 요청 결과를 먼저 확인해 주세요. 확인 전에는 새 요청을 보내지 않습니다.";
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }

    public async Task ProposeAsync(bool full, long quantity, string? evidence, bool acknowledged)
    {
        SynchronizeOwner();
        if (!CanAct || Value?.CanProposeResolution != true) return;
        if (!acknowledged || !NoteValid(evidence) || quantity < 0 || quantity > Value.TotalOrderQuantity || full && quantity != Value.TotalOrderQuantity)
        { ValidationError = "실제 확인 수량과 개인정보 없는 확인 근거를 입력하고 내용을 확인해 주세요."; Changed(); return; }
        var request = new 마트수령해결제안요청 { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = Value.AttemptStableId,
            ExpectedRevision = Value.Revision, OutcomeCode = full ? MartReceiptResolutionCodes.FullReceiptReconfirmed : MartReceiptResolutionCodes.OperationalReview,
            ConfirmedQuantity = quantity, EvidenceNote = evidence!.Trim() };
        await SendAsync(request.ClientRequestId, request.ExpectedRevision, request.ExpectedAttemptStableId,
            () => client.ProposeAsync(OrderId, request, Token));
    }

    public async Task ConsentAsync(bool granted, long quantity, string? evidence, bool acknowledged)
    {
        SynchronizeOwner();
        if (!CanAct || Value?.CanConsentResolution != true || Value.ProposalId is null) return;
        if (!acknowledged || !NoteValid(evidence) || quantity != Value.ProposedQuantity)
        { ValidationError = "제안 수량과 본인이 확인한 수량을 대조하고 개인정보 없는 확인 근거를 남겨 주세요."; Changed(); return; }
        var request = new 마트수령해결동의요청 { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = Value.AttemptStableId,
            ExpectedRevision = Value.Revision, ProposalId = Value.ProposalId, Granted = granted, ConfirmedQuantity = quantity, EvidenceNote = evidence!.Trim() };
        await SendAsync(request.ClientRequestId, request.ExpectedRevision, request.ExpectedAttemptStableId,
            () => client.ConsentAsync(OrderId, request, Token));
    }

    private async Task SendAsync(Guid id, long revision, string attempt, Func<Task<마트수령문제해결Dto?>> send)
    {
        var operation = Begin(); var scope = Scope; var orderId = OrderId;
        IsSending = true; Error = null; Notice = null; ValidationError = null; Changed();
        // 재전송 원문·근거 진술은 저장하지 않습니다. 결과 조회에 필요한 번호와 판본만 보호 저장합니다.
        var record = new WorkflowRecoveryRecord(operation.Owner!, scope, id,
            JsonSerializer.Serialize(new PendingProof(attempt, revision)), DateTime.UtcNow, orderId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var stored = false;
        try
        {
            await recovery.SaveAsync(record, Token); stored = true;
            if (!Current(operation)) return;
            pending = record; Changed();
            var response = await send(); if (!Current(operation)) return;
            await CompleteAsync(response, record, operation, orderId);
        }
        catch (Exception ex)
        {
            if (!Current(operation)) return;
            if (stored && ConfirmedRejection(ex))
            {
                try
                {
                    await recovery.RemoveAsync(operation.Owner!, scope, id, Token);
                    if (!Current(operation)) return;
                    pending = null; Value = null; // 낡은 판본으로 다음 버튼을 누르지 못하게 합니다.
                }
                catch (Exception storageError) { if (Current(operation)) ApplyError(storageError, true); return; }
            }
            ApplyError(ex, true);
        }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }

    public async Task RecoverAsync()
    {
        SynchronizeOwner(); if (IsSending || IsLoading || !CheckOwner() || pending is not { } record) return;
        var operation = Begin(); var id = OrderId; IsSending = true; Error = null; Notice = null; Changed();
        try
        {
            var value = await client.ResultAsync(id, record.RequestId, Token);
            if (!Current(operation)) return;
            if (value is null) { Notice = "처리 기록이 아직 확인되지 않았습니다. 새 요청을 보내지 않고 결과를 다시 확인해 주세요."; return; }
            await CompleteAsync(value, record, operation, id);
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }

    private async Task CompleteAsync(마트수령문제해결Dto? response, WorkflowRecoveryRecord record,
        (long Generation, string? Owner) operation, long orderId)
    {
        Validate(response, orderId);
        var proof = record.PayloadJson is null ? null : JsonSerializer.Deserialize<PendingProof>(record.PayloadJson);
        if (response!.ResponseRequestId != record.RequestId || proof is not null && (response.AttemptStableId != proof.AttemptStableId || response.Revision <= proof.ExpectedRevision))
            throw new InvalidOperationException("수령 해결 요청의 처리 결과 연결을 확인하지 못했습니다.");
        var latest = await client.ReadAsync(orderId, Token);
        if (!Current(operation)) return;
        Validate(latest, orderId);
        if (latest!.AttemptStableId != response.AttemptStableId || latest.Revision < response.Revision)
            throw new InvalidOperationException("현재 수령 해결 정본을 확인하지 못했습니다.");
        await recovery.RemoveAsync(operation.Owner!, record.Scope, record.RequestId, Token);
        if (!Current(operation)) return;
        pending = null; Value = latest; Notice = "요청 기록과 현재 해결 상태를 확인했습니다. 정산·입금은 별도로 확인합니다.";
    }
    private static bool NoteValid(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 240 && !value.Any(char.IsControl);
    private static void Validate(마트수령문제해결Dto? value, long id)
    {
        if (value is null || value.MartOrderId != id || value.MyRole is not ("Buyer" or "Seller") || value.Revision < 0
            || value.PaymentReleaseReady && value.ResolutionStatus != "ResolvedFullReceipt" && value.HoldRecorded)
            throw new InvalidOperationException("수령 해결 정보의 주문 연결을 확인하지 못했습니다.");
    }
    private static bool ConfirmedRejection(Exception ex)
    {
        if (ex is not SsalddelApiException { StatusCode: 400 or 404 or 409 or 422 } api) return false;
        try { using var json = JsonDocument.Parse(api.ResponseBody); return json.RootElement.TryGetProperty("commandRejected", out var value) && value.ValueKind == JsonValueKind.True; }
        catch (JsonException) { return false; }
    }
    public sealed record PendingProof(string AttemptStableId, long ExpectedRevision);
}
