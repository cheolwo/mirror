using Ssalddel.Controllers;
using Ssalddel.Application.Driver.Recommendation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 살뜰.도메인.공통;
using Ssalddel.ApiMetadata;

namespace Ssalddel.Controllers.Driver.Recommendation02
{
    [SsalddelApiVersion(SsalddelProductVersion.V2_0)]
    [SsalddelApiCapability(SsalddelCapability.TransportRequest)]
    [SsalddelApiOperation(SsalddelOperation.Browse)]
    [ApiController]
    [Authorize(Roles = 역할명.기사)]
    [Route("api/v1/driver/requests")]
    public sealed class 기사운송의뢰Controller : DriverControllerBase
    {
        private readonly I기사배차추천UseCase _useCase;

        public 기사운송의뢰Controller(I기사배차추천UseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet("{requestId}")]
        public async Task<IActionResult> 상세조회(string requestId)
        {
            var driverId = 현재기사Id();
            try
            {
                var result = await _useCase.운송의뢰상세조회Async(driverId, requestId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return this.ToForbiddenProblem("현재 배송에 배정되거나 제안받은 기사만 조회할 수 있습니다.");
            }
        }

    }

}
