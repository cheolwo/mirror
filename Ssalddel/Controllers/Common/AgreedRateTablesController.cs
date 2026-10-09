using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Security;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_0)]
[SsalddelApiCapability(SsalddelCapability.Settlement)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelApiAudience(SsalddelActor.Driver)]
[SsalddelApiAudience(SsalddelActor.Seller)]
[SsalddelApiAudience(SsalddelActor.Shipper)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[SsalddelApiOperation(SsalddelOperation.Decide)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.CoreLogistics)]
[Route("api/v1/agreed-rate-tables")]
[SsalddelApiContractName("AgreedRateTablesController")]
public sealed class AgreedRateTablesController(IAgreedRateTableUseCase useCase, IAuthorizationService authorization,
    IVersionFeatureFlagService featureFlags) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] string partyKind, CancellationToken ct)
        => Run(partyKind, actor => useCase.ListAsync(actor, ct));
    [HttpGet("{id}")]
    public Task<IActionResult> Get(string id, [FromQuery] string partyKind, CancellationToken ct)
        => Run(partyKind, actor => useCase.GetAsync(id, actor, ct));
    [HttpPost]
    public Task<IActionResult> Propose([FromBody] AgreedRateTableProposalRequest request, CancellationToken ct)
        => Run(request.ActorPartyKind, actor => useCase.ProposeAsync(actor, request, ct));
    [HttpPost("{id}/actions")]
    public Task<IActionResult> Act(string id, [FromBody] AgreedRateTableActionRequest request, CancellationToken ct)
        => Run(request.ActorPartyKind, actor => useCase.ActAsync(id, actor, request, ct));
    [HttpPost("{id}/preview")]
    public Task<IActionResult> Preview(string id, [FromBody] AgreedRateTablePreviewRequest request, CancellationToken ct)
        => Run(request.ActorPartyKind, actor => useCase.PreviewAsync(id, actor, request, ct));
    [HttpPost("{id}/selections")]
    public Task<IActionResult> Select(string id, [FromBody] AgreedRateTableSelectionRequest request, CancellationToken ct)
        => Run(request.ActorPartyKind, actor => useCase.SelectAsync(id, actor, request, ct));
    [HttpGet("{id}/selections/{workId}")]
    public Task<IActionResult> Selection(string id, string workId, [FromQuery] string partyKind, CancellationToken ct)
        => Run(partyKind, actor => useCase.GetSelectionAsync(id, workId, actor, ct));

    private async Task<IActionResult> Run<T>(string partyKind, Func<AgreedRatePartyDto, Task<T>> operation)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        AgreedRatePartyDto actor;
        if (partyKind == AgreedRatePartyKinds.Restaurant)
        {
            if (!(await authorization.AuthorizeAsync(User, null, "음식점운영자전용")).Succeeded
                || 음식점접근범위Resolver.음식점Id조회(User) is not { } id) return Forbid();
            actor = new(partyKind, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        else if (partyKind is AgreedRatePartyKinds.FoodDriver or AgreedRatePartyKinds.CargoDriver)
        {
            if (!User.IsInRole(역할명.기사)) return Forbid(); actor = new(partyKind, userId);
        }
        else if (partyKind == AgreedRatePartyKinds.Shipper) actor = new(partyKind, userId);
        else return Forbid();
        var feature = partyKind is AgreedRatePartyKinds.Restaurant or AgreedRatePartyKinds.FoodDriver
            ? VersionFeatureFlagKeys.FoodDeliveryWorkflow : VersionFeatureFlagKeys.DomesticTransportWorkflow;
        if (!featureFlags.IsEnabled(feature)) return NotFound(new { errorCode = "FeatureDisabled", featureKey = feature,
            message = "현재 버전에서 비활성화된 기능입니다." });
        try { return Ok(await operation(actor)); }
        catch (AgreedRateTableException ex) { return StatusCode(ex.StatusCode, new { errorCode = ex.Code, message = "요금표의 권한·판본·적용 조건을 다시 확인해 주세요." }); }
        catch (ArgumentException) { return BadRequest(new { errorCode = "RateTableTermsInvalid", message = "요금·거리·유효기간을 확인해 주세요." }); }
    }
}
