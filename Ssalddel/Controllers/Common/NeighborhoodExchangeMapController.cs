using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Common;

[ApiController]
[AllowAnonymous]
[Route(NeighborhoodExchangeMapRoutes.Api)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.Community)]
[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Content, SsalddelModuleKind.Api,
    "공개 동네 대표점과 제공·필요 글의 지도 집계 HTTP 경계",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.Persistence,
    Boundary = "공개 동네와 공개 글만 조회하며 배송 주소·GPS·물품의 정확 좌표를 수집하거나 반환하지 않습니다.")]
public sealed class 생활교류지도Controller(I생활교류지도조회UseCase useCase) : CommunityControllerBase
{
    [HttpGet("regions")]
    [SsalddelApiContractName("ListNeighborhoodRegions")]
    public async Task<IActionResult> 동네목록(CancellationToken cancellationToken)
        => Ok(await useCase.공개지역Async(cancellationToken));

    [HttpGet]
    [SsalddelApiContractName("GetNeighborhoodMap")]
    public async Task<IActionResult> 지도([FromQuery] NeighborhoodExchangeMapQuery query, CancellationToken cancellationToken)
        => this.ToActionResult(await useCase.지도Async(query, cancellationToken));

    [HttpGet("posts")]
    [SsalddelApiContractName("ListNeighborhoodPosts")]
    public async Task<IActionResult> 글목록([FromQuery] NeighborhoodExchangeMapQuery query, [FromQuery] int page = 1,
        [FromQuery] int pageSize = NeighborhoodExchange.PageSize, CancellationToken cancellationToken = default)
        => this.ToActionResult(await useCase.글목록Async(query, page, pageSize, cancellationToken));
}
