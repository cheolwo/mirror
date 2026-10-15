using System.Security.Claims;
using Ssalddel.Application.Driver.Notification;
using Ssalddel.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Driver.Notification;
using Ssalddel.ApiMetadata;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Driver.Notification07
{
    [SsalddelApiVersion(SsalddelProductVersion.V2_0)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.OperationalDispatchCore)]
    [SsalddelApiAudience(SsalddelActor.Driver)]
    [SsalddelApiCapability(SsalddelCapability.Notification)]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    [SsalddelApiOperation(SsalddelOperation.Manage)]
    [ApiController]
    [Authorize(Roles = 역할명.기사)]
    [Route("api/v1/driver/notifications")]
    public sealed class 기사알림Controller : ControllerBase
    {
        private readonly I기사알림UseCase _useCase;

        public 기사알림Controller(I기사알림UseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet("push-token")]
        public async Task<IActionResult> 조회()
        {
            var result = await _useCase.푸시토큰조회Async(현재기사Id());
            return this.ToActionResult(result);
        }

        [HttpPut("push-token")]
        public async Task<IActionResult> 등록([FromBody] 기사푸시토큰등록요청 request)
        {
            var result = await _useCase.푸시토큰등록Async(현재기사Id(), request);
            return this.ToActionResult(result);
        }

        [HttpDelete("push-token")]
        public async Task<IActionResult> 삭제()
        {
            var result = await _useCase.푸시토큰삭제Async(현재기사Id());
            return this.ToNoContentActionResult(result);
        }

        [HttpGet("settings")]
        public async Task<IActionResult> 설정조회()
        {
            var result = await _useCase.설정조회Async(현재기사Id());
            return this.ToActionResult(result);
        }

        [HttpPut("settings")]
        public async Task<IActionResult> 설정수정([FromBody] 기사알림설정수정요청 request)
        {
            var result = await _useCase.설정수정Async(현재기사Id(), request);
            return this.ToActionResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> 알림함(CancellationToken cancellationToken)
        {
            var result = await _useCase.알림함조회Async(현재기사Id(), cancellationToken);
            return this.ToActionResult(result);
        }

        [HttpPut("{notificationId:long}/read")]
        public async Task<IActionResult> 읽음처리(long notificationId, CancellationToken cancellationToken)
        {
            var result = await _useCase.읽음처리Async(현재기사Id(), notificationId, cancellationToken);
            return this.ToActionResult(result);
        }

        private string? 현재기사Id()
            => User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

}
