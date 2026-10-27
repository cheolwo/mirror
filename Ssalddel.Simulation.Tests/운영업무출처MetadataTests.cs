using System.Text.Json;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Presentation;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "운영 업무 출처의 읽기 호환·종류·참조와 상태 권위 분리를 검사한다.",
    Boundary = "정적 메타데이터·순수 규칙 시험; 운영 연결/Unity 화면/역사적 코드 계보 자동 증명 아님")]
public sealed class 운영업무출처MetadataTests
{
    private static SsalddelCodeMetadataDescriptor[] 실제단계()
        => SsalddelCodeMetadataReader.ReadFeature(SsalddelCodeFeatureKeys.FoodWorkflowLineage,
            typeof(경영SimulationSessionAggregate).Assembly, typeof(음식배달관찰상태).Assembly).ToArray();

    [Fact]
    public void 기존표기는_출처필드없이_호환된다()
    {
        var value = Assert.Single(SsalddelCodeMetadataReader.Read(typeof(기존표기)));
        Assert.Empty(value.SourceCodeRefs);
        Assert.Empty(value.SharedRuleRefs);
        Assert.Empty(value.ReuseKind);
        Assert.Empty(value.Adaptation);
        Assert.Empty(SsalddelCodeMetadataValidator.Validate(SsalddelCodeMetadataGraphBuilder.Build(new[] { value })));
    }

