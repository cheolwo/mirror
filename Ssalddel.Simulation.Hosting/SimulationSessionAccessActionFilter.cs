using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Persistence;

namespace Ssalddel.Simulation.Hosting;

/// <summary>
/// 로그인 계정과 Simulation 세션의 접근 원장을 HTTP 경계에서 결속한다.
/// Simulation Domain에는 운영 계정 식별자를 넣지 않는다.
/// </summary>
public sealed class SimulationSessionAccessActionFilter(
    ISimulationSessionAccessLedger accessLedger,
    ISimulationSessionSaveStore saveStore,
    IHostEnvironment environment,
    IConfiguration configuration) : IAsyncActionFilter
{
    public const string SessionNotFoundCode = "SimulationSessionNotFound";

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var subjectId = context.HttpContext.User.FindFirstValue(
                            ClaimTypes.NameIdentifier)
                        ?? context.HttpContext.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (context.Controller is Controllers.SimulationOnlineWorldsController)
        {
            // 공유 온라인 세계는 방 소유자·초대자·참여자 권한을 해당 서비스가
            // 같은 로그인 주체로 판정한다. 개인 세션 단일 소유 원장을 적용하지 않는다.
            await next();
            return;
        }

        var targetSessionStableId = ResolveTargetSessionStableId(context);
        if (!string.IsNullOrWhiteSpace(targetSessionStableId)
            && !await EnsureOwnershipAsync(
                subjectId,
                targetSessionStableId,
                context.HttpContext.RequestAborted))
        {
            context.Result = SessionNotFound();
            return;
        }

        SimulationSessionAccessReservation? reservation = null;
        var creationRequest = context.ActionArguments.Values
            .OfType<경영SimulationSession생성Request>()
            .SingleOrDefault();
        if (creationRequest is not null)
        {
            var sessionStableId = "simulation-session:"
                                  + creationRequest.ClientRequestId.ToString("N");
            reservation = await accessLedger.ReserveAsync(
                subjectId,
                sessionStableId,
                context.HttpContext.RequestAborted);
            if (!reservation.Granted)
            {
                context.Result = SessionNotFound();
                return;
            }
        }

        var executed = await next();
        if (reservation is not null && (!IsSuccessful(executed) || executed.Exception is not null))
        {
            await accessLedger.ReleaseAsync(
                reservation,
                context.HttpContext.RequestAborted);
            return;
        }

        if (reservation is null
            && string.IsNullOrWhiteSpace(targetSessionStableId)
            && TryResolveSessionStableId(executed.Result, out var createdSessionStableId))
        {
            var result = await accessLedger.ReserveAsync(
                subjectId,
                createdSessionStableId,
                context.HttpContext.RequestAborted);
            if (!result.Granted)
                executed.Result = SessionNotFound();
        }
    }

    private async Task<bool> EnsureOwnershipAsync(
        string subjectId,
        string sessionStableId,
        CancellationToken cancellationToken)
    {
        if (await accessLedger.OwnsAsync(
                subjectId,
                sessionStableId,
                cancellationToken))
        {
            return true;
        }

        // 일부 기존 HTTP 계약 시험은 같은 시험 호스트의 Application 서비스를
        // 직접 호출해 세션을 준비한다. 운영 환경에서 미등록 세션을 먼저 호출한
        // 계정이 가져가는 동작은 금지하고, 정확한 Testing 환경에서 명시적으로
        // 우회를 켠 경우에만 고정 시험 주체의 접근 원장을 보충한다.
        if (!environment.IsEnvironment("Testing")
            || !configuration.GetValue<bool>(
                SsalddelSimulationModuleOptions.SectionName
                + ":"
                + nameof(SsalddelSimulationModuleOptions.AllowUnauthenticatedTesting)))
        {
            return false;
        }

        var reservation = await accessLedger.ReserveAsync(
            subjectId,
            sessionStableId,
            cancellationToken);
        return reservation.Granted;
    }

    private string? ResolveTargetSessionStableId(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("sessionStableId", out var routeValue)
            && routeValue is string routeSessionStableId
            && !string.IsNullOrWhiteSpace(routeSessionStableId))
        {
            return routeSessionStableId.Trim();
        }

        var restore = context.ActionArguments.Values
            .OfType<SimulationSessionRestoreRequest>()
            .SingleOrDefault();
        if (restore is not null && !string.IsNullOrWhiteSpace(restore.SaveStableId))
            return saveStore.Find(restore.SaveStableId)?.SessionStableId;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (TryReadSessionStableId(argument, out var value))
                return value;
        }

        return null;
    }

    private static bool TryResolveSessionStableId(
        IActionResult? result,
        out string sessionStableId)
    {
        var value = result switch
        {
            ObjectResult objectResult => objectResult.Value,
            _ => null,
        };
        return TryReadSessionStableId(value, out sessionStableId);
    }

    private static bool TryReadSessionStableId(
        object? value,
        out string sessionStableId)
    {
        sessionStableId = string.Empty;
        if (value is null || value is string)
            return false;

        var property = value.GetType().GetProperty(
            "SessionStableId",
            BindingFlags.Instance | BindingFlags.Public);
        if (property?.PropertyType != typeof(string))
            return false;

        var candidate = property.GetValue(value) as string;
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        sessionStableId = candidate.Trim();
        return true;
    }

    private static bool IsSuccessful(ActionExecutedContext context)
    {
        var statusCode = context.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? StatusCodes.Status200OK,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => StatusCodes.Status200OK,
        };
        return statusCode < StatusCodes.Status400BadRequest;
    }

    private static NotFoundObjectResult SessionNotFound()
        => new(new SimulationErrorResponse { ErrorCode = SessionNotFoundCode });
}
