using System.Security.Claims;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using Hongdal.Services.External.YouTube;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Controllers.Admin.Content07;

[HongdalApiVersion(HongdalProductVersion.V1_0)]
[ApiController]
[Route("api/v1/admin/content/youtube-food")]
[Authorize(Policy = "서버관리자전용")]
public sealed class YouTube음식상품관리Controller : ControllerBase
{
    private readonly IYouTube음식상품발견Service _service;
    private readonly IYouTube영상재료자동인지Service _ingredientRecognitionService;
    private readonly IYouTubeTranscriptSource _transcriptSource;

    public YouTube음식상품관리Controller(
        IYouTube음식상품발견Service service,
        IYouTube영상재료자동인지Service ingredientRecognitionService,
        IYouTubeTranscriptSource transcriptSource)
    {
        _service = service;
        _ingredientRecognitionService = ingredientRecognitionService;
        _transcriptSource = transcriptSource;
    }

    [HttpGet("product-candidates")]
    public async Task<IActionResult> 상품후보목록(
        [FromQuery] string? reviewStatus,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
        => Ok(await _service.상품후보목록조회Async(reviewStatus, take, cancellationToken));

    [HttpPost("product-candidates")]
    public async Task<IActionResult> 상품후보등록(
        [FromBody] YouTube상품후보등록요청Dto 요청,
        CancellationToken cancellationToken)
    {
        var created = await _service.상품후보등록Async(요청, cancellationToken);
        return CreatedAtAction(nameof(상품후보목록), new { created.후보Id }, created);
    }

    [HttpPost("videos/{videoId}/transcript")]
    public async Task<IActionResult> 영상자막조회(
        [FromRoute] string videoId,
        [FromQuery] string? targetLanguage,
        CancellationToken cancellationToken)
    {
        var result = await _transcriptSource.GetAsync(
            new YouTubeTranscriptRequest(videoId, targetLanguage),
            cancellationToken);
        return result is null
            ? NotFound("공개 자막을 찾지 못했습니다.")
            : Ok(result);
    }

    [HttpPost("videos/{videoId}/ingredient-recognition")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> 영상재료자동인지(
        [FromRoute] string videoId,
        [FromForm] YouTube영상재료자동인지Form form,
        CancellationToken cancellationToken)
    {
        if (!form.ContentAnalysisAuthorized)
        {
            return BadRequest("분석할 자막과 영상 프레임에 대한 사용 권한을 확인해야 합니다.");
        }

        var timestamps = ParseFrameTimestamps(form.FrameTimestampsSeconds, form.Frames.Count);
        var frames = new List<YouTube영상재료인지업로드프레임>(form.Frames.Count);
        for (var index = 0; index < form.Frames.Count; index++)
        {
            var file = form.Frames[index];
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            frames.Add(new YouTube영상재료인지업로드프레임(
                timestamps[index],
                file.ContentType,
                stream.ToArray()));
        }

        var result = await _ingredientRecognitionService.분석Async(
            videoId,
            new YouTube영상재료자동인지요청(
                form.ContentAnalysisAuthorized,
                form.Transcript,
                frames),
            cancellationToken);
        return result.실행됨 ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    [HttpPut("product-candidates/{candidateId:long}/review")]
    public async Task<IActionResult> 상품후보검수(
        [FromRoute] long candidateId,
        [FromBody] YouTube상품후보검수요청Dto 요청,
        CancellationToken cancellationToken)
        => Ok(await _service.상품후보검수Async(
            candidateId,
            요청,
            CurrentUserId(),
            cancellationToken));

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? throw new UnauthorizedAccessException("관리자 식별자를 찾을 수 없습니다.");

    private static IReadOnlyList<int?> ParseFrameTimestamps(string? value, int frameCount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Enumerable.Repeat<int?>(null, frameCount).ToArray();
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != frameCount)
        {
            throw new ArgumentException("프레임 시각의 개수는 업로드한 프레임 수와 같아야 합니다.", nameof(value));
        }

        return parts.Select(part =>
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                return (int?)null;
            }

            if (!int.TryParse(part, out var seconds) || seconds < 0)
            {
                throw new ArgumentException("프레임 시각은 0 이상의 초 단위 정수여야 합니다.", nameof(value));
            }

            return (int?)seconds;
        }).ToArray();
    }
}

public sealed class YouTube영상재료자동인지Form
{
    public bool ContentAnalysisAuthorized { get; set; }

    public string? Transcript { get; set; }

    /// <summary>
    /// 업로드 순서에 맞춘 쉼표 구분 초 단위 시각입니다. 예: 10,45,120
    /// </summary>
    public string? FrameTimestampsSeconds { get; set; }

    public List<IFormFile> Frames { get; set; } = [];
}
