using Hongdal.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 홍달.도메인.공통;
using MediatR;
using FluentResults;
using Hongdal.Contracts.Driver.Work;
using Hongdal.Application.Driver.Work;
using Hongdal.ApiMetadata;

namespace Hongdal.Controllers.Driver.Work01
{
    [HongdalApiVersion(HongdalProductVersion.V1_0)]
    [ApiController]
    [Authorize(Roles = 역할명.기사)]
    [Route("api/v1/driver/work")]
    public sealed class 기사운행Controller : DriverControllerBase
    {
        private readonly ISender _sender;

        public 기사운행Controller(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("status")]
        public async Task<IActionResult> 상태조회()
        {
            var driverId = 현재기사Id();
            var result = await _sender.Send(new 운행상태조회Query(driverId));
            return Ok(result);
        }

        [HttpGet("current")]
        public async Task<IActionResult> 현재근무조회()
        {
            var driverId = 현재기사Id();
            var result = await _sender.Send(new 현재근무조회Query(driverId));
            return Ok(result);
        }

        [HttpPost("start")]
        public async Task<IActionResult> 시작([FromBody] 기사운행시작요청 request)
        {
            var driverId = 현재기사Id();
            var result = await _sender.Send(new 운행시작Command(
                driverId,
                request.시작모드,
                request.시작시각,
                request.시작위치,
                request.복귀지,
                request.오늘의복귀지주소,
                request.오늘의복귀지위도,
                request.오늘의복귀지경도,
                request.기본복귀지사용,
                request.복귀지출처,
                request.복귀콜선호,
                request.커뮤니티운행공개,
                request.커뮤니티구단위위치공개동의));

            if (result.IsFailed)
            {
                return this.ToProblemActionResult(result.Errors.Select(x => x.Message));
            }

            return CreatedAtAction(nameof(상태조회), new { }, result.Value);
        }

        [HttpPost("stop")]
        public async Task<IActionResult> 종료()
        {
            var driverId = 현재기사Id();
            await _sender.Send(new 운행종료Command(driverId));

            return NoContent();
        }

        [HttpPost("location")]
        public async Task<IActionResult> 위치갱신([FromBody] 기사위치갱신요청 request)
        {
            var driverId = 현재기사Id();
            var result = await _sender.Send(new 위치갱신Command(
                driverId,
                request.AppKey,
                request.위도,
                request.경도,
                request.정확도_m,
                request.상차접근허용반경Km,
                request.운행상태,
                request.기록시각));

            if (result.IsFailed)
            {
                return this.ToProblemActionResult(result.Errors.Select(x => x.Message));
            }

            return Ok(result.Value);
        }

    }
}
