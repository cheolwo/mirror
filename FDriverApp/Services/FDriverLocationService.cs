namespace FDriverApp.Services;

public sealed class FDriverLocationService : IFDriverLocationService
{
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private long _generation;
    private EventHandler<GeolocationLocationChangedEventArgs>? _locationHandler;
    private EventHandler<GeolocationListeningFailedEventArgs>? _failureHandler;
    public bool IsListening { get; private set; }
    public FDriverLocationSnapshot? LatestLocation { get; private set; }
    public event EventHandler<FDriverLocationSnapshot>? LocationChanged;
    public event EventHandler<string>? ListeningFailed;

    public async Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false)
    {
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsListening) return true;
            var generation = _generation;
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted && requestPermission)
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            cancellationToken.ThrowIfCancellationRequested();
            if (permission != PermissionStatus.Granted || generation != _generation) return false;

            _locationHandler = (_, args) =>
            {
                if (!IsListening || generation != _generation) return;
                LatestLocation = Snapshot(args.Location);
                LocationChanged?.Invoke(this, LatestLocation);
            };
            _failureHandler = (_, _) =>
            {
                if (generation != _generation) return;
                StopListening();
                ListeningFailed?.Invoke(this, "위치 정보를 받지 못했습니다. GPS와 위치 권한을 확인해 주세요.");
            };
            Geolocation.Default.LocationChanged += _locationHandler;
            Geolocation.Default.ListeningFailed += _failureHandler;
            var started = await Geolocation.Default.StartListeningForegroundAsync(
                new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(1)));
            if (cancellationToken.IsCancellationRequested || generation != _generation || !started)
            {
                StopListening();
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }
            IsListening = true;
            return true;
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            StopListening();
            return false;
        }
        finally { _startGate.Release(); }
    }

    public void StopListening()
    {
        Interlocked.Increment(ref _generation);
        IsListening = false;
        LatestLocation = null;
        if (_locationHandler is not null) Geolocation.Default.LocationChanged -= _locationHandler;
        if (_failureHandler is not null) Geolocation.Default.ListeningFailed -= _failureHandler;
        _locationHandler = null;
        _failureHandler = null;
        try { Geolocation.Default.StopListeningForeground(); }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException) { }
    }

    public async Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (LatestLocation is { } latest && DateTime.UtcNow - latest.RecordedAtUtc is var age
            && age >= TimeSpan.FromSeconds(-5) && age <= TimeSpan.FromSeconds(30))
            return latest;
        try
        {
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                return null;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(12)),
                cancellationToken);
            return location is null
                ? null
                : Snapshot(location);
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException
                                   or FeatureNotEnabledException
                                   or PermissionException)
        {
            return null;
        }
    }

    private static FDriverLocationSnapshot Snapshot(Location location) => new(
        (decimal)location.Latitude, (decimal)location.Longitude,
        location.Accuracy.HasValue ? (decimal)location.Accuracy.Value : null,
        location.Timestamp.UtcDateTime);
}
