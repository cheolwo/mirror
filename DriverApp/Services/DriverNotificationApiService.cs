using Hongdal.Contracts.Driver.Notification;

namespace DriverApp.Services;

public interface IDriverNotificationApiService
{
    Task<기사푸시토큰응답?> 푸시토큰조회Async(CancellationToken cancellationToken = default);
    Task<기사푸시토큰응답?> 푸시토큰등록Async(
        기사푸시토큰등록요청 request,
        CancellationToken cancellationToken = default);
    Task 푸시토큰삭제Async(CancellationToken cancellationToken = default);
    Task<기사알림설정응답?> 설정조회Async(CancellationToken cancellationToken = default);
    Task<기사알림설정응답?> 설정수정Async(
        기사알림설정수정요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class DriverNotificationApiService : IDriverNotificationApiService
{
    private const string BasePath = "api/v1/driver/notifications";
    private readonly IDriverApiClient _client;

    public DriverNotificationApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public Task<기사푸시토큰응답?> 푸시토큰조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사푸시토큰응답>(
            $"{BasePath}/push-token",
            "기사 푸시 토큰 조회",
            cancellationToken);

    public Task<기사푸시토큰응답?> 푸시토큰등록Async(
        기사푸시토큰등록요청 request,
        CancellationToken cancellationToken = default)
        => _client.PutAsync<기사푸시토큰등록요청, 기사푸시토큰응답>(
            $"{BasePath}/push-token",
            request,
            "기사 푸시 토큰 등록",
            cancellationToken);

    public Task 푸시토큰삭제Async(CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            $"{BasePath}/push-token",
            "기사 푸시 토큰 삭제",
            cancellationToken);

    public Task<기사알림설정응답?> 설정조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사알림설정응답>(
            $"{BasePath}/settings",
            "기사 알림 설정 조회",
            cancellationToken);

    public Task<기사알림설정응답?> 설정수정Async(
        기사알림설정수정요청 request,
        CancellationToken cancellationToken = default)
        => _client.PutAsync<기사알림설정수정요청, 기사알림설정응답>(
            $"{BasePath}/settings",
            request,
            "기사 알림 설정 수정",
            cancellationToken);
}
