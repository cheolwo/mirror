using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.AgriculturalFisheries;
using Ssalddel.Contracts.Common.Content;
using Ssalddel.Services.AgriculturalFisheries.Information;
using Ssalddel.Services.FoodCulture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ssalddel.Controllers.Common;

[ApiController]
[AllowAnonymous]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[Route("api/v1/agricultural-fisheries")]
[SsalddelApiContractName("AgriculturalFisheriesInformationController")]
public sealed class 농수산정보Controller : ControllerBase
{
    private readonly IAgriculturalFisheriesInformationService _informationService;
    private readonly IBls평균소매가격ArchiveService _blsAverageRetailPriceService;
    private readonly I국제농수산가격ArchiveService _internationalPriceArchiveService;
    private readonly IUsdaAms시장가격ArchiveService _usdaAmsMarketPriceArchiveService;
    private readonly IUsdaAms공개사업체QueryService
        _usdaAmsPublicBusinessQueryService;
    private readonly IKamis중심UsdaAms가격비교QueryService
        _kamisCenteredUsdaAmsComparisonService;
    private readonly IHs식품국가가격CardQueryService
        _hsFoodCountryPriceCardService;
    private readonly I미국농수산가격조회Service _usPriceService;
    private readonly I호주농수산식품가격조회Service _australiaFoodPriceService;
    private readonly I미국농어업경영체정보원천Service _usOperatorSourceService;
    private readonly IOfficialFoodRecipeIngredientIndexService _ingredientIndexService;
    private readonly IOfficialFoodRecipeArchiveService _recipeArchiveService;
    private readonly IOfficialFoodIngredientCompanyArchiveService _companyArchiveService;

    public 농수산정보Controller(
        IAgriculturalFisheriesInformationService informationService,
        IBls평균소매가격ArchiveService blsAverageRetailPriceService,
        I국제농수산가격ArchiveService internationalPriceArchiveService,
        IUsdaAms시장가격ArchiveService usdaAmsMarketPriceArchiveService,
        IUsdaAms공개사업체QueryService usdaAmsPublicBusinessQueryService,
        IKamis중심UsdaAms가격비교QueryService
            kamisCenteredUsdaAmsComparisonService,
        IHs식품국가가격CardQueryService hsFoodCountryPriceCardService,
        I미국농수산가격조회Service usPriceService,
        I호주농수산식품가격조회Service australiaFoodPriceService,
        I미국농어업경영체정보원천Service usOperatorSourceService,
        IOfficialFoodRecipeIngredientIndexService ingredientIndexService,
        IOfficialFoodRecipeArchiveService recipeArchiveService,
        IOfficialFoodIngredientCompanyArchiveService companyArchiveService)
    {
        _informationService = informationService;
        _blsAverageRetailPriceService = blsAverageRetailPriceService;
        _internationalPriceArchiveService = internationalPriceArchiveService;
        _usdaAmsMarketPriceArchiveService = usdaAmsMarketPriceArchiveService;
        _usdaAmsPublicBusinessQueryService =
            usdaAmsPublicBusinessQueryService;
        _kamisCenteredUsdaAmsComparisonService =
            kamisCenteredUsdaAmsComparisonService;
        _hsFoodCountryPriceCardService = hsFoodCountryPriceCardService;
        _usPriceService = usPriceService;
        _australiaFoodPriceService = australiaFoodPriceService;
        _usOperatorSourceService = usOperatorSourceService;
        _ingredientIndexService = ingredientIndexService;
        _recipeArchiveService = recipeArchiveService;
        _companyArchiveService = companyArchiveService;
    }

    [HttpGet]
    [SsalddelApiContractName("GetOverview")]
    public ActionResult<AgriculturalFisheriesInformationOverviewResponse> 개요조회()
        => Ok(_informationService.GetOverview());

