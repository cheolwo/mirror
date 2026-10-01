using Hongdal.Contracts.Driver.Settings;

namespace DriverApp.Services;

public interface IDriverSettingsApiService
{
    Task<기사콜범위응답?> 콜범위조회Async(CancellationToken cancellationToken = default);
    Task<기사콜범위응답?> 콜범위수정Async(
        기사콜범위수정요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class DriverSettingsApiService : IDriverSettingsApiService
{
    private const string CallScopePath = "api/v1/driver/preferences/call-scope";
    private readonly IDriverApiClient _client;

    public DriverSettingsApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public Task<기사콜범위응답?> 콜범위조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사콜범위응답>(CallScopePath, "기사 콜 범위 조회", cancellationToken);

    public Task<기사콜범위응답?> 콜범위수정Async(
        기사콜범위수정요청 request,
        CancellationToken cancellationToken = default)
        => _client.PutAsync<기사콜범위수정요청, 기사콜범위응답>(
            CallScopePath,
            request,
            "기사 콜 범위 수정",
            cancellationToken);
}
