using Hongdal.ApiMetadata;
using Hongdal.Services.Education;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/education/courses")]
public sealed class 교육과정Controller : ControllerBase
{
    private readonly I교육과정정의Service _service;

    public 교육과정Controller(I교육과정정의Service service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> 목록조회(CancellationToken cancellationToken)
        => Ok(await _service.목록조회Async(true, cancellationToken));

    [HttpGet("{과정코드}")]
    [AllowAnonymous]
    public async Task<IActionResult> 상세조회(string 과정코드, CancellationToken cancellationToken)
        => Ok(await _service.상세조회Async(과정코드, true, cancellationToken));
}
