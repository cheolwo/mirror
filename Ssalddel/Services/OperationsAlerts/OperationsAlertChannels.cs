using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Domain.Notifications;
using Ssalddel.Services.Notifications;
using 살뜰.Data;
using 살뜰.Services.Notifications;

namespace Ssalddel.Services.OperationsAlerts;

public interface IOperationsAlertChannel
{
    string Key { get; }
    Task<string> SendAsync(OperationsAlertOutbox notice, CancellationToken ct);
}
public sealed class OperationsAlertEmailChannel(IOptionsMonitor<OperationsAlertsOptions> options) : IOperationsAlertChannel
{
    public string Key => "Email";
    public async Task<string> SendAsync(OperationsAlertOutbox notice, CancellationToken ct)
    {
        var o = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.RecipientEmail) || string.IsNullOrWhiteSpace(o.Gmail.UserName) || string.IsNullOrWhiteSpace(o.Gmail.AppPassword)
            || !Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out var url) || url.Scheme != "https") return "ConfigurationRequired";
        var path = notice.AlertId is null ? "/operations-alerts" : "/operations-alerts/" + Uri.EscapeDataString(notice.AlertId);
        try
        {
            await GmailSmtpTransport.SendAsync(o.Gmail, o.RecipientEmail, "[Mirror] 운영 확인 알림",
                "운영 앱에서 최신 업무 상태를 확인하세요.\n알림 ID: " + notice.Id + "\n시각(UTC): " + DateTime.SpecifyKind(notice.CreatedAtUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture)
                + "\n" + new Uri(url, path).AbsoluteUri, "X-Mirror-Operations-Notification-Id", notice.Id, ct);
            return "Sent";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return "Failed"; }
    }
}
public sealed class OperationsAlertPushChannel(SsalddelContext db, IFcmPushService push,
    IOptions<살뜰.Services.Options.PushNotificationsOptions> configuration) : IOperationsAlertChannel
{
    public string Key => "Push";
    public async Task<string> SendAsync(OperationsAlertOutbox notice, CancellationToken ct)
    {
        var o = configuration.Value;
        var credentials = string.IsNullOrWhiteSpace(o.ServiceAccountJsonPath)
            ? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") : o.ServiceAccountJsonPath;
        if ((string.IsNullOrWhiteSpace(credentials) || !File.Exists(Environment.ExpandEnvironmentVariables(credentials)))
            && string.IsNullOrWhiteSpace(o.ServerKey)) return "ConfigurationRequired";
        var tokens = await db.Set<SsalddelMobilePushInstallation>().AsNoTracking()
            .Where(x => x.UserId == notice.RecipientUserId && x.AppKey == "ssalddel.admin" && x.IsActive).Select(x => x.PushToken).Distinct().ToListAsync(ct);
        if (tokens.Count == 0) return "ConfigurationRequired";
        var sent = false;
        foreach (var token in tokens)
            sent |= await push.SendAsync(new(token, "Mirror · 운영 확인", "앱에서 최신 업무 상태를 확인하세요.",
                new Dictionary<string, string> { ["type"] = "operations-alert", ["appKey"] = "ssalddel.admin",
                    ["notificationId"] = notice.Id, ["alertId"] = notice.AlertId ?? "", ["userId"] = notice.RecipientUserId,
                    ["createdAtUtc"] = DateTime.SpecifyKind(notice.CreatedAtUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) }, HighPriority: true, DataOnly: true), ct);
        return sent ? "Sent" : "Failed";
    }
}
