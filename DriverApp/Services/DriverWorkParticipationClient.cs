using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;

namespace DriverApp.Services;

public sealed class DriverWorkParticipationClient : IDriverWorkParticipationClient
{
    private readonly IDriverApiClient api;
    private readonly IAuthSession session;
    public DriverWorkParticipationClient(IDriverApiClient api, IAuthSession session)
    {
        this.api = api; this.session = session;
        session.Changed += () => SessionChanged?.Invoke();
    }
    public DriverWorkParticipationIdentity Identity => new(session.UserId, session.SessionRevision, session.IsAuthenticated);
    public event Action? SessionChanged;
    public async Task<DriverWorkParticipationResponse> GetAsync(CancellationToken cancellationToken = default)
        => await api.GetAsync<DriverWorkParticipationResponse>(IDriverWorkParticipationClient.Endpoint, "업무·적재 설정 조회", cancellationToken)
            ?? throw new InvalidDataException("업무·적재 설정을 확인하지 못했습니다.");
    public async Task<DriverWorkParticipationResponse> UpdateAsync(DriverWorkParticipationUpdate request, CancellationToken cancellationToken = default)
        => await api.PutAsync<DriverWorkParticipationUpdate, DriverWorkParticipationResponse>(
            IDriverWorkParticipationClient.Endpoint, request, "업무·적재 설정 저장", cancellationToken)
            ?? throw new InvalidDataException("저장 결과를 확인하지 못했습니다.");
}
