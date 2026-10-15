using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Security;

namespace Ssalddel.Controllers.Platform;

/// <summary>기본 비활성 입금 알림 어댑터. 알림 내용은 결제 사실이 아니며 저장한 비밀 해시 확인 뒤 PG 정본만 조회합니다.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/platform/payments/toss/deposit")]
[SsalddelApiVersion(SsalddelProductVersion.V2_0)]
[SsalddelApiCapability(SsalddelCapability.Payment)]
[SsalddelApiOperation(SsalddelOperation.Request)]
[RequestSizeLimit(16 * 1024)]
[EnableRateLimiting(RequestRateLimitPolicyNames.CommunityMutation)]
public sealed class TossDepositCallbackController(토스결제입금재조회UseCase useCase) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Deposit([FromBody] TossDepositNotice notice, CancellationToken ct)
    {
        try { await useCase.입금알림Async(notice.OrderId, notice.Secret, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* 존재 여부·상태·키를 callback 응답이나 로그에 노출하지 않습니다. */ }
        return Ok(new { received = true });
    }
}

public sealed class TossDepositNotice
{
    public string OrderId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}
