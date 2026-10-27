using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Work;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private readonly FDriverLocationTransmissionSession _locationTransmissions = new(LocationHeartbeatInterval);

    // Map GPS remains available before/after driving. Only the operational heartbeat
    // belongs to the current signed-in driver's active driving lifetime.
    private void CancelLocationTransmissions() => _locationTransmissions.Cancel();

    private bool IsLocationCaptureCurrent(string? owner, CancellationTokenSource workspace, long mapLifetime)
        => owner == _authSession.UserId && HasDriverSession() && IsAuthenticated
            && IsWorkspaceLifetimeCurrent(workspace) && _foodMapForeground
            && mapLifetime == _foodMapLifetime;

    private async Task<FDriverLocationSnapshot?> CaptureLocationAsync(bool sendToServer, CancellationToken cancellationToken = default)
    {
        var owner = _authSession.UserId;
        var workspace = _workspaceCancellation;
        var mapLifetime = _foodMapLifetime;
        var transmissionGeneration = _locationTransmissions.Generation;
        cancellationToken = cancellationToken == default ? workspace.Token : cancellationToken;
        if (!IsLocationCaptureCurrent(owner, workspace, mapLifetime) || (sendToServer && !IsOnDuty)) return null;

        var location = await AcquireFoodMapLocationAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsLocationCaptureCurrent(owner, workspace, mapLifetime)) return null;
        if (location is null || !IsFreshFoodMapLocation(location))
        {
            if (IsFreshFoodMapLocation(_foodMapLocation)) location = _foodMapLocation!;
            else
            {
                MarkFoodMapLocationUnavailable();
                return null;
            }
        }

        if (!ApplyFoodMapLocation(location)) return null;
        if (!sendToServer) return location;

        var accepted = await _locationTransmissions.TransmitAsync(transmissionGeneration, workspace,
            isCaptureCurrent: () => IsLocationCaptureCurrent(owner, workspace, mapLifetime) && IsOnDuty,
            isFresh: () => IsFreshFoodMapLocation(location),
            send: token => _api.UpdateLocationAsync(new 기사위치갱신요청
            {
                AppKey = _profile.AppKey,
                위도 = location.Latitude,
                경도 = location.Longitude,
                정확도_m = location.AccuracyMeters,
                상차접근허용반경Km = 3m,
                운행상태 = DrivingStatus,
                기록시각 = location.RecordedAtUtc
            }, token),
            confirm: sentAt => LocationSyncText = $"기사 위치 전송 {sentAt.ToLocalTime():HH:mm:ss}",
            cancellationToken);
        return accepted ? location : null;
    }
}
