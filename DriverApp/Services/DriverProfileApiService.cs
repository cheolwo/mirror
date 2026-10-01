using Hongdal.Contracts.Driver.Home;
using Hongdal.Contracts.Driver.Profile;

namespace DriverApp.Services;

public interface IDriverProfileApiService
{
    Task<기사홈요약응답?> 홈조회Async(CancellationToken cancellationToken = default);
    Task<용달기사등록응답?> 내프로필조회Async(CancellationToken cancellationToken = default);
    Task<용달기사등록응답?> 등록Async(
        용달기사등록요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class DriverProfileApiService : IDriverProfileApiService
{
    private readonly IDriverApiClient _client;

    public DriverProfileApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public Task<기사홈요약응답?> 홈조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사홈요약응답>(
            "api/v1/driver/home",
            "기사 홈 조회",
            cancellationToken);

    public Task<용달기사등록응답?> 내프로필조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<용달기사등록응답>(
            "api/v1/drivers/me",
            "기사 프로필 조회",
            cancellationToken);

    public Task<용달기사등록응답?> 등록Async(
        용달기사등록요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<용달기사등록요청, 용달기사등록응답>(
            "api/v1/drivers/register",
            request,
            "기사 프로필 등록",
            cancellationToken);
}
