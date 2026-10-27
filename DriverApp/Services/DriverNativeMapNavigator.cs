using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace DriverApp.Services;

public sealed class DriverNativeMapNavigator(IServiceProvider services, IAuthSession session) : IDriverNativeMapNavigator
{
    private readonly SemaphoreSlim _navigationGate = new(1, 1);

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        var owner = session.UserId; var revision = session.SessionRevision; var authenticated = session.IsAuthenticated;
        void 현재요청확인()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session.UserId != owner || session.SessionRevision != revision || session.IsAuthenticated != authenticated)
                throw new OperationCanceledException("로그인 정보가 변경되어 지도 이동을 취소했습니다.", cancellationToken);
        }
        현재요청확인();
        await _navigationGate.WaitAsync(cancellationToken);
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                현재요청확인();
                var rootPage = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (rootPage is not NavigationPage navigation)
                {
                    throw new InvalidOperationException("기사 앱 NavigationPage를 찾지 못했습니다.");
                }

                if (navigation.CurrentPage is NativeDriverHomePage)
                {
                    return;
                }

                var mapPage = services.GetRequiredService<NativeDriverHomePage>();
                현재요청확인();
                await navigation.PushAsync(mapPage);
            });
        }
        finally { _navigationGate.Release(); }
    }
}
