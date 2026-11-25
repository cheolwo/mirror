using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.HumanResources;
using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Controllers;
using Ssalddel.Filters;
using Ssalddel.Security;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController, Authorize]
[Route("api/v1/hr/work-availability")]
[SsalddelApiContractName("WorkforceAvailabilityController")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V2_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.HrParticipationWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.HrParticipation)]
[RequireVersionFeature(VersionFeatureFlagKeys.HrParticipationWorkflow)]
public sealed class 근무가능요청Controller(I근무가능요청UseCase useCase) : ControllerBase
{
    [HttpGet("preferences")]
    [SsalddelApiContractName("GetPreferences")]
    public async Task<IActionResult> Preferences(CancellationToken ct)
        => this.ToActionResult(await useCase.동의조회Async(ct));
    [HttpPut("preferences")]
    [SsalddelApiContractName("SetPreferences")]
    public async Task<IActionResult> Preferences(근무연락동의 request, CancellationToken ct)
        => this.ToActionResult(await useCase.동의저장Async(request, ct));
    [HttpGet("inbox")]
    [SsalddelApiContractName("MyInbox")]
    public async Task<IActionResult> Inbox(CancellationToken ct, [FromQuery] int page = 0)
        => this.ToActionResult(await useCase.목록Async(null, ct, page));
    [HttpPost("invitations/{invitationId:guid}/reply")]
    [SsalddelApiContractName("Reply")]
    public async Task<IActionResult> Reply(Guid invitationId, 근무가능답변요청 request, CancellationToken ct)
        => this.ToActionResult(await useCase.답변Async(invitationId, request, false, ct));
    [HttpGet("warehouses/{warehouseId:long}/context")]
    [SsalddelApiContractName("ManagerContext")]
    [Authorize(Policy = "운영사용자전용")]
    [SsalddelApiFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    [RequireVersionFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> Context(long warehouseId, [FromQuery] 근무가능업무범위 request, CancellationToken ct)
    {
        request.WarehouseId = warehouseId;
        return this.ToActionResult(await useCase.문맥Async(request, ct));
    }
    [HttpGet("warehouses/{warehouseId:long}/requests")]
    [SsalddelApiContractName("ManagerRequests")]
    [Authorize(Policy = "운영사용자전용")]
    [SsalddelApiFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    [RequireVersionFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> Requests(long warehouseId, CancellationToken ct, [FromQuery] int page = 0)
        => this.ToActionResult(await useCase.목록Async(warehouseId, ct, page));
    [HttpPost("warehouses/{warehouseId:long}/requests")]
    [SsalddelApiContractName("CreateRequest")]
    [Authorize(Policy = "운영사용자전용")]
    [SsalddelApiFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    [RequireVersionFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> Create(long warehouseId, 근무가능요청등록요청 request, CancellationToken ct)
    {
        if (request.WarehouseId != warehouseId) return BadRequest("주소와 요청의 창고가 다릅니다.");
        return this.ToActionResult(await useCase.생성Async(request, ct));
    }
    [HttpPost("warehouses/{warehouseId:long}/invitations/{invitationId:guid}/confirm")]
    [SsalddelApiContractName("ConfirmPlan")]
    [Authorize(Policy = "운영사용자전용")]
    [SsalddelApiFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    [RequireVersionFeature(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)]
    [RequireHrRole(HrDetailedRoleCodes.WarehouseManager)]
    public async Task<IActionResult> Confirm(long warehouseId, Guid invitationId, 근무가능답변요청 request, CancellationToken ct)
        => this.ToActionResult(await useCase.답변Async(invitationId, request, true, ct, warehouseId));
}
