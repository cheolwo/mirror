using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Warehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Security;
using System.Security.Claims;
using Ssalddel.Controllers;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Services.Privacy;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[SsalddelApiIntroducedIn(SsalddelProductVersion.V2_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
[SsalddelApiCapability(SsalddelCapability.WarehouseInbound)]
[SsalddelApiCapability(SsalddelCapability.InventoryManagement)]
[SsalddelApiCapability(SsalddelCapability.WarehouseFulfillment)]
[SsalddelApiAudience(SsalddelActor.WarehouseManager)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Execute)]
[SsalddelApiOperation(SsalddelOperation.Record)]
[SsalddelApiOperation(SsalddelOperation.Manage)]
[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.Warehouse)]
[ApiController]
[Authorize(Policy = "운영사용자전용")]
[Route("api/v1/warehouse-operations")]
[SsalddelApiContractName("WarehouseOperationsController")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.WarehouseInboundVertical,
    SsalddelCodeLayer.Api,
    "인증된 창고 작업자의 입고 수령·검수·적재 요청을 기존 운영 UseCase로 전달한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    ContractType = typeof(입고완료요청),
    FlowOrder = 40,
    Boundary = "운영 인증과 창고 접근 범위를 검사하며 Simulation NPC 권한을 대신하지 않는다.")]
public sealed class 창고작업Controller : ControllerBase
{
    private readonly I창고작업UseCase _useCase;
    private readonly I피킹작업UseCase _pickingTaskUseCase;
    private readonly I재고현황UseCase _inventoryOverviewUseCase;
    private readonly I적재작업UseCase _putAwayTaskUseCase;
    private readonly I포장작업UseCase _packingTaskUseCase;
    private readonly I출고인계준비UseCase _outboundHandoffUseCase;
    private readonly I출고예정검토UseCase _outboundPlanReviewUseCase;
    private readonly I출고운송인계완료UseCase _outboundTransportHandoffUseCase;
    private readonly I신청개인정보동의증적Service _applicationPrivacyConsent;
    private readonly I가입고사진신청UseCase? _photoInboundUseCase;

    public 창고작업Controller(
        I창고작업UseCase useCase,
        I피킹작업UseCase pickingTaskUseCase,
        I재고현황UseCase inventoryOverviewUseCase,
        I적재작업UseCase putAwayTaskUseCase,
        I포장작업UseCase packingTaskUseCase,
        I출고인계준비UseCase outboundHandoffUseCase,
        I출고예정검토UseCase outboundPlanReviewUseCase,
        I출고운송인계완료UseCase outboundTransportHandoffUseCase,
        I신청개인정보동의증적Service applicationPrivacyConsent,
        I가입고사진신청UseCase? photoInboundUseCase = null)
    {
        _useCase = useCase;
        _pickingTaskUseCase = pickingTaskUseCase;
        _inventoryOverviewUseCase = inventoryOverviewUseCase;
        _putAwayTaskUseCase = putAwayTaskUseCase;
        _packingTaskUseCase = packingTaskUseCase;
        _outboundHandoffUseCase = outboundHandoffUseCase;
        _outboundPlanReviewUseCase = outboundPlanReviewUseCase;
        _outboundTransportHandoffUseCase = outboundTransportHandoffUseCase;
        _applicationPrivacyConsent = applicationPrivacyConsent;
        _photoInboundUseCase = photoInboundUseCase;
    }

