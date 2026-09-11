using System.Reflection;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E3,
    "WI 실행 머리와 E9→E1 책임 메타데이터의 회귀를 검증한다.",
    Boundary = "정적 메타데이터 검증이며 실제 E 승격 증거가 아니다.")]
public sealed class WorldInteractionDownwardModuleSkeletonTests
{
    [Fact]
    public void WI하향모듈은_E9부터E1까지_이름과메서드머리만노출한다()
    {
        var expected = new[]
        {
            ModuleHead("E9", 세계상호작용ModuleTechnicalNames.E9변화봉투,
                typeof(I세계상호작용E9변화봉투Module), "변화봉투Review"),
            ModuleHead("E8", 세계상호작용ModuleTechnicalNames.E8생활연속성,
                typeof(I세계상호작용E8생활연속성Module), "생활연속성Review"),
            ModuleHead("E7",
                세계상호작용ModuleTechnicalNames.E7플레이경험폐루프,
                typeof(I세계상호작용E7플레이경험폐루프Module),
                "플레이경험폐루프Review"),
            ModuleHead("E6", 세계상호작용ModuleTechnicalNames.E6세계정제,
                typeof(I세계상호작용E6세계정제Module), "세계정제Review"),
            ModuleHead("E5", 세계상호작용ModuleTechnicalNames.E5세계발현,
                typeof(I세계상호작용E5세계발현Module),
                "세계발현Review"),
            ModuleHead("E4",
                세계상호작용ModuleTechnicalNames.E4실행문맥결속,
                typeof(I세계상호작용E4실행문맥결속Module),
                "실행문맥결속Review"),
            ModuleHead("E3", 세계상호작용ModuleTechnicalNames.E3회귀증거,
                typeof(I세계상호작용E3회귀증거Module), "회귀증거Review"),
            ModuleHead("E2", 세계상호작용ModuleTechnicalNames.E2실행경계,
                typeof(I세계상호작용E2실행경계Module), "실행경계Review"),
            ModuleHead("E1", 세계상호작용ModuleTechnicalNames.E1핵심계약,
                typeof(I세계상호작용E1핵심계약Module), "핵심계약Review"),
        };

        Assert.Equal(9, expected.Length);
        foreach (var head in expected)
        {
            Assert.Matches("^E[1-9]$", head.EvidenceStage);
            Assert.EndsWith(head.ModuleTechnicalName, head.InterfaceType.Name);
            Assert.True(head.InterfaceType.IsInterface);
            Assert.Contains(typeof(I세계상호작용E단계Module),
                head.InterfaceType.GetInterfaces());

            var method = head.InterfaceType.GetMethod(head.MethodName,
                BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            Assert.NotNull(method);
            Assert.Equal(typeof(세계상호작용E단계ModuleOutline), method!.ReturnType);
            Assert.Equal(new[] { typeof(세계상호작용E단계ReviewContext) },
                method.GetParameters().Select(parameter => parameter.ParameterType));
        }

        Assert.False(typeof(I세계상호작용E단계ModuleSet)
            .IsAssignableFrom(typeof(LocalSimulationRuntime)));
    }

    [Fact]
    public void WI실행머리대장은_권위연산과_E9부터E1모듈을_함께가리킨다()
    {
        var expectedWiIds = new[]
        {
            "WI-FARM-01", "WI-FARM-02", "WI-FARM-03",
            "WI-FARM-04", "WI-FARM-05", "WI-FARM-06",
            "WI-NATURE-01", "WI-NATURE-02", "WI-NATURE-03",
            "WI-NATURE-04", "WI-NATURE-05", "WI-NATURE-06",
            "WI-NATURE-19", "WI-NATURE-20",
            "WI-NATURE-18", "WI-NATURE-07", "WI-NATURE-08", "WI-NATURE-09",
            "WI-NATURE-10", "WI-NATURE-11", "WI-NATURE-12",
            "WI-NATURE-13", "WI-NATURE-14", "WI-NATURE-15",
            "WI-NATURE-16", "WI-NATURE-17", "WI-CON-01",
        };
        var expectedModuleNames = new[]
        {
            세계상호작용ModuleTechnicalNames.E9변화봉투,
            세계상호작용ModuleTechnicalNames.E8생활연속성,
            세계상호작용ModuleTechnicalNames.E7플레이경험폐루프,
            세계상호작용ModuleTechnicalNames.E6세계정제,
            세계상호작용ModuleTechnicalNames.E5세계발현,
            세계상호작용ModuleTechnicalNames.E4실행문맥결속,
            세계상호작용ModuleTechnicalNames.E3회귀증거,
            세계상호작용ModuleTechnicalNames.E2실행경계,
            세계상호작용ModuleTechnicalNames.E1핵심계약,
        };
        var runtimeMethodNames = typeof(ISimulationFarmWorldInteractionRuntime)
            .GetMethods()
            .Concat(typeof(ISimulationNatureWorldInteractionRuntime).GetMethods())
            .Concat(typeof(ISimulationNatureSurvivalRuntime).GetMethods())
            .Concat(typeof(ISimulationSessionRuntime).GetMethods())
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expectedWiIds,
            세계상호작용ExecutionHeadCatalog.All.Select(head =>
                head.WorldInteractionId));
        Assert.All(세계상호작용ExecutionHeadCatalog.All, head =>
        {
            Assert.Contains(head.PreviewMethodName, runtimeMethodNames);
            Assert.Contains(head.ConfirmMethodName, runtimeMethodNames);
            if (head.WorldInteractionId == SimulationNatureSurvivalCodes
                .PrepareFieldSupplyDelegatedWorldInteractionId)
            {
                Assert.StartsWith("Get", head.PreviewMethodName);
                Assert.StartsWith("Advance", head.ConfirmMethodName);
            }
            else
            {
                Assert.StartsWith("Preview", head.PreviewMethodName);
                Assert.StartsWith("Confirm", head.ConfirmMethodName);
            }
            Assert.Equal(expectedModuleNames, head.DownwardModuleTechnicalNames);
        });
    }

