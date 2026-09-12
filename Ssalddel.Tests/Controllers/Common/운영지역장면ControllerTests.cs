using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Controllers.Common;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Controllers.Common;

public sealed class 운영지역장면ControllerTests
{
    [Fact]
    public async Task 판본을생략하면_v1응답모양에_v2필드가노출되지않는다()
    {
        var useCase = new ReturningUseCase();
        var controller = new 운영지역장면Controller(useCase)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.조회("area:test", 0, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Equal(OperationalWorldScenePolicy.SchemaVersionV1, useCase.RequestedSchemaVersion);
        Assert.Contains(OperationalWorldScenePolicy.SchemaVersionV1, json, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkStableId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceKindCode", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 지원하지않는_schemaVersion은_400ProblemDetails로거절한다()
    {
        var controller = new 운영지역장면Controller(new NeverCalledUseCase())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.조회(
            "area:test",
            0,
            "operational-world-scene.v999",
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("OperationalWorldSceneSchemaVersionUnsupported", problem.Extensions["errorCode"]);
    }

    private sealed class NeverCalledUseCase : I운영지역장면조회UseCase
    {
        public Task<OperationalWorldSceneResponse> 조회Async(
            string areaStableId,
            long cursor,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("should not be called");

        public Task<OperationalWorldSceneResponse> 조회Async(
            string areaStableId,
            long cursor,
            string schemaVersion,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("should not be called");
    }

    private sealed class ReturningUseCase : I운영지역장면조회UseCase
    {
        public string RequestedSchemaVersion { get; private set; } = string.Empty;

        public Task<OperationalWorldSceneResponse> 조회Async(
            string areaStableId,
            long cursor,
            CancellationToken cancellationToken)
            => 조회Async(areaStableId, cursor, OperationalWorldScenePolicy.SchemaVersionV1, cancellationToken);

        public Task<OperationalWorldSceneResponse> 조회Async(
            string areaStableId,
            long cursor,
            string schemaVersion,
            CancellationToken cancellationToken)
        {
            RequestedSchemaVersion = schemaVersion;
            return Task.FromResult(new OperationalWorldSceneResponse
            {
                SchemaVersion = schemaVersion,
                AreaStableId = areaStableId,
                Items =
                [
                    new OperationalWorldSceneItem
                    {
                        SnapshotStableId = "snapshot:test",
                        AreaStableId = areaStableId,
                        WorkStableId = "work:test",
                        SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample
                    }
                ]
            });
        }
    }
}
