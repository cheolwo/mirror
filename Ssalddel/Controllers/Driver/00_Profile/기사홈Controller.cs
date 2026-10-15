using Ssalddel.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Ssalddel.Application.Driver.Home;
using Ssalddel.ApiMetadata;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Driver.Profile00
{
    [SsalddelApiVersion(SsalddelProductVersion.V2_0)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    [SsalddelApiCapability(SsalddelCapability.DriverWork)]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    [ApiController]
    [Authorize(Roles = 역할명.기사)]
    [Route("api/v1/driver/home")]
    public sealed class 기사홈Controller : DriverControllerBase
    {
        private readonly ISender _sender;

        public 기사홈Controller(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> 조회()
        {
            var driverId = 현재기사Id();
            var result = await _sender.Send(new 기사홈조회Query(driverId));

            if (result == null)
            {
                return this.ToNotFoundProblem("용달기사 정보를 찾을 수 없습니다.");
            }

            return Ok(result);
        }

    }
}
