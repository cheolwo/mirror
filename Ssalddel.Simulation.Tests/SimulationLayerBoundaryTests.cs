using System.Reflection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Persistence;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationLayerBoundaryTests
{
    [Fact]
    public void Domain은_Application과_Infrastructure를_참조하지_않는다()
    {
        var references = ReferenceNames(typeof(SimulationSessionReplay).Assembly);

        Assert.DoesNotContain("Ssalddel.Simulation.Application", references);
        Assert.DoesNotContain("Ssalddel.Simulation.Infrastructure", references);
        Assert.DoesNotContain("Ssalddel.Simulation.Hosting", references);
    }

    [Fact]
    public void Application은_Domain을_참조하되_Infrastructure와_Server를_참조하지_않는다()
    {
        var references = ReferenceNames(typeof(경영SimulationSessionService).Assembly);

        Assert.Contains("Ssalddel.Simulation.Domain", references);
        Assert.DoesNotContain("Ssalddel.Simulation.Infrastructure", references);
        Assert.DoesNotContain("Ssalddel.Simulation.Hosting", references);
    }

    [Fact]
    public void Infrastructure는_Application의_저장계약을_구현한다()
    {
        Assert.IsAssignableFrom<I경영SimulationSessionStore>(
            new InMemory경영SimulationSessionStore());
        Assert.IsAssignableFrom<ISimulationSessionSaveStore>(
            new InMemorySimulationSessionSaveStore());
    }

    [Fact]
    public void Persistence는_공공데이터조회Port를_구현하고_Server를_참조하지_않는다()
    {
        var references = ReferenceNames(typeof(Simulation공유공공데이터Reader).Assembly);

        Assert.Contains("Ssalddel.Simulation.Application", references);
        Assert.Contains("Ssalddel.Infrastructure", references);
        Assert.DoesNotContain("Ssalddel.Simulation.Hosting", references);
        Assert.True(typeof(ISimulation공유공공데이터조회Port)
            .IsAssignableFrom(typeof(Simulation공유공공데이터Reader)));
    }

    [Fact]
    public void Persistence는_파생원장Store를_구현하고_Server를_참조하지_않는다()
    {
        var references = ReferenceNames(typeof(SimulationWorld파생원장Store).Assembly);

        Assert.Contains("Ssalddel.Simulation.Application", references);
        Assert.Contains("Ssalddel.Simulation.Domain", references);
        Assert.DoesNotContain("Ssalddel.Simulation.Hosting", references);
        Assert.True(typeof(ISimulationWorld파생원장Store)
            .IsAssignableFrom(typeof(SimulationWorld파생원장Store)));
    }

    private static IReadOnlySet<string> ReferenceNames(Assembly assembly)
        => assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
}
