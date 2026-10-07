using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

public sealed record RoleWorkspaceField(string Label, string Value);
public sealed record RoleWorkspaceSection(string Title, IReadOnlyList<RoleWorkspaceField> Fields);
/// <summary>펼치지 않아도 현재 판단·수행에 필요한 목적지와 짧은 핵심 정보입니다.</summary>
public sealed record RoleWorkspaceSummary(string DestinationLabel, string Destination,
    IReadOnlyList<RoleWorkspaceField>? Metrics = null, RoleWorkspaceField? Request = null);
public sealed record RoleWorkspaceAction(string Key, string Label, string? Route = null,
    bool IsPrimary = false, bool Enabled = true, string? DisabledReason = null, bool RequiresConfirmation = true);
public sealed record RoleWorkspaceItem(string Id, string Title, string Status, string? Subtitle = null,
    IReadOnlyList<RoleWorkspaceSection>? Sections = null, IReadOnlyList<RoleWorkspaceAction>? Actions = null,
    IReadOnlyList<NeighborhoodMapMarker>? Markers = null, IReadOnlyList<NeighborhoodMapRoute>? Routes = null,
    bool IsCurrent = false, RoleWorkspaceSummary? Summary = null);
public sealed record RoleWorkspaceSnapshot(string RoleKey, IReadOnlyList<RoleWorkspaceItem> Items,
    string? Message = null, string? SelectedId = null, IReadOnlyList<RoleWorkspaceAction>? EmptyActions = null);
public sealed record RoleWorkspaceIdentity(string? OwnerId, long Revision, bool IsAuthenticated);
public sealed record RoleWorkspaceLocation(double Latitude, double Longitude, double? AccuracyMeters, DateTimeOffset MeasuredAt);

public interface IRoleWorkspaceAdapter
{
    string RoleKey { get; }
    Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken);
    Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken);
    void Clear();
}
public interface IRoleWorkspaceAccess
{
    event Action? Changed;
    RoleWorkspaceIdentity GetIdentity(string roleKey);
    Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default);
    Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default);
    Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default);
}
public interface IRoleWorkspaceApi
{
    Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken);
    Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken);
    Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken);
    Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken);
}
public interface IRoleWorkspaceLocationProvider
{
    Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken);
}
public sealed class RoleWorkspaceAccessException(int statusCode, string message, string? errorCode = null, string? responseBody = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? ErrorCode { get; } = errorCode;
    public string? ResponseBody { get; } = responseBody;
}
