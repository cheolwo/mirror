using Ssalddel.Contracts.Driver.Food;

namespace Ssalddel.Contracts.Mart;

/// <summary>마트 배송 전용 계약. 음식주문·음식 정산 상태로 대체하지 않습니다.</summary>
public sealed class 마트기사업무목록Dto
{
    public bool Enabled { get; set; }
    public string Notice { get; set; } = string.Empty;
    public IReadOnlyList<마트기사업무항목Dto> Items { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class 마트기사업무항목Dto
{
    public string OfferId { get; set; } = string.Empty;
    public string OrderReference { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string AttemptStableId { get; set; } = string.Empty;
    public int RecommendationRound { get; set; }
    public DateTime QueueUpdatedAtUtc { get; set; }
    public FoodDeliveryDriverStopDto Pickup { get; set; } = new();
    public FoodDeliveryDriverStopDto Dropoff { get; set; } = new();
    public decimal? DriverPayout { get; set; }
    public long? LabelRevision { get; set; }
    public bool CanAccept { get; set; }
    public bool CanReject { get; set; }
    public bool CanPickup { get; set; }
    public bool CanDeliver { get; set; }
    public bool IsReplay { get; set; }
}

public sealed class 마트기사수락요청
{
    public Guid ClientRequestId { get; set; }
    public int ExpectedRecommendationRound { get; set; }
    public DateTime ExpectedQueueUpdatedAtUtc { get; set; }
}

public sealed class 마트기사거절요청
{
    public Guid ClientRequestId { get; set; }
    public int ExpectedRecommendationRound { get; set; }
    public DateTime ExpectedQueueUpdatedAtUtc { get; set; }
    public string? ReasonCode { get; set; }
}

public sealed class 마트기사픽업요청
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public long ExpectedLabelRevision { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool ByNumber { get; set; }
}

public sealed class 마트기사전달요청
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
}

public sealed class 마트포장전표발급요청
{
    public Guid ClientRequestId { get; set; }
}

/// <summary>현재 관련 창고 담당자에게만 반환하는 전표입니다. 기사 목록·로그에는 코드를 포함하지 않습니다.</summary>
public sealed class 마트포장전표Dto
{
    public string OrderReference { get; set; } = string.Empty;
    public string LabelId { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string ConfirmationCode { get; set; } = string.Empty;
    public long Revision { get; set; }
    public bool Active { get; set; }
    public DateTime IssuedAtUtc { get; set; }
}
