using System.Text.Json;
using Microsoft.Extensions.Logging;
using Hongdal.Contracts.Common.ViewSettings;
using Microsoft.AspNetCore.Http;
using 홍달.Services.Audit;

namespace Hongdal.Application.CommandProcessing;

public sealed class Command감사로그Processor : ICommand후처리Processor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<Command감사로그Processor> _logger;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly I사용자행위로그Service _activityLogService;

    public Command감사로그Processor(
        ILogger<Command감사로그Processor> logger,
        ICurrentUserAccessor currentUserAccessor,
        IHttpContextAccessor httpContextAccessor,
        I사용자행위로그Service activityLogService)
    {
        _logger = logger;
        _currentUserAccessor = currentUserAccessor;
        _httpContextAccessor = httpContextAccessor;
        _activityLogService = activityLogService;
    }

    public string Name => "AuditLog";

    public int Order => 100;

    public bool CanProcess(Command후처리Context context) => Command후처리규칙.IsAuditLogEnabled(context.Rule);

    public async Task ProcessAsync(Command후처리Context context, CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(new
        {
            context.CommandName,
            EventName = context.Rule.EventName,
            Target = context.Rule.Target,
            RequestType = context.Request.GetType().Name,
            ResponseType = context.Response?.GetType().Name ?? string.Empty
        }, JsonOptions);

        _logger.LogInformation(
            "Command 후처리 AuditLog CommandName={CommandName} EventName={EventName} Target={Target} TraceId={TraceId} OccurredAt={OccurredAt} Request={Request}",
            context.CommandName,
            context.Rule.EventName,
            context.Rule.Target,
            context.TraceId,
            context.OccurredAt,
            requestJson);

        await _activityLogService.기록Async(new 사용자행위로그기록
        {
            AppKey = ResolveAppKey(_httpContextAccessor.HttpContext?.Request.Path.Value),
            UserId = _currentUserAccessor.UserId ?? string.Empty,
            RoleName = _currentUserAccessor.Role ?? string.Empty,
            ActionType = "Command",
            ActionName = context.CommandName,
            Route = _httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty,
            TraceId = context.TraceId,
            IsSuccess = context.IsSuccess,
            ClientIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty,
            OccurredAtUtc = context.OccurredAt,
            MetadataJson = requestJson
        }, cancellationToken);
    }

    private static string ResolveAppKey(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Hongdal.Server";
        }

        if (path.StartsWith("/api/v1/driver", StringComparison.OrdinalIgnoreCase))
        {
            return App식별자.DriverApp;
        }

        if (path.StartsWith("/api/v1/shipper", StringComparison.OrdinalIgnoreCase))
        {
            return App식별자.HongdalApp;
        }

        if (path.StartsWith("/api/v1/admin", StringComparison.OrdinalIgnoreCase))
        {
            return App식별자.HongdalAdmin;
        }

        return "Hongdal.Server";
    }
}
