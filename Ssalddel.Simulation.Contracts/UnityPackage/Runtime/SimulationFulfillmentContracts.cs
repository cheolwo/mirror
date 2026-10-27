using System;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>가상 출고 명령입니다. 운영 재고·계약·기사 인수에 대한 권한이 아닙니다.</summary>
    public static class SimulationFulfillmentActionCodes
    {
        public const string Configure = "Configure";
        public const string Reserve = "Reserve";
        public const string Pick = "Pick";
        public const string PackReady = "PackReady";
        public const string Cancel = "Cancel";
    }

    public static class SimulationFulfillmentStateCodes
    {
        public const string Preview = "Preview";
        public const string Rejected = "Rejected";
        public const string Reserved = "Reserved";
        public const string Picking = "Picking";
        public const string PackReady = "PackReady";
        public const string Cancelled = "Cancelled";
    }

    public sealed class SimulationFulfillmentStateSnapshot
    {
        public string ScenarioStableId { get; set; } = string.Empty;
        public string ConfigurationRevision { get; set; } = string.Empty;
        public SimulationFulfillmentCenter[] Centers { get; set; } = Array.Empty<SimulationFulfillmentCenter>();
        public SimulationFulfillmentLocation[] Locations { get; set; } = Array.Empty<SimulationFulfillmentLocation>();
        public SimulationFulfillmentStock[] Stocks { get; set; } = Array.Empty<SimulationFulfillmentStock>();
        public SimulationFulfillmentWorker[] Workers { get; set; } = Array.Empty<SimulationFulfillmentWorker>();
        public SimulationFulfillmentPlanSnapshot[] Orders { get; set; } = Array.Empty<SimulationFulfillmentPlanSnapshot>();
    }

    public sealed class SimulationFulfillmentCenter
    {
        public string CenterStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool OutboundEnabled { get; set; } = true;
    }

    public sealed class SimulationFulfillmentLocation
    {
        public string LocationStableId { get; set; } = string.Empty;
        public string CenterStableId { get; set; } = string.Empty;
        public string LocationBarcode { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>OnHand는 선반 위 수량, Reserved는 그 수량 안의 점유입니다. 피킹 수량은 배분에 따로 남깁니다.</summary>
    public sealed class SimulationFulfillmentStock
    {
        public string StockStableId { get; set; } = string.Empty;
        public string CenterStableId { get; set; } = string.Empty;
        public string LocationStableId { get; set; } = string.Empty;
        public string ProductStableId { get; set; } = string.Empty;
        public string ProductBarcode { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public string LotStableId { get; set; } = string.Empty;
        public decimal OnHandQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public sealed class SimulationFulfillmentWorker
    {
        public string WorkerStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string CenterStableId { get; set; } = string.Empty;
        public bool Available { get; set; } = true;
        public bool CanPick { get; set; } = true;
        /// <summary>수용량은 이 단위의 배정량끼리만 합산합니다. 단위 환산은 하지 않습니다.</summary>
        public string CapacityUnitCode { get; set; } = string.Empty;
        public decimal MaximumActiveQuantity { get; set; } = 40m;
        public int MaximumActiveOrders { get; set; } = 1;
    }

    public sealed class SimulationFulfillmentOrderLine
    {
        public string LineStableId { get; set; } = string.Empty;
        public string ProductStableId { get; set; } = string.Empty;
        public string ProductBarcode { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    /// <summary>명시 입력한 추정값만 담습니다. 실제 주행·GPS·최단경로 확인이 아닙니다.</summary>
    public sealed class SimulationFulfillmentCenterEstimate
    {
        public string CenterStableId { get; set; } = string.Empty;
        public decimal? EstimatedDistanceKm { get; set; }
        public decimal? EstimatedCost { get; set; }
    }

    public sealed class SimulationFulfillmentOrderRequest
    {
        public string OrderStableId { get; set; } = string.Empty;
        public bool AllowSplitCenters { get; set; }
        /// <summary>빈 배열은 전체 후보, 값이 있으면 사용자가 허용한 센터만 후보로 사용합니다.</summary>
        public string[] AllowedCenterStableIds { get; set; } = Array.Empty<string>();
        public SimulationFulfillmentOrderLine[] Lines { get; set; } = Array.Empty<SimulationFulfillmentOrderLine>();
        public SimulationFulfillmentCenterEstimate[] CenterEstimates { get; set; } = Array.Empty<SimulationFulfillmentCenterEstimate>();
    }

    public sealed class SimulationFulfillmentCenterCandidate
    {
        public string CenterStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsEligible { get; set; }
        public bool CanFulfillWholeOrder { get; set; }
        public string[] ReasonCodes { get; set; } = Array.Empty<string>();
        public decimal? EstimatedDistanceKm { get; set; }
        public decimal? EstimatedCost { get; set; }
        public decimal? PreferenceScore { get; set; }
    }

    public sealed class SimulationFulfillmentAllocation
    {
        public string AllocationStableId { get; set; } = string.Empty;
        public string LineStableId { get; set; } = string.Empty;
        public string CenterStableId { get; set; } = string.Empty;
        public string StockStableId { get; set; } = string.Empty;
        public string LocationStableId { get; set; } = string.Empty;
        public string LocationBarcode { get; set; } = string.Empty;
        public string ProductStableId { get; set; } = string.Empty;
        public string ProductBarcode { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public string LotStableId { get; set; } = string.Empty;
        public string WorkerStableId { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal PickedQuantity { get; set; }
    }

    public sealed class SimulationFulfillmentPlanSnapshot
    {
        public string OrderStableId { get; set; } = string.Empty;
        public SimulationFulfillmentOrderRequest Order { get; set; } = new SimulationFulfillmentOrderRequest();
        public string StateCode { get; set; } = SimulationFulfillmentStateCodes.Preview;
        public bool IsFeasible { get; set; }
        public string[] ReasonCodes { get; set; } = Array.Empty<string>();
        public SimulationFulfillmentCenterCandidate[] CandidateCenters { get; set; } = Array.Empty<SimulationFulfillmentCenterCandidate>();
        public string[] SelectedCenterStableIds { get; set; } = Array.Empty<string>();
        public SimulationFulfillmentAllocation[] Allocations { get; set; } = Array.Empty<SimulationFulfillmentAllocation>();
    }

    public sealed class SimulationFulfillmentCommand
    {
        public string CommandId { get; set; } = string.Empty;
        public long ExpectedRevision { get; set; }
        public string ActionCode { get; set; } = string.Empty;
        public SimulationFulfillmentStateSnapshot? InitialState { get; set; }
        public SimulationFulfillmentOrderRequest? Order { get; set; }
        public string OrderStableId { get; set; } = string.Empty;
        public string AllocationStableId { get; set; } = string.Empty;
        public string LocationBarcode { get; set; } = string.Empty;
        public string ProductBarcode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }
}
