using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Contracts.Common.Versioning;

public sealed class OperatingSystemInteractionCatalogTests
{
    [Fact]
    public void ExistingOperatingSystemHandoffs_AreRegisteredWithStableContracts()
    {
        var interactions = OperatingSystemInteractionCatalog.GetAll();

        Assert.Collection(
            interactions,
            shipperToCargo =>
            {
                Assert.Equal(OperatingSystemInteractionIds.ShipperRequestToCargo, shipperToCargo.InteractionId);
                Assert.Equal(OperatingSystemIds.ShipperTransportManagement, shipperToCargo.SourceOperatingSystemId);
                Assert.Equal(OperatingSystemLifecycleStageIds.ShipperTransportHandoff, shipperToCargo.SourceLifecycleStageId);
                Assert.Equal(OperatingSystemIds.DomesticCargoTransport, shipperToCargo.TargetOperatingSystemId);
                Assert.Equal(OperatingSystemLifecycleStageIds.CargoRequest, shipperToCargo.TargetLifecycleStageId);
                Assert.Equal(OperatingSystemInteractionIds.CargoCompletionToShipperAcceptance, shipperToCargo.ReturnInteractionId);
            },
            cargoToShipper =>
            {
                Assert.Equal(OperatingSystemInteractionIds.CargoCompletionToShipperAcceptance, cargoToShipper.InteractionId);
                Assert.Equal(OperatingSystemInteractionModes.ReturnForAcceptance, cargoToShipper.Mode);
                Assert.Equal(OperatingSystemLifecycleStageIds.CargoEvidenceSettlement, cargoToShipper.SourceLifecycleStageId);
                Assert.Equal(OperatingSystemLifecycleStageIds.ShipperDeliveryAcceptance, cargoToShipper.TargetLifecycleStageId);
            },
            warehouseToCargo =>
            {
                Assert.Equal(OperatingSystemInteractionIds.WarehouseOutboundToCargo, warehouseToCargo.InteractionId);
                Assert.Equal(OperatingSystemInteractionLifecycleBindingStatuses.SourceLifecyclePending, warehouseToCargo.LifecycleBindingStatus);
                Assert.Null(warehouseToCargo.SourceLifecycleStageId);
                Assert.Equal(OperatingSystemLifecycleStageIds.CargoRequest, warehouseToCargo.TargetLifecycleStageId);
            },
            martToFood =>
            {
                Assert.Equal(OperatingSystemInteractionIds.MartLastMileToFoodDelivery, martToFood.InteractionId);
                Assert.Equal(OperatingSystemLifecycleStageIds.MartLastMileHandoff, martToFood.SourceLifecycleStageId);
                Assert.Equal(OperatingSystemLifecycleStageIds.FoodDispatch, martToFood.TargetLifecycleStageId);
            });
    }

    [Fact]
    public void EveryInteraction_UsesKnownOperatingSystemsAndUniqueContractCodes()
    {
        var interactions = OperatingSystemInteractionCatalog.GetAll();

        Assert.Equal(interactions.Count, interactions.Select(item => item.InteractionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(interactions.Count, interactions.Select(item => item.ContractCode).Distinct(StringComparer.Ordinal).Count());
        Assert.All(interactions, item =>
        {
            Assert.Contains(item.SourceOperatingSystemId, OperatingSystemIds.All);
            Assert.Contains(item.TargetOperatingSystemId, OperatingSystemIds.All);
        });
    }

    [Fact]
    public void LifecycleCoverage_ExposesDefinedAndPendingOperatingSystemsWithoutInventingStages()
    {
        var coverage = OperatingSystemInteractionCatalog.GetLifecycleCoverage();

        Assert.Equal(OperatingSystemIds.All.Count, coverage.Count);
        Assert.Equal(4, coverage.Count(item => item.HasLifecycle));
        Assert.All(coverage.Where(item => item.HasLifecycle), item => Assert.Equal(8, item.DefinedStageCount));
        Assert.Contains(coverage, item =>
            item.OperatingSystemId == OperatingSystemIds.WarehouseCommerceFulfillment
            && !item.HasLifecycle
            && item.DefinedStageCount == 0);
    }

    [Fact]
    public void CurrentStructure_ClassifiesOrderSegmentsOnlyForVerifiedLifecycles()
    {
        var structures = OperatingSystemInteractionCatalog.GetCurrentStructure();

        Assert.Equal(OperatingSystemIds.All.Count, structures.Count);
        Assert.Equal(
            OperatingSystemIds.All.OrderBy(x => x),
            structures.Select(x => x.OperatingSystemId).OrderBy(x => x));
        Assert.All(
            structures.Where(x => x.HasLifecycle),
            item =>
            {
                Assert.Equal(OperatingSystemCurrentStructureStatuses.VerifiedFromLifecycle, item.Status);
                Assert.NotEmpty(item.OrderSegments);
                Assert.True(item.DefinedStageCount > 0);
            });
        Assert.All(
            structures.Where(x => !x.HasLifecycle),
            item =>
            {
                Assert.Equal(OperatingSystemCurrentStructureStatuses.LifecyclePending, item.Status);
                Assert.Empty(item.OrderSegments);
                Assert.Equal(0, item.DefinedStageCount);
            });
    }

    [Fact]
    public void ShipperCargoRoundTrip_IsNavigableInBothDirections()
    {
        var outbound = OperatingSystemInteractionCatalog.GetBySourceOperatingSystem(
            OperatingSystemIds.ShipperTransportManagement);
        var inbound = OperatingSystemInteractionCatalog.GetByTargetOperatingSystem(
            OperatingSystemIds.ShipperTransportManagement);

        var requestHandoff = Assert.Single(outbound);
        var completionReturn = Assert.Single(inbound);
        Assert.Equal(completionReturn.InteractionId, requestHandoff.ReturnInteractionId);
        Assert.Equal(OperatingSystemInteractionModes.ResponsibilityTransfer, requestHandoff.Mode);
        Assert.Equal(OperatingSystemInteractionModes.ReturnForAcceptance, completionReturn.Mode);
    }
}
