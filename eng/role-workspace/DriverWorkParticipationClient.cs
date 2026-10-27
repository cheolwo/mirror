using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;

namespace Ssalddel.Client.RoleWorkspace;

/// <summary>통합 앱의 현재 화물기사 계정을 기존 업무·적재 설정 계약에 연결합니다.</summary>
public sealed class DriverWorkParticipationClient(IRoleWorkspaceAccess access, IRoleWorkspaceApi api)
    : IDriverWorkParticipationClient
{
    private const string Role = RoleWorkspaceCatalog.CargoDriver;
    public DriverWorkParticipationIdentity Identity
    {
        get
        {
            var identity = access.GetIdentity(Role);
            return new(identity.OwnerId, identity.Revision, identity.IsAuthenticated);
        }
    }
    public event Action? SessionChanged { add => access.Changed += value; remove => access.Changed -= value; }
    public Task<DriverWorkParticipationResponse> GetAsync(CancellationToken cancellationToken = default)
        => api.GetAsync<DriverWorkParticipationResponse>(Role, IDriverWorkParticipationClient.Endpoint, cancellationToken);
    public async Task<DriverWorkParticipationResponse> UpdateAsync(DriverWorkParticipationUpdate request,
        CancellationToken cancellationToken = default)
        => await api.PutAsync<DriverWorkParticipationResponse>(Role, IDriverWorkParticipationClient.Endpoint, request, cancellationToken)
            ?? throw new InvalidDataException("저장 결과를 확인하지 못했습니다.");
}
