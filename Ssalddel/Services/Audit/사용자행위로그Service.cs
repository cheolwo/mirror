using Ssalddel.Contracts.Common.ViewSettings;
using Microsoft.AspNetCore.Identity;
using 살뜰.Data;
using 살뜰.도메인.설정;

namespace 살뜰.Services.Audit;

public sealed class 사용자행위로그Service : I사용자행위로그Service
{
    private readonly SsalddelContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public 사용자행위로그Service(SsalddelContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default)
    {
        var snapshot = await ResolveUserSnapshotAsync(entry.UserId);

        var entity = new 사용자행위로그
        {
            AppKey = string.IsNullOrWhiteSpace(entry.AppKey) ? App식별자.SsalddelAdmin : entry.AppKey,
            UserId = entry.UserId ?? string.Empty,
            UserName = FirstNonEmpty(entry.UserName, snapshot.UserName),
            RoleName = entry.RoleName ?? string.Empty,
            EmailMasked = MaskEmail(FirstNonEmpty(entry.Email, snapshot.Email)),
            PhoneLast4 = GetPhoneLast4(FirstNonEmpty(entry.PhoneNumber, snapshot.PhoneNumber)),
            ActionType = entry.ActionType ?? string.Empty,
            ActionName = entry.ActionName ?? string.Empty,
            Route = entry.Route ?? string.Empty,
            TraceId = entry.TraceId ?? string.Empty,
            IsSuccess = entry.IsSuccess,
            ErrorCode = entry.ErrorCode ?? string.Empty,
            ErrorMessage = 사용자행위로그보호Policy.SafeError(entry.ErrorMessage),
            ClientIp = Trim(entry.ClientIp, 100),
            UserAgent = Trim(entry.UserAgent, 1000),
            MetadataJson = 사용자행위로그보호Policy.SafeMetadata(entry.MetadataJson),
            OccurredAtUtc = entry.OccurredAtUtc == default ? DateTime.UtcNow : entry.OccurredAtUtc
        };

        _db.사용자행위로그.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(string UserName, string Email, string PhoneNumber)> ResolveUserSnapshotAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        return (user.UserName ?? string.Empty, user.Email ?? string.Empty, user.PhoneNumber ?? string.Empty);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return "***";
        }

        var local = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        if (string.IsNullOrWhiteSpace(domain))
        {
            return local[0] + "***";
        }

        return $"{local[0]}***@{domain}";
    }

    private static string GetPhoneLast4(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
        {
            return digits;
        }

        return digits[^4..];
    }

    private static string Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
