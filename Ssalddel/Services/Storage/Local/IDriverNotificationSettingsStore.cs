namespace 살뜰.Services.Storage.Local
{
    public interface IDriverNotificationSettingsStore
    {
        Task<DriverNotificationSettings> GetAsync(string driverId, CancellationToken cancellationToken = default);
        Task SetAsync(string driverId, DriverNotificationSettings settings, CancellationToken cancellationToken = default);
    }

    public sealed record DriverNotificationSettings(
        bool 배차추천알림사용,
        bool 운전중푸시만사용,
        bool 소리사용,
        bool 진동사용,
        bool 야간알림제한,
        bool 정차후모아보기,
        bool 음식배달추천알림사용 = false,
        bool 화물운송추천알림사용 = false);
}
