using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace CargoAiClientProbeR114;

internal sealed class ReadOnlyProbeApi : IRoleWorkspaceApi, IDisposable
{
    private readonly HttpClient http;
    private readonly HashSet<string> allowed;
    private readonly List<object> reads = [];
    internal int MutationsDenied { get; private set; }
    internal IReadOnlyList<object> Reads => reads;
    internal ReadOnlyProbeApi(ProbeInput input)
    {
        http = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = new(input.BaseAddress), Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);
        allowed = new(StringComparer.Ordinal)
        {
            "api/v1/driver/recommendations", "api/v1/driver/transports/workspace",
            "api/v1/driver/requests/" + Uri.EscapeDataString(input.RequestId)
        };
        if (input.TransportId is > 0) allowed.Add("api/v1/driver/transports/" + input.TransportId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
    {
        if (roleKey != RoleWorkspaceCatalog.CargoDriver || !allowed.Contains(path))
            throw new InvalidOperationException("ProbeReadOutsideSelectedSyntheticCase");
        using var response = await http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        reads.Add(new { method = "GET", path, status = (int)response.StatusCode });
        if (!response.IsSuccessStatusCode) throw new RoleWorkspaceAccessException((int)response.StatusCode, "검증 서버의 현재 조회를 확인하지 못했습니다.");
        if (response.Content.Headers.ContentLength is > 2 * 1024 * 1024) throw new InvalidOperationException("ProbeResponseTooLarge");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var limited = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            if (limited.Length + count > 2 * 1024 * 1024) throw new InvalidOperationException("ProbeResponseTooLarge");
            limited.Write(buffer, 0, count);
        }
        return JsonSerializer.Deserialize<T>(limited.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("ProbeReadReturnedNull");
    }
    private Task<T?> Deny<T>() { MutationsDenied++; throw new InvalidOperationException("ProbeMutationForbidden"); }
    public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => Deny<T>();
    public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => Deny<T>();
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken) => Deny<T>();
    public void Dispose() => http.Dispose();
}

internal sealed class ProbeAccess(string driverId) : IRoleWorkspaceAccess
{
    public event Action? Changed { add { } remove { } }
    public RoleWorkspaceIdentity GetIdentity(string roleKey) => new(driverId, 1, roleKey == RoleWorkspaceCatalog.CargoDriver);
    public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ProbeAuthenticationMutationForbidden");
    public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ProbeAuthenticationMutationForbidden");
}

/// <summary>현재 API 응답을 제품의 순수 표시 함수로 변환한 결과만 공급한다. 위치 갱신을 포함하는 운영 adapter를 호출하지 않는다.</summary>
internal sealed class ProjectionAdapter(RoleWorkspaceSnapshot snapshot) : IRoleWorkspaceAdapter
{
    public string RoleKey => RoleWorkspaceCatalog.CargoDriver;
    public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken) => throw new InvalidOperationException("ProbeMutationForbidden");
    public void Clear() { }
}

internal sealed class ProbeNavigation(string baseUri) : NavigationManager
{
    public void Prepare() => Initialize(baseUri, baseUri);
    protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("ProbeNavigationForbidden");
}
internal sealed class NoScriptRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new JSException("ProbeJavaScriptNotExecuted");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new JSException("ProbeJavaScriptNotExecuted");
}
internal sealed class NoMapHost : INeighborhoodMapHost
{
    public Task HideAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<NeighborhoodMapHostStatus> RenderAsync(string elementId, NeighborhoodMapRenderState state, Func<string, Task> markerSelected, CancellationToken cancellationToken = default)
        => Task.FromResult(NeighborhoodMapHostStatus.Unavailable);
}
