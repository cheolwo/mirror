using Ssalddel.Contracts.Driver.Notification;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverNotifications;

public interface IDriverRecommendationSettingsClient
{
    event Action? Changed;
    RoleWorkspaceIdentity Identity { get; }
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<기사알림설정응답?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(기사알림설정수정요청 request, CancellationToken cancellationToken);
}
