namespace 홍달.Services.Options;

public sealed class PublicDataOptions
{
    public const string SectionName = "PublicData";

    public string ServiceKey { get; set; } = string.Empty;

    public string DataGoKrServiceKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 20;

    public RoadAddressOptions RoadAddress { get; set; } = new();

    public ApartmentComplexOptions ApartmentComplex { get; set; } = new();

    public ApartmentManagementFeeOptions ApartmentManagementFee { get; set; } = new();

    public CustomsTradeStatisticsOptions CustomsTradeStatistics { get; set; } = new();

    public CustomsRequirementsOptions CustomsRequirements { get; set; } = new();

    public CustomsExchangeRateOptions CustomsExchangeRate { get; set; } = new();

    public AtFoodPricesOptions AtFoodPrices { get; set; } = new();

    public KamisOptions Kamis { get; set; } = new();

    public UsdaNassQuickStatsOptions UsdaNassQuickStats { get; set; } = new();

    public AbsConsumerPriceIndexOptions AbsConsumerPriceIndex { get; set; } = new();

    public TraditionalMarketOptions TraditionalMarket { get; set; } = new();

    public FishCooperativeStatisticsOptions FishCooperativeStatistics { get; set; } = new();
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
