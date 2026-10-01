using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.ViewSettings;
using 홍달.Services.Audit;

namespace Hongdal.Middleware;

public sealed class 사용자행위로그Middleware : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I사용자행위로그Service _activityLogService;

    public 사용자행위로그Middleware(ICurrentUserAccessor currentUserAccessor, I사용자행위로그Service activityLogService)
    {
        _currentUserAccessor = currentUserAccessor;
        _activityLogService = activityLogService;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase))
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
                var actionName = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? string.Empty;
                var metadata = JsonSerializer.Serialize(new
                {
                    Method = context.Request.Method,
                    QueryString = context.Request.QueryString.Value ?? string.Empty,
                    StatusCode = context.Response.StatusCode,
                    Endpoint = context.GetEndpoint()?.DisplayName ?? string.Empty,
                    Url = context.Request.GetDisplayUrl()
                }, JsonOptions);

                await _activityLogService.기록Async(new 사용자행위로그기록
                {
                    AppKey = ResolveAppKey(context.Request.Path),
                    UserId = _currentUserAccessor.UserId ?? string.Empty,
                    RoleName = _currentUserAccessor.Role ?? string.Empty,
                    ActionType = actionType,
                    ActionName = actionName,
                    Route = context.Request.Path.Value ?? string.Empty,
                    TraceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
                    IsSuccess = capturedException is null && context.Response.StatusCode < 400,
                    ErrorCode = capturedException?.GetType().Name ?? string.Empty,
                    ErrorMessage = capturedException?.Message ?? string.Empty,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    UserAgent = context.Request.Headers.UserAgent.ToString(),
                    OccurredAtUtc = DateTime.UtcNow,
                    MetadataJson = metadata
                });
            }
            catch
            {
                // activity log failure must not break main request pipeline
            }
        }
    }

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
            return App식별자.HongdalApp;
        }

        if (value.StartsWith("/api/v1/admin", StringComparison.OrdinalIgnoreCase))
        {
            return App식별자.HongdalAdmin;
        }

        return "Hongdal.Server";
    }
}
