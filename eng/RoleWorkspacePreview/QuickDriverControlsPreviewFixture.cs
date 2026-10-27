using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace RoleWorkspacePreview;

/// <summary>실제 공통 퀵 기사 화면에 메모리 예시만 공급한다. 운영 HTTP·DB·GPS·사진 저장·지급은 호출하지 않는다.</summary>
public sealed class QuickDriverControlsPreviewFixture : IRoleWorkspaceApi, IRoleWorkspaceAccess, IRoleWorkspaceLocationProvider, IDisposable
{
    private const string Owner = "preview-quick-driver";
    private const long TransportId = 1101;
    private readonly PreviewClock clock = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool automaticReadScheduled;
    private Guid? startRequestId;
    private string transportState = "운송중";
    private bool loseStartResponse, loseIntentResponse, loseCompletionResponse;

    public QuickDriverControlsPreviewFixture() => Model = CreateModel();
    public RoleWorkspaceViewModel Model { get; private set; }
    public string Scenario { get; private set; } = "";
    public bool OnDuty { get; private set; }
    public bool Receiving { get; private set; }
    public bool Active { get; private set; }
    public bool LocationAllowed { get; private set; } = true;
    public int StartPosts { get; private set; }
    public int StopPosts { get; private set; }
    public int IntentWrites { get; private set; }
    public int LocationPosts { get; private set; }
    public int ExplicitLocationRequests { get; private set; }
    public int Uploads { get; private set; }
    public int CompletionPosts { get; private set; }
    public int TransportReads { get; private set; }
    public string CurrentTransportState => transportState;
    public event Action? Changed;

    public async Task ConfigureControlsAsync(string scenario)
    {
        ResetScenario(scenario);
        OnDuty = scenario is "on" or "off" or "active" or "toggle-lost";
        Receiving = scenario is "on" or "active";
        Active = scenario == "active";
        LocationAllowed = scenario != "permission-denied";
        loseStartResponse = scenario == "start-lost";
        loseIntentResponse = scenario == "toggle-lost";
        await Model.InitializeAsync(RoleWorkspaceCatalog.CargoDriver);
    }

    public void ConfigureAction(string scenario)
    {
        ResetScenario(scenario);
        OnDuty = true;
        Active = !scenario.StartsWith("dropoff-completed", StringComparison.Ordinal);
        transportState = scenario switch
        {
            "pickup-completed" => "상차완료",
            "dropoff-completed" => "인수완료",
            _ when scenario.StartsWith("dropoff", StringComparison.Ordinal) => "하차지도착",
            _ => "상차지도착"
        };
        loseCompletionResponse = scenario.EndsWith("-lost", StringComparison.Ordinal);
    }

    private void ResetScenario(string scenario)
    {
        Model.PropertyChanged -= ModelChanged;
        Model.Dispose();
        Scenario = scenario;
        OnDuty = Receiving = Active = loseStartResponse = loseIntentResponse = loseCompletionResponse = false;
        LocationAllowed = true;
        transportState = "운송중";
        startRequestId = null;
        StartPosts = StopPosts = IntentWrites = LocationPosts = ExplicitLocationRequests = Uploads = CompletionPosts = TransportReads = 0;
        Model = CreateModel();
    }

