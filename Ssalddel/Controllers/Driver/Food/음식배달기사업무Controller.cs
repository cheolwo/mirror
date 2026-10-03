using Ssalddel.ApiMetadata;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Application.Driver.Work;
using Ssalddel.Controllers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Food;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Ssalddel.Filters;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;

namespace Ssalddel.Controllers.Driver.Food;

[SsalddelApiVersion(
    SsalddelProductVersion.V3_0,
    FeatureKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[RequireVersionFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelApiCapability(SsalddelCapability.FoodDelivery)]
[SsalddelApiOperation(SsalddelOperation.Execute)]
[ApiController]
[Authorize(Roles = 역할명.기사)]
[Route("api/v1/driver/food-deliveries")]
[SsalddelApiContractName("FoodDeliveryDriverController")]
public sealed class 음식배달기사업무Controller : DriverControllerBase
{
    private readonly IFoodDeliveryDriverWorkspaceUseCase _업무공간UseCase;
    private readonly IFoodDeliveryDriverRouteService _경로Service;
    private readonly I음식배달기사업무Service _음식배달기사업무Service;
    private readonly ISender _sender;

    public 음식배달기사업무Controller(
        IFoodDeliveryDriverWorkspaceUseCase 업무공간UseCase,
        IFoodDeliveryDriverRouteService 경로Service,
        I음식배달기사업무Service 음식배달기사업무Service,
        ISender sender)
    {
        _업무공간UseCase = 업무공간UseCase;
        _경로Service = 경로Service;
        _음식배달기사업무Service = 음식배달기사업무Service;
        _sender = sender;
    }

    [HttpGet("workspace")]
    [SsalddelApiContractName("GetWorkspace")]
    public async Task<IActionResult> 업무공간조회(CancellationToken cancellationToken)
        => Ok(await _업무공간UseCase.GetAsync(CurrentDriverId(), cancellationToken));

    [HttpGet("settlements/daily")]
    [SsalddelApiContractName("GetDailySettlement")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 당일정산조회(
        [FromQuery] string? date,
        CancellationToken cancellationToken)
    {
        DateOnly? completionDate = null;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
                return BadRequest(new { code = "FoodDelivery.InvalidSettlementDate", message = "날짜는 yyyy-MM-dd 형식으로 입력해 주세요." });
            completionDate = parsed;
        }
        // 요청에 기사 ID를 받지 않습니다. 현재 인증된 기사 본인 원장만 조회합니다.
        var driverId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(driverId)) return Unauthorized();
        try
        {
            return Ok(await _업무공간UseCase.GetDailySettlementAsync(driverId, completionDate, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "FoodDelivery.InvalidSettlementDate", message = ex.Message });
        }
    }

    [HttpGet("work/status")]
    [SsalddelApiContractName("GetWorkStatus")]
    public async Task<IActionResult> 운행상태조회(CancellationToken cancellationToken)
        => Ok(await _sender.Send(
            new 운행상태조회Query(CurrentDriverId()),
            cancellationToken));

    [HttpPost("work/start")]
    [SsalddelApiContractName("StartWork")]
    public async Task<IActionResult> 운행시작(
        [FromBody] 기사운행시작요청 요청,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new 운행시작Command(
                CurrentDriverId(),
                요청.시작모드,
                요청.시작시각,
                요청.시작위치,
                요청.복귀지,
                요청.오늘의복귀지주소,
                요청.오늘의복귀지위도,
                요청.오늘의복귀지경도,
                요청.기본복귀지사용,
                요청.복귀지출처,
                요청.복귀콜선호,
                커뮤니티운행공개: false,
                커뮤니티구단위위치공개동의: false,
                운송실행유형: 운송실행유형코드.음식배달),
            cancellationToken);

        return result.IsFailed
            ? this.ToProblemActionResult(result.Errors.Select(x => x.Message))
            : CreatedAtAction(nameof(운행상태조회), result.Value);
    }

    [HttpPost("work/stop")]
    [SsalddelApiContractName("StopWork")]
    public async Task<IActionResult> 운행종료(CancellationToken cancellationToken)
    {
        await _sender.Send(new 운행종료Command(CurrentDriverId()), cancellationToken);
        return NoContent();
    }

    [HttpPost("work/location")]
    [SsalddelApiContractName("UpdateLocation")]
    public async Task<IActionResult> 위치갱신(
        [FromBody] 기사위치갱신요청 요청,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new 위치갱신Command(
                CurrentDriverId(),
                요청.AppKey,
                요청.위도,
                요청.경도,
                요청.정확도_m,
                요청.상차접근허용반경Km,
                요청.운행상태,
                요청.기록시각),
            cancellationToken);

        return result.IsFailed
            ? this.ToProblemActionResult(result.Errors.Select(x => x.Message))
            : Ok(result.Value);
    }

    [HttpGet("offers")]
    [SsalddelApiContractName("GetOffers")]
    public async Task<IActionResult> 제안목록조회(CancellationToken cancellationToken)
        => Ok(await _음식배달기사업무Service.제안조회Async(CurrentDriverId(), cancellationToken));

    [HttpPost("offers/{offerId}/accept")]
    [SsalddelApiContractName("Accept")]
    public async Task<IActionResult> 제안수락(
        [FromRoute(Name = "offerId")] string 제안Id,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _음식배달기사업무Service.수락Async(
            CurrentDriverId(),
            제안Id,
            cancellationToken));

    [HttpPost("offers/{offerId}/reject")]
    [SsalddelApiContractName("Reject")]
    public async Task<IActionResult> 제안거절(
        [FromRoute(Name = "offerId")] string 제안Id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FoodDeliveryOfferRejectRequest? 요청,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _음식배달기사업무Service.거절Async(
            CurrentDriverId(),
            제안Id,
            요청?.ReasonCode,
            cancellationToken));

    [HttpPost("bundles/accept")]
    [SsalddelApiContractName("AcceptBundle")]
    public async Task<IActionResult> 묶음제안수락(
        [FromBody] FoodDeliveryBundleAcceptRequest 요청,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _음식배달기사업무Service.묶음수락Async(
            CurrentDriverId(),
            요청?.OfferIds ?? [],
            cancellationToken));

    [HttpPost("offers/{offerId}/pickup-complete")]
    [SsalddelApiContractName("ConfirmPickup")]
    public async Task<IActionResult> 픽업완료(
        [FromRoute(Name = "offerId")] string 제안Id,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _음식배달기사업무Service.픽업완료Async(
            CurrentDriverId(),
            제안Id,
            cancellationToken));

    [HttpPost("offers/{offerId}/restaurant-arrival")]
    [SsalddelApiContractName("RecordRestaurantArrival")]
    public async Task<IActionResult> 가게도착(
        [FromRoute(Name = "offerId")] string 제안Id,
        [FromBody] 음식배달가게도착요청 요청,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _음식배달기사업무Service.가게도착Async(
            CurrentDriverId(),
            제안Id,
            요청,
            cancellationToken));

    [HttpPost("offers/{offerId}/interruption")]
    [SsalddelApiContractName("InterruptDelivery")]
    public async Task<IActionResult> 배달중단(
        [FromRoute(Name = "offerId")] string 제안Id,
        [FromBody] 음식배달중단요청 요청,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _음식배달기사업무Service.중단Async(
            CurrentDriverId(),
            제안Id,
            요청,
            cancellationToken));

    [HttpPost("offers/{offerId}/delivery-complete")]
    [SsalddelApiContractName("Complete")]
    public async Task<IActionResult> 전달완료(
        [FromRoute(Name = "offerId")] string 제안Id,
        CancellationToken cancellationToken)
        => this.ToActionResult(await _음식배달기사업무Service.전달완료Async(
            CurrentDriverId(),
            제안Id,
            cancellationToken));

    [HttpPost("route")]
    [SsalddelApiContractName("GetRoute")]
    public async Task<IActionResult> 경로조회(
        [FromBody] FoodDeliveryDriverRouteRequestDto 요청,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _경로Service.GetRouteAsync(요청, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new FoodDeliveryDriverActionResultDto
            {
                IsSuccess = false,
                Message = ex.Message
            });
        }
    }

}
