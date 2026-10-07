using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Controllers;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[Route(NeighborhoodStorageRoutes.Api)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.Community)]
[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Ledger, SsalddelModuleKind.Api,
    "생활 보관 능력 공개와 합의된 예약·인수·반환 HTTP 경계",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "공개 API는 동네 대표점과 보관 능력만 반환합니다. 상세 주소와 연락처는 본인·동의한 현재 참여자에게만 제공합니다.")]
public sealed class 생활보관공간Controller(I생활보관공간Service service) : CommunityControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [SsalddelApiContractName("ListNeighborhoodStorageSpaces")]
    public async Task<IActionResult> 공개목록([FromQuery] string? regionKey, [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
        => Ok(await service.공개목록Async(regionKey, page, cancellationToken));

    [AllowAnonymous]
    [HttpGet("map")]
    [SsalddelApiContractName("GetNeighborhoodStorageMap")]
    public async Task<IActionResult> 공개지도(CancellationToken cancellationToken)
        => Ok(await service.공개지도Async(cancellationToken));

    [AllowAnonymous]
    [HttpGet("{spaceId}")]
    [SsalddelApiContractName("GetNeighborhoodStorageSpace")]
    public async Task<IActionResult> 공개상세(string spaceId, CancellationToken cancellationToken)
    {
        var result = await service.공개상세Async(spaceId, cancellationToken);
        return result is null ? this.ToNotFoundProblem("공개 중인 보관 공간을 찾을 수 없습니다.") : Ok(result);
    }

    [HttpGet("mine")]
    [SsalddelApiContractName("ListMyNeighborhoodStorageSpaces")]
    public async Task<IActionResult> 내목록([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        NoStore();
        return Ok(await service.내목록Async(page, cancellationToken));
    }

    [HttpGet("{spaceId}/private")]
    [SsalddelApiContractName("GetPrivateNeighborhoodStorageSpace")]
    public async Task<IActionResult> 비공개상세(string spaceId, [FromQuery] string? collaborationId,
        CancellationToken cancellationToken)
    {
        NoStore();
        var result = await service.비공개상세Async(spaceId, collaborationId, cancellationToken);
        return result is null ? this.ToNotFoundProblem("현재 확인할 수 있는 보관 합의를 찾을 수 없습니다.") : Ok(result);
    }

    [HttpGet("requests/{requestId:guid}")]
    [SsalddelApiContractName("GetNeighborhoodStorageRequestResult")]
    public async Task<IActionResult> 요청결과(Guid requestId, [FromQuery] string? spaceId, [FromQuery] string? collaborationId, CancellationToken cancellationToken)
    {
        NoStore();
        var result = await service.요청결과Async(requestId, spaceId, collaborationId, cancellationToken);
        return result is null ? this.ToNotFoundProblem("저장된 요청 결과를 찾을 수 없습니다.") : Ok(result);
    }

    [HttpPost]
    [SsalddelApiContractName("CreateNeighborhoodStorageSpace")]
    public async Task<IActionResult> 등록([FromBody] NeighborhoodStorageSpaceRequest request, CancellationToken cancellationToken)
    {
        NoStore();
        return this.ToActionResult(await service.등록Async(request, cancellationToken));
    }

    [HttpPost("{spaceId}/update")]
    [SsalddelApiContractName("UpdateNeighborhoodStorageSpace")]
    public async Task<IActionResult> 수정(string spaceId, [FromBody] NeighborhoodStorageSpaceRequest request,
        CancellationToken cancellationToken)
    {
        NoStore();
        return this.ToActionResult(await service.수정Async(spaceId, request, cancellationToken));
    }

    [HttpPost("{spaceId}/publish")]
    [SsalddelApiContractName("PublishNeighborhoodStorageSpace")]
    public Task<IActionResult> 공개(string spaceId, [FromBody] NeighborhoodStorageMutationRequest request,
        CancellationToken cancellationToken) => 상태변경(spaceId, NeighborhoodStorageStatus.Published, request, cancellationToken);

    [HttpPost("{spaceId}/pause")]
    [SsalddelApiContractName("PauseNeighborhoodStorageSpace")]
    public Task<IActionResult> 중지(string spaceId, [FromBody] NeighborhoodStorageMutationRequest request,
        CancellationToken cancellationToken) => 상태변경(spaceId, NeighborhoodStorageStatus.Paused, request, cancellationToken);

    [HttpPost("{spaceId}/close")]
    [SsalddelApiContractName("CloseNeighborhoodStorageSpace")]
    public Task<IActionResult> 종료(string spaceId, [FromBody] NeighborhoodStorageMutationRequest request,
        CancellationToken cancellationToken) => 상태변경(spaceId, NeighborhoodStorageStatus.Closed, request, cancellationToken);

    [HttpPost("{spaceId}/reservations")]
    [SsalddelApiContractName("ReserveNeighborhoodStorageSpace")]
    public async Task<IActionResult> 예약(string spaceId, [FromBody] NeighborhoodStorageReservationRequest request,
        CancellationToken cancellationToken)
    {
        NoStore();
        return this.ToActionResult(await service.예약Async(spaceId, request, cancellationToken));
    }

    [HttpPost("{spaceId}/reservations/{collaborationId}/action")]
    [SsalddelApiContractName("ConfirmNeighborhoodStorageHandover")]
    public async Task<IActionResult> 인계(string spaceId, string collaborationId,
        [FromBody] NeighborhoodStorageReservationActionRequest request, CancellationToken cancellationToken)
    {
        NoStore();
        return this.ToActionResult(await service.예약변경Async(spaceId, collaborationId, request, cancellationToken));
    }

    [NonAction]
    private async Task<IActionResult> 상태변경(string spaceId, string status, NeighborhoodStorageMutationRequest request,
        CancellationToken cancellationToken)
    {
        NoStore();
        return this.ToActionResult(await service.상태변경Async(spaceId, status, request, cancellationToken));
    }

    private void NoStore() => Response.Headers.CacheControl = "private, no-store";
}
