using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController, Authorize, Route(NeighborhoodMemberDeliveryRoutes.Api)]
[SsalddelApiIntroducedIn(SsalddelProductVersion.V2_0)]
[SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
[SsalddelApiCapability(SsalddelCapability.TransportRequest)]
[SsalddelApiAudience(SsalddelActor.CommunityMember)]
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.CoreLogistics)]
public sealed class NeighborhoodMemberDeliveryController(NeighborhoodMemberDeliveryUseCase service) : ControllerBase
{
    [HttpGet("participation"), SsalddelApiOperation(SsalddelOperation.Browse)]
    public Task<IActionResult> Participation(CancellationToken ct) => Execute(() => service.ParticipationAsync(ct));
    [HttpPut("participation"), SsalddelApiOperation(SsalddelOperation.Record)]
    public Task<IActionResult> SaveParticipation(NeighborhoodMemberParticipationSaveRequest request, CancellationToken ct)
        => Execute(() => service.SaveParticipationAsync(request, ct));
    [HttpGet("offers"), SsalddelApiOperation(SsalddelOperation.Browse)]
    public Task<IActionResult> Offers(CancellationToken ct) => Execute(() => service.OffersAsync(ct));
    [HttpGet("offers/{offerId}"), SsalddelApiOperation(SsalddelOperation.Browse)]
    public Task<IActionResult> Offer(string offerId, CancellationToken ct) => Execute(() => service.OfferAsync(offerId, ct));
    [HttpPost("offers/{offerId}/accept"), SsalddelApiOperation(SsalddelOperation.Decide)]
    public Task<IActionResult> Accept(string offerId, NeighborhoodMemberAcceptRequest request, CancellationToken ct)
        => Execute(() => service.AcceptAsync(offerId, request, ct));
    [HttpPost("offers/{offerId}/decline"), SsalddelApiOperation(SsalddelOperation.Decide)]
    public Task<IActionResult> Decline(string offerId, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => Execute(() => service.DeclineAsync(offerId, request, ct));
    [HttpGet("work"), SsalddelApiOperation(SsalddelOperation.Browse)]
    public Task<IActionResult> WorkList(CancellationToken ct) => Execute(() => service.WorkListAsync(ct));
    [HttpGet("work/{requestId}"), SsalddelApiOperation(SsalddelOperation.Browse)]
    public Task<IActionResult> Work(string requestId, CancellationToken ct) => Execute(() => service.WorkAsync(requestId, ct));
    [HttpPost("work/{requestId}/evidence"), RequestSizeLimit(7 * 1024 * 1024), SsalddelApiOperation(SsalddelOperation.Record)]
    public Task<IActionResult> Evidence(string requestId, NeighborhoodMemberEvidenceRequest request, CancellationToken ct)
        => Execute(() => service.EvidenceAsync(requestId, request, ct));
    [HttpGet("work/{requestId}/evidence-receipts/{clientRequestId:guid}"), SsalddelApiOperation(SsalddelOperation.Browse)]
    public Task<IActionResult> EvidenceReceipt(string requestId, Guid clientRequestId, CancellationToken ct)
        => Execute(() => service.EvidenceReceiptAsync(requestId, clientRequestId, ct));
    [HttpPost("work/{requestId}/pickup"), SsalddelApiOperation(SsalddelOperation.Record)]
    public Task<IActionResult> Pickup(string requestId, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => Execute(() => service.PickupAsync(requestId, request, ct));
    [HttpPost("work/{requestId}/deliver"), SsalddelApiOperation(SsalddelOperation.Record)]
    public Task<IActionResult> Deliver(string requestId, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => Execute(() => service.DeliverAsync(requestId, request, ct));
    [HttpPost("work/{requestId}/issue"), SsalddelApiOperation(SsalddelOperation.Record)]
    public Task<IActionResult> Issue(string requestId, NeighborhoodMemberIssueRequest request, CancellationToken ct)
        => Execute(() => service.IssueAsync(requestId, request, ct));
    [HttpPost("requests/{requestId}/cancel"), SsalddelApiOperation(SsalddelOperation.Decide)]
    public Task<IActionResult> Cancel(string requestId, NeighborhoodMemberCommandRequest request, CancellationToken ct)
        => Execute(async () => { await service.CancelAsync(requestId, request, ct); return new { requestId }; });
    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        Response.Headers.CacheControl = "private, no-store";
        try { var result = await action(); return result is null ? NotFound() : Ok(result); }
        catch (NeighborhoodMemberDeliveryException ex)
        { var p = new ProblemDetails { Status = ex.StatusCode, Title = ex.Message }; p.Extensions["code"] = ex.Code; return StatusCode(ex.StatusCode, p); }
        catch (Ssalddel.Services.PrivacySupport.보호지원Exception ex)
        { var p = new ProblemDetails { Status = ex.StatusCode, Title = ex.Message }; p.Extensions["code"] = ex.Code; return StatusCode(ex.StatusCode, p); }
        catch (System.Data.Common.DbException)
        { var p = new ProblemDetails { Status = 409, Title = "저장 결과를 확인해 주세요. 같은 요청 번호로 최신 상태를 다시 조회할 수 있습니다." }; p.Extensions["code"] = "MemberRevisionConflict"; return StatusCode(409, p); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or NotSupportedException)
        { var p = new ProblemDetails { Status = 503, Title = "배송 검증에 필요한 연결을 확인할 수 없습니다. 최신 상태를 다시 확인해 주세요." }; p.Extensions["code"] = "MemberDependencyUnavailable"; return StatusCode(503, p); }
    }
}
