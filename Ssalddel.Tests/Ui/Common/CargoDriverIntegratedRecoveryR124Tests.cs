using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoDriverIntegratedRecoveryR124Tests
{
    [Fact]
    public async Task 통합_설정은_현재_기사계정과_기존_판본계약으로_저장후_재조회한다()
    {
        var access = new Access(); var api = new Api();
        var client = new Ssalddel.Client.RoleWorkspace.DriverWorkParticipationClient(access, api);
        using var model = new DriverWorkParticipationViewModel(client);
        await model.LoadAsync();
        model.VehicleKind = DriverVehicleKinds.Truck; model.CargoTransport = true; model.CapacityKg = 1000;
        await model.SaveAsync();
        Assert.True(model.IsLoaded); Assert.False(model.NeedsResultCheck); Assert.Equal(1, model.ConfirmedRevision);
        Assert.Equal(1, api.SettingsWrites); Assert.Equal(2, api.SettingsReads);
        Assert.False(model.CargoEligible); // 희망 설정 저장을 서버 자격 취득으로 바꾸지 않는다.
        access.SignOut();
        Assert.True(model.RequiresLogin); Assert.False(model.CanEdit);
    }

    [Fact]
    public void 업무설정은_현재운송_행동과_분리한_보조도구로_진입한다()
    {
        var session = new CargoDriverWorkSession(new(new Api()), null);
        var link = Assert.Single(session.Actions(false).Where(action => action.Key == CargoDriverWorkSession.WorkSettings));
        Assert.Equal("/workspace/cargo-driver/settings", link.Route);
        Assert.False(link.RequiresConfirmation);
        Assert.Equal(RoleWorkspaceActionUsage.OptionalTool, RoleWorkspaceActionPolicy.GetUsage(RoleWorkspaceCatalog.CargoDriver, link));
    }

    [Fact]
    public async Task 명시적_거절응답과_최신목록을_확인하고_지도선택을_해제한다()
    {
        var access = new Access(); var api = new Api(); var store = new RecordingStore();
        using var model = Model(api, access, store);
        await ReadyToReject(model);
        model.RejectReason = "차량·적재 조건이 맞지 않음";
        await model.RejectAsync();
        Assert.True(model.Rejected); Assert.False(model.Saved); Assert.False(model.AwaitingRejectionResult);
        Assert.False(model.CanSubmit); Assert.False(model.CanReject);
        Assert.Equal("/workspace/cargo-driver", model.ReturnHref);
        Assert.Equal(1, api.RejectWrites); Assert.Equal(0, api.AcceptWrites);
        Assert.Equal(model.RejectReason, api.LastReason);
        Assert.Equal(Assert.Single(store.Saved).RequestId, Assert.Single(store.Removed));
        Assert.Null(store.Saved[0].PayloadJson);
    }

    [Fact]
    public async Task 거절전_다른_추천라운드가_오면_확인을_초기화하고_전송하지_않는다()
    {
        var access = new Access(); var api = new Api(); var store = new RecordingStore();
        using var model = Model(api, access, store);
        await ReadyToReject(model);
        model.ConditionsConfirmed = model.AcceptConfirmed = true;
        api.Round = 2;
        await model.RejectAsync();
        Assert.True(model.RecommendationChanged); Assert.False(model.RejectInputVisible);
        Assert.False(model.ConditionsConfirmed); Assert.False(model.AcceptConfirmed);
        Assert.Equal(0, api.RejectWrites); Assert.Empty(store.Saved);
        model.OpenRejection(); await model.RejectAsync();
        Assert.True(model.Rejected); Assert.Equal(1, api.RejectWrites);
    }

    [Fact]
    public async Task 거절전_보수조건이_바뀌어도_이전_확인을_재사용하지_않는다()
    {
        var access = new Access(); var api = new Api(); var store = new RecordingStore();
        using var model = Model(api, access, store);
        await ReadyToReject(model); api.Payout = 9000;
        await model.RejectAsync();
        Assert.True(model.RecommendationChanged); Assert.False(model.RejectInputVisible);
        Assert.Equal(0, api.RejectWrites); Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task 거절응답이_유실되면_추천소멸만으로_성공처리하거나_재전송하지_않는다()
    {
        var access = new Access(); var api = new Api { TimeoutReject = true }; var store = new RecordingStore();
        Guid requestId;
        using (var model = Model(api, access, store))
        {
            await ReadyToReject(model); await model.RejectAsync();
            requestId = Assert.Single(store.Saved).RequestId;
            Assert.True(model.AwaitingRejectionResult); Assert.True(model.Lifetime.HasError);
            await model.RejectAsync();
            model.ConditionsConfirmed = model.AcceptConfirmed = true; await model.SubmitAsync();
        }
        using var resumed = Model(api, access, store);
        await resumed.LoadAsync("request-7");
        await resumed.RejectAsync(); await resumed.SubmitAsync();
        Assert.True(resumed.AwaitingRejectionResult); Assert.False(resumed.Rejected); Assert.False(resumed.Saved);
        Assert.False(resumed.CanSubmit); Assert.False(resumed.CanReject);
        Assert.Equal(1, api.RejectWrites); Assert.Equal(0, api.AcceptWrites);
        Assert.Equal(requestId, Assert.Single(store.Saved).RequestId); Assert.Empty(store.Removed);
    }

    [Fact]
    public async Task 거절응답후_재조회실패는_같은화면에서_GET만_다시확인한다()
    {
        var access = new Access(); var api = new Api { FailReadback = true }; var store = new RecordingStore();
        using var model = Model(api, access, store);
        await ReadyToReject(model); await model.RejectAsync();
        Assert.True(model.AwaitingRejectionResult); Assert.False(model.Rejected);
        api.FailReadback = false;
        await model.LoadAsync("request-7");
        Assert.True(model.Rejected); Assert.False(model.AwaitingRejectionResult);
        Assert.Equal(1, api.RejectWrites); Assert.Single(store.Removed);
    }

    [Fact]
    public async Task 거절응답후_오래된추천이_남아있으면_최신읽기까지_대기한다()
    {
        var access = new Access(); var api = new Api { StaleRecommendations = true }; var store = new RecordingStore();
        using var model = Model(api, access, store);
        await ReadyToReject(model); await model.RejectAsync();
        Assert.True(model.AwaitingRejectionResult); Assert.False(model.Rejected);
        api.StaleRecommendations = false;
        await model.LoadAsync("request-7");
        Assert.True(model.Rejected); Assert.Equal(1, api.RejectWrites);
    }

    [Fact]
    public async Task 거절결과를_기다리다_다른화면의_활성운송이_확인되면_거절로_표시하지_않는다()
    {
        var access = new Access(); var api = new Api { TimeoutReject = true }; var store = new RecordingStore();
        using var model = Model(api, access, store);
        await ReadyToReject(model); await model.RejectAsync();
        api.HasTransport = true;
        await model.LoadAsync("request-7");
        Assert.True(model.Saved); Assert.False(model.Rejected); Assert.False(model.AwaitingRejectionResult);
        Assert.Equal("7", model.TransportId); Assert.Equal(1, api.RejectWrites);
    }

    [Fact]
    public async Task 계정이_바뀌면_늦은_거절응답이_새계정에_성공을_표시하지_않는다()
    {
        var access = new Access(); var api = new Api { AfterReject = access.SignOut }; var store = new RecordingStore();
        using var model = Model(api, access, store);
        await ReadyToReject(model); await model.RejectAsync();
        Assert.False(model.Rejected); Assert.Null(model.Offer); Assert.Null(model.Source);
        Assert.True(model.Lifetime.RequiresLogin); Assert.Empty(store.Removed);
    }

    [Fact]
    public async Task 사유가_비었거나_300자를_넘으면_거절을_전송하지_않는다()
    {
        var access = new Access(); var api = new Api(); var store = new RecordingStore();
        using var model = Model(api, access, store);
        await ReadyToReject(model);
        model.RejectReason = " "; await model.RejectAsync();
        model.RejectReason = new string('가', 301); await model.RejectAsync();
        Assert.Equal(0, api.RejectWrites); Assert.Empty(store.Saved);
    }

    private static CargoOfferActionViewModel Model(Api api, Access access, IWorkflowRecoveryStore store)
        => new(new(api), access, store, new WorkflowRecoveryContext(() => access.Identity, () => new Uri("http://localhost:5001/")));
    private static async Task ReadyToReject(CargoOfferActionViewModel model)
    {
        await model.LoadAsync("request-7"); Assert.True(model.CanReject);
        model.OpenRejection(); Assert.True(model.CanConfirmRejection);
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public RoleWorkspaceIdentity Identity { get; private set; } = new("driver-7", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string role) => Identity;
        public void SignOut() { Identity = new(null, Identity.Revision + 1, false); Changed?.Invoke(); }
        public Task EnsureInitializedAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string role, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string role, CancellationToken ct = default) { SignOut(); return Task.CompletedTask; }
    }
    private sealed class RecordingStore : IWorkflowRecoveryStore
    {
        private readonly VolatileWorkflowRecoveryStore inner = new();
        public List<WorkflowRecoveryRecord> Saved { get; } = [];
        public List<Guid> Removed { get; } = [];
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => inner.LoadAsync(owner, scope, ct);
        public async Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct) { await inner.SaveAsync(record, ct); Saved.Add(record); }
        public async Task RemoveAsync(string owner, string scope, Guid requestId, CancellationToken ct)
        { await inner.RemoveAsync(owner, scope, requestId, ct); Removed.Add(requestId); }
    }
    private sealed class Api : IRoleWorkspaceApi
    {
        private readonly DateTime starts = DateTime.UtcNow.AddMinutes(-1);
        private bool rejected;
        private DriverWorkParticipationSettings settings = new() { VehicleKind = DriverVehicleKinds.Car };
        public int Round { get; set; } = 1;
        public decimal Payout { get; set; } = 6000;
        public bool TimeoutReject { get; init; }
        public bool FailReadback { get; set; }
        public bool StaleRecommendations { get; set; }
        public bool HasTransport { get; set; }
        public Action? AfterReject { get; init; }
        public int RejectWrites { get; private set; }
        public int AcceptWrites { get; private set; }
        public int SettingsReads { get; private set; }
        public int SettingsWrites { get; private set; }
        public string? LastReason { get; private set; }
        public Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
        {
            Assert.Equal(RoleWorkspaceCatalog.CargoDriver, role);
            object value;
            if (path == IDriverWorkParticipationClient.Endpoint)
            {
                SettingsReads++; value = SettingsResponse();
            }
            else if (path.EndsWith("/recommendations", StringComparison.Ordinal))
                value = (IReadOnlyList<기사배차추천항목응답>)(rejected && !StaleRecommendations ? [] : [new()
                {
                    의뢰Id = "request-7", 차량적합여부 = true, 추천라운드 = Round, 추천시작시각 = starts,
                    추천만료시각 = starts.AddHours(1), 기사지급예정운임 = Payout, 기사지급예정운임근거 = "운임구성.기사지급예정운임"
                }]);
            else if (path.EndsWith("/workspace", StringComparison.Ordinal))
            {
                if (rejected && FailReadback) throw new HttpRequestException("Synthetic GET failure");
                value = new 기사화물운송작업공간응답 { 활성운송목록 = HasTransport ? [new() { Id = 7, 운송번호 = "request-7", 상태 = "배차확정" }] : [] };
            }
            else value = new 기사운송의뢰상세응답 { 의뢰Id = "request-7", 차량적합여부 = true, 배차상태 = "대기" };
            return Task.FromResult((T)value);
        }
        public Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct)
        {
            Assert.Equal(RoleWorkspaceCatalog.CargoDriver, role);
            if (path.EndsWith("/reject", StringComparison.Ordinal))
            {
                Assert.Equal("api/v1/driver/dispatch-actions/request-7/reject", path);
                RejectWrites++; LastReason = Assert.IsType<기사배차거절요청>(body).사유; rejected = true;
                AfterReject?.Invoke();
                if (TimeoutReject) throw new TaskCanceledException("Synthetic response timeout");
                return Task.FromResult<T?>(default);
            }
            AcceptWrites++; return Task.FromResult((T?)(object)new 기사배차처리응답 { RequestId = "request-7" });
        }
        public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct)
        {
            Assert.Equal(RoleWorkspaceCatalog.CargoDriver, role); Assert.Equal(IDriverWorkParticipationClient.Endpoint, path);
            var update = Assert.IsType<DriverWorkParticipationUpdate>(body);
            Assert.Equal(settings.Revision, update.ExpectedRevision);
            SettingsWrites++; settings = update.Settings with { Revision = update.ExpectedRevision + 1 };
            return Task.FromResult((T?)(object)SettingsResponse());
        }
        private DriverWorkParticipationResponse SettingsResponse()
            => new() { DriverId = "driver-7", Settings = settings, CargoEligible = false, OperationalMaxConcurrentJobs = 1 };
        public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
    }
}
