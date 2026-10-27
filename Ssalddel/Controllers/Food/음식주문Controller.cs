using Ssalddel.ApiMetadata;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Ssalddel.Security;
using Ssalddel.Services.Food;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Food;

[SsalddelApiVersion(SsalddelProductVersion.V3_0, FeatureKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow, WorkflowKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.FoodDelivery)]
[RequireVersionFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[ApiController]
[Route("api/v1/food-orders")]
public sealed class 음식주문Controller(
    I음식주문접수UseCase commandUseCase,
    I주문자음식주문조회UseCase readUseCase,
    I음식점음식주문조회UseCase restaurantReadUseCase,
    I음식점조리시간Service? preparationTimeService = null,
    I음식자동수행UseCase? autoFulfillment = null,
    I음식주문취소합의UseCase? cancellationAgreements = null) : ControllerBase
{
    [HttpGet("{orderNo}/cancellation-agreements")]
    [Authorize]
    public Task<IActionResult> 취소합의목록(string orderNo, CancellationToken ct)
        => 취소합의응답Async(async () => await cancellationAgreements!.목록Async(orderNo, ct));

    [HttpGet("{orderNo}/cancellation-agreements/{agreementId}")]
    [Authorize]
    public Task<IActionResult> 취소합의조회(string orderNo, string agreementId, [FromQuery] Guid? clientRequestId, CancellationToken ct)
        => 취소합의응답Async(async () => await cancellationAgreements!.조회Async(orderNo, agreementId, clientRequestId, ct));

    [HttpPost("{orderNo}/cancellation-agreements")]
    [Authorize]
    public Task<IActionResult> 취소합의제안(string orderNo, [FromBody] 음식주문취소합의제안요청 request, CancellationToken ct)
        => 취소합의응답Async(async () => await cancellationAgreements!.제안Async(orderNo, request, ct));

    [HttpPost("{orderNo}/cancellation-agreements/{agreementId}/consents")]
    [Authorize]
    public Task<IActionResult> 취소조건동의(string orderNo, string agreementId, [FromBody] 음식주문취소합의변경요청 request, CancellationToken ct)
        => 취소합의응답Async(async () => await cancellationAgreements!.동의Async(orderNo, agreementId, request, ct));

    [HttpPost("{orderNo}/cancellation-agreements/{agreementId}/apply")]
    [Authorize]
    public Task<IActionResult> 합의취소반영(string orderNo, string agreementId, [FromBody] 음식주문취소합의변경요청 request, CancellationToken ct)
        => 취소합의응답Async(async () => await cancellationAgreements!.실행Async(orderNo, agreementId, request, ct));

    private async Task<IActionResult> 취소합의응답Async(Func<Task<object?>> execute)
    {
        NoStore();
        if (cancellationAgreements is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        try { var result = await execute(); return result is null ? NotFound() : Ok(result); }
        catch (음식주문취소합의Exception ex)
        {
            var problem = new ProblemDetails { Title = ex.Message, Status = ex.Status };
            problem.Extensions["errorCode"] = ex.Code;
            return StatusCode(ex.Status, problem);
        }
    }

    [HttpPost("fulfillment-quote")]
    [Authorize]
    public async Task<IActionResult> 자동수행조건조회([FromBody] 음식주문등록요청 request, CancellationToken ct)
    {
        NoStore();
        if (string.IsNullOrWhiteSpace(현재사용자Id())) return Forbid();
        return autoFulfillment is null ? StatusCode(503) : this.ToActionResult(await autoFulfillment.조건조회Async(request.음식점Id, ct));
    }

    [HttpGet("{orderNo}/fulfillment")]
    [Authorize]
    public async Task<IActionResult> 자동수행조회(string orderNo, CancellationToken ct)
    {
        NoStore();
        var owner = 현재사용자Id();
        if (string.IsNullOrWhiteSpace(owner)) return Forbid();
        return autoFulfillment is null ? StatusCode(503) : this.ToActionResult(await autoFulfillment.상태Async(orderNo, owner, ct));
    }

    [HttpPost("{orderNo}/fulfillment-authorization")]
    [Authorize]
    public async Task<IActionResult> 자동수행동의(string orderNo, [FromBody] AutoFulfillmentConsent request, CancellationToken ct)
    {
        NoStore();
        var owner = 현재사용자Id();
        if (string.IsNullOrWhiteSpace(owner)) return Forbid();
        return autoFulfillment is null ? StatusCode(503) : this.ToActionResult(await autoFulfillment.동의Async(orderNo, owner, request, ct));
    }

    [HttpPost("{orderNo}/fulfillment-refresh")]
    [Authorize]
    public async Task<IActionResult> 자동수행재확인(string orderNo, [FromBody] AutoFulfillmentActionRequest request, CancellationToken ct)
    {
        NoStore();
        var owner = 현재사용자Id();
        if (string.IsNullOrWhiteSpace(owner)) return Forbid();
        return autoFulfillment is null ? StatusCode(503) : this.ToActionResult(await autoFulfillment.재확인Async(orderNo, owner, request, ct));
    }

    [HttpPost("{orderNo}/fulfillment-cancellation")]
    [Authorize]
    public async Task<IActionResult> 자동수행취소(string orderNo, [FromBody] AutoFulfillmentActionRequest request, CancellationToken ct)
    {
        NoStore();
        var owner = 현재사용자Id();
        if (string.IsNullOrWhiteSpace(owner)) return Forbid();
        return autoFulfillment is null ? StatusCode(503) : this.ToActionResult(await autoFulfillment.수행취소Async(orderNo, owner, request, ct));
    }

    [HttpGet("restaurant/fulfillment-settings")]
    [Authorize(Policy = "음식점운영자전용")]
    public async Task<IActionResult> 자동수행식당설정조회(CancellationToken ct)
    {
        NoStore();
        var restaurant = 현재음식점Id();
        if (restaurant is null) return Forbid();
        return autoFulfillment is null ? StatusCode(503) : this.ToActionResult(await autoFulfillment.식당설정조회Async(restaurant.Value, ct));
    }

    [HttpPut("restaurant/fulfillment-settings")]
    [Authorize(Policy = "음식점운영자전용")]
    public async Task<IActionResult> 자동수행식당설정저장([FromBody] RestaurantFulfillmentSupportRequest request, CancellationToken ct)
    {
        NoStore();
        var restaurant = 현재음식점Id(); var actor = 현재사용자Id();
        if (restaurant is null || string.IsNullOrWhiteSpace(actor)) return Forbid();
        return autoFulfillment is null ? StatusCode(503) : this.ToActionResult(await autoFulfillment.식당설정저장Async(restaurant.Value, actor, request, ct));
    }

    private void NoStore() => Response.Headers.CacheControl = "private, no-store";

    [HttpGet("client-requests/{requestId:guid}")]
    [Authorize]
    public async Task<IActionResult> 접수결과조회(Guid requestId, CancellationToken cancellationToken)
        => this.ToActionResult(await readUseCase.접수결과Async(requestId, 현재사용자Id(), cancellationToken));

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> 목록조회(
        [FromQuery] 주문자음식주문목록조회요청 request,
        CancellationToken cancellationToken)
        => this.ToActionResult(await readUseCase.목록Async(request, 현재사용자Id(), cancellationToken));

    [HttpGet("{orderNo}")]
    [Authorize]
    public async Task<IActionResult> 상세조회(string orderNo, CancellationToken cancellationToken)
        => this.ToActionResult(await readUseCase.상세Async(orderNo, 현재사용자Id(), cancellationToken));

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<음식주문응답>> 등록([FromBody] 음식주문등록요청 request, CancellationToken cancellationToken)
    {
        request.주문자UserId = 현재사용자Id()
            ?? throw new InvalidOperationException("로그인 사용자 식별자를 확인할 수 없습니다.");
        try
        {
            return Ok(음식배달가능행동Projector.주문자용(
                await commandUseCase.등록Async(request, cancellationToken)));
        }
        catch (Ssalddel.Services.Commerce.거래보호Exception ex)
        {
            var problem = new ProblemDetails { Title = ex.Message, Status = ex.Status };
            problem.Extensions["errorCode"] = ex.Code;
            return StatusCode(ex.Status, problem);
        }
        catch (음식주문메뉴가격변경Exception ex)
        {
            return 주문입력실패(ex.Message, FoodOrderSubmissionErrorCodes.MenuPriceChanged);
        }
        catch (음식주문입력확인Exception ex)
        {
            return 주문입력실패(ex.Message, ex.ErrorCode);
        }
    }

    private BadRequestObjectResult 주문입력실패(string message, string errorCode)
    {
        var problem = new ProblemDetails
        {
            Title = message,
            Status = StatusCodes.Status400BadRequest,
            Type = "https://httpstatuses.com/400",
            Instance = HttpContext?.Request.Path.Value
        };
        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = HttpContext?.TraceIdentifier ?? string.Empty;
        return BadRequest(problem);
    }

    [HttpPost("{orderNo}/receipt-confirmation")]
    [Authorize]
    public async Task<ActionResult<음식주문응답>> 주문자수령확인(
        string orderNo,
        [FromBody] 주문자음식주문수령확인요청 request,
        CancellationToken cancellationToken)
    {
        var ordererUserId = 현재사용자Id();
        if (string.IsNullOrWhiteSpace(ordererUserId))
        {
            return Forbid();
        }

        try
        {
            var order = await commandUseCase.주문자수령확인Async(
                orderNo,
                request,
                ordererUserId,
                cancellationToken);
            return order is null ? NotFound() : Ok(음식배달가능행동Projector.주문자용(order));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderNo}/cancellation")]
    [Authorize]
    public async Task<ActionResult<음식주문응답>> 주문자취소(
        string orderNo,
        [FromBody] 주문자음식주문취소요청 request,
        CancellationToken cancellationToken)
    {
        var ordererUserId = 현재사용자Id();
        if (string.IsNullOrWhiteSpace(ordererUserId))
        {
            return Forbid();
        }

        try
        {
            var order = await commandUseCase.주문자취소Async(
                orderNo,
                request,
                ordererUserId,
                cancellationToken);
            return order is null ? NotFound() : Ok(음식배달가능행동Projector.주문자용(order));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("restaurant/inbox")]
    [Authorize(Policy = "음식점운영자전용")]
    public ActionResult<음식점주문수신함응답> 음식점수신함(
        [FromQuery] 음식점주문수신함조회요청 request)
    {
        if (!string.IsNullOrWhiteSpace(request.처리상태)
            && !음식점주문수신함처리상태코드.전체목록.Contains(
                request.처리상태.Trim(),
                StringComparer.Ordinal))
        {
            return BadRequest(new { message = "처리상태는 미처리, 완료 또는 전체만 사용할 수 있습니다." });
        }

        var restaurantId = 현재음식점Id();
        return restaurantId is null
            ? Forbid()
            : Ok(restaurantReadUseCase.목록(request, restaurantId.Value));
    }

    [HttpGet("restaurant/inbox/{orderNo}")]
    [Authorize(Policy = "음식점운영자전용")]
    public ActionResult<음식주문응답> 음식점상세(string orderNo)
    {
        var restaurantId = 현재음식점Id();
        if (restaurantId is null)
        {
            return Forbid();
        }

        var order = restaurantReadUseCase.상세(orderNo, restaurantId.Value);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("restaurant/preparation-time-settings")]
    [Authorize(Policy = "음식점운영자전용")]
    public async Task<ActionResult<음식점조리시간설정응답>> 음식점조리시간설정조회(CancellationToken cancellationToken)
    {
        var restaurantId = 현재음식점Id();
        if (restaurantId is null || preparationTimeService is null) return Forbid();
        return Ok(await preparationTimeService.설정조회Async(restaurantId.Value, cancellationToken));
    }

    [HttpPut("restaurant/preparation-time-settings")]
    [Authorize(Policy = "음식점운영자전용")]
    public async Task<ActionResult<음식점조리시간설정응답>> 음식점조리시간설정변경(
        [FromBody] 음식점조리시간설정변경요청 request,
        CancellationToken cancellationToken)
    {
        var restaurantId = 현재음식점Id();
        var actorUserId = 현재사용자Id();
        if (restaurantId is null || string.IsNullOrWhiteSpace(actorUserId) || preparationTimeService is null) return Forbid();
        try
        {
            return Ok(await preparationTimeService.설정교체Async(restaurantId.Value, actorUserId, request, cancellationToken));
        }
        catch (DbUpdateConcurrencyException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("restaurant/inbox/{orderNo}/preparation-reference")]
    [Authorize(Policy = "음식점운영자전용")]
    public async Task<ActionResult<음식점조리참고응답>> 음식점조리참고조회(string orderNo, CancellationToken cancellationToken)
    {
        var restaurantId = 현재음식점Id();
        if (restaurantId is null || preparationTimeService is null) return Forbid();
        if (restaurantReadUseCase.상세(orderNo, restaurantId.Value) is null) return NotFound();
        var result = await preparationTimeService.주문참고조회Async(orderNo, restaurantId.Value, DateTime.UtcNow, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{orderNo}/restaurant-acceptance")]
    [Authorize(Policy = "음식점운영자전용")]
    public async Task<ActionResult<음식주문응답>> 음식점수락(
        string orderNo,
        [FromBody] 음식점주문수락요청 request,
        CancellationToken cancellationToken)
    {
        var restaurantId = 현재음식점Id();
        var actorUserId = 현재사용자Id();
        if (restaurantId is null || string.IsNullOrWhiteSpace(actorUserId))
        {
            return Forbid();
        }

        if (restaurantReadUseCase.상세(orderNo, restaurantId.Value) is null)
        {
            return NotFound();
        }

        try
        {
            var order = await commandUseCase.음식점수락Async(
                orderNo,
                request,
                actorUserId,
                cancellationToken);
            return order is null ? NotFound() : Ok(음식배달가능행동Projector.음식점용(order));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderNo}/restaurant-progress")]
    [Authorize(Policy = "음식점운영자전용")]
    public async Task<ActionResult<음식주문응답>> 음식점진행변경(
        string orderNo,
        [FromBody] 음식점주문진행변경요청 request,
        CancellationToken cancellationToken)
    {
        var restaurantId = 현재음식점Id();
        var actorUserId = 현재사용자Id();
        if (restaurantId is null || string.IsNullOrWhiteSpace(actorUserId))
        {
            return Forbid();
        }

        if (restaurantReadUseCase.상세(orderNo, restaurantId.Value) is null)
        {
            return NotFound();
        }

        try
        {
            var order = await commandUseCase.음식점진행변경Async(
                orderNo,
                request,
                actorUserId,
                cancellationToken);
            return order is null ? NotFound() : Ok(음식배달가능행동Projector.음식점용(order));
        }
        catch (InvalidOperationException ex)
        {
            return this.To업무실패Problem(
                ex.Message,
                StatusCodes.Status409Conflict,
                "FoodOrderBusinessRuleBlocked",
                업무실패분류Codes.업무규칙차단,
                업무실패책임역할Codes.음식배달운영체제,
                requiresStateRefresh: true,
                업무재시도정책Codes.상태재조회후사용자재시도,
                [업무복구행동Ids.상태전체재조회],
                restaurantReadUseCase.상세(orderNo, restaurantId.Value)?.Revision);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return this.To업무실패Problem(
                ex.Message,
                StatusCodes.Status409Conflict,
                "FoodOrderRevisionConflict",
                업무실패분류Codes.판본충돌,
                업무실패책임역할Codes.음식배달운영체제,
                requiresStateRefresh: true,
                업무재시도정책Codes.상태재조회후사용자재시도,
                [업무복구행동Ids.상태전체재조회],
                restaurantReadUseCase.상세(orderNo, restaurantId.Value)?.Revision);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string? 현재사용자Id()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.Identity?.Name;

    private long? 현재음식점Id()
        => 음식점접근범위Resolver.음식점Id조회(User);
}
