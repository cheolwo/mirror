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
    [SsalddelApiOperation(SsalddelOperation.Decide)]
    [ApiController]
    [Authorize(Roles = 역할명.기사)]
    [Route("api/v1/driver/public-dispatches")]
    public sealed class 기사공개배차Controller : DriverControllerBase
    {
        private readonly I기사배차추천UseCase _useCase;

        public 기사공개배차Controller(I기사배차추천UseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        public async Task<IActionResult> 조회()
        {
            var driverId = 현재기사Id();
            var items = await _useCase.공개배차조회Async(driverId);
            return Ok(items);
        }

    }
}
