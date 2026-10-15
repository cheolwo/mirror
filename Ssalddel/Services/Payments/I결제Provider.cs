namespace 살뜰.Services.Payments;

public interface I결제Provider
{
    int 제공자유형 { get; }

    Task<결제승인결과> 결제승인Async(결제승인요청 request, CancellationToken cancellationToken = default);
    Task<결제승인결과> 결제조회Async(결제승인요청 request, CancellationToken cancellationToken = default)
        => Task.FromResult(new 결제승인결과(false, "{\"code\":\"PaymentLookupUnavailable\"}", null, null, IsUncertain: true));
}

public sealed record 결제승인요청(string PaymentKey, string OrderId, int Amount);

public sealed record 결제승인결과(bool IsSuccess, string ResponseJson, string? PaymentMethod, string? ExternalTransactionNo,
    string? ProviderStatus = null, string? PaymentKey = null, string? OrderId = null, int? Amount = null,
    string? Currency = null, bool IsUncertain = false, string? DepositCallbackVerificationHash = null);
