using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Common;

[ApiController]
[HongdalApiVersion(HongdalProductVersion.V1_0)]
[Route("api/v1/operations/third-party-logistics/providers")]
public sealed class ThirdPartyLogisticsProvidersController : ControllerBase
{
    private readonly IThirdPartyLogisticsProviderDirectoryService _service;

    public ThirdPartyLogisticsProvidersController(
        IThirdPartyLogisticsProviderDirectoryService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ThirdPartyLogisticsProviderDirectoryResponse> Get(
        [FromQuery] string? q = null,
        [FromQuery] string? capabilityCode = null,
        [FromQuery] string? segmentCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var response = _service.Search(new ThirdPartyLogisticsProviderDirectoryQuery
        {
            SearchText = q,
            CapabilityCode = capabilityCode,
            SegmentCode = segmentCode,
            Page = page,
            PageSize = pageSize
        });

        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpGet("collective-purchase")]
    [AllowAnonymous]
    public ActionResult<CollectivePurchaseLogisticsDirectoryResponse>
        GetForCollectivePurchase(
            [FromQuery] string? q = null,
            [FromQuery] string? stageCode = null,
            [FromQuery] string? productHandlingCode = null,
            [FromQuery] string? engagementSignalCode = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
    {
        var response = _service.SearchForCollectivePurchase(
            new CollectivePurchaseLogisticsDirectoryQuery
            {
                SearchText = q,
                StageCode = stageCode,
                ProductHandlingCode = productHandlingCode,
                EngagementSignalCode = engagementSignalCode,
                Page = page,
                PageSize = pageSize
            });

        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpGet("bonded-to-door")]
    [AllowAnonymous]
    public ActionResult<BondedToDoorLogisticsDirectoryResponse> GetBondedToDoor(
        [FromQuery] string? q = null,
        [FromQuery] string? stageCode = null,
        [FromQuery] string? storageModelCode = null,
        [FromQuery] string? stateCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var response = _service.SearchBondedToDoor(
            new BondedToDoorLogisticsDirectoryQuery
            {
                SearchText = q,
                StageCode = stageCode,
                StorageModelCode = storageModelCode,
                StateCode = stateCode,
                Page = page,
                PageSize = pageSize
            });

        return response.Success ? Ok(response) : NotFound(response);
    }
}
