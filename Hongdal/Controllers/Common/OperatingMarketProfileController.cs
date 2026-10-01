using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[ApiController]
[HongdalApiVersion(HongdalProductVersion.V1_0)]
[Route("api/v1/operations/market-profile")]
public sealed class OperatingMarketProfileController : ControllerBase
{
    private readonly IOperatingMarketRuntimeProfileService _service;

    public OperatingMarketProfileController(IOperatingMarketRuntimeProfileService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<OperatingMarketRuntimeProfileResponse> Get()
        => Ok(_service.GetCurrent());
}
