using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Admin;

[ApiController]
[Authorize(Policy = "서버관리자전용")]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[Route(역세권디오라마건물증거Routes.Base)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class 역세권디오라마건물증거Controller(
    I역세권디오라마건물증거조회UseCase useCase,
    IHostEnvironment environment) : ControllerBase
{
    [HttpGet(역세권디오라마건물증거Routes.Manifest)]
    public Task<IActionResult> Manifest(
        string transitStationStableId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            transitStationStableId,
            useCase.ManifestAsync,
            value => value.ProjectionHashSha256,
            cancellationToken);

    [HttpGet(역세권디오라마건물증거Routes.PresentationBuildingBindings)]
    public Task<IActionResult> PresentationBuildingBindings(
        string transitStationStableId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            transitStationStableId,
            useCase.PresentationBuildingBindingsAsync,
            value => value.ProjectionHashSha256,
            cancellationToken);

    [HttpGet(역세권디오라마건물증거Routes.PresentationBuildingAddresses)]
    public Task<IActionResult> PresentationBuildingAddresses(
        string transitStationStableId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            transitStationStableId,
            useCase.PresentationBuildingAddressesAsync,
            value => value.ProjectionHashSha256,
            cancellationToken);

    private async Task<IActionResult> ExecuteAsync<T>(
        string transitStationStableId,
        Func<string, CancellationToken, Task<T?>> read,
        Func<T, string> revision,
        CancellationToken cancellationToken)
        where T : class
    {
        ApplyPrivateNoCache();
        if (!environment.IsDevelopment()) return NotFound();
        if (!StationDioramaPolicy.IsTransitStationStableId(transitStationStableId))
            return BadRequest();
        if (!string.Equals(
                transitStationStableId,
                StationDioramaPolicy.SagajeongTransitStationStableId,
                StringComparison.Ordinal))
            return NotFound();

        try
        {
            var value = await read(transitStationStableId, cancellationToken);
            return value is null ? NotFound() : WithEtag(value, revision(value));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (역세권디오라마건물증거UnavailableException exception)
        {
            return Unavailable(exception.Code);
        }
        catch (Exception)
        {
            // DB 연결과 JSON 원인을 응답에 노출하거나 표본으로 대체하지 않는다.
            return Unavailable("StationDioramaBuildingEvidenceReadUnavailable");
        }
    }

    private IActionResult WithEtag(object value, string revision)
    {
        var etag = "\"" + revision + "\"";
        Response.Headers.ETag = etag;
        if (Request.Headers.IfNoneMatch
            .SelectMany(candidate => candidate?.Split(',', StringSplitOptions.TrimEntries) ?? [])
            .Any(candidate => string.Equals(candidate, etag, StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status304NotModified);
        return Ok(value);
    }

    private void ApplyPrivateNoCache()
    {
        Response.Headers.CacheControl = "private, no-cache, no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private ObjectResult Unavailable(string code)
        => StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "역세권 디오라마 건물 증거를 조회할 수 없습니다.",
            Detail = "누락되었거나 판본과 일치하지 않는 검토 자료는 대체 자료로 추정하지 않습니다.",
            Extensions = { ["code"] = code }
        });
}
