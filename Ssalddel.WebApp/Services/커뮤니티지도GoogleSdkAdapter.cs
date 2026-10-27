using Microsoft.JSInterop;

namespace Ssalddel.WebApp.Services;

internal enum GoogleMapState
{
    Loading,
    Ready,
    Unconfigured,
    BlockedOrigin,
    Failed
}

// SDK 자원과 JS callback만 소유하며 선택·이동·업무 명령은 페이지의 좁은 동작으로 돌려줍니다.
internal sealed class 커뮤니티지도GoogleSdkAdapter(
    IJSRuntime js,
    GoogleMapsBrowserRuntimeClient runtime,
    Func<string, string, Task> selectFeature,
    Func<string, string, double, double, Task> openApplications,
    Func<double, double, Task> openSelectedApplications) : IAsyncDisposable
{
    private const string ElementId = "community-world-google-map";
    private IJSObjectReference? _module;
    private DotNetObjectReference<커뮤니티지도GoogleSdkAdapter>? _callback;

    public GoogleMapState State { get; private set; } = GoogleMapState.Loading;
    public bool HasModule => _module is not null;
    public string? SimulationOverlayMessage { get; private set; }

    public async Task InitializeAsync(
        Func<object> markers,
        Func<string> datasetCode,
        Func<string?> countryCode,
        Func<string?> selectedMarkerId,
        Action<bool> configureReducedMotion)
    {
        _module = await js.InvokeAsync<IJSObjectReference>(
            "import",
            "./js/community-world-google-map.js");
        configureReducedMotion(await _module.InvokeAsync<bool>("prefersReducedMotion"));
        _callback = DotNetObjectReference.Create(this);
        var runtimeSettings = await runtime.TryGetAsync();
        var status = await _module.InvokeAsync<string>(
            "initialize",
            ElementId,
            markers(),
            datasetCode(),
            countryCode(),
            selectedMarkerId(),
            _callback,
            runtimeSettings);

        State = status switch
        {
            "ready" => GoogleMapState.Ready,
            "failed" => GoogleMapState.Failed,
            "blocked-origin" => GoogleMapState.BlockedOrigin,
            _ => GoogleMapState.Unconfigured
        };
    }

    public async Task UpdateDatasetAsync(
        object markers, string datasetCode, string? countryCode, string? selectedMarkerId, bool preserveViewport)
    {
        if (State == GoogleMapState.Ready && _module is not null)
        {
            await _module.InvokeVoidAsync(
                "updateDataset", ElementId, markers, datasetCode, countryCode, selectedMarkerId, preserveViewport);
        }
    }

    public async Task UpdateSelectionAsync(string? countryCode, string? selectedMarkerId)
    {
        if (State == GoogleMapState.Ready && _module is not null)
        {
            await _module.InvokeVoidAsync("updateSelection", ElementId, countryCode, selectedMarkerId);
        }
    }

    public async Task FocusElementAsync(string elementId)
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("focusElement", elementId);
        }
    }

    public async Task UpdateTransportSimulationLayerAsync(object routes, bool animationEnabled)
    {
        if (State != GoogleMapState.Ready || _module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync(
                "updateTransportSimulationLayer",
                ElementId,
                routes,
                new
                {
                    animationEnabled,
                    maxVisibleObjects = 12,
                    rendererKind = "google-overlay-canvas"
                });
            SimulationOverlayMessage = null;
        }
        catch (JSException exception)
        {
            Console.Error.WriteLine($"Transport simulation overlay failed: {exception.Message}");
            SimulationOverlayMessage = "Google 지도 시뮬레이션 overlay를 표시하지 못했습니다. 고정 예시 정보는 계속 확인할 수 있습니다.";
        }
    }

    [JSInvokable]
    public Task SelectMapFeatureFromGoogleMap(string countryCode, string observationId)
        => selectFeature(countryCode, observationId);

    [JSInvokable]
    public Task OpenMapApplicationsFromGoogleMap(string countryCode, string observationId, double clientX, double clientY)
        => openApplications(countryCode, observationId, clientX, clientY);

    [JSInvokable]
    public Task OpenSelectedMapApplicationsFromKeyboard(double clientX, double clientY)
        => openSelectedApplications(clientX, clientY);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", ElementId);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _callback?.Dispose();
    }
}
