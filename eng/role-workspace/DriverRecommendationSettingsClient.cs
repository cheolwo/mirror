using Ssalddel.Contracts.Driver.Notification;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverNotifications;

namespace Ssalddel.Client.RoleWorkspace;

/// <summary>현재 일반 기사 계정의 기존 설정 API만 호출합니다. 음식 기사 별도 인증을 합치지 않습니다.</summary>
public sealed class DriverRecommendationSettingsClient(IRoleWorkspaceAccess access, IRoleWorkspaceApi api)
    : IDriverRecommendationSettingsClient
{
    private const string Path = "api/v1/driver/notifications/settings";
    private const string Role = RoleWorkspaceCatalog.CargoDriver;
    public event Action? Changed { add => access.Changed += value; remove => access.Changed -= value; }
    public RoleWorkspaceIdentity Identity => access.GetIdentity(Role);
    public Task InitializeAsync(CancellationToken ct) => access.EnsureInitializedAsync(Role, ct);
    public async Task<기사알림설정응답?> LoadAsync(CancellationToken ct) => await api.GetAsync<기사알림설정응답>(Role, Path, ct);
    public async Task SaveAsync(기사알림설정수정요청 request, CancellationToken ct)
        => _ = await api.PutAsync<기사알림설정응답>(Role, Path, request, ct);
}
