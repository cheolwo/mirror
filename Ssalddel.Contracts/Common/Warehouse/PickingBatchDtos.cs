namespace Ssalddel.Contracts.Common.Warehouse;

public enum 피킹포장처리방식
{
    피킹만 = 0,
    피킹포장통합 = 1,
    피킹포장분리 = 2
}

public sealed class 피킹배치계획요청
{
    public string 출고참조번호 { get; set; } = string.Empty;

    public long? 대상창고Id { get; set; }

    public IReadOnlyList<피킹배치출고라인> 출고라인목록 { get; set; } = [];

    public IReadOnlyList<피킹포장작업자후보> 작업자후보목록 { get; set; } = [];

    public IReadOnlyList<피킹배치창고옵션> 창고옵션목록 { get; set; } = [];

    public 피킹포장처리방식 기본처리방식 { get; set; } = 피킹포장처리방식.피킹포장분리;

    public int 작업자별권장최대피킹수량 { get; set; } = 40;

    public int 작업자별권장최대포장수량 { get; set; } = 40;

    public int 작업자별권장최대작업수 { get; set; } = 8;
}

public sealed class 피킹배치창고옵션
{
    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public bool 살뜰마트도심창고여부 { get; set; }

    public 피킹포장처리방식 기본처리방식 { get; set; } = 피킹포장처리방식.피킹포장분리;

    public bool 상품바코드검증필수 { get; set; } = true;

    public string 운영메모 { get; set; } = string.Empty;

    public int? 작업자별권장최대피킹수량 { get; set; }

    public int? 작업자별권장최대포장수량 { get; set; }

    public int? 작업자별권장최대작업수 { get; set; }
}

public sealed class 피킹배치출고라인
{
    public long? 출고예정Id { get; set; }

    public string 출고작업Key { get; set; } = string.Empty;

    public string LineKey { get; set; } = string.Empty;

    public long InboundProductId { get; set; }

    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? 상품바코드 { get; set; }

    public string? 적재상품바코드 { get; set; }

    public int Quantity { get; set; }

    public string? 적재대코드 { get; set; }

    public string? 보관위치코드 { get; set; }

    public string? LotNo { get; set; }

    public 피킹포장처리방식? 처리방식 { get; set; }
}

public sealed class 피킹포장작업자후보
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? 휴대폰뒤8자리 { get; set; }

    public string? 작업자묶음바코드 { get; set; }

    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public IReadOnlyList<string> 가능작업유형코드목록 { get; set; } = [];

    public string? 담당구역코드 { get; set; }

    public bool IsAvailable { get; set; } = true;

    public int 진행중작업수 { get; set; }

    public int 기배정수량 { get; set; }
}

public sealed class 피킹배치계획결과
{
    public bool IsComplete { get; set; }

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<피킹작업배정> 피킹작업목록 { get; set; } = [];

    public IReadOnlyList<포장작업배정> 포장작업목록 { get; set; } = [];

    public IReadOnlyList<피킹작업묶음> 피킹묶음목록 { get; set; } = [];

    public IReadOnlyList<피킹배치미배정라인> 미배정라인목록 { get; set; } = [];

    public IReadOnlyList<피킹포장작업자부하> 작업자부하목록 { get; set; } = [];
}

public sealed class 피킹작업배정
{
    public long? 출고예정Id { get; set; }

    public string TaskKey { get; set; } = string.Empty;

    public string WorkerUserId { get; set; } = string.Empty;

    public string WorkerName { get; set; } = string.Empty;

    public string WorkerBundleBarcode { get; set; } = string.Empty;

    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string LineKey { get; set; } = string.Empty;

    public long InboundProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? 상품바코드 { get; set; }

    public string? 적재상품바코드 { get; set; }

    public bool IsBarcodeMatched { get; set; }

    public int Quantity { get; set; }

    public string? 적재대코드 { get; set; }

    public string? 보관위치코드 { get; set; }

    public string? LotNo { get; set; }

    public 피킹포장처리방식 처리방식 { get; set; }

    public string? 포장작업Key { get; set; }

    public string NextStep { get; set; } = string.Empty;

    public string AssignmentReason { get; set; } = string.Empty;
}

public sealed class 포장작업배정
{
    public long? 출고예정Id { get; set; }

    public string TaskKey { get; set; } = string.Empty;

    public string PickerUserId { get; set; } = string.Empty;

    public string PackerUserId { get; set; } = string.Empty;

    public string PackerName { get; set; } = string.Empty;

    public string PickerBundleBarcode { get; set; } = string.Empty;

    public string PackerBundleBarcode { get; set; } = string.Empty;

    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string LineKey { get; set; } = string.Empty;

    public long InboundProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? 상품바코드 { get; set; }

    public string? 적재상품바코드 { get; set; }

    public int Quantity { get; set; }

    public string AssignmentReason { get; set; } = string.Empty;
}

public sealed class 피킹작업묶음
{
    public string WorkerUserId { get; set; } = string.Empty;

    public string WorkerName { get; set; } = string.Empty;

    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string WorkerBundleBarcode { get; set; } = string.Empty;

    public int TotalTaskCount { get; set; }

    public int TotalQuantity { get; set; }
}

public sealed class 피킹배치미배정라인
{
    public string LineKey { get; set; } = string.Empty;

    public long InboundProductId { get; set; }

    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? 상품바코드 { get; set; }

    public string? 적재상품바코드 { get; set; }

    public int Quantity { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

public sealed class 피킹포장작업자부하
{
    public string WorkerUserId { get; set; } = string.Empty;

    public string WorkerName { get; set; } = string.Empty;

    public long WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public int TotalTaskCount { get; set; }

    public int TotalQuantity { get; set; }

    public bool IsOverloaded { get; set; }
}
