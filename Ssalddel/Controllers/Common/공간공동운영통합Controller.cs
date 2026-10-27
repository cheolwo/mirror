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
    "공동운영 상위 원장·본인 주문·전문 원장의 관계를 비공개로 조회",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "기존 원장과 당사자 권한으로 조회하며 주문·재고·배차·지급 상태를 복제하거나 변경하지 않습니다.")]
public sealed class 공간공동운영통합Controller(I공간공동운영통합Query query) : CommunityControllerBase
{
    [HttpGet("{ledgerId}/integrated-ledger")]
    [SsalddelApiContractName("GetMySpaceCooperationIntegratedLedger")]
    public async Task<IActionResult> 조회(string ledgerId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        try
        {
            var item = await query.조회Async(ledgerId, cancellationToken);
            return item is null ? this.ToNotFoundProblem("현재 계정의 공동운영 원장을 찾을 수 없습니다.") : Ok(item);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        { return Problem(statusCode: 503, title: "공동운영 원장을 지금 확인하지 못했습니다. 잠시 뒤 다시 조회해 주세요."); }
    }
}
