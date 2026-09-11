using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Interior.Contracts;
using Ssalddel.Interior.Domain;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Persistence;
using Ssalddel.Simulation.Hosting.Controllers;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationUnityCodeMetadataTests
{
    private static readonly string[] SimulationFeatureKeys =
    {
        SsalddelCodeFeatureKeys.SimulationSessionLifecycle,
        SsalddelCodeFeatureKeys.SimulationParallelBattle,
        SsalddelCodeFeatureKeys.SimulationFarmCombatInput,
        SsalddelCodeFeatureKeys.SimulationSaveReplay,
        SsalddelCodeFeatureKeys.SimulationWorldDerivation,
        SsalddelCodeFeatureKeys.SimulationSyntyLandscape,
        SsalddelCodeFeatureKeys.SimulationWorldStreaming,
    };

    [Fact]
    public void Simulation기준흐름은_중복과끊긴단계없이_탐색된다()
    {
        var metadata = ReadSimulationMetadata();
        var graph = SsalddelCodeMetadataGraphBuilder.Build(metadata);
        var diagnostics = SsalddelCodeMetadataValidator.Validate(
            graph,
            requireNavigationFields: true);

        Assert.DoesNotContain(diagnostics, item =>
            item.Severity == SsalddelCodeMetadataDiagnosticSeverity.Error);
        Assert.All(SimulationFeatureKeys, featureKey =>
            Assert.Contains(metadata, item => item.FeatureKey == featureKey));
        Assert.Contains(metadata, item =>
            item.ComponentType == typeof(평창군공간파생Pipeline)
            && item.ReadsFrom.HasFlag(SsalddelCodeDataScope.SharedPublicData)
            && item.WritesTo == SsalddelCodeDataScope.DerivedWorld);
    }

    [Fact]
    public void SimulationMetadata는_운영상태와공유공공데이터를_쓰지않는다()
    {
        var metadata = ReadSimulationMetadata();

        Assert.DoesNotContain(metadata, item =>
            item.WritesTo.HasFlag(SsalddelCodeDataScope.OperationalState));
        Assert.DoesNotContain(metadata, item =>
            item.WritesTo.HasFlag(SsalddelCodeDataScope.SharedPublicData));
        Assert.All(
            metadata.Where(item => item.WritesTo != SsalddelCodeDataScope.None),
            item => Assert.True(
                item.Effects.HasFlag(SsalddelCodeEffect.PersistentWrite)
                || item.Effects.HasFlag(SsalddelCodeEffect.StateMutation)
                || item.Effects.HasFlag(SsalddelCodeEffect.UiStateMutation)));
    }

    [Fact]
    public void MetadataValidator는_중복과순환단계를_오류로보고한다()
    {
        var first = Descriptor("test", "one", 20, "two");
        var second = Descriptor("test", "two", 10, "one");
        var duplicate = Descriptor("test", "one", 30);

        var diagnostics = SsalddelCodeMetadataValidator.Validate(
            SsalddelCodeMetadataGraphBuilder.Build(new[] { first, second, duplicate }),
            requireNavigationFields: true);

        Assert.Contains(diagnostics, item => item.Code == "CODEMAP004");
        Assert.Contains(diagnostics, item => item.Code == "CODEMAP008");
    }

    private static IReadOnlyList<SsalddelCodeMetadataDescriptor> ReadSimulationMetadata()
        => SsalddelCodeMetadataReader.Read(
                typeof(I실내공간조립Engine).Assembly,
                typeof(DeterministicInteriorLayoutEngine).Assembly,
                typeof(경영SimulationSession생성Request).Assembly,
                typeof(경영SimulationSessionAggregate).Assembly,
                typeof(경영SimulationSession생명주기Service).Assembly,
                typeof(InMemory경영SimulationSessionStore).Assembly,
                typeof(SimulationSessionSaveStore).Assembly,
                typeof(경영SimulationSessionsController).Assembly)
            .Where(item => SimulationFeatureKeys.Contains(item.FeatureKey, StringComparer.Ordinal))
            .ToArray();

    private static SsalddelCodeMetadataDescriptor Descriptor(
        string featureKey,
        string stepKey,
        int flowOrder,
        params string[] dependencies)
        => new(
            typeof(SimulationUnityCodeMetadataTests),
            featureKey,
            SsalddelCodeLayer.Application,
            "검증 fixture",
            SsalddelCodeEffect.None,
            null,
            flowOrder,
            "검증 전용")
        {
            StepKey = stepKey,
            DependsOnStepKeys = dependencies,
            ExecutionStage = SsalddelCodeExecutionStage.Query,
        };
}
