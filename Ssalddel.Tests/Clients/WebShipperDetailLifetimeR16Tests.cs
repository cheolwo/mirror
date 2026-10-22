using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using WebModel = Ssalddel.WebApp.ViewModels.ShipperRequestDetailPageViewModel;

namespace Ssalddel.Tests.Clients;

public sealed class WebShipperDetailLifetimeR16Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 이전조회의늦은성공과실패및finally는_최신대상과조회중상태를덮지않는다(bool oldFails)
    {
        using var fixture = await Fixture.CreateAsync();
        var firstRun = fixture.Model.LoadAsync("A");
        var first = await fixture.Http.NextAsync();
        var lastRun = fixture.Model.LoadAsync(" B ", created: true);
        var last = await fixture.Http.NextAsync();
        Assert.True(first.Token.IsCancellationRequested);
        Assert.Equal("B", fixture.Model.State.LookupRequestId);
        Assert.Null(fixture.Model.State.Request);
        if (oldFails) first.Completion.SetException(new IOException("이전 A 조회 실패"));
        else first.Complete("A");
        await firstRun.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(fixture.Model.State.IsBusy);
        Assert.Equal("B", fixture.Model.State.LookupRequestId);
        Assert.Null(fixture.Model.State.Request);
        Assert.Null(fixture.Model.State.QueryError);
        Assert.True(fixture.Model.State.Created);
        last.Complete("B");
        await lastRun;
        Assert.Equal("B", fixture.Model.State.Request?.RequestId);
        Assert.False(fixture.Model.State.IsBusy);
        Assert.Equal(2, fixture.Http.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 이전기능조회가늦게실패하거나비활성으로끝나도_현재화면을변경하지않는다(bool oldFails)
    {
        using var fixture = await Fixture.CreateAsync();
        var firstMetadata = new TaskCompletionSource<VersionFeatureFlagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        fixture.Metadata.Read = _ => ++calls == 1 ? firstMetadata.Task : Task.FromResult(Enabled());
        var firstRun = fixture.Model.LoadAsync("A");
        var lastRun = fixture.Model.LoadAsync("B");
        var last = await fixture.Http.NextAsync();
        if (oldFails) firstMetadata.SetException(new IOException("이전 기능 조회 실패"));
        else firstMetadata.SetResult(Enabled(false));
        await firstRun;
        Assert.True(fixture.Model.State.IsWorkflowEnabled);
        Assert.True(fixture.Model.State.IsBusy);
        Assert.Equal("B", fixture.Model.State.LookupRequestId);
        Assert.Null(fixture.Model.State.QueryError);
        last.Complete("B");
        await lastRun;
        Assert.Equal("B", fixture.Model.State.Request?.RequestId);
        Assert.Equal(1, fixture.Http.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 다른계정이나같은계정의새로그인은_이전세션응답을폐기한다(bool sameOwner)
    {
        using var fixture = await Fixture.CreateAsync();
        var firstRun = fixture.Model.LoadAsync("A");
        var first = await fixture.Http.NextAsync();
        fixture.SetOwner(sameOwner ? "owner-a" : "owner-b", "replacement-token");
        await fixture.RestoreAsync();
        Assert.True(first.Token.IsCancellationRequested);
        Assert.Null(fixture.Model.State.Request);
        Assert.False(fixture.Model.State.IsBusy);
        first.Complete("A");
        await firstRun;
        Assert.Null(fixture.Model.State.Request);
        var lastRun = fixture.Model.LoadAsync("B");
        var last = await fixture.Http.NextAsync();
        Assert.Equal("replacement-token", last.AccessToken);
        last.Complete("B");
        await lastRun;
        Assert.Equal("B", fixture.Model.State.Request?.RequestId);
    }

    [Fact]
    public async Task 로그아웃은_이미표시한개인정보와진행중응답을즉시무효화한다()
    {
        using var fixture = await Fixture.CreateAsync();
        var loaded = fixture.Model.LoadAsync("A");
        (await fixture.Http.NextAsync()).Complete("A");
        await loaded;
        Assert.NotNull(fixture.Model.State.Request);
        var pendingRun = fixture.Model.LoadAsync("B");
        var pending = await fixture.Http.NextAsync();
        await fixture.ClearAuthAsync();
        Assert.True(pending.Token.IsCancellationRequested);
        Assert.True(fixture.Model.State.RequiresLogin);
        Assert.Null(fixture.Model.State.Request);
        pending.Complete("B");
        await pendingRun;
        Assert.Null(fixture.Model.State.Request);
        Assert.True(fixture.Model.State.RequiresLogin);
        Assert.False(fixture.Model.State.IsBusy);
    }

    [Fact]
    public async Task 변경이벤트없는인증자연만료도_늦은응답과조회중상태를정리한다()
    {
        using var fixture = await Fixture.CreateAsync();
        var run = fixture.Model.LoadAsync("A");
        var pending = await fixture.Http.NextAsync();
        fixture.Guard.AllowAccess = false;
        pending.Complete("A");
        await run;
        Assert.Null(fixture.Model.State.Request);
        Assert.Null(fixture.Model.State.QueryError);
        Assert.True(fixture.Model.State.RequiresLogin);
        Assert.False(fixture.Model.State.IsBusy);
    }

    [Fact]
    public async Task 호출자취소는_늦은결과를버리고_오류없이다음조회를허용한다()
    {
        using var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var canceledRun = fixture.Model.LoadAsync("A", cancellationToken: cancellation.Token);
        var canceled = await fixture.Http.NextAsync();
        cancellation.Cancel();
        canceled.Complete("A");
        await canceledRun;
        Assert.Null(fixture.Model.State.Request);
        Assert.Null(fixture.Model.State.QueryError);
        Assert.False(fixture.Model.State.IsBusy);
        Assert.NotEqual(ShipperRequestDetailMessageTone.Error, fixture.Model.State.StatusTone);
        var nextRun = fixture.Model.LoadAsync("B");
        (await fixture.Http.NextAsync()).Complete("B");
        await nextRun;
        Assert.Equal("B", fixture.Model.State.Request?.RequestId);
    }

    [Fact]
    public async Task 화면수명종료뒤에는_늦은실패와새조회를적용하지않는다()
    {
        using var fixture = await Fixture.CreateAsync();
        var run = fixture.Model.LoadAsync("A");
        var pending = await fixture.Http.NextAsync();
        fixture.Model.Dispose();
        Assert.True(pending.Token.IsCancellationRequested);
        pending.Completion.SetException(new IOException("폐기한 화면의 실패"));
        await run;
        await fixture.Model.LoadAsync("B");
        fixture.SetOwner("owner-b");
        await fixture.RestoreAsync();
        Assert.Null(fixture.Model.State.Request);
        Assert.Null(fixture.Model.State.QueryError);
        Assert.False(fixture.Model.State.IsBusy);
        Assert.Equal(1, fixture.Http.Calls);
    }

    [Fact]
    public async Task 현재조회의시간초과는_호출자취소와달리오류로안내한다()
    {
        using var fixture = await Fixture.CreateAsync();
        var run = fixture.Model.LoadAsync("A");
        var pending = await fixture.Http.NextAsync();
        pending.Completion.SetException(new TaskCanceledException("synthetic timeout", new TimeoutException()));
        await run;
        Assert.NotNull(fixture.Model.State.QueryError);
        Assert.Equal(ShipperRequestDetailMessageTone.Error, fixture.Model.State.StatusTone);
        Assert.False(fixture.Model.State.IsBusy);
        Assert.Null(fixture.Model.State.Request);
    }

    [Fact]
    public async Task 다른의뢰응답을_선택한의뢰로표시하지않는다()
    {
        using var fixture = await Fixture.CreateAsync();
        var run = fixture.Model.LoadAsync("A");
        (await fixture.Http.NextAsync()).Complete("different-request");
        await run;
        Assert.Equal("A", fixture.Model.State.LookupRequestId);
        Assert.Null(fixture.Model.State.Request);
        Assert.NotNull(fixture.Model.State.QueryError);
    }

    [Fact]
    public async Task 기능비활성은_의뢰를조회하지않고_기존안내를유지한다()
    {
        using var fixture = await Fixture.CreateAsync();
        fixture.Metadata.Read = _ => Task.FromResult(Enabled(false));
        await fixture.Model.LoadAsync("A");
        Assert.False(fixture.Model.State.IsWorkflowEnabled);
        Assert.Equal(0, fixture.Http.Calls);
        Assert.Contains("비활성", fixture.Model.State.StatusMessage);
        Assert.False(fixture.Model.State.IsBusy);
    }

    [Fact]
    public async Task 미인증조회는_로그인을안내하고_개인원장을조회하지않는다()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.ClearAuthAsync();
        await fixture.Model.LoadAsync("A");
        Assert.True(fixture.Model.State.RequiresLogin);
        Assert.Null(fixture.Model.State.Request);
        Assert.False(fixture.Model.State.IsBusy);
        Assert.Equal(0, fixture.Http.Calls);
    }

    private static VersionFeatureFlagsResponse Enabled(bool value = true) => new()
    {
        Flags = new Dictionary<string, bool> { ["DomesticTransportWorkflow"] = value }
    };

    private sealed class Fixture : IDisposable
    {
        public StorageJs Js { get; } = new();
        public DelayedHttp Http { get; } = new();
        public SessionGuard Guard { get; } = new();
        public MetadataProxy Metadata { get; }
        public WebModel Model { get; }
        private readonly object auth;
        private readonly HttpClient client;
        private Fixture()
        {
            client = new(Http) { BaseAddress = new("https://test.invalid/") };
            SetOwner("owner-a");
            // Tests의 기존 linked 인증 사본 대신 실제 Web assembly의 인증·조회 서비스를 소비한다.
            var assembly = typeof(WebModel).Assembly;
            auth = Activator.CreateInstance(assembly.GetType("Ssalddel.WebApp.Services.WebAuthSessionService")!,
                client, Js, Guard)!;
            var settlement = Activator.CreateInstance(assembly.GetType("Ssalddel.WebApp.Services.화주결제정산Service")!,
                client, auth, new TransportRequestLedgerObserver())!;
            var metadata = DispatchProxy.Create<ICommunityProcurementClient, MetadataProxy>();
            Metadata = (MetadataProxy)(object)metadata;
            Model = (WebModel)Activator.CreateInstance(typeof(WebModel), settlement, auth, metadata)!;
        }
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            await fixture.RestoreAsync();
            return fixture;
        }
        public void SetOwner(string owner, string? token = null) => Js.Json = JsonSerializer.Serialize(new ClientAuthTokenSnapshot(
            token ?? "token-" + owner, DateTime.UtcNow.AddHours(2), "refresh-" + owner, DateTime.UtcNow.AddDays(1),
            owner, "합성 화주", ["화주"]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        public Task RestoreAsync() => (Task)auth.GetType().GetMethod("RestoreAsync")!.Invoke(auth, [CancellationToken.None])!;
        public Task ClearAuthAsync() => (Task)auth.GetType().GetMethod("ClearAsync")!.Invoke(auth, [CancellationToken.None])!;
        public void Dispose() { Model.Dispose(); client.Dispose(); }
    }

    private sealed class SessionGuard : IClientSessionGuard
    {
        private readonly ClientSessionGuard inner = new();
        public bool AllowAccess { get; set; } = true;
        public bool IsAccessTokenUsable(ClientAuthTokenSnapshot? snapshot, DateTime utcNow)
            => AllowAccess && inner.IsAccessTokenUsable(snapshot, utcNow);
        public bool IsRefreshTokenUsable(ClientAuthTokenSnapshot? snapshot, DateTime utcNow)
            => inner.IsRefreshTokenUsable(snapshot, utcNow);
    }

    public class MetadataProxy : DispatchProxy
    {
        public Func<CancellationToken, Task<VersionFeatureFlagsResponse>> Read { get; set; } = _ => Task.FromResult(Enabled());
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "GetVersionWorkflowMetadataAsync" ? Read((CancellationToken)args![0]!) : throw new NotSupportedException();
    }

    private sealed record Pending(string Id, string? AccessToken, CancellationToken Token)
    {
        public TaskCompletionSource<HttpResponseMessage> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete(string id) => Completion.SetResult(new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new 화주운송의뢰응답 { 의뢰Id = id, 의뢰상태 = "생성됨", 결제상태 = "결제대기" })
        });
    }

    private sealed class DelayedHttp : HttpMessageHandler
    {
        private readonly Channel<Pending> pending = Channel.CreateUnbounded<Pending>();
        public int Calls { get; private set; }
        public async Task<Pending> NextAsync() => await pending.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.StartsWith("/api/v1/shipper/requests/", request.RequestUri!.AbsolutePath);
            var item = new Pending(Uri.UnescapeDataString(request.RequestUri.Segments[^1]), request.Headers.Authorization?.Parameter, cancellationToken);
            Calls++;
            await pending.Writer.WriteAsync(item);
            // 미협조 전송도 늦은 성공·실패를 반환할 수 있도록 token을 일부러 기다리지 않는다.
            return await item.Completion.Task;
        }
    }

    private sealed class StorageJs : IJSRuntime
    {
        public string? Json { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identifier == "localStorage.getItem") return ValueTask.FromResult((TValue)(object?)Json!);
            if (identifier == "localStorage.removeItem") { Json = null; return ValueTask.FromResult(default(TValue)!); }
            throw new NotSupportedException(identifier);
        }
    }
}
