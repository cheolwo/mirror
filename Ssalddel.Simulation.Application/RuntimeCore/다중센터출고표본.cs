using System;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Application
{
    /// <summary>실제 매장·주문·작업자를 사용하지 않는 다중 센터 출고 연습 표본.</summary>
    public static class 다중센터출고표본
    {
        public const string ScenarioId = "scenario:regional-fulfillment.v1";
        public const string NearCenter = "center:sample:near";
        public const string ReadyCenter = "center:sample:ready";

        public static 경영SimulationSession생성Request Create(Guid requestId)
            => new 경영SimulationSession생성Request
            {
                ClientRequestId = requestId,
                ScenarioStableId = ScenarioId,
                ScenarioDataRevision = "regional-fulfillment-data.r1",
                RuleRevision = "regional-fulfillment-rule.r1",
                ScenarioSeed = 1025,
                DurationTicks = 28,
                WorldContext = new SimulationWorldContext생성Request
                {
                    FactionStableId = "faction:sample:operator",
                    TerritoryStableId = "territory:sample:region",
                    SettlementStableId = "settlement:sample:centers",
                    GameDateStartsOn = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero),
                },
            };

        public static SimulationFulfillmentStateSnapshot InitialState()
            => new SimulationFulfillmentStateSnapshot
            {
                ScenarioStableId = ScenarioId,
                ConfigurationRevision = "regional-fulfillment-configuration.r1",
                Centers = new[]
                {
                    new SimulationFulfillmentCenter { CenterStableId = NearCenter, DisplayName = "가까운 모의 센터" },
                    new SimulationFulfillmentCenter { CenterStableId = ReadyCenter, DisplayName = "출고 가능한 모의 센터" },
                },
                Locations = new[]
                {
                    new SimulationFulfillmentLocation { LocationStableId = "location:sample:near", CenterStableId = NearCenter, LocationBarcode = "SAMPLE-NEAR-A-01" },
                    new SimulationFulfillmentLocation { LocationStableId = "location:sample:ready", CenterStableId = ReadyCenter, LocationBarcode = "SAMPLE-READY-A-03-2-B" },
                },
                Stocks = new[]
                {
                    Stock("stock:sample:near", NearCenter, "location:sample:near"),
                    Stock("stock:sample:ready", ReadyCenter, "location:sample:ready"),
                },
                Workers = new[]
                {
                    new SimulationFulfillmentWorker { WorkerStableId = "worker:sample:near", DisplayName = "근무 전 직원", CenterStableId = NearCenter, CapacityUnitCode = "piece", Available = false },
                    new SimulationFulfillmentWorker { WorkerStableId = "worker:sample:ready", DisplayName = "모의 출고 직원", CenterStableId = ReadyCenter, CapacityUnitCode = "piece", MaximumActiveOrders = 2, MaximumActiveQuantity = 20m },
                },
            };

        public static SimulationFulfillmentOrderRequest Order(string orderId = "order:sample:001")
            => new SimulationFulfillmentOrderRequest
            {
                OrderStableId = orderId,
                Lines = new[]
                {
                    new SimulationFulfillmentOrderLine { LineStableId = "line:sample:001", ProductStableId = "product:sample:water", ProductBarcode = "SAMPLE-WATER-500", UnitCode = "piece", Quantity = 3m },
                },
                CenterEstimates = new[]
                {
                    new SimulationFulfillmentCenterEstimate { CenterStableId = NearCenter, EstimatedDistanceKm = 1m, EstimatedCost = 1m },
                    new SimulationFulfillmentCenterEstimate { CenterStableId = ReadyCenter, EstimatedDistanceKm = 2m, EstimatedCost = 2m },
                },
            };

        private static SimulationFulfillmentStock Stock(string id, string center, string location)
            => new SimulationFulfillmentStock
            {
                StockStableId = id, CenterStableId = center, LocationStableId = location,
                ProductStableId = "product:sample:water", ProductBarcode = "SAMPLE-WATER-500",
                UnitCode = "piece", LotStableId = "lot:" + id, OnHandQuantity = 10m,
            };
    }
}
