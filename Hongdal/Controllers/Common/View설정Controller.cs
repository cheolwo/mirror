using System.Security.Claims;
using Hongdal.Application.ViewSettings;
using Hongdal.Contracts.Common.ViewSettings;
using Hongdal.Controllers;
using Microsoft.AspNetCore.Mvc;
using Hongdal.ApiMetadata;

namespace Hongdal.Controllers.Common;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[ApiController]
[Route("api/v1/view-settings")]
public sealed class View설정Controller : ControllerBase
{
    private const string UserIdHeaderName = "X-View-UserId";
    private const string RoleHeaderName = "X-View-Role";

    private readonly IView설정UseCase _useCase;

    public View설정Controller(IView설정UseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet("effective")]
    public async Task<IActionResult> 조회([FromQuery] string appKey, CancellationToken cancellationToken)
    {
        var result = await _useCase.조회Async(appKey, 요청Context생성(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("user")]
    public async Task<IActionResult> 저장([FromBody] 사용자View가시성수정요청 request, CancellationToken cancellationToken)
    {
        var result = await _useCase.저장Async(request, 요청Context생성(), cancellationToken);
        return this.ToNoContentActionResult(result);
    }

    private View설정요청Context 요청Context생성()
        => new(
            ResolveUserId(),
            ResolveRoleName(),
            Request.Path.Value ?? string.Empty,
            HttpContext.TraceIdentifier,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Request.Headers.UserAgent.ToString());

    private string? ResolveUserId()
        => FirstNonBlank(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            Request.Headers[UserIdHeaderName].ToString());

    private string? ResolveRoleName()
        => FirstNonBlank(
            User.FindFirstValue(ClaimTypes.Role),
            Request.Headers[RoleHeaderName].ToString());

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
