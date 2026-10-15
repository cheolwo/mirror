using Ssalddel.Contracts.Driver.Settings;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;

public sealed record DriverWorkParticipationIdentity(string? DriverId, long SessionRevision, bool IsAuthenticated);

/// <summary>두 기사 앱의 기존 인증 HTTP와 계정 수명을 같은 설정 업무에 연결합니다.</summary>
public interface IDriverWorkParticipationClient
{
    const string Endpoint = "api/v1/driver/work-participation";
    DriverWorkParticipationIdentity Identity { get; }
    event Action? SessionChanged;
    Task<DriverWorkParticipationResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<DriverWorkParticipationResponse> UpdateAsync(DriverWorkParticipationUpdate request, CancellationToken cancellationToken = default);
}
