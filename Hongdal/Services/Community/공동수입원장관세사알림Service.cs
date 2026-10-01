using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hongdal.Contracts.Common.Community;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Services.Notifications;
using 홍달.도메인.사용자;
using 홍달.도메인.설정;

namespace Hongdal.Services.Community;

public interface I공동수입원장관세사알림Service
{
    Task<int> 등록알림적재Async(
        커뮤니티원장Dto 원장,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class 공동수입원장관세사알림Service : I공동수입원장관세사알림Service
{
    private const string TargetCustomsBroker = "CustomsBroker";

    private readonly HongdalContext _db;
    private readonly ILogger<공동수입원장관세사알림Service> _logger;

    public 공동수입원장관세사알림Service(
        HongdalContext db,
        ILogger<공동수입원장관세사알림Service> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> 등록알림적재Async(
        커뮤니티원장Dto 원장,
        string eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(원장);

        var traceId = 공동수입원장관세사알림Policy.BuildTraceId(
            eventId,
            $"{원장.원장Id}:{원장.Revision}");
        var alreadyQueued = await _db.Command알림Outbox
            .AsNoTracking()
            .AnyAsync(
                x => x.TraceId == traceId
                     && x.FeatureName == Command알림FeatureNames.공동수입원장등록
                     && x.Target == TargetCustomsBroker,
                cancellationToken);
        if (alreadyQueued)
        {
            return 0;
        }

        var 대상관세사참여자Ids = await (
                from profile in _db.관세사프로필.AsNoTracking()
                join participant in _db.홍달참여자.AsNoTracking()
                    on profile.참여자Id equals participant.Id
                join role in _db.홍달참여자역할.AsNoTracking()
                    on participant.Id equals role.참여자Id
                where profile.관리자승인여부
                      && profile.수임가능여부
                      && profile.수입전문여부
                      && participant.활성화여부
                      && role.활성화여부
                      && role.역할유형 == 홍달역할유형.관세사
                select profile.참여자Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (대상관세사참여자Ids.Count == 0)
        {
            _logger.LogInformation(
                "공동수입 원장 등록 알림 대상 관세사가 없습니다. LedgerId={LedgerId}",
                원장.원장Id);
            return 0;
        }

        var hsCodes = 공동수입원장관세사알림Policy.ExtractHsCodes(원장);
        var now = occurredAtUtc == default ? DateTime.UtcNow : occurredAtUtc.ToUniversalTime();
        foreach (var participantId in 대상관세사참여자Ids)
        {
            _db.Command알림Outbox.Add(new Command알림Outbox
            {
                CommandName = "공동수입원장등록",
                EventName = "커뮤니티원장변경됨Event",
                FeatureName = Command알림FeatureNames.공동수입원장등록,
                Target = TargetCustomsBroker,
                PayloadJson = 공동수입원장관세사알림Policy.BuildPayload(원장, participantId, hsCodes),
                Status = "Pending",
                TraceId = traceId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "공동수입 원장 등록 관세사 알림 적재 완료. LedgerId={LedgerId}, BrokerCount={BrokerCount}, HsCodeCount={HsCodeCount}",
            원장.원장Id,
            대상관세사참여자Ids.Count,
            hsCodes.Count);
        return 대상관세사참여자Ids.Count;
    }
}

public static class 공동수입원장관세사알림Policy
{
    private static readonly char[] ValueSeparators = [',', ';', '|', '·', '\n', '\r'];

    public static bool ShouldQueue(string 변경유형, 커뮤니티원장Dto 원장)
        => string.Equals(변경유형, "저장", StringComparison.Ordinal)
           && 원장.Revision == 1
           && string.Equals(
               원장.원장템플릿Key,
               CommunityLedgerTemplateKeys.GroupImport,
               StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ExtractHsCodes(커뮤니티원장Dto 원장)
    {
        ArgumentNullException.ThrowIfNull(원장);

        var values = new List<string>();
        CollectHsCodeValues(원장.확장속성, values);
        CollectHsCodeValues(원장.외부참조, values);
        foreach (var block in 원장.블록목록)
        {
            CollectHsCodeValues(block.Data, values);
        }

        return values
            .SelectMany(SplitValues)
            .Select(NormalizeHsCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    public static string BuildTraceId(string? eventId, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(eventId) ? fallback.Trim() : eventId.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    public static string BuildPayload(
        커뮤니티원장Dto 원장,
        string targetParticipantId,
        IReadOnlyList<string> hsCodes)
    {
        var summary = hsCodes.Count switch
        {
            0 => "HS 코드 확인이 필요한",
            1 => $"HS 코드 {hsCodes[0]}",
            _ => $"HS 코드 {hsCodes[0]} 외 {hsCodes.Count - 1}건"
        };
        var ledgerId = 원장.원장Id.Trim();

        return JsonSerializer.Serialize(new
        {
            notificationType = Command알림FeatureNames.공동수입원장등록,
            targetUserId = targetParticipantId,
            ledgerId,
            hsCodes = string.Join(",", hsCodes),
            title = "새 공동수입 원장이 등록되었습니다",
            body = $"{summary}의 공동수입 검토 요청이 등록되었습니다.",
            deepLink = $"/customs/hs-codes?communityLedgerId={Uri.EscapeDataString(ledgerId)}",
            channels = new[] { "Push" }
        });
    }

    private static void CollectHsCodeValues(
        IReadOnlyDictionary<string, string>? source,
        ICollection<string> destination)
    {
        if (source is null)
        {
            return;
        }

        foreach (var (key, value) in source)
        {
            if (IsHsCodeKey(key) && !string.IsNullOrWhiteSpace(value))
            {
                destination.Add(value);
            }
        }
    }

    private static bool IsHsCodeKey(string key)
    {
        var normalized = new string(key
            .Where(character => char.IsLetterOrDigit(character) || character >= 0xAC00)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return normalized.Contains("hscode", StringComparison.Ordinal)
               || normalized.Contains("hs코드", StringComparison.Ordinal);
    }

    private static IEnumerable<string> SplitValues(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var items = JsonSerializer.Deserialize<string[]>(trimmed);
                if (items is not null)
                {
                    return items;
                }
            }
            catch (JsonException)
            {
                // 기존 원장의 단순 구분자 문자열도 아래에서 처리합니다.
            }
        }

        return trimmed.Split(ValueSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeHsCode(string value)
    {
        var trimmed = value.Trim().Trim('"', '\'', '[', ']');
        if (trimmed.Length is 0 or > 40)
        {
            return string.Empty;
        }

        return trimmed.All(character => char.IsLetterOrDigit(character)
                                        || character is '.' or '-' or ' ')
            ? trimmed
            : string.Empty;
    }
}
