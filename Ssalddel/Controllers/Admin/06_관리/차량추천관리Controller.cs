using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Controllers;
using Ssalddel.Application.Admin.Management;
using Ssalddel.Contracts.Admin.Management;
using Ssalddel.ApiMetadata;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Master06
{
    [SsalddelApiVersion(SsalddelProductVersion.V2_0)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    [ApiController]
    [Route("api/v1/admin/vehicle-recommendations")]
    [Authorize(Policy = "서버관리자전용")]
    public class 차량추천관리Controller : ControllerBase
    {
        private readonly ISender _sender;

        public 차량추천관리Controller(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("criteria")]
        public async Task<IActionResult> 추천기준목록조회()
        {
            return Ok(await _sender.Send(new 차량추천기준목록조회Query()));
        }

        [HttpPut("criteria/{vehicleCode}")]
        public async Task<IActionResult> 추천기준수정(string vehicleCode, [FromBody] 차량추천기준수정요청 request)
        {
            if (request == null)
            {
                return this.ToProblemActionResult("request body is required");
            }

            차량추천기준응답? item;
            try
            {
                item = await _sender.Send(new 차량추천기준수정Command(
                    vehicleCode,
                    request.권장최대CBM,
                    request.추천우선순위,
                    request.추천사용여부,
                    request.운영권장중량Kg,
                    request.팔레트적재개수,
                    request.적재함길이Mm,
                    request.적재함폭Mm,
                    request.적재함높이Mm,
                    request.최대적재중량Kg));
            }
            catch (ArgumentException error)
            {
                return this.ToProblemActionResult(error.Message, 400);
            }

            if (item == null)
            {
                return this.ToNotFoundProblem("차량추천 기준을 찾을 수 없습니다.");
            }

            return Ok(item);
        }

        [HttpPost("simulate")]
        public async Task<IActionResult> 추천시뮬레이션([FromBody] 차량추천시뮬레이션요청 request)
        {
            if (request == null)
            {
                return this.ToProblemActionResult("request body is required");
            }

            return Ok(await _sender.Send(new 차량추천시뮬레이션Query(request)));
        }

        [HttpGet("drivers/{driverId}/actual-loading")]
        public async Task<IActionResult> 실차적재조회(string driverId, CancellationToken ct)
        {
            var result = await _sender.Send(new 기사실차적재제원조회Query(driverId), ct);
            return result is null ? this.ToNotFoundProblem("기사를 찾을 수 없습니다.") : Ok(result);
        }

        [HttpPut("drivers/{driverId}/actual-loading")]
        public async Task<IActionResult> 실차적재수정(string driverId, [FromBody] 기사실차적재제원수정요청 request, CancellationToken ct)
        {
            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Unauthorized();
            try
            {
                var result = await _sender.Send(new 기사실차적재제원수정Command(driverId, actor, request), ct);
                return result is null ? this.ToNotFoundProblem("기사를 찾을 수 없습니다.") : Ok(result);
            }
            catch (ArgumentException error) { return this.ToProblemActionResult(error.Message, 400); }
            catch (DbUpdateConcurrencyException) { return this.ToProblemActionResult("실차 적재 확인이 변경됐습니다. 다시 조회해 주세요.", 409); }
        }
    }
}
