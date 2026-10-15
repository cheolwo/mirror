using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;

namespace FDriverApp.Services;

public sealed class DriverWorkParticipationClient : IDriverWorkParticipationClient
{
    private readonly IFoodDeliveryDriverApiService api;
    private readonly IFDriverAuthSession session;
    public DriverWorkParticipationClient(IFoodDeliveryDriverApiService api, IFDriverAuthSession session)
    {
        this.api = api; this.session = session;
        session.SessionChanged += (_, _) => SessionChanged?.Invoke();
    }
    public DriverWorkParticipationIdentity Identity => new(session.UserId, session.SessionRevision, session.IsAuthenticated);
    public event Action? SessionChanged;
    public Task<DriverWorkParticipationResponse> GetAsync(CancellationToken cancellationToken = default)
        => api.GetWorkParticipationAsync(cancellationToken);
    public Task<DriverWorkParticipationResponse> UpdateAsync(DriverWorkParticipationUpdate request, CancellationToken cancellationToken = default)
        => api.UpdateWorkParticipationAsync(request, cancellationToken);
}
