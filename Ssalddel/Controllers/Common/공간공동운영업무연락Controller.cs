using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Common;

[ApiController, Authorize]
[Route(공간공동운영Routes.Api)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Ledger, SsalddelModuleKind.Api,
    "현재 연결 원천과 전문 권한으로 등록된 업무 연락처만 비공개 조회",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "새 연락처·담당 배정을 만들지 않으며 계정 연락처 조회·전화 발신·외부 연락을 하지 않습니다.")]
public sealed class 공간공동운영업무연락Controller(I공간공동운영업무연락Query query) : CommunityControllerBase
{
    [HttpGet("{ledgerId}/cycles/{cycleId}/work-contacts")]
    [SsalddelApiContractName("GetMySpaceCooperationRegisteredWorkContacts")]
    public async Task<IActionResult> 조회(string ledgerId, string cycleId, [FromQuery] string stageKey,
        [FromQuery] long expectedRevision, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        try
        {
            var response = await query.조회Async(ledgerId, cycleId, stageKey, expectedRevision, cancellationToken);
            return response is null ? this.ToNotFoundProblem("현재 계정의 업무 자료를 확인할 수 없습니다.") : Ok(response);
        }
        catch (공간공동운영업무연락Exception error)
        { return Problem(statusCode: error.StatusCode, title: error.StatusCode == 400
            ? "연락처를 확인할 공동운영·회차·절차를 선택해 주세요." : "업무 자료가 달라졌어요. 다시 불러온 뒤 확인해 주세요."); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Problem(statusCode: 503, title: "업무 연락처를 지금 확인하지 못했어요. 잠시 뒤 다시 확인해 주세요."); }
    }
}
