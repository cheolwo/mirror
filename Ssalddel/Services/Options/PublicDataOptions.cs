namespace 살뜰.Services.Options;

public sealed class PublicDataOptions
{
    public const string SectionName = "PublicData";

    public string ServiceKey { get; set; } = string.Empty;

    public string DataGoKrServiceKey { get; set; } = string.Empty;

    public string DataGoKrBaseUrl { get; set; } = "https://apis.data.go.kr";

    public int TimeoutSeconds { get; set; } = 20;

    public RoadAddressOptions RoadAddress { get; set; } = new();

    public ApartmentComplexOptions ApartmentComplex { get; set; } = new();

    public ApartmentManagementFeeOptions ApartmentManagementFee { get; set; } = new();

    public CustomsTradeStatisticsOptions CustomsTradeStatistics { get; set; } = new();

    public CustomsRequirementsOptions CustomsRequirements { get; set; } = new();

    public CustomsExchangeRateOptions CustomsExchangeRate { get; set; } = new();

    public AtFoodPricesOptions AtFoodPrices { get; set; } = new();

    public DomesticAgriculturalAuctionPricesOptions DomesticAgriculturalAuctionPrices { get; set; } =
        new();

    public KamisOptions Kamis { get; set; } = new();

    public UsdaNassQuickStatsOptions UsdaNassQuickStats { get; set; } = new();

    public UsdaAmsMarketNewsOptions UsdaAmsMarketNews { get; set; } = new();

    public UsdaAmsLocalFoodDirectoryOptions UsdaAmsLocalFoodDirectory { get; set; } =
        new();

    public AbsConsumerPriceIndexOptions AbsConsumerPriceIndex { get; set; } = new();

    public TraditionalMarketOptions TraditionalMarket { get; set; } = new();

    public FishCooperativeStatisticsOptions FishCooperativeStatistics { get; set; } = new();

    public GyeonggiDataDreamOptions GyeonggiDataDream { get; set; } = new();

    public SelectedPublicDataMapOptions SelectedPublicDataMap { get; set; } = new();

    public MafraFisheriesAuctionOptions FisheriesAuction { get; set; } = new();

    public MofFishingAreaCatalogOptions MofFishingAreas { get; set; } = new();

    public MfdsCookRecipeOptions MfdsCookRecipe { get; set; } = new();

    public MfdsIngredientCompanyOptions MfdsIngredientCompanies { get; set; } = new();

    public RdaLocalFoodOptions RdaLocalFood { get; set; } = new();

    public NongsaroOpenApiOptions Nongsaro { get; set; } = new();

    public KmaAsosOptions KmaAsos { get; set; } = new();

    public KmaUltraShortNowcastOptions KmaUltraShortNowcast { get; set; } = new();

    public MaffRegionalCuisineOptions MaffRegionalCuisine { get; set; } = new();

    public JapanRegionalDataOptions Japan { get; set; } = new();

    public NhsHealthierFamiliesRecipeOptions NhsHealthierFamiliesRecipes { get; set; } = new();
}

public sealed class RoadAddressOptions
{
    public string ConfirmKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://business.juso.go.kr";

    public string SearchPath { get; set; } = "/addrlink/addrLinkApi.do";
}

public sealed class ApartmentComplexOptions
{
    public string ServiceKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string ListPath { get; set; } = "/1613000/AptListService3/getLegaldongAptList";

    public string BasicInfoPath { get; set; } = "/1613000/AptBasisInfoServiceV4/getAphusBassInfo";
}

public sealed class ApartmentManagementFeeOptions
{
    public string ServiceKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string PublicManagementFeePath { get; set; } = "/1613000/AptPublicManageCostService/getHsmpPublicManageCostInfo";

    public string IndividualUsageFeePath { get; set; } = "/1613000/AptIndvdlzManageCostService/getHsmpIndvdlzManageCostInfo";

    public string LongTermRepairReservePath { get; set; } = "/1613000/AptLongTermRepairReserveService/getHsmpLongTermRepairReserveInfo";
}

public sealed class CustomsTradeStatisticsOptions
{
    public string ServiceKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string HsCountryMonthlyPath { get; set; } = "/1220000/nitemtrade/getNitemtradeList";
}

public sealed class CustomsRequirementsOptions
{
    public string ServiceKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string LookupPath { get; set; } = "/1220000/retrieveCcctLworCd/getRetrieveCcctLworCd";
}

public sealed class CustomsExchangeRateOptions
{
    public string ServiceKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string LookupPath { get; set; } = "/1220000/retrieveTrifFxrtInfo/getRetrieveTrifFxrtInfo";
}

public sealed class AtFoodPricesOptions
{
    public string ServiceKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string DailyPricePath { get; set; } = "/B552845/perDay/price";

    public decimal DefaultSimulationFxRateKrwPerUsd { get; set; } = 1350m;
}

public sealed class DomesticAgriculturalAuctionPricesOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "http://211.237.50.150:7080";

    public bool AllowInsecureHttp { get; set; }

    public string DatasetName { get; set; } = "Grid_20240625000000000655_1";

    public int MaxPageSize { get; set; } = 1000;

    public string DocumentationUrl { get; set; } =
        "https://data.mafra.go.kr/opendata/data/indexOpenDataDetail.do?data_id=20240625000000002462";
}

public sealed class KamisOptions
{
    public string CertificationKey { get; set; } = string.Empty;

    public string RequesterId { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://www.kamis.or.kr";

    public string DailyCategoryPricePath { get; set; } = "/service/price/xml.do";
}

public sealed class UsdaNassQuickStatsOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://quickstats.nass.usda.gov";

    public string DataPath { get; set; } = "/api/api_GET/";
}

public sealed class UsdaAmsMarketNewsOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://marsapi.ams.usda.gov";

    public string ReportsPath { get; set; } = "/services/v1.2/reports";

    public int TimeoutSeconds { get; set; } = 180;
}

public sealed class UsdaAmsLocalFoodDirectoryOptions
{
    public string BaseUrl { get; set; } = "https://www.usdalocalfoodportal.com";

    public string BulkDownloadPath { get; set; } =
        "/api/download_by_directory";

    public string DataSharingUrl { get; set; } =
        "https://www.usdalocalfoodportal.com/fe/datasharing/";

    public int TimeoutSeconds { get; set; } = 180;
}

public sealed class AbsConsumerPriceIndexOptions
{
    public string BaseUrl { get; set; } = "https://data.api.abs.gov.au";

    public string DataPath { get; set; } = "/rest/data/CPI";
}

public sealed class TraditionalMarketOptions
{
    public string ServiceKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.odcloud.kr";

    public string ApiPath { get; set; } = "/api/15052837/v1/uddi:1fd54eb7-0565-4755-8ec7-a70931b6dc77";

    public string DatasetKey { get; set; } = "semas-traditional-market-status";

    public string SourceReferenceDate { get; set; } = "2025-07-22";

    public int PageSize { get; set; } = 1000;
}

public sealed class FishCooperativeStatisticsOptions
{
    public string ServiceKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string GeneralStatisticsPath { get; set; }
        = "/1160100/service/GetFishCoopInfoService/getFishCoopGeneInfo";

    public string GeneralStatisticsTitle { get; set; } = "수협_일반현황_임직원현황";

    public int PageSize { get; set; } = 1000;
}

public sealed class GyeonggiDataDreamOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = "https://openapi.gg.go.kr";

    public string PortalBaseUrl { get; set; } = "https://data.gg.go.kr";

    public int PageSize { get; set; } = 1000;

    public bool MapProjectionEnabled { get; set; }

    public int MapProjectionRefreshHours { get; set; } = 12;
}

public sealed class SelectedPublicDataMapOptions
{
    public bool TourismEnabled { get; set; }

    public bool OnlinePriceEnabled { get; set; }

    public bool KosisEnabled { get; set; }

    public int RefreshHours { get; set; } = 24;

    public int TourismMarkerLimit { get; set; } = 50;

    public string KosisIndicatorSearchName { get; set; } = "소비자물가";

    public string KosisIndicatorName { get; set; } = "소비자물가지수";

    public int KosisRecentPeriodCount { get; set; } = 3;
}

public sealed class MafraFisheriesAuctionOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "http://211.237.50.150:7080";

    public bool AllowInsecureHttp { get; set; }

    public string DatasetName { get; set; } = "Grid_20151125000000000310_1";

    public int MaxPageSize { get; set; } = 1000;

    public string DocumentationUrl { get; set; }
        = "https://www.data.go.kr/data/15109239/openapi.do";
}

public sealed class MofFishingAreaCatalogOptions
{
    public string BaseUrl { get; set; } = "https://www.data.go.kr";

    public string DownloadPath { get; set; }
        = "/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003229245&fileDetailSn=1&insertDataPrcus=N";

    public string SourceUrl { get; set; }
        = "https://www.data.go.kr/data/15147444/fileData.do";

    public string DatasetVersion { get; set; } = "20211230";

    public int CacheHours { get; set; } = 24;
}

public sealed class MfdsCookRecipeOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://openapi.foodsafetykorea.go.kr";

    public string ServiceId { get; set; } = "COOKRCP01";

    public int PageSize { get; set; } = 1000;
}

public sealed class MfdsIngredientCompanyOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://openapi.foodsafetykorea.go.kr";

    public string ServiceId { get; set; } = "C002";

    public int PageSize { get; set; } = 100;

    public int MaxForeignFacilityLookups { get; set; } = 5;
}

public sealed class RdaLocalFoodOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.nongsaro.go.kr";

    public string ListPath { get; set; } = "/service/nvpcFdCkry/fdNmLst";

    public string DetailPath { get; set; } = "/service/nvpcFdCkry/fdNmDtl";

    public int PageSize { get; set; } = 100;
}

public sealed class NongsaroOpenApiOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.nongsaro.go.kr";
}

public sealed class KmaAsosOptions
{
    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string DailyPath { get; set; }
        = "/1360000/AsosDalyInfoService/getWthrDataList";
}

public sealed class KmaUltraShortNowcastOptions
{
    public string BaseUrl { get; set; } = "https://apis.data.go.kr";

    public string ObservationPath { get; set; }
        = "/1360000/VilageFcstInfoService_2.0/getUltraSrtNcst";

    public int BaseTimeSafetyDelayMinutes { get; set; } = 50;

    public int ObservationCacheMinutes { get; set; } = 70;

    public int FailureCacheSeconds { get; set; } = 60;
}

public sealed class MaffRegionalCuisineOptions
{
    public string BaseUrl { get; set; } = "https://www.maff.go.jp";

    public string IndexPath { get; set; } = "/e/policies/market/k_ryouri/";
}

public sealed class JapanRegionalDataOptions
{
    public JapanEStatOptions EStat { get; set; } = new();

    public JapanResasOptions Resas { get; set; } = new();
}

public sealed class JapanEStatOptions
{
    public string AppId { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.e-stat.go.jp";

    public string ApiVersion { get; set; } = "3.0";
}

public sealed class JapanResasOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://opendata.resas-portal.go.jp";

    public string AgricultureSalesPath { get; set; }
        = "/api/v1/agriculture/sales/forLine";
}

public sealed class NhsHealthierFamiliesRecipeOptions
{
    public string BaseUrl { get; set; } = "https://www.nhs.uk";

    public string IndexPath { get; set; } = "/healthier-families/recipes/";
}
