using System.Text.Json;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "가상 음식배달 생활의 업무 순서와 저장 복원을 검증한다.",
    Boundary = "Simulation 자동 시험이며 실제 배차나 Unity 화면 증거가 아니다.")]
public sealed class 오행음식배달생활관찰Tests
{
    [Fact]
    public void 한기사의_첫주문은_배차부터수령복귀까지_기존업무순서와저장을보존한다()
    {
        var aggregate = new 경영SimulationSessionAggregate(
            가상배달관찰표본.Create(Guid.Parse("0c1c4fb5-4b1f-48af-88fa-a1528af632ef")));
        string observedOrderStableId = string.Empty;
        경영SimulationSessionSnapshot state = aggregate.Snapshot();

        for (var tick = 0; tick < 160; tick++)
        {
            state = aggregate.Advance(new 경영SimulationTick진행Request
            {
                CommandId = "five-element-observer:tick:" + tick,
                ExpectedRevision = state.Revision,
                TickCount = 1,
            });

            var courier = Assert.IsType<가상배달기사Snapshot>(state.SyntheticCourier);
            if (!string.IsNullOrWhiteSpace(courier.OrderStableId))
            {
                observedOrderStableId = observedOrderStableId.Length == 0
                    ? courier.OrderStableId
                    : observedOrderStableId;
                Assert.Equal(observedOrderStableId, courier.OrderStableId);
                Assert.Single(state.FoodDeliveries,
                    order => order.FoodOrderStableId == courier.OrderStableId);
            }

            if (observedOrderStableId.Length > 0
                && string.Equals(courier.Stage, "Idle", StringComparison.Ordinal)
                && state.FoodDeliveries.Single(order =>
                    order.FoodOrderStableId == observedOrderStableId).ReceivedTick.HasValue)
            {
                break;
            }
        }

        Assert.NotEmpty(observedOrderStableId);
        var finalCourier = Assert.IsType<가상배달기사Snapshot>(state.SyntheticCourier);
        var completed = Assert.Single(state.FoodDeliveries,
            order => order.FoodOrderStableId == observedOrderStableId);
        Assert.Equal("Idle", finalCourier.Stage);
        Assert.Empty(finalCourier.OrderStableId);
        Assert.False(finalCourier.Carrying);
        Assert.NotNull(completed.ReadyForPickupTick);
        Assert.NotNull(completed.DispatchCandidateTick);
        Assert.NotNull(completed.PickedUpTick);
        Assert.NotNull(completed.DeliveredTick);
        Assert.NotNull(completed.ReceivedTick);
        Assert.True(completed.ReadyForPickupTick <= completed.DispatchCandidateTick);
        Assert.True(completed.DispatchCandidateTick <= completed.PickedUpTick);
        Assert.True(completed.PickedUpTick <= completed.DeliveredTick);
        Assert.True(completed.DeliveredTick <= completed.ReceivedTick);

        AssertOrderedSubsequence(completed.StateHistory.Select(value => value.ToStateCode),
            음식배달상태코드.조리중,
            음식배달상태코드.픽업대기,
            음식배달상태코드.기사배정,
            음식배달상태코드.픽업완료,
            음식배달상태코드.전달완료,
            음식배달상태코드.수령확인);

        var save = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:five-element-food-delivery-observer",
            ExpectedRevision = state.Revision,
        });
        var records = Assert.IsType<Simulation행위기록LedgerSnapshot>(
                save.ActionManifestationLedger)
            .TailRecords
            .Where(record => record.TargetStableIds.Contains(observedOrderStableId,
                StringComparer.Ordinal))
            .ToArray();
        AssertOrderedSubsequence(records.Select(record => record.WorldInteractionId),
            "WI-CITY-SYNTHETIC-ASSIGN",
            "WI-CITY-SYNTHETIC-MOVE",
            "WI-CITY-SYNTHETIC-PICKUP",
            "WI-CITY-SYNTHETIC-DELIVER",
            "WI-CITY-SYNTHETIC-RECEIVE",
            "WI-CITY-SYNTHETIC-RETURN");

        var restored = SimulationSessionReplay.Restore(save);
        Assert.Equal(Json(state), Json(restored.Snapshot()));
        var restoredSave = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = save.SaveStableId,
            ExpectedRevision = restored.Snapshot().Revision,
        });
        Assert.Equal(save.ReplayHash, restoredSave.ReplayHash);

        var nextOrder = state.FoodDeliveries.Single(order =>
            order.FoodOrderStableId != observedOrderStableId);
        Assert.Null(nextOrder.DispatchCandidateTick);
        Assert.Null(nextOrder.PickedUpTick);
        Assert.Null(nextOrder.DeliveredTick);
        Assert.Null(nextOrder.ReceivedTick);
    }

    private static void AssertOrderedSubsequence(IEnumerable<string> actual,
        params string[] expected)
    {
        var index = 0;
        foreach (var value in actual)
        {
            if (index < expected.Length
                && string.Equals(value, expected[index], StringComparison.Ordinal))
            {
                index++;
            }
        }
        Assert.Equal(expected.Length, index);
    }

    private static string Json(object value)
        => JsonSerializer.Serialize(value);
}
