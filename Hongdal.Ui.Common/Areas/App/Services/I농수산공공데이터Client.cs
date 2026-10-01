using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.PublicData;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface I농수산공공데이터Client
{
    Task<AgriculturalFisheriesInformationOverviewResponse> 개요조회Async(
        CancellationToken cancellationToken = default);

    Task<AgriculturalFisheriesItemSearchResponse> 국내품목조회Async(
        string? query = null,
        string? categoryCode = null,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<AgriculturalFisheriesDomesticPriceResponse> 국내가격조회Async(
        string hsCode,
        int lookbackDays = 14,
        CancellationToken cancellationToken = default);

    Task<미국농수산가격조회응답> 미국가격조회Async(
        string commodity,
        string program,
        int yearFrom,
        int yearTo,
        int maxItems = 100,
        CancellationToken cancellationToken = default);

    Task<호주농수산식품가격Catalog응답> 호주가격원천Catalog조회Async(
        CancellationToken cancellationToken = default);

    Task<호주농수산식품가격조회응답> 호주식품가격지수조회Async(
        호주농수산식품가격조회요청 request,
        CancellationToken cancellationToken = default);

    Task<FoodPriceComparisonResponse> 식품가격비교Async(
        FoodPriceComparisonRequest request,
        CancellationToken cancellationToken = default);

    Task<HsCountryImportUnitPriceSimulationResult> 수입평균단가조회Async(
        HsCountryMonthlyTradeUnitPriceRequest request,
        CancellationToken cancellationToken = default);
}
