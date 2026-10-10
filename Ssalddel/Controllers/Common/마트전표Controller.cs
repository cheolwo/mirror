using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Contracts.Mart;
using Ssalddel.Controllers;
using Ssalddel.Filters;
using Ssalddel.Security;
using Ssalddel.Services.Operations;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize(Policy = "운영사용자전용")]
[RequireVersionFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[RequireHrRole(HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseDispatchOperator)]
[Route("api/v1/warehouse-operations/mart/{orderReference}/slip")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.SsalddelMartWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.SsalddelMart)]
[SsalddelApiCapability(SsalddelCapability.WarehouseFulfillment)]
[SsalddelApiAudience(SsalddelActor.WarehouseManager)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Record)]
[SsalddelApiContractName("MartPackageSlipController")]
public sealed class 마트전표Controller(I마트기사업무Service service) : SsalddelControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 조회(string orderReference, CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : this.ToActionResult(await service.전표조회Async(orderReference, ct));

    [HttpPost]
    public async Task<IActionResult> 발급(string orderReference, [FromBody] 마트포장전표발급요청 request, CancellationToken ct)
        => string.IsNullOrWhiteSpace(CurrentUserId) ? Unauthorized()
            : this.ToActionResult(await service.전표발급Async(orderReference, request, ct));
}
