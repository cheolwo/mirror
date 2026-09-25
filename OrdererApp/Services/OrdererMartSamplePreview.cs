using Ssalddel.Contracts.Mart;

namespace OrdererApp.Services;

/// <summary>
/// 주문·재고 권위와 분리된 주문자 앱 화면 검토용 자료입니다.
/// 명시적인 preview=sample 요청에서만 사용합니다.
/// </summary>
public static class OrdererMartSamplePreview
{
    private static readonly DateTime 기준시각Utc = new(2026, 9, 22, 1, 0, 0, DateTimeKind.Utc);

    public static 마트공개상품목록응답 Create() => new()
    {
        Items =
        [
            Product(91001, "당일 수확 감자", "농산", "포슬포슬한 식감의 산지 감자", "2kg 한 봉", 7_900m, 26, true),
            Product(91002, "햇양파 묶음", "농산", "요리에 쓰기 좋은 단단한 햇양파", "1.5kg 한 망", 5_400m, 18, true),
            Product(91003, "무항생제 달걀", "신선", "가까운 농장에서 들여온 신선 달걀", "10구 한 팩", 6_800m, 9, true),
            Product(91004, "동네 방앗간 현미", "곡물", "고소한 향을 살린 소포장 현미", "2kg 한 포", 11_500m, 14, true),
            Product(91005, "저온 압착 들기름", "가공", "향을 살려 천천히 짜낸 들기름", "180ml 한 병", 15_900m, 4, true),
            Product(91006, "제철 풋고추", "농산", "이번 주 입고분이 모두 판매되었습니다", "300g 한 봉", 3_900m, 0, false)
        ],
        TotalCount = 6,
        Page = 1,
        PageSize = 12,
        재고기준안내 = "화면 검토용 샘플 재고입니다. 실제 주문·가격·재고가 아닙니다."
    };

    private static 마트공개상품요약응답 Product(
        long id,
        string name,
        string category,
        string description,
        string unit,
        decimal price,
        int availableQuantity,
        bool available) => new()
        {
            Id = id,
            상품명 = name,
            카테고리 = category,
            짧은설명 = description,
            판매단위 = unit,
            판매가 = price,
            판매가능수량 = availableQuantity,
            판매가능여부 = available,
            재고기준시각Utc = 기준시각Utc,
            수정일시Utc = 기준시각Utc
        };
}
