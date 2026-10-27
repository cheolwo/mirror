using Ssalddel.Contracts.Driver.Notification;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverNotifications;

namespace DriverApp.Services;

public sealed class DriverRecommendationSettingsClient(IAuthSession auth, IDriverNotificationApiService api,
    IDriverCargoRecommendationAlertService alerts) : IDriverRecommendationSettingsClient
{
    public event Action? Changed { add => auth.Changed += value; remove => auth.Changed -= value; }
    public RoleWorkspaceIdentity Identity => new(auth.UserId, auth.SessionRevision, auth.IsAuthenticated);
    public Task InitializeAsync(CancellationToken ct) => auth.RestoreAsync(ct);
    public Task<기사알림설정응답?> LoadAsync(CancellationToken ct) => api.설정조회Async(ct);
    public async Task SaveAsync(기사알림설정수정요청 request, CancellationToken ct)
    {
        // A rejected/uncertain write must not leave an older visible recommendation alert behind.
        alerts.Cancel();
        await api.설정수정Async(request, ct);
    }
}
