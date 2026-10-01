using Hongdal.Contracts.Common.PublicData;
using Hongdal.Filters;
using Hongdal.Application.PublicData;
using Microsoft.AspNetCore.Mvc;
using 홍달.Services.Versioning;
using Hongdal.ApiMetadata;

namespace Hongdal.Controllers.Orderer;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[ApiController]
[Route("api/v1/orderer/public-data")]
public sealed class PublicDataLookupController : ControllerBase
{
    private readonly I공공데이터조회UseCase _useCase;

    public PublicDataLookupController(I공공데이터조회UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> SearchAddresses(
        [FromQuery] string keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _useCase.도로명주소검색Async(keyword, page, pageSize, cancellationToken);
        return this.ToActionResult(result);
    }

    [HongdalApiVersion(HongdalProductVersion.V2_5, FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow, WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
    [RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HttpGet("orderer-group-scopes")]
    public IActionResult 주문자집단배송권검색(
        [FromQuery] string? roadAddress,
        [FromQuery] string? jibunAddress,
        [FromQuery] string? kakaoRegionLevel1,
        [FromQuery] string? kakaoRegionLevel2,
        [FromQuery] string? kakaoRegionLevel3,
        [FromQuery] int pageSize = 5)
    {
        var result = _useCase.주문자집단배송권검색(new 주문자집단배송권조회요청
        {
            RoadAddress = roadAddress,
            JibunAddress = jibunAddress,
            KakaoRegionLevel1 = kakaoRegionLevel1,
            KakaoRegionLevel2 = kakaoRegionLevel2,
            KakaoRegionLevel3 = kakaoRegionLevel3,
            PageSize = pageSize
        });

        return this.ToActionResult(result);
    }

    [HongdalApiVersion(HongdalProductVersion.V2_5, FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow, WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
    [RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HttpGet("apartment-complexes")]
    public async Task<IActionResult> SearchApartmentComplexes(
        [FromQuery] string? sidoCode,
        [FromQuery] string? sigunguCode,
        [FromQuery] string? eupmyeondongCode,
        [FromQuery] string? roadName,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _useCase.공동주택단지검색Async(new ApartmentComplexSearchRequest
        {
            SidoCode = sidoCode,
            SigunguCode = sigunguCode,
            EupmyeondongCode = eupmyeondongCode,
            RoadName = roadName,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return this.ToActionResult(result);
    }

    [HongdalApiVersion(HongdalProductVersion.V2_5, FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow, WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
    [RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HttpGet("apartment-complexes/{complexCode}/basic")]
    public async Task<IActionResult> GetApartmentComplexBasicInfo(
        string complexCode,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.공동주택기본정보조회Async(complexCode, cancellationToken);
        return this.ToActionResult(result);
    }

    [HongdalApiVersion(HongdalProductVersion.V2_5, FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow, WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
    [RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HttpGet("apartment-complexes/{complexCode}/management-fee-snapshot")]
    public async Task<IActionResult> GetApartmentManagementFeeSnapshot(
        string complexCode,
        [FromQuery] string month,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.관리비스냅샷조회Async(complexCode, month, cancellationToken);
        return this.ToActionResult(result);
    }

    [HongdalApiVersion(HongdalProductVersion.V2_5, FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow, WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
    [RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HttpPost("apartment-complexes/group-commerce-offset-simulation")]
    public async Task<IActionResult> SimulateGroupCommerceOffset(
        [FromBody] ApartmentGroupCommerceOffsetSimulationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.공동커머스관리비상쇄시뮬레이션Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HongdalApiVersion(HongdalProductVersion.V2_5, FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow, WorkflowKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
    [RequireVersionFeature(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
    [HttpPost("customs/hs-country-import-unit-price-simulation")]
    public async Task<IActionResult> SimulateHsCountryImportUnitPrice(
        [FromBody] HsCountryMonthlyTradeUnitPriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.수입평균단가시뮬레이션Async(request, cancellationToken);
        return this.ToActionResult(result);
    }
}
