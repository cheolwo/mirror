using Ssalddel.Contracts.Common.Metadata;

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
using FluentResults;
using Ssalddel.Contracts.Common.Workflow;
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
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/driver/food-deliveries")]
[SsalddelApiContractName("FoodDeliveryDriverController")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Api,
    "인증된 음식 기사의 조회·운행·배달 동작을 기존 route와 오류 계약으로 연결한다.",
    ContractType = typeof(IFoodDeliveryDriverWorkspaceUseCase),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation | SsalddelCodeEffect.ThirdPartyApiCall | SsalddelCodeEffect.MayIncurExternalCost,
    FlowOrder = 180,
    StepKey = "api.food-driver",
    DependsOnStepKeys = ["application.work-start", "application.work-status", "application.work-stop", "application.work-location", "application.current-work", "application.legacy-workspace", "application.driver-actions", "application.daily-settlement", "application.completed-detail"],
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "workspace/current는 정산 없는 조회이며 호환 workspace·정산은 별도다. 하위 SQL·Mongo·Redis·알림·경로 API 효과를 위임하며 서버 권한·Revision·개인정보와 Operational/Simulation 게이트를 보존한다.")]
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
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId)) return Unauthorized();
        try
        {
            return Ok(await _업무공간UseCase.GetAsync(CurrentDriverId(), cancellationToken));
        }
        catch (FoodDeliverySettlementUnavailableException)
        {
            return ToFoodFailure([new Error("정산 정보를 확인할 수 없습니다. 현재 업무는 계속 확인할 수 있습니다.")
                .WithMetadata("StatusCode", StatusCodes.Status503ServiceUnavailable)
                .WithMetadata("ErrorCode", FoodDeliverySettlementUnavailableException.Code)]);
        }
    }

    [HttpGet("workspace/current")]
    [SsalddelApiContractName("GetCurrentWork")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> 현재업무조회(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId)) return Unauthorized();
        return Ok(await _업무공간UseCase.GetCurrentWorkAsync(CurrentDriverId(), cancellationToken));
    }

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

    [HttpGet("settlements/{settlementId}/detail")]
    [SsalddelApiContractName("GetCompletedDeliveryDetail")]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> 완료배달상세조회(
        [FromRoute] string settlementId, CancellationToken cancellationToken)
    {
        // 브라우저·중간 proxy에 완료 고객 정보를 보관하지 않습니다. 실패 응답에도 동일하게 적용합니다.
        Response.Headers.CacheControl = "no-store";
        var driverId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(driverId)) return Unauthorized();
        var result = await _업무공간UseCase.GetCompletedDeliveryDetailAsync(driverId, settlementId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
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
                운송실행유형: 운송실행유형코드.음식배달,
                ClientRequestId: 요청.ClientRequestId),
            cancellationToken);

        return result.IsFailed
            ? ToFoodFailure(result.Errors)
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
            ? ToFoodFailure(result.Errors)
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
        => ToFoodActionResult(await _음식배달기사업무Service.수락Async(
            CurrentDriverId(),
            제안Id,
            cancellationToken));

    [HttpPost("offers/{offerId}/reject")]
    [SsalddelApiContractName("Reject")]
    public async Task<IActionResult> 제안거절(
        [FromRoute(Name = "offerId")] string 제안Id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FoodDeliveryOfferRejectRequest? 요청,
        CancellationToken cancellationToken)
        => ToFoodActionResult(await _음식배달기사업무Service.거절Async(
            CurrentDriverId(),
            제안Id,
            요청?.ReasonCode,
            cancellationToken));

    [HttpPost("bundles/accept")]
    [SsalddelApiContractName("AcceptBundle")]
    public async Task<IActionResult> 묶음제안수락(
        [FromBody] FoodDeliveryBundleAcceptRequest 요청,
        CancellationToken cancellationToken)
        => ToFoodActionResult(await _음식배달기사업무Service.묶음수락Async(
            CurrentDriverId(),
            요청?.OfferIds ?? [],
            cancellationToken));

    [HttpPost("offers/{offerId}/pickup-complete")]
    [SsalddelApiContractName("ConfirmPickup")]
    public async Task<IActionResult> 픽업완료(
        [FromRoute(Name = "offerId")] string 제안Id,
        CancellationToken cancellationToken)
        => ToFoodActionResult(await _음식배달기사업무Service.픽업완료Async(
            CurrentDriverId(),
            제안Id,
            cancellationToken));

    [HttpPost("offers/{offerId}/restaurant-arrival")]
    [SsalddelApiContractName("RecordRestaurantArrival")]
    public async Task<IActionResult> 가게도착(
        [FromRoute(Name = "offerId")] string 제안Id,
        [FromBody] 음식배달가게도착요청 요청,
        CancellationToken cancellationToken)
        => ToFoodActionResult(await _음식배달기사업무Service.가게도착Async(
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
        => ToFoodActionResult(await _음식배달기사업무Service.중단Async(
            CurrentDriverId(),
            제안Id,
            요청,
            cancellationToken));

    [HttpPost("offers/{offerId}/delivery-complete")]
    [SsalddelApiContractName("Complete")]
    public async Task<IActionResult> 전달완료(
        [FromRoute(Name = "offerId")] string 제안Id,
        CancellationToken cancellationToken)
        => ToFoodActionResult(await _음식배달기사업무Service.전달완료Async(
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

    private IActionResult ToFoodActionResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : ToFoodFailure(result.Errors);

    private IActionResult ToFoodFailure(IReadOnlyCollection<IError> errors)
    {
        var message = errors.Select(x => x.Message).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                      ?? "요청을 처리할 수 없습니다.";
        object? Metadata(string key) => errors.Select(x => x.Metadata.GetValueOrDefault(key))
            .FirstOrDefault(x => x is not null);
        var classification = Result응답확장.실패분류(message);
        var status = Metadata("StatusCode") is int explicitStatus ? explicitStatus : classification.StatusCode;
        var code = Metadata("ErrorCode") as string ?? classification.Code;
        var revision = Metadata("CurrentRevision") switch { long value => value, int value => value, _ => (long?)null };
        var refresh = status is StatusCodes.Status409Conflict or StatusCodes.Status503ServiceUnavailable;
        return this.To업무실패Problem(message, status, code,
            Metadata("FailureClassCode") as string ?? (status >= 500
                ? 업무실패분류Codes.일시기술장애
                : 업무실패분류Codes.업무규칙차단),
            Metadata("ResponsibilityRoleCode") as string ?? 업무실패책임역할Codes.음식배달운영체제,
            Metadata("RequiresStateRefresh") is bool explicitRefresh ? explicitRefresh : refresh,
            Metadata("RetryPolicyCode") as string ?? (refresh
                ? 업무재시도정책Codes.상태재조회후사용자재시도
                : 업무재시도정책Codes.자동재시도안함),
            Metadata("AvailableRecoveryActions") as IEnumerable<string> ?? (refresh
                ? [업무복구행동Ids.상태전체재조회] : []),
            revision);
    }
}
