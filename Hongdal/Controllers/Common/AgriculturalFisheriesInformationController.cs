using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[ApiController]
[AllowAnonymous]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[Route("api/v1/agricultural-fisheries")]
public sealed class AgriculturalFisheriesInformationController : ControllerBase
{
    private readonly IAgriculturalFisheriesInformationService _informationService;
    private readonly I미국농수산가격조회Service _usPriceService;
    private readonly I호주농수산식품가격조회Service _australiaFoodPriceService;
    private readonly I미국농어업경영체정보원천Service _usOperatorSourceService;

    public AgriculturalFisheriesInformationController(
        IAgriculturalFisheriesInformationService informationService,
        I미국농수산가격조회Service usPriceService,
        I호주농수산식품가격조회Service australiaFoodPriceService,
        I미국농어업경영체정보원천Service usOperatorSourceService)
    {
        _informationService = informationService;
        _usPriceService = usPriceService;
        _australiaFoodPriceService = australiaFoodPriceService;
        _usOperatorSourceService = usOperatorSourceService;
    }

    [HttpGet]
    public ActionResult<AgriculturalFisheriesInformationOverviewResponse> GetOverview()
        => Ok(_informationService.GetOverview());

    [HttpGet("items")]
    public ActionResult<AgriculturalFisheriesItemSearchResponse> SearchItems(
        [FromQuery] string? query,
        [FromQuery] string? categoryCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
        => Ok(_informationService.SearchItems(query, categoryCode, page, pageSize));

    [HttpGet("items/{hsCode}/domestic-price")]
    public async Task<ActionResult<AgriculturalFisheriesDomesticPriceResponse>> GetDomesticPrice(
        string hsCode,
        [FromQuery] string? referenceDate = null,
        [FromQuery] int lookbackDays = 14,
        CancellationToken cancellationToken = default)
    {
        var response = await _informationService.GetDomesticPriceAsync(
            new AgriculturalFisheriesDomesticPriceRequest
            {
                HsCode = hsCode,
                ReferenceDate = referenceDate ?? string.Empty,
                LookbackDays = lookbackDays
            },
            cancellationToken);

        return response.StatusCode switch
        {
            "InvalidRequest" => BadRequest(response),
            "MappingRequired" => NotFound(response),
            "DataUnavailable" => StatusCode(StatusCodes.Status503ServiceUnavailable, response),
            _ => Ok(response)
        };
    }

    [HttpGet("us-prices")]
    public async Task<ActionResult<미국농수산가격조회응답>> GetUsPrices(
        [FromQuery] string commodity,
        [FromQuery] string sourceKey = 미국농수산가격출처Keys.UsdaNassQuickStats,
        [FromQuery] string statisticCategory = "PRICE RECEIVED",
        [FromQuery] string program = "SURVEY",
        [FromQuery] string? sector = null,
        [FromQuery] string? group = null,
        [FromQuery] string aggregationLevel = "NATIONAL",
        [FromQuery] string? stateAlpha = null,
        [FromQuery] string domain = "TOTAL",
        [FromQuery] string? frequency = null,
        [FromQuery] int yearFrom = 0,
        [FromQuery] int? yearTo = null,
        [FromQuery] int maxItems = 100,
        CancellationToken cancellationToken = default)
    {
        var response = await _usPriceService.조회Async(
            new 미국농수산가격조회요청
            {
                SourceKey = sourceKey,
                Commodity = commodity,
                StatisticCategory = statisticCategory,
                Program = program,
                Sector = sector,
                Group = group,
                AggregationLevel = aggregationLevel,
                StateAlpha = stateAlpha,
                Domain = domain,
                Frequency = frequency,
                YearFrom = yearFrom,
                YearTo = yearTo,
                MaxItems = maxItems
            },
            cancellationToken);

        return response.StatusCode switch
        {
            미국농수산가격조회상태Codes.잘못된요청 => BadRequest(response),
            미국농수산가격조회상태Codes.지원하지않는출처 => BadRequest(response),
            미국농수산가격조회상태Codes.설정안됨 =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, response),
            미국농수산가격조회상태Codes.자료조회불가 =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, response),
            _ => Ok(response)
        };
    }

    [HttpGet("au-food-price-indexes/catalog")]
    public ActionResult<호주농수산식품가격Catalog응답> GetAustraliaFoodPriceCatalog()
        => Ok(_australiaFoodPriceService.GetCatalog());

    [HttpGet("au-food-price-indexes")]
    public async Task<ActionResult<호주농수산식품가격조회응답>> GetAustraliaFoodPriceIndexes(
        [FromQuery] string sourceKey = 호주농수산식품가격출처Keys.AbsConsumerPriceIndex,
        [FromQuery] string indexCode = 호주식품가격지수Codes.FoodAndNonAlcoholicBeverages,
        [FromQuery] string measureCode = 호주식품가격지수측정Codes.IndexNumber,
        [FromQuery] string regionCode = 호주식품가격지수지역Codes.Australia,
        [FromQuery] string? startPeriod = null,
        [FromQuery] string? endPeriod = null,
        [FromQuery] int maxItems = 60,
        CancellationToken cancellationToken = default)
    {
        var response = await _australiaFoodPriceService.조회Async(
            new 호주농수산식품가격조회요청
            {
                SourceKey = sourceKey,
                IndexCode = indexCode,
                MeasureCode = measureCode,
                RegionCode = regionCode,
                StartPeriod = startPeriod ?? string.Empty,
                EndPeriod = endPeriod ?? string.Empty,
                MaxItems = maxItems
            },
            cancellationToken);

        return response.StatusCode switch
        {
            호주농수산식품가격조회상태Codes.잘못된요청 => BadRequest(response),
            호주농수산식품가격조회상태Codes.지원하지않는출처 => BadRequest(response),
            호주농수산식품가격조회상태Codes.자료조회불가 =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, response),
            _ => Ok(response)
        };
    }

    [HttpGet("us-operator-information-sources")]
    public ActionResult<미국농어업경영체정보원천조회응답> GetUsOperatorInformationSources(
        [FromQuery] string? q = null,
        [FromQuery] string? sectorCode = null,
        [FromQuery] string? recordTypeCode = null,
        [FromQuery] string? publicAccessCode = null,
        [FromQuery] string? integrationStatusCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => Ok(_usOperatorSourceService.Search(
            new 미국농어업경영체정보원천조회요청
            {
                SearchText = q,
                SectorCode = sectorCode,
                RecordTypeCode = recordTypeCode,
                PublicAccessCode = publicAccessCode,
                IntegrationStatusCode = integrationStatusCode,
                Page = page,
                PageSize = pageSize
            }));
}
