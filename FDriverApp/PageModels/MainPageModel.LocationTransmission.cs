using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Work;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private readonly SemaphoreSlim _locationTransmissionGate = new(1, 1);
    private CancellationTokenSource? _locationTransmissionCancellation;
    private long _locationTransmissionGeneration;

    // Map GPS remains available before/after driving. Only the operational heartbeat
    // belongs to the current signed-in driver's active driving lifetime.
    private void CancelLocationTransmissions()
    {
        Interlocked.Increment(ref _locationTransmissionGeneration);
        var previous = _locationTransmissionCancellation;
        _locationTransmissionCancellation = null;
        previous?.Cancel();
        previous?.Dispose();
        _lastLocationSentAtUtc = null;
    }

    private bool IsLocationCaptureCurrent(string? owner, CancellationTokenSource workspace, long mapLifetime)
        => owner == _authSession.UserId && HasDriverSession() && IsAuthenticated
            && IsWorkspaceLifetimeCurrent(workspace) && _foodMapForeground
            && mapLifetime == _foodMapLifetime;

    private async Task<FDriverLocationSnapshot?> CaptureLocationAsync(bool sendToServer, CancellationToken cancellationToken = default)
    {
        var owner = _authSession.UserId;
        var workspace = _workspaceCancellation;
        var mapLifetime = _foodMapLifetime;
        var transmissionGeneration = Interlocked.Read(ref _locationTransmissionGeneration);
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

        await _locationTransmissionGate.WaitAsync(cancellationToken);
        try
        {
            // A stop/start or account switch during GPS acquisition must never turn
            // an old reading into a fresh operational location for the new lifetime.
            if (!IsLocationCaptureCurrent(owner, workspace, mapLifetime) || !IsOnDuty
                || transmissionGeneration != Interlocked.Read(ref _locationTransmissionGeneration)
                || !IsFreshFoodMapLocation(location)) return null;
            // Commands and polling can request the same heartbeat concurrently.
            if (_lastLocationSentAtUtc is { } sentAt && DateTime.UtcNow - sentAt < LocationHeartbeatInterval)
                return location;

            var transmission = _locationTransmissionCancellation ??= new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, workspace.Token, transmission.Token);
            await _api.UpdateLocationAsync(new 기사위치갱신요청
            {
                AppKey = _profile.AppKey,
                위도 = location.Latitude,
                경도 = location.Longitude,
                정확도_m = location.AccuracyMeters,
                상차접근허용반경Km = 3m,
                운행상태 = DrivingStatus,
                기록시각 = location.RecordedAtUtc
            }, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (!IsLocationCaptureCurrent(owner, workspace, mapLifetime) || !IsOnDuty
                || transmissionGeneration != Interlocked.Read(ref _locationTransmissionGeneration)) return null;
            _lastLocationSentAtUtc = DateTime.UtcNow;
            LocationSyncText = $"기사 위치 전송 {_lastLocationSentAtUtc.Value.ToLocalTime():HH:mm:ss}";
        }
        finally { _locationTransmissionGate.Release(); }
        return location;
    }
}
