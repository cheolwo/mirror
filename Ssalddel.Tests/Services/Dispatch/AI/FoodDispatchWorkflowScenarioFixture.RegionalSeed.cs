namespace Ssalddel.Tests.Services.Dispatch.AI;

/// <summary>공개 매장 관측과 합성 주문을 잇는 로컬 시험 입력. 과거 주문을 새 운영 주문으로 복제하지 않는다.</summary>
public sealed record FoodDispatchRegionalSeed(
    string SourceRecordId, string PublicStoreName, string PublicPickupAddress,
    decimal PickupLatitude, decimal PickupLongitude,
    decimal SyntheticDropoffLatitude, decimal SyntheticDropoffLongitude,
    string HistoricalMenuName, decimal SyntheticUnitPriceKrw,
    bool CustomerDestinationIsSynthetic)
{
    public void Validate()
    {
        if (!CustomerDestinationIsSynthetic || string.IsNullOrWhiteSpace(SourceRecordId)
            || string.IsNullOrWhiteSpace(PublicStoreName) || string.IsNullOrWhiteSpace(PublicPickupAddress)
            || string.IsNullOrWhiteSpace(HistoricalMenuName) || SyntheticUnitPriceKrw <= 0
            || PickupLatitude is < 33 or > 39 || SyntheticDropoffLatitude is < 33 or > 39
            || PickupLongitude is < 124 or > 132 || SyntheticDropoffLongitude is < 124 or > 132)
            throw new ArgumentException("Only public-store observations with explicitly synthetic customer destinations are accepted.");
    }
}
