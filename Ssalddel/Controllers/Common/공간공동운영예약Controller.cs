using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Common;

[ApiController, Authorize]
[Route(공간공동운영예약Routes.Api)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Ledger, SsalddelModuleKind.Api,
    "공동운영 당사자의 시간 약속과 변경 영향 확인", ReleaseStage = SsalddelCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "보호된 원장 안에서 약속만 기록하며 실제 근무 권한·재고·배차·금전·외부 알림을 만들지 않습니다.")]
public sealed class 공간공동운영예약Controller(I공간공동운영예약UseCase useCase) : CommunityControllerBase
{
    [HttpGet]
    public Task<IActionResult> 목록(string ledgerId, CancellationToken ct)
        => Read(async () => await useCase.목록Async(ledgerId, ct));

    [HttpGet("candidates")]
    public Task<IActionResult> 후보(string ledgerId, CancellationToken ct)
        => Read(async () => await useCase.후보Async(ledgerId, ct));

    [HttpGet("{bookingId:guid}")]
    public Task<IActionResult> 상세(string ledgerId, Guid bookingId, CancellationToken ct)
        => Read(async () => await useCase.조회Async(ledgerId, bookingId, ct));

    [HttpGet("requests/{requestId:guid}")]
    public Task<IActionResult> 요청결과(string ledgerId, Guid requestId, CancellationToken ct)
        => Read(async () => await useCase.요청결과Async(ledgerId, requestId, ct));

    [HttpGet("{bookingId:guid}/cancel-impact")]
    public Task<IActionResult> 취소영향(string ledgerId, Guid bookingId, CancellationToken ct)
        => Read(async () => await useCase.취소영향Async(ledgerId, bookingId, ct));

    [HttpPost("{bookingId:guid}/commands")]
    public async Task<IActionResult> 변경(string ledgerId, Guid bookingId, [FromBody] 공간공동운영예약명령Request request, CancellationToken ct)
        => this.ToActionResult(await useCase.변경Async(ledgerId, bookingId, request, ct));

    private async Task<IActionResult> Read(Func<Task<object?>> query)
    {
        try
        {
            var value = await query();
            return value is null ? this.ToNotFoundProblem("현재 계정의 시간 약속을 찾지 못했습니다.") : Ok(value);
        }
        catch (공간공동운영예약Exception e)
        {
            return StatusCode(e.Status, new ProblemDetails { Title = "시간 약속을 확인하지 못했습니다.",
                Detail = "현재 참여와 약속 조건을 다시 확인해 주세요.", Status = e.Status, Extensions = { ["code"] = e.Code } });
        }
    }
}
