using 살뜰.도메인.결제;
using 살뜰.Services.External.Toss;
using 살뜰.Services.Options;

namespace 살뜰.Services.Payments;

public sealed class Toss결제Provider : I결제Provider
{
    private readonly ITossPaymentsService _tossPaymentsService;
    private readonly ISsalddelExecutionModePolicy _executionModePolicy;

    public Toss결제Provider(
        ITossPaymentsService tossPaymentsService,
        ISsalddelExecutionModePolicy executionModePolicy)
    {
        _tossPaymentsService = tossPaymentsService;
        _executionModePolicy = executionModePolicy;
    }

    public int 제공자유형 => 결제공통정의.결제제공자.TossPayments;

    public async Task<결제승인결과> 결제승인Async(결제승인요청 request, CancellationToken cancellationToken = default)
    {
        if (!_executionModePolicy.IsOperational)
        {
            return new 결제승인결과(
                false,
                "{\"code\":\"OperationalModeRequired\",\"message\":\"Toss payment approval is disabled in Simulation mode.\"}",
                null,
                null);
        }

        var result = await _tossPaymentsService.ConfirmAsync(new TossConfirmApiRequest(
            request.PaymentKey,
            request.OrderId,
            request.Amount), cancellationToken);
        return Validate(result, request);
    }
    public async Task<결제승인결과> 결제조회Async(결제승인요청 request, CancellationToken cancellationToken = default)
    {
        if (!_executionModePolicy.IsOperational) return new(false, "{\"code\":\"OperationalModeRequired\"}", null, null);
        return Validate(await _tossPaymentsService.GetAsync(request.PaymentKey, cancellationToken), request);
    }
    private static 결제승인결과 Validate(TossConfirmResult result, 결제승인요청 expected)
    {
        var sanitized = TossPaymentResponsePolicy.Sanitize(result.ResponseJson, out var secretHash);
        if (!result.IsSuccess) return new(false, sanitized, null, null, IsUncertain: result.IsUncertain);
        if (!TossPaymentResponsePolicy.TryRead(sanitized, out var fact) || fact!.PaymentKey != expected.PaymentKey
            || fact.OrderId != expected.OrderId || fact.TotalAmount != expected.Amount)
            return new(false, "{\"code\":\"PaymentEvidenceMismatch\"}", null, null, IsUncertain: true);
        return new(true, sanitized, fact.Method, fact.PaymentKey, fact.Status, fact.PaymentKey, fact.OrderId,
            fact.TotalAmount, fact.Currency, DepositCallbackVerificationHash: result.DepositCallbackVerificationHash ?? secretHash);
    }
}