    [Fact]
    public void E책임Reader는_대표보조타입과_WI메서드를_분리해서읽는다()
    {
        var sessionResponsibilities = SsalddelEvidenceResponsibilityReader
            .Read(typeof(ISimulationSessionRuntime));
        Assert.Single(sessionResponsibilities, item =>
            item.Role == SsalddelEvidenceResponsibilityRole.Primary
            && item.EvidenceStage == SsalddelEvidenceStage.E2);
        Assert.Single(sessionResponsibilities, item =>
            item.Role == SsalddelEvidenceResponsibilityRole.Secondary
            && item.EvidenceStage == SsalddelEvidenceStage.E3);

        var farmResponsibilities = SsalddelEvidenceResponsibilityReader
            .Read(typeof(ISimulationFarmWorldInteractionRuntime));
        var farmWorkMethods = farmResponsibilities.Where(item =>
            item.ComponentMethod?.Name is "PreviewFarmWorkAsync"
                or "ConfirmFarmWorkAsync").ToArray();
        Assert.Equal(2, farmWorkMethods.Length);
        Assert.All(farmWorkMethods, item =>
        {
            Assert.Equal(SsalddelEvidenceStage.E2, item.EvidenceStage);
            Assert.Equal(new[] { "WI-FARM-01", "WI-FARM-02", "WI-FARM-03",
                "WI-FARM-04", "WI-FARM-05", "WI-FARM-06" },
                item.WorldInteractionIds);
        });

        var natureResponsibilities = SsalddelEvidenceResponsibilityReader
            .Read(typeof(ISimulationNatureWorldInteractionRuntime));
        Assert.Equal(8, natureResponsibilities.Count(item =>
            item.ComponentMethod is not null));
        Assert.All(natureResponsibilities.Where(item =>
            item.ComponentMethod is not null), item =>
            Assert.Equal(SsalddelEvidenceStage.E2, item.EvidenceStage));
    }

    [Fact]
    public void E책임검증기는_현재Application후보의_무사유누락을허용하지않는다()
    {
        var diagnostics = SsalddelEvidenceCoverageValidator.Validate(
            requireCoverage: true,
            typeof(LocalSimulationRuntime).Assembly);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void E책임Attribute는_Unspecified와빈책임을거부한다()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SsalddelEvidenceResponsibilityAttribute(
                SsalddelEvidenceStage.Unspecified, "책임"));
        Assert.Throws<ArgumentException>(() =>
            new SsalddelEvidenceResponsibilityAttribute(
                SsalddelEvidenceStage.E1, " "));
        Assert.Throws<ArgumentException>(() =>
            new SsalddelEvidenceCoverageExclusionAttribute(
                SsalddelEvidenceCoverageExclusionCategory.TechnicalHelper,
                " "));
    }

    [Theory]
    [InlineData(typeof(ISimulationHexagramCampaignAttemptStore), SsalddelEvidenceStage.E1, SsalddelEvidenceSubmoduleKeys.E1저장재생계약)]
    [InlineData(typeof(InMemorySimulationHexagramCampaignAttemptStore), SsalddelEvidenceStage.E2, SsalddelEvidenceSubmoduleKeys.E2세션실행)]
    [InlineData(typeof(SimulationHexagramCampaignService), SsalddelEvidenceStage.E2, SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행)]
    [InlineData(typeof(ISimulationHexagramCampaignRuntime), SsalddelEvidenceStage.E2, SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행)]
    [InlineData(typeof(관찰세계진행Policy), SsalddelEvidenceStage.E1, SsalddelEvidenceSubmoduleKeys.E1세션권위계약)]
    [InlineData(typeof(ISimulationFoodOrderRuntime), SsalddelEvidenceStage.E2, SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행)]
    [InlineData(typeof(ISimulationNpcPolicyRuntime), SsalddelEvidenceStage.E2, SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행)]
    [InlineData(typeof(Ssalddel.Simulation.Hosting.Controllers.SimulationHexagramCampaignController), SsalddelEvidenceStage.E2, SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
    [InlineData(typeof(Ssalddel.Unity.Warehouse.창고정책카드Presenter), SsalddelEvidenceStage.E2, SsalddelEvidenceSubmoduleKeys.E2Unity권위Client)]
    public void 관찰운영과이야기경계는_정확한E책임과_미검증범위를_명시한다(Type type,
        SsalddelEvidenceStage expectedStage, string expectedSubmodule)
    {
        var responsibility = Assert.Single(SsalddelEvidenceResponsibilityReader.Read(type),
            item => item.ComponentMethod == null && item.Role == SsalddelEvidenceResponsibilityRole.Primary);
        Assert.Equal(expectedStage, responsibility.EvidenceStage);
        Assert.Equal(expectedSubmodule, responsibility.SubmoduleKey);
        Assert.False(string.IsNullOrWhiteSpace(responsibility.Boundary));
        Assert.Empty(type.GetCustomAttributes<SsalddelEvidenceCoverageExclusionAttribute>());
    }

    private static (string EvidenceStage, string ModuleTechnicalName,
        Type InterfaceType, string MethodName) ModuleHead(
        string evidenceStage,
        string moduleTechnicalName,
        Type interfaceType,
        string methodName)
        => (evidenceStage, moduleTechnicalName, interfaceType, methodName);
}
