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
[Route(CargoNoticeRoutes.Api)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.Community)]
[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Content, SsalddelModuleKind.Api,
    "화물 정보 공고의 제한된 공개 조회와 인증한 작성자의 명시 게시·철회",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.Persistence,
    Boundary = "독립 기본 OFF 옵션을 UseCase에서 검사합니다. 운송 의뢰·배차·수납·원장 인계 API가 아닙니다.")]
public sealed class 화물정보공고Controller(I화물정보공고UseCase service) : CommunityControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [SsalddelApiContractName("ListCargoInformationNotices")]
    public async Task<IActionResult> 공개목록([FromQuery] int page = 1, CancellationToken ct = default)
        => this.ToActionResult(await service.공개목록Async(page, ct));

    [AllowAnonymous]
    [HttpGet("regions")]
    [SsalddelApiContractName("ListCargoInformationNoticeRegions")]
    public async Task<IActionResult> 지역목록(CancellationToken ct)
        => this.ToActionResult(await service.지역목록Async(ct));

    [AllowAnonymous]
    [HttpGet("{noticeId}")]
    [SsalddelApiContractName("GetCargoInformationNotice")]
    public async Task<IActionResult> 공개상세(string noticeId, CancellationToken ct)
        => this.ToActionResult(await service.공개상세Async(noticeId, ct));

    [HttpGet("mine")]
    [SsalddelApiContractName("ListMyCargoInformationNotices")]
    public async Task<IActionResult> 내목록([FromQuery] int page = 1, CancellationToken ct = default)
        => this.ToActionResult(await service.내목록Async(page, ct));

    [HttpGet("mine/{noticeId}")]
    [SsalddelApiContractName("GetMyCargoInformationNotice")]
    public async Task<IActionResult> 내상세(string noticeId, CancellationToken ct)
        => this.ToActionResult(await service.내상세Async(noticeId, ct));

    [HttpGet("requests/{requestId:guid}")]
    [SsalddelApiContractName("GetCargoInformationNoticeRequestResult")]
    public async Task<IActionResult> 요청결과(Guid requestId, [FromQuery] string? noticeId, CancellationToken ct)
        => this.ToActionResult(await service.요청결과Async(requestId, noticeId, ct));

    [HttpPost]
    [SsalddelApiContractName("CreateCargoInformationNoticeDraft")]
    public async Task<IActionResult> 초안생성([FromBody] CargoNoticeDraftRequest request, CancellationToken ct)
        => this.ToActionResult(await service.초안생성Async(request, ct));

    [HttpPut("{noticeId}")]
    [SsalddelApiContractName("UpdateCargoInformationNoticeDraft")]
    public async Task<IActionResult> 수정(string noticeId, [FromBody] CargoNoticeDraftRequest request, CancellationToken ct)
        => this.ToActionResult(await service.수정Async(noticeId, request, ct));

    [HttpPost("{noticeId}/publish")]
    [SsalddelApiContractName("PublishCargoInformationNotice")]
    public async Task<IActionResult> 공개(string noticeId, [FromBody] CargoNoticePublishRequest request, CancellationToken ct)
        => this.ToActionResult(await service.공개Async(noticeId, request, ct));

    [HttpPost("{noticeId}/withdraw")]
    [SsalddelApiContractName("WithdrawCargoInformationNotice")]
    public async Task<IActionResult> 철회(string noticeId, [FromBody] CargoNoticeMutationRequest request, CancellationToken ct)
        => this.ToActionResult(await service.철회Async(noticeId, request, ct));
}
