using FluentResults;
using Ssalddel.Services.Orderer;
using Microsoft.AspNetCore.Mvc;

namespace Ssalddel.Controllers;

public static class Result응답확장
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return controller.ToFailureActionResult(result.Errors);
    }

    public static IActionResult ToNoContentActionResult(this ControllerBase controller, Result result)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return controller.ToFailureActionResult(result.Errors);
    }

    public static IActionResult ToNoContentActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return controller.ToFailureActionResult(result.Errors);
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

    public static IActionResult ToNotFoundProblem(this ControllerBase controller, string message = "요청한 데이터를 찾을 수 없습니다.")
    {
        return controller.ToProblemActionResult(message, StatusCodes.Status404NotFound);
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
        DateTime? retryAfterUtc = null)
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

        return controller.StatusCode(statusCode, problem);
    }

    private static IActionResult ToProblemActionResult(
        this ControllerBase controller,
        IEnumerable<string> errors,
        int? statusCode,
        string? errorCode = null)
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

        return controller.StatusCode(failure.StatusCode, problem);
    }

    private static IActionResult ToFailureActionResult(this ControllerBase controller, IReadOnlyCollection<IError> errors)
    {
        var statusCode = errors
            .Select(x => x.Metadata.TryGetValue("StatusCode", out var value) ? value : null)
            .OfType<int>()
            .FirstOrDefault();
        var errorCode = errors
            .Select(x => x.Metadata.TryGetValue("ErrorCode", out var value) ? value : null)
            .OfType<string>()
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return controller.ToProblemActionResult(
            errors.Select(x => x.Message),
            statusCode == 0 ? null : statusCode,
            errorCode);
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
