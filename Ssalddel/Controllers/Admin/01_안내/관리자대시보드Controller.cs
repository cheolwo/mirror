using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Ssalddel.Application.Admin.Dashboard;
using Ssalddel.ApiMetadata;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Home01;

[SsalddelApiVersion(
    SsalddelProductVersion.V1_0,
    FeatureKey = VersionFeatureFlagKeys.DomesticTransportWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.DomesticTransportWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Policy = "서버관리자전용")]
public sealed class 관리자대시보드Controller : ControllerBase
{
    private readonly ISender _sender;

    public 관리자대시보드Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> 요약조회()
    {
        var result = await _sender.Send(new 관리자대시보드요약조회Query());
        return Ok(result);
    }
}
