using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Client.Infrastructure;
using Hongdal.Contracts.Common.Payments;
using Hongdal.Contracts.Shipper.Payment;
using Microsoft.Extensions.Options;
using HongdalApp.Models.Shipper;

namespace HongdalApp.Services;

public sealed class FakeShipperPaymentService
{
    public const string SecuredStatus = "결제완료";

    private readonly ConcurrentDictionary<string, FakeShipperPaymentReceipt> _receipts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;
    private readonly IOptions<ClientDataModeOptions> _dataModeOptions;

    public FakeShipperPaymentService(
        HttpClient httpClient,
        IAuthSession authSession,
        IOptions<ClientDataModeOptions> dataModeOptions)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _dataModeOptions = dataModeOptions;
    }

    public FakeShipperPaymentReceipt? GetReceipt(string? requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        return _receipts.TryGetValue(requestId.Trim(), out var receipt)
            ? receipt
            : null;
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

        var payload = new 페이크결제승인요청
        {
            의뢰Id = request.의뢰Id,
            Amount = plan.Draft.Amount,
            결제수단 = ToPaymentMethodText(plan.Draft.PaymentMethod),
            메모 = memo,
            IdempotencyKey = $"shipper-fake-{request.의뢰Id}-{plan.OrderId}"
        };

        if (string.IsNullOrWhiteSpace(_authSession.AccessToken) && CanUseLocalFakePayment())
        {
            return CreateLocalSmokeReceipt(request, plan, memo);
        }

        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/payments/fake/confirm");
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (CanUseLocalFakePayment())
            {
                return CreateLocalSmokeReceipt(request, plan, memo);
            }

            throw new InvalidOperationException(await BuildFailureMessageAsync(response, cancellationToken));
        }

        var result = await response.Content.ReadFromJsonAsync<페이크결제승인응답>(cancellationToken)
                     ?? throw new InvalidOperationException("서버 FakePG 승인 응답이 비어 있습니다.");

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

        StoreReceipt(request, receipt);
        return receipt;
    }

    public bool TryApplyReceipt(ShipperRequestItem? request)
    {
        if (request is null)
        {
            return false;
        }

        var receipt = GetReceipt(request.의뢰Id);
        if (receipt is null)
        {
            return false;
        }

        ApplyReceipt(request, receipt);
        return true;
    }

    private static void ApplyReceipt(ShipperRequestItem request, FakeShipperPaymentReceipt receipt)
    {
        request.결제상태 = string.IsNullOrWhiteSpace(receipt.PaymentStatus) ? SecuredStatus : receipt.PaymentStatus;
        request.결제수단 = receipt.PaymentMethodText;
        request.결제예정금액 ??= receipt.Amount;
    }

    private FakeShipperPaymentReceipt CreateLocalSmokeReceipt(ShipperRequestItem request, PaymentRequestPlan plan, string? memo)
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

        StoreReceipt(request, receipt);
        return receipt;
    }

    private void StoreReceipt(ShipperRequestItem request, FakeShipperPaymentReceipt receipt)
    {
        _receipts[request.의뢰Id] = receipt;
        ApplyReceipt(request, receipt);
    }

    private bool CanUseLocalFakePayment()
        => _dataModeOptions.Value.CanUseSampleFallback;

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            throw new InvalidOperationException("서버 인증 정보가 없어 FakePG 결제 승인 API를 호출할 수 없습니다.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        return request;
    }

    private static async Task<string> BuildFailureMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body)
            ? $"서버 FakePG 승인 요청에 실패했습니다. HTTP {(int)response.StatusCode}"
            : $"서버 FakePG 승인 요청에 실패했습니다. HTTP {(int)response.StatusCode}: {body}";
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
