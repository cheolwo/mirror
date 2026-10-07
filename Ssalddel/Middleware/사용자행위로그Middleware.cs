using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.ViewSettings;
using 살뜰.Services.Audit;
using 살뜰.Services.Options;

namespace Ssalddel.Middleware;

public sealed class 사용자행위로그Middleware : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I사용자행위로그Service _activityLogService;
    private readonly IHostEnvironment _environment;
    private readonly ISsalddelExecutionModePolicy _executionMode;
    private readonly SsalddelExecutionOptions _executionOptions;
    private readonly ILogger<사용자행위로그Middleware>? _logger;

    public 사용자행위로그Middleware(
        ICurrentUserAccessor currentUserAccessor,
        I사용자행위로그Service activityLogService,
        IHostEnvironment environment,
        ISsalddelExecutionModePolicy executionMode,
        IOptions<SsalddelExecutionOptions> executionOptions,
        ILogger<사용자행위로그Middleware>? logger = null)
    {
        _currentUserAccessor = currentUserAccessor;
        _activityLogService = activityLogService;
        _environment = environment;
        _executionMode = executionMode;
        _executionOptions = executionOptions.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (ShouldSkipDevelopmentMapReadAudit(
                _environment.IsDevelopment(),
                _executionMode.IsSimulation,
                _executionOptions.DevelopmentReadOnly,
                context.Request.Method,
                context.Request.Path))
        {
            await next(context);
            return;
        }

        Exception? capturedException = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            try
            {
                var actionType = ResolveActionType(context.Request.Method, context.Request.Path);
                var routeTemplate = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "/api/v1/unmatched";
                var actionName = context.GetEndpoint()?.DisplayName ?? routeTemplate;
                var metadata = JsonSerializer.Serialize(new
                {
                    Method = context.Request.Method,
                    Query = 사용자행위로그보호Policy.SafeQuery(context.Request.Query),
                    StatusCode = context.Response.StatusCode,
                    RouteTemplate = routeTemplate
                }, JsonOptions);

                await _activityLogService.기록Async(new 사용자행위로그기록
                {
                    AppKey = ResolveAppKey(context.Request.Path),
                    UserId = _currentUserAccessor.UserId ?? string.Empty,
                    RoleName = _currentUserAccessor.Role ?? string.Empty,
                    ActionType = actionType,
                    ActionName = actionName,
                    Route = routeTemplate,
                    TraceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
                    IsSuccess = capturedException is null && context.Response.StatusCode < 400,
                    ErrorCode = capturedException?.GetType().Name ?? string.Empty,
                    ErrorMessage = 사용자행위로그보호Policy.SafeError(capturedException?.Message),
                    ClientIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    UserAgent = context.Request.Headers.UserAgent.ToString(),
                    OccurredAtUtc = DateTime.UtcNow,
                    MetadataJson = metadata
                });
            }
            catch (Exception auditException)
            {
                // Keep the request result, but alert without exception text, URL values or personal data.
                _logger?.LogError("SecurityAuditPersistenceFailure Method={Method} ErrorType={ErrorType}",
                    context.Request.Method, auditException.GetType().Name);
            }
        }
    }

    internal static bool ShouldSkipDevelopmentMapReadAudit(
        bool isDevelopment,
        bool isSimulation,
        bool developmentReadOnly,
        string method,
        PathString path)
        => isDevelopment
           && isSimulation
           && developmentReadOnly
           && HttpMethods.IsGet(method)
           && (path.Equals(new PathString("/api/v1/platform/runtime/google-maps"))
               || path.Equals(new PathString("/api/v1/community/world-map/observations")));

    private static string ResolveActionType(string method, PathString path)
    {
        if (path.StartsWithSegments("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
        {
            return "Auth";
        }

        return method.ToUpperInvariant() switch
        {
            "GET" => "Read",
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Update",
            "DELETE" => "Delete",
            _ => "Http"
        };
    }

    private static string ResolveAppKey(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.StartsWith("/api/v1/driver", StringComparison.OrdinalIgnoreCase))
        {
            return App식별자.DriverApp;
        }

        if (value.StartsWith("/api/v1/shipper", StringComparison.OrdinalIgnoreCase))
        {
            return App식별자.SsalddelApp;
        }

        if (value.StartsWith("/api/v1/admin", StringComparison.OrdinalIgnoreCase))
        {
            return App식별자.SsalddelAdmin;
        }

        return "Ssalddel.Server";
    }
}
