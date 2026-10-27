using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.JSInterop;
using Ssalddel.Contracts.Common.Platform;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.WebApp;

public sealed class CommunityMapGoogleSdkAdapterTests
{
    [Fact]
    public async Task 초기화는_module_접근성_설정_조회_SDK순서를_유지하고_동일한_선택과_callback을_전달한다()
    {
        var calls = new List<string>();
        var js = new RecordingJs(calls) { ReducedMotion = true };
        var runtime = new GoogleMapsBrowserRuntimeResponse { BrowserApiKey = "synthetic-only", AllowedOrigins = ["https://unit.invalid"] };
        var http = new HttpClient(new RuntimeHandler(calls, runtime)) { BaseAddress = new("https://unit.invalid/") };
        await using var adapter = Create(js, new(http));
        var markers = new[] { new { id = "public-marker" } };
        var reducedMotion = false;

        await adapter.InitializeAsync(() => markers, () => "public", () => "KR", () => "public-marker", value => { calls.Add("configure"); reducedMotion = value; });

        Assert.Equal(new[] { "import", "prefersReducedMotion", "configure", "runtime", "initialize" }, calls);
        Assert.True(reducedMotion);
        Assert.True(adapter.HasModule);
        Assert.Equal(GoogleMapState.Ready, adapter.State);
        var args = Assert.Single(js.Invocations.Where(x => x.Name == "initialize")).Args;
        Assert.Equal("community-world-google-map", args[0]);
        Assert.Same(markers, args[1]);
        Assert.Equal("public", args[2]);
        Assert.Equal("KR", args[3]);
        Assert.Equal("public-marker", args[4]);
        Assert.Same(adapter, Assert.IsType<DotNetObjectReference<커뮤니티지도GoogleSdkAdapter>>(args[5]).Value);
        var sentRuntime = Assert.IsType<GoogleMapsBrowserRuntimeResponse>(args[6]);
        Assert.Equal(runtime.BrowserApiKey, sentRuntime.BrowserApiKey);
        Assert.Equal(runtime.AllowedOrigins, sentRuntime.AllowedOrigins);
    }

    [Theory]
    [InlineData("ready", "Ready")]
    [InlineData("failed", "Failed")]
    [InlineData("blocked-origin", "BlockedOrigin")]
    [InlineData("unconfigured", "Unconfigured")]
    [InlineData("unexpected-status", "Unconfigured")]
    public async Task SDK_상태를_그대로_구분하고_ready일_때만_지도데이터를_변경한다(string status, string expectedState)
    {
        var expected = Enum.Parse<GoogleMapState>(expectedState);
        var js = new RecordingJs([]) { Status = status };
        await using var adapter = Create(js);
        await adapter.InitializeAsync(() => Array.Empty<object>(), () => "public", () => null, () => null, _ => { });
        js.Invocations.Clear();

        var markers = new[] { "marker-2" };
        await adapter.UpdateDatasetAsync(markers, "other", "JP", "marker-2", preserveViewport: true);
        await adapter.UpdateSelectionAsync("JP", "marker-2");
        await adapter.UpdateTransportSimulationLayerAsync(new[] { "route-1" }, animationEnabled: false);
        await adapter.FocusElementAsync("application-card");

        Assert.Equal(expected, adapter.State);
        Assert.Equal(expected == GoogleMapState.Ready ? 4 : 1, js.Invocations.Count);
        Assert.Equal("focusElement", js.Invocations[^1].Name);
        Assert.Equal("application-card", js.Invocations[^1].Args[0]);
        if (expected == GoogleMapState.Ready)
        {
            var dataset = js.Invocations[0];
            Assert.Equal("updateDataset", dataset.Name);
            Assert.Same(markers, dataset.Args[1]);
            Assert.Equal(new object?[] { "other", "JP", "marker-2", true }, dataset.Args.Skip(2));
            Assert.Equal(new object?[] { "community-world-google-map", "JP", "marker-2" }, js.Invocations[1].Args);
            var overlay = js.Invocations[2];
            Assert.Equal("updateTransportSimulationLayer", overlay.Name);
            var options = overlay.Args[2]!;
            Assert.Equal(false, options.GetType().GetProperty("animationEnabled")!.GetValue(options));
            Assert.Equal(12, options.GetType().GetProperty("maxVisibleObjects")!.GetValue(options));
            Assert.Equal("google-overlay-canvas", options.GetType().GetProperty("rendererKind")!.GetValue(options));
        }
    }

    [Fact]
    public async Task 초기화중_선택이_바뀌면_런타임조회후의_최신마커와_선택을_SDK에_전달한다()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        var js = new RecordingJs(calls);
        using var http = new HttpClient(new RuntimeHandler(calls, new(), gate.Task)) { BaseAddress = new("https://unit.invalid/") };
        await using var adapter = Create(js, new(http));
        object markers = new[] { "before" };
        var dataset = "before-dataset";
        var country = "KR";
        var marker = "before-marker";
        var reads = 0;

        var pending = adapter.InitializeAsync(() => { reads++; return markers; }, () => dataset, () => country, () => marker, _ => { });
        Assert.Equal(0, reads);
        markers = new[] { "after" };
        dataset = "after-dataset";
        country = "JP";
        marker = "after-marker";
        gate.SetResult();
        await pending;

