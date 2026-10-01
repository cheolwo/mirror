using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private readonly Dictionary<string, SimulationWorldBuildingInteriorSnapshot>
            worldInventoryBuildings = new Dictionary<string, SimulationWorldBuildingInteriorSnapshot>(
                StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationWorldContainerSnapshot>
            worldInventoryContainers = new Dictionary<string, SimulationWorldContainerSnapshot>(
                StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationWorldItemStackSnapshot>
            worldInventoryItemStacks = new Dictionary<string, SimulationWorldItemStackSnapshot>(
                StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationWorldPlayerInventorySnapshot>
            worldInventoryPlayers = new Dictionary<string, SimulationWorldPlayerInventorySnapshot>(
                StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationWorldPlayerItemSnapshot>
            worldInventoryPlayerItems = new Dictionary<string, SimulationWorldPlayerItemSnapshot>(
                StringComparer.Ordinal);
        private readonly List<SimulationWorldItemTransferSnapshot> worldInventoryTransfers =
            new List<SimulationWorldItemTransferSnapshot>();
        private readonly Dictionary<string, AppliedWorldItemAcquisitionCommand>
            appliedWorldItemAcquisitionCommands =
                new Dictionary<string, AppliedWorldItemAcquisitionCommand>(StringComparer.Ordinal);
        private string worldInventoryRuleRevision = string.Empty;
        private string worldInventoryInitialPayloadKey = "none";
        private SimulationWorldInventoryInitialStateRequest? worldInventoryCreationState;

        public SimulationWorldInventorySnapshot GetWorldInventory()
        {
            lock (gate)
            {
                return CreateWorldInventorySnapshot();
            }
        }

        public SimulationWorldItemAcquisitionPreviewSnapshot PreviewWorldItemAcquisition(
            SimulationWorldItemAcquisitionPreviewRequest request)
        {
            ValidateWorldItemAcquisitionPreviewRequest(request);
            lock (gate)
            {
                return CreateWorldItemAcquisitionPreview(request);
            }
        }

        public SimulationWorldItemAcquisitionResultSnapshot ConfirmWorldItemAcquisition(
            SimulationWorldItemAcquisitionConfirmRequest request)
        {
            ValidateWorldItemAcquisitionConfirmRequest(request);
            lock (gate)
            {
                var payloadKey = BuildWorldItemAcquisitionPayloadKey(request);
                if (appliedWorldItemAcquisitionCommands.TryGetValue(
                    request.CommandId.Trim(), out var applied))
                {
                    if (!string.Equals(applied.PayloadKey, payloadKey, StringComparison.Ordinal))
                        throw new SimulationConflictException(
                            SimulationWorldSurvivalInventoryCodes.CommandPayloadConflict);
                    return CloneWorldItemAcquisitionResult(applied.Result);
                }

                if (HasDifferentKindCommand(request.CommandId.Trim()))
                    throw new SimulationConflictException("SimulationCommandKindConflict");
                if (request.ExpectedRevision != Revision)
                    throw new SimulationConflictException(
                        SimulationWorldSurvivalInventoryCodes.ExpectedRevisionMismatch);

                var preview = CreateWorldItemAcquisitionPreview(
                    new SimulationWorldItemAcquisitionPreviewRequest
                    {
                        ObservedWorldRevision = request.ExpectedRevision,
                        PlayerStableId = request.PlayerStableId,
                        BuildingStableId = request.BuildingStableId,
                        ContainerStableId = request.ContainerStableId,
                        ItemStackStableId = request.ItemStackStableId,
                        Quantity = request.Quantity,
                    });
                if (!preview.CanConfirm)
                    throw new SimulationConflictException(
                        preview.BlockReasonCodes.FirstOrDefault()
                        ?? SimulationWorldSurvivalInventoryCodes.AcquisitionBlocked);

                var itemStack = worldInventoryItemStacks[request.ItemStackStableId.Trim()];
                itemStack.Quantity -= request.Quantity;
                var playerItemKey = PlayerItemKey(
                    request.PlayerStableId, itemStack.ItemCode, itemStack.UnitCode);
                if (!worldInventoryPlayerItems.TryGetValue(playerItemKey, out var playerItem))
                {
                    playerItem = new SimulationWorldPlayerItemSnapshot
                    {
                        ItemCode = itemStack.ItemCode,
                        KoreanName = itemStack.KoreanName,
                        UnitCode = itemStack.UnitCode,
                    };
                    worldInventoryPlayerItems.Add(playerItemKey, playerItem);
                }
                playerItem.Quantity += request.Quantity;

                Revision++;
                var transfer = new SimulationWorldItemTransferSnapshot
                {
                    TransferStableId = "world-item-transfer:" + request.CommandId.Trim(),
                    CommandId = request.CommandId.Trim(),
                    PlayerStableId = request.PlayerStableId.Trim(),
                    BuildingStableId = request.BuildingStableId.Trim(),
                    SourceContainerStableId = request.ContainerStableId.Trim(),
                    SourceItemStackStableId = request.ItemStackStableId.Trim(),
                    ItemCode = itemStack.ItemCode,
                    Quantity = request.Quantity,
                    UnitCode = itemStack.UnitCode,
                    AppliedWorldTick = CurrentTick,
                    AppliedWorldRevision = Revision,
                    EvidenceKindCode = SimulationWorldSurvivalInventoryCodes.SimulationScenario,
                    SimulationOnly = true,
                };
                worldInventoryTransfers.Add(transfer);

                var result = new SimulationWorldItemAcquisitionResultSnapshot
                {
                    CommandId = request.CommandId.Trim(),
                    AppliedWorldRevision = Revision,
                    AppliedWorldTick = CurrentTick,
                    Transfer = CloneWorldItemTransfer(transfer),
                    Inventory = CreateWorldInventorySnapshot(),
                    SimulationOnly = true,
                    IsOperationalState = false,
                };
                appliedWorldItemAcquisitionCommands.Add(
                    request.CommandId.Trim(),
                    new AppliedWorldItemAcquisitionCommand(
                        payloadKey,
                        CloneWorldItemAcquisitionResult(result)));
                AppendWorldItemAcquisitionCommand(request);
                return result;
            }
        }

        private void InitializeWorldInventory(SimulationWorldInventoryInitialStateRequest? request)
        {
            ValidateWorldInventoryInitialState(request);
            worldInventoryInitialPayloadKey = BuildWorldInventoryPayloadKey(request);
            worldInventoryCreationState = CloneWorldInventoryInitialState(request);
            if (request == null) return;

            worldInventoryRuleRevision = request.RuleRevision.Trim();
            foreach (var building in request.Buildings)
            {
                worldInventoryBuildings.Add(building.BuildingStableId.Trim(),
                    new SimulationWorldBuildingInteriorSnapshot
                    {
                        BuildingStableId = building.BuildingStableId.Trim(),
                        TileKey = building.TileKey.Trim(),
                        RegionStableId = building.RegionStableId.Trim(),
                        BuildingEvidenceKindCode = building.BuildingEvidenceKindCode.Trim(),
                        SourceRecordStableId = building.SourceRecordStableId.Trim(),
                        InteriorSpaceStableId = building.InteriorSpaceStableId.Trim(),
                        InteriorEvidenceKindCode = building.InteriorEvidenceKindCode.Trim(),
                    });
            }

            foreach (var container in request.Containers)
            {
                worldInventoryContainers.Add(container.ContainerStableId.Trim(),
                    new SimulationWorldContainerSnapshot
                    {
                        ContainerStableId = container.ContainerStableId.Trim(),
                        BuildingStableId = container.BuildingStableId.Trim(),
                        InteriorSpaceStableId = container.InteriorSpaceStableId.Trim(),
                        AccessPolicyCode = container.AccessPolicyCode.Trim(),
                        CapacityUnits = container.CapacityUnits,
                        ManagerPlayerStableIds = container.ManagerPlayerStableIds
                            .Select(value => value.Trim()).OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray(),
                        EvidenceKindCode = container.EvidenceKindCode.Trim(),
                    });
            }

            foreach (var player in request.Players)
            {
                worldInventoryPlayers.Add(player.PlayerStableId.Trim(),
                    new SimulationWorldPlayerInventorySnapshot
                    {
                        PlayerStableId = player.PlayerStableId.Trim(),
                        CurrentBuildingStableId = player.CurrentBuildingStableId.Trim(),
                        InventoryCapacityUnits = player.InventoryCapacityUnits,
                        ManagedContainerStableIds = player.ManagedContainerStableIds
                            .Select(value => value.Trim()).OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray(),
                    });
            }

            foreach (var itemStack in request.ItemStacks)
            {
                worldInventoryItemStacks.Add(itemStack.ItemStackStableId.Trim(),
                    new SimulationWorldItemStackSnapshot
                    {
                        ItemStackStableId = itemStack.ItemStackStableId.Trim(),
                        ContainerStableId = itemStack.ContainerStableId.Trim(),
                        ItemCode = itemStack.ItemCode.Trim(),
                        KoreanName = itemStack.KoreanName.Trim(),
                        Quantity = itemStack.Quantity,
                        UnitCode = itemStack.UnitCode.Trim(),
                        BuildingItemRelationStableId =
                            itemStack.BuildingItemRelationStableId.Trim(),
                        EvidenceKindCode = itemStack.EvidenceKindCode.Trim(),
                    });
            }
        }

        internal static void ValidateWorldInventoryInitialState(
            SimulationWorldInventoryInitialStateRequest? request)
        {
            if (request == null) return;
            RequireStableId(request.RuleRevision, "SimulationWorldInventoryRuleRevisionInvalid");
            if (request.IsOperationalInventory)
                throw new SimulationContractException(
                    SimulationWorldSurvivalInventoryCodes.OperationalInventoryForbidden);
            if (request.Buildings == null || request.Players == null
                || request.Containers == null || request.ItemStacks == null)
                throw new SimulationContractException("SimulationWorldInventoryInitialStateInvalid");

            EnsureUnique(request.Buildings.Select(value => value.BuildingStableId),
                "SimulationWorldInventoryBuildingDuplicate");
            EnsureUnique(request.Players.Select(value => value.PlayerStableId),
                "SimulationWorldInventoryPlayerDuplicate");
            EnsureUnique(request.Containers.Select(value => value.ContainerStableId),
                "SimulationWorldInventoryContainerDuplicate");
            EnsureUnique(request.ItemStacks.Select(value => value.ItemStackStableId),
                "SimulationWorldInventoryItemStackDuplicate");

            var buildings = request.Buildings.ToDictionary(
                value => RequiredTrim(value.BuildingStableId,
                    "SimulationWorldInventoryBuildingStableIdInvalid"),
                StringComparer.Ordinal);
            var players = request.Players.ToDictionary(
                value => RequiredTrim(value.PlayerStableId,
                    "SimulationWorldInventoryPlayerStableIdInvalid"),
                StringComparer.Ordinal);
            var containers = request.Containers.ToDictionary(
                value => RequiredTrim(value.ContainerStableId,
                    "SimulationWorldInventoryContainerStableIdInvalid"),
                StringComparer.Ordinal);

            foreach (var building in request.Buildings)
            {
                RequireStableId(building.InteriorSpaceStableId,
                    "SimulationWorldInventoryInteriorSpaceStableIdInvalid");
                RequiredTrim(building.TileKey, "SimulationWorldInventoryTileKeyInvalid");
                RequireStableId(building.RegionStableId,
                    "SimulationWorldInventoryRegionStableIdInvalid");
                RequiredTrim(building.BuildingEvidenceKindCode,
                    "SimulationWorldInventoryBuildingEvidenceInvalid");
                RequireStableId(building.SourceRecordStableId,
                    "SimulationWorldInventorySourceRecordStableIdInvalid");
                RequiredTrim(building.InteriorEvidenceKindCode,
                    "SimulationWorldInventoryInteriorEvidenceInvalid");
            }

            foreach (var player in request.Players)
            {
                if (player.InventoryCapacityUnits <= 0m)
                    throw new SimulationContractException(
                        "SimulationWorldInventoryPlayerCapacityInvalid");
                if (!string.IsNullOrWhiteSpace(player.CurrentBuildingStableId)
                    && !buildings.ContainsKey(player.CurrentBuildingStableId.Trim()))
                    throw new SimulationContractException(
                        SimulationWorldSurvivalInventoryCodes.BuildingNotFound);
                if (player.ManagedContainerStableIds == null)
                    throw new SimulationContractException("SimulationWorldInventoryManagementInvalid");
            }

            foreach (var container in request.Containers)
            {
                if (!buildings.TryGetValue(container.BuildingStableId.Trim(), out var building))
                    throw new SimulationContractException(
                        SimulationWorldSurvivalInventoryCodes.BuildingNotFound);
                if (!string.Equals(container.InteriorSpaceStableId.Trim(),
                    building.InteriorSpaceStableId.Trim(), StringComparison.Ordinal))
                    throw new SimulationContractException(
                        "SimulationWorldInventoryInteriorSpaceMismatch");
                if (container.CapacityUnits <= 0m || container.ManagerPlayerStableIds == null)
                    throw new SimulationContractException(
                        "SimulationWorldInventoryContainerCapacityInvalid");
                if (container.AccessPolicyCode != SimulationWorldSurvivalInventoryCodes.PublicAcquisition
                    && container.AccessPolicyCode != SimulationWorldSurvivalInventoryCodes.ManagerOnly
                    && container.AccessPolicyCode != SimulationWorldSurvivalInventoryCodes.Locked)
                    throw new SimulationContractException(
                        "SimulationWorldInventoryAccessPolicyInvalid");
                foreach (var managerId in container.ManagerPlayerStableIds)
                {
                    if (!players.TryGetValue(managerId.Trim(), out var manager)
                        || !manager.ManagedContainerStableIds.Contains(
                            container.ContainerStableId.Trim(), StringComparer.Ordinal))
                        throw new SimulationContractException(
                            "SimulationWorldInventoryManagementRelationMismatch");
                }
            }

            foreach (var player in request.Players)
            {
                foreach (var containerId in player.ManagedContainerStableIds)
                {
                    if (!containers.TryGetValue(containerId.Trim(), out var container)
                        || !container.ManagerPlayerStableIds.Contains(
                            player.PlayerStableId.Trim(), StringComparer.Ordinal))
                        throw new SimulationContractException(
                            "SimulationWorldInventoryManagementRelationMismatch");
                }
            }

            foreach (var itemStack in request.ItemStacks)
            {
                if (!containers.ContainsKey(itemStack.ContainerStableId.Trim()))
                    throw new SimulationContractException(
                        SimulationWorldSurvivalInventoryCodes.ContainerNotFound);
                RequireStableId(itemStack.ItemCode,
                    "SimulationWorldInventoryItemCodeInvalid");
                RequiredTrim(itemStack.KoreanName,
                    "SimulationWorldInventoryItemNameInvalid");
                RequiredTrim(itemStack.UnitCode,
                    "SimulationWorldInventoryItemUnitInvalid");
                RequireStableId(itemStack.BuildingItemRelationStableId,
                    "SimulationWorldInventoryBuildingItemRelationInvalid");
                RequiredTrim(itemStack.EvidenceKindCode,
                    "SimulationWorldInventoryItemEvidenceInvalid");
                if (itemStack.Quantity <= 0m)
                    throw new SimulationContractException(
                        "SimulationWorldInventoryItemQuantityInvalid");
            }

            foreach (var container in request.Containers)
            {
                var quantity = request.ItemStacks
                    .Where(value => string.Equals(value.ContainerStableId.Trim(),
                        container.ContainerStableId.Trim(), StringComparison.Ordinal))
                    .Sum(value => value.Quantity);
                if (quantity > container.CapacityUnits)
                    throw new SimulationContractException(
                        "SimulationWorldInventoryContainerCapacityExceeded");
            }
        }

        internal static string BuildWorldInventoryPayloadKey(
            SimulationWorldInventoryInitialStateRequest? request)
        {
            if (request == null) return "none";
            var parts = new List<string>
            {
                request.RuleRevision.Trim(),
                request.IsOperationalInventory ? "operational" : "simulation",
            };
            parts.AddRange(request.Buildings.OrderBy(value => value.BuildingStableId,
                StringComparer.Ordinal).Select(value => string.Join("~", new[]
                {
                    value.BuildingStableId.Trim(), value.TileKey.Trim(),
                    value.RegionStableId.Trim(), value.BuildingEvidenceKindCode.Trim(),
                    value.SourceRecordStableId.Trim(), value.InteriorSpaceStableId.Trim(),
                    value.InteriorEvidenceKindCode.Trim(),
                })));
            parts.AddRange(request.Players.OrderBy(value => value.PlayerStableId,
                StringComparer.Ordinal).Select(value => string.Join("~", new[]
                {
                    value.PlayerStableId.Trim(), value.CurrentBuildingStableId.Trim(),
                    value.InventoryCapacityUnits.ToString(CultureInfo.InvariantCulture),
                    string.Join(",", value.ManagedContainerStableIds
                        .Select(item => item.Trim()).OrderBy(item => item, StringComparer.Ordinal)),
                })));
            parts.AddRange(request.Containers.OrderBy(value => value.ContainerStableId,
                StringComparer.Ordinal).Select(value => string.Join("~", new[]
                {
                    value.ContainerStableId.Trim(), value.BuildingStableId.Trim(),
                    value.InteriorSpaceStableId.Trim(), value.AccessPolicyCode.Trim(),
                    value.CapacityUnits.ToString(CultureInfo.InvariantCulture),
                    string.Join(",", value.ManagerPlayerStableIds
                        .Select(item => item.Trim()).OrderBy(item => item, StringComparer.Ordinal)),
                    value.EvidenceKindCode.Trim(),
                })));
            parts.AddRange(request.ItemStacks.OrderBy(value => value.ItemStackStableId,
                StringComparer.Ordinal).Select(value => string.Join("~", new[]
                {
                    value.ItemStackStableId.Trim(), value.ContainerStableId.Trim(),
                    value.ItemCode.Trim(), value.KoreanName.Trim(),
                    value.Quantity.ToString(CultureInfo.InvariantCulture), value.UnitCode.Trim(),
                    value.BuildingItemRelationStableId.Trim(), value.EvidenceKindCode.Trim(),
                })));
            return string.Join("|", parts);
        }

        internal static SimulationWorldInventoryInitialStateRequest? CloneWorldInventoryInitialState(
            SimulationWorldInventoryInitialStateRequest? source)
            => source == null ? null : new SimulationWorldInventoryInitialStateRequest
            {
                RuleRevision = source.RuleRevision,
                Buildings = source.Buildings.Select(value =>
                    new SimulationWorldBuildingInteriorInitialStateRequest
                    {
                        BuildingStableId = value.BuildingStableId,
                        TileKey = value.TileKey,
                        RegionStableId = value.RegionStableId,
                        BuildingEvidenceKindCode = value.BuildingEvidenceKindCode,
                        SourceRecordStableId = value.SourceRecordStableId,
                        InteriorSpaceStableId = value.InteriorSpaceStableId,
                        InteriorEvidenceKindCode = value.InteriorEvidenceKindCode,
                    }).ToArray(),
                Players = source.Players.Select(value => new SimulationWorldPlayerInitialStateRequest
                {
                    PlayerStableId = value.PlayerStableId,
                    CurrentBuildingStableId = value.CurrentBuildingStableId,
                    InventoryCapacityUnits = value.InventoryCapacityUnits,
                    ManagedContainerStableIds = value.ManagedContainerStableIds.ToArray(),
                }).ToArray(),
                Containers = source.Containers.Select(value =>
                    new SimulationWorldContainerInitialStateRequest
                    {
                        ContainerStableId = value.ContainerStableId,
                        BuildingStableId = value.BuildingStableId,
                        InteriorSpaceStableId = value.InteriorSpaceStableId,
                        AccessPolicyCode = value.AccessPolicyCode,
                        CapacityUnits = value.CapacityUnits,
                        ManagerPlayerStableIds = value.ManagerPlayerStableIds.ToArray(),
                        EvidenceKindCode = value.EvidenceKindCode,
                    }).ToArray(),
                ItemStacks = source.ItemStacks.Select(value =>
                    new SimulationWorldItemStackInitialStateRequest
                    {
                        ItemStackStableId = value.ItemStackStableId,
                        ContainerStableId = value.ContainerStableId,
                        ItemCode = value.ItemCode,
                        KoreanName = value.KoreanName,
                        Quantity = value.Quantity,
                        UnitCode = value.UnitCode,
                        BuildingItemRelationStableId = value.BuildingItemRelationStableId,
                        EvidenceKindCode = value.EvidenceKindCode,
                    }).ToArray(),
                IsOperationalInventory = source.IsOperationalInventory,
            };

        private SimulationWorldItemAcquisitionPreviewSnapshot CreateWorldItemAcquisitionPreview(
            SimulationWorldItemAcquisitionPreviewRequest request)
        {
            var reasons = new List<string>();
            worldInventoryPlayers.TryGetValue(request.PlayerStableId.Trim(), out var player);
            worldInventoryBuildings.TryGetValue(request.BuildingStableId.Trim(), out var building);
            worldInventoryContainers.TryGetValue(request.ContainerStableId.Trim(), out var container);
            worldInventoryItemStacks.TryGetValue(request.ItemStackStableId.Trim(), out var itemStack);

            if (request.ObservedWorldRevision != Revision)
                reasons.Add(SimulationWorldSurvivalInventoryCodes.ExpectedRevisionMismatch);
            if (player == null)
                reasons.Add(SimulationWorldSurvivalInventoryCodes.PlayerNotFound);
            if (building == null)
                reasons.Add(SimulationWorldSurvivalInventoryCodes.BuildingNotFound);
            if (player != null && !string.Equals(player.CurrentBuildingStableId,
                request.BuildingStableId.Trim(), StringComparison.Ordinal))
                reasons.Add(SimulationWorldSurvivalInventoryCodes.PlayerOutsideBuilding);
            if (container == null)
                reasons.Add(SimulationWorldSurvivalInventoryCodes.ContainerNotFound);
            if (container != null && !string.Equals(container.BuildingStableId,
                request.BuildingStableId.Trim(), StringComparison.Ordinal))
                reasons.Add(SimulationWorldSurvivalInventoryCodes.ContainerBuildingMismatch);
            if (container != null && player != null && !CanAcquire(container, player))
                reasons.Add(SimulationWorldSurvivalInventoryCodes.ContainerAccessDenied);
            if (itemStack == null)
                reasons.Add(SimulationWorldSurvivalInventoryCodes.ItemStackNotFound);
            if (itemStack != null && !string.Equals(itemStack.ContainerStableId,
                request.ContainerStableId.Trim(), StringComparison.Ordinal))
                reasons.Add(SimulationWorldSurvivalInventoryCodes.ItemStackContainerMismatch);
            if (itemStack != null && itemStack.Quantity < request.Quantity)
                reasons.Add(SimulationWorldSurvivalInventoryCodes.QuantityUnavailable);

            var playerQuantity = itemStack == null || player == null
                ? 0m
                : PlayerItemQuantity(player.PlayerStableId, itemStack.ItemCode, itemStack.UnitCode);
            var playerTotal = player == null ? 0m : PlayerTotalQuantity(player.PlayerStableId);
            if (player != null && playerTotal + request.Quantity > player.InventoryCapacityUnits)
                reasons.Add(SimulationWorldSurvivalInventoryCodes.PlayerCapacityExceeded);

            return new SimulationWorldItemAcquisitionPreviewSnapshot
            {
                SessionStableId = SessionStableId,
                WorldRevision = Revision,
                PlayerStableId = request.PlayerStableId.Trim(),
                BuildingStableId = request.BuildingStableId.Trim(),
                ContainerStableId = request.ContainerStableId.Trim(),
                ItemStackStableId = request.ItemStackStableId.Trim(),
                ItemCode = itemStack?.ItemCode ?? string.Empty,
                RequestedQuantity = request.Quantity,
                ContainerQuantityBefore = itemStack?.Quantity ?? 0m,
                ContainerQuantityAfter = itemStack == null ? 0m : itemStack.Quantity - request.Quantity,
                PlayerQuantityBefore = playerQuantity,
                PlayerQuantityAfter = playerQuantity + request.Quantity,
                EligibilityStateCode = reasons.Count == 0
                    ? SimulationWorldSurvivalInventoryCodes.Allowed
                    : SimulationWorldSurvivalInventoryCodes.Blocked,
                BlockReasonCodes = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                CanConfirm = reasons.Count == 0,
                StateChanged = false,
                SimulationOnly = true,
                IsOperationalState = false,
            };
        }

        private SimulationWorldInventorySnapshot CreateWorldInventorySnapshot()
            => new SimulationWorldInventorySnapshot
            {
                SessionStableId = SessionStableId,
                WorldRevision = Revision,
                WorldTick = CurrentTick,
                RuleRevision = worldInventoryRuleRevision,
                Buildings = worldInventoryBuildings.Values.OrderBy(value => value.BuildingStableId,
                    StringComparer.Ordinal).Select(CloneWorldBuildingInterior).ToArray(),
                Containers = worldInventoryContainers.Values.OrderBy(value => value.ContainerStableId,
                    StringComparer.Ordinal).Select(CloneWorldContainer).ToArray(),
                ContainerItemStacks = worldInventoryItemStacks.Values
                    .OrderBy(value => value.ItemStackStableId, StringComparer.Ordinal)
                    .Select(CloneWorldItemStack).ToArray(),
                Players = worldInventoryPlayers.Values.OrderBy(value => value.PlayerStableId,
                    StringComparer.Ordinal).Select(value => new SimulationWorldPlayerInventorySnapshot
                    {
                        PlayerStableId = value.PlayerStableId,
                        CurrentBuildingStableId = value.CurrentBuildingStableId,
                        InventoryCapacityUnits = value.InventoryCapacityUnits,
                        ManagedContainerStableIds = value.ManagedContainerStableIds.ToArray(),
                        Items = worldInventoryPlayerItems
                            .Where(item => item.Key.StartsWith(value.PlayerStableId + "|",
                                StringComparison.Ordinal))
                            .Select(item => CloneWorldPlayerItem(item.Value))
                            .OrderBy(item => item.ItemCode, StringComparer.Ordinal).ToArray(),
                    }).ToArray(),
                Transfers = worldInventoryTransfers.Select(CloneWorldItemTransfer).ToArray(),
                SimulationOnly = true,
                IsOperationalState = false,
            };

        private bool HasDifferentKindCommand(string commandId)
            => appliedCommands.ContainsKey(commandId)
                || appliedDecisionCommands.ContainsKey(commandId)
                || appliedTurnClosingCommands.ContainsKey(commandId)
                || appliedNpcPolicyCommands.ContainsKey(commandId)
                || appliedLogisticsMovementCommands.ContainsKey(commandId)
                || appliedWorldItemAcquisitionCommands.ContainsKey(commandId)
                || HasAppliedSurvivalTarotCommand(commandId)
                || appliedFreightReceiptCommands.ContainsKey(commandId)
                || appliedIndividualOrderCommands.ContainsKey(commandId)
                || appliedHarvestImpactCommands.ContainsKey(commandId)
                || appliedGroupOrderCommands.ContainsKey(commandId)
                || appliedMarketConsumptionCommands.ContainsKey(commandId)
                || appliedFoodDeliveryCommands.ContainsKey(commandId)
                || appliedFoodReceiptCommands.ContainsKey(commandId)
                || HasAppliedFarmSurvivalCommand(commandId)
                || HasAppliedCollectibleCardCommand(commandId)
                || HasAppliedNatureSurvivalCommand(commandId)
                || HasAppliedActorEquipmentCommand(commandId)
                || appliedRegionalIncidentResponseCommands.ContainsKey(commandId)
                || appliedIntegratedWorldCommands.ContainsKey(commandId)
                || HasAppliedFranchiseSupplyCommand(commandId);

        private static bool CanAcquire(
            SimulationWorldContainerSnapshot container,
            SimulationWorldPlayerInventorySnapshot player)
            => container.AccessPolicyCode == SimulationWorldSurvivalInventoryCodes.PublicAcquisition
                || (container.AccessPolicyCode == SimulationWorldSurvivalInventoryCodes.ManagerOnly
                    && container.ManagerPlayerStableIds.Contains(
                        player.PlayerStableId, StringComparer.Ordinal)
                    && player.ManagedContainerStableIds.Contains(
                        container.ContainerStableId, StringComparer.Ordinal));

        private decimal PlayerTotalQuantity(string playerStableId)
            => worldInventoryPlayerItems
                .Where(value => value.Key.StartsWith(playerStableId + "|", StringComparison.Ordinal))
                .Sum(value => value.Value.Quantity);

        private decimal PlayerItemQuantity(string playerStableId, string itemCode, string unitCode)
            => worldInventoryPlayerItems.TryGetValue(
                PlayerItemKey(playerStableId, itemCode, unitCode), out var value)
                    ? value.Quantity : 0m;

        private static string PlayerItemKey(string playerStableId, string itemCode, string unitCode)
            => playerStableId.Trim() + "|" + itemCode.Trim() + "|" + unitCode.Trim();

        private static string BuildWorldItemAcquisitionPayloadKey(
            SimulationWorldItemAcquisitionConfirmRequest request)
            => string.Join("|", new[]
            {
                request.PlayerStableId.Trim(), request.BuildingStableId.Trim(),
                request.ContainerStableId.Trim(), request.ItemStackStableId.Trim(),
                request.Quantity.ToString(CultureInfo.InvariantCulture),
                request.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
            });

        private static void ValidateWorldItemAcquisitionPreviewRequest(
            SimulationWorldItemAcquisitionPreviewRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.ObservedWorldRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");
            ValidateWorldItemAcquisitionFields(request.PlayerStableId, request.BuildingStableId,
                request.ContainerStableId, request.ItemStackStableId, request.Quantity);
        }

        internal static void ValidateWorldItemAcquisitionConfirmRequest(
            SimulationWorldItemAcquisitionConfirmRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireStableId(request.CommandId, "SimulationCommandIdInvalid");
            if (request.ExpectedRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");
            ValidateWorldItemAcquisitionFields(request.PlayerStableId, request.BuildingStableId,
                request.ContainerStableId, request.ItemStackStableId, request.Quantity);
        }

        private static void ValidateWorldItemAcquisitionFields(
            string playerStableId, string buildingStableId, string containerStableId,
            string itemStackStableId, decimal quantity)
        {
            RequireStableId(playerStableId, "SimulationWorldInventoryPlayerStableIdInvalid");
            RequireStableId(buildingStableId, "SimulationWorldInventoryBuildingStableIdInvalid");
            RequireStableId(containerStableId, "SimulationWorldInventoryContainerStableIdInvalid");
            RequireStableId(itemStackStableId, "SimulationWorldInventoryItemStackStableIdInvalid");
            if (quantity <= 0m)
                throw new SimulationContractException("SimulationWorldInventoryQuantityInvalid");
        }

        private static void EnsureUnique(IEnumerable<string> values, string errorCode)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var normalized = RequiredTrim(value, errorCode);
                if (!set.Add(normalized)) throw new SimulationContractException(errorCode);
            }
        }

        private static string RequiredTrim(string value, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new SimulationContractException(errorCode);
            return value.Trim();
        }

        private static SimulationWorldBuildingInteriorSnapshot CloneWorldBuildingInterior(
            SimulationWorldBuildingInteriorSnapshot source)
            => new SimulationWorldBuildingInteriorSnapshot
            {
                BuildingStableId = source.BuildingStableId,
                TileKey = source.TileKey,
                RegionStableId = source.RegionStableId,
                BuildingEvidenceKindCode = source.BuildingEvidenceKindCode,
                SourceRecordStableId = source.SourceRecordStableId,
                InteriorSpaceStableId = source.InteriorSpaceStableId,
                InteriorEvidenceKindCode = source.InteriorEvidenceKindCode,
            };

        private static SimulationWorldContainerSnapshot CloneWorldContainer(
            SimulationWorldContainerSnapshot source)
            => new SimulationWorldContainerSnapshot
            {
                ContainerStableId = source.ContainerStableId,
                BuildingStableId = source.BuildingStableId,
                InteriorSpaceStableId = source.InteriorSpaceStableId,
                AccessPolicyCode = source.AccessPolicyCode,
                CapacityUnits = source.CapacityUnits,
                ManagerPlayerStableIds = source.ManagerPlayerStableIds.ToArray(),
                EvidenceKindCode = source.EvidenceKindCode,
            };

        private static SimulationWorldItemStackSnapshot CloneWorldItemStack(
            SimulationWorldItemStackSnapshot source)
            => new SimulationWorldItemStackSnapshot
            {
                ItemStackStableId = source.ItemStackStableId,
                ContainerStableId = source.ContainerStableId,
                ItemCode = source.ItemCode,
                KoreanName = source.KoreanName,
                Quantity = source.Quantity,
                UnitCode = source.UnitCode,
                BuildingItemRelationStableId = source.BuildingItemRelationStableId,
                EvidenceKindCode = source.EvidenceKindCode,
            };

        private static SimulationWorldPlayerItemSnapshot CloneWorldPlayerItem(
            SimulationWorldPlayerItemSnapshot source)
            => new SimulationWorldPlayerItemSnapshot
            {
                ItemCode = source.ItemCode,
                KoreanName = source.KoreanName,
                Quantity = source.Quantity,
                UnitCode = source.UnitCode,
            };

        private static SimulationWorldItemTransferSnapshot CloneWorldItemTransfer(
            SimulationWorldItemTransferSnapshot source)
            => new SimulationWorldItemTransferSnapshot
            {
                TransferStableId = source.TransferStableId,
                CommandId = source.CommandId,
                PlayerStableId = source.PlayerStableId,
                BuildingStableId = source.BuildingStableId,
                SourceContainerStableId = source.SourceContainerStableId,
                SourceItemStackStableId = source.SourceItemStackStableId,
                ItemCode = source.ItemCode,
                Quantity = source.Quantity,
                UnitCode = source.UnitCode,
                AppliedWorldTick = source.AppliedWorldTick,
                AppliedWorldRevision = source.AppliedWorldRevision,
                EvidenceKindCode = source.EvidenceKindCode,
                SimulationOnly = source.SimulationOnly,
            };

        internal static SimulationWorldInventorySnapshot CloneWorldInventory(
            SimulationWorldInventorySnapshot source)
            => new SimulationWorldInventorySnapshot
            {
                SessionStableId = source.SessionStableId,
                WorldRevision = source.WorldRevision,
                WorldTick = source.WorldTick,
                RuleRevision = source.RuleRevision,
                Buildings = source.Buildings.Select(CloneWorldBuildingInterior).ToArray(),
                Containers = source.Containers.Select(CloneWorldContainer).ToArray(),
                ContainerItemStacks = source.ContainerItemStacks.Select(CloneWorldItemStack).ToArray(),
                Players = source.Players.Select(value => new SimulationWorldPlayerInventorySnapshot
                {
                    PlayerStableId = value.PlayerStableId,
                    CurrentBuildingStableId = value.CurrentBuildingStableId,
                    InventoryCapacityUnits = value.InventoryCapacityUnits,
                    ManagedContainerStableIds = value.ManagedContainerStableIds.ToArray(),
                    Items = value.Items.Select(CloneWorldPlayerItem).ToArray(),
                }).ToArray(),
                Transfers = source.Transfers.Select(CloneWorldItemTransfer).ToArray(),
                SimulationOnly = source.SimulationOnly,
                IsOperationalState = source.IsOperationalState,
            };

        private static SimulationWorldItemAcquisitionResultSnapshot CloneWorldItemAcquisitionResult(
            SimulationWorldItemAcquisitionResultSnapshot source)
            => new SimulationWorldItemAcquisitionResultSnapshot
            {
                CommandId = source.CommandId,
                AppliedWorldRevision = source.AppliedWorldRevision,
                AppliedWorldTick = source.AppliedWorldTick,
                Transfer = CloneWorldItemTransfer(source.Transfer),
                Inventory = CloneWorldInventory(source.Inventory),
                SimulationOnly = source.SimulationOnly,
                IsOperationalState = source.IsOperationalState,
            };

        private sealed class AppliedWorldItemAcquisitionCommand
        {
            public AppliedWorldItemAcquisitionCommand(
                string payloadKey, SimulationWorldItemAcquisitionResultSnapshot result)
            {
                PayloadKey = payloadKey;
                Result = result;
            }

            public string PayloadKey { get; }
            public SimulationWorldItemAcquisitionResultSnapshot Result { get; }
        }
    }
}
