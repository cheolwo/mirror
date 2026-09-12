using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Filters;
using Ssalddel.Services.Development.ObservableOperations;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Development;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[SsalddelApiVersion(
    SsalddelProductVersion.V2_5,
    FeatureKey = VersionFeatureFlagKeys.OperationalWorldObservationWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.OperationalWorldObservationWorkflow)]
[RequireVersionFeature(VersionFeatureFlagKeys.OperationalWorldObservationWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[Route("verification/observable-operations")]
public sealed class 관찰운영검증Controller(
    IConfiguration configuration,
    IServiceProvider services) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
        => TryResolve(out _) ? NoContent() : NotFound();

    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken cancellationToken)
        => !TryResolve(out var useCase)
            ? NotFound()
            : !Authorized()
                ? Unauthorized()
                : Ok(await useCase.ReadAsync(cancellationToken));

    [HttpGet("scene-snapshots")]
    public async Task<IActionResult> ReadSceneSnapshots(
        [FromQuery] long cursor,
        CancellationToken cancellationToken)
    {
        if (!TryResolve(out _))
            return NotFound();
        if (!Authorized())
            return Unauthorized();
        var worldReader = services.GetRequiredService<Ssalddel.Application.WorldProjection.I운영지역장면조회UseCase>();
        return Ok(await worldReader.조회Async(
            관찰운영검증Runner.DefaultAreaStableId,
            cursor,
            Ssalddel.WorkflowRules.Contracts.OperationalWorldScenePolicy.SchemaVersionV2,
            cancellationToken));
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromBody] 관찰운영검증시작요청? request,
        CancellationToken cancellationToken)
        => !TryResolve(out var useCase)
            ? NotFound()
            : !Authorized()
                ? Unauthorized()
                : Accepted(await useCase.StartAsync(request?.RunStableId, cancellationToken));

    [HttpPost("pause")]
    public async Task<IActionResult> Pause(CancellationToken cancellationToken)
        => !TryResolve(out var useCase)
            ? NotFound()
            : !Authorized()
                ? Unauthorized()
                : Ok(await useCase.PauseAsync(cancellationToken));

    [HttpPost("resume")]
    public async Task<IActionResult> Resume(CancellationToken cancellationToken)
        => !TryResolve(out var useCase)
            ? NotFound()
            : !Authorized()
                ? Unauthorized()
                : Ok(await useCase.ResumeAsync(cancellationToken));

    [HttpPost("outbox/retry")]
    public async Task<IActionResult> Retry(CancellationToken cancellationToken)
        => !TryResolve(out var useCase)
            ? NotFound()
            : !Authorized()
                ? Unauthorized()
                : Ok(await useCase.RetryAsync(cancellationToken));

    private bool TryResolve(out I관찰운영검증UseCase useCase)
    {
        var enabled = configuration.GetValue<bool>($"{관찰운영검증Options.Section}:Enabled");
        useCase = enabled
            ? services.GetService<I관찰운영검증UseCase>()!
            : null!;
        return enabled && useCase is not null;
    }

    private bool Authorized()
    {
        var expected = configuration[$"{관찰운영검증Options.Section}:AccessKey"] ?? string.Empty;
        var actual = Request.Headers["X-Verification-Key"].ToString();
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
               && expectedBytes.Length >= 32
               && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
