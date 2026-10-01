using Hongdal.Contracts.Driver.Development;

namespace DriverApp.Services;

public interface IDriverDevelopmentApiService
{
    Task<기사개발스냅샷응답?> 스냅샷조회Async(CancellationToken cancellationToken = default);
}

public sealed class DriverDevelopmentApiService : IDriverDevelopmentApiService
{
    private readonly IDriverApiClient _client;

    public DriverDevelopmentApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public Task<기사개발스냅샷응답?> 스냅샷조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사개발스냅샷응답>(
            "api/v1/driver/dev-snapshot",
            "기사 개발 스냅샷 조회",
            cancellationToken);
}