    [HttpPost("inbound-photo-requests")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseInboundOperator)]
    [RequestSizeLimit(5 * 1024 * 1024 + 128 * 1024)]
    public async Task<IActionResult> 가입고사진신청생성(
        [FromForm] Guid clientRequestId, [FromForm(Name = "name")] string? name,
        [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await _photoInboundUseCase!.CreateAsync(clientRequestId, name, file, cancellationToken));
    }

    [HttpGet("inbound-photo-requests")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 가입고사진신청목록([FromQuery] int page = 0, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await _photoInboundUseCase!.GetListAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("inbound-photo-requests/{id:guid}")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 가입고사진신청상세(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await _photoInboundUseCase!.GetAsync(id, cancellationToken));
    }

    [HttpGet("inbound-photo-requests/{id:guid}/photo")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 가입고사진내용(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await _photoInboundUseCase!.GetPhotoAsync(id, cancellationToken));
    }

    [HttpPut("inbound-photo-requests/{id:guid}/name")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 가입고사진이름변경(Guid id, [FromBody] 가입고사진이름변경요청 request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return this.ToActionResult(await _photoInboundUseCase!.RenameAsync(id, request, cancellationToken));
    }

    [HttpGet("warehouses")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator,
        HrDetailedRoleCodes.WarehouseInventoryOperator,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 창고목록(CancellationToken cancellationToken)
    {
        var result = await _useCase.창고목록Async(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("warehouses")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> 창고생성([FromBody] 창고저장요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.창고생성Async(request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("warehouses/{warehouseId:long}")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> 창고수정(long warehouseId, [FromBody] 창고저장요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.창고수정Async(warehouseId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("warehouses/{warehouseId:long}")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> 창고삭제(long warehouseId, CancellationToken cancellationToken)
    {
        var result = await _useCase.창고삭제Async(warehouseId, 요청Context생성(), cancellationToken);
        return this.ToNoContentActionResult(result);
    }

    [HttpGet("warehouses/{warehouseId:long}/users")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> 창고사용자목록(long warehouseId, CancellationToken cancellationToken)
    {
        var result = await _useCase.창고사용자목록Async(warehouseId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("warehouses/{warehouseId:long}/users")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> 창고사용자추가(long warehouseId, [FromBody] 창고사용자저장요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.창고사용자추가Async(warehouseId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("warehouses/{warehouseId:long}/users/{warehouseUserId:long}")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> 창고사용자수정(
        long warehouseId,
        long warehouseUserId,
        [FromBody] 창고사용자저장요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.창고사용자수정Async(
            warehouseId,
            warehouseUserId,
            request,
            요청Context생성(),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("warehouses/{warehouseId:long}/users/{warehouseUserId:long}")]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> 창고사용자삭제(long warehouseId, long warehouseUserId, CancellationToken cancellationToken)
    {
        var result = await _useCase.창고사용자삭제Async(
            warehouseId,
            warehouseUserId,
            요청Context생성(),
            cancellationToken);
        return this.ToNoContentActionResult(result);
    }

    [HttpGet("inbounds")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고목록(CancellationToken cancellationToken)
    {
        var result = await _useCase.입고목록Async(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("inbounds/query")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고목록조회(
        [FromQuery] 입고요청목록조회요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.입고목록조회Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("inbounds/{inboundId:long}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고상세(long inboundId, CancellationToken cancellationToken)
    {
        var result = await _useCase.입고상세Async(inboundId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("inbounds/by-client-request/{clientRequestId:guid}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고원요청조회(Guid clientRequestId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var result = await _useCase.입고원요청조회Async(clientRequestId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("inbounds")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고생성([FromBody] 입고요청저장요청 request, CancellationToken cancellationToken)
    {
        try
        {
            await _applicationPrivacyConsent.유효한동의요구Async(
                request.신청개인정보동의증적Id,
                신청개인정보업무Codes.물류대행,
                request.신청출처Code,
                현재UserId(),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "개인정보 동의를 확인할 수 없습니다.", Detail = ex.Message });
        }

        var result = await _useCase.입고생성Async(request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("inbounds/unplanned-requests")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 현장입고요청생성(
        [FromBody] 현장입고요청등록요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.현장입고요청생성Async(request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("inbounds/{inboundId:long}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고수정(long inboundId, [FromBody] 입고요청저장요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.입고수정Async(inboundId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpDelete("inbounds/{inboundId:long}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고취소(long inboundId, CancellationToken cancellationToken)
    {
        var result = await _useCase.입고취소Async(inboundId, 요청Context생성(), cancellationToken);
        return this.ToNoContentActionResult(result);
    }

    [HttpPost("inbounds/{inboundId:long}/complete")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고완료(long inboundId, [FromBody] 입고완료요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.입고완료Async(inboundId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("inventory")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInventoryOperator)]
    public async Task<IActionResult> 재고목록(CancellationToken cancellationToken)
    {
        var result = await _useCase.재고목록Async(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("inventory/inspection-targets")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고검수대상목록(
        [FromQuery] 입고검수대상목록조회요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.입고검수대상목록Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("inventory/{inboundItemId:long}/inspection-target")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고검수대상상세(long inboundItemId, CancellationToken cancellationToken)
    {
        var result = await _useCase.입고검수대상상세Async(inboundItemId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("inventory/{inboundItemId:long}/inspect")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고검수(long inboundItemId, [FromBody] 입고검수요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.입고검수Async(inboundItemId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("inventory/{inboundItemId:long}/put-away")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInventoryOperator)]
    public async Task<IActionResult> 적재위치배정(long inboundItemId, [FromBody] 적재위치배정요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.적재위치배정Async(inboundItemId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("inventory/{inboundItemId:long}/pack")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 포장작업(long inboundItemId, [FromBody] 포장작업요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.포장작업Async(inboundItemId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("inventory-overview")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInventoryOperator,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 재고현황목록(
        [FromQuery] 창고재고현황목록조회요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryOverviewUseCase.목록Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("inventory-overview/{inboundItemId:long}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInventoryOperator,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 재고현황상세(long inboundItemId, CancellationToken cancellationToken)
    {
        var result = await _inventoryOverviewUseCase.상세Async(inboundItemId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("put-away-tasks")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInventoryOperator)]
    public async Task<IActionResult> 적재작업목록([FromQuery] 적재작업목록조회요청 request, CancellationToken cancellationToken)
    {
        var result = await _putAwayTaskUseCase.목록Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("put-away-tasks/{inboundItemId:long}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInventoryOperator)]
    public async Task<IActionResult> 적재작업상세(long inboundItemId, CancellationToken cancellationToken)
    {
        var result = await _putAwayTaskUseCase.상세Async(inboundItemId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("put-away-tasks/{inboundItemId:long}/complete")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInventoryOperator)]
    public async Task<IActionResult> 적재작업완료(long inboundItemId, [FromBody] 적재작업완료요청 request, CancellationToken cancellationToken)
    {
        var result = await _putAwayTaskUseCase.완료Async(inboundItemId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("packing-tasks")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 포장작업목록([FromQuery] 포장작업목록조회요청 request, CancellationToken cancellationToken)
    {
        var result = await _packingTaskUseCase.목록Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("packing-tasks/{inboundItemId:long}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 포장작업상세(long inboundItemId, CancellationToken cancellationToken)
    {
        var result = await _packingTaskUseCase.상세Async(inboundItemId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("packing-tasks/{inboundItemId:long}/complete")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 포장작업완료(long inboundItemId, [FromBody] 포장작업완료요청 request, CancellationToken cancellationToken)
    {
        var result = await _packingTaskUseCase.완료Async(inboundItemId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("outbound-handoff-tasks")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 출고인계준비목록([FromQuery] 출고인계준비목록조회요청 request, CancellationToken cancellationToken)
    {
        var result = await _outboundHandoffUseCase.목록Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("outbound-handoff-tasks/{inboundItemId:long}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 출고인계준비상세(long inboundItemId, CancellationToken cancellationToken)
    {
        var result = await _outboundHandoffUseCase.상세Async(inboundItemId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("outbound-handoff-tasks/{inboundItemId:long}/complete")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 출고인계준비완료(long inboundItemId, [FromBody] 출고인계준비완료요청 request, CancellationToken cancellationToken)
    {
        var result = await _outboundHandoffUseCase.완료Async(inboundItemId, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("outbound-plan-reviews")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 출고예정검토목록(
        [FromQuery] 출고예정검토목록조회요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _outboundPlanReviewUseCase.목록Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("outbound-plan-reviews/{outboundPlanId:long}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 출고예정검토상세(long outboundPlanId, CancellationToken cancellationToken)
    {
        var result = await _outboundPlanReviewUseCase.상세Async(outboundPlanId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("outbound-plan-reviews/{outboundPlanId:long}/handoff-complete")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 출고운송인계완료(
        long outboundPlanId,
        [FromBody] 출고운송인계완료요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _outboundTransportHandoffUseCase.완료Async(
            outboundPlanId,
            request,
            요청Context생성(),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("picking-tasks")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 피킹작업목록(
        [FromQuery] 피킹작업목록조회요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _pickingTaskUseCase.목록Async(request, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("picking-tasks/{taskKey}")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 피킹작업상세(string taskKey, CancellationToken cancellationToken)
    {
        var result = await _pickingTaskUseCase.상세Async(taskKey, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("picking-tasks/{taskKey}/start")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 피킹작업시작(string taskKey, CancellationToken cancellationToken)
    {
        var result = await _pickingTaskUseCase.시작Async(taskKey, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("picking-tasks/{taskKey}/complete")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 피킹작업완료(
        string taskKey,
        [FromBody] 피킹작업완료요청 request,
        CancellationToken cancellationToken)
    {
        var result = await _pickingTaskUseCase.완료Async(taskKey, request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("inventory/reconsignment")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInventoryOperator,
        HrDetailedRoleCodes.WarehouseDispatchOperator)]
    public async Task<IActionResult> 재위탁운송생성([FromBody] 재고운송의뢰생성요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.재위탁운송생성Async(request, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    private 창고작업요청Context 요청Context생성()
        => new(
            Request.Headers["X-App-Key"].ToString(),
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty,
            User.Identity?.Name ?? string.Empty,
            User.Claims.FirstOrDefault(x => x.Type.EndsWith("role", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
            Request.Path.Value ?? string.Empty,
            HttpContext.TraceIdentifier,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Request.Headers.UserAgent.ToString());

    private string 현재UserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.Identity?.Name
           ?? string.Empty;

}
