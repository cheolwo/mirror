using 살뜰.도메인.결제;

namespace 살뜰.Services.Payments;

public interface I공통결제Service
{
    Task<결제승인결과> 결제승인Async(int 결제제공자유형, 결제승인요청 request, CancellationToken cancellationToken = default);
    Task<결제승인결과> 결제조회Async(int 결제제공자유형, 결제승인요청 request, CancellationToken cancellationToken = default)
        => Task.FromResult(new 결제승인결과(false, "{\"code\":\"PaymentLookupUnavailable\"}", null, null, IsUncertain: true));
}

public sealed class 공통결제Service : I공통결제Service
{
    private readonly IReadOnlyDictionary<int, I결제Provider> _providers;

    public 공통결제Service(IEnumerable<I결제Provider> providers)
    {
        _providers = providers.ToDictionary(x => x.제공자유형);
    }

    public async Task<결제승인결과> 결제승인Async(int 결제제공자유형, 결제승인요청 request, CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(결제제공자유형, out var provider))
        {
            throw new InvalidOperationException($"지원하지 않는 결제 제공자입니다: {결제제공자유형}");
        }

        return await provider.결제승인Async(request, cancellationToken);
    }
    public Task<결제승인결과> 결제조회Async(int 결제제공자유형, 결제승인요청 request, CancellationToken cancellationToken = default)
        => _providers.TryGetValue(결제제공자유형, out var provider)
            ? provider.결제조회Async(request, cancellationToken)
            : Task.FromResult(new 결제승인결과(false, "{\"code\":\"PaymentLookupUnavailable\"}", null, null, IsUncertain: true));
}
