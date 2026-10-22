using System.Net;
using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Microsoft.AspNetCore.Components;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Clients;

public sealed class CargoCompletionDurableRecoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 새VM은_미확정관측을복원하고_GET실제완료만확인하여_보호기록과사진을정리한다(bool pickup)
    {
        using var f = new Fixture(pickup);
        using (var first = f.NewPage())
        {
            await first.Prepare(); await first.Complete();
            Assert.True(first.Pending); Assert.False(first.CanComplete); Assert.True(first.HasPhoto);
            Assert.Equal(1, f.Completion.Calls);
        }
        using var next = f.NewPage(); await next.Initialize();
        Assert.True(next.Pending); Assert.False(next.CanComplete); Assert.True(next.HasPhoto);
        Assert.Equal("Unknown", next.Failure?.WorkflowContext?.OutcomeCode);
        await next.Complete(); Assert.Equal(1, f.Completion.Calls);
        f.Api.State = pickup ? "상차완료" : "인수완료";
        await next.Initialize();
        Assert.False(next.Pending); Assert.Null(next.Failure); Assert.False(next.HasPhoto);
        Assert.Empty(f.Backend.Values); Assert.Equal(1, f.Completion.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장실패나_noop쓰기에는_POST를보내지않고_보관복구뒤명시적으로다시진행한다(bool noOpWrite)
    {
        using var f = new Fixture(true); using var page = f.NewPage(); await page.Prepare();
        f.Backend.FailWrite = !noOpWrite; f.Backend.NoOpWrite = noOpWrite;
        await page.Complete();
        Assert.Equal(0, f.Completion.Calls); Assert.True(page.HasPhoto);
        Assert.False(page.Pending); Assert.True(page.CanComplete);
        f.Backend.FailWrite = false; f.Backend.NoOpWrite = false;
        f.Completion.Send = _ => { f.Api.State = "상차완료"; return Task.FromResult(new DriverTransportCompletionPhotoResult(true, true, "확인됨")); };
        await page.Complete(); Assert.Equal(1, f.Completion.Calls); Assert.Empty(f.Backend.Values);
    }

    [Fact]
    public async Task 보호조회실패는_운송정보가있어도_완료버튼을허용하지않는다()
    {
        using var f = new Fixture(true); f.Backend.FailRead = true;
        using var page = f.NewPage(); await page.Prepare(expectReady: false);
        Assert.False(page.CanComplete); Assert.True(page.Pending);
        await page.Complete(); Assert.Equal(0, f.Completion.Calls);
        f.Backend.FailRead = false; await page.Initialize(); await page.ConfirmFields();
        Assert.False(page.Pending); Assert.True(page.CanComplete);
    }

    [Fact]
    public async Task 삭제_noop는_정본완료와구분하여_정리미확인안내를남긴다()
    {
        using var f = new Fixture(false);
        using (var first = f.NewPage()) { await first.Prepare(); await first.Complete(); }
        f.Api.State = "인수완료"; f.Backend.NoOpRemove = true;
        using var next = f.NewPage(); await next.Initialize();
        Assert.Equal("CargoCompletionRecoveryCleanupPending", next.Failure?.코드);
        Assert.Equal("Applied", next.Failure?.WorkflowContext?.OutcomeCode);
        Assert.False(next.CanComplete); Assert.NotEmpty(f.Backend.Values); Assert.True(next.HasPhoto);
        f.Backend.NoOpRemove = false; await next.Initialize();
        Assert.Null(next.Failure); Assert.Empty(f.Backend.Values); Assert.False(next.HasPhoto);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 준비미완료증거는_GET확인뒤에만_해당미확정관측을해제한다(bool pickup)
    {
        using var f = new Fixture(pickup); using var page = f.NewPage(); await page.Prepare();
        f.Completion.Send = _ => Task.FromResult(new DriverTransportCompletionPhotoResult(false, false, "준비 미완료", CompletionNotSubmitted: true));
        await page.Complete();
        Assert.Equal(1, f.Completion.Calls); Assert.False(page.Pending); Assert.True(page.CanComplete);
        Assert.Empty(f.Backend.Values); Assert.True(page.HasPhoto);
    }

    [Theory]
    [InlineData("CargoPickupPhotoRequired", "cargo.pickup")]
    [InlineData("CargoTransportHeld", "cargo.interruption-recovery")]
    public async Task 정형_NotApplied만_같은활성관측과정본조회에묶어_해제한다(string code, string stage)
    {
        using var f = new Fixture(true); using var page = f.NewPage(); await page.Prepare();
        var problem = SsalddelApiProblemParser.Parse(System.Text.Json.JsonSerializer.Serialize(new
        { workflowContext = new { operatingSystemId = "DomesticCargoTransportOS", stageId = stage, outcomeCode = "NotApplied" } }));
        f.Completion.Send = _ => Task.FromException<DriverTransportCompletionPhotoResult>(new DriverApiException(HttpStatusCode.BadRequest, "상차", code, null, problem));
        await page.Complete();
        Assert.False(page.Pending); Assert.True(page.CanComplete); Assert.Empty(f.Backend.Values);
        Assert.Equal(code, page.Failure?.코드);
    }

    [Fact]
    public async Task 늦은A거절을두번관측해도_후속B보호기록을지우지않는다()
    {
        using var f = new Fixture(true); await f.Auth.RestoreAsync();
        var state = new DriverCargoCompletionRecoverySession(f.Recovery, DriverTransportCompletionPhotoKind.Pickup); state.Reset(11);
        var snapshot = state.StartQuery(); await state.ObserveAsync(snapshot, false, false, default); await state.BeginAsync(default);
        var a = state.Pending!;
        await f.Recovery.ClearAsync(snapshot!, DriverTransportCompletionPhotoKind.Pickup, 11, a.RequestId);
        var b = await f.Recovery.BeginAsync(snapshot!, DriverTransportCompletionPhotoKind.Pickup, 11, Guid.NewGuid());
        await state.ObserveAsync(snapshot, false, true, default);
        await state.ObserveAsync(snapshot, false, true, default);
        Assert.Equal(b.RequestId, state.Pending?.RequestId);
        Assert.Equal(b.RequestId, (await f.Recovery.LoadAsync(snapshot!, DriverTransportCompletionPhotoKind.Pickup, 11))?.RequestId);
        Assert.False(state.CanSubmit);
    }

    [Fact]
    public async Task 같은운송번호도_서버와계정이달라지면_서로의기록을읽거나지우지않는다()
    {
        using var f = new Fixture(true); await f.Auth.RestoreAsync(); var original = f.Recovery.Capture();
        var saved = await f.Recovery.BeginAsync(original, DriverTransportCompletionPhotoKind.Pickup, 11, Guid.NewGuid());
        f.Endpoint = new("https://second.example.test/"); var second = f.Recovery.Capture();
        Assert.Null(await f.Recovery.LoadAsync(second, DriverTransportCompletionPhotoKind.Pickup, 11));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Recovery.ClearAsync(original, DriverTransportCompletionPhotoKind.Pickup, 11, saved.RequestId));
        await f.Auth.ApplyAsync(TokenStore.Snapshot("driver-two")); var other = f.Recovery.Capture();
        Assert.Null(await f.Recovery.LoadAsync(other, DriverTransportCompletionPhotoKind.Pickup, 11));
        f.Endpoint = new("https://first.example.test/"); await f.Auth.ApplyAsync(TokenStore.Snapshot());
        Assert.Equal(saved.RequestId, (await f.Recovery.LoadAsync(f.Recovery.Capture(), DriverTransportCompletionPhotoKind.Pickup, 11))?.RequestId);
    }

    [Fact]
    public async Task 만료된_payload없는관측도_미완료GET만으로해제하지않는다()
    {
        using var f = new Fixture(true); await f.Auth.RestoreAsync(); var snapshot = f.Recovery.Capture();
        var scope = snapshot.Scope(DriverCargoCompletionRecovery.LogicalScope(DriverTransportCompletionPhotoKind.Pickup, 11));
        await f.Store.SaveAsync(new(snapshot.Owner, scope, Guid.NewGuid(), null, DateTime.UtcNow.AddDays(-4), "11"), default);
        using var page = f.NewPage(); await page.Initialize();
        Assert.True(page.Pending); Assert.False(page.CanComplete); Assert.NotEmpty(f.Backend.Values);
        Assert.Equal(0, f.Completion.Calls);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("dispose")]
    [InlineData("transport")]
    public async Task 전환뒤늦은완료응답은_새업무를바꾸지않고_원래계정의관측은보존한다(string transition)
    {
        using var f = new Fixture(true); using var page = f.NewPage(); await page.Prepare();
        var response = new TaskCompletionSource<DriverTransportCompletionPhotoResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Completion.Send = _ => response.Task;
        var completing = page.Complete();
        Assert.NotEmpty(f.Backend.Values);
        if (transition == "logout") await f.Auth.ClearAsync();
        else if (transition == "dispose") page.Dispose();
        else await page.Initialize(22);
        response.SetException(new IOException("late response")); await completing;
        Assert.Null(page.Failure); Assert.NotEmpty(f.Backend.Values); Assert.Equal(1, f.Completion.Calls);
        if (transition == "logout") await f.Auth.ApplyAsync(TokenStore.Snapshot());
        using var restored = f.NewPage(); await restored.Initialize();
        Assert.True(restored.Pending); Assert.False(restored.CanComplete); Assert.Equal(1, f.Completion.Calls);
    }

    private sealed class Fixture : IDisposable
    {
        public AuthSession Auth { get; } = new(new TokenStore(), new ClientSessionGuard());
        public Uri Endpoint { get; set; } = new("https://first.example.test/");
        public Backend Backend { get; } = new();
        public PhotoStore Photos { get; } = new();
        public CompletionService Completion { get; } = new();
        public ProtectedWorkflowRecoveryStore Store { get; }
        public DriverCargoCompletionRecovery Recovery { get; }
        public ApiStub Api { get; }
        private readonly IDriverTransportApiService api;
        private readonly bool pickup;
        public Fixture(bool pickup)
        {
            this.pickup = pickup;
            api = DispatchProxy.Create<IDriverTransportApiService, ApiStub>(); Api = (ApiStub)(object)api;
            Api.State = pickup ? "상차지도착" : "하차지도착";
            RoleWorkspaceIdentity Identity() => new(Auth.UserId, Auth.SessionRevision, Auth.IsAuthenticated);
            Store = new(_ => Identity(), Backend.Read, Backend.Write, Backend.Remove);
            Recovery = new(Store, new WorkflowRecoveryContext(Identity, () => Endpoint));
        }
        public Page NewPage() => pickup
            ? new(new 기사상차PageViewModel(new(api, Auth), new(Completion), new Exceptions(), new NavigationStub(), Photos, Recovery), null)
            : new(null, new 기사하차PageViewModel(new(api, Auth), new(Completion), new Exceptions(), Photos, Recovery));
        public void Dispose() { }
    }

    private sealed class Page(기사상차PageViewModel? pickup, 기사하차PageViewModel? dropoff) : IDisposable
    {
        public bool Pending => pickup?.완료결과확인필요 ?? dropoff!.완료결과확인필요;
        public bool CanComplete => pickup?.완료가능 ?? dropoff!.완료가능;
        public bool HasPhoto => pickup?.사진.사진있음 ?? dropoff!.사진.사진있음;
        public Api작업오류? Failure => pickup is not null ? pickup.완료실패 : dropoff!.완료실패;
        public Task Initialize(long id = 11) => pickup?.InitializeAsync(id) ?? dropoff!.InitializeAsync(id);
        public Task Complete() => pickup?.완료Async() ?? dropoff!.완료Async();
        public async Task Prepare(bool expectReady = true)
        {
            await Initialize(); using var camera = MauiCargoPhotoTestSupport.UseSyntheticCapture();
            await (pickup?.사진.촬영Async() ?? dropoff!.사진.촬영Async()); await ConfirmFields();
            Assert.Equal(expectReady, CanComplete);
        }
        public Task ConfirmFields()
        {
            if (pickup is not null) pickup.현장.상차지확인 = pickup.현장.화물상태확인 = pickup.현장.화주연락확인 = true;
            else dropoff!.인계.하차지확인 = dropoff.인계.수령자확인 = dropoff.인계.결제증빙확인 = true;
            return Task.CompletedTask;
        }
        public void Dispose() { pickup?.Dispose(); dropoff?.Dispose(); }
    }

    private sealed class Backend
    {
        public Dictionary<string, string> Values { get; } = [];
        public bool FailWrite, NoOpWrite, FailRead, NoOpRemove;
        public Task<string?> Read(string key) => FailRead ? Task.FromException<string?>(new IOException("protected read failed"))
            : Task.FromResult(Values.GetValueOrDefault(key));
        public Task Write(string key, string value)
        {
            if (FailWrite) return Task.FromException(new IOException("protected write failed"));
            if (!NoOpWrite) Values[key] = value; return Task.CompletedTask;
        }
        public void Remove(string key) { if (!NoOpRemove) Values.Remove(key); }
    }

    public class ApiStub : DispatchProxy
    {
        public string State = string.Empty;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == nameof(IDriverTransportApiService.상세조회Async)
                ? Task.FromResult<기사운송상세응답?>(new() { Id = (long)args![0]!, 상태 = State, 운송번호 = "synthetic-cargo" })
                : throw new NotSupportedException();
    }
    private sealed class CompletionService : IDriverTransportCompletionPhotoService
    {
        public int Calls;
        public Func<DriverTransportCompletionPhoto, Task<DriverTransportCompletionPhotoResult>> Send = _ => Task.FromException<DriverTransportCompletionPhotoResult>(new IOException("lost response"));
        public Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(DriverTransportCompletionPhoto photo, CancellationToken ct = default)
        { Calls++; return Send(photo); }
    }
    private sealed class PhotoStore : IDriverCargoPhotoRecoveryStore
    {
        private readonly Dictionary<(string, DriverTransportCompletionPhotoKind, long), DriverTransportCompletionPhoto> values = [];
        public Task SaveAsync(string owner, DriverTransportCompletionPhoto photo, CancellationToken ct = default)
        { values[(owner, photo.Kind, photo.TransportId)] = photo; return Task.CompletedTask; }
        public Task<DriverTransportCompletionPhoto?> LoadAsync(string owner, DriverTransportCompletionPhotoKind kind, long id, CancellationToken ct = default)
            => Task.FromResult(values.GetValueOrDefault((owner, kind, id)));
        public Task RemoveAsync(string owner, DriverTransportCompletionPhotoKind kind, long id, CancellationToken ct = default)
        { values.Remove((owner, kind, id)); return Task.CompletedTask; }
    }
    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? value = Snapshot();
        public static ClientAuthTokenSnapshot Snapshot(string owner = "driver-one") => new("test-access", DateTime.UtcNow.AddHours(1), "test-refresh", DateTime.UtcNow.AddDays(1), owner, owner, ["Driver"]);
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken ct = default) => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken ct = default) { value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken ct = default) { value = null; return Task.CompletedTask; }
    }
    private sealed class NavigationStub : NavigationManager
    { public NavigationStub() => Initialize("http://localhost/", "http://localhost/"); protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString(); }
    private sealed class Exceptions : IDriverTransportExceptionService
    { public Task<DriverTransportExceptionReportResult> ReportAsync(DriverTransportExceptionReport report, CancellationToken ct = default) => throw new NotSupportedException(); }
}
