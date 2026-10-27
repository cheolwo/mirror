using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Services.Operations;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin.Operations;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[SsalddelApiVersion(SsalddelProductVersion.V3_5)]
[SsalddelApiFeature(VersionFeatureFlagKeys.PlatformOperationsControl)]
[Route(FreightPlatformReadinessRoutes.Readiness)]
[SsalddelApiContractName("FreightPlatformReadinessController")]
public sealed class 화물플랫폼등록준비Controller(I화물플랫폼등록준비Service service) : ControllerBase
{
    [HttpGet]
    [SsalddelApiContractName("GetFreightPlatformRegistrationReadiness")]
    public ActionResult<FreightPlatformReadinessDto> 조회() => Ok(service.조회());
}
