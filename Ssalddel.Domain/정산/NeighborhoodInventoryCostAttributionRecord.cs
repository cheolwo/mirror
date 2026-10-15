namespace 살뜰.도메인.정산;

/// <summary>
/// 이미 발생한 입고·보관 비용의 주문별 귀속 근거입니다. 신규 지급 채무나 재고 상태를 만들지 않습니다.
/// 수량·금액·원천 판본은 보호된 DetailsJson에 동결하며 저장 이후 덮어쓰지 않습니다.
/// </summary>
public sealed class NeighborhoodInventoryCostAttributionRecord
{
    public string AttributionId { get; set; } = "";
    public string SourceEntryId { get; set; } = "";
    public string SourceAgreementId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string AgreementId { get; set; } = "";
    public string CalculationDigest { get; set; } = "";
    public string DetailsJson { get; set; } = "{}";
    public DateTime RecordedAtUtc { get; set; }
}
