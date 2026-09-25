using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Ssalddel.Application.Admin.Operating;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.ApiMetadata;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Progress03
{
    [SsalddelApiVersion(SsalddelProductVersion.V2_0)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    [SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
    [ApiController]
    [Route("api/v1/admin/drivers/operating")]
    [Authorize(Policy = "서버관리자전용")]
    public class 기사운행현황Controller : ControllerBase
    {
        private readonly ISender _sender;

        public 기사운행현황Controller(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> 현재운행기사조회([FromQuery] 현재운행기사조회요청 요청)
        {
            var 현재운행기사목록 = await _sender.Send(new 현재운행기사조회Query(요청 ?? new 현재운행기사조회요청()));

            return Ok(현재운행기사목록);
        }
    }

}
