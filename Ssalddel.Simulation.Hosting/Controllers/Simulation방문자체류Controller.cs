using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Route("api/simulation/v1/community-visitor-stay-ledgers/{ledgerStableId}")]
[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E2,
    "방문자 원장의 조회·Preview·Confirm을 공통 Application 서비스에 전달한다.",
    Boundary = "수용 규칙·멱등·행위 기록은 Domain 소유. Session 인증·저장 통합은 별도다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행,
    WorldInteractionIds = new[] { Simulation공동체방문자체류Codes.WorldInteractionId })]
public sealed class Simulation방문자체류Controller(
    Simulation공동체방문자체류Service service) : ControllerBase
{
    [HttpGet]
    public ActionResult<Simulation공동체방문자체류LedgerSnapshot> Get(string ledgerStableId)
        => Ok(service.Get(ledgerStableId));

    [HttpPost("previews")]
    public ActionResult<Simulation공동체방문자체류PreviewSnapshot> Preview(
        string ledgerStableId, [FromBody] Simulation공동체방문자체류PreviewRequest request)
        => Ok(service.Preview(ledgerStableId, request));

    [HttpPost("confirmations")]
    public ActionResult<Simulation공동체방문자체류ConfirmResult> Confirm(
        string ledgerStableId, [FromBody] Simulation공동체방문자체류ConfirmRequest request)
        => Ok(service.Confirm(ledgerStableId, request));
}
