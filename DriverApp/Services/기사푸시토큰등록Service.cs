using Hongdal.Contracts.Driver.Notification;
using Microsoft.Maui.Storage;

namespace DriverApp.Services;

public sealed class 기사푸시토큰등록Service : I기사푸시토큰등록Service
{
    private const string 저장키 = "hongdal.driver.fcmToken.v1";

    private readonly IDriverNotificationApiService _notificationApi;
    private readonly IAuthSession _authSession;

    public 기사푸시토큰등록Service(
        IDriverNotificationApiService notificationApi,
        IAuthSession authSession)
    {
        _notificationApi = notificationApi;
        _authSession = authSession;
    }

    public async Task 수신토큰저장및등록Async(string? pushToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pushToken))
        {
            return;
        }

        var trimmedToken = pushToken.Trim();
        Preferences.Set(저장키, trimmedToken);
        await 서버등록Async(trimmedToken, cancellationToken);
    }

    public async Task 저장토큰등록Async(CancellationToken cancellationToken = default)
    {
        var token = Preferences.Get(저장키, string.Empty);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        await 서버등록Async(token, cancellationToken);
    }

    private async Task 서버등록Async(string pushToken, CancellationToken cancellationToken)
    {
        await _authSession.RestoreAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            return;
        }

        await _notificationApi.푸시토큰등록Async(
            new 기사푸시토큰등록요청 { PushToken = pushToken },
            cancellationToken);
    }
}
