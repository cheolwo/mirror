#if ANDROID && DEBUG
#define SSALDDEL_LOCATION_DIAGNOSTICS
#endif

namespace FDriverApp.Services;

public sealed class FDriverLocationService : IFDriverLocationService
{
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly SemaphoreSlim _fixGate = new(1, 1);
    private long _generation;
    private EventHandler<GeolocationLocationChangedEventArgs>? _locationHandler;
    private EventHandler<GeolocationListeningFailedEventArgs>? _failureHandler;
    public bool IsListening { get; private set; }
    public FDriverLocationStatus Status { get; private set; } = FDriverLocationStatus.Stopped;
    public FDriverLocationSnapshot? LatestLocation { get; private set; }
    public event EventHandler<FDriverLocationSnapshot>? LocationChanged;
    public event EventHandler<string>? ListeningFailed;

    public async Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false)
    {
#if ANDROID && DEBUG
        var diagnosticStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
#else
        const long diagnosticStartedAt = 0;
#endif
        LogLocationDebug("start", diagnosticStartedAt, _generation, cancellationToken, LatestLocation);
        await _startGate.WaitAsync(cancellationToken);
        var generation = _generation;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsListening) return true;
            LogLocationDebug("permission-check-start", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted && requestPermission)
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            LogLocationDebug("permission-check-end", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _generation) return false;
            if (permission != PermissionStatus.Granted)
            {
                Status = FDriverLocationStatus.PermissionRequired;
                return false;
            }
            Status = FDriverLocationStatus.Acquiring;

            _locationHandler = (_, args) =>
            {
                if (!IsListening || generation != _generation) return;
                LatestLocation = Snapshot(args.Location);
                Status = FDriverLocationFreshness.Assess(LatestLocation, DateTime.UtcNow);
                LogLocationDebug("listener-fix", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
                LocationChanged?.Invoke(this, LatestLocation);
            };
            _failureHandler = (_, args) =>
            {
                if (generation != _generation) return;
                StopListening();
                Status = args.Error == GeolocationError.Unauthorized
                    ? FDriverLocationStatus.PermissionRequired : FDriverLocationStatus.SensorFailure;
                LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
                ListeningFailed?.Invoke(this, "위치 정보를 받지 못했습니다. 다시 확인해 주세요.");
            };
            Geolocation.Default.LocationChanged += _locationHandler;
            Geolocation.Default.ListeningFailed += _failureHandler;
            // The provider may publish the first fix before its start task returns.
            IsListening = true;
            LogLocationDebug("listener-start", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            var started = await Geolocation.Default.StartListeningForegroundAsync(
                new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(1)));
            LogLocationDebug("listener-end", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _generation)
            {
                // The start gate still excludes a newer listener. A native start that
                // finished after pause/logout must not survive that earlier stop.
                StopNativeListening();
                return false;
            }
            if (!started)
            {
                StopListening();
                Status = FDriverLocationStatus.SignalUnavailable;
                return false;
            }
            IsListening = true;
            return true;
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            if (generation == _generation)
            {
                StopListening();
                Status = StatusFor(ex);
            }
            else StopNativeListening();
            LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            return false;
        }
        catch (OperationCanceledException)
        {
            if (generation == _generation) StopListening();
            else StopNativeListening();
            LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            throw;
        }
        catch (Exception)
        {
            if (generation == _generation)
            {
                StopListening();
                Status = FDriverLocationStatus.SensorFailure;
            }
            else StopNativeListening();
            LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            return false;
        }
        finally
        {
            _startGate.Release();
            LogLocationDebug("end", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
        }
    }

    public void StopListening()
    {
        Interlocked.Increment(ref _generation);
        IsListening = false;
        LatestLocation = null;
        Status = FDriverLocationStatus.Stopped;
        if (_locationHandler is not null) Geolocation.Default.LocationChanged -= _locationHandler;
        if (_failureHandler is not null) Geolocation.Default.ListeningFailed -= _failureHandler;
        _locationHandler = null;
        _failureHandler = null;
        StopNativeListening();
        LogLocationDebug("stop", System.Diagnostics.Stopwatch.GetTimestamp(), _generation, default, LatestLocation);
    }

    private static void StopNativeListening()
    {
        try { Geolocation.Default.StopListeningForeground(); }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException) { }
    }

    public async Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
