using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ssalddel.Controllers.Admin;
/// <summary>메일의 공개 링크는 일반 앱 열기 안내만 제공한다. 업무 데이터는 관리자 API에서 다시 조회한다.</summary>
[ApiController, AllowAnonymous, Route("operations-alerts")]
[Ssalddel.ApiMetadata.SsalddelApiVersion(Ssalddel.ApiMetadata.SsalddelProductVersion.V0_0)]
public sealed class OperationsAlertLinkController : ControllerBase
{
    [HttpGet, HttpGet("{id}")]
    public IActionResult Open(string? id = null)
    {
        if (id is not null && (id.Length != 64 || !id.All(char.IsAsciiHexDigit))) return NotFound();
        var target = "mirror-operations://alerts/" + (id ?? "");
        Response.Headers.CacheControl = "no-store";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
        return Content("<!doctype html><html lang=ko><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>"
            + "<title>Mirror 운영 알림</title><body style='font-family:system-ui;padding:24px;background:#f5f7fa'><h1>운영 알림</h1>"
            + "<p>관리자 앱에서 로그인 후 최신 업무 상태를 확인하세요.</p><a href='" + target + "'>관리자 앱 열기</a>"
            + "<p>앱이 열리지 않으면 관리자 앱의 운영 알림 메뉴를 이용하세요.</p></body></html>", "text/html; charset=utf-8");
    }
}
