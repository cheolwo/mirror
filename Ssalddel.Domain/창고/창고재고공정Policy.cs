namespace 살뜰.도메인.창고;

/// <summary>기존 창고 API와 작업별 API가 같은 재고 상태·수량 관문을 사용합니다.</summary>
public static class 창고재고공정Policy
{
    public static string? 적재차단사유(string? inventoryState)
        => inventoryState?.StartsWith("검수완료", StringComparison.Ordinal) == true
            ? null
            : "검수 완료 상태의 재고만 적재할 수 있습니다.";

    public static string? 포장차단사유(string? inventoryState, int requestedQuantity, int availableQuantity)
    {
        if (inventoryState != "적재완료")
            return "적재 완료 상태의 재고만 포장할 수 있습니다.";
        if (requestedQuantity <= 0 || requestedQuantity != availableQuantity)
            return "부분 포장 상태를 만들지 않도록 현재 전체 가용수량과 같은 수량을 확인해 주세요.";
        return null;
    }
}
