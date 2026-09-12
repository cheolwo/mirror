using System.Text.Json;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

public sealed class OperationalWorldScenePlacementPlannerTests
{
    private static readonly DateTime Now = new(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 샘플운영사본은_만료항목을제외하고_OS별배치준비명령으로변환된다()
    {
        var interpreter = new OperationalWorldSceneInterpreter();
        var applyResult = interpreter.Apply(new OperationalWorldSceneResponse
        {
            AreaStableId = "region:kr:bjd:1126010100",
            Cursor = 31,
            AsOfUtc = Now,
            Items =
            [
                Item("food:done:1", OperationalWorldOperatingSystemIds.FoodDelivery,
                    OperationalWorldSceneItemKinds.CompletedLifecycle, Now.AddMinutes(5)),
                Item("warehouse:actor:1", OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
                    OperationalWorldSceneItemKinds.WarehouseActor, Now.AddMinutes(5)),
                Item("cargo:handoff:1", OperationalWorldOperatingSystemIds.DomesticCargoTransport,
                    OperationalWorldSceneItemKinds.CargoHandoff, Now.AddMinutes(5)),
                Item("expired:1", OperationalWorldOperatingSystemIds.FoodDelivery,
                    OperationalWorldSceneItemKinds.CompletedLifecycle, Now.AddSeconds(-1))
            ]
        }, Now);

        var result = new OperationalWorldScenePlacementPlanner().Create(applyResult);

        Assert.True(result.Accepted);
        Assert.Equal(3, result.Instructions.Length);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.Instructions, x => x.AnchorKey == "world.area.food-delivery");
        Assert.Contains(result.Instructions, x => x.AnchorKey == "world.area.warehouse");
        Assert.Contains(result.Instructions, x => x.AnchorKey == "world.area.cargo");
        Assert.DoesNotContain(result.Instructions, x => x.ObjectStableId == "expired:1");
    }

    [Fact]
    public void 배치준비명령은_표현JSON과개인정보를복사하지않는다()
    {
        var item = Item("food:done:private", OperationalWorldOperatingSystemIds.FoodDelivery,
            OperationalWorldSceneItemKinds.CompletedLifecycle, Now.AddMinutes(5));
        item.RepresentationDataJson = "{\"address\":\"서울 비공개 주소\",\"driverId\":\"driver-private\"}";
        var result = new OperationalWorldScenePlacementPlanner().Create(new OperationalWorldSceneApplyResult
        {
            Accepted = true,
            CurrentItems = [item]
        });

        var serialized = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("비공개 주소", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("driver-private", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("RepresentationDataJson", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void 영속저장을허용한사본과알수없는OS는_배치명령에서격리된다()
    {
        var persisted = Item("unsafe:1", OperationalWorldOperatingSystemIds.FoodDelivery,
            OperationalWorldSceneItemKinds.CompletedLifecycle, Now.AddMinutes(5));
        persisted.LocalStorageAllowed = true;
        var unknown = Item("unknown:1", "UnknownOS",
            OperationalWorldSceneItemKinds.CompletedLifecycle, Now.AddMinutes(5));

        var result = new OperationalWorldScenePlacementPlanner().Create(new OperationalWorldSceneApplyResult
        {
            Accepted = true,
            CurrentItems = [persisted, unknown]
        });

        Assert.Empty(result.Instructions);
        Assert.Contains(result.Diagnostics, x => x.Code == OperationalWorldPlacementDiagnosticCodes.LocalPersistenceForbidden);
        Assert.Contains(result.Diagnostics, x => x.Code == OperationalWorldPlacementDiagnosticCodes.OperatingSystemUnsupported);
    }

    private static OperationalWorldSceneItem Item(
        string id,
        string operatingSystemId,
        string itemKind,
        DateTime expiresAtUtc)
        => new()
        {
            SnapshotStableId = id,
            AreaStableId = "region:kr:bjd:1126010100",
            OperatingSystemId = operatingSystemId,
            ItemKind = itemKind,
            ActivityCode = "Completed",
            Revision = 1,
            OccurredAtUtc = Now.AddMinutes(-1),
            PublishedAtUtc = Now,
            ExpiresAtUtc = expiresAtUtc,
            DataPolicyCode = OperationalWorldScenePolicy.OnlineEphemeral,
            LocalStorageAllowed = false,
            ReplayAllowed = false
        };
}
