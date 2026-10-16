using System.Net;
using System.Net.Http.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace SsalddelAdminApp.Services;

public interface IDispatchTrainingClient
{
    Task<DispatchTrainingCapabilityDto> CapabilityAsync(CancellationToken ct);
    Task<DispatchTrainingSessionDto> CurrentAsync(CancellationToken ct);
    Task<DispatchTrainingDecisionsResponse> DecisionsAsync(CancellationToken ct);
    Task<DispatchTrainingIncidentsResponse> IncidentsAsync(CancellationToken ct);
    Task<DispatchTrainingCommandResultDto> CommandAsync(string action, DispatchTrainingCommandRequest request, CancellationToken ct);
}

/// <summary>전용 훈련 호스트의 명시 경로만 사용합니다. 제품 API로 우회하거나 합성 주문을 화면에서 만들지 않습니다.</summary>
public sealed class DispatchTrainingClient(HttpClient http, AdminAuthenticatedApiClient api) : IDispatchTrainingClient
{
    public async Task<DispatchTrainingCapabilityDto> CapabilityAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync(DispatchTrainingRoutes.Api + "/capability", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return new();
        if (!response.IsSuccessStatusCode) throw new AdminApiException("훈련 기능을 확인하지 못했습니다.", response.StatusCode);
        return await response.Content.ReadFromJsonAsync<DispatchTrainingCapabilityDto>(ct)
            ?? throw new AdminApiException("훈련 기능 응답을 확인하지 못했습니다.", response.StatusCode);
    }
    public Task<DispatchTrainingSessionDto> CurrentAsync(CancellationToken ct)
        => api.GetAsync<DispatchTrainingSessionDto>(DispatchTrainingRoutes.Api + "/current", ct);
    public Task<DispatchTrainingDecisionsResponse> DecisionsAsync(CancellationToken ct)
        => api.GetAsync<DispatchTrainingDecisionsResponse>(DispatchTrainingRoutes.Api + "/decisions", ct);
    public Task<DispatchTrainingIncidentsResponse> IncidentsAsync(CancellationToken ct)
        => api.GetAsync<DispatchTrainingIncidentsResponse>(DispatchTrainingRoutes.Api + "/incidents", ct);
    public Task<DispatchTrainingCommandResultDto> CommandAsync(string action, DispatchTrainingCommandRequest request, CancellationToken ct)
    {
        if (action is not (DispatchTrainingCommandCodes.Start or DispatchTrainingCommandCodes.Pause or DispatchTrainingCommandCodes.Resume
            or DispatchTrainingCommandCodes.Stop or DispatchTrainingCommandCodes.SupportDrivers)) throw new ArgumentException("훈련 명령 확인 필요", nameof(action));
        return api.PostAsync<DispatchTrainingCommandRequest, DispatchTrainingCommandResultDto>(DispatchTrainingRoutes.Api + "/" + action, request, ct);
    }
}

public interface IDispatchTrainingIdentity
{
    string? Owner { get; }
    bool IsAdmin { get; }
    event Action? Changed;
    Task<string?> EnsureAsync(CancellationToken ct);
}

public sealed class DispatchTrainingIdentity(AdminAuthSession session, AdminAuthService auth) : IDispatchTrainingIdentity
{
    public string? Owner => session.AuthenticationOwnerId;
    public bool IsAdmin => session.IsServerAdmin;
    public event Action? Changed { add => session.Changed += value; remove => session.Changed -= value; }
    public Task<string?> EnsureAsync(CancellationToken ct) => auth.EnsureAccessTokenAsync(cancellationToken: ct);
}