#if ANDROID && DEBUG
        var diagnosticStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
#else
        const long diagnosticStartedAt = 0;
#endif
        LogLocationDebug("fix-request-start", diagnosticStartedAt, _generation, cancellationToken, LatestLocation);
        await _fixGate.WaitAsync(cancellationToken);
        var generation = _generation;
        try
        {
            if (FreshLatest() is { } latest) return latest;
            LogLocationDebug("permission-check-start", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            LogLocationDebug("permission-check-end", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _generation) return null;
            if (permission != PermissionStatus.Granted)
            {
                Status = FDriverLocationStatus.PermissionRequired;
                return null;
            }

            Status = FDriverLocationStatus.Acquiring;
            LogLocationDebug("fix-start", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(12)),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _generation) return null;
            // A listener fix received during acquisition wins over a missing/older result.
            var snapshot = location is null ? null : Snapshot(location);
            var snapshotStatus = FDriverLocationFreshness.Assess(snapshot, DateTime.UtcNow);
            LogLocationDebug("fix-end", diagnosticStartedAt, generation, cancellationToken, snapshot);
            if (FreshLatest() is { } current && (snapshotStatus != FDriverLocationStatus.Ready
                || current.RecordedAtUtc >= snapshot!.RecordedAtUtc))
                return current;
            Status = snapshotStatus;
            if (Status != FDriverLocationStatus.Ready) return null;
            LatestLocation = snapshot;
            return snapshot;
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException
                                   or FeatureNotEnabledException
                                   or PermissionException)
        {
            if (generation == _generation) Status = StatusFor(ex);
            LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (generation == _generation) Status = FDriverLocationStatus.TimedOut;
            LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            return generation == _generation ? FreshLatest() : null;
        }
        catch (TimeoutException)
        {
            if (generation == _generation) Status = FDriverLocationStatus.TimedOut;
            LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            return generation == _generation ? FreshLatest() : null;
        }
        catch (OperationCanceledException)
        {
            LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            throw;
        }
        catch (Exception)
        {
            if (generation == _generation) Status = FDriverLocationStatus.SensorFailure;
            LogLocationDebug("failure", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
            return generation == _generation ? FreshLatest() : null;
        }
        finally
        {
            _fixGate.Release();
            LogLocationDebug("fix-request-end", diagnosticStartedAt, generation, cancellationToken, LatestLocation);
        }
    }

    [System.Diagnostics.Conditional("SSALDDEL_LOCATION_DIAGNOSTICS")]
    private void LogLocationDebug(string phase, long startedAt, long generation, CancellationToken cancellationToken,
        FDriverLocationSnapshot? fix)
    {
#if ANDROID && DEBUG
        // Device diagnostics deliberately omit location values and personal data.
        try
        {
            var elapsedMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var freshness = FDriverLocationFreshness.Assess(fix, DateTime.UtcNow);
            Android.Util.Log.Debug("Ssalddel.Location", $"phase={phase} elapsedMs={elapsedMs} status={Status} "
                + $"isListening={IsListening} generationCurrent={generation == _generation} canceled={cancellationToken.IsCancellationRequested} "
                + $"fixPresent={fix is not null} freshness={freshness}");
        }
        catch { }
#endif
    }

    private FDriverLocationSnapshot? FreshLatest()
    {
        if (FDriverLocationFreshness.Assess(LatestLocation, DateTime.UtcNow) != FDriverLocationStatus.Ready) return null;
        Status = FDriverLocationStatus.Ready;
        return LatestLocation;
    }

    private static FDriverLocationStatus StatusFor(Exception exception) => exception switch
    {
        PermissionException => FDriverLocationStatus.PermissionRequired,
        FeatureNotEnabledException => FDriverLocationStatus.ServicesDisabled,
        FeatureNotSupportedException => FDriverLocationStatus.Unsupported,
        _ => FDriverLocationStatus.SensorFailure
    };

    private static FDriverLocationSnapshot Snapshot(Location location) => new(
        (decimal)location.Latitude, (decimal)location.Longitude,
        location.Accuracy.HasValue ? (decimal)location.Accuracy.Value : null,
        location.Timestamp.UtcDateTime);
}
