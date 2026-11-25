using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>과거 설명의 조회와 의견 접수만 담당합니다. 의견은 원 배차를 변경하지 않습니다.</summary>
public sealed class CargoDispatchExplanationFeedbackViewModel : IDisposable
{
    private readonly ICargoDispatchExplanationClient client;
    private readonly IWorkflowRecoveryStore recovery;
    private readonly IWorkflowRecoveryContext context;
    private CancellationTokenSource lifetime = new();
    private WorkflowRecoverySnapshot? loadedContext;
    private PendingFeedback? pending;
    private string requestId = string.Empty;
    private long generation, selection;
    private bool disposed;

    public CargoDispatchExplanationFeedbackViewModel(ICargoDispatchExplanationClient client,
        IWorkflowRecoveryStore recovery, IWorkflowRecoveryContext context)
    {
        this.client = client; this.recovery = recovery; this.context = context;
        client.SessionChanged += OnSessionChanged;
    }

    public event Action? Changed;
    public CargoDispatchExplanationResponse? Explanation { get; private set; }
    public CargoDispatchFeedbackReceipt? Receipt { get; private set; }
    public string RequestId => requestId;
    public string SelectedEvidenceId { get; private set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string? CommentValidation => Comment.Length > 500 ? "추가 의견은 500자 이내로 입력해 주세요."
        : Comment.Any(char.IsControl) ? "추가 의견은 줄바꿈·제어문자 없이 입력해 주세요." : null;
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsSending { get; private set; }
    public bool IsBusy => IsLoading || IsSending;
    public bool RequiresLogin => !client.Identity.IsAuthenticated;
    public bool HasPending => pending is not null;
    public Guid? PendingRequestId => pending?.ClientRequestId;
    public bool PendingInputExpired => pending is { Payload: null };
    public bool CanRetry { get; private set; }
    public bool CanDiscardExpired { get; private set; }
    public bool CanSubmit => !IsBusy && !HasPending && !RequiresLogin && loadedContext is not null && context.IsCurrent(loadedContext)
        && SelectedItem is { CanSubmitFeedback: true } && CargoDispatchFeedbackCategories.All.Contains(CategoryCode)
        && CommentValidation is null;
    public CargoDispatchExplanationItem? SelectedItem => Explanation?.Items.FirstOrDefault(x => x.EvidenceId == SelectedEvidenceId);

    public async Task LoadAsync(string? id)
    {
        if (disposed || IsSending && id == requestId) return;
        var normalized = id?.Trim() ?? string.Empty;
        var ticket = ++selection;
        var keepDraft = normalized == requestId && loadedContext is not null && context.IsCurrent(loadedContext);
        ClearCurrent(keepDraft);
        requestId = normalized;
        if (string.IsNullOrWhiteSpace(requestId)) { Notify(); return; }
        IsLoading = true; Notify();
        try
        {
            // 인증 복원 자체는 앱 수명 작업이다. 복원이 발생시키는 SessionChanged가
            // 현재 조회 토큰을 취소해도, 결과는 선택 번호와 새 문맥을 확인한 뒤 읽는다.
            await client.RestoreAsync(CancellationToken.None);
            if (disposed || ticket != selection || requestId != normalized) return;
            var captured = context.Capture();
            var currentGeneration = generation;
            var ct = lifetime.Token;
            if (!client.Identity.IsAuthenticated) throw new InvalidOperationException();
            IsLoading = true;
            var response = await client.ReadAsync(normalized, ct);
            if (!Current(currentGeneration, normalized, captured)) return;
            if (response.Items.Any(x => x.RequestId != normalized || string.IsNullOrWhiteSpace(x.EvidenceId))
                || response.Items.Select(x => x.EvidenceId).Distinct(StringComparer.Ordinal).Count() != response.Items.Count)
                throw new InvalidDataException();
            Explanation = response;
            loadedContext = captured;
            if (!response.Items.Any(x => x.EvidenceId == SelectedEvidenceId))
                SelectedEvidenceId = response.Items.FirstOrDefault(x => x.CanSubmitFeedback)?.EvidenceId
                    ?? response.Items.FirstOrDefault()?.EvidenceId ?? string.Empty;
            var record = await recovery.LoadAsync(captured.Owner, Scope(captured, normalized), ct);
            if (!Current(currentGeneration, normalized, captured)) return;
            if (record is not null)
            {
                if (record.Owner != captured.Owner || record.Scope != Scope(captured, normalized)
                    || record.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(record.Target)) throw new InvalidDataException();
                var saved = record.PayloadJson is null ? null : JsonSerializer.Deserialize<PendingPayload>(record.PayloadJson);
                if (saved is not null && (saved.RequestId != normalized || saved.Request.ClientRequestId != record.RequestId
                    || saved.Request.EvidenceId != record.Target)) throw new InvalidDataException();
                pending = new(record.RequestId, record.Target, saved?.Request, record.CreatedAtUtc);
                SelectedEvidenceId = record.Target;
                CategoryCode = saved?.Request.CategoryCode ?? string.Empty;
                Comment = saved?.Request.Comment ?? string.Empty;
                await CheckPendingCoreAsync(currentGeneration, normalized, captured, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ReceiptMismatchException)
        {
            if (!disposed && ticket == selection && requestId == normalized) Error = ReceiptMismatchNotice;
        }
        catch
        {
            if (!disposed && ticket == selection && requestId == normalized)
                Error = RequiresLogin ? "로그인한 뒤 같은 의뢰를 다시 확인해 주세요." : "배차 설명을 확인하지 못했습니다. 다시 확인해 주세요.";
        }
        finally
        {
            if (!disposed && ticket == selection && requestId == normalized) { IsLoading = false; Notify(); }
        }
    }

    public void SelectEvidence(string evidenceId)
    {
        if (IsBusy || HasPending || Explanation?.Items.Any(x => x.EvidenceId == evidenceId) != true) return;
        SelectedEvidenceId = evidenceId; CategoryCode = string.Empty; Comment = string.Empty;
        Receipt = null; Error = null; Notice = null; Notify();
    }

    public async Task SubmitAsync()
    {
        if (!CanSubmit || loadedContext is not { } captured || !context.IsCurrent(captured)) return;
        var id = requestId; var currentGeneration = generation; var ct = lifetime.Token;
        var payload = new CargoDispatchFeedbackRequest
        {
            ClientRequestId = Guid.NewGuid(), EvidenceId = SelectedEvidenceId, CategoryCode = CategoryCode,
            Comment = string.IsNullOrEmpty(Comment) ? null : Comment
        };
        IsSending = true; Error = null; Notice = null; Receipt = null; Notify();
        try
        {
            var created = DateTime.UtcNow;
            await recovery.SaveAsync(new(captured.Owner, Scope(captured, id), payload.ClientRequestId,
                JsonSerializer.Serialize(new PendingPayload(id, payload)), created, payload.EvidenceId), ct);
            if (!Current(currentGeneration, id, captured)) return;
            pending = new(payload.ClientRequestId, payload.EvidenceId, payload, created);
            var result = await client.SubmitAsync(id, payload, ct);
            if (!Current(currentGeneration, id, captured)) return;
            await CompleteAsync(result, currentGeneration, id, captured, ct);
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
        {
            if (Current(currentGeneration, id, captured))
                await ReleaseRejectedAsync(currentGeneration, id, captured, ex.StatusCode.Value, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            if (Current(currentGeneration, id, captured)) Error = "같은 요청 번호의 다른 입력과 충돌했습니다. 먼저 접수 기록을 확인해 주세요.";
        }
        catch (ReceiptMismatchException) { if (Current(currentGeneration, id, captured)) Error = ReceiptMismatchNotice; }
        catch
        {
            if (Current(currentGeneration, id, captured))
                Error = HasPending ? "접수 결과가 확인되지 않았습니다. 입력과 요청 번호를 보존했습니다. 먼저 접수 여부를 확인해 주세요."
                    : "의견을 보내기 전에 입력을 보존하지 못했습니다. 다시 확인해 주세요.";
        }
        finally { if (Current(currentGeneration, id, captured)) { IsSending = false; Notify(); } }
    }

    public async Task CheckPendingAsync()
    {
        if (IsBusy || pending is null || loadedContext is not { } captured || !context.IsCurrent(captured)) return;
        var id = requestId; var currentGeneration = generation; var ct = lifetime.Token;
        IsSending = true; Error = null; Notify();
        try { await CheckPendingCoreAsync(currentGeneration, id, captured, ct); }
        catch (OperationCanceledException) { }
        catch (ReceiptMismatchException) { if (Current(currentGeneration, id, captured)) Error = ReceiptMismatchNotice; }
        catch { if (Current(currentGeneration, id, captured)) Error = "접수 여부를 확인하지 못했습니다. 같은 요청을 보존했습니다."; }
        finally { if (Current(currentGeneration, id, captured)) { IsSending = false; Notify(); } }
    }

    public async Task RetryAsync()
    {
        if (IsBusy || !CanRetry || pending?.Payload is not { } payload || loadedContext is not { } captured || !context.IsCurrent(captured)) return;
        var id = requestId; var currentGeneration = generation; var ct = lifetime.Token;
        var postStarted = false;
        IsSending = true; Error = null; CanRetry = false; Notify();
        try
        {
            // 다시 보내기 직전에도 같은 요청의 결과를 먼저 조회합니다.
            await CheckPendingCoreAsync(currentGeneration, id, captured, ct);
            if (!Current(currentGeneration, id, captured) || pending is null || !CanRetry) return;
            CanRetry = false;
            postStarted = true;
            var result = await client.SubmitAsync(id, payload, ct);
            if (Current(currentGeneration, id, captured)) await CompleteAsync(result, currentGeneration, id, captured, ct);
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException ex) when (postStarted && (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden))
        {
            if (Current(currentGeneration, id, captured))
                await ReleaseRejectedAsync(currentGeneration, id, captured, ex.StatusCode.Value, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            if (Current(currentGeneration, id, captured)) Error = "같은 요청 번호의 다른 입력과 충돌했습니다. 먼저 접수 기록을 확인해 주세요.";
        }
        catch (ReceiptMismatchException) { if (Current(currentGeneration, id, captured)) Error = ReceiptMismatchNotice; }
        catch { if (Current(currentGeneration, id, captured)) Error = "접수 결과가 확인되지 않았습니다. 먼저 같은 요청의 접수 여부를 확인해 주세요."; }
        finally { if (Current(currentGeneration, id, captured)) { IsSending = false; Notify(); } }
    }

    public async Task DiscardExpiredAsync()
    {
        if (IsBusy || !CanDiscardExpired || pending is not { Payload: null } saved
            || loadedContext is not { } captured || !context.IsCurrent(captured)) return;
        var id = requestId; var currentGeneration = generation; var ct = lifetime.Token;
        IsSending = true; Error = null; Notify();
        try
        {
            await CheckPendingCoreAsync(currentGeneration, id, captured, ct);
            if (!Current(currentGeneration, id, captured) || pending is null || !CanDiscardExpired) return;
            await recovery.RemoveAsync(captured.Owner, Scope(captured, id), saved.ClientRequestId, ct);
            if (!Current(currentGeneration, id, captured)) return;
            pending = null; CanDiscardExpired = false; CategoryCode = string.Empty; Comment = string.Empty;
            Notice = "이전 요청의 접수 기록이 없어 새 의견을 작성할 수 있습니다.";
        }
        catch (OperationCanceledException) { }
        catch { if (Current(currentGeneration, id, captured)) Error = "이전 요청을 확인하지 못했습니다. 다시 확인해 주세요."; }
        finally { if (Current(currentGeneration, id, captured)) { IsSending = false; Notify(); } }
    }

    private async Task CheckPendingCoreAsync(long currentGeneration, string id, WorkflowRecoverySnapshot captured, CancellationToken ct)
    {
        if (pending is not { } saved) return;
        CanRetry = CanDiscardExpired = false;
        var result = await client.ReadFeedbackAsync(id, saved.EvidenceId, saved.ClientRequestId, ct);
        if (!Current(currentGeneration, id, captured)) return;
        if (result is not null) { await CompleteAsync(result, currentGeneration, id, captured, ct); return; }
        CanRetry = saved.Payload is not null && SelectedItem?.CanSubmitFeedback == true;
        CanDiscardExpired = saved.Payload is null;
        Notice = saved.Payload is null ? "이전 입력의 보관 기간이 지났으며 접수 기록은 확인되지 않았습니다. 새 작성은 직접 선택해 주세요."
            : "접수 기록이 아직 없습니다. 보존한 같은 번호와 입력으로 직접 다시 보낼 수 있습니다.";
    }

    private async Task CompleteAsync(CargoDispatchFeedbackReceipt result, long currentGeneration, string id,
        WorkflowRecoverySnapshot captured, CancellationToken ct)
    {
        if (pending is not { } saved || result.ClientRequestId != saved.ClientRequestId || result.EvidenceId != saved.EvidenceId
            || result.Status != "received" || string.IsNullOrWhiteSpace(result.FeedbackId)
            || !CargoDispatchFeedbackCategories.All.Contains(result.CategoryCode)
            || result.SubmissionFingerprint is not { Length: 64 }
            || !result.SubmissionFingerprint.All(char.IsAsciiHexDigit)
            || saved.Payload is { } payload && (result.CategoryCode != payload.CategoryCode
                || result.SubmissionFingerprint != CargoDispatchFeedbackFingerprint.Compute(payload.CategoryCode, payload.Comment)))
            throw new ReceiptMismatchException();
        var expired = saved.Payload is null;
        await recovery.RemoveAsync(captured.Owner, Scope(captured, id), saved.ClientRequestId, ct);
        if (!Current(currentGeneration, id, captured)) return;
        Receipt = result; pending = null; CanRetry = CanDiscardExpired = false; CategoryCode = string.Empty; Comment = string.Empty;
        Notice = expired ? "이전 요청의 접수 기록이 확인되었습니다. 입력 보관 기간이 지나 원문 내용은 확인하지 않았습니다."
            : "의견이 접수되었습니다. 배차·운임 변경이나 운영자 검토 완료를 의미하지 않습니다.";
        Error = null;
    }

    private bool Current(long currentGeneration, string id, WorkflowRecoverySnapshot captured)
        => !disposed && currentGeneration == generation && id == requestId && context.IsCurrent(captured);
    private static string Scope(WorkflowRecoverySnapshot snapshot, string id)
        => snapshot.Scope("cargo-feedback.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..24].ToLowerInvariant());
    private void OnSessionChanged()
    {
        if (disposed) return;
        ClearCurrent(); Notice = "로그인 정보가 변경되었습니다. 같은 의뢰를 다시 확인해 주세요."; Notify();
    }
    private void ClearCurrent(bool keepDraft = false)
    {
        generation++; lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        Explanation = null; Receipt = null; pending = null; loadedContext = null; IsLoading = IsSending = false;
        Error = Notice = null; CanRetry = CanDiscardExpired = false;
        if (!keepDraft) { SelectedEvidenceId = string.Empty; CategoryCode = string.Empty; Comment = string.Empty; }
    }
    private void Notify() => Changed?.Invoke();
    private async Task ReleaseRejectedAsync(long currentGeneration, string id, WorkflowRecoverySnapshot captured, HttpStatusCode status, CancellationToken ct)
    {
        if (pending is not { } saved) return;
        try
        {
            await recovery.RemoveAsync(captured.Owner, Scope(captured, id), saved.ClientRequestId, ct);
            if (!Current(currentGeneration, id, captured)) return;
            pending = null; CanRetry = CanDiscardExpired = false; Receipt = null; Notice = null;
            Error = status == HttpStatusCode.BadRequest
                ? "서버가 입력을 거절해 접수되지 않았습니다. 입력을 확인·수정한 뒤 다시 보내 주세요."
                : "현재 권한으로 의견을 접수할 수 없습니다. 입력은 유지했으며 권한을 다시 확인해 주세요.";
        }
        catch (OperationCanceledException) { }
        catch { if (Current(currentGeneration, id, captured)) Error = "입력이 거절되었지만 기기 기록을 정리하지 못했습니다. 접수 여부를 다시 확인해 주세요."; }
    }
    private const string ReceiptMismatchNotice = "보존한 입력과 다른 접수 기록입니다. 같은 내용이 접수된 것으로 표시하지 않았습니다. 확인이 필요합니다.";
    private sealed class ReceiptMismatchException : Exception { }
    public void Dispose()
    {
        if (disposed) return; disposed = true; selection++; generation++; lifetime.Cancel(); lifetime.Dispose();
        client.SessionChanged -= OnSessionChanged; Explanation = null; Receipt = null; pending = null; Comment = string.Empty;
    }
    private sealed record PendingPayload(string RequestId, CargoDispatchFeedbackRequest Request);
    private sealed record PendingFeedback(Guid ClientRequestId, string EvidenceId, CargoDispatchFeedbackRequest? Payload, DateTime CreatedAtUtc);
}