    private RoleWorkspaceViewModel CreateModel()
    {
        var model = new RoleWorkspaceViewModel([new CargoDriverWorkspaceAdapter(new(this), this, clock)], this, new RoleWorkspaceState(), clock);
        model.PropertyChanged += ModelChanged;
        return model;
    }

    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken token)
    {
        Require(roleKey, token);
        object value = path switch
        {
            "api/v1/driver/work/status" => new 기사운행상태응답
            {
                DriverId = Owner, Status = OnDuty ? "운행중" : "대기", ShiftId = OnDuty ? 110 : null,
                운송실행유형 = 운송실행유형코드.화물운송, WorkStartClientRequestId = startRequestId, UpdatedAt = clock.GetUtcNow().UtcDateTime
            },
            "api/v1/driver/operational-dispatch/availability" => Availability(),
            "api/v1/driver/transports/workspace" => new 기사화물운송작업공간응답
            {
                활성운송목록 = Active ? [Transport()] : [], 다음행동운송 = Active ? Transport() : null
            },
            "api/v1/driver/recommendations" => Array.Empty<기사배차추천항목응답>(),
            "api/v1/driver/transports/1101" => ReadTransport(),
            _ => throw new InvalidOperationException("이 모의 화면에서 지원하지 않는 조회입니다.")
        };
        return Task.FromResult((T)value);
    }

    private 기사운송상세응답 ReadTransport() { TransportReads++; Changed?.Invoke(); return Transport(); }
    private 기사운송상세응답 Transport() => new()
    {
        Id = TransportId, 운송번호 = "예시 퀵 1101", 기사_운송자 = Owner, 상태 = transportState,
        출발지 = "서울 중랑구 사가정로 · 예시 상차 장소", 도착지 = "서울 중랑구 면목로 · 예시 하차 장소",
        상차담당자명 = "예시 상차 담당자", 수령자명 = "예시 수령 담당자", 전달요청 = "예시 담당자에게 물품을 전달해 주세요.",
        운임 = 6000, 예상거리Km = 2.4m, 거리계산방식 = "합성 예시 거리 · 실제 경로 아님",
        픽업위도 = 37.580m, 픽업경도 = 127.087m, 하차위도 = 37.583m, 하차경도 = 127.089m,
        인수증필요 = true, 인수증서명필수 = true, UpdatedAt = clock.GetUtcNow().UtcDateTime,
        가능한행동 = transportState switch
        {
            "상차지도착" => ["pickup", "issue"], "하차지도착" => ["dropoff", "issue"],
            "인수완료" => [], _ => ["arrive-dropoff", "issue"]
        }
    };

    public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken token)
    {
        Require(roleKey, token);
        object? value;
        switch (path)
        {
            case "api/v1/driver/work/start":
                startRequestId = ((기사운행시작요청)body).ClientRequestId;
                OnDuty = true; StartPosts++; Changed?.Invoke();
                if (loseStartResponse) { loseStartResponse = false; throw new HttpRequestException("모의 운행 시작 응답 유실"); }
                value = new 기사운행시작응답 { DriverId = Owner, Status = "운행중", ShiftId = 110, StartedAt = clock.GetUtcNow().UtcDateTime };
                break;
            case "api/v1/driver/work/stop":
                if (Active) throw new InvalidOperationException("진행 중인 예시 운송이 있습니다.");
                StopPosts++; OnDuty = Receiving = false; value = new { }; break;
            case "api/v1/driver/work/location":
                var location = (기사위치갱신요청)body;
                if (location.AppKey != 기사앱식별자.CargoYongdalDriverApp) throw new InvalidOperationException("예시 화물 기사 앱을 확인해 주세요.");
                LocationPosts++;
                value = new 기사위치갱신응답 { DriverId = Owner, Status = "운행중", 현재위도 = location.위도, 현재경도 = location.경도 };
                break;
            case "api/v1/driver/transports/1101/pickup-complete":
                if (transportState != "상차지도착" || string.IsNullOrWhiteSpace(((기사운송상차완료요청)body).상차사진ObjectName))
                    throw new InvalidOperationException("예시 상차 상태와 사진을 확인해 주세요.");
                transportState = "상차완료"; CompletionPosts++; value = Completion(); break;
            case "api/v1/driver/transports/1101/complete":
                if (transportState != "하차지도착" || string.IsNullOrWhiteSpace(((기사운송하차완료요청)body).하차사진ObjectName))
                    throw new InvalidOperationException("예시 하차 상태와 사진을 확인해 주세요.");
                transportState = "인수완료"; Active = false; CompletionPosts++; value = Completion(); break;
            case "api/v1/driver/transports/1101/arrive-dropoff":
                transportState = "하차지도착"; value = Completion(); break;
            default: throw new InvalidOperationException("이 모의 화면에서 지원하지 않는 변경입니다.");
        }
        Changed?.Invoke();
        if (CompletionPosts > 0 && loseCompletionResponse)
        { loseCompletionResponse = false; throw new HttpRequestException("모의 완료 저장 응답 유실 · GET으로 확인 필요"); }
        return Task.FromResult((T?)value);
    }

    private 기사운송상태변경응답 Completion() => new() { Id = TransportId, 운송번호 = "예시 퀵 1101", 상태 = transportState, UpdatedAt = clock.GetUtcNow().UtcDateTime };
    public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken token)
    {
        Require(roleKey, token);
        if (path != "api/v1/driver/operational-dispatch/availability/intent") throw new InvalidOperationException("지원하지 않는 모의 수신 변경입니다.");
        var request = (운영배차수신의사변경요청)body;
        Receiving = request.수신의사Code == 운영배차수신의사Code.On; IntentWrites++; Changed?.Invoke();
        if (loseIntentResponse) { loseIntentResponse = false; throw new HttpRequestException("모의 수신 변경 응답 유실"); }
        return Task.FromResult((T?)(object)Availability());
    }
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken token)
    {
        Require(roleKey, token);
        if (path != "api/v1/files/upload") throw new InvalidOperationException("지원하지 않는 모의 사진 업로드입니다.");
        Uploads++; Changed?.Invoke();
        return Task.FromResult((T?)(object)new CargoEvidenceUploadResponse("preview-only/quick-1101-photo", null));
    }
    private 운영배차수신상태Dto Availability() => new()
    {
        주체Id = Owner, 수신의사Code = Receiving ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
        실효상태Code = Receiving && OnDuty ? 운영배차실효상태Code.배차가능 : 운영배차실효상태Code.조건부적합,
        서버관측시각Utc = clock.GetUtcNow()
    };
    public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken token)
    { ExplicitLocationRequests++; Changed?.Invoke(); return GetCurrentWithoutPermissionPromptAsync(token); }
    public Task<RoleWorkspaceLocation?> GetCurrentWithoutPermissionPromptAsync(CancellationToken token)
    { token.ThrowIfCancellationRequested(); return Task.FromResult<RoleWorkspaceLocation?>(LocationAllowed ? new(37.580, 127.087, 5, clock.GetUtcNow()) : null); }
    public RoleWorkspaceIdentity GetIdentity(string roleKey) => new(Owner, 1, true);
    public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
    { Require(roleKey, cancellationToken); return Task.CompletedTask; }
    public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    private static void Require(string role, CancellationToken token)
    { token.ThrowIfCancellationRequested(); if (role != RoleWorkspaceCatalog.CargoDriver) throw new InvalidOperationException("예시 퀵 기사 역할이 일치하지 않습니다."); }
    private void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (lifetime.IsCancellationRequested || automaticReadScheduled || Model.CommandBusy
            || !Model.FoodDispatchStateUnconfirmed && !Model.FoodStartStateUnconfirmed) return;
        automaticReadScheduled = true; _ = ReadAutomaticallyAsync();
    }
    private async Task ReadAutomaticallyAsync()
    {
        try { await Task.Delay(TimeSpan.FromSeconds(3), lifetime.Token); clock.Advance(TimeSpan.FromSeconds(15)); await Model.TickAsync(); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally { automaticReadScheduled = false; }
    }
    public void Dispose()
    { lifetime.Cancel(); Model.PropertyChanged -= ModelChanged; Model.Dispose(); lifetime.Dispose(); }
    private sealed class PreviewClock : TimeProvider
    {
        private long offsetTicks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddTicks(Interlocked.Read(ref offsetTicks));
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref offsetTicks, elapsed.Ticks);
    }
}
