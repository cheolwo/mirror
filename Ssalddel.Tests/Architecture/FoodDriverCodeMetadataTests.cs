using System.Text.Json;
using Ssalddel.Application.Driver.Food;
using Ssalddel.CodeMap;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using 살뜰.Infrastructure.Persistence.Configurations.Driver;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Architecture;

public sealed class FoodDriverCodeMetadataTests
{
    [Fact]
    public void 음식기사흐름은_필수단계와계약부터DB및공통UI까지_끊김없이연결한다()
    {
        var metadata = ReadMetadata();
        var diagnostics = SsalddelCodeMetadataValidator.Validate(
            SsalddelCodeMetadataGraphBuilder.Build(metadata), requireNavigationFields: true);
        var feature = Assert.Single(ReadManifest().Features,
            item => item.Key == SsalddelCodeFeatureKeys.FoodDriverWork);

        Assert.NotEmpty(metadata);
        Assert.NotEmpty(feature.RequiredStepKeys);
        Assert.DoesNotContain(diagnostics,
            item => item.Severity == SsalddelCodeMetadataDiagnosticSeverity.Error);
        Assert.All(feature.RequiredStepKeys, key =>
            Assert.Single(metadata, item => item.StepKey == key));
        Assert.All(new[]
        {
            typeof(기사운행시작요청).Assembly,
            typeof(기사근무).Assembly,
            typeof(기사근무Configuration).Assembly,
            typeof(FoodDeliveryDriverWorkspaceUseCase).Assembly,
            typeof(FoodDriverWorkspacePresentation).Assembly,
        }, assembly => Assert.Contains(metadata, item => item.ComponentType.Assembly == assembly));
    }

    [Fact]
    public void 현재업무조회와_일일정산및완료상세는_서로다른단계다()
    {
        var metadata = ReadMetadata();
        var current = Step(metadata, "application.current-work");
        var settlement = Step(metadata, "application.daily-settlement");
        var completed = Step(metadata, "application.completed-detail");

        Assert.Equal(typeof(FoodDeliveryDriverWorkspaceUseCase), current.ComponentType);
        Assert.Equal(current.ComponentType, settlement.ComponentType);
        Assert.Equal(current.ComponentType, completed.ComponentType);
        Assert.Equal(SsalddelCodeExecutionStage.Query, current.ExecutionStage);
        Assert.Equal(SsalddelCodeExecutionStage.Query, settlement.ExecutionStage);
        Assert.Equal(SsalddelCodeExecutionStage.Query, completed.ExecutionStage);
        var currentDependencies = DependencyClosure(metadata, current.StepKey);
        Assert.DoesNotContain(settlement.StepKey, currentDependencies);
        Assert.DoesNotContain(completed.StepKey, currentDependencies);
        Assert.DoesNotContain(current.StepKey, DependencyClosure(metadata, settlement.StepKey));
        Assert.DoesNotContain(current.StepKey, DependencyClosure(metadata, completed.StepKey));
        Assert.Equal(SsalddelCodeDataScope.None, settlement.WritesTo);
        Assert.Equal(SsalddelCodeDataScope.None, completed.WritesTo);
    }

    [Theory]
    [InlineData("application.current-bundle-policy", "FoodDeliveryCurrentBundlePolicy")]
    [InlineData("application.current-work-projector", "FoodDeliveryCurrentWorkProjector")]
    public void 순수묶음판정과현재업무투영은_저장이나외부호출효과를갖지않는다(
        string stepKey, string typeName)
    {
        var step = Step(ReadMetadata(), stepKey);

        Assert.Equal(typeName, step.ComponentType.Name);
        Assert.Equal(SsalddelCodeEffect.None, step.Effects);
        Assert.Equal(SsalddelCodeDataScope.None, step.WritesTo);
        Assert.False(string.IsNullOrWhiteSpace(step.Boundary));
    }

    [Theory]
    [InlineData("ui.recovery-policy")]
    [InlineData("ui.stage-presentation")]
    [InlineData("ui.food-presentation")]
    [InlineData("ui.workspace-presentation")]
    public void UI의복구판정과표시변환은_HTTP및영속쓰기효과를갖지않는다(string stepKey)
    {
        var step = Step(ReadMetadata(), stepKey);

        Assert.Equal(SsalddelCodeEffect.None, step.Effects);
        Assert.Equal(SsalddelCodeDataScope.None, step.WritesTo);
    }

    [Fact]
    public void 공통역할셸은_구체음식기사Adapter대신_업무와복구포트를소비한다()
    {
        var shell = Step(ReadMetadata(), "ui.workspace-view-model");

        Assert.Contains("contract.workspace-port", shell.DependsOnStepKeys);
        Assert.Contains("contract.recovery-port", shell.DependsOnStepKeys);
        Assert.DoesNotContain("ui.food-driver-adapter", shell.DependsOnStepKeys);
    }

