using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Warehouse;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.OperationalTransport;
using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Samples.WarehouseWorld
{
    public sealed class WarehouseWorldApiOptions { public string BaseUrl { get; set; } = string.Empty; public int TimeoutSeconds { get; set; } = 15; }
    public sealed class WarehouseRuntimeSessionTokenProvider
        : MonoBehaviour, IOperationalRuntimeAccessTokenProvider
    {
        private static string accessToken = string.Empty;
        public static void SetAccessToken(string token) => accessToken = token?.Trim() ?? string.Empty;
        public string GetAccessToken() => accessToken;
        public string GetRequiredToken() => string.IsNullOrWhiteSpace(accessToken) ? throw new InvalidOperationException("WarehouseWorldAccessTokenMissing") : accessToken;
        private void OnDestroy() => accessToken = string.Empty;
    }

    public sealed class SimulatedWarehouseWorldApiClient : IWarehouseWorldApiClient
    {
        public Task<WarehouseWorldSnapshotApiModel> GetAsync(long warehouseId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); var time = DateTimeOffset.Parse("2026-08-08T15:00:00+09:00");
            return Task.FromResult(new WarehouseWorldSnapshotApiModel
            {
                StableId = "warehouse-zone:" + warehouseId, Revision = "simulation-warehouse-world-1", GeneratedAtUtc = time,
                TotalAvailableQuantity = 16, TotalReservedQuantity = 3, UnassignedLocationCount = 1,
                InventoryItems = new[]
                {
                    new WarehouseWorldInventoryItemApiModel { StableId = "warehouse-inventory:31", WarehouseStableId = "warehouse:" + warehouseId, WarehouseName = "SIMULATED 도심 창고", ProductName = "감자", Sku = "POTATO-20", AvailableQuantity = 12, ReservedQuantity = 3, StorageLocation = "A-01", Status = "적재완료", UpdatedAtUtc = time },
                    new WarehouseWorldInventoryItemApiModel { StableId = "warehouse-inventory:32", WarehouseStableId = "warehouse:" + warehouseId, WarehouseName = "SIMULATED 도심 창고", ProductName = "양파", Sku = "ONION-10", AvailableQuantity = 4, Status = "검수완료", UpdatedAtUtc = time },
                },
                Tasks = new[]
                {
                    new WarehouseWorldTaskApiModel { StableId = "warehouse-putaway:32", WarehouseStableId = "warehouse:" + warehouseId, InventoryItemStableId = "warehouse-inventory:32", CanonicalTaskStableId = "inbound-task:91", TaskKind = "PutAway", ProductName = "양파", Sku = "ONION-10", Quantity = 4, Status = "검수완료", CanExecute = true, UpdatedAtUtc = time },
                    new WarehouseWorldTaskApiModel { StableId = "warehouse-picking:fixture", WarehouseStableId = "warehouse:" + warehouseId, TaskKind = "Picking", ProductName = "감자", Sku = "POTATO-20", Quantity = 3, LocationCode = "A-01", Status = "대기", CanExecute = true, UpdatedAtUtc = time },
                },
                Npcs = new[]
                {
                    new WarehouseWorldNpcApiModel { StableId = "warehouse-npc:dock-worker:fixture", WarehouseStableId = "warehouse:" + warehouseId, SourceTaskStableId = "warehouse-putaway:32", RoleCode = "DockWorker", RouteCode = "warehouse.inbound-to-storage", CurrentWaypointKey = "warehouse.inbound-dock", DestinationWaypointKey = "warehouse.storage-zone", ActivityCode = "PutAway" },
                    new WarehouseWorldNpcApiModel { StableId = "warehouse-npc:picker:fixture", WarehouseStableId = "warehouse:" + warehouseId, SourceTaskStableId = "warehouse-picking:fixture", RoleCode = "Picker", RouteCode = "warehouse.rack-to-outbound", CurrentWaypointKey = "warehouse.rack-zone", DestinationWaypointKey = "warehouse.outbound-staging", ActivityCode = "Picking" },
                },
                InboundHandoffs = new[]
                {
                    new CargoWarehouseHandoffApiModel
                    {
                        StableId = "cargo-handoff:transport-71.inbound-91", Revision = 1,
                        HandoffStateCode = CargoHandoffStateCodes.ArrivedAtWarehouse,
                        CargoStableId = "cargo:transport-71", TransportTaskStableId = "transport-task:71",
                        InboundTaskStableId = "inbound-task:91", GeneratedAt = time,
                        Movements = new[]
                        {
                            new NpcMovementApiModel { StableId = "npc-movement:transporter.transport-71.inbound-91", Revision = 1, NpcStableId = "npc:transport-driver.71", ActorRoleCode = "Transporter", WorldZoneCode = "warehouse", RouteCode = "warehouse-transporter-dropoff", CurrentWaypointKey = "warehouse.approach", DestinationWaypointKey = "warehouse.inbound-dock", MovementStateCode = NpcMovementStateCodes.Moving, ArrivalActionCode = "open-cargo-door", SourceTypeCode = NpcMovementSourceTypeCodes.OperationalProjection, CanonicalTaskStableId = "transport-task:71", GeneratedAt = time },
                            new NpcMovementApiModel { StableId = "npc-movement:inbound-worker.transport-71.inbound-91", Revision = 1, NpcStableId = "npc:warehouse-inbound-worker.91", ActorRoleCode = "WarehouseInboundWorker", WorldZoneCode = "warehouse", RouteCode = "warehouse-inbound-worker-handoff", CurrentWaypointKey = "warehouse.staff-entry", DestinationWaypointKey = "warehouse.inbound-dock", MovementStateCode = NpcMovementStateCodes.Moving, ArrivalActionCode = "unload-cargo", SourceTypeCode = NpcMovementSourceTypeCodes.OperationalProjection, CanonicalTaskStableId = "inbound-task:91", GeneratedAt = time },
                        },
                    },
                },
            });
        }
    }

    public sealed class OperationalWarehouseWorldApiClient : IWarehouseWorldApiClient
    {
        private readonly IOperationalWorldProjectionTransport transport;
        public OperationalWarehouseWorldApiClient(WarehouseWorldApiOptions options, WarehouseRuntimeSessionTokenProvider tokenProvider)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (tokenProvider == null) throw new ArgumentNullException(nameof(tokenProvider));
            transport = new UnityWebRequestOperationalWorldProjectionTransport(
                new OperationalWorldProjectionEndpoint(
                    options.BaseUrl,
                    Math.Max(1, options.TimeoutSeconds)),
                tokenProvider);
        }
        public async Task<WarehouseWorldSnapshotApiModel> GetAsync(long warehouseId, CancellationToken cancellationToken = default)
        {
            var route = WarehouseWorldApiRoutes.AuthorizedSnapshot + "?warehouseId=" + warehouseId.ToString(CultureInfo.InvariantCulture);
            var json = await transport.GetAsync(route, false, true, cancellationToken);
            var wire = JsonUtility.FromJson<WarehouseWorldSnapshotWire>(json);
            return wire?.ToApiModel()
                ?? throw new InvalidOperationException("WarehouseWorldJsonInvalid");
        }
    }

    [Serializable] internal sealed class WarehouseWorldSnapshotWire
    {
        public string stableId = string.Empty, revision = string.Empty, generatedAtUtc = string.Empty; public int totalAvailableQuantity, totalReservedQuantity, unassignedLocationCount;
        public WarehouseInventoryWire[] inventoryItems = Array.Empty<WarehouseInventoryWire>(); public WarehouseTaskWire[] tasks = Array.Empty<WarehouseTaskWire>(); public WarehouseNpcWire[] npcs = Array.Empty<WarehouseNpcWire>(); public WarehouseHandoffWire[] inboundHandoffs = Array.Empty<WarehouseHandoffWire>();
        public WarehouseWorldSnapshotApiModel ToApiModel() => new WarehouseWorldSnapshotApiModel { StableId = stableId, Revision = revision, GeneratedAtUtc = Parse(generatedAtUtc), TotalAvailableQuantity = totalAvailableQuantity, TotalReservedQuantity = totalReservedQuantity, UnassignedLocationCount = unassignedLocationCount, InventoryItems = Array.ConvertAll(inventoryItems, value => value.ToApiModel()), Tasks = Array.ConvertAll(tasks, value => value.ToApiModel()), Npcs = Array.ConvertAll(npcs, value => value.ToApiModel()), InboundHandoffs = Array.ConvertAll(inboundHandoffs, value => value.ToApiModel()) };
        internal static DateTimeOffset Parse(string value) { if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)) throw new InvalidOperationException("WarehouseWorldDateInvalid"); return result; }
    }
    [Serializable] internal sealed class WarehouseInventoryWire
    {
        public string stableId = string.Empty, warehouseStableId = string.Empty, warehouseName = string.Empty, productName = string.Empty, sku = string.Empty, optionName = string.Empty, storageLocation = string.Empty, status = string.Empty, updatedAtUtc = string.Empty; public int availableQuantity, reservedQuantity; public bool hasCommunityLedger;
        public WarehouseWorldInventoryItemApiModel ToApiModel() => new WarehouseWorldInventoryItemApiModel { StableId = stableId, WarehouseStableId = warehouseStableId, WarehouseName = warehouseName, ProductName = productName, Sku = sku, OptionName = optionName, AvailableQuantity = availableQuantity, ReservedQuantity = reservedQuantity, StorageLocation = storageLocation, Status = status, HasCommunityLedger = hasCommunityLedger, UpdatedAtUtc = WarehouseWorldSnapshotWire.Parse(updatedAtUtc) };
    }
    [Serializable] internal sealed class WarehouseTaskWire
    {
        public string stableId = string.Empty, warehouseStableId = string.Empty, inventoryItemStableId = string.Empty, canonicalTaskStableId = string.Empty, taskKind = string.Empty, productName = string.Empty, sku = string.Empty, locationCode = string.Empty, status = string.Empty, updatedAtUtc = string.Empty; public int quantity; public bool canExecute;
        public WarehouseWorldTaskApiModel ToApiModel() => new WarehouseWorldTaskApiModel { StableId = stableId, WarehouseStableId = warehouseStableId, InventoryItemStableId = inventoryItemStableId, CanonicalTaskStableId = canonicalTaskStableId, TaskKind = taskKind, ProductName = productName, Sku = sku, Quantity = quantity, LocationCode = locationCode, Status = status, CanExecute = canExecute, UpdatedAtUtc = WarehouseWorldSnapshotWire.Parse(updatedAtUtc) };
    }
    [Serializable] internal sealed class WarehouseNpcWire
    {
        public string stableId = string.Empty, warehouseStableId = string.Empty, sourceTaskStableId = string.Empty, roleCode = string.Empty, routeCode = string.Empty, currentWaypointKey = string.Empty, destinationWaypointKey = string.Empty, activityCode = string.Empty;
        public WarehouseWorldNpcApiModel ToApiModel() => new WarehouseWorldNpcApiModel { StableId = stableId, WarehouseStableId = warehouseStableId, SourceTaskStableId = sourceTaskStableId, RoleCode = roleCode, RouteCode = routeCode, CurrentWaypointKey = currentWaypointKey, DestinationWaypointKey = destinationWaypointKey, ActivityCode = activityCode };
    }
    [Serializable] internal sealed class WarehouseHandoffWire
    {
        public string stableId = string.Empty, handoffStateCode = string.Empty, cargoStableId = string.Empty, transportTaskStableId = string.Empty, inboundTaskStableId = string.Empty, generatedAt = string.Empty; public long revision; public WarehouseNpcMovementWire[] movements = Array.Empty<WarehouseNpcMovementWire>();
        public CargoWarehouseHandoffApiModel ToApiModel() => new CargoWarehouseHandoffApiModel { StableId = stableId, Revision = revision, HandoffStateCode = handoffStateCode, CargoStableId = cargoStableId, TransportTaskStableId = transportTaskStableId, InboundTaskStableId = inboundTaskStableId, Movements = Array.ConvertAll(movements, value => value.ToApiModel()), GeneratedAt = WarehouseWorldSnapshotWire.Parse(generatedAt) };
    }
    [Serializable] internal sealed class WarehouseNpcMovementWire
    {
        public string stableId = string.Empty, npcStableId = string.Empty, actorRoleCode = string.Empty, worldZoneCode = string.Empty, routeCode = string.Empty, currentWaypointKey = string.Empty, destinationWaypointKey = string.Empty, movementStateCode = string.Empty, arrivalActionCode = string.Empty, sourceTypeCode = string.Empty, canonicalTaskStableId = string.Empty, generatedAt = string.Empty; public long revision;
        public NpcMovementApiModel ToApiModel() => new NpcMovementApiModel { StableId = stableId, Revision = revision, NpcStableId = npcStableId, ActorRoleCode = actorRoleCode, WorldZoneCode = worldZoneCode, RouteCode = routeCode, CurrentWaypointKey = currentWaypointKey, DestinationWaypointKey = destinationWaypointKey, MovementStateCode = movementStateCode, ArrivalActionCode = arrivalActionCode, SourceTypeCode = sourceTypeCode, CanonicalTaskStableId = canonicalTaskStableId, GeneratedAt = WarehouseWorldSnapshotWire.Parse(generatedAt) };
    }
}
