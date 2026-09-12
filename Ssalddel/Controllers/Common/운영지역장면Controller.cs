using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Filters;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Common;

[ApiController]
[Authorize]
[SsalddelApiVersion(
    SsalddelProductVersion.V2_5,
    FeatureKey = VersionFeatureFlagKeys.OperationalWorldObservationWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.OperationalWorldObservationWorkflow)]
[RequireVersionFeature(VersionFeatureFlagKeys.OperationalWorldObservationWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[Route(OperationalWorldSceneRoutes.AreaSnapshot)]
public sealed class 운영지역장면Controller(I운영지역장면조회UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> 조회(
        string areaStableId,
        [FromQuery] long cursor = 0,
        [FromQuery] string? schemaVersion = null,
        CancellationToken cancellationToken = default)
    {
        var requestedSchema = string.IsNullOrWhiteSpace(schemaVersion)
            ? OperationalWorldScenePolicy.SchemaVersionV1
            : schemaVersion.Trim();
        if (!OperationalWorldScenePolicy.IsSupported(requestedSchema))
        {
            var problem = new ProblemDetails
            {
                Title = "지원하지 않는 운영 지역 장면 계약 판본입니다.",
                Detail = $"schemaVersion은 {OperationalWorldScenePolicy.SchemaVersionV1} 또는 {OperationalWorldScenePolicy.SchemaVersionV2}이어야 합니다.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            };
            problem.Extensions["errorCode"] = "OperationalWorldSceneSchemaVersionUnsupported";
            return BadRequest(problem);
        }

        var response = await useCase.조회Async(
            areaStableId,
            cursor,
            requestedSchema,
            cancellationToken);
        if (string.Equals(requestedSchema, OperationalWorldScenePolicy.SchemaVersionV2, StringComparison.Ordinal))
            return Ok(response);

        // v1은 새 필드가 wire에 섞이지 않도록 기존 응답 모양을 명시적으로 유지합니다.
        return Ok(new
        {
            response.SchemaVersion,
            response.AreaStableId,
            response.Cursor,
            response.IsFullSnapshot,
            response.AsOfUtc,
            response.RefreshAfterSeconds,
            Items = response.Items.Select(item => new
            {
                item.SnapshotStableId,
                item.AreaStableId,
                item.OperatingSystemId,
                item.ItemKind,
                item.RoleCode,
                item.ActivityCode,
                item.Revision,
                item.OccurredAtUtc,
                item.PublishedAtUtc,
                item.ExpiresAtUtc,
                item.IsTombstone,
                item.DataPolicyCode,
                item.LocalStorageAllowed,
                item.ReplayAllowed,
                item.RepresentationDataJson
            }),
            response.SourceFailures
        });
    }
}
