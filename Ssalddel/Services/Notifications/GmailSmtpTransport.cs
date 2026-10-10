using System.Net;
using System.Net.Mail;
using System.Text;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Notifications;

/// <summary>업무 수신자·문안 결정과 분리한 Gmail SMTP 전송 기술. 비밀번호·오류 원문을 반환하지 않는다.</summary>
public static class GmailSmtpTransport
{
    public static async Task SendAsync(CommunityPostEmailGmailOptions gmail, string recipient, string subject, string body,
        string headerName, string notificationId, CancellationToken ct)
    {
        var user = gmail.UserName.Trim();
        using var message = new MailMessage
        {
            From = new MailAddress(string.IsNullOrWhiteSpace(gmail.FromAddress) ? user : gmail.FromAddress.Trim(), gmail.FromDisplayName, Encoding.UTF8),
            Subject = subject, SubjectEncoding = Encoding.UTF8, Body = body, BodyEncoding = Encoding.UTF8, IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipient)); message.Headers.Add(headerName, notificationId);
        using var smtp = new SmtpClient("smtp.gmail.com", 587)
        { EnableSsl = true, UseDefaultCredentials = false, Credentials = new NetworkCredential(user, string.Concat(gmail.AppPassword.Where(c => !char.IsWhiteSpace(c)))) };
        await smtp.SendMailAsync(message, ct);
    }
}
