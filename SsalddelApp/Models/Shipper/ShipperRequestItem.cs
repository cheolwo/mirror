using Ssalddel.Contracts.Shipper.Request;

namespace SsalddelApp.Models.Shipper;

public sealed class ShipperRequestItem
{
    public string 의뢰Id { get; set; } = string.Empty;
    public string 화물종류 { get; set; } = string.Empty;
    public string 화물적재형태 { get; set; } = string.Empty;
    public string 의뢰상태 { get; set; } = string.Empty;
    public string 결제상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
    public string 운송상태 { get; set; } = string.Empty;
    public DateTime? 운송원장갱신일시Utc { get; set; }
    public string 확정기사Id { get; set; } = string.Empty;
    public string 확정기사명 { get; set; } = string.Empty;
    public string 확정기사차량 { get; set; } = string.Empty;
    public decimal? 기사최근위도 { get; set; }
    public decimal? 기사최근경도 { get; set; }
    public DateTime? 기사최근위치시각Utc { get; set; }
    public string 정산상태 { get; set; } = string.Empty;
    public string 운송방식 { get; set; } = string.Empty;
    public string 차량종류 { get; set; } = string.Empty;
    public string 결제수단 { get; set; } = string.Empty;
    public int? 결제예정금액 { get; set; }
    public decimal? 예상거리Km { get; set; }
    public decimal? 기준운임 { get; set; }
    public decimal? 기사지급예정운임 { get; set; }
    public string 정산시점 { get; set; } = string.Empty;
    public string 증빙방식 { get; set; } = string.Empty;
    public string 수납주체 { get; set; } = string.Empty;
    public bool 세금계산서필요 { get; set; }
    public bool 현금영수증필요 { get; set; }
    public string 정산메모 { get; set; } = string.Empty;
    public string 인수증번호 { get; set; } = string.Empty;
    public DateTime? 인수증등록일시 { get; set; }
    public DateTime? 현장수금확인일시 { get; set; }
    public string 현장지급메모 { get; set; } = string.Empty;
    public int? 화물길이Mm { get; set; }
    public int? 화물폭Mm { get; set; }
    public int? 화물높이Mm { get; set; }
    public int? 팔레트개수 { get; set; }
    public int 알선단계 { get; set; } = 1;
    public bool 재알선금지 { get; set; } = true;
    public bool 정책위반 { get; set; }
    public bool 재알선의심 { get; set; }
    public IReadOnlyList<string> 정책경고목록 { get; set; } = [];
    public DateTime 생성일시 { get; set; }
    public string? 픽업지 { get; set; }
    public string? 하차지 { get; set; }
    public LocationContactDTO? 픽업정보 { get; set; }
    public LocationContactDTO? 하차정보 { get; set; }

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
