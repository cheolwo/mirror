using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[ApiController]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[Route("api/v1/community/dynamic-topic-feeds")]
public sealed class CommunityDynamicTopicFeedsController : ControllerBase
{
    private readonly ICommunityDynamicDiscoveryService _service;

    public CommunityDynamicTopicFeedsController(ICommunityDynamicDiscoveryService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<CommunityDynamicTopicCatalogResponse> GetCatalog()
        => Ok(_service.GetCatalog());

    [HttpGet("{topicKey}")]
    [AllowAnonymous]
    public async Task<ActionResult<CommunityDynamicTopicFeedResponse>> Get(
        string topicKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetFeedAsync(topicKey, page, pageSize, cancellationToken);
        return result is null
            ? NotFound()
            : Ok(result);
    }
}
