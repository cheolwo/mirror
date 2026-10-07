namespace 살뜰.Services.Storage.Local
{
    public interface IDriverPushTokenStore
    {
        Task SetAsync(string driverId, string pushToken, CancellationToken cancellationToken = default);
        Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default);
        // 기존 기사 ID 전용 토큰은 화물 앱 등록으로만 취급합니다.
        // 음식 앱의 별도 등록 근거가 없으면 같은 계정의 화물 기기로 보내지 않습니다.
        Task<string?> GetForAppAsync(string driverId, string appKey, CancellationToken cancellationToken = default)
            => string.Equals(appKey, Ssalddel.Contracts.Common.Drivers.기사앱식별자.CargoYongdalDriverApp, StringComparison.Ordinal)
                ? GetAsync(driverId, cancellationToken)
                : Task.FromResult<string?>(null);
        Task ClearAsync(string driverId, CancellationToken cancellationToken = default);
    }
}



