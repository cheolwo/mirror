namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum 음식점식재료공급경로
{
    국내산지,
    공동수입
}

public enum 음식점식재료보관방식
{
    상온,
    냉장,
    냉동
}

public enum 음식점식재료공급요청상태
{
    초안,
    수요모으는중,
    공급조건검토,
    공급준비
}

public enum 음식점식재료공급메시지종류
{
    정보,
    성공,
    경고,
    오류
}

public sealed class 음식점식재료공급요청Draft
{
    public 음식점식재료공급경로 공급경로 { get; set; }
    public string 품목명 { get; set; } = string.Empty;
    public string 품목분류 { get; set; } = string.Empty;
    public string 규격 { get; set; } = string.Empty;
    public decimal 필요수량 { get; set; }
    public string 수량단위 { get; set; } = "kg";
    public string 납품주기 { get; set; } = "매주";
    public DateTime? 희망납품일 { get; set; }
    public decimal 현재구매단가 { get; set; }
    public decimal 희망도착단가 { get; set; }
    public string 통화코드 { get; set; } = "KRW";
    public string 희망원산지 { get; set; } = string.Empty;
    public string 납품지역 { get; set; } = string.Empty;
    public 음식점식재료보관방식 보관방식 { get; set; }
    public string 사용목적 { get; set; } = string.Empty;
    public string 추가조건 { get; set; } = string.Empty;
    public bool 공동수요집계동의 { get; set; } = true;
    public bool 원산지대체허용 { get; set; }
    public bool 산지Lot추적필수 { get; set; } = true;

    public 음식점식재료공급요청Draft 복사()
        => new()
        {
            공급경로 = 공급경로,
            품목명 = 품목명,
            품목분류 = 품목분류,
            규격 = 규격,
            필요수량 = 필요수량,
            수량단위 = 수량단위,
            납품주기 = 납품주기,
            희망납품일 = 희망납품일,
            현재구매단가 = 현재구매단가,
            희망도착단가 = 희망도착단가,
            통화코드 = 통화코드,
            희망원산지 = 희망원산지,
            납품지역 = 납품지역,
            보관방식 = 보관방식,
            사용목적 = 사용목적,
            추가조건 = 추가조건,
            공동수요집계동의 = 공동수요집계동의,
            원산지대체허용 = 원산지대체허용,
            산지Lot추적필수 = 산지Lot추적필수
        };
}

public sealed record 음식점식재료공급후보(
    string 후보Id,
    음식점식재료공급경로 공급경로,
    string 공급경로명,
    string 공급주체명,
    string 원산지Label,
    string 품목Label,
    decimal 최소공동수량,
    string 수량단위,
    int 현재참여음식점수,
    decimal 품목단가,
    decimal 물류작업단가,
    decimal 수입부대비용단가,
    decimal 예상도착단가,
    decimal 현재비교단가,
    string 통화코드,
    DateTime 예상납품일,
    string 보관조건Label,
    string 가격기준Label,
    string 제외비용Label,
    IReadOnlyList<string> 필요역할,
    bool 직접조건수락필수,
    bool 운영효력없음)
{
    public decimal 단위당예상절감액 => Math.Max(0m, 현재비교단가 - 예상도착단가);

    public decimal 예상절감률
        => 현재비교단가 <= 0
            ? 0
            : Math.Round(단위당예상절감액 / 현재비교단가 * 100m, 1);
}

public sealed record 음식점식재료공급요청Snapshot(
    string 요청Id,
    음식점식재료공급요청상태 상태,
    string 상태Label,
    음식점식재료공급요청Draft 요청,
    string? 선택후보Id,
    string? 선택후보Label,
    DateTimeOffset 생성시각,
    bool 운영효력없음);

public interface I음식점식재료공급요청Service
{
    bool SimulationMode { get; }

    Task<IReadOnlyList<음식점식재료공급후보>> 공급후보조회Async(
        음식점식재료공급요청Draft request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<음식점식재료공급요청Snapshot>> 요청목록조회Async(
        CancellationToken cancellationToken = default);

    Task<음식점식재료공급요청Snapshot> 초안저장Async(
        음식점식재료공급요청Draft request,
        string? selectedCandidateId,
        CancellationToken cancellationToken = default);
}
