using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacySupport;
using Ssalddel.ApiMetadata;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin;

[ApiController, Authorize(Policy = "서버관리자전용"), Route("api/v1/admin/neighborhood-member-deliveries")]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V2_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
[SsalddelApiCapability(SsalddelCapability.TransportRequest)]
[SsalddelApiAudience(SsalddelActor.PlatformOperator)]
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.CoreLogistics)]
public sealed class NeighborhoodMemberDeliveryRecoveryController(NeighborhoodMemberDeliveryUseCase service, I보호지원Store cases) : ControllerBase
{
    [HttpGet("{requestId}/recovery"), SsalddelApiOperation(SsalddelOperation.Browse)]
    public async Task<IActionResult> Recovery(string requestId, [FromQuery] NeighborhoodMemberRecoveryRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        try { return Ok(await service.RecoveryContextAsync(requestId, request, cases, ct)); }
        catch (NeighborhoodMemberDeliveryException ex)
        { var p = new ProblemDetails { Status = ex.StatusCode, Title = ex.Message }; p.Extensions["code"] = ex.Code; return StatusCode(ex.StatusCode, p); }
    }

    [HttpPost("{requestId}/recover"), SsalddelApiOperation(SsalddelOperation.Decide)]
    public async Task<IActionResult> Recover(string requestId, NeighborhoodMemberRecoveryRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        try { return Ok(await service.RecoverAsync(requestId, request, cases, ct)); }
        catch (NeighborhoodMemberDeliveryException ex)
        { var p = new ProblemDetails { Status = ex.StatusCode, Title = ex.Message }; p.Extensions["code"] = ex.Code; return StatusCode(ex.StatusCode, p); }
    }
}
