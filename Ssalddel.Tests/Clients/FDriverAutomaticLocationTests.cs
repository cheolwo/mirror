using System.Reflection;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;

namespace Ssalddel.Tests.Clients;

// Foreground policy and request ownership only; no device GPS/background-service evidence.
public sealed class FDriverAutomaticLocationTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Driving_RefreshesLocationPeriodically_WithDispatchOnOrCurrentDelivery(bool receives, bool active)
    {
        var gps = new LocationFake();
        var api = Api(receives, active, driving: true);
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync();
            var previous = api.LocationCalls;
            FDriverLifecycleTestSupport.LastLocationSentAtUtc(model, DateTime.UtcNow.AddSeconds(-11));
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(previous + 1, api.LocationCalls);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(previous + 1, api.LocationCalls);
            Assert.Equal(receives, model.ReceivesNewDispatches);
            Assert.Equal(active, model.HasActiveWork);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task StopWork_StopsOperationalHeartbeat_WhileMapGpsAndWorkspaceRemainAvailable()
    {
        var gps = new LocationFake();
        var api = Api(true, false, driving: true);
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync();
            await model.StartMonitoringAsync();
            var previous = api.LocationCalls;
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.False(model.IsOnDuty);
            Assert.Equal(1, api.StopWorkCalls);
            Assert.True(gps.IsListening);
            gps.Emit(Fix());
            Assert.True(model.HasCurrentLocation);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Null(await Capture(model));
            Assert.Equal(previous, api.LocationCalls);
            Assert.True(model.IsAuthenticated);
            Assert.NotNull(FDriverLifecycleTestSupport.MonitorTask(model));
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateGpsAfterWorkStop_IsNotUploaded_EvenIfAnotherDrivingSessionStarts(bool restart)
    {
        var fix = new TaskCompletionSource<FDriverLocationSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gps = new LocationFake { LatestLocation = null, Query = _ => fix.Task };
        var api = Api(false, false, driving: false);
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync();
            model.IsOnDuty = true;
            var capture = Capture(model);
            model.IsOnDuty = false;
            if (restart) model.IsOnDuty = true;
            fix.SetResult(Fix());
            Assert.Null(await capture.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(0, api.LocationCalls);
            Assert.True(model.HasCurrentLocation); // Local map GPS has an independent lifetime.
            if (restart)
            {
                await Capture(model);
                Assert.Equal(1, api.LocationCalls);
            }
        }
        finally { fix.TrySetResult(null); await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task PermissionFailure_ClearsCurrentFix_AndDoesNotUploadCachedOrLateListenerReadings()
    {
        var gps = new LocationFake();
        var api = Api(true, false, driving: true);
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync();
            await model.StartMonitoringAsync();
            var previous = api.LocationCalls;
            gps.DenyPermission();
            gps.Emit(Fix(), ready: false);
            Assert.False(model.HasCurrentLocation);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(previous, api.LocationCalls);
            Assert.Equal(FDriverLocationStatus.PermissionRequired, model.LocationStatus);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("pause")]
    [InlineData("logout")]
    public async Task InFlightUpload_IsCanceled_AndLateResponseCannotConfirmCurrentLocation(string action)
    {
        var session = new FDriverTestSession();
        var api = Api(true, false, driving: true);
        var gps = new LocationFake();
        var model = FDriverLifecycleTestSupport.Model(session, api, gps);
        var response = new TaskCompletionSource<기사위치갱신응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken uploadToken = default;
        try
        {
            await model.InitializeAsync();
            await model.StartMonitoringAsync();
            FDriverLifecycleTestSupport.LastLocationSentAtUtc(model, null);
            api.LocationUpdate = (_, token) => { uploadToken = token; return response.Task; };
            var upload = Capture(model);
            Assert.True(uploadToken.CanBeCanceled);
            if (action == "stop") await model.ToggleWorkCommand.ExecuteAsync(null);
            else if (action == "pause") model.PauseFoodWorkspace();
            else await session.ClearAsync();
            Assert.True(uploadToken.IsCancellationRequested);
            response.SetResult(null); // A provider may complete after cancellation.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);
            Assert.Null(FDriverLifecycleTestSupport.LastLocationSentAtUtc(model));
            if (action == "stop") Assert.True(gps.IsListening);
            else Assert.False(gps.IsListening);
        }
        finally { response.TrySetResult(null); await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task AccountChangeDuringGpsAcquisition_NeverUploadsOldReadingUnderNewAccount()
    {
        var session = new FDriverTestSession();
        var fix = new TaskCompletionSource<FDriverLocationSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gps = new LocationFake { LatestLocation = null, Query = _ => fix.Task };
        var api = Api(true, false, driving: false);
        var model = FDriverLifecycleTestSupport.Model(session, api, gps);
        try
        {
            await model.InitializeAsync();
            model.IsOnDuty = true;
            var capture = Capture(model);
            await session.ApplyAsync(new ClientAuthTokenSnapshot("other-access", DateTime.UtcNow.AddMinutes(30),
                "other-refresh", DateTime.UtcNow.AddHours(1), "other-driver", "다른 기사", ["Driver"]));
            fix.SetResult(Fix());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
            Assert.Equal(0, api.LocationCalls);
            Assert.False(model.HasCurrentLocation);
            Assert.False(gps.IsListening);
        }
        finally { fix.TrySetResult(null); await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task OverlappingHeartbeatRequests_UseOneUploadWithinTenSeconds()
    {
        var api = Api(true, false, driving: true);
        var model = FDriverLifecycleTestSupport.Model(new(), api, new LocationFake());
        var response = new TaskCompletionSource<기사위치갱신응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await model.InitializeAsync();
            var previous = api.LocationCalls;
            FDriverLifecycleTestSupport.LastLocationSentAtUtc(model, null);
            api.LocationUpdate = (_, _) => response.Task;
            var first = Capture(model);
            var second = Capture(model);
            Assert.Equal(previous + 1, api.LocationCalls);
            response.SetResult(null);
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(previous + 1, api.LocationCalls);
        }
        finally { response.TrySetResult(null); await model.StopMonitoringAsync(); }
    }

    private static FDriverTestWorkspaceApi Api(bool receives, bool active, bool driving) => new()
    {
        WorkStatus = driving ? "운행중" : "운행종료",
        Availability = _ => Task.FromResult(new 운영배차수신상태Dto
            { 수신의사Code = receives ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off }),
        Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto
        {
            DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3,
            ActiveDeliveries = active ? [new()
            {
                OfferId = "current", WorkStatus = DriverWorkOfferStatus.MovingToPickup,
                Pickup = new() { Label = "음식점", Latitude = 37.51m, Longitude = 127.01m },
                Dropoff = new() { Label = "전달지", Latitude = 37.52m, Longitude = 127.02m }
            }] : []
        })
    };

    private static FDriverLocationSnapshot Fix() => new(37.5m, 127m, 5, DateTime.UtcNow);
    private static Task<FDriverLocationSnapshot?> Capture(MainPageModel model)
        => (Task<FDriverLocationSnapshot?>)typeof(MainPageModel).GetMethod("CaptureLocationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(model, [true, CancellationToken.None])!;

    private sealed class LocationFake : IFDriverLocationService
    {
        public bool IsListening { get; private set; }
        public FDriverLocationStatus Status { get; private set; } = FDriverLocationStatus.Ready;
        public FDriverLocationSnapshot? LatestLocation { get; set; } = Fix();
        public Func<CancellationToken, Task<FDriverLocationSnapshot?>>? Query { get; set; }
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged;
        public event EventHandler<string>? ListeningFailed;
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsListening = Status != FDriverLocationStatus.PermissionRequired;
            return Task.FromResult(IsListening);
        }
        public void StopListening() { IsListening = false; LatestLocation = null; }
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Status == FDriverLocationStatus.PermissionRequired ? Task.FromResult<FDriverLocationSnapshot?>(null)
                : Query?.Invoke(cancellationToken) ?? Task.FromResult(LatestLocation);
        public void Emit(FDriverLocationSnapshot fix, bool ready = true)
        {
            LatestLocation = fix;
            if (ready) Status = FDriverLocationStatus.Ready;
            LocationChanged?.Invoke(this, fix);
        }
        public void DenyPermission()
        {
            Status = FDriverLocationStatus.PermissionRequired;
            IsListening = false;
            LatestLocation = null;
            ListeningFailed?.Invoke(this, "위치 사용이 허용되지 않았습니다.");
        }
    }
}
