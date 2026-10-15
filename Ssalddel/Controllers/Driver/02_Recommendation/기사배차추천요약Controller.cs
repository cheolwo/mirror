using Ssalddel.Controllers;
using Ssalddel.Application.Driver.Recommendation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 살뜰.도메인.공통;
using Ssalddel.ApiMetadata;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Driver.Recommendation02
{
    [SsalddelApiVersion(SsalddelProductVersion.V2_0)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    [SsalddelApiCapability(SsalddelCapability.Dispatch)]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    [ApiController]
    [Authorize(Roles = 역할명.기사)]
    [Route("api/v1/driver/recommendations")]
    public sealed class 기사배차추천요약Controller : DriverControllerBase
    {
        private readonly I기사배차추천UseCase _useCase;

        public 기사배차추천요약Controller(I기사배차추천UseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> 요약()
        {
            var driverId = 현재기사Id();
            var result = await _useCase.추천요약조회Async(driverId);
            return Ok(result);
        }

    }
}
