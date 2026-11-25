namespace Ssalddel.Contracts.Common.Inventory;

public static class 출고예정검토조회상태코드
{
    public const string 검토대기 = "검토 대기";
    public const string 운송연결 = "운송 연결";
    public const string 전체 = "전체";

    public static IReadOnlyList<string> 전체목록 { get; } = [검토대기, 운송연결, 전체];

    public static string Normalize(string? value)
        => 전체목록.Contains(value?.Trim(), StringComparer.Ordinal) ? value!.Trim() : 검토대기;
}

public static class 출고예정검토항목상태코드
{
    public const string 확인완료 = "확인 완료";
    public const string 입력필요 = "입력 필요";
    public const string 차단 = "차단";
}

public sealed class 출고예정검토목록조회요청
{
    public string? Search { get; set; }
    public string Status { get; set; } = 출고예정검토조회상태코드.검토대기;
    public long? WarehouseId { get; set; }
    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public string? PeriodStage { get; set; }
    public int BatchPage { get; set; }
    public int BatchPageSize { get; set; } = 6;
    public string? BatchKey { get; set; }
    public int MemberPage { get; set; }
    public int MemberPageSize { get; set; } = 12;
    public int Page { get; set; }
    public int PageSize { get; set; } = 12;
}

public sealed class 출고예정검토목록항목응답
{
    public long OutboundPlanId { get; set; }
    public long? InboundItemId { get; set; }
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string OrderReference { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string OutboundStatus { get; set; } = string.Empty;
    public string? TransportRequestId { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class 출고예정검토목록페이지응답
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OutboundPeriodResponse? Period { get; set; }
    public IReadOnlyList<출고예정검토목록항목응답> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } = 12;
    public bool HasNextPage => (Page + 1) * PageSize < TotalCount;
}

public sealed class 출고예정검토항목응답
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class 출고예정검토상세응답
{
    public long OutboundPlanId { get; set; }
    public long? InboundItemId { get; set; }
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public bool PickupAddressConfigured { get; set; }
    public bool WarehouseActive { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string OrderReference { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string OutboundStatus { get; set; } = string.Empty;
    public string? TransportRequestId { get; set; }
    public string TransportRequestStatus { get; set; } = string.Empty;
    public string DispatchStatus { get; set; } = string.Empty;
    public string TransportStatus { get; set; } = string.Empty;
    public string HandoffStatus { get; set; } = string.Empty;
    public string? AssignedDriverId { get; set; }
    public string RequestedVehicleType { get; set; } = string.Empty;
    public string AssignedDriverVehicle { get; set; } = string.Empty;
    public bool DriverAccepted { get; set; }
    public bool VehicleConfirmed { get; set; }
    public bool CanCompleteHandoff { get; set; }
    public DateTime? HandoffCompletedAtUtc { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public string DestinationAddressDetail { get; set; } = string.Empty;
    public int? AvailableQuantity { get; set; }
    public int? ReservedQuantity { get; set; }
    public int? DefectiveQuantity { get; set; }
    public string InventoryStatus { get; set; } = string.Empty;
    public string StorageLocation { get; set; } = string.Empty;
    public string StorageCondition { get; set; } = string.Empty;
    public string PackagingType { get; set; } = string.Empty;
    public DateTime? PackedAtUtc { get; set; }
    public DateTime HandoffReadyAtUtc { get; set; }
    public IReadOnlyList<출고예정검토항목응답> Checks { get; set; } = [];
    public bool CanStartTransportRequestDraft { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string NextStep { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class 출고운송인계완료요청
{
    public bool DriverIdentityConfirmed { get; set; }
    public bool VehicleConfirmed { get; set; }
    public bool CargoReleasedConfirmed { get; set; }
    public string? Memo { get; set; }
}

public sealed class 출고운송인계완료응답
{
    public long OutboundPlanId { get; set; }
    public string TransportRequestId { get; set; } = string.Empty;
    public string OutboundStatus { get; set; } = string.Empty;
    public string AssignedDriverId { get; set; } = string.Empty;
    public string AssignedDriverVehicle { get; set; } = string.Empty;
    public DateTime HandoffCompletedAtUtc { get; set; }
    public bool IdempotentReplay { get; set; }
}
