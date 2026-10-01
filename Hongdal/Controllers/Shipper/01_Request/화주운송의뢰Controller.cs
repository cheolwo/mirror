using Hongdal.Controllers;
using Hongdal.Application.Shipper.Request;
using Hongdal.Contracts.Shipper.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Hongdal.ApiMetadata;
using 홍달.Services.Versioning;

namespace Hongdal.Controllers.Shipper.Request01
{
    [HongdalApiVersion(
        HongdalProductVersion.V1_0,
        FeatureKey = VersionFeatureFlagKeys.DomesticTransportWorkflow,
        WorkflowKey = VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    [HongdalApiWorkflow(HongdalWorkflow.DomesticTransport)]
    [HongdalApiGrowthTrack(HongdalApiGrowthTrack.CoreLogistics)]
    [ApiController]
    [Route("api/v1/shipper/requests")]
    [Authorize]
    public class 화주운송의뢰Controller : ControllerBase
    {
        private readonly I화주운송의뢰UseCase _useCase;

        public 화주운송의뢰Controller(I화주운송의뢰UseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        public async Task<IActionResult> 의뢰목록조회(
            [FromQuery] string? shipperId,
            [FromQuery] string? status,
            [FromQuery] string? paymentStatus,
            [FromQuery] string? dispatchStatus,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var items = await _useCase.의뢰목록조회Async(shipperId, status, paymentStatus, dispatchStatus, page, pageSize);
            return Ok(items);
        }

        [AllowAnonymous]
        [HttpGet("public")]
        public async Task<IActionResult> 공개화물요약조회(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var items = await _useCase.공개화물요약조회Async(page, pageSize);
            return Ok(items);
        }

        [HttpPost("recommend-vehicle")]
        public async Task<ActionResult<차량추천응답>> 차량추천([FromBody] 차량추천요청 request, CancellationToken cancellationToken)
        {
            var result = await _useCase.차량추천Async(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("fare-estimate")]
        public async Task<IActionResult> 기준운임견적([FromBody] 화주운송기준운임견적요청 request, CancellationToken cancellationToken)
        {
            var result = await _useCase.기준운임견적Async(request, cancellationToken);
            return this.ToActionResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> 의뢰생성([FromBody] 화주운송의뢰생성요청 req)
        {
            var result = await _useCase.의뢰생성Async(req);
            return result.IsSuccess
                ? CreatedAtAction(nameof(의뢰단건조회), new { requestId = result.Value.의뢰Id }, result.Value)
                : this.ToProblemActionResult(result.Errors.Select(x => x.Message));
        }

        [HttpGet("{requestId}")]
        public async Task<IActionResult> 의뢰단건조회(string requestId)
        {
            var item = await _useCase.의뢰단건조회Async(requestId);
            return item == null ? this.ToNotFoundProblem("운송의뢰 데이터를 찾을 수 없습니다.") : Ok(item);
        }

        [HttpPut("{requestId}")]
        public async Task<IActionResult> 의뢰수정(string requestId, [FromBody] 화주운송의뢰수정요청 req)
        {
            var result = await _useCase.의뢰수정Async(requestId, req);
            return this.ToActionResult(result);
        }

        [HttpDelete("{requestId}")]
        public async Task<IActionResult> 의뢰삭제(string requestId)
        {
            var result = await _useCase.의뢰삭제Async(requestId);
            return this.ToNoContentActionResult(result);
        }

        [HttpPost("bulk/preview")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> 일괄미리보기([FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            var fileProblem = ValidateBulkFile(file);
            if (fileProblem is not null)
            {
                return fileProblem;
            }

            await using var stream = file.OpenReadStream();
            var result = await _useCase.일괄미리보기Async(stream, file.FileName, cancellationToken);
            return this.ToActionResult(result);
        }

        [HttpPost("bulk/confirm")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> 일괄등록([FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            var fileProblem = ValidateBulkFile(file);
            if (fileProblem is not null)
            {
                return fileProblem;
            }

            await using var stream = file.OpenReadStream();
            var result = await _useCase.일괄등록Async(stream, file.FileName, cancellationToken);
            return this.ToActionResult(result);
        }

        [HttpPost("bulk/confirm-preview")]
        public async Task<IActionResult> 일괄미리보기확정등록([FromBody] 화주운송의뢰일괄확정등록요청 request, CancellationToken cancellationToken)
        {
            var result = await _useCase.일괄미리보기확정등록Async(request, cancellationToken);
            return this.ToActionResult(result);
        }

        [HttpPost("{requestId}/settlement/offline")]
        public async Task<IActionResult> 현장지급처리(string requestId, [FromBody] 화주운송의뢰현장지급처리요청 req)
        {
            var result = await _useCase.현장지급처리Async(requestId, req);
            return this.ToActionResult(result);
        }

        [HttpPost("{requestId}/settlement/postpay/approve")]
        public async Task<IActionResult> 후불승인(string requestId, [FromBody] 화주운송의뢰후불승인요청 req)
        {
            var result = await _useCase.후불승인Async(requestId, req);
            return this.ToActionResult(result);
        }

        [HttpPost("{requestId}/settlement/receipt")]
        public async Task<IActionResult> 인수증등록(string requestId, [FromBody] 화주운송의뢰인수증등록요청 req)
        {
            var result = await _useCase.인수증등록Async(requestId, req);
            return this.ToActionResult(result);
        }

        private IActionResult? ValidateBulkFile(IFormFile? file)
        {
            if (file == null)
            {
                return this.ToProblemActionResult("file is required");
            }

            if (file.Length <= 0)
            {
                return this.ToProblemActionResult("empty file is not allowed");
            }

            return null;
        }
    }
}
