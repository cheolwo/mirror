using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace RoleWorkspacePreview;

/// <summary>공통 제품 음식기사 화면의 로컬 클릭 검증용이며 운영 API에 접속하지 않습니다.</summary>
public sealed class FoodDriverControlsPreviewFixture : IRoleWorkspaceApi, IRoleWorkspaceLocationProvider, IRoleWorkspaceAccess, IDisposable
{
    private readonly PreviewClock clock = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool automaticReadScheduled;
    private Guid? startRequestId;
    public FoodDriverControlsPreviewFixture()
    {
        Model = new([new FoodDriverRoleWorkspaceAdapter(this, this, clock)], this, new RoleWorkspaceState(), clock);
        Model.PropertyChanged += ModelChanged;
    }
    public RoleWorkspaceViewModel Model { get; }
    public bool OnDuty { get; private set; } = true;
    public bool Receiving { get; private set; } = true;
    public bool Active { get; private set; }
    public bool LocationAllowed { get; private set; } = true;
    public bool FailIntent { get; private set; }
    public bool LoseIntentResponse { get; private set; }
    public bool LoseStartResponse { get; private set; }
    public int IntentWrites { get; private set; }
    public int LocationPosts { get; private set; }
    public int ExplicitLocationRequests { get; private set; }
    public int StopPosts { get; private set; }
    public event Action? Changed;

    public async Task ConfigureAsync(string scenario)
    {
        OnDuty = !scenario.StartsWith("not-working", StringComparison.Ordinal);
        Receiving = OnDuty && scenario is not ("off" or "active-off");
        Active = scenario == "active-off"; LocationAllowed = scenario != "permission";
        FailIntent = scenario == "toggle-error";
        LoseIntentResponse = scenario is "toggle-response-lost" or "not-working-intent-response-lost";
        LoseStartResponse = scenario == "not-working-start-response-lost";
        await Model.InitializeAsync("food-driver", null);
    }

    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken token)
    {
        Require(roleKey, token);
        object value = path.EndsWith("work/status", StringComparison.Ordinal) ? new 기사운행상태응답 { Status = OnDuty ? "운행중" : "운행종료",
            WorkStartClientRequestId = startRequestId, 운송실행유형 = "FoodDelivery" }
            : path.EndsWith("availability", StringComparison.Ordinal) ? Availability()
            : new FoodDeliveryDriverCurrentWorkDto { ActiveDeliveries = Active ? [new()
            {
                OfferId = "sample-offer", DeliveryAttemptId = "sample-attempt", RestaurantName = "예시 음식점", AttemptRevision = 1,
                Pickup = new() { Address = "예시 픽업 주소" }, Dropoff = new() { Address = "예시 전달 주소" }
            }] : [] };
        return Task.FromResult((T)value);
    }

    public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken token)
    {
        Require(roleKey, token); object? value = null;
        if (path.EndsWith("work/location", StringComparison.Ordinal))
        {
            var request = (기사위치갱신요청)body; LocationPosts++;
            value = new 기사위치갱신응답 { 현재위도 = request.위도, 현재경도 = request.경도 };
        }
        else if (path.EndsWith("work/stop", StringComparison.Ordinal))
        {
            if (Active) throw new InvalidOperationException("진행 중인 배달을 완료해 주세요.");
            StopPosts++; OnDuty = Receiving = false;
        }
        else if (path.EndsWith("work/start", StringComparison.Ordinal))
        {
            startRequestId = ((기사운행시작요청)body).ClientRequestId;
            OnDuty = true; Changed?.Invoke();
            if (LoseStartResponse) throw new InvalidOperationException("운행은 시작됐으나 응답이 유실되었습니다.");
            value = new 기사운행시작응답 { Status = "운행중" };
        }
        Changed?.Invoke(); return Task.FromResult((T?)value);
    }

    public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken token)
    {
        Require(roleKey, token);
        if (!path.EndsWith("operational-dispatch/availability/intent", StringComparison.Ordinal)) throw new NotSupportedException();
        if (FailIntent) throw new InvalidOperationException("신규 배차 받기 변경을 확인하지 못했습니다. 다시 시도해 주세요.");
        var request = (운영배차수신의사변경요청)body; Receiving = request.수신의사Code == 운영배차수신의사Code.On;
        IntentWrites++; Changed?.Invoke();
        if (LoseIntentResponse) throw new InvalidOperationException("서버는 수신 상태를 변경했지만 응답이 유실되었습니다.");
        return Task.FromResult((T?)(object)Availability());
    }
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken token) => throw new NotSupportedException();
    private 운영배차수신상태Dto Availability() => new()
    {
        수신의사Code = Receiving ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
        실효상태Code = Receiving && OnDuty ? 운영배차실효상태Code.배차가능 : 운영배차실효상태Code.조건부적합
    };
    public Task<RoleWorkspaceLocation?> GetCurrentWithoutPermissionPromptAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult<RoleWorkspaceLocation?>(LocationAllowed ? new(37.56, 127.07, 5, clock.GetUtcNow()) : null);
    }
    public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); ExplicitLocationRequests++; LocationAllowed = true;
        Changed?.Invoke(); return GetCurrentWithoutPermissionPromptAsync(token);
    }
    private static void Require(string role, CancellationToken token)
    { token.ThrowIfCancellationRequested(); if (role != "food-driver") throw new InvalidOperationException("Preview role mismatch."); }
    public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("preview-driver", 1, true);
    public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    private void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (lifetime.IsCancellationRequested || automaticReadScheduled || Model.CommandBusy
            || !Model.FoodDispatchStateUnconfirmed && !Model.FoodStartStateUnconfirmed) return;
        automaticReadScheduled = true;
        _ = ReadAutomaticallyAsync();
    }

    private async Task ReadAutomaticallyAsync()
    {
        try
        {
            // 제품의 15초 Tick 경로를 쓰되 로컬 프리뷰 대기만 단축합니다. 별도 화면/API 명령은 없습니다.
            await Task.Delay(TimeSpan.FromSeconds(3), lifetime.Token);
            clock.Advance(TimeSpan.FromSeconds(15));
            await Model.TickAsync();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally { automaticReadScheduled = false; }
    }

    public void Dispose()
    {
        lifetime.Cancel(); Model.PropertyChanged -= ModelChanged; Model.Dispose(); lifetime.Dispose();
    }

    private sealed class PreviewClock : TimeProvider
    {
        private long offsetTicks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddTicks(Interlocked.Read(ref offsetTicks));
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref offsetTicks, elapsed.Ticks);
    }
}
