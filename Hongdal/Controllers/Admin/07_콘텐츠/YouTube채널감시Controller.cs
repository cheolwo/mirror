using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Admin.Content07;

[HongdalApiVersion(HongdalProductVersion.V1_0)]
[ApiController]
[Route("api/v1/admin/content/youtube")]
[Authorize(Policy = "서버관리자전용")]
public sealed class YouTube채널감시Controller : ControllerBase
{
    private readonly IYouTube채널감시Service _service;

    public YouTube채널감시Controller(IYouTube채널감시Service service)
    {
        _service = service;
    }

    [HttpGet("channels")]
    public async Task<IActionResult> 채널목록조회(
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken)
        => Ok(await _service.채널목록조회Async(countryCode, cancellationToken));

    [HttpGet("channels/search")]
    public async Task<IActionResult> 음식채널검색(
        [FromQuery(Name = "query")] string 검색어,
        [FromQuery] int take = 10,
        [FromQuery] string? regionCode = null,
        [FromQuery] string? languageCode = null,
        CancellationToken cancellationToken = default)
        => Ok(await _service.채널검색Async(
            검색어,
            take,
            regionCode,
            languageCode,
            cancellationToken));

    [HttpPost("channels")]
    public async Task<IActionResult> 채널등록(
        [FromBody] YouTube감시채널등록요청Dto 요청,
        CancellationToken cancellationToken)
    {
        var created = await _service.채널등록Async(요청, cancellationToken);
        return CreatedAtAction(nameof(채널목록조회), created);
    }

    [HttpPut("channels/{channelId}/food-profile")]
    public async Task<IActionResult> 음식채널프로필설정(
        [FromRoute] string channelId,
        [FromBody] YouTube음식채널프로필설정요청Dto 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.음식채널프로필설정Async(channelId, 요청, cancellationToken));

    [HttpPut("channels/{channelId}/knowledge-reflection-profile")]
    public async Task<IActionResult> 지식성찰채널프로필설정(
        [FromRoute] string channelId,
        [FromBody] YouTube지식성찰채널프로필설정요청Dto 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.지식성찰채널프로필설정Async(channelId, 요청, cancellationToken));

    [HttpPut("channels/{channelId}/prajna-publication")]
    public async Task<IActionResult> 반야게시채널설정(
        [FromRoute] string channelId,
        [FromBody] YouTube반야게시채널설정요청Dto 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.반야게시채널설정Async(channelId, 요청.허용여부, cancellationToken));

    [HttpGet("videos")]
    public async Task<IActionResult> 영상목록조회(
        [FromQuery] string? channelId,
        [FromQuery] bool newOnly = false,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => Ok(await _service.영상목록조회Async(channelId, newOnly, take, cancellationToken));

    [HttpGet("playlists")]
    public async Task<IActionResult> 재생목록조회(
        [FromQuery] string channelId,
        CancellationToken cancellationToken = default)
        => Ok(await _service.재생목록목록조회Async(channelId, cancellationToken));

    [HttpGet("playlists/{playlistId}/videos")]
    public async Task<IActionResult> 재생목록영상조회(
        [FromRoute] string playlistId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => Ok(await _service.재생목록영상목록조회Async(playlistId, take, cancellationToken));

    [HttpPut("videos/{videoId}/publication")]
    public async Task<IActionResult> 영상공개설정(
        [FromRoute] string videoId,
        [FromBody] YouTube영상공개설정요청Dto 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.영상공개설정Async(videoId, 요청.공개여부, cancellationToken));

    [HttpPost("sync")]
    public async Task<IActionResult> 동기화(
        [FromQuery] string? channelId,
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(channelId) && !string.IsNullOrWhiteSpace(countryCode))
        {
            return BadRequest("channelId와 countryCode는 동시에 지정할 수 없습니다.");
        }

        return Ok(string.IsNullOrWhiteSpace(countryCode)
            ? await _service.동기화Async(channelId, cancellationToken)
            : await _service.국가별동기화Async(countryCode, cancellationToken));
    }
}
