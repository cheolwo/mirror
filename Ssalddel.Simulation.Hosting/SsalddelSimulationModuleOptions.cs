namespace Ssalddel.Simulation.Hosting;

public sealed class SsalddelSimulationModuleOptions
{
    public const string SectionName = "SsalddelSimulation";

    /// <summary>
    /// 기존 HTTP 계약 시험을 위한 우회다. Testing 환경에서만 적용된다.
    /// </summary>
    public bool AllowUnauthenticatedTesting { get; set; }
}
