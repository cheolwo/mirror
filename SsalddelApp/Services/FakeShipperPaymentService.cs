using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure;
using Ssalddel.Contracts.Common.Payments;
using Ssalddel.Contracts.Shipper.Payment;
using Microsoft.Extensions.Options;
using SsalddelApp.Models.Shipper;
using Ssalddel.Ui.Common.Areas.App.Services;
using System.Security.Cryptography;
using System.Text;

namespace SsalddelApp.Services;

public sealed class FakeShipperPaymentService
{
    public const string SecuredStatus = "결제완료";

    private readonly ConcurrentDictionary<ReceiptScope, FakeShipperPaymentReceipt> _receipts = new();
    private readonly ConcurrentDictionary<(string Owner, string Scope), WorkflowRecoveryRecord> _pending = new();
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;
    private readonly IOptions<ClientDataModeOptions> _dataModeOptions;
    private readonly IWorkflowRecoveryStore? _recovery;
    private readonly IWorkflowRecoveryContext? _context;

    public FakeShipperPaymentService(
        HttpClient httpClient,
        IAuthSession authSession,
        IOptions<ClientDataModeOptions> dataModeOptions,
        IWorkflowRecoveryStore? recovery = null,
        IWorkflowRecoveryContext? context = null)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _dataModeOptions = dataModeOptions;
        _recovery = recovery;
        _context = context;
    }

    public FakeShipperPaymentReceipt? GetReceipt(string? requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        return _receipts.TryGetValue(CurrentReceiptScope(requestId.Trim()), out var receipt)
            ? receipt
            : null;
    }

    /// <summary>같은 계정·서버·의뢰의 미확정 기록만 읽습니다. 결제 성공을 추정하거나 재전송하지 않습니다.</summary>
    public async Task<bool> HasPendingAsync(string requestId, CancellationToken cancellationToken = default)
    {
        var identity = CaptureContext();
        var record = await ReadPendingAsync(identity, requestId, cancellationToken);
        EnsureCurrent(identity);
        return record is not null;
    }

    public async Task<FakeShipperPaymentReceipt> ConfirmAsync(
        ShipperRequestItem request,
        PaymentRequestPlan plan,
        string? memo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.의뢰Id))
        {
            throw new InvalidOperationException("Fake 결제를 승인할 의뢰 ID가 없습니다.");
        }

        if (plan.Draft.TargetId != request.의뢰Id || plan.Draft.Amount <= 0 || plan.Draft.Currency != "KRW")
            throw new InvalidOperationException("같은 의뢰의 원화 결제 조건을 다시 확인해 주세요.");

        var receiptScope = CurrentReceiptScope(request.의뢰Id);
        if (string.IsNullOrWhiteSpace(_authSession.AccessToken) && CanUseLocalFakePayment())
            return CreateLocalSmokeReceipt(request, plan, memo, receiptScope);

        var identity = CaptureContext();
        var accessToken = _authSession.AccessToken;
        var scope = PaymentScope(identity, request.의뢰Id);
        if (await ReadPendingAsync(identity, request.의뢰Id, cancellationToken) is not null)
            throw new InvalidOperationException("이전 테스트 결제 결과부터 확인해 주세요. 다시 전송하지 않습니다.");
        EnsureCurrent(identity);
        var record = new WorkflowRecoveryRecord(identity.Owner, scope, Guid.NewGuid(), null, DateTime.UtcNow, request.의뢰Id);
        await _recovery!.SaveAsync(record, cancellationToken);
        _pending[(identity.Owner, scope)] = record;
        EnsureCurrent(identity);
        var stored = await ReadPendingAsync(identity, request.의뢰Id, cancellationToken);
        EnsureCurrent(identity);
        if (stored?.RequestId != record.RequestId)
            throw new InvalidDataException("결제 요청 보호 기록을 확인하지 못해 전송하지 않았습니다.");

        var payload = new 페이크결제승인요청
        {
            의뢰Id = request.의뢰Id,
            Amount = plan.Draft.Amount,
            결제수단 = ToPaymentMethodText(plan.Draft.PaymentMethod),
            메모 = memo,
            IdempotencyKey = "shipper-fake-" + record.RequestId.ToString("N")
        };

        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/payments/fake/confirm", accessToken);
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("테스트 결제 응답을 확인하지 못했습니다. 같은 의뢰의 결과를 다시 확인해 주세요.");
        }

        var result = await response.Content.ReadFromJsonAsync<페이크결제승인응답>(cancellationToken)
                     ?? throw new InvalidOperationException("서버 FakePG 승인 응답이 비어 있습니다.");
        EnsureCurrent(identity);
        if (result.의뢰Id != request.의뢰Id || result.Amount != plan.Draft.Amount
            || result.결제상태 != SecuredStatus || string.IsNullOrWhiteSpace(result.결제Id)
            || string.IsNullOrWhiteSpace(result.OrderId) || string.IsNullOrWhiteSpace(result.PaymentKey)
            || result.승인일시Utc == default)
            throw new InvalidDataException("같은 의뢰의 테스트 결제 결과를 확인하지 못했습니다.");

        var receipt = new FakeShipperPaymentReceipt(
            ReceiptId: result.결제Id,
            RequestId: request.의뢰Id,
            OrderId: result.OrderId,
            Amount: result.Amount,
            Currency: plan.Draft.Currency,
            PaymentMethod: plan.Draft.PaymentMethod,
            PaymentMethodText: ToPaymentMethodText(plan.Draft.PaymentMethod),
            SettlementMode: plan.Draft.SettlementMode,
            SettlementModeText: ToSettlementModeText(plan.Draft.SettlementMode),
            PaymentStatus: result.결제상태,
            ProviderTransactionKey: result.PaymentKey,
            ApprovedAt: ToLocalTime(result.승인일시Utc),
            PayerMemo: string.IsNullOrWhiteSpace(memo) ? null : memo.Trim());

        _receipts[receiptScope] = receipt;
        await _recovery.RemoveAsync(identity.Owner, scope, record.RequestId, cancellationToken);
        EnsureCurrent(identity);
        if (await _recovery.LoadAsync(identity.Owner, scope, cancellationToken) is not null)
            throw new InvalidDataException("결제 결과 확인 기록의 정리를 확인하지 못했습니다.");
        EnsureCurrent(identity);
        _pending.TryRemove((identity.Owner, scope), out _);
        return receipt;
    }

    private FakeShipperPaymentReceipt CreateLocalSmokeReceipt(ShipperRequestItem request, PaymentRequestPlan plan, string? memo, ReceiptScope scope)
    {
        var receipt = new FakeShipperPaymentReceipt(
            ReceiptId: $"smoke-fakepg-{Guid.NewGuid():N}",
            RequestId: request.의뢰Id,
            OrderId: plan.OrderId,
            Amount: plan.Draft.Amount,
            Currency: plan.Draft.Currency,
            PaymentMethod: plan.Draft.PaymentMethod,
            PaymentMethodText: ToPaymentMethodText(plan.Draft.PaymentMethod),
            SettlementMode: plan.Draft.SettlementMode,
            SettlementModeText: ToSettlementModeText(plan.Draft.SettlementMode),
            PaymentStatus: SecuredStatus,
            ProviderTransactionKey: $"smoke-local-{plan.OrderId}",
            ApprovedAt: DateTimeOffset.Now,
            PayerMemo: string.IsNullOrWhiteSpace(memo) ? null : memo.Trim());

        _receipts[scope] = receipt;
        return receipt;
    }

    private ReceiptScope CurrentReceiptScope(string requestId)
        => new(_authSession.UserId ?? string.Empty, _authSession.SessionRevision,
            _httpClient.BaseAddress?.AbsoluteUri ?? string.Empty, requestId);

    private sealed record ReceiptScope(string Owner, long SessionRevision, string Endpoint, string RequestId);

    private WorkflowRecoverySnapshot CaptureContext()
    {
        if (_recovery is null || _context is null || !_authSession.IsLoggedIn)
            throw new InvalidOperationException("로그인과 결제 요청 보호 저장소를 확인해 주세요.");
        var identity = _context.Capture();
        if (identity.Owner != _authSession.UserId || identity.SessionRevision != _authSession.SessionRevision)
            throw new InvalidOperationException("로그인과 결제 요청의 계정이 다릅니다.");
        return identity;
    }

    private void EnsureCurrent(WorkflowRecoverySnapshot identity)
    {
        if (_context?.IsCurrent(identity) != true || !_authSession.IsLoggedIn
            || identity.Owner != _authSession.UserId || identity.SessionRevision != _authSession.SessionRevision)
            throw new OperationCanceledException("계정 또는 연결 서버가 변경되었습니다.");
    }

    private static string PaymentScope(WorkflowRecoverySnapshot identity, string requestId)
        => identity.Scope("shipper.fake-payment." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestId))).ToLowerInvariant()[..32]);

    private async Task<WorkflowRecoveryRecord?> ReadPendingAsync(WorkflowRecoverySnapshot identity, string requestId, CancellationToken ct)
    {
        var scope = PaymentScope(identity, requestId);
        var record = await _recovery!.LoadAsync(identity.Owner, scope, ct);
        EnsureCurrent(identity);
        if (record is null)
        {
            if (_pending.ContainsKey((identity.Owner, scope)))
                throw new InvalidDataException("이전 결제의 보호 기록을 확인하지 못했습니다.");
            return null;
        }
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != identity.Owner || record.Scope != scope || record.Target != requestId || record.PayloadJson is not null)
            throw new InvalidDataException("이전 결제 확인 기록이 현재 의뢰와 다릅니다.");
        _pending[(identity.Owner, scope)] = record;
        return record;
    }

    private bool CanUseLocalFakePayment()
        => _dataModeOptions.Value.CanUseSampleFallback;

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("서버 인증 정보가 없어 FakePG 결제 승인 API를 호출할 수 없습니다.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static DateTimeOffset ToLocalTime(DateTime approvedAtUtc)
    {
        var utc = approvedAtUtc.Kind == DateTimeKind.Utc
            ? approvedAtUtc
            : DateTime.SpecifyKind(approvedAtUtc, DateTimeKind.Utc);
        return new DateTimeOffset(utc).ToLocalTime();
    }

    private static string ToPaymentMethodText(string paymentMethod)
        => paymentMethod switch
        {
            PaymentMethodCode.TossCard => "카드(FakePG)",
            PaymentMethodCode.TossTransfer => "계좌이체(FakePG)",
            PaymentMethodCode.TossVirtualAccount => "가상계좌(FakePG)",
            PaymentMethodCode.TossBilling => "자동결제(FakePG)",
            PaymentMethodCode.MonthlySettlement => "월정산(Fake)",
            _ => $"FakePG {paymentMethod}"
        };

    private static string ToSettlementModeText(string settlementMode)
        => settlementMode switch
        {
            SettlementModeCode.Prepaid => "선결제",
            SettlementModeCode.PayOnCompletion => "완료 후 결제",
            SettlementModeCode.MonthlyInvoice => "월말 청구",
            SettlementModeCode.Subscription => "구독",
            _ => settlementMode
        };
}

public sealed record FakeShipperPaymentReceipt(
    string ReceiptId,
    string RequestId,
    string OrderId,
    int Amount,
    string Currency,
    string PaymentMethod,
    string PaymentMethodText,
    string SettlementMode,
    string SettlementModeText,
    string PaymentStatus,
    string ProviderTransactionKey,
    DateTimeOffset ApprovedAt,
    string? PayerMemo);
