using FDriverApp.Services;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private FDriverPushRegistrationService? _foodPushRegistration;

    private async Task RefreshFoodPushRegistrationAsync(CancellationToken cancellationToken)
    {
        if (_foodPushRegistration is null) return;
        var owner = _authSession.UserId;
        var lifetime = _workspaceCancellation;
        var result = await _foodPushRegistration.EnsureRegisteredAsync(cancellationToken);
        if (owner != _authSession.UserId || !IsWorkspaceLifetimeCurrent(lifetime)) return;
        RecommendationNotificationText = result.State == FDriverPushRegistrationState.Registered
            ? _foodPushRegistration.AreNotificationsEnabled
                ? "새 배달 알림 · 앱 복귀와 자동 갱신으로 최신 요청 확인"
                : "알림 권한이 꺼져 있습니다. 휴대폰 설정에서 허용해 주세요. 앱에서는 요청을 자동으로 확인합니다."
            : result.Message ?? "앱 복귀와 자동 갱신으로 요청 확인";
    }

    private async Task<string?> DeactivateFoodPushAsync()
    {
        if (_foodPushRegistration is null) return null;
        var result = await _foodPushRegistration.DeactivateCurrentOwnerAsync(CancellationToken.None);
        return result.State == FDriverPushRegistrationState.Failed ? result.Message : null;
    }
}
