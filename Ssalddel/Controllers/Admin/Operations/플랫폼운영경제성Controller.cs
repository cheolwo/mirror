using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Finance;
using Ssalddel.Application.Admin.Operations;
using Ssalddel.Contracts.Admin.Finance;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Controllers.Admin.Operations;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[SsalddelApiVersion(SsalddelProductVersion.V3_5)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.PlatformOperations)]
[SsalddelApiCapability(SsalddelCapability.Settlement)]
[SsalddelApiAudience(SsalddelActor.PlatformOperator)]
[SsalddelApiOperation(SsalddelOperation.Browse)]
[Route("api/v1/admin/operations/economics")]
[SsalddelApiContractName("PlatformOperatingEconomicsController")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.PlatformOperatingEconomics,
    SsalddelCodeLayer.Api,
    "권한 있는 운영자에게 플랫폼 운영경제성 관리 Simulation Preview를 제공합니다.",
    StepKey = "api.platform-operating-economics-preview",
    DependsOnStepKeys = ["application.platform-operating-economics-preview"],
    FlowOrder = 40,
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    Effects = SsalddelCodeEffect.None,
    ContractType = typeof(플랫폼운영경제성평가요청Dto),
    Boundary = "읽기 전용 계산 API이며 외부 환율·결제·정산·회계 시스템을 호출하지 않습니다.")]
public sealed class 플랫폼운영경제성Controller(
    I플랫폼운영경제성UseCase useCase,
    I운영재무조회UseCase financeUseCase) : ControllerBase
{
    [HttpPost("evaluate")]
    [SsalddelApiContractName("EvaluatePlatformOperatingEconomics")]
    public IActionResult 평가([FromBody] 플랫폼운영경제성평가요청Dto 요청)
    {
        try
        {
            return Ok(useCase.평가(요청));
        }
        catch (ArgumentException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("evaluate-from-ledger")]
    [SsalddelApiContractName("EvaluatePlatformOperatingEconomicsFromFinancialLedger")]
    public async Task<IActionResult> 원장기반평가(
        [FromBody] 플랫폼운영경제성원장평가요청Dto 요청,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await financeUseCase.원장기반경제성평가Async(요청, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return this.ToProblemActionResult(ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
