using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoCompletionRejectedRecoveryTests
{
    [Theory]
    [InlineData("pickup", "cargo.pickup")]
    [InlineData("dropoff", "cargo.dropoff")]
    public async Task 확정미적용은_같은운송GET뒤_사진을보존하고_사용자의명시재시도만허용한다(string action, string stage)
    {
        var f = new Fixture(action) { Failure = Rejection(stage) };
        using var model = f.NewModel(); await f.Ready(model);
        await model.SubmitAsync();
        Assert.False(model.AwaitingResult); Assert.True(model.Lifetime.HasError);
        Assert.Equal("현장.jpg", model.PhotoName); Assert.Empty(f.Store.Records);
        Assert.Null(model.CompletionFeedback.Current); Assert.False(model.Saved);
        await model.SubmitAsync(); Assert.Equal(1, f.Writes);
        await model.LoadAsync(1, action);
        Assert.True(model.CanSubmit);
        f.Failure = null;
        await model.SubmitAsync();
        Assert.Equal(2, f.Writes); Assert.Equal(1, f.Uploads);
        Assert.True(model.Saved); Assert.NotNull(model.CompletionFeedback.Current);
    }

    [Theory]
    [InlineData("pickup")]
    [InlineData("dropoff")]
    public async Task 전체보류거절은_미확정기록만해제하며_운송보류나허용행동을완화하지않는다(string action)
    {
        var f = new Fixture(action) { Failure = Rejection("cargo.interruption-recovery", code: "CargoTransportHeld"), HeldAfterWrite = true };
        using var model = f.NewModel(); await f.Ready(model);
        await model.SubmitAsync();
        Assert.False(model.AwaitingResult); Assert.Empty(f.Store.Records);
        await model.LoadAsync(1, action);
        Assert.True(model.Source!.운송진행보류); Assert.False(model.StageAllowsInput); Assert.False(model.CanSubmit);
        await model.SubmitAsync(); Assert.Equal(1, f.Writes);
        f.HeldAfterWrite = false; await model.LoadAsync(1, action);
        Assert.True(model.CanSubmit); Assert.Null(model.CompletionFeedback.Current);
    }

    [Theory]
    [InlineData("pickup", "cargo.pickup")]
    [InlineData("dropoff", "cargo.dropoff")]
    public async Task 거절후GET실패는_보호기록을남기며_같은화면의명시재조회뒤에만해제한다(string action, string stage)
    {
        var f = new Fixture(action) { Failure = Rejection(stage), FailReadAfterWrite = true };
        using var model = f.NewModel(); await f.Ready(model);
        await model.SubmitAsync(); Assert.True(model.AwaitingResult); Assert.Single(f.Store.Records);
        await model.SubmitAsync(); Assert.Equal(1, f.Writes);
        f.FailReadAfterWrite = false; await model.LoadAsync(1, action);
        Assert.False(model.AwaitingResult); Assert.True(model.CanSubmit); Assert.Empty(f.Store.Records);
        Assert.Equal(1, f.Writes); Assert.Null(model.CompletionFeedback.Current);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("network")]
    [InlineData("server")]
    [InlineData("unknown")]
    [InlineData("read")]
    [InlineData("other-stage")]
    [InlineData("other-os")]
    [InlineData("unstructured")]
    [InlineData("auth")]
    [InlineData("too-many")]
    [InlineData("other-recovery")]
    public async Task 미확정과_관계없는거절은_GET미완료만으로_재전송보호를해제하지않는다(string kind)
    {
        Exception failure = kind switch
        {
            "timeout" => new TaskCanceledException("합성 응답 지연"),
            "network" => new HttpRequestException("합성 연결 끊김"),
            "server" => Rejection("cargo.pickup", status: 503),
            "unknown" => Rejection("cargo.pickup", outcome: "Unknown"),
            "read" => Rejection("cargo.pickup", outcome: "Read"),
            "other-stage" => Rejection("cargo.dropoff"),
            "other-os" => Rejection("food.pickup", os: "FoodDeliveryOS"),
            "unstructured" => new RoleWorkspaceAccessException(400, "합성 거절"),
            "auth" => Rejection("cargo.pickup", status: 401),
            "too-many" => Rejection("cargo.pickup", status: 429),
            _ => Rejection("cargo.interruption-recovery", code: "OtherError")
        };
        var f = new Fixture("pickup") { Failure = failure };
        using var model = f.NewModel(); await f.Ready(model);
        await model.SubmitAsync();
        await model.LoadAsync(1, "pickup");
        Assert.True(model.AwaitingResult); Assert.False(model.CanSubmit); Assert.Single(f.Store.Records);
        await model.SubmitAsync(); Assert.Equal(1, f.Writes); Assert.Null(model.CompletionFeedback.Current);
    }

    [Theory]
    [InlineData("other-transport")]
    [InlineData("other-state")]
    [InlineData("other-driver")]
    [InlineData("privacy")]
    public async Task 거절뒤_다른운송이나단계나정보보류GET은_기존보호기록을해제하지않는다(string kind)
    {
        var f = new Fixture("pickup") { Failure = Rejection("cargo.pickup"), InvalidReadAfterWrite = kind };
        using var model = f.NewModel(); await f.Ready(model);
        await model.SubmitAsync();
        Assert.Single(f.Store.Records); Assert.False(model.CanSubmit); Assert.Null(model.CompletionFeedback.Current);
        // 권한 오류가 현재 화면을 초기화해도 영속 보호 기록은 그대로 남아야 한다.
        await model.LoadAsync(1, "pickup");
        Assert.Single(f.Store.Records); await model.SubmitAsync(); Assert.Equal(1, f.Writes);
    }

    [Fact]
    public async Task 앱재실행은_소실된거절근거를_과거미완료GET만으로복원하지않는다()
    {
        var f = new Fixture("pickup") { Failure = Rejection("cargo.pickup"), FailReadAfterWrite = true };
        using (var first = f.NewModel()) { await f.Ready(first); await first.SubmitAsync(); Assert.True(first.AwaitingResult); }
        f.FailReadAfterWrite = false;
        using var second = f.NewModel(); await second.LoadAsync(1, "pickup");
        Assert.True(second.AwaitingResult); Assert.False(second.CanSubmit); Assert.Single(f.Store.Records);
        await second.SubmitAsync(); Assert.Equal(1, f.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 보호기록정리의무효쓰기나_다른요청교체는_새명령을허용하지않는다(bool replace)
    {
        var f = new Fixture("pickup") { Failure = Rejection("cargo.pickup") };
        f.Store.NoOpRemove = !replace; f.Store.ReplaceOnRemove = replace;
        using var model = f.NewModel(); await f.Ready(model);
        await model.SubmitAsync();
        Assert.True(model.AwaitingResult); Assert.Single(f.Store.Records);
        Assert.False(model.CanSubmit); Assert.Null(model.CompletionFeedback.Current);
        var preservedId = f.Store.Records.Single().Value.RequestId;
        await model.LoadAsync(1, "pickup");
        Assert.True(model.AwaitingResult); Assert.Equal(preservedId, f.Store.Records.Single().Value.RequestId);
        await model.SubmitAsync(); Assert.Equal(1, f.Writes);
    }

    [Theory]
    [InlineData("transport")]
    [InlineData("account")]
    [InlineData("endpoint")]
    [InlineData("dispose")]
    public async Task 화면문맥전환뒤_늦은거절은_원래보호기록이나새화면을바꾸지않는다(string transition)
    {
        var f = new Fixture("pickup");
        using var model = f.NewModel(); await f.Ready(model);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.BeforeWrite = async () => { entered.SetResult(); await release.Task; };
        f.Failure = Rejection("cargo.pickup");
        var sending = model.SubmitAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (transition == "transport") await model.LoadAsync(2, "pickup");
        else if (transition == "account") f.Access.Switch();
        else if (transition == "endpoint") f.Context.Endpoint = new('1', 32);
        else model.Dispose();
        release.SetResult(); await sending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(f.Store.Records); Assert.Equal(1, f.Writes); Assert.Null(model.CompletionFeedback.Current);
        if (transition == "transport") { Assert.Equal(2, model.ItemId); Assert.Equal(2, model.Source!.Id); Assert.False(model.Lifetime.HasError); }
        else if (transition is "account" or "dispose") Assert.Null(model.Source);
    }

    private static RoleWorkspaceAccessException Rejection(string stage, string outcome = "NotApplied", int status = 400,
        string code = "CargoPhotoRequired", string os = "DomesticCargoTransportOS")
        => new(status, "합성 업무 거절", code, JsonSerializer.Serialize(new
        { errorCode = code, workflowContext = new { operatingSystemId = os, stageId = stage, outcomeCode = outcome } }));

    private sealed class Fixture(string action) : IRoleWorkspaceApi
    {
        public Access Access { get; } = new();
        public Store Store { get; } = new();
        public Context Context { get; } = new();
        public Exception? Failure;
        public bool FailReadAfterWrite, HeldAfterWrite;
        public string? InvalidReadAfterWrite;
        public Func<Task>? BeforeWrite;
        public int Writes, Uploads;
        private bool completed;
        public CargoTransportActionViewModel NewModel()
        { Context.Access = Access; return new(new(this), this, Access, recoveryStore: Store, recoveryContext: Context); }
        public async Task Ready(CargoTransportActionViewModel model)
        {
            await model.LoadAsync(1, action); model.SetPhoto(new Photo());
            model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.PaymentConfirmed = model.SaveConfirmed = true;
            Assert.True(model.CanSubmit);
        }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
        {
            if (Writes > 0 && FailReadAfterWrite) throw new HttpRequestException("합성 재조회 실패");
            var id = long.Parse(path[(path.LastIndexOf('/') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            var held = Writes > 0 && HeldAfterWrite;
            var invalid = Writes > 0 ? InvalidReadAfterWrite : null;
            return Task.FromResult((T)(object)new 기사운송상세응답
            {
                Id = invalid == "other-transport" ? 99 : id,
                상태 = invalid == "other-state" ? "배차확정" : completed ? action == "pickup" ? "상차완료" : "인수완료"
                    : action == "pickup" ? "상차지도착" : "하차지도착",
                기사_운송자 = invalid == "other-driver" ? "unrelated-driver" : Access.Owner,
                운송진행보류 = held, 개인정보제공보류 = invalid == "privacy",
                가능한행동 = held ? ["issue"] : [action, "issue"]
            });
        }
        public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        {
            Writes++;
            if (BeforeWrite is not null) await BeforeWrite(); // 취소를 무시한 늦은 응답을 대조한다.
            if (Failure is not null) throw Failure;
            completed = true;
            return (T?)(object)new 기사운송상태변경응답 { Id = 1, 상태 = action == "pickup" ? "상차완료" : "인수완료" };
        }
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct)
        { Uploads++; return Task.FromResult((T?)(object)new CargoEvidenceUploadResponse("synthetic-photo", null)); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public string Owner = "synthetic-driver";
        private long revision = 1;
        public event Action? Changed;
        public void Switch() { Owner = "other-driver"; revision++; Changed?.Invoke(); }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new(Owner, revision, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Context : IWorkflowRecoveryContext
    {
        public Access Access = null!;
        public string Endpoint = new('0', 32);
        public WorkflowRecoverySnapshot Capture()
        { var identity = Access.GetIdentity(RoleWorkspaceCatalog.CargoDriver); return new(identity.OwnerId!, identity.Revision, Endpoint); }
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => Capture() == snapshot;
    }
    private sealed class Store : IWorkflowRecoveryStore
    {
        public Dictionary<(string, string), WorkflowRecoveryRecord> Records { get; } = [];
        public bool NoOpRemove, ReplaceOnRemove;
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(Records.GetValueOrDefault((owner, scope))); }
        public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); Records.Add((record.Owner, record.Scope), record); return Task.CompletedTask; }
        public Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Records.GetValueOrDefault((owner, scope)) is { } value && value.RequestId == expectedRequestId)
            {
                if (ReplaceOnRemove) Records[(owner, scope)] = value with { RequestId = Guid.NewGuid() };
                else if (!NoOpRemove) Records.Remove((owner, scope));
            }
            return Task.CompletedTask;
        }
    }
    private sealed class Photo : IBrowserFile
    {
        public string Name => "현장.jpg";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 3;
        public string ContentType => "image/jpeg";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }
}
