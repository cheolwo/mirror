using System.Reflection;
using FDriverApp.PageModels;
using FDriverApp.Services;

namespace Ssalddel.Tests.Clients;

// Linked ViewModel recovery tests; no Android sensor, private device location, or GPS injection.
public sealed class FDriverLocationRecoveryTests
{
    [Theory]
    [InlineData(-5, FDriverLocationStatus.Ready)]
    [InlineData(-6, FDriverLocationStatus.Stale)]
    [InlineData(0, FDriverLocationStatus.Ready)]
    [InlineData(30, FDriverLocationStatus.Ready)]
    [InlineData(31, FDriverLocationStatus.Stale)]
    public void Freshness_PreservesThirtySecondAndFutureSkewBoundary(int ageSeconds, FDriverLocationStatus expected)
    {
        var now = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, FDriverLocationFreshness.Assess(Fix(now.AddSeconds(-ageSeconds)), now));
    }

    [Theory]
    [InlineData(0, 127, 5)]
    [InlineData(91, 127, 5)]
    [InlineData(37, 181, 5)]
    [InlineData(37, 127, -1)]
    public void InvalidReading_IsNotPermissionDenial(int latitude, int longitude, int accuracy)
        => Assert.Equal(FDriverLocationStatus.InvalidReading,
            FDriverLocationFreshness.Assess(new(latitude, longitude, accuracy, DateTime.UtcNow), DateTime.UtcNow));

    [Fact]
    public async Task StaleProviderCache_DoesNotSuppressNewFix()
    {
        var gps = new QuietLocationService { LatestLocation = Fix(DateTime.UtcNow.AddMinutes(-1)) };
        gps.Query = _ => Task.FromResult<FDriverLocationSnapshot?>(Fix(DateTime.UtcNow));
        var model = await Model(gps);
        try
        {
            await model.ResumeFoodMapAsync();
            Assert.Equal(1, gps.QueryCalls);
            Assert.True(model.HasCurrentLocation);
            Assert.Equal(FDriverLocationStatus.Ready, model.LocationStatus);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task QuietRunningListener_RecoversAfterAppliedFixExpires()
    {
        var gps = new QuietLocationService { LatestLocation = Fix(DateTime.UtcNow) };
        var model = await Model(gps);
        try
        {
            await model.ResumeFoodMapAsync();
            Assert.Equal(0, gps.QueryCalls);
            var stale = Fix(DateTime.UtcNow.AddMinutes(-1));
            gps.LatestLocation = stale;
            Set(model, "_foodMapLocation", stale);
            gps.Query = _ => Task.FromResult<FDriverLocationSnapshot?>(Fix(DateTime.UtcNow));
            Schedule(model);
            await ListenerTask(model);
            Assert.Equal(1, gps.QueryCalls);
            Assert.True(gps.IsListening);
            Assert.Equal(0, gps.StopCalls);
            Assert.True(model.HasCurrentLocation);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task ResumeAndOverlappingHeartbeat_ShareOneFixButKeepServerHeartbeat()
    {
        var late = new TaskCompletionSource<FDriverLocationSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gps = new QuietLocationService { Query = _ => late.Task };
        var api = new FDriverTestWorkspaceApi();
        var model = await Model(gps, api);
        try
        {
            var resume = model.ResumeFoodMapAsync();
            var first = Capture(model, sendToServer: false);
            var heartbeat = Capture(model, sendToServer: true);
            Assert.Equal(1, gps.QueryCalls);
            Assert.Equal(FDriverLocationStatus.Acquiring, model.LocationStatus);
            late.SetResult(Fix(DateTime.UtcNow));
            await Task.WhenAll(resume, first, heartbeat).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(1, gps.QueryCalls);
            Assert.Equal(1, api.LocationCalls);
            Assert.True(model.HasCurrentLocation);
        }
        finally { late.TrySetResult(null); await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task CancelingOneCaller_DoesNotCancelSharedForegroundFix()
    {
        var late = new TaskCompletionSource<FDriverLocationSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gps = new QuietLocationService { Query = _ => late.Task };
        var model = await Model(gps);
        using var caller = new CancellationTokenSource();
        try
        {
            var resume = model.ResumeFoodMapAsync();
            var canceledCaller = Capture(model, false, caller.Token);
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledCaller);
            Assert.False(gps.QueryToken.IsCancellationRequested);
            late.SetResult(Fix(DateTime.UtcNow));
            await resume.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(1, gps.QueryCalls);
            Assert.True(model.HasCurrentLocation);
        }
        finally { late.TrySetResult(null); await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task PauseRejectsLateFixAndResumeAcquiresWithoutOldBackoff()
    {
        // Deliberately returns after cancellation, exercising the lifetime gate.
        var late = new TaskCompletionSource<FDriverLocationSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gps = new QuietLocationService { Query = _ => late.Task };
        var api = new FDriverTestWorkspaceApi();
        var model = await Model(gps, api);
        try
        {
            var oldResume = model.ResumeFoodMapAsync();
            var oldCapture = Capture(model, true);
            model.PauseFoodMap();
            Assert.True(gps.QueryToken.IsCancellationRequested);
            late.SetResult(Fix(DateTime.UtcNow));
            await Task.WhenAll(oldResume, oldCapture).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(model.HasCurrentLocation);
            Assert.Equal(FDriverLocationStatus.Stopped, model.LocationStatus);
            Assert.Equal(0, api.LocationCalls);
            gps.Query = _ => Task.FromResult<FDriverLocationSnapshot?>(Fix(DateTime.UtcNow));
            await model.ResumeFoodMapAsync();
            Assert.Equal(2, gps.QueryCalls);
            Assert.True(model.HasCurrentLocation);
        }
        finally { late.TrySetResult(null); await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task FailedFix_ThrottlesHeartbeatAndMapForSixtySeconds_ManualRetryIsImmediate()
    {
        var gps = new QuietLocationService { Status = FDriverLocationStatus.TimedOut };
        var api = new FDriverTestWorkspaceApi();
        var model = await Model(gps, api);
        try
        {
            var before = DateTime.UtcNow;
            await model.ResumeFoodMapAsync();
            await Capture(model, true);
            await Capture(model, true);
            Schedule(model);
            Assert.Equal(1, gps.QueryCalls);
            Assert.Equal(0, api.LocationCalls);
            Assert.Equal(FDriverLocationStatus.TimedOut, model.LocationStatus);
            var retry = Get<DateTime>(model, "_foodMapFixRetryAtUtc");
            Assert.InRange(retry, before.AddSeconds(60), DateTime.UtcNow.AddSeconds(60));
            gps.Query = _ => Task.FromResult<FDriverLocationSnapshot?>(Fix(DateTime.UtcNow));
            await model.RetryMapLocationCommand.ExecuteAsync(null);
            Assert.Equal(2, gps.QueryCalls);
            Assert.True(model.HasCurrentLocation);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task ListenerFixDuringEmptyAcquisition_PreservesReadyAndServerHeartbeat()
    {
        var late = new TaskCompletionSource<FDriverLocationSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gps = new QuietLocationService { Query = _ => late.Task };
        var api = new FDriverTestWorkspaceApi();
        var model = await Model(gps, api);
        try
        {
            var resume = model.ResumeFoodMapAsync();
            var heartbeat = Capture(model, true);
            gps.Emit(Fix(DateTime.UtcNow));
            late.SetResult(null);
            await Task.WhenAll(resume, heartbeat).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(model.HasCurrentLocation);
            Assert.Equal(FDriverLocationStatus.Ready, model.LocationStatus);
            Assert.Equal(1, api.LocationCalls);
        }
        finally { late.TrySetResult(null); await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData(31)]
    [InlineData(-6)]
    public async Task UnfreshAcquisition_IsNeverSentToServer(int ageSeconds)
    {
        var gps = new QuietLocationService
        {
            Query = _ => Task.FromResult<FDriverLocationSnapshot?>(Fix(DateTime.UtcNow.AddSeconds(-ageSeconds)))
        };
        var api = new FDriverTestWorkspaceApi();
        var model = await Model(gps, api);
        try
        {
            await Capture(model, true);
            Assert.False(model.HasCurrentLocation);
            Assert.Equal(0, api.LocationCalls);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData(FDriverLocationStatus.PermissionRequired, "허용")]
    [InlineData(FDriverLocationStatus.ServicesDisabled, "켜 주세요")]
    [InlineData(FDriverLocationStatus.Acquiring, "확인하고")]
    [InlineData(FDriverLocationStatus.SignalUnavailable, "기다리고")]
    [InlineData(FDriverLocationStatus.TimedOut, "기다리고")]
    [InlineData(FDriverLocationStatus.Stale, "오래되었습니다")]
    [InlineData(FDriverLocationStatus.SensorFailure, "받지 못했습니다")]
    public async Task Notice_UsesReasonAndNotifiesWaitingPresentation(FDriverLocationStatus status, string expected)
    {
        var model = await Model(new());
        var changed = new List<string?>();
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        try
        {
            model.LocationStatus = status;
            Assert.Contains(expected, model.LocationNotice);
            Assert.Contains(nameof(MainPageModel.LocationNotice), changed);
            Assert.Contains(nameof(MainPageModel.WaitingNotice), changed);
            if (status != FDriverLocationStatus.PermissionRequired)
                Assert.DoesNotContain("허용", model.LocationNotice);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    private static FDriverLocationSnapshot Fix(DateTime at) => new(37.5m, 127m, 5, at);
    private static async Task<MainPageModel> Model(QuietLocationService gps, FDriverTestWorkspaceApi? api = null)
    {
        var model = FDriverLifecycleTestSupport.Model(new(), api ?? new(), gps);
        await model.InitializeAsync();
        // Server heartbeat cases represent an explicitly started driving session.
        model.IsOnDuty = true;
        return model;
    }
    private static Task<FDriverLocationSnapshot?> Capture(MainPageModel model, bool sendToServer, CancellationToken token = default)
        => (Task<FDriverLocationSnapshot?>)typeof(MainPageModel).GetMethod("CaptureLocationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(model, [sendToServer, token])!;
    private static void Schedule(MainPageModel model)
        => typeof(MainPageModel).GetMethod("ScheduleFoodMapListener", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(model, [Get<long>(model, "_foodMapLifetime"), FDriverLifecycleTestSupport.WorkspaceToken(model)]);
    private static Task ListenerTask(MainPageModel model) => Get<Task>(model, "_foodMapListenerTask");
    private static T Get<T>(MainPageModel model, string name)
        => (T)typeof(MainPageModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model)!;
    private static void Set(MainPageModel model, string name, object value)
        => typeof(MainPageModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, value);

    private sealed class QuietLocationService : IFDriverLocationService
    {
        public bool IsListening { get; private set; } = true;
        public FDriverLocationStatus Status { get; set; } = FDriverLocationStatus.SignalUnavailable;
        public FDriverLocationSnapshot? LatestLocation { get; set; }
        public int QueryCalls { get; private set; }
        public int StopCalls { get; private set; }
        public CancellationToken QueryToken { get; private set; }
        public Func<CancellationToken, Task<FDriverLocationSnapshot?>> Query { get; set; }
            = _ => Task.FromResult<FDriverLocationSnapshot?>(null);
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged;
        public event EventHandler<string>? ListeningFailed { add { } remove { } }
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false)
        { cancellationToken.ThrowIfCancellationRequested(); IsListening = true; return Task.FromResult(true); }
        public void StopListening()
        { StopCalls++; IsListening = false; Status = FDriverLocationStatus.Stopped; LatestLocation = null; }
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
        { QueryCalls++; QueryToken = cancellationToken; return Query(cancellationToken); }
        public void Emit(FDriverLocationSnapshot location)
        { LatestLocation = location; Status = FDriverLocationStatus.Ready; LocationChanged?.Invoke(this, location); }
    }
}
