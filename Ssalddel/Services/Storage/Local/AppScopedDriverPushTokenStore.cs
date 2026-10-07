using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.Data;

namespace 살뜰.Services.Storage.Local;

/// <summary>기존 화물 토큰 저장 계약과 음식 앱의 활성 설치를 분리합니다.</summary>
public sealed class AppScopedDriverPushTokenStore(SsalddelContext db, IDriverPushTokenStore cargoTokens)
    : IDriverPushTokenStore
{
    public Task SetAsync(string driverId, string pushToken, CancellationToken cancellationToken = default)
        => cargoTokens.SetAsync(driverId, pushToken, cancellationToken);

    public Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default)
        => cargoTokens.GetAsync(driverId, cancellationToken);

    public Task ClearAsync(string driverId, CancellationToken cancellationToken = default)
        => cargoTokens.ClearAsync(driverId, cancellationToken);

    public async Task<string?> GetForAppAsync(string driverId, string appKey, CancellationToken cancellationToken = default)
    {
        if (string.Equals(appKey, 기사앱식별자.CargoYongdalDriverApp, StringComparison.Ordinal))
            return await cargoTokens.GetAsync(driverId, cancellationToken);
        if (!string.Equals(appKey, 기사앱식별자.FoodDeliveryDriverApp, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(driverId)) return null;

        // The legacy port returns one token: use the latest active installation,
        // never a cargo/app-agnostic token or a different user's installation.
        var installations = await db.SsalddelMobilePushInstallations.AsNoTracking()
            .Where(x => x.UserId == driverId && x.AppKey == 기사앱식별자.FoodDeliveryDriverApp && x.IsActive)
            .OrderByDescending(x => x.LastSeenAtUtc).ThenByDescending(x => x.Id)
            .Select(x => new { x.UserId, x.AppKey, x.PushToken }).ToListAsync(cancellationToken);
        // Recheck the binding after materialization even when the database uses
        // a case-insensitive collation for user/app identifiers.
        var token = installations.FirstOrDefault(x => string.Equals(x.UserId, driverId, StringComparison.Ordinal)
            && string.Equals(x.AppKey, 기사앱식별자.FoodDeliveryDriverApp, StringComparison.Ordinal))?.PushToken;
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
