using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.공통;

namespace Ssalddel.Controllers.Common;

[ApiController, Authorize, Route(DistributedFulfillmentCodes.BaseRoute + "/evidence")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V3_5)]
[SsalddelApiCapability(SsalddelCapability.WarehouseFulfillment)]
[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelApiAudience(SsalddelActor.WarehouseManager), SsalddelApiAudience(SsalddelActor.Shipper)]
[SsalddelApiAudience(SsalddelActor.Driver), SsalddelApiAudience(SsalddelActor.Orderer)]
[SsalddelApiOperation(SsalddelOperation.Browse), SsalddelApiOperation(SsalddelOperation.Decide)]
[SsalddelApiContractName("DistributedPhotoEvidenceController")]
public sealed class DistributedPhotoEvidenceController(IDistributedFulfillmentUseCase useCase, DistributedPhotoEvidenceService evidence) : ControllerBase
{
    [HttpPost, RequestSizeLimit(8 * 1024 * 1024)]
    public Task<IActionResult> Upload(DistributedPhotoUploadRequest request, CancellationToken ct)
        => Run(async actor => Ok(await evidence.UploadAsync(actor, await useCase.AuthorizeEvidenceAsync(actor, request.Work, ct), request, ct)));
    [HttpGet("{id}")]
    public Task<IActionResult> Metadata(string id, CancellationToken ct) => Run(async a => Ok(await evidence.GetAsync(a, id, ct)));
    [HttpGet("{id}/content")]
    public Task<IActionResult> Content(string id, CancellationToken ct) => Run(async a =>
    { var value = await evidence.ContentAsync(a, id, ct); return File(value.Bytes, value.ContentType); });
    private async Task<IActionResult> Run(Func<DistributedActor, Task<IActionResult>> action)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(id)) return Unauthorized();
        try { return await action(new(id, User.IsInRole(역할명.기사), User.IsInRole(역할명.서버관리자))); }
        catch (DistributedFulfillmentException ex) { return StatusCode(ex.Status, new { errorCode = ex.Code, message = "사진과 현재 업무 정보를 다시 확인해 주세요." }); }
    }
}
