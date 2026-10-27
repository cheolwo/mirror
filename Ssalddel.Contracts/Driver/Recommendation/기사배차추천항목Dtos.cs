namespace Ssalddel.Contracts.Driver.Recommendation;

/// <summary>
/// 기사 추천·검색·전국콜·공개배차 API가 공통으로 반환하는 항목입니다.
/// </summary>
public sealed class 기사배차추천항목응답
{
    public string? WorkKind { get; set; }
    public IReadOnlyList<string> AssignedActivities { get; set; } = [];
    public Ssalddel.Contracts.Common.Drivers.DriverOfferPricingDto? Pricing { get; set; }
    /// <summary>원의뢰의 확인된 기사 약정액입니다. 고객 청구액·예상 수익·입금 완료 금액과 구분합니다.</summary>
    public decimal? 기사지급예정운임 { get; set; }
    /// <summary>지급 예정액의 원장 근거입니다. 구버전 응답 또는 미확인 값은 null입니다.</summary>
    public string? 기사지급예정운임근거 { get; set; }
    public string 의뢰Id { get; set; } = string.Empty;
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
    public string 운송의뢰유형코드 { get; set; } = "GeneralCargoTransport";
    public string 운송의뢰유형표시 { get; set; } = "일반 화물";
    public bool 공동주문운송여부 { get; set; }
    public bool 세대배송포함여부 { get; set; }
    public int? 세대배송건수 { get; set; }
    public string 세대배송업무표시 { get; set; } = "상하차";
    public string 픽업지 { get; set; } = string.Empty;
    public string 하차지 { get; set; } = string.Empty;
    public decimal? 픽업_위도 { get; set; }
    public decimal? 픽업_경도 { get; set; }
    public decimal? 하차_위도 { get; set; }
    public decimal? 하차_경도 { get; set; }
    public decimal? 직선거리Km { get; set; }
    public decimal? 주행거리Km { get; set; }
    public decimal? 픽업거리Km { get; set; }
    public decimal? 공차거리Km { get; set; }
    public decimal? 운송거리Km { get; set; }
    public decimal? 복귀예상거리Km { get; set; }
    public decimal? 지금바로복귀거리Km { get; set; }
    public decimal? 복귀우회증가거리Km { get; set; }
    public decimal? 총공차거리Km { get; set; }
    public decimal? 예상톨비 { get; set; }
    public decimal? 예상연료비 { get; set; }
    public decimal? 예상총비용 { get; set; }
    public decimal? 예상수익 { get; set; }
    public decimal? 예상추가순이익 { get; set; }
    public decimal? 분당추가수익 { get; set; }
    public string 추천유형 { get; set; } = string.Empty;
    public decimal? 추가예상시간분 { get; set; }
    public decimal? 기존배송지연분 { get; set; }
    public decimal? 기존경로거리Km { get; set; }
    public decimal? 삽입경로거리Km { get; set; }
    public decimal? 기존경로소요시간분 { get; set; }
    public decimal? 삽입경로소요시간분 { get; set; }
    public decimal? 삽입추가톨비 { get; set; }
    public decimal? 추천점수 { get; set; }
    public string 추천사유 { get; set; } = string.Empty;
    public bool 일정삽입가능여부 { get; set; }
    public bool 전체일정완수가능여부 { get; set; }
    public int? 최적삽입인덱스 { get; set; }
    public bool 경로변경이점여부 { get; set; }
    public decimal? 경로변경절감분 { get; set; }
    public string[] 권장경로순서 { get; set; } = [];
    public decimal? 최대시간위반분 { get; set; }
    public string[] 일정위반사유 { get; set; } = [];
    public bool 복귀지기준추천여부 { get; set; }
    public string? 복귀지출처 { get; set; }
    public string? 복귀추천사유 { get; set; }
    public string[] 배지 { get; set; } = [];
    public string[] 경고 { get; set; } = [];
    public bool 차량적합여부 { get; set; } = true;
    public string[] 차량부적합사유 { get; set; } = [];
    public string[] 차량경고 { get; set; } = [];
    private DateTime? _추천시작시각;
    private DateTime? _추천만료시각;
    /// <summary>시간대가 생략된 구버전 서버 응답도 추천 원장의 UTC로 읽습니다.</summary>
    public DateTime? 추천시작시각
    {
        get => _추천시작시각;
        set => _추천시작시각 = 기사추천시각Policy.Utc로정규화(value);
    }
    public DateTime? 추천만료시각
    {
        get => _추천만료시각;
        set => _추천만료시각 = 기사추천시각Policy.Utc로정규화(value);
    }
    public int 추천라운드 { get; set; }
    public string 상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
}
