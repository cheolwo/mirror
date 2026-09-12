using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "음식배달 상태 사본과 Unity 표시 단계의 계약 호환을 검증한다.",
    Boundary = "표현 모델 자동 시험이며 실제 Scene·Game View 증거가 아니다.")]
public sealed class 음식배달수명주기표현Tests
{
    [Fact]
    public void Simulation사본과Unity표현은_같은상태코드를소비한다()
    {
        var source = new Simulation음식배달Snapshot
        {
            FoodOrderStableId = "sim-food-1", Revision = 4, StateCode = "픽업완료",
            RestaurantFacilityStableId = "restaurant-1", OrdererStableId = "orderer-1"
        };
        var snapshot = Simulation음식배달수명주기Adapter.ToLifecycleSnapshot(source, "simulation.r1");
        var presentation = new 음식배달수명주기표현(snapshot);

        Assert.Equal(음식배달상태원천코드.SimulationCore, snapshot.SourceCode);
        Assert.Equal(4, presentation.StepIndex);
        Assert.Contains("전달지", presentation.Label);
    }
}
