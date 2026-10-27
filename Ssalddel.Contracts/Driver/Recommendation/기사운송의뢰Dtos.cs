using Request = Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Contracts.Driver.Recommendation;

public sealed class 기사운송의뢰상세응답
{
    public string 의뢰Id { get; set; } = string.Empty;
    public string 화주Id { get; set; } = string.Empty;
    public string 화물종류 { get; set; } = string.Empty;
    /// <summary>원의뢰에 입력된 운송 방식입니다. 표시를 위한 독차 명칭과 별개로 원문을 보존합니다.</summary>
    public string? 운송방식 { get; set; }
    public string? 차량종류 { get; set; }
    public decimal? 화물중량Kg { get; set; }
    public int? 화물수량 { get; set; }
    /// <summary>원의뢰의 UTC 상차 시간창입니다. null은 시간 미확인입니다.</summary>
    public DateTime? 상차시간창시작일시 { get; set; }
    public DateTime? 상차시간창종료일시 { get; set; }
    /// <summary>원의뢰의 UTC 하차 시간창입니다. null은 시간 미확인입니다.</summary>
    public DateTime? 하차시간창시작일시 { get; set; }
    public DateTime? 하차시간창종료일시 { get; set; }
    public string 화물설명 { get; set; } = string.Empty;
    public string 픽업지 { get; set; } = string.Empty;
    public string 픽업상세지 { get; set; } = string.Empty;
    public decimal? 픽업위도 { get; set; }
    public decimal? 픽업경도 { get; set; }
    public string 하차지 { get; set; } = string.Empty;
    public string 하차상세지 { get; set; } = string.Empty;
    public decimal? 하차위도 { get; set; }
    public decimal? 하차경도 { get; set; }
    public string 결제상태 { get; set; } = string.Empty;
    public string 정산상태 { get; set; } = string.Empty;
    public string 의뢰상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
    public string 결제수단 { get; set; } = string.Empty;
    public int? 결제예정금액 { get; set; }
    public Request.정산시점? 정산시점 { get; set; }
    public Request.증빙방식? 증빙방식 { get; set; }
    public Request.수납주체? 수납주체 { get; set; }
    public bool 세금계산서필요 { get; set; }
    public bool 현금영수증필요 { get; set; }
    public string? 정산메모 { get; set; }
    public int? 화물길이Mm { get; set; }
    public int? 화물폭Mm { get; set; }
    public int? 화물높이Mm { get; set; }
    public int? 화물팔레트개수 { get; set; }
    public bool 차량적합여부 { get; set; }
    public string[] 부적합사유 { get; set; } = Array.Empty<string>();
    public string[] 경고 { get; set; } = Array.Empty<string>();
    public string? 배차대기상태 { get; set; }
    public DateTime 생성일시 { get; set; }
    public DateTime 수정일시 { get; set; }
}