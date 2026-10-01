using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 홍달.도메인.공통;

namespace Hongdal.Controllers.Common;

[ApiController]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalApiGrowthTrack(HongdalApiGrowthTrack.Community)]
[Route("api/v1/community/driver-availability")]
public sealed class CommunityDriverAvailabilityController : ControllerBase
{
    private readonly ICommunityDriverAvailabilityService service;

    public CommunityDriverAvailabilityController(ICommunityDriverAvailabilityService service)
    {
        this.service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetActive([FromQuery] string? operatingArea = null)
        => Ok(service.GetActive(operatingArea));

    [HttpGet("my-inquiries")]
    [Authorize]
    public IActionResult GetMyInquiries()
        => Ok(service.GetRequesterInquiries(CurrentUserId()));

    [HttpPost("{postId:guid}/inquiries")]
    [Authorize]
    public IActionResult CreateInquiry(Guid postId, [FromBody] CommunityDriverInquiryCreateRequest request)
    {
        try
        {
            return Ok(service.CreateInquiry(postId, CurrentUserId(), CurrentRole(), request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("사용자 식별자를 찾을 수 없습니다.");

    private string CurrentRole()
        => User.FindFirstValue(ClaimTypes.Role)
           ?? User.FindFirstValue("role")
           ?? "운송 요청자";
}

[ApiController]
[Authorize(Roles = 역할명.기사)]
[HongdalApiVersion(HongdalProductVersion.V0_0)]
[HongdalApiWorkflow(HongdalWorkflow.DomesticTransport)]
[Route("api/v1/driver/community-inquiries")]
public sealed class DriverCommunityInquiriesController : DriverControllerBase
{
    private readonly ICommunityDriverAvailabilityService service;

    public DriverCommunityInquiriesController(ICommunityDriverAvailabilityService service)
    {
        this.service = service;
    }

    [HttpGet]
    public IActionResult GetMine()
        => Ok(service.GetDriverInquiries(현재기사Id()));

    [HttpPost("{inquiryId:guid}/decision")]
    public IActionResult Decide(Guid inquiryId, [FromBody] CommunityDriverInquiryDecisionRequest request)
    {
        try
        {
            return Ok(service.Decide(현재기사Id(), inquiryId, request));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