    [HttpGet("items")]
    [SsalddelApiContractName("SearchItems")]
    public ActionResult<AgriculturalFisheriesItemSearchResponse> 품목검색(
        [FromQuery] string? query,
        [FromQuery] string? categoryCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
        => Ok(_informationService.SearchItems(query, categoryCode, page, pageSize));

    [HttpGet("market-prices/sources")]
    [SsalddelApiContractName("GetMarketPriceSources")]
    public ActionResult<농수산시세정보원목록응답> 시세정보원목록조회(
        [FromQuery] string? countryCode,
        [FromQuery] string? marketStageCode)
        => Ok(_informationService.GetMarketPriceSources(
            countryCode,
            marketStageCode));

    [HttpGet("international-prices/sources")]
    [SsalddelApiContractName("GetInternationalPriceSources")]
    public ActionResult<IReadOnlyList<국제농수산가격Source응답>>
        국제가격정보원목록조회()
        => Ok(_internationalPriceArchiveService.GetSources());

    [HttpGet("international-prices/archive")]
    [SsalddelApiContractName("GetInternationalPriceArchive")]
    public async Task<ActionResult<국제농수산가격Archive응답>>
        국제가격Archive조회(
            [FromQuery] 국제농수산가격ArchiveQuery query,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _internationalPriceArchiveService.GetArchiveAsync(
                query,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    [HttpGet("us-market-news-prices/archive")]
    [SsalddelApiContractName("GetUsMarketNewsPriceArchive")]
    public async Task<ActionResult<UsdaAms시장가격Archive응답>>
        미국MarketNews가격Archive조회(
            [FromQuery] UsdaAms시장가격ArchiveQuery query,
            CancellationToken cancellationToken = default)
        => Ok(await _usdaAmsMarketPriceArchiveService.GetArchiveAsync(
            query,
            cancellationToken));

    [HttpGet("market-prices/kamis-centered-usda-ams")]
    [SsalddelApiContractName("GetKamisCenteredUsdaAmsPriceComparison")]
    public async Task<ActionResult<Kamis중심UsdaAms가격비교응답>>
        카미스중심미국농산물가격비교조회(
            [FromQuery] Kamis중심UsdaAms가격비교Query query,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _kamisCenteredUsdaAmsComparisonService.GetAsync(
                query,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "KAMIS 중심 USDA AMS 가격 비교 조건을 확인해 주세요.",
                Detail = exception.Message
            });
        }
    }

    [HttpGet("market-prices/comparability")]
    [SsalddelApiContractName("AssessMarketPriceComparability")]
    public ActionResult<농수산시세비교판정응답> 시세정보원비교가능성조회(
        [FromQuery] string? leftSourceKey,
        [FromQuery] string? rightSourceKey)
    {
        var response = _informationService.AssessMarketPriceComparability(
            leftSourceKey,
            rightSourceKey);
        return response.StatusCode switch
        {
            농수산시세비교판정Codes.잘못된요청 => BadRequest(response),
            농수산시세비교판정Codes.정보원없음 => NotFound(response),
            _ => Ok(response)
        };
    }

    [HttpGet("items/{hsCode}/country-price-card")]
    [SsalddelApiContractName("GetHsFoodCountryPriceCard")]
    public async Task<ActionResult<Hs식품국가가격Card응답>>
        식품국가가격Card조회(
            string hsCode,
            [FromQuery] Hs식품국가가격CardQuery query,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _hsFoodCountryPriceCardService.GetAsync(
                hsCode,
                query,
                cancellationToken);
            return response.StatusCode == Hs식품국가가격Card상태Codes.품목없음
                ? NotFound(response)
                : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "HS 식품 국가별 가격 카드 조건을 확인해 주세요.",
                Detail = exception.Message
            });
        }
    }

    [HttpGet("us-retail-average-prices/catalog")]
    [SsalddelApiContractName("GetUsRetailAveragePriceCatalog")]
    public ActionResult<IReadOnlyList<Bls평균소매가격Series응답>>
        미국평균소매가격Series목록조회()
        => Ok(_blsAverageRetailPriceService.GetSeriesCatalog());

    [HttpGet("us-retail-average-prices/kamis-crosswalk")]
    [SsalddelApiContractName("GetUsRetailAveragePriceKamisCrosswalk")]
    public ActionResult<BlsKamis비교Catalog응답> 미국평균소매가격Kamis비교후보조회()
        => Ok(_blsAverageRetailPriceService.GetKamisComparisonCatalog());

    [HttpGet("us-retail-average-prices/archive")]
    [SsalddelApiContractName("GetUsRetailAveragePriceArchive")]
    public async Task<ActionResult<Bls평균소매가격Archive응답>>
        미국평균소매가격Archive조회(
            [FromQuery] Bls평균소매가격ArchiveQuery query,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _blsAverageRetailPriceService.GetArchiveAsync(
                query,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "미국 평균 소매가격 조회 조건을 확인해 주세요.",
                Detail = exception.Message
            });
        }
    }

    [HttpGet("food-ingredients")]
    [SsalddelApiContractName("SearchFoodIngredients")]
    public async Task<ActionResult<IReadOnlyList<OfficialFoodIngredientDto>>> 음식식재료검색(
        [FromQuery] OfficialFoodIngredientQuery query,
        CancellationToken cancellationToken = default)
    {
        var publicQuery = new OfficialFoodIngredientQuery
        {
            CategoryCode = query.CategoryCode,
            LanguageCode = query.LanguageCode,
            ClassificationState = query.ClassificationState,
            SearchText = query.SearchText,
            Take = Math.Clamp(query.Take, 1, 50)
        };
        return Ok(await _ingredientIndexService.SearchIngredientsAsync(
            publicQuery,
            cancellationToken));
    }

    [HttpGet("food-ingredients/hs-codes")]
    [SsalddelApiContractName("GetFoodIngredientHsCodes")]
    public async Task<ActionResult<OfficialFoodIngredientHsMappingResponse>>
        음식식재료HSCode조회(
            [FromServices] IOfficialFoodIngredientHsMappingService mappingService,
            [FromQuery] OfficialFoodIngredientHsQuery query,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.IngredientKey)
            && string.IsNullOrWhiteSpace(query.IngredientName))
        {
            return BadRequest();
        }

        try
        {
            return Ok(await mappingService.GetOrCreateAsync(query, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    [HttpGet("food-ingredients/companies")]
    [SsalddelApiContractName("SearchFoodIngredientCompanies")]
    public async Task<ActionResult<OfficialFoodIngredientCompanyResearchResponse>>
        음식식재료기업검색(
            [FromQuery] OfficialFoodIngredientCompanyQuery query,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.IngredientName)
            || (query.IngredientName.Trim().Length < 2
                && string.IsNullOrWhiteSpace(query.IngredientKey)))
        {
            return BadRequest();
        }

        try
        {
            return Ok(await _companyArchiveService.ResearchAndArchiveAsync(
                new OfficialFoodIngredientCompanyQuery
                {
                    IngredientKey = query.IngredientKey,
                    IngredientName = query.IngredientName,
                    Take = Math.Clamp(query.Take, 1, 20)
                },
                cancellationToken: cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("food-ingredients/companies/archive")]
    [SsalddelApiContractName("GetFoodIngredientCompanyArchive")]
    public async Task<ActionResult<OfficialFoodIngredientCompanyArchiveResponse>>
        음식식재료기업Archive조회(
            [FromQuery] string? ingredientKey,
            [FromQuery] string? ingredientName,
            [FromQuery] bool includeInactive = false,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ingredientKey)
            && string.IsNullOrWhiteSpace(ingredientName))
        {
            return BadRequest();
        }

        var archive = await _companyArchiveService.GetArchiveAsync(
            ingredientKey,
            ingredientName,
            includeInactive,
            cancellationToken);
        return archive is null ? NotFound() : Ok(archive);
    }

    [HttpGet("food-ingredients/companies/coverage")]
    [SsalddelApiContractName("GetFoodIngredientCompanyCoverage")]
    public async Task<ActionResult<OfficialFoodIngredientCompanyCoverageResponse>>
        음식식재료기업Coverage조회(
            [FromQuery] int staleAfterDays = 30,
            CancellationToken cancellationToken = default)
        => Ok(await _companyArchiveService.GetCoverageAsync(
            Math.Clamp(staleAfterDays, 1, 3650),
            cancellationToken));

    [HttpGet("food-dishes")]
    [SsalddelApiContractName("SearchFoodDishes")]
    public async Task<ActionResult<IReadOnlyList<OfficialFoodRecipeDishDto>>> 음식목록검색(
        [FromQuery] OfficialFoodDishDiscoveryQuery query,
        CancellationToken cancellationToken = default)
    {
        var dishes = await _recipeArchiveService.SearchDishesAsync(
            new OfficialFoodRecipeQuery
            {
                CountryCode = string.IsNullOrWhiteSpace(query.CountryCode)
                    ? null
                    : query.CountryCode.Trim().ToUpperInvariant(),
                SearchText = query.SearchText,
                Take = Math.Clamp(query.Take, 1, 50),
                OnlyWithBrowsableIngredients = true
            },
            cancellationToken);
        return Ok(dishes
            .Where(dish => dish.ReviewState != OfficialFoodRecipeReviewStates.Excluded
                           && dish.RepresentationState != OfficialFoodRecipeRepresentationStates.Excluded)
            .ToArray());
    }

    [HttpGet("food-dishes/{dishKey}")]
    [SsalddelApiContractName("GetFoodDish")]
    public async Task<ActionResult<OfficialFoodDishDetailDto>> 음식상세조회(
        string dishKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dish = await _recipeArchiveService.GetDishAsync(dishKey, cancellationToken);
            if (dish is null
                || dish.ReviewState == OfficialFoodRecipeReviewStates.Excluded
                || dish.RepresentationState == OfficialFoodRecipeRepresentationStates.Excluded)
            {
                return NotFound();
            }

            var variants = await _recipeArchiveService.GetVariantsAsync(dish.DishKey, cancellationToken);
            var representative = variants.FirstOrDefault(variant =>
                variant.IsFreshForPublication && variant.StructuredIngredients?.Count > 0);
            if (representative is null)
            {
                return NotFound();
            }

            return Ok(new OfficialFoodDishDetailDto(
                dish,
                representative.RecordKey,
                representative.SourceKey,
                representative.Provider,
                representative.Title,
                representative.ServingText,
                representative.OriginalUrl,
                representative.AttributionText,
                representative.CollectedAtUtc,
                representative.IsFreshForPublication,
                representative.StructuredIngredients ?? []));
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    [HttpGet("items/{hsCode}/domestic-price")]
    [SsalddelApiContractName("GetDomesticPrice")]
    public async Task<ActionResult<AgriculturalFisheriesDomesticPriceResponse>> 국내가격조회(
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
    [SsalddelApiContractName("GetUsPrices")]
    public async Task<ActionResult<미국농수산가격조회응답>> 미국가격조회(
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
    [SsalddelApiContractName("GetAustraliaFoodPriceCatalog")]
    public ActionResult<호주농수산식품가격Catalog응답> 호주식품가격목록조회()
        => Ok(_australiaFoodPriceService.GetCatalog());

    [HttpGet("au-food-price-indexes")]
    [SsalddelApiContractName("GetAustraliaFoodPriceIndexes")]
    public async Task<ActionResult<호주농수산식품가격조회응답>> 호주식품가격지수조회(
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
    [SsalddelApiContractName("GetUsOperatorInformationSources")]
    public ActionResult<미국농어업경영체정보원천조회응답> 미국경영체정보출처조회(
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

    [HttpGet("us-operator-profiles")]
    [SsalddelApiContractName("SearchUsOperatorProfiles")]
    public async Task<ActionResult<UsdaAms공개사업체조회응답>>
        미국경영체Profile조회(
            [FromQuery] string? q = null,
            [FromQuery] string? directoryTypeCode = null,
            [FromQuery] string? stateCode = null,
            [FromQuery] string? productKey = null,
            [FromQuery] bool currentOnly = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _usdaAmsPublicBusinessQueryService.SearchAsync(
                new UsdaAms공개사업체조회요청
                {
                    SearchText = q,
                    DirectoryTypeCode = directoryTypeCode,
                    StateCode = stateCode,
                    ProductKey = productKey,
                    CurrentOnly = currentOnly,
                    Page = page,
                    PageSize = pageSize
                },
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "미국 공개 사업체 조회 조건을 확인해 주세요.",
                Detail = exception.Message
            });
        }
    }
}
