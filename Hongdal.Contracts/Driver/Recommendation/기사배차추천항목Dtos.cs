namespace Hongdal.Contracts.Driver.Recommendation;

/// <summary>
/// 기사 추천·검색·전국콜·공개배차 API가 공통으로 반환하는 항목입니다.
/// </summary>
public sealed class 기사배차추천항목응답
{
    public string 의뢰Id { get; set; } = string.Empty;
    public string 화물종류 { get; set; } = string.Empty;
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
    public DateTime? 추천시작시각 { get; set; }
    public DateTime? 추천만료시각 { get; set; }
    public string 상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
}
