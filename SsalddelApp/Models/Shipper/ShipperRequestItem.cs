using Ssalddel.Contracts.Shipper.Request;

namespace SsalddelApp.Models.Shipper;

public sealed class ShipperRequestItem
{
    public string 의뢰Id { get; set; } = string.Empty;
    public Guid? 신청개인정보동의증적Id { get; set; }
    public string 신청출처Code { get; set; } = string.Empty;
    public string 화물종류 { get; set; } = string.Empty;
    public CargoDTO? 화물원본 { get; set; }
    public CargoDTO? 원본화물 { get; set; }
    public 화물적재입력결과? 적재조건 { get; set; }
    public string? 원본화물종류 { get; set; }
    public string 화물적재형태 { get; set; } = string.Empty;
    public string 의뢰상태 { get; set; } = string.Empty;
    public string 결제상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
    public string 운송상태 { get; set; } = string.Empty;
    public bool 비정상운송검토보류중 { get; set; }
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
    public string? 서비스레벨 { get; set; }
    public string? 요청사항 { get; set; }
    public decimal? 대기료 { get; set; }
    public decimal? 수작업비 { get; set; }
    public decimal? 할증 { get; set; }
    public string? 알선소Id { get; set; }
    public string? 절차메모 { get; set; }
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

    public bool CanPay => Ssalddel.Ui.Common.Areas.App.ViewModels.ShipperRequestDetailPresentation.CanPay(
        배차상태, 결제상태, 정산시점, 의뢰상태);

    internal static CargoDTO? 화물복사(CargoDTO? source) => source is null ? null : new()
    {
        화물종류 = source.화물종류, 설명 = source.설명, 수량 = source.수량,
        길이Mm = source.길이Mm, 폭Mm = source.폭Mm, 높이Mm = source.높이Mm,
        중량Kg = source.중량Kg, 부피Cbm = source.부피Cbm, 팔레트개수 = source.팔레트개수,
        화물파손주의여부 = source.화물파손주의여부, 온도조건 = source.온도조건,
        바닥회전가능여부 = source.바닥회전가능여부, 적층가능여부 = source.적층가능여부,
        부피입력초기화 = source.부피입력초기화
    };

    internal static 결제수단 신규결제수단확인(string? value)
        => value switch
        {
            null or "" => Ssalddel.Contracts.Shipper.Request.결제수단.카드,
            "카드" => Ssalddel.Contracts.Shipper.Request.결제수단.카드,
            "가상계좌" => Ssalddel.Contracts.Shipper.Request.결제수단.가상계좌,
            "계좌이체" => Ssalddel.Contracts.Shipper.Request.결제수단.계좌이체,
            _ => throw new InvalidOperationException("선택한 결제 방식의 정산 시점과 수납 주체를 확인해야 합니다. 인수증·현금성 정산은 계약 조건을 확인한 뒤 접수해 주세요.")
        };

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
