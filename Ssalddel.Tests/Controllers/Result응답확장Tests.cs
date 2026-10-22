using FluentResults;
using Ssalddel.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Controllers;

public sealed class Result응답확장Tests
{
    [Theory]
    [InlineData("기사 인증 정보가 없습니다.", StatusCodes.Status401Unauthorized, "AuthenticationRequired")]
    [InlineData("통관 조회 동의 등록 권한이 없습니다.", StatusCodes.Status403Forbidden, "Forbidden")]
    [InlineData("배차대기 데이터를 찾을 수 없습니다.", StatusCodes.Status404NotFound, "NotFound")]
    [InlineData("수락 가능한 배차가 아닙니다.", StatusCodes.Status409Conflict, "InvalidState")]
    [InlineData("결제완료 의뢰만 수락할 수 있습니다.", StatusCodes.Status409Conflict, "InvalidState")]
    [InlineData("거절 처리 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.", StatusCodes.Status503ServiceUnavailable, "TemporaryFailure")]
    public void ToActionResult_MapsKnownFailuresToProblemDetails(string message, int expectedStatus, string expectedCode)
    {
        var controller = new TestController();

        var actionResult = controller.ToActionResult(Result.Fail<object>(message));

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal(message, problem.Title);
        Assert.Equal(expectedCode, problem.Extensions["errorCode"]);
        Assert.Equal("test-trace", problem.Extensions["traceId"]);
    }

    [Fact]
    public void 구조화된첫실패의맥락과기존복구필드만전달하고_내부메타데이터는다루지않는다()
    {
        var context = new 업무실패맥락Dto(OperatingSystemIds.DomesticCargoTransport,
            OperatingSystemLifecycleStageIds.CargoPickup, 업무처리결과Codes.미적용);
        var error = new Error("상차 사진 확인")
            .WithMetadata("StatusCode", 400).WithMetadata("ErrorCode", "CargoPickupPhotoRequired")
            .WithMetadata("WorkflowContext", context)
            .WithMetadata("FailureClassCode", 업무실패분류Codes.업무규칙차단)
            .WithMetadata("ResponsibilityRoleCode", OperatingSystemIds.DomesticCargoTransport)
            .WithMetadata("RequiresStateRefresh", true)
            .WithMetadata("RetryPolicyCode", 업무재시도정책Codes.상태재조회후사용자재시도)
            .WithMetadata("AvailableRecoveryActions", new[] { "RefreshState", "RefreshState", "unsafe-command" })
            .WithMetadata("CurrentRevision", 8L).WithMetadata("PrivateStoragePath", "/private/photo");
        var fallback = context with { StageId = OperatingSystemLifecycleStageIds.CargoTransport,
            OutcomeCode = 업무처리결과Codes.미확정 };
        var result = Assert.IsType<ObjectResult>(new TestController().ToActionResult(Result.Fail<object>(error), fallback));
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("CargoPickupPhotoRequired", problem.Extensions["errorCode"]);
        Assert.Equal(context, problem.Extensions["workflowContext"]);
        Assert.Equal(업무실패분류Codes.업무규칙차단, problem.Extensions["failureClassCode"]);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, problem.Extensions["responsibilityRoleCode"]);
        Assert.Equal(true, problem.Extensions["requiresStateRefresh"]);
        Assert.Equal(8L, problem.Extensions["currentRevision"]);
        Assert.Equal(new[] { "RefreshState" }, Assert.IsType<string[]>(problem.Extensions["availableRecoveryActions"]));
        Assert.False(problem.Extensions.ContainsKey("PrivateStoragePath"));
    }

    [Fact]
    public void 서로다른실패의코드나맥락을첫실패와섞지않는다()
    {
        var later = new Error("다른 업무")
            .WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", "LaterError")
            .WithMetadata("WorkflowContext", new 업무실패맥락Dto(OperatingSystemIds.DomesticCargoTransport,
                OperatingSystemLifecycleStageIds.CargoDropoff, 업무처리결과Codes.미적용));
        var result = new TestController().ToActionResult(Result.Fail<object>(new[] { new Error("먼저 확인"), later }));
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(400, problem.Status);
        Assert.Equal("BadRequest", problem.Extensions["errorCode"]);
        Assert.False(problem.Extensions.ContainsKey("workflowContext"));
    }

    [Fact]
    public void 알려지지않은단계의맥락은노출하지않고_기존오류는보존한다()
    {
        var context = new 업무실패맥락Dto(OperatingSystemIds.DomesticCargoTransport,
            OperatingSystemLifecycleStageIds.FoodCooking, 업무처리결과Codes.미확정);
        var result = new TestController().ToActionResult(Result.Fail<object>("입력 확인"), context);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("입력 확인", problem.Title);
        Assert.False(problem.Extensions.ContainsKey("workflowContext"));
    }

    [Fact]
    public void 조회실패는명령의미적용과구분하며_성공응답은맥락을추가하지않는다()
    {
        var context = new 업무실패맥락Dto(OperatingSystemIds.DomesticCargoTransport,
            OperatingSystemLifecycleStageIds.CargoTransport, 업무처리결과Codes.조회);
        var result = new TestController().ToNotFoundProblem("운송 조회", context, "CargoTransportNotFound");
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(context, problem.Extensions["workflowContext"]);
        Assert.Equal("CargoTransportNotFound", problem.Extensions["errorCode"]);
        var payload = new object();
        Assert.Same(payload, Assert.IsType<OkObjectResult>(new TestController().ToActionResult(Result.Ok(payload), context)).Value);
    }

    private sealed class TestController : ControllerBase
    {
        public TestController()
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "test-trace"
                }
            };
            ControllerContext.HttpContext.Request.Path = "/test";
        }
    }
}
