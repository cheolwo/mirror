using Microsoft.AspNetCore.Mvc;
using MediatR;
using Ssalddel.Controllers;
using Ssalddel.Application.Admin.Inbound;
using Ssalddel.Contracts.Admin.Inbound;
using Ssalddel.ApiMetadata;
using Microsoft.AspNetCore.Authorization;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Inflow02
{
    [SsalddelApiVersion(SsalddelProductVersion.V2_0)]
    [ApiController]
    [Route("api/v1/dispatch/wait")]
    [Authorize(Policy = "서버관리자전용")]
    public class 배차대기Controller : ControllerBase
    {
        private readonly ISender _sender;

        public 배차대기Controller(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
        public async Task<IActionResult> 목록조회()
        {
            return Ok(await _sender.Send(new 배차대기목록조회Query()));
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> 단건조회(long id)
        {
            var item = await _sender.Send(new 배차대기단건조회Query(id));
            if (item == null) return this.ToNotFoundProblem("배차대기 정보를 찾을 수 없습니다.");
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> 생성([FromBody] 배차대기요청 request)
        {
            if (request == null) return this.ToProblemActionResult("request body is required");

            var entity = await _sender.Send(new 배차대기생성Command(
                request.의뢰Id,
                request.화주Id,
                request.배차업무유형,
                request.원본의뢰유형,
                request.원본의뢰Id,
                request.픽업_도로명주소,
                request.픽업_상세주소,
                request.픽업_위도,
                request.픽업_경도,
                request.하차_도로명주소,
                request.하차_상세주소,
                request.하차_위도,
                request.하차_경도,
                request.상태,
                request.공동구매도착지유형코드,
                request.공동구매기사세대배송여부,
                request.공동구매세대배송방식코드,
                request.공동구매세대배송건수,
                request.공동구매분배책임코드));
            return CreatedAtAction(nameof(단건조회), new { id = entity.Id }, entity);
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> 수정(long id, [FromBody] 배차대기요청 request)
        {
            if (request == null) return this.ToProblemActionResult("request body is required");

            var entity = await _sender.Send(new 배차대기수정Command(
                id,
                request.의뢰Id,
                request.화주Id,
                request.배차업무유형,
                request.원본의뢰유형,
                request.원본의뢰Id,
                request.픽업_도로명주소,
                request.픽업_상세주소,
                request.픽업_위도,
                request.픽업_경도,
                request.하차_도로명주소,
                request.하차_상세주소,
                request.하차_위도,
                request.하차_경도,
                request.상태,
                request.공동구매도착지유형코드,
                request.공동구매기사세대배송여부,
                request.공동구매세대배송방식코드,
                request.공동구매세대배송건수,
                request.공동구매분배책임코드));
            if (entity == null) return this.ToNotFoundProblem("배차대기 정보를 찾을 수 없습니다.");
            return Ok(entity);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> 삭제(long id)
        {
            var result = await _sender.Send(new 배차대기삭제Command(id));
            return this.ToNoContentActionResult(result);
        }
    }

}
