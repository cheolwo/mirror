using Hongdal.ApiMetadata;
using Hongdal.Application.Customs;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 홍달.Services.Versioning;

namespace Hongdal.Controllers.Common;

[ApiController]
[AllowAnonymous]
[HongdalApiVersion(
    HongdalProductVersion.V2_5,
    FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
[HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
[RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
[Route("api/v1/customs/hs-codes")]
public sealed class 공동수입HS코드Controller : ControllerBase
{
    private readonly I공동수입HS코드조회UseCase _useCase;

    public 공동수입HS코드Controller(I공동수입HS코드조회UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    public async Task<IActionResult> 검색(
        [FromQuery] string? query,
        [FromQuery] int? businessCategory,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _useCase.검색Async(query, businessCategory, page, pageSize, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{hsCode}/food-price-comparison")]
    public async Task<IActionResult> 식품가격비교(
        string hsCode,
        [FromQuery] string countryCode = "CN",
        [FromQuery] string? referenceDate = null,
        [FromQuery] int domesticLookbackDays = 14,
        [FromQuery] string? referenceMonth = null,
        [FromQuery] int importLookbackMonths = 3,
        [FromQuery] decimal? fxRateKrwPerUsd = null,
        [FromQuery] decimal? estimatedImportAdditionalCostKrwPerKg = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _useCase.식품가격비교Async(new FoodPriceComparisonRequest
        {
            HsCode = hsCode,
            CountryCode = countryCode,
            ReferenceDate = referenceDate ?? string.Empty,
            DomesticLookbackDays = domesticLookbackDays,
            ReferenceMonth = referenceMonth ?? string.Empty,
            ImportLookbackMonths = importLookbackMonths,
            FxRateKrwPerUsd = fxRateKrwPerUsd,
            EstimatedImportAdditionalCostKrwPerKg = estimatedImportAdditionalCostKrwPerKg
        }, cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{hsCode}/public-data")]
    public async Task<IActionResult> 공공데이터수집(
        string hsCode,
        [FromQuery] string countryCode = "CN",
        [FromQuery] string? referenceMonth = null,
        [FromQuery] int lookbackMonths = 3,
        [FromQuery] string? referenceDate = null,
        [FromQuery] decimal? expectedFxRateKrwPerUsd = null,
        [FromQuery] string[]? sourceKeys = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _useCase.공공데이터수집Async(new Hs공공데이터수집요청
        {
            HsCode = hsCode,
            CountryCode = countryCode,
            ReferenceMonth = referenceMonth ?? string.Empty,
            LookbackMonths = lookbackMonths,
            ReferenceDate = referenceDate ?? string.Empty,
            ExpectedFxRateKrwPerUsd = expectedFxRateKrwPerUsd,
            SourceKeys = sourceKeys ?? []
        }, cancellationToken);

        return this.ToActionResult(result);
    }
}
