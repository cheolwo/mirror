using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Restaurants;
using Ssalddel.Contracts.Admin.Restaurants;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin;

[SsalddelApiVersion(SsalddelProductVersion.V3_0)]
[ApiController]
[Authorize(Policy = "서버관리자전용")]
[Route("api/v1/admin/restaurant-reviews")]
[SsalddelApiContractName("RestaurantReviewOperationsController")]
public sealed class 음식점리뷰관리Controller(
    I음식운영관리UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 목록(CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.리뷰목록Async(cancellationToken));

    [HttpGet("policy")]
    public async Task<IActionResult> 정책조회(CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.리뷰정책조회Async(cancellationToken));

    [HttpPut("policy")]
    public async Task<IActionResult> 정책수정(
        [FromBody] 음식점리뷰운영정책수정요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.리뷰정책수정Async(
            request,
            CurrentAdminId(),
            cancellationToken));

    private string CurrentAdminId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.Identity?.Name
           ?? "unknown-admin";
}

[SsalddelApiVersion(SsalddelProductVersion.V3_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[ApiController]
[Authorize(Policy = "서버관리자전용")]
[Route("api/v1/admin/food-delivery-pricing-policy")]
[SsalddelApiContractName("FoodDeliveryPricingPolicyController")]
public sealed class 음식배달요금정책Controller(
    I음식운영관리UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 조회(CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.배달요금정책조회Async(cancellationToken));

    [HttpPut]
    public async Task<IActionResult> 수정(
        [FromBody] 음식배달요금정책응답 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.배달요금정책수정Async(
            request,
            CurrentAdminId(),
            cancellationToken));

    [HttpGet("temporary-demand-surcharge")]
    public async Task<IActionResult> 한시수요할증조회(CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.한시수요할증조회Async(cancellationToken));

    [HttpPut("temporary-demand-surcharge")]
    public async Task<IActionResult> 한시수요할증적용(
        [FromBody] 음식배달한시수요할증적용요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.한시수요할증적용Async(
            request,
            CurrentAdminId(),
            cancellationToken));

    private string CurrentAdminId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.Identity?.Name
           ?? "unknown-admin";
}
