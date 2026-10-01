using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Admin.Master06;

[ApiController]
[HongdalApiVersion(HongdalProductVersion.V1_0)]
[Authorize(Policy = "서버관리자전용")]
[Route("api/v1/admin/operations/third-party-logistics/outreach")]
public sealed class ThirdPartyLogisticsProviderOutreachController : ControllerBase
{
    private readonly IThirdPartyLogisticsProviderOutreachPreparationService _service;

    public ThirdPartyLogisticsProviderOutreachController(
        IThirdPartyLogisticsProviderOutreachPreparationService service)
    {
        _service = service;
    }

    [HttpPost("preview")]
    public ActionResult<ThirdPartyLogisticsProviderOutreachPreparationResponse>
        Preview([FromBody] PrepareThirdPartyLogisticsProviderOutreachRequest request)
    {
        var response = _service.Prepare(request);
        if (response.Success)
        {
            return Ok(response);
        }

        return response.ErrorCode ==
               ThirdPartyLogisticsProviderOutreachErrorCodes
                   .MarketNotAvailableInDeployment
            ? NotFound(response)
            : BadRequest(response);
    }
}
