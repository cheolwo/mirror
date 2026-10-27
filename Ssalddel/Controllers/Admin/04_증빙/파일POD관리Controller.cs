using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Evidence;
using Ssalddel.Controllers;
using Ssalddel.ApiMetadata;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Evidence04;

[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[ApiController]
[Route("api/v1/admin/files/pod")]
[Authorize(Policy = "서버관리자전용")]
public sealed class 파일POD관리Controller : ControllerBase
{
    private readonly I파일POD관리UseCase _useCase;

    public 파일POD관리Controller(I파일POD관리UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> 업로드([FromForm] 파일POD업로드요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.업로드Async(
            new 파일POD업로드Command(request?.File, request?.FileType, request?.RequestId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public IActionResult 목록조회([FromQuery] string? fileType, [FromQuery] string? requestId)
    {
        var result = _useCase.목록조회(fileType, requestId);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    public IActionResult 업로드상태변경(Guid id, [FromBody] 파일POD상태변경요청 request)
    {
        var result = _useCase.업로드상태변경(id, request?.UploadStatus);
        return this.ToActionResult(result);
    }
}
