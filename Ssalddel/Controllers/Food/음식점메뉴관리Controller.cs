using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Filters;
using Ssalddel.Security;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Food;

[SsalddelApiVersion(SsalddelProductVersion.V3_0, FeatureKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow, WorkflowKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[RequireVersionFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[ApiController]
[Authorize(Policy = "음식점운영자전용")]
[Route("api/v1/restaurant/menus")]
public sealed class 음식점메뉴관리Controller(I음식점메뉴관리UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<음식점메뉴관리응답>>> 목록(CancellationToken cancellationToken)
        => 현재음식점Id() is { } restaurantId
            ? Ok(await useCase.목록Async(restaurantId, cancellationToken))
            : Forbid();

    [HttpPost]
    public async Task<ActionResult<음식점메뉴관리응답>> 등록(
        [FromBody] 음식점메뉴등록요청 request,
        CancellationToken cancellationToken)
    {
        if (현재음식점Id() is not { } restaurantId) return Forbid();
        try
        {
            var result = await useCase.등록Async(restaurantId, request, cancellationToken);
            return result.새로생성됨
                ? CreatedAtAction(nameof(목록), result)
                : Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (음식점메뉴명충돌Exception ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{menuId:long}")]
    public async Task<ActionResult<음식점메뉴관리응답>> 수정(
        long menuId,
        [FromBody] 음식점메뉴수정요청 request,
        CancellationToken cancellationToken)
    {
        if (현재음식점Id() is not { } restaurantId) return Forbid();
        try
        {
            var result = await useCase.수정Async(restaurantId, menuId, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (DbUpdateConcurrencyException ex) { return Conflict(new { message = ex.Message }); }
        catch (음식점메뉴명충돌Exception ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private long? 현재음식점Id() => 음식점접근범위Resolver.음식점Id조회(User);
}
