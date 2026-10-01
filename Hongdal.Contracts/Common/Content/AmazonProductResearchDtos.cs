namespace Hongdal.Contracts.Common.Content;

public static class 외부상품참고자료검수상태코드
{
    public const string 대기 = "Pending";
}

public sealed class Amazon상품참고자료조회요청Dto
{
    public string 상품Url { get; set; } = string.Empty;
}

public sealed record 외부상품가격스냅샷Dto(
    decimal? 현재가격,
    decimal? 정가,
    decimal? 배송비,
    string? 통화코드);

public sealed record 외부상품속성Dto(
    string 항목명,
    string 값);

/// <summary>
/// 외부 상품 페이지를 Hongdal 상품으로 확정하지 않고 운영자 검수 근거로 보관하는 읽기 모델입니다.
/// 가격·재고·평점은 관측 시각과 국가에 종속된 스냅샷이며 주문이나 판매 상태를 만들지 않습니다.
/// </summary>
public sealed record Amazon상품참고자료Dto(
    string 참조키,
    string Asin,
    string 상품명,
    string? 브랜드명,
    string 원문Url,
    string 마켓플레이스국가코드,
    외부상품가격스냅샷Dto 가격,
    bool? 재고여부,
    string? 재고표시문구,
    decimal? 평점,
    int? 리뷰수,
    string? 카테고리경로,
    string? 썸네일Url,
    IReadOnlyList<string> 이미지Url목록,
    IReadOnlyList<string> 특징목록,
    IReadOnlyList<외부상품속성Dto> 속성목록,
    DateTime 관측일시Utc,
    string 검수상태,
    IReadOnlyDictionary<string, string> 원장외부참조,
    string 안내문);
