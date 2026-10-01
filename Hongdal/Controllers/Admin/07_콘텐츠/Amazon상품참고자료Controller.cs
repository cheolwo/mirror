using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Admin.Content07;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[ApiController]
[Route("api/v1/admin/content/product-research/amazon")]
[Authorize(Policy = "서버관리자전용")]
public sealed class Amazon상품참고자료Controller : ControllerBase
{
    private readonly IAmazon상품참고자료Service _service;

    public Amazon상품참고자료Controller(IAmazon상품참고자료Service service)
    {
        _service = service;
    }

    [HttpPost("preview")]
    public async Task<ActionResult<Amazon상품참고자료Dto>> 미리보기(
        [FromBody] Amazon상품참고자료조회요청Dto 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.미리보기Async(요청, cancellationToken));
}
