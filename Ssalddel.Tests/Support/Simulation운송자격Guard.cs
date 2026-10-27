using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Services.Dispatch.Compliance;

namespace Ssalddel.Tests.Support;

/// <summary>기존 업무 규칙 시험의 명시적 가상 전제. 제품 DI 또는 운영 설정에 등록하지 않는다.</summary>
internal sealed class Simulation운송자격Guard : I신규화물업무자격Guard
{
    public bool RequiresCargoEvidence => false;
    public static Simulation운송자격Guard Instance { get; } = new();
    public Task<운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default)
        => Task.FromResult(new 운송자격평가결과(true, "Qualification.SimulationFixture",
            "명시적 가상 업무 시험", 운송자격Codes.Pending, IsSimulationBypass: true));
}