        var args = Assert.Single(js.Invocations.Where(x => x.Name == "initialize")).Args;
        Assert.Equal(1, reads);
        Assert.Same(markers, args[1]);
        Assert.Equal(new object?[] { dataset, country, marker }, args.Skip(2).Take(3));
    }

    [Fact]
    public async Task SDK_callback_이름과_좌표를_보존하고_페이지의_선택과_신청_동작으로_돌려준다()
    {
        var received = new List<string>();
        await using var adapter = new 커뮤니티지도GoogleSdkAdapter(new RecordingJs([]), Runtime([]),
            (country, observation) => { received.Add($"select:{country}:{observation}"); return Task.CompletedTask; },
            (country, observation, x, y) => { received.Add($"open:{country}:{observation}:{x}:{y}"); return Task.CompletedTask; },
            (x, y) => { received.Add($"keyboard:{x}:{y}"); return Task.CompletedTask; });

        await adapter.SelectMapFeatureFromGoogleMap("KR", "observation");
        await adapter.OpenMapApplicationsFromGoogleMap("KR", "observation", 17, 31);
        await adapter.OpenSelectedMapApplicationsFromKeyboard(41, 53);

        Assert.Equal(new[] { "select:KR:observation", "open:KR:observation:17:31", "keyboard:41:53" }, received);
        foreach (var name in new[] { "SelectMapFeatureFromGoogleMap", "OpenMapApplicationsFromGoogleMap", "OpenSelectedMapApplicationsFromKeyboard" })
            Assert.NotNull(typeof(커뮤니티지도GoogleSdkAdapter).GetMethod(name)!.GetCustomAttribute<JSInvokableAttribute>());
    }

    [Fact]
    public async Task overlay_JS실패는_고정정보를_유지하는_메시지를_남기고_다음_성공에만_지운다()
    {
        var js = new RecordingJs([]);
        await using var adapter = Create(js);
        await adapter.InitializeAsync(() => Array.Empty<object>(), () => "public", () => null, () => null, _ => { });
        js.OverlayError = new JSException("synthetic failure");

        await adapter.UpdateTransportSimulationLayerAsync(Array.Empty<object>(), false);

        Assert.Equal(GoogleMapState.Ready, adapter.State);
        Assert.Contains("고정 예시 정보는 계속 확인", adapter.SimulationOverlayMessage);
        js.OverlayError = null;
        await adapter.UpdateTransportSimulationLayerAsync(Array.Empty<object>(), true);
        Assert.Null(adapter.SimulationOverlayMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 종료는_JS지도_해제후_module을_폐기하고_연결종료만_기존대로_흡수한다(bool disconnected)
    {
        var calls = new List<string>();
        var js = new RecordingJs(calls);
        var adapter = Create(js);
        await adapter.InitializeAsync(() => Array.Empty<object>(), () => "public", () => null, () => null, _ => { });
        calls.Clear();
        js.DisconnectedOnDispose = disconnected;

        await adapter.DisposeAsync();

        Assert.Equal(disconnected ? new[] { "dispose" } : new[] { "dispose", "module.DisposeAsync" }, calls);
    }

    [Fact]
    public async Task 초기화중에는_ready를_노출하지_않으며_SDK예외를_새로운_성공으로_바꾸지_않는다()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var js = new RecordingJs([]) { Initialization = gate.Task };
        await using var adapter = Create(js);
        var pending = adapter.InitializeAsync(() => Array.Empty<object>(), () => "public", () => null, () => null, _ => { });
        Assert.Equal(GoogleMapState.Loading, adapter.State);
        gate.SetException(new JSException("initialization failed"));
        await Assert.ThrowsAsync<JSException>(() => pending);
        Assert.Equal(GoogleMapState.Loading, adapter.State);
        Assert.True(adapter.HasModule);
    }

    private static 커뮤니티지도GoogleSdkAdapter Create(RecordingJs js, GoogleMapsBrowserRuntimeClient? runtime = null)
        => new(js, runtime ?? Runtime([]), (_, _) => Task.CompletedTask, (_, _, _, _) => Task.CompletedTask, (_, _) => Task.CompletedTask);

    private static GoogleMapsBrowserRuntimeClient Runtime(List<string> calls)
        => new(new HttpClient(new RuntimeHandler(calls, new())) { BaseAddress = new("https://unit.invalid/") });

    private sealed class RuntimeHandler(List<string> calls, GoogleMapsBrowserRuntimeResponse value, Task? responseGate = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            calls.Add("runtime");
            Assert.Equal("/" + GoogleMapsBrowserRuntimeRoutes.LocalDevelopment, request.RequestUri!.AbsolutePath);
            if (responseGate is not null) await responseGate.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        }
    }

    private sealed record Invocation(string Name, object?[] Args);

    private sealed class RecordingJs(List<string> calls) : IJSRuntime, IJSObjectReference
    {
        public string Status { get; init; } = "ready";
        public bool ReducedMotion { get; init; }
        public Task<string>? Initialization { get; init; }
        public Exception? OverlayError { get; set; }
        public bool DisconnectedOnDispose { get; set; }
        public List<Invocation> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            calls.Add(identifier);
            Invocations.Add(new(identifier, args ?? []));
            object? result = identifier switch
            {
                "import" => this,
                "prefersReducedMotion" => ReducedMotion,
                "initialize" => Initialization is null ? Status : await Initialization,
                "updateTransportSimulationLayer" when OverlayError is not null => throw OverlayError,
                "dispose" when DisconnectedOnDispose => throw new JSDisconnectedException("synthetic disconnect"),
                _ => default(TValue)
            };
            return (TValue)result!;
        }

        public ValueTask DisposeAsync()
        {
            calls.Add("module.DisposeAsync");
            return ValueTask.CompletedTask;
        }
    }
}
