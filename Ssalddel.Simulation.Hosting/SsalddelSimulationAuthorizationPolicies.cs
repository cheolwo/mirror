namespace Ssalddel.Simulation.Hosting;

/// <summary>
/// Simulation은 별도 인증 체계를 만들지 않고 살뜰 서버의 로그인 주체를 사용한다.
/// </summary>
public static class SsalddelSimulationAuthorizationPolicies
{
    public const string Participant = "SimulationParticipant";
    internal const string TestingScheme = "SimulationTesting";

    // 기존 Controller와 Hub attribute의 source 호환 별칭이다.
    public const string OnlineWorld = Participant;
}
