namespace HongdalApp.Models.Shipper;

public sealed class ShipperRequestItem
{
    public string 의뢰Id { get; set; } = string.Empty;
    public string 화물종류 { get; set; } = string.Empty;
    public string 화물적재형태 { get; set; } = string.Empty;
    public string 의뢰상태 { get; set; } = string.Empty;
    public string 결제상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
    public string 정산상태 { get; set; } = string.Empty;
    public string 운송방식 { get; set; } = string.Empty;
    public string 차량종류 { get; set; } = string.Empty;
    public string 결제수단 { get; set; } = string.Empty;
    public int? 결제예정금액 { get; set; }
    public decimal? 예상거리Km { get; set; }
    public decimal? 기준운임 { get; set; }
    public decimal? 기사지급예정운임 { get; set; }
    public int 알선단계 { get; set; } = 1;
    public bool 재알선금지 { get; set; } = true;
    public bool 정책위반 { get; set; }
    public bool 재알선의심 { get; set; }
    public IReadOnlyList<string> 정책경고목록 { get; set; } = [];
    public DateTime 생성일시 { get; set; }
    public string? 픽업지 { get; set; }
    public string? 하차지 { get; set; }

    public bool CanPay => ContainsAny(배차상태, "상차완료", "운송중", "하차지도착", "하차완료", "인수완료")
        && !IsPaymentSecured(결제상태);

    private static bool IsPaymentSecured(string? paymentStatus)
        => ContainsAny(paymentStatus, "결제완료", "결제확보", "입금확인", "승인완료");

    private static bool ContainsAny(string? value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
