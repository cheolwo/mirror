using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients
{
    internal sealed class FDriverTestSession : IFDriverAuthSession
    {
        public event EventHandler? SessionChanged;
        private ClientAuthTokenSnapshot? snapshot;
        private readonly ClientSessionGuard guard = new();
        public FDriverTestSession(bool expired = false) => snapshot = Snapshot(expired);
        public int ClearCount { get; private set; }
        public int ApplyCount { get; private set; }
        public Exception? ClearFailure { get; set; }
        public Func<Task<ClientAuthSessionRestoreState>>? Restore { get; set; }
        public string? AccessToken => snapshot?.AccessToken;
        public DateTime AccessTokenExpiresAtUtc => snapshot?.AccessTokenExpiresAtUtc ?? default;
        public string? RefreshToken => snapshot?.RefreshToken;
        public DateTime RefreshTokenExpiresAtUtc => snapshot?.RefreshTokenExpiresAtUtc ?? default;
        public string? UserId => snapshot?.UserId;
        public string? UserName => snapshot?.UserName;
        public IReadOnlyList<string> Roles => snapshot?.Roles ?? [];
        public bool IsAuthenticated => CurrentState == ClientAuthSessionRestoreState.Authenticated;
        public ClientAuthSessionRestoreState CurrentState => guard.IsAccessTokenUsable(snapshot, DateTime.UtcNow)
            ? ClientAuthSessionRestoreState.Authenticated
            : guard.IsRefreshTokenUsable(snapshot, DateTime.UtcNow)
                ? ClientAuthSessionRestoreState.RefreshRequired
                : ClientAuthSessionRestoreState.Anonymous;
        public Task<ClientAuthSessionRestoreState> RestoreAsync(CancellationToken cancellationToken = default)
            => Restore?.Invoke() ?? Task.FromResult(CurrentState);
        public Task ApplyAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            snapshot = value;
            SessionChanged?.Invoke(this, EventArgs.Empty);
            ApplyCount++;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            snapshot = null;
            SessionChanged?.Invoke(this, EventArgs.Empty);
            ClearCount++;
            if (ClearFailure is not null) throw ClearFailure;
            return Task.CompletedTask;
        }
        private static ClientAuthTokenSnapshot Snapshot(bool expired) => new(
            "test-access", DateTime.UtcNow.AddMinutes(expired ? -5 : 30), "test-refresh",
            DateTime.UtcNow.AddHours(1), "test-driver", "검증 기사", ["Driver"]);
    }

    internal sealed class FDriverTestHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return send(request, cancellationToken);
        }
        public static HttpResponseMessage TokenResponse() => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new 토큰응답
            {
                AccessToken = "test-new-access",
                RefreshToken = "test-new-refresh",
                AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                UserId = "test-driver", UserName = "검증 기사", Roles = ["Driver"]
            })
        };
    }

    internal sealed class FDriverTestWorkspaceApi : IFoodDeliveryDriverApiService
    {
        public Func<string, CancellationToken, Task<FoodDeliveryCompletedDeliveryDetailDto>> CompletedDetail { get; set; }
            = (_, _) => throw new FDriverApiException("상세 조회 결과가 없습니다.", HttpStatusCode.NotFound);
        public Task<FoodDeliveryCompletedDeliveryDetailDto> GetCompletedDeliveryDetailAsync(string settlementId, CancellationToken cancellationToken = default)
            => CompletedDetail(settlementId, cancellationToken);
        public Func<DateOnly, CancellationToken, Task<FoodDeliveryDailySettlementDto>> DailySettlement { get; set; }
            = (date, _) => Task.FromResult(new FoodDeliveryDailySettlementDto { DriverId = "test-driver", CompletionDateKst = date });
        public Task<FoodDeliveryDailySettlementDto> GetDailySettlementAsync(DateOnly date, CancellationToken cancellationToken = default)
            => DailySettlement(date, cancellationToken);
        public Func<CancellationToken, Task<운영배차수신상태Dto>> Availability { get; set; }
            = _ => Task.FromResult(new 운영배차수신상태Dto());
        public Func<운영배차수신의사변경요청, CancellationToken, Task<운영배차수신상태Dto>>? ChangeIntent { get; set; }
        public Func<string, 음식배달가게도착요청, CancellationToken, Task<FoodDeliveryDriverActionResponse>>? Arrival { get; set; }
        public Func<string, 음식배달중단요청, CancellationToken, Task<FoodDeliveryDriverActionResponse>>? Interruption { get; set; }
        public int StopWorkCalls { get; private set; }
        public int LocationCalls { get; private set; }
        public Func<CancellationToken, Task<FoodDeliveryDriverWorkspaceDto>> Workspace { get; set; }
            = _ => Task.FromResult(Data("before"));
        public Func<CancellationToken, Task<FoodDeliveryDriverRouteResponseDto>> Route { get; set; }
            = _ => Task.FromResult(new FoodDeliveryDriverRouteResponseDto());
        public int WorkStatusCalls { get; private set; }
        public string WorkStatus { get; set; } = "운행종료";
        public List<FoodDeliveryDriverRouteRequestDto> RouteRequests { get; } = [];
        public Task<FoodDeliveryDriverWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
            => Workspace(cancellationToken);
        public Task<기사운행상태응답?> GetWorkStatusAsync(CancellationToken cancellationToken = default)
        {
            WorkStatusCalls++;
            return Task.FromResult<기사운행상태응답?>(new() { Status = WorkStatus });
        }
        public Task StartWorkAsync(string startLocation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopWorkAsync(CancellationToken cancellationToken = default) { StopWorkCalls++; return Task.CompletedTask; }
        public Task<기사위치갱신응답?> UpdateLocationAsync(기사위치갱신요청 request, CancellationToken cancellationToken = default)
        { LocationCalls++; return Task.FromResult<기사위치갱신응답?>(null); }
        public Task<운영배차수신상태Dto> GetDispatchAvailabilityAsync(CancellationToken cancellationToken = default)
            => Availability(cancellationToken);
        public Task<운영배차수신상태Dto> ChangeDispatchIntentAsync(운영배차수신의사변경요청 request, CancellationToken cancellationToken = default)
            => ChangeIntent?.Invoke(request, cancellationToken) ?? Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = request.수신의사Code });
        public Task<FoodDeliveryDriverActionResponse> RecordRestaurantArrivalAsync(string offerId, 음식배달가게도착요청 request, CancellationToken cancellationToken = default)
            => Arrival?.Invoke(offerId, request, cancellationToken) ?? Action();
        public Task<FoodDeliveryDriverActionResponse> InterruptAsync(string offerId, 음식배달중단요청 request, CancellationToken cancellationToken = default)
            => Interruption?.Invoke(offerId, request, cancellationToken) ?? Action();
        public Task<FoodDeliveryDriverActionResponse> AcceptAsync(string offerId, CancellationToken cancellationToken = default) => Action();
        public Task<FoodDeliveryDriverActionResponse> RejectAsync(string offerId, CancellationToken cancellationToken = default) => Action();
        public Task<FoodDeliveryDriverActionResponse> AcceptBundleAsync(IReadOnlyList<string> offerIds, CancellationToken cancellationToken = default) => Action();
        public Task<FoodDeliveryDriverActionResponse> ConfirmPickupAsync(string offerId, CancellationToken cancellationToken = default) => Action();
        public Task<FoodDeliveryDriverActionResponse> CompleteAsync(string offerId, CancellationToken cancellationToken = default) => Action();
        public Task<FoodDeliveryDriverRouteResponseDto> GetRouteAsync(FoodDeliveryDriverRouteRequestDto request, CancellationToken cancellationToken = default)
        {
            RouteRequests.Add(request);
            return Route(cancellationToken);
        }
        private static Task<FoodDeliveryDriverActionResponse> Action() => Task.FromResult(new FoodDeliveryDriverActionResponse());
        public static FoodDeliveryDriverWorkspaceDto Data(string id, bool coordinates = false) => new()
        {
            DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3,
            Recommendations = [new()
            {
                OfferId = id, RestaurantName = "검증 음식점", DriverPayout = 2500,
                Pickup = new() { Label = "음식점", Latitude = coordinates ? 37.5m : null, Longitude = coordinates ? 127m : null },
                Dropoff = new() { Label = "전달지", Latitude = coordinates ? 37.6m : null, Longitude = coordinates ? 127.1m : null }
            }]
        };
    }

    internal sealed class FDriverTestLocationService : IFDriverLocationService
    {
        public bool IsListening => false;
        public FDriverLocationSnapshot? LatestLocation => null;
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged { add { } remove { } }
        public event EventHandler<string>? ListeningFailed { add { } remove { } }
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false) => Task.FromResult(false);
        public void StopListening() { }
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<FDriverLocationSnapshot?>(null);
    }

    internal sealed class FDriverFixedTestLocationService : IFDriverLocationService
    {
        public bool IsListening => true;
        public FDriverLocationSnapshot? LatestLocation => new(37.49m, 127m, 5, DateTime.UtcNow);
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged { add { } remove { } }
        public event EventHandler<string>? ListeningFailed { add { } remove { } }
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false) => Task.FromResult(true);
        public void StopListening() { }
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(LatestLocation);
    }

    internal static class FDriverLifecycleTestSupport
    {
        public static MainPageModel Model(FDriverTestSession session, FDriverTestWorkspaceApi api, IFDriverLocationService? location = null)
        {
            var http = new HttpClient(new FDriverTestHttpHandler((_, _) => Task.FromResult(FDriverTestHttpHandler.TokenResponse())))
                { BaseAddress = new Uri("http://localhost/") };
            return new MainPageModel(new(), session, new(http, session), api, location ?? new FDriverTestLocationService(), new());
        }
        public static CancellationToken WorkspaceToken(MainPageModel model)
            => ((CancellationTokenSource)typeof(MainPageModel).GetField("_workspaceCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(model)!).Token;
        public static Task RefreshBackground(MainPageModel model)
            => (Task)typeof(MainPageModel).GetMethod("RefreshWorkspaceFromBackgroundAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(model, ["검증 갱신", WorkspaceToken(model)])!;
        public static void MonitorTask(MainPageModel model, Task task)
            => typeof(MainPageModel).GetField("_monitorTask", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, task);
        public static Task? MonitorTask(MainPageModel model)
            => (Task?)typeof(MainPageModel).GetField("_monitorTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model);
    }
}

// Device boundaries only. These tests execute the linked production API/ViewModel but do not
// provide evidence for Android dispatching, GPS, SecureStorage, or UI rendering.
namespace FDriverApp.Services
{
    internal static class SecureStorage
    {
        public static UnavailableStorage Default { get; } = new();
        public sealed class UnavailableStorage
        {
            public Task<string?> GetAsync(string key) => throw new NotSupportedException("실제 SecureStorage는 단위 시험 범위 밖입니다.");
            public Task SetAsync(string key, string value) => throw new NotSupportedException("실제 SecureStorage는 단위 시험 범위 밖입니다.");
            public bool Remove(string key) => throw new NotSupportedException("실제 SecureStorage는 단위 시험 범위 밖입니다.");
        }
    }
}

namespace FDriverApp.PageModels
{
    internal static class MainThread
    {
        public static Task InvokeOnMainThreadAsync(Action action) { action(); return Task.CompletedTask; }
        public static Task InvokeOnMainThreadAsync(Func<Task> action) => action();
    }
}