    [Fact]
    public void 출처는_정규화하고_조회사본으로_보존한다()
    {
        var value = Assert.Single(SsalddelCodeMetadataReader.Read(typeof(출처표기)));
        Assert.Equal(new[] { "a.cs", "b.cs" }, value.SourceCodeRefs);
        Assert.Equal(new[] { "rule.cs" }, value.SharedRuleRefs);
        Assert.Equal("SharedRuleCall", value.ReuseKind);
        Assert.Equal("경계", value.Adaptation);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new {
            value.SourceCodeRefs, value.ReuseKind, value.SharedRuleRefs, value.Adaptation }));
        Assert.Equal(2, json.RootElement.GetProperty("SourceCodeRefs").GetArrayLength());
        Assert.Equal("경계", json.RootElement.GetProperty("Adaptation").GetString());
    }

    [Theory]
    [InlineData("", "CODEMAP040")]
    [InlineData("Unknown", "CODEMAP040")]
    [InlineData("SharedRuleCall", "CODEMAP041")]
    public void 불완전한_출처는_관문에서_거부한다(string kind, string expected)
    {
        var value = Assert.Single(SsalddelCodeMetadataReader.Read(typeof(기존표기))) with {
            SourceCodeRefs = new[] { "a.cs" }, ReuseKind = kind, Adaptation = "경계" };
        Assert.Contains(SsalddelCodeMetadataValidator.Validate(SsalddelCodeMetadataGraphBuilder.Build(new[] { value })),
            issue => issue.Code == expected);
    }

    [Fact]
    public void 실제아홉단계는_다섯업무의_의미재구성_공유계산_표현소비를_구별한다()
    {
        var steps = 실제단계();
        Assert.Equal(9, steps.Length);
        var kinds = steps.ToDictionary(step => step.StepKey, step => step.ReuseKind, StringComparer.Ordinal);
        Assert.Equal("SemanticAdaptation", kinds["domain.food-order-adaptation"]);
        Assert.Equal("SemanticAdaptation", kinds["domain.restaurant-response-adaptation"]);
        Assert.Equal("SemanticAdaptation", kinds["domain.restaurant-cooking-adaptation"]);
        Assert.Equal("SharedRuleCall", kinds["domain.food-dispatch-shared-rule"]);
        Assert.Equal("SharedRuleCall", kinds["domain.freight-dispatch-shared-rule"]);
        Assert.Equal("SemanticAdaptation", kinds["domain.freight-transport-adaptation"]);
        Assert.Equal("SemanticAdaptation", kinds["domain.warehouse-put-away-adaptation"]);
        Assert.Equal("SharedRuleCall", kinds["domain.warehouse-outbound-shared-rule"]);
        Assert.Equal("ProjectionConsumption", kinds["unity.food-state-projection"]);
        Assert.Empty(SsalddelCodeMetadataValidator.Validate(SsalddelCodeMetadataGraphBuilder.Build(steps), true));
        Assert.All(steps, step => {
            Assert.False(step.ReadsFrom.HasFlag(SsalddelCodeDataScope.OperationalState));
            Assert.False(step.WritesTo.HasFlag(SsalddelCodeDataScope.OperationalState));
            Assert.False(step.Effects.HasFlag(SsalddelCodeEffect.NetworkCall));
            Assert.False(step.Effects.HasFlag(SsalddelCodeEffect.PersistentWrite));
            Assert.Empty(step.DependsOnStepKeys); // 출처를 런타임 호출 의존성으로 바꾸지 않는다.
        });
        Assert.Equal(SsalddelCodeDataScope.None,
            steps.Single(step => step.StepKey == "unity.food-state-projection").WritesTo);
        var root = 저장소();
        Assert.All(steps.SelectMany(x => x.SourceCodeRefs.Concat(x.SharedRuleRefs)), reference => {
            Assert.False(Path.IsPathRooted(reference));
            Assert.DoesNotContain("..", reference.Split('/'));
            Assert.True(File.Exists(Path.Combine(root, reference)), reference);
        });
    }

    [Fact]
    public void 공유규칙표기는_운영과Simulation_양쪽의_실제호출과_일치한다()
    {
        var root = 저장소();
        var steps = 실제단계().ToDictionary(step => step.StepKey, StringComparer.Ordinal);

        AssertSharedRuleStep(steps["domain.food-dispatch-shared-rule"]);
        AssertSourceContains(root, "Ssalddel/Services/Dispatch/Queue/음식배달후보평가Service.cs",
            "음식배달픽업평가Policy.판정(");
        AssertSourceContains(root, "Ssalddel/Services/Dispatch/Queue/음식배달후보순위Policy.cs",
            "음식배달픽업평가Policy.정렬(");
        AssertSourceContains(root, "Ssalddel.Simulation.Domain/UnityPackage/Runtime/가상배달대기.cs",
            "음식배달픽업평가Policy");

        AssertSharedRuleStep(steps["domain.freight-dispatch-shared-rule"]);
        AssertSourceContains(root, "Ssalddel/Services/Dispatch/Recommendation/배차추천평가Service.Scoring.cs",
            "화물배차추천점수Policy");
        AssertSourceContains(root, "Ssalddel/Services/Dispatch/Queue/기사대기Aging점수정책.cs",
            "화물배차기사대기점수Policy");
        AssertSourceContains(root, "Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFreightDispatch.cs",
            "화물배차후보선정Policy");

        AssertSharedRuleStep(steps["domain.warehouse-outbound-shared-rule"]);
        AssertSourceContains(root, "Ssalddel/Services/LogisticsProcessing/Warehouse/OutboundBatchEngine.cs",
            "창고출고배분Policy");
        AssertSourceContains(root, "Ssalddel.Simulation.Domain/UnityPackage/Runtime/가상동네보충.cs",
            "창고출고배분Policy");
    }

    [Fact]
    public void 공유규칙은_운영과가상의_다른시간입력에서도_동일조건이면_같은평가를낸다()
    {
        var now = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);
        var ops = Ssalddel.WorkflowRules.음식배달픽업평가Policy.판정(2, 3, now.AddMinutes(5), now, 30, 1, 2);
        var sim = Ssalddel.WorkflowRules.음식배달픽업평가Policy.예상소요시간판정(2, 3, now.AddMinutes(5), now, 4, 1, 2);
        Assert.Equal((ops.단계, ops.차이분, ops.추천점수, ops.설명), (sim.단계, sim.차이분, sim.추천점수, sim.설명));
    }

    private static string 저장소()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "eng/work-areas/simulation-unity.json"))) return dir.FullName;
        throw new InvalidOperationException("RepositoryRootNotFound");
    }

    private static void AssertSharedRuleStep(SsalddelCodeMetadataDescriptor step)
    {
        Assert.Equal("SharedRuleCall", step.ReuseKind);
        Assert.NotEmpty(step.SourceCodeRefs);
        Assert.NotEmpty(step.SharedRuleRefs);
    }

    private static void AssertSourceContains(string root, string relativePath, string marker)
    {
        var path = Path.Combine(root, relativePath);
        Assert.True(File.Exists(path), relativePath);
        Assert.Contains(marker, File.ReadAllText(path), StringComparison.Ordinal);
    }

    [SsalddelCodeMetadata("fixture", SsalddelCodeLayer.Domain, "기존 표기")]
    private sealed class 기존표기 { }

    [SsalddelCodeMetadata("fixture", SsalddelCodeLayer.Domain, "출처 표기",
        SourceCodeRefs = new[] { " b.cs ", "a.cs", "a.cs", " " },
        ReuseKind = " SharedRuleCall ", SharedRuleRefs = new[] { " rule.cs " }, Adaptation = " 경계 ")]
    private sealed class 출처표기 { }
}
