namespace Ssalddel.Contracts.Common.Community;

public static class 공간공동운영주문Routes
{
    public const string Api = 공간공동운영Routes.Api + "/{ledgerId}/orders";
}

/// <summary>주소·결제정보는 기존 화물 의뢰에서 수집합니다. 같은 조건에 주문자와 판매자가 각각 동의합니다.</summary>
public sealed class 공간공동운영주문조건Dto
{
    public Guid OrderRequestId { get; set; }
    public long PublicProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceKrw { get; set; }
    public string ReceiptMethodCode { get; set; } = "delivery";
    public DateTime DeadlineUtc { get; set; }
    public string CycleId { get; set; } = "";
    public string SiteId { get; set; } = "";
    public string LotId { get; set; } = "";
    public string WaveId { get; set; } = "";
    public string CargoRequestId { get; set; } = "";
}

public sealed class 공간공동운영주문명령Request
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public int ExpectedTermsVersion { get; set; }
    public string Action { get; set; } = "propose";
    public bool ConfirmTerms { get; set; }
    public 공간공동운영주문조건Dto? Terms { get; set; }
}

public sealed class 공간공동운영주문Response
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public Guid OrderRequestId { get; set; }
    public int TermsVersion { get; set; }
    public 공간공동운영주문조건Dto Terms { get; set; } = new();
    public string ProductName { get; set; } = "";
    public decimal TotalKrw { get; set; }
    public bool BuyerConsented { get; set; }
    public bool SellerConsented { get; set; }
    public string StatusCode { get; set; } = "proposed";
    public string RootOrderLedgerId { get; set; } = "";
    public string? DistributedOrderId { get; set; }
    public string? OrderLedgerConnectionStatusCode { get; set; }
    public string? HoldCode { get; set; }
    public bool Replayed { get; set; }
    /// <summary>동일 원요청의 전문 재고 예약 생성이 확인됐다는 사실이며 현재 남은 수량·전달·수령 완료가 아닙니다.</summary>
    public bool InventoryReserved { get; set; }
    public bool PaymentConfirmed { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
}

public sealed class 공간공동운영주문목록Response
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public IReadOnlyList<공간공동운영주문Response> Items { get; set; } = [];
}

public sealed class 공간공동운영주문후보Dto
{
    public string Label { get; set; } = "";
    public string ProductName { get; set; } = "";
    public 공간공동운영주문조건Dto Terms { get; set; } = new();
}
public sealed class 공간공동운영주문후보목록Response
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public IReadOnlyList<공간공동운영주문후보Dto> Items { get; set; } = [];
}
