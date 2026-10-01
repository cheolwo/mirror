using Hongdal.Contracts.Common.PublicData;
using Hongdal.Contracts.Common.Customs;

namespace 홍달.Services.External.PublicData;

public interface IRoadAddressLookupService
{
    Task<PublicDataLookupResponse<RoadAddressItem>> SearchAsync(
        RoadAddressSearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IApartmentComplexLookupService
{
    Task<PublicDataLookupResponse<ApartmentComplexItem>> SearchAsync(
        ApartmentComplexSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<PublicDataLookupResponse<ApartmentComplexBasicItem>> GetBasicInfoAsync(
        ApartmentComplexBasicRequest request,
        CancellationToken cancellationToken = default);
}

public interface IApartmentManagementFeeLookupService
{
    Task<PublicDataLookupResponse<ApartmentManagementFeeSnapshotItem>> GetSnapshotAsync(
        ApartmentManagementFeeSnapshotRequest request,
        CancellationToken cancellationToken = default);

    Task<ApartmentGroupCommerceOffsetSimulationResult> SimulateGroupCommerceOffsetAsync(
        ApartmentGroupCommerceOffsetSimulationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IHsCountryTradeUnitPriceLookupService
{
    Task<HsCountryImportUnitPriceSimulationResult> SimulateImportUnitPriceAsync(
        HsCountryMonthlyTradeUnitPriceRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAtDomesticFoodPriceLookupService
{
    Task<AtDomesticFoodPriceLookupResult> LookupAsync(
        AtDomesticFoodPriceRequest request,
        CancellationToken cancellationToken = default);
}

public interface IFoodPriceComparisonService
{
    Task<FoodPriceComparisonResponse> CompareAsync(
        FoodPriceComparisonRequest request,
        CancellationToken cancellationToken = default);
}

public interface I주문자집단배송권조회Service
{
    PublicDataLookupResponse<주문자집단배송권후보항목> 후보검색(
        주문자집단배송권조회요청 request);
}
