using System.Net;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMapHostAdapterTests
{
    [Fact]
    public async Task 지도시점은_유효한_현재_렌더의_메모리_이벤트로만_전달한다()
    {
        var module = new Module(); await using var host = Host(module);
        var viewports = new List<NeighborhoodMapViewport>(); host.ViewportChanged += viewports.Add;
        await host.RenderAsync("map", State(1), _ => Task.CompletedTask);
        await host.CameraChanged(37.5, 127.1, 13, 1);
        await host.CameraChanged(double.NaN, 127.1, 13, 1);
        await host.CameraChanged(37.5, double.PositiveInfinity, 13, 1);
        await host.CameraChanged(37.5, 127.1, 21, 1);
        await host.RenderAsync("map", State(2), _ => Task.CompletedTask);
        await host.CameraChanged(38, 128, 12, 1);
        await host.CameraChanged(37.6, 127.2, 14, 2);
        Assert.Equal(new NeighborhoodMapViewport[] { new(37.5, 127.1, 13), new(37.6, 127.2, 14) }, viewports);
    }

    [Fact]
    public async Task 가려진_지도는_선택과_시점이벤트를_차단하고_복귀때_현재조회가_필요함을_알린다()
    {
        var module = new Module(); await using var host = Host(module);
        var selected = new List<string>(); var suspended = 0; var invalidated = 0; var viewports = 0;
        host.Suspended += () => suspended++; host.Invalidated += () => invalidated++;
        host.ViewportChanged += _ => viewports++;
        await host.RenderAsync("map", State(1), id => { selected.Add(id); return Task.CompletedTask; });
        await host.MapSuspended(0);
        await host.MapMarkerSelected("private-delivery", 1);
        await host.MarkerSelected("private-delivery");
        await host.CameraChanged(37.5, 127.1, 13, 1);
        var hidden = await host.RenderAsync("map", State(2), _ => Task.CompletedTask);
        Assert.Equal("unavailable", hidden.Code); Assert.Single(module.RenderEpochs);
        Assert.Equal(1, suspended); Assert.Empty(selected); Assert.Equal(0, viewports);
        await host.MapInvalidated(0); Assert.Equal(1, invalidated);
        await host.RenderAsync("map", State(3), id => { selected.Add(id); return Task.CompletedTask; });
        await host.MapMarkerSelected("current", module.RenderEpochs.Last());
        Assert.Equal(["current"], selected);
    }

    [Fact]
    public async Task 늦게_완료한_렌더와_이전마커콜백은_현재화면을_되돌리지않는다()
    {
        var delayed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new Module { FirstRender = delayed.Task }; await using var host = Host(module);
        var selected = new List<string>();
        var first = host.RenderAsync("map", State(1), id => { selected.Add("old:" + id); return Task.CompletedTask; });
        await module.RenderStarted.Task;
        var current = await host.RenderAsync("map", State(2), id => { selected.Add(id); return Task.CompletedTask; });
        delayed.SetResult("ready");
        Assert.Equal("ready", current.Code); Assert.Equal("unavailable", (await first).Code);
        await host.MapMarkerSelected("old", 1); await host.MapMarkerSelected("current", 2);
        Assert.Equal(["current"], selected);
    }

    [Fact]
    public async Task 지도이탈과_폐기후_선택과_생명주기콜백은_남기지않는다()
    {
        var module = new Module(); var host = Host(module); var selected = 0; var invalidated = 0;
        host.Invalidated += () => invalidated++;
        await host.RenderAsync("map", State(1), _ => { selected++; return Task.CompletedTask; });
        await host.HideAsync(); await host.MarkerSelected("old");
        Assert.Equal(0, selected); Assert.Equal(1, module.HideCount);
        await host.DisposeAsync(); await host.MapInvalidated(0); await host.MapSuspended(0);
        Assert.Equal(0, invalidated); Assert.Equal(1, module.HideCount);
    }

    [Fact]
    public async Task 가려진_페이지에서_이탈한뒤_다시열면_새지도는_이전생명주기와_분리한다()
    {
        var module = new Module(); await using var host = Host(module); var suspended = 0; var invalidated = 0;
        host.Suspended += () => suspended++; host.Invalidated += () => invalidated++;
        await host.RenderAsync("map", State(1), _ => Task.CompletedTask);
        await host.MapSuspended(0); await host.HideAsync();
        var reopened = await host.RenderAsync("map", State(2), _ => Task.CompletedTask);
        Assert.Equal("ready", reopened.Code); Assert.Equal(2, module.RenderEpochs.Count);
        await host.MapSuspended(0); await host.MapInvalidated(0);
        Assert.Equal(1, suspended); Assert.Equal(0, invalidated);
        await host.MapSuspended(1); await host.MapInvalidated(1);
        Assert.Equal(2, suspended); Assert.Equal(1, invalidated);
    }

    private static NeighborhoodMapRenderState State(long revision) => new(revision, [], []);
    private static NeighborhoodGoogleMapHost Host(Module module) => new(new Js(module),
        new GoogleMapsBrowserRuntimeClient(new HttpClient(new RuntimeResponse()) { BaseAddress = new("https://api.invalid/") }));
    private sealed class RuntimeResponse : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }
    private sealed class Js(Module module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult((TValue)(object)module);
    }
    private sealed class Module : IJSObjectReference
    {
        public List<long> RenderEpochs { get; } = [];
        public int HideCount { get; private set; }
        public Task<string>? FirstRender { get; init; }
        public TaskCompletionSource<bool> RenderStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "render")
            {
                RenderEpochs.Add((long)args![5]!); RenderStarted.TrySetResult(true);
                var result = RenderEpochs.Count == 1 && FirstRender is not null ? await FirstRender : "ready";
                return (TValue)(object)result;
            }
            if (identifier == "hide") HideCount++;
            return default!;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
