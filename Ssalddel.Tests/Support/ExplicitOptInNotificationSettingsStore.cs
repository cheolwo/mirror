using 살뜰.Services.Storage.Local;

namespace Ssalddel.Tests.Support;

/// <summary>기존 추천 전송 회귀가 두 종류에 명시적으로 동의한 기사 조건을 사용하도록 하는 시험 전용 입력입니다.</summary>
public sealed class ExplicitOptInNotificationSettingsStore : IDriverNotificationSettingsStore
{
    public Task<DriverNotificationSettings> GetAsync(string driverId, CancellationToken cancellationToken = default)
        => Task.FromResult(new DriverNotificationSettings(true, false, true, true, false, false,
            음식배달추천알림사용: true, 화물운송추천알림사용: true));
    public Task SetAsync(string driverId, DriverNotificationSettings settings, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("이 시험은 알림 설정을 변경하지 않습니다.");
}
