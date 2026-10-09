namespace FDriverApp.Services;

public sealed record FDriverLocationSnapshot(
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters,
    DateTime RecordedAtUtc);

public enum FDriverLocationStatus
{
    Unknown, Stopped, Acquiring, Ready, PermissionRequired, ServicesDisabled,
    Unsupported, SignalUnavailable, TimedOut, Stale, InvalidReading, SensorFailure
}

public static class FDriverLocationFreshness
{
    public static FDriverLocationStatus Assess(FDriverLocationSnapshot? location, DateTime nowUtc)
    {
        if (location is null) return FDriverLocationStatus.SignalUnavailable;
        if (location.Latitude is < -90 or > 90 or 0 || location.Longitude is < -180 or > 180 or 0
            || location.AccuracyMeters is < 0) return FDriverLocationStatus.InvalidReading;
        var age = nowUtc - DateTime.SpecifyKind(location.RecordedAtUtc, DateTimeKind.Utc);
        return age >= TimeSpan.FromSeconds(-5) && age <= TimeSpan.FromSeconds(30)
            ? FDriverLocationStatus.Ready : FDriverLocationStatus.Stale;
    }
}

public interface IFDriverLocationService
{
    Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default);
    bool IsListening { get; }
    FDriverLocationStatus Status => FDriverLocationStatus.Unknown;
    FDriverLocationSnapshot? LatestLocation { get; }
    event EventHandler<FDriverLocationSnapshot>? LocationChanged;
    event EventHandler<string>? ListeningFailed;
    Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false);
    void StopListening();
}
