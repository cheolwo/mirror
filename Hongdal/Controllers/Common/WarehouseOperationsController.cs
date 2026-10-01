using Hongdal.Application.Warehouse;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Hr;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Contracts.Common.Warehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hongdal.Security;
using System.Security.Claims;
using Hongdal.Controllers;
using Hongdal.ApiMetadata;
using 홍달.Services.Versioning;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(
    HongdalProductVersion.V1_5,
    FeatureKey = VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
[HongdalApiWorkflow(HongdalWorkflow.WarehouseFulfillment)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Warehouse)]
[ApiController]
[Authorize(Policy = "운영사용자전용")]
[Route("api/v1/warehouse-operations")]
public sealed class WarehouseOperationsController : ControllerBase
{
    private readonly I창고작업UseCase _useCase;

    public WarehouseOperationsController(I창고작업UseCase useCase)
    {
        _useCase = useCase;
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

    [HttpPost("inbounds")]
    [RequireHrRole(
        HrDetailedRoleCodes.WarehouseManager,
        HrDetailedRoleCodes.WarehouseInboundOperator)]
    public async Task<IActionResult> 입고생성([FromBody] 입고요청저장요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.입고생성Async(request, 요청Context생성(), cancellationToken);
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

}
