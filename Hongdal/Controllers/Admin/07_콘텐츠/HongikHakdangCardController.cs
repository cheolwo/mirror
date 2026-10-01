using Hongdal.ApiMetadata;
using Hongdal.Services.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Admin.Content07;

[HongdalApiVersion(HongdalProductVersion.V1_0)]
[ApiController]
[Route("api/v1/admin/content/hongik-hakdang/cards")]
[Authorize(Policy = "서버관리자전용")]
public sealed class HongikHakdangCardController : ControllerBase
{
    private readonly IHongikHakdangCardService _service;
    private readonly IHongikHakdangCardVariantService _variantService;

    public HongikHakdangCardController(
        IHongikHakdangCardService service,
        IHongikHakdangCardVariantService variantService)
    {
        _service = service;
        _variantService = variantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCollections(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetCollectionsAsync(includeInactive, cancellationToken));

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
        => Ok(await _service.SyncAsync(cancellationToken));

    [HttpPost("variants/prepare")]
    public async Task<IActionResult> PrepareVariants(CancellationToken cancellationToken)
        => Ok(await _variantService.EnsureActiveVariantsAsync(cancellationToken));

    [HttpPut("collections/{collectionId:long}/activation")]
    public async Task<IActionResult> SetCollectionActivation(
        long collectionId,
        [FromBody] Hongdal.Contracts.Common.Content.HongikHakdangCardActivationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SetCollectionEnabledAsync(collectionId, request.Enabled, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{cardId:long}/activation")]
    public async Task<IActionResult> SetCardActivation(
        long cardId,
        [FromBody] Hongdal.Contracts.Common.Content.HongikHakdangCardActivationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SetCardEnabledAsync(cardId, request.Enabled, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{cardId:long}/community-publication")]
    public async Task<IActionResult> SetCardCommunityPublication(
        long cardId,
        [FromBody] Hongdal.Contracts.Common.Content.HongikHakdangCardCommunityPublicationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SetCardCommunityPublicationApprovedAsync(
            cardId,
            request.Approved,
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{cardId:long}/image")]
    public async Task<IActionResult> GetCardImage(long cardId, CancellationToken cancellationToken)
    {
        var result = await _service.GetCardImageAsync(cardId, cancellationToken);
        return result is null ? NotFound() : File(result.Bytes, result.ContentType);
    }
}
