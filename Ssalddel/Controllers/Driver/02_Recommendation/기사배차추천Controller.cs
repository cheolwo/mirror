using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Controllers;
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
    public sealed class 기사배차추천Controller : DriverControllerBase
    {
        private readonly I기사배차추천UseCase _useCase;

        public 기사배차추천Controller(I기사배차추천UseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        public async Task<IActionResult> 조회()
        {
            var driverId = 현재기사Id();
            var items = await _useCase.추천조회Async(driverId);
            return Ok(items);
        }

        [HttpGet("idle")]
        public async Task<IActionResult> 비운행중조회()
        {
            var driverId = 현재기사Id();
            var items = await _useCase.비운행중추천조회Async(driverId);
            return Ok(items);
        }

        [HttpGet("driving")]
        public async Task<IActionResult> 운행중조회()
        {
            var driverId = 현재기사Id();
            var items = await _useCase.운행중추천조회Async(driverId);
            return Ok(items);
        }

        [HttpGet("search")]
        public async Task<IActionResult> 검색([FromQuery] decimal latitude, [FromQuery] decimal longitude, [FromQuery] decimal radiusKm)
        {
            var driverId = 현재기사Id();
            if (radiusKm <= 0)
            {
                return this.ToProblemActionResult("radiusKm must be greater than 0.");
            }

            var items = await _useCase.위치기반추천검색Async(driverId, latitude, longitude, radiusKm);
            return Ok(items);
        }

        [HttpGet("national")]
        public async Task<IActionResult> 전국콜조회()
        {
            var driverId = 현재기사Id();
            var items = await _useCase.전국콜조회Async(driverId);
            return Ok(items);
        }

    }
}
