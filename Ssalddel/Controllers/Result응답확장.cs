using FluentResults;
using Ssalddel.Services.Orderer;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Workflow;

namespace Ssalddel.Controllers;

public static class Result응답확장
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result,
        업무실패맥락Dto? workflowContext = null)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return controller.ToFailureActionResult(result.Errors, workflowContext);
    }

    public static IActionResult ToNoContentActionResult(this ControllerBase controller, Result result,
        업무실패맥락Dto? workflowContext = null)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return controller.ToFailureActionResult(result.Errors, workflowContext);
    }

    public static IActionResult ToNoContentActionResult<T>(this ControllerBase controller, Result<T> result,
        업무실패맥락Dto? workflowContext = null)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return controller.ToFailureActionResult(result.Errors, workflowContext);
    }

    public static IActionResult ToActionResult<T>(this ControllerBase controller, 공동구매처리결과<T> result)
    {
        if (result.성공)
        {
            return controller.Ok(result.값);
        }

        if (result.값 is not null)
        {
            return controller.StatusCode(result.상태코드, result.값);
        }

        return controller.ToProblemActionResult(result.메시지, result.상태코드);
    }

    public static IActionResult ToProblemActionResult(this ControllerBase controller, IEnumerable<string> errors)
    {
        return controller.ToProblemActionResult(errors, null);
    }

    public static IActionResult ToProblemActionResult(this ControllerBase controller, string message, int? statusCode = null)
    {
        return controller.ToProblemActionResult([message], statusCode);
    }

    public static IActionResult ToAuthenticationProblem(this ControllerBase controller, string message)
    {
        return controller.ToProblemActionResult(message, StatusCodes.Status401Unauthorized);
    }

    public static IActionResult ToNotFoundProblem(this ControllerBase controller,
        string message = "요청한 데이터를 찾을 수 없습니다.", 업무실패맥락Dto? workflowContext = null, string? errorCode = null)
    {
        return controller.ToProblemActionResult([message], StatusCodes.Status404NotFound, errorCode, workflowContext);
    }

    public static IActionResult ToConflictProblem(this ControllerBase controller, string message)
    {
        return controller.ToProblemActionResult(message, StatusCodes.Status409Conflict);
    }

    public static IActionResult ToForbiddenProblem(this ControllerBase controller, string message = "현재 사용자 또는 역할로는 이 작업을 수행할 수 없습니다.")
    {
        return controller.ToProblemActionResult(message, StatusCodes.Status403Forbidden);
    }

    public static ActionResult To업무실패Problem(
        this ControllerBase controller,
        string message,
        int statusCode,
        string failureCode,
        string failureClassCode,
        string responsibilityRoleCode,
        bool requiresStateRefresh,
        string retryPolicyCode,
        IEnumerable<string>? availableRecoveryActions = null,
        long? currentRevision = null,
        DateTime? retryAfterUtc = null,
        업무실패맥락Dto? workflowContext = null)
    {
        var failure = 실패분류(message, statusCode);
        var problem = new ProblemDetails
        {
            Title = message,
            Status = statusCode,
            Type = failure.Type,
            Detail = failure.Detail,
            Instance = controller.HttpContext?.Request?.Path.Value
        };
        problem.Extensions["errors"] = new[] { message };
        problem.Extensions["errorCode"] = failureCode;
        problem.Extensions["failureClassCode"] = failureClassCode;
        problem.Extensions["responsibilityRoleCode"] = responsibilityRoleCode;
        problem.Extensions["requiresStateRefresh"] = requiresStateRefresh;
        problem.Extensions["retryPolicyCode"] = retryPolicyCode;
        problem.Extensions["availableRecoveryActions"] = availableRecoveryActions?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
        problem.Extensions["traceId"] = controller.HttpContext?.TraceIdentifier ?? string.Empty;
        if (currentRevision.HasValue)
        {
            problem.Extensions["currentRevision"] = currentRevision.Value;
        }

        if (retryAfterUtc.HasValue)
        {
            problem.Extensions["retryAfterUtc"] = retryAfterUtc.Value;
        }

        맥락추가(problem, workflowContext);

        return controller.StatusCode(statusCode, problem);
    }

    private static IActionResult ToProblemActionResult(
        this ControllerBase controller,
        IEnumerable<string> errors,
        int? statusCode,
        string? errorCode = null,
        업무실패맥락Dto? workflowContext = null)
    {
        var messages = errors.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var firstMessage = messages.FirstOrDefault() ?? "요청을 처리할 수 없습니다.";
        var failure = statusCode.HasValue
            ? 실패분류(firstMessage, statusCode.Value)
            : 실패분류(firstMessage);
        var problem = new ProblemDetails
        {
            Title = firstMessage,
            Status = failure.StatusCode,
            Type = failure.Type,
            Detail = failure.Detail,
            Instance = controller.HttpContext?.Request?.Path.Value
        };
        problem.Extensions["errors"] = messages;
        problem.Extensions["errorCode"] = string.IsNullOrWhiteSpace(errorCode)
            ? failure.Code
            : errorCode;
        problem.Extensions["traceId"] = controller.HttpContext?.TraceIdentifier ?? string.Empty;
        맥락추가(problem, workflowContext);

        return controller.StatusCode(failure.StatusCode, problem);
    }

    private static IActionResult ToFailureActionResult(this ControllerBase controller, IReadOnlyCollection<IError> errors,
        업무실패맥락Dto? workflowContext)
    {
        // 사유·상태·복구 맥락은 같은 첫 실패에서 읽는다. 서로 다른 실패의 메타데이터를 섞지 않는다.
        var primary = errors.FirstOrDefault();
        object? Metadata(string key) => primary?.Metadata.GetValueOrDefault(key);
        var statusCode = Metadata("StatusCode") is int status ? status : 0;
        var errorCode = Metadata("ErrorCode") as string;
        var response = controller.ToProblemActionResult(
            errors.Select(x => x.Message),
            statusCode == 0 ? null : statusCode,
            errorCode,
            Metadata("WorkflowContext") as 업무실패맥락Dto ?? workflowContext);
        if (response is not ObjectResult { Value: ProblemDetails problem }) return response;

        // 원문/임의 내부 값은 복제하지 않고 기존 공개 복구 계약의 필드만 전달한다.
        foreach (var (source, target) in new[]
        {
            ("FailureClassCode", "failureClassCode"), ("ResponsibilityRoleCode", "responsibilityRoleCode"),
            ("RetryPolicyCode", "retryPolicyCode")
        })
            if (Metadata(source) is string value && !string.IsNullOrWhiteSpace(value)) problem.Extensions[target] = value;
        if (Metadata("RequiresStateRefresh") is bool refresh) problem.Extensions["requiresStateRefresh"] = refresh;
        if (Metadata("AvailableRecoveryActions") is IEnumerable<string> actions)
            problem.Extensions["availableRecoveryActions"] = actions.Where(action => action is
                업무복구행동Ids.상태전체재조회 or 업무복구행동Ids.동일요청멱등재시도 or 업무복구행동Ids.운영자안전재시도예약)
                .Distinct(StringComparer.Ordinal).ToArray();
        var revision = Metadata("CurrentRevision") switch { long value => value, int value => value, _ => (long?)null };
        if (revision is >= 0) problem.Extensions["currentRevision"] = revision.Value;
        if (Metadata("RetryAfterUtc") is DateTime retryAfter) problem.Extensions["retryAfterUtc"] = retryAfter;
        return response;
    }

    private static void 맥락추가(ProblemDetails problem, 업무실패맥락Dto? context)
    {
        if (context?.유효한가() == true) problem.Extensions["workflowContext"] = context;
    }

    private static FailureClassification 실패분류(string message, int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status401Unauthorized => new FailureClassification(statusCode, "AuthenticationRequired", "https://httpstatuses.com/401", "로그인 세션을 확인한 뒤 다시 시도해야 합니다."),
            StatusCodes.Status403Forbidden => new FailureClassification(statusCode, "Forbidden", "https://httpstatuses.com/403", "현재 사용자 또는 역할로는 이 작업을 수행할 수 없습니다."),
            StatusCodes.Status404NotFound => new FailureClassification(statusCode, "NotFound", "https://httpstatuses.com/404", "요청한 대상 데이터가 존재하지 않거나 조회 범위에 없습니다."),
            StatusCodes.Status409Conflict => new FailureClassification(statusCode, "InvalidState", "https://httpstatuses.com/409", "요청 대상의 현재 상태가 이 작업을 허용하지 않습니다."),
            _ => new FailureClassification(statusCode, "BadRequest", $"https://httpstatuses.com/{statusCode}", "요청값 또는 업무 조건을 확인해야 합니다.")
        };
    }

    internal static FailureClassification 실패분류(string message)
    {
        if (message.Contains("인증 정보", StringComparison.Ordinal)
            || message.Contains("인증이 필요", StringComparison.Ordinal))
        {
            return new FailureClassification(
                StatusCodes.Status401Unauthorized,
                "AuthenticationRequired",
                "https://httpstatuses.com/401",
                "로그인 세션을 확인한 뒤 다시 시도해야 합니다.");
        }

        if (message.Contains("권한", StringComparison.Ordinal)
            || message.Contains("실행할 수 없습니다", StringComparison.Ordinal)
            || message.Contains("접근 조건", StringComparison.Ordinal))
        {
            return new FailureClassification(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "https://httpstatuses.com/403",
                "현재 사용자 또는 역할로는 이 작업을 수행할 수 없습니다.");
        }

        if (message.Contains("찾을 수 없습니다", StringComparison.Ordinal))
        {
            return new FailureClassification(
                StatusCodes.Status404NotFound,
                "NotFound",
                "https://httpstatuses.com/404",
                "요청한 대상 데이터가 존재하지 않거나 조회 범위에 없습니다.");
        }

        if (message.Contains("이미", StringComparison.Ordinal)
            || message.Contains("다른 기사", StringComparison.Ordinal)
            || message.Contains("현재 상태", StringComparison.Ordinal)
            || message.Contains("가능한 배차가 아닙니다", StringComparison.Ordinal)
            || message.Contains("수락할 수 없습니다", StringComparison.Ordinal)
            || message.Contains("결제완료 의뢰만", StringComparison.Ordinal))
        {
            return new FailureClassification(
                StatusCodes.Status409Conflict,
                "InvalidState",
                "https://httpstatuses.com/409",
                "요청 대상의 현재 상태가 이 작업을 허용하지 않습니다.");
        }

        if (message.Contains("처리 중 오류", StringComparison.Ordinal)
            || message.Contains("잠시 후 다시 시도", StringComparison.Ordinal))
        {
            return new FailureClassification(
                StatusCodes.Status503ServiceUnavailable,
                "TemporaryFailure",
                "https://httpstatuses.com/503",
                "일시적인 처리 실패입니다. 같은 요청을 다시 시도할 수 있습니다.");
        }

        return new FailureClassification(
            StatusCodes.Status400BadRequest,
            "BadRequest",
            "https://httpstatuses.com/400",
            "요청값 또는 업무 조건을 확인해야 합니다.");
    }

    internal sealed record FailureClassification(
        int StatusCode,
        string Code,
        string Type,
        string Detail);
}
