namespace Ssalddel.Ui.Common.Areas.App.Models;

public sealed class 운송모델작성Draft
{
    public string 작성출처 { get; set; } = string.Empty;
    public string 화물종류 { get; set; } = string.Empty;
    public string? 화물설명 { get; set; }
    public string 화물적재형태 { get; set; } = "일반 화물(박스/팔레트)";
    public int? 화물수량 { get; set; }
    public decimal? 화물중량Kg { get; set; }
    public decimal? 화물부피Cbm { get; set; }
    public string? 온도조건 { get; set; }
    public string 운송방식 { get; set; } = "혼적";
    public string 픽업도로명주소 { get; set; } = string.Empty;
    public string? 픽업상세주소 { get; set; }
    public string 픽업연락처이름 { get; set; } = string.Empty;
    public string 픽업연락처전화번호 { get; set; } = string.Empty;
    // 작성 화면의 한국시간(UTC+9). API adapter에서 UTC로 변환합니다.
    public DateTime? 픽업시간창시작일시 { get; set; }
    public DateTime? 픽업시간창종료일시 { get; set; }
    public string 하차도로명주소 { get; set; } = string.Empty;
    public string? 하차상세주소 { get; set; }
    public string 하차연락처이름 { get; set; } = string.Empty;
    public string 하차연락처전화번호 { get; set; } = string.Empty;
    public DateTime? 하차시간창시작일시 { get; set; }
    public DateTime? 하차시간창종료일시 { get; set; }
    public string? 서비스레벨 { get; set; }
    public string? 요청사항 { get; set; }
    public string? 차량종류 { get; set; }
    public decimal? 예상거리Km { get; set; }
    public string 결제수단 { get; set; } = "카드";
    public int? 결제예정금액 { get; set; }
    public decimal? 기준운임 { get; set; }
    public int? 기사지급예정운임 { get; set; }
    public decimal? 대기료 { get; set; }
    public decimal? 수작업비 { get; set; }
    public decimal? 할증 { get; set; }
    public int 알선단계 { get; set; } = 1;
    public bool 재알선금지 { get; set; } = true;
    public string? 알선소Id { get; set; }
    public string? 절차메모 { get; set; }
    public IReadOnlyList<string> 정책경고목록 { get; set; } = [];
    public DateTime 작성일시 { get; set; } = DateTime.Now;

    public decimal 부가비용합계 => (대기료 ?? 0) + (수작업비 ?? 0) + (할증 ?? 0);
    public bool 정책위반 => 정책경고목록.Count > 0;
    public bool 재알선의심 => 알선단계 > 1 || !string.IsNullOrWhiteSpace(알선소Id);
}