    [Fact]
    public void 표시판정의실제호출은_순수규칙부터카드변환순서로연결한다()
    {
        var metadata = ReadMetadata();
        var food = Step(metadata, "ui.food-presentation");
        var stage = Step(metadata, "ui.stage-presentation");
        var workspace = Step(metadata, "ui.workspace-presentation");
        var view = Step(metadata, "ui.role-map-view");

        Assert.Contains("ui.recovery-policy", food.DependsOnStepKeys);
        Assert.Contains(food.StepKey, stage.DependsOnStepKeys);
        Assert.Contains(stage.StepKey, workspace.DependsOnStepKeys);
        Assert.True(food.FlowOrder < stage.FlowOrder && stage.FlowOrder < workspace.FlowOrder);
        Assert.Contains("ui.workspace-view-model", view.DependsOnStepKeys);
        Assert.DoesNotContain(workspace.StepKey, view.DependsOnStepKeys);
    }

    [Theory]
    [InlineData("application.work-start")]
    [InlineData("application.work-stop")]
    [InlineData("application.work-location")]
    [InlineData("application.driver-state-persistence")]
    public void 실제운영상태쓰기단계는_영속효과와운영원장경계를명시한다(string stepKey)
    {
        var step = Step(ReadMetadata(), stepKey);

        Assert.True(step.Effects.HasFlag(SsalddelCodeEffect.PersistentWrite));
        Assert.True(step.WritesTo.HasFlag(SsalddelCodeDataScope.OperationalState));
        Assert.False(string.IsNullOrWhiteSpace(step.Boundary));
    }

    [Theory]
    [InlineData("infrastructure.shift-mapping")]
    [InlineData("infrastructure.work-start-followup-mapping")]
    public void DB매핑정의는_매핑구성자체를영속쓰기완료로표시하지않는다(string stepKey)
    {
        var step = Step(ReadMetadata(), stepKey);

        Assert.Equal(SsalddelCodeLayer.Infrastructure, step.Layer);
        Assert.Equal(SsalddelCodeEffect.None, step.Effects);
        Assert.Equal(SsalddelCodeDataScope.None, step.WritesTo);
    }

    [Fact]
    public void 음식기사단계의_중복키와없는의존단계는_검증에실패한다()
    {
        var metadata = ReadMetadata();
        var current = Step(metadata, "application.current-work");
        var broken = current with
        {
            StepKey = "test.broken-current-work",
            DependsOnStepKeys = ["application.missing-step"],
        };
        var graph = SsalddelCodeMetadataGraphBuilder.Build(metadata.Concat([current, broken]));
        var diagnostics = SsalddelCodeMetadataValidator.Validate(graph, requireNavigationFields: true);

        Assert.Contains(diagnostics, item => item.Code == "CODEMAP004"
            && item.StepKey == current.StepKey);
        Assert.Contains(diagnostics, item => item.Code == "CODEMAP005"
            && item.StepKey == broken.StepKey);
    }

    internal static IReadOnlyList<SsalddelCodeMetadataDescriptor> ReadMetadata()
        => SsalddelCodeMetadataReader.ReadFeature(
            SsalddelCodeFeatureKeys.FoodDriverWork,
            typeof(기사운행시작요청).Assembly,
            typeof(기사근무).Assembly,
            typeof(기사근무Configuration).Assembly,
            typeof(FoodDeliveryDriverWorkspaceUseCase).Assembly,
            typeof(FoodDriverWorkspacePresentation).Assembly);

    internal static WorkAreaManifest ReadManifest()
        => JsonSerializer.Deserialize<WorkAreaManifest>(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "eng", "work-areas", "food-driver.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("음식 기사 코드 지도 manifest를 읽지 못했습니다.");

    internal static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }

    private static SsalddelCodeMetadataDescriptor Step(
        IReadOnlyList<SsalddelCodeMetadataDescriptor> metadata, string key)
        => Assert.Single(metadata, item => item.StepKey == key);

    private static IReadOnlySet<string> DependencyClosure(
        IReadOnlyList<SsalddelCodeMetadataDescriptor> metadata, string key)
    {
        var byKey = metadata.ToDictionary(item => item.StepKey, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(byKey[key].DependsOnStepKeys);
        while (pending.TryPop(out var dependency))
        {
            if (!visited.Add(dependency)) continue;
            foreach (var next in byKey[dependency].DependsOnStepKeys) pending.Push(next);
        }

        return visited;
    }
}
