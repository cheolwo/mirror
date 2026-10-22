using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoInputTimeoutRecoveryR16Tests
{
    [Fact]
    public async Task Http시간초과는_화면이탈취소와달리_오류로표시한다()
    {
        using var lifetime = new CargoInputLifetime(new Access(), RoleWorkspaceCatalog.CargoDriver, () => { });
        await lifetime.RunAsync(_ => throw new TaskCanceledException("RAW_HTTP_TIMEOUT"), failure: "처리 결과를 확인해 주세요.");
        Assert.True(lifetime.HasError); Assert.False(lifetime.IsBusy);
        Assert.Equal("처리 결과를 확인해 주세요.", lifetime.Message);
        Assert.DoesNotContain("RAW", lifetime.Message);
    }

    [Fact]
    public async Task 화면이탈취소와_이전finally는_새입력의상태를덮지않는다()
    {
        using var lifetime = new CargoInputLifetime(new Access(), RoleWorkspaceCatalog.CargoDriver, () => { });
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = lifetime.RunAsync(async ct => { await first.Task; ct.ThrowIfCancellationRequested(); });
        lifetime.Reset();
        var current = lifetime.RunAsync(_ => second.Task, "새 조회 완료");
        first.SetResult(); await old;
        Assert.True(lifetime.IsBusy); Assert.False(lifetime.HasError); Assert.Null(lifetime.Message);
        second.SetResult(); await current;
        Assert.False(lifetime.IsBusy); Assert.Equal("새 조회 완료", lifetime.Message);
    }

    [Theory]
    [InlineData("pickup")]
    [InlineData("dropoff")]
    public async Task 상하차시간초과후_오래된GET과재실행은_재POST를허용하지않는다(string action)
    {
        var api = new Api(action) { TimeoutPost = true, StaleRead = true };
        var access = new Access(); var store = new RecordingStore(); var context = Context(access);
        Guid observed;
        using (var model = Transport(api, access, store, context))
        {
            await model.LoadAsync(7, action); Ready(model);
            await model.SubmitAsync();
            Assert.True(model.AwaitingResult); Assert.True(model.Lifetime.HasError); Assert.False(model.CanSubmit);
            observed = Assert.Single(store.Saved).RequestId;
            Assert.Null(store.Saved[0].PayloadJson); Assert.Equal("7", store.Saved[0].Target);
            await model.LoadAsync(7, action);
            Assert.True(model.AwaitingResult); Assert.False(model.CompletionConfirmed);
            Ready(model); await model.SubmitAsync(); Assert.Equal(1, api.Writes); Assert.Equal(1, api.Uploads);
        }
        using var resumed = Transport(api, access, store, context);
        await resumed.LoadAsync(7, action); Ready(resumed); await resumed.SubmitAsync();
        Assert.True(resumed.AwaitingResult); Assert.Equal(1, api.Writes); Assert.Empty(store.Removed);
        api.StaleRead = false;
        await resumed.LoadAsync(7, action);
        Assert.False(resumed.AwaitingResult); Assert.True(resumed.CompletionConfirmed); Assert.False(resumed.CanSubmit);
        Assert.Equal(observed, Assert.Single(store.Removed));
        await resumed.SubmitAsync(); Assert.Equal(1, api.Writes); Assert.Equal(1, api.Uploads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수락응답시간초과는_재실행후_추천이사라져도_현재운송GET으로만복구한다(bool stale)
    {
        var api = new Api("accept") { TimeoutPost = true, StaleRead = stale };
        var access = new Access(); var store = new RecordingStore(); var context = Context(access);
        using (var model = Offer(api, access, store, context))
        {
            await model.LoadAsync("request-7"); model.ConditionsConfirmed = model.AcceptConfirmed = true;
            await model.SubmitAsync();
            Assert.True(model.AwaitingResult); Assert.True(model.Lifetime.HasError); Assert.False(model.CanSubmit);
            await model.SubmitAsync(); Assert.Equal(1, api.Writes);
        }
        api.HideRecommendations = true;
        using var resumed = Offer(api, access, store, context);
        await resumed.LoadAsync("request-7");
        if (stale)
        {
            Assert.True(resumed.AwaitingResult); Assert.False(resumed.Saved);
            resumed.ConditionsConfirmed = resumed.AcceptConfirmed = true;
            await resumed.SubmitAsync(); Assert.Equal(1, api.Writes);
            api.StaleRead = false; await resumed.LoadAsync("request-7");
        }
        Assert.True(resumed.Saved); Assert.Equal("7", resumed.TransportId);
        Assert.False(resumed.AwaitingResult); Assert.Equal(1, api.Writes); Assert.Single(store.Removed);
    }

    [Theory]
    [InlineData("pickup")]
    [InlineData("dropoff")]
    [InlineData("accept")]
    public async Task 실제화면이탈취소도_전송가능성이있으면_복구기록을유지한다(string action)
    {
        var api = new Api(action) { WaitForCancellation = true };
        var access = new Access(); var store = new RecordingStore(); var context = Context(access);
        if (action == "accept")
        {
            var model = Offer(api, access, store, context);
            await model.LoadAsync("request-7"); model.ConditionsConfirmed = model.AcceptConfirmed = true;
            var work = model.SubmitAsync(); await api.Sent.Task; model.Dispose(); await work;
            Assert.False(model.Lifetime.HasError);
            using var resumed = Offer(api, access, store, context);
            await resumed.LoadAsync("request-7");
            Assert.True(resumed.AwaitingResult); Assert.False(resumed.CanSubmit);
        }
        else
        {
            var model = Transport(api, access, store, context);
            await model.LoadAsync(7, action); Ready(model);
            var work = model.SubmitAsync(); await api.Sent.Task; model.Dispose(); await work;
            Assert.False(model.Lifetime.HasError);
            using var resumed = Transport(api, access, store, context);
            await resumed.LoadAsync(7, action);
            Assert.True(resumed.AwaitingResult); Assert.False(resumed.CanSubmit);
        }
        Assert.Single(store.Saved); Assert.Empty(store.Removed); Assert.Equal(1, api.Writes);
    }

    [Fact]
    public async Task 결과기록저장실패는_완료명령을보내지않는다()
    {
        var api = new Api("dropoff"); var access = new Access();
        using var model = Transport(api, access, new RecordingStore { RejectSave = true }, Context(access));
        await model.LoadAsync(7, "dropoff"); Ready(model); await model.SubmitAsync();
        Assert.True(model.Lifetime.HasError); Assert.False(model.Saved); Assert.False(model.CanSubmit);
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 사진업로드시간초과는_완료POST나완료관측을생성하지않는다()
    {
        var api = new Api("pickup") { TimeoutUpload = true }; var access = new Access(); var store = new RecordingStore();
        using var model = Transport(api, access, store, Context(access));
        await model.LoadAsync(7, "pickup"); Ready(model); await model.SubmitAsync();
        Assert.True(model.Lifetime.HasError); Assert.False(model.AwaitingResult);
        Assert.Equal(0, api.Writes); Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task 같은기사라도_다른서버의확인기록은_재사용하지않는다()
    {
        var access = new Access(); var store = new RecordingStore(); var endpoint = new Uri("http://localhost:5001/");
        var context = new WorkflowRecoveryContext(() => access.Identity, () => endpoint);
        var first = new CargoInputRecovery(access, store, context); first.Reset("7", "dropoff");
        await first.LoadAsync(default); await first.BeginAsync(default);
        endpoint = new Uri("http://localhost:5002/");
        Assert.Throws<OperationCanceledException>(() => first.RequireCurrent(default));
        var second = new CargoInputRecovery(access, store, context); second.Reset("7", "dropoff");
        await second.LoadAsync(default);
        Assert.True(second.CanSubmit); Assert.Null(second.Pending); Assert.Empty(store.Removed);
    }

    [Fact]
    public async Task 계정변경뒤_이전쓰기성공은_새화면을완료시키거나복구기록을지우지않는다()
    {
        var api = new Api("dropoff") { WaitForCancellation = true }; var access = new Access(); var store = new RecordingStore();
        using var model = Transport(api, access, store, Context(access));
        await model.LoadAsync(7, "dropoff"); Ready(model);
        var work = model.SubmitAsync(); await api.Sent.Task; access.Switch("other-driver"); await work;
        Assert.Null(model.Source); Assert.False(model.Saved); Assert.Empty(store.Removed); Assert.Single(store.Saved);
    }

    private static WorkflowRecoveryContext Context(Access access)
        => new(() => access.Identity, () => new Uri("http://localhost:5001/"));
    private static CargoTransportActionViewModel Transport(Api api, Access access, IWorkflowRecoveryStore store, IWorkflowRecoveryContext context)
        => new(new(api), api, access, recoveryStore: store, recoveryContext: context);
    private static CargoOfferActionViewModel Offer(Api api, Access access, IWorkflowRecoveryStore store, IWorkflowRecoveryContext context)
        => new(new(api), access, store, context);
    private static void Ready(CargoTransportActionViewModel model)
    {
        model.SetPhoto(new Photo());
        model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.PaymentConfirmed = model.SaveConfirmed = true;
    }
    private sealed class Photo : IBrowserFile
    {
        public string Name => "현장.jpg"; public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 3; public string ContentType => "image/jpeg";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }
    private sealed class RecordingStore : IWorkflowRecoveryStore
    {
        private readonly VolatileWorkflowRecoveryStore inner = new();
        public bool RejectSave { get; init; }
        public List<WorkflowRecoveryRecord> Saved { get; } = [];
        public List<Guid> Removed { get; } = [];
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => inner.LoadAsync(owner, scope, ct);
        public async Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
        {
            if (RejectSave) throw new IOException("Synthetic protected store failure");
            await inner.SaveAsync(record, ct); Saved.Add(record);
        }
        public async Task RemoveAsync(string owner, string scope, Guid id, CancellationToken ct)
        { await inner.RemoveAsync(owner, scope, id, ct); Removed.Add(id); }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public RoleWorkspaceIdentity Identity { get; private set; } = new("driver-7", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string role) => Identity;
        public void Switch(string owner) { Identity = new(owner, Identity.Revision + 1, true); Changed?.Invoke(); }
        public Task EnsureInitializedAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string role, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Api(string action) : IRoleWorkspaceApi
    {
        private bool applied;
        private readonly DateTime starts = DateTime.UtcNow.AddMinutes(-1);
        public bool TimeoutPost { get; init; }
        public bool WaitForCancellation { get; init; }
        public bool TimeoutUpload { get; init; }
        public bool StaleRead { get; set; }
        public bool HideRecommendations { get; set; }
        public int Writes { get; private set; }
        public int Uploads { get; private set; }
        public TaskCompletionSource Sent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
        {
            Assert.Equal(RoleWorkspaceCatalog.CargoDriver, role);
            object result = path.EndsWith("/recommendations", StringComparison.Ordinal)
                ? (IReadOnlyList<기사배차추천항목응답>)(HideRecommendations ? [] : [new() { 의뢰Id = "request-7", 차량적합여부 = true,
                    추천라운드 = 1, 추천시작시각 = starts, 추천만료시각 = starts.AddHours(1), 기사지급예정운임 = 6000m,
                    기사지급예정운임근거 = "운임구성.기사지급예정운임" }])
                : path.EndsWith("/workspace", StringComparison.Ordinal)
                    ? new 기사화물운송작업공간응답 { 활성운송목록 = applied && !StaleRead ? [new() { Id = 7, 운송번호 = "request-7", 상태 = "배차확정" }] : [] }
                : path.Contains("/requests/", StringComparison.Ordinal)
                    ? new 기사운송의뢰상세응답 { 의뢰Id = "request-7", 차량적합여부 = true, 배차상태 = "대기" }
                : new 기사운송상세응답 { Id = 7, 운송번호 = "request-7", 상태 = applied && !StaleRead
                    ? action == "pickup" ? "상차완료" : "인수완료" : action == "pickup" ? "상차지도착" : "하차지도착" };
            return Task.FromResult((T)result);
        }
        public async Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct)
        {
            Writes++; Sent.TrySetResult();
            if (WaitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            applied = true;
            if (TimeoutPost) throw new TaskCanceledException("Synthetic response timeout");
            object result = action == "accept" ? new 기사배차처리응답 { RequestId = "request-7" }
                : new 기사운송상태변경응답 { Id = 7, 상태 = action == "pickup" ? "상차완료" : "인수완료" };
            return (T)result;
        }
        public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct)
        {
            Uploads++;
            if (TimeoutUpload) throw new TaskCanceledException("Synthetic upload timeout");
            return Task.FromResult((T?)(object)new CargoEvidenceUploadResponse("private-photo-ref", null));
        }
    }
}
