namespace FDriverApp.Services;

public sealed record FDriverLocationSnapshot(
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters,
    DateTime RecordedAtUtc);

public interface IFDriverLocationService
{
    Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default);
    bool IsListening { get; }
    FDriverLocationSnapshot? LatestLocation { get; }
    event EventHandler<FDriverLocationSnapshot>? LocationChanged;
    event EventHandler<string>? ListeningFailed;
    Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false);
    void StopListening();
}
