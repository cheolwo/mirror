using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Participation,
    HongdalModuleKind.Api,
    "원장·다이어그램 대화방과 메시지 조회 HTTP 경계",
    ReleaseStage = HongdalCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "참여자와 공개 범위를 확인하지 않은 비공개 대화 노출을 허용하지 않습니다.")]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[ApiController]
[Route("api/v1/community/diagram-conversations")]
public sealed class 커뮤니티대화Controller : ControllerBase
{
    private readonly I커뮤니티대화UseCase _useCase;

    public 커뮤니티대화Controller(I커뮤니티대화UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ListDiagramConversations(
        [FromQuery] string? communityId,
        [FromQuery] string? ledgerId,
        [FromQuery] string? diagramId,
        [FromQuery] string? participantUserId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _useCase.다이어그램대화방목록Async(
            communityId,
            ledgerId,
            diagramId,
            participantUserId,
            limit,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{roomId}/messages")]
    [AllowAnonymous]
    public async Task<IActionResult> ListDiagramMessages(
        [FromRoute] string roomId,
        [FromQuery] int limit = 80,
        CancellationToken cancellationToken = default)
    {
        var result = await _useCase.다이어그램메시지목록Async(roomId, limit, cancellationToken);
        return this.ToActionResult(result);
    }
}
