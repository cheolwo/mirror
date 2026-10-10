using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Notifications;

public enum CommunityPostEmailDeliveryStatus
{
    Sent,
    ConfigurationRequired,
    Failed
}

public sealed record CommunityPostEmailMessage(
    long PostId,
    string RecipientEmail,
    string Subject,
    string Body);

public sealed record CommunityPostEmailDeliveryResult(
    CommunityPostEmailDeliveryStatus Status,
    string? Error = null);

public interface ICommunityPostEmailSender
{
    Task<CommunityPostEmailDeliveryResult> SendAsync(
        CommunityPostEmailMessage message,
        CancellationToken cancellationToken);
}

public sealed class GmailCommunityPostEmailSender : ICommunityPostEmailSender
{
    private const string GmailSmtpHost = "smtp.gmail.com";
    private const int GmailSmtpPort = 587;

    private readonly IOptionsMonitor<CommunityPostEmailNotificationOptions> _options;

    public GmailCommunityPostEmailSender(
        IOptionsMonitor<CommunityPostEmailNotificationOptions> options)
    {
        _options = options;
    }

    public async Task<CommunityPostEmailDeliveryResult> SendAsync(
        CommunityPostEmailMessage message,
        CancellationToken cancellationToken)
    {
        var gmail = _options.CurrentValue.Gmail;
        var userName = gmail.UserName.Trim();
        var appPassword = RemoveWhitespace(gmail.AppPassword);
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(appPassword))
        {
            return new(
                CommunityPostEmailDeliveryStatus.ConfigurationRequired,
                "Gmail 주소와 앱 비밀번호 설정이 필요합니다.");
        }

        var fromValue = string.IsNullOrWhiteSpace(gmail.FromAddress)
            ? userName
            : gmail.FromAddress.Trim();
        if (!TryCreateMailAddress(fromValue, out var from))
        {
            return new(
                CommunityPostEmailDeliveryStatus.ConfigurationRequired,
                "Gmail 발신 주소 형식이 올바르지 않습니다.");
        }

        if (!TryCreateMailAddress(message.RecipientEmail, out var recipient))
        {
            return new(
                CommunityPostEmailDeliveryStatus.ConfigurationRequired,
                "게시글 알림 수신 주소 형식이 올바르지 않습니다.");
        }

        try
        {
            await GmailSmtpTransport.SendAsync(gmail, message.RecipientEmail, message.Subject, message.Body,
                "X-Ssalddel-Community-Post-Id", message.PostId.ToString(CultureInfo.InvariantCulture), cancellationToken);
            return new(CommunityPostEmailDeliveryStatus.Sent);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new(CommunityPostEmailDeliveryStatus.Failed, exception.Message);
        }
    }

    private static bool TryCreateMailAddress(string value, out MailAddress? address)
    {
        try
        {
            address = new MailAddress(value.Trim());
            return true;
        }
        catch (FormatException)
        {
            address = null;
            return false;
        }
    }

    private static string RemoveWhitespace(string value)
        => string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
}
