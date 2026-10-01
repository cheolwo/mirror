using Microsoft.AspNetCore.Mvc;
using Hongdal.Application.Files;
using Hongdal.Controllers;
using Hongdal.ApiMetadata;
using Microsoft.AspNetCore.Authorization;

namespace Hongdal.Controllers.Common
{
[HongdalApiVersion(HongdalProductVersion.V0_0)]
    [ApiController]
    [Route("api/v1/files")]
    public class 파일업로드Controller : ControllerBase
    {
        private readonly I파일업로드UseCase _useCase;

        public 파일업로드Controller(I파일업로드UseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpPost("upload")]
        [Authorize(Policy = "물류운영사용자전용")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> 업로드([FromForm] 파일업로드요청 request, CancellationToken cancellationToken)
        {
            var result = await _useCase.업로드Async(
                new 파일업로드Command(request?.File, request?.CommandName, request?.ReferenceId),
                cancellationToken);

            return this.ToActionResult(result);
        }
    }

}
