namespace Ssalddel.Contracts.Common.Warehouse;

/// <summary>배송·배차와 독립적인 한 공간의 인증 재고 조회 결과. 전체 합계가 아닌 한 페이지입니다.</summary>
public sealed record 분산공간재고PageDto(
    분산공간재고SiteDto Site,
    string RoleKey,
    int Skip,
    int Take,
    bool HasMore,
    IReadOnlyList<분산공간재고ItemDto> Items)
{
    public bool IsSimulation { get; init; } = true;
    public string Profile { get; init; } = DistributedFulfillmentCodes.Profile;
}

public sealed record 분산공간재고SiteDto(string SiteId, string PublicLabel, string StatusCode, long Revision);

/// <summary>예정은 실재고가 아닙니다. 수량은 해당 Lot에 연결된 기존 입고상품에서 읽습니다.</summary>
public sealed record 분산공간재고ItemDto(
    string LotId,
    long Revision,
    string PlanId,
    string LocationId,
    string LocationLabel,
    string Sku,
    string LotCode,
    string ProductLabel,
    int ExpectedQuantity,
    int ReceivedQuantity,
    int RemainingExpectedQuantity,
    int AvailableQuantity,
    int ReservedQuantity,
    int DefectiveQuantity);
