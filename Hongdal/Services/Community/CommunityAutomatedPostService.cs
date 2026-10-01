using System.Data;
using System.Globalization;
using Hongdal.Application.Community;
using Hongdal.Contracts.Common.Community;
using Hongdal.Domain.AgriculturalFisheries;
using Hongdal.Domain.Community;
using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

public static class CommunityAutomatedPostSourceKeys
{
    public const string KamisPriceBrief = "kamis-price-brief";
    public const string Reflection = "reflection";
    public const string ActivityDigest = "activity-digest";
    public const string Prajna = "prajna";
    public const string PrajnaCard = "prajna-card";
    public const string PrajnaVideo = "prajna-video";
}

public sealed record CommunityAutomatedPostDraft(
    string SourceKey,
    string PeriodKey,
    string Category,
    string WorkflowTag,
    string RoleTag,
    string Title,
    string Body,
    string Nickname,
    string? SharedLinkUrl = null)
{
    public string SystemAuthorKey => CommunityAutomatedPostPublication.BuildSystemAuthorKey(SourceKey, PeriodKey);
}

public sealed record CommunityAutomatedPostPublishResult(long PostId, bool Created);

public interface ICommunityAutomatedPostPublisher
{
    Task<CommunityAutomatedPostPublishResult> PublishIfMissingAsync(
        CommunityAutomatedPostDraft draft,
        CancellationToken cancellationToken = default);
}

public interface ICommunityAutomatedPostSource
{
    string SourceKey { get; }

    Task<CommunityAutomatedPostDraft?> BuildAsync(
        DateOnly publicationDate,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default);
}

public static class CommunityAutomatedPostPublication
{
    public const string SystemAuthorPrefix = "system:community-editorial:";

    public static string BuildSystemAuthorKey(string sourceKey, string periodKey)
        => $"{SystemAuthorPrefix}{NormalizeKeySegment(sourceKey)}:{NormalizeKeySegment(periodKey)}";

    public static bool IsAutomatedPost(PlatformCommunityPost post)
        => post.AuthorUserId?.StartsWith(SystemAuthorPrefix, StringComparison.Ordinal) == true;

    public static string? GetSystemPostKind(PlatformCommunityPost post)
    {
        if (CommunityLedgerCompletionPublication.IsSystemPost(post))
        {
            return PlatformCommunitySystemPostKinds.LedgerCompletion;
        }

        if (!IsAutomatedPost(post))
        {
            return null;
        }

        var sourceAndPeriod = post.AuthorUserId![SystemAuthorPrefix.Length..];
        var separatorIndex = sourceAndPeriod.IndexOf(':');
        var sourceKey = separatorIndex < 0
            ? sourceAndPeriod
            : sourceAndPeriod[..separatorIndex];
        return sourceKey switch
        {
            CommunityAutomatedPostSourceKeys.KamisPriceBrief => PlatformCommunitySystemPostKinds.KamisPriceBrief,
            CommunityAutomatedPostSourceKeys.Reflection => PlatformCommunitySystemPostKinds.Reflection,
            CommunityAutomatedPostSourceKeys.ActivityDigest => PlatformCommunitySystemPostKinds.ActivityDigest,
            CommunityAutomatedPostSourceKeys.PrajnaCard or CommunityAutomatedPostSourceKeys.PrajnaVideo =>
                PlatformCommunitySystemPostKinds.PrajnaContent,
            _ => PlatformCommunitySystemPostKinds.AutomatedEditorial
        };
    }

    public static string? GetPrivacyNotice(string? systemPostKind)
        => systemPostKind switch
        {
            PlatformCommunitySystemPostKinds.LedgerCompletion =>
                "원장 종류와 절차 구조만 공개되며 이름, 연락처, 상세 주소, 금액과 원문 증빙은 공개하지 않습니다.",
            PlatformCommunitySystemPostKinds.KamisPriceBrief =>
                "KAMIS 원천 관측값을 자동 정리한 정보 글입니다. 출처, 조사일, 품목·등급과 단위를 함께 확인해야 합니다.",
            PlatformCommunitySystemPostKinds.Reflection =>
                "홍달 운영 원칙을 바탕으로 자동 편집한 성찰문이며 특정 사상가의 실제 인용문이 아닙니다.",
            PlatformCommunitySystemPostKinds.ActivityDigest =>
                "완료 상태로 기록된 비식별 원장의 건수만 자동 집계했으며 참여자, 금액, 주소와 거래 세부정보를 포함하지 않습니다.",
            PlatformCommunitySystemPostKinds.PrajnaContent =>
                "관리자가 선별한 외부 공개 자료의 제목과 짧은 소개만 게시합니다. 원 출처를 확인해야 하며 홍달과 해당 기관의 제휴를 뜻하지 않습니다.",
            PlatformCommunitySystemPostKinds.AutomatedEditorial =>
                "출처와 기준 시각을 표시해 자동 작성한 홍달 시스템 정보 글입니다.",
            _ => null
        };

    private static string NormalizeKeySegment(string value)
    {
        var normalized = new string((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        if (normalized.Length == 0)
        {
            throw new ArgumentException("자동 게시 식별자에는 문자 또는 숫자가 필요합니다.", nameof(value));
        }

        return normalized.Length <= 80 ? normalized : normalized[..80];
    }
}

public sealed class EfCommunityAutomatedPostPublisher : ICommunityAutomatedPostPublisher
{
    private readonly HongdalContext _db;
    private readonly I커뮤니티게시글음성작업예약Service _audioQueue;
    private readonly ICommunityKeywordNotificationQueue _keywordQueue;
    private readonly IPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EfCommunityAutomatedPostPublisher> _logger;

    public EfCommunityAutomatedPostPublisher(
        HongdalContext db,
        I커뮤니티게시글음성작업예약Service audioQueue,
        ICommunityKeywordNotificationQueue keywordQueue,
        IPublisher publisher,
        TimeProvider timeProvider,
        ILogger<EfCommunityAutomatedPostPublisher> logger)
    {
        _db = db;
        _audioQueue = audioQueue;
        _keywordQueue = keywordQueue;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CommunityAutomatedPostPublishResult> PublishIfMissingAsync(
        CommunityAutomatedPostDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrWhiteSpace(draft.Title) || string.IsNullOrWhiteSpace(draft.Body))
        {
            throw new ArgumentException("자동 게시글에는 제목과 본문이 필요합니다.", nameof(draft));
        }

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var existingPostId = await _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => !post.IsDeleted && post.AuthorUserId == draft.SystemAuthorKey)
            .Select(post => (long?)post.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingPostId.HasValue)
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new CommunityAutomatedPostPublishResult(existingPostId.Value, false);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entity = new PlatformCommunityPost
        {
            AppKey = "platform",
            Category = CommunityBoardCatalog.ResolveCanonicalCategory(draft.Category),
            WorkflowTag = Limit(draft.WorkflowTag, 60),
            RoleTag = Limit(draft.RoleTag, 40),
            Title = Limit(draft.Title, 160),
            Body = Limit(draft.Body, 4000),
            OriginalLanguageCode = CommunityDisplayLanguageCodes.Korean,
            SharedLinkUrl = LimitOptional(draft.SharedLinkUrl, 1000),
            AuthorUserId = draft.SystemAuthorKey,
            Nickname = Limit(draft.Nickname, 40),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.PlatformCommunityPosts.Add(entity);
        _audioQueue.예약(entity, now);
        _keywordQueue.Enqueue(entity, now);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            await _publisher.Publish(new 커뮤니티게시글등록됨Event(entity.Id), cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "자동 정보 게시글 후속 작업 신호 발행에 실패했습니다. DB 대기열에서 복구합니다. PostId={PostId} SourceKey={SourceKey} PeriodKey={PeriodKey}",
                entity.Id,
                draft.SourceKey,
                draft.PeriodKey);
        }

        return new CommunityAutomatedPostPublishResult(entity.Id, true);
    }

    private static string Limit(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? LimitOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public sealed record KamisPriceBriefItem(
    string ProductClassName,
    string CategoryName,
    string ItemName,
    string KindName,
    string RankName,
    string Unit,
    decimal PriceKrw,
    decimal? PreviousDayPriceKrw);

public sealed class CommunityKamisPriceBriefSource : ICommunityAutomatedPostSource
{
    private readonly AgriculturalFisheriesDbContext _db;
    private readonly CommunityEditorialBatchOptions _options;

    public CommunityKamisPriceBriefSource(
        AgriculturalFisheriesDbContext db,
        IOptions<CommunityEditorialBatchOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public string SourceKey => CommunityAutomatedPostSourceKeys.KamisPriceBrief;

    public async Task<CommunityAutomatedPostDraft?> BuildAsync(
        DateOnly publicationDate,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default)
    {
        var latestSurveyDate = await _db.KamisPriceObservations
            .AsNoTracking()
            .Where(observation => observation.FrequencyCode == "Daily"
                                  && observation.SurveyDate <= publicationDate
                                  && !observation.IsPriceMissing
                                  && observation.PriceKrw.HasValue)
            .MaxAsync(observation => (DateOnly?)observation.SurveyDate, cancellationToken);
        if (!latestSurveyDate.HasValue)
        {
            return null;
        }

        var observations = await _db.KamisPriceObservations
            .AsNoTracking()
            .Where(observation => observation.FrequencyCode == "Daily"
                                  && observation.SurveyDate == latestSurveyDate.Value
                                  && !observation.IsPriceMissing
                                  && observation.PriceKrw.HasValue)
            .OrderBy(observation => observation.CategoryName)
            .ThenBy(observation => observation.ItemName)
            .ThenBy(observation => observation.KindName)
            .ThenBy(observation => observation.RankName)
            .Take(500)
            .ToListAsync(cancellationToken);
        var maxItems = Math.Clamp(_options.KamisPriceBriefMaxItems, 1, 12);
        var selected = observations
            .GroupBy(observation => new
            {
                observation.ProductClassName,
                observation.CategoryName,
                observation.ItemName,
                observation.Unit
            })
            .Select(group => group.First())
            .Take(maxItems)
            .Select(observation => new KamisPriceBriefItem(
                observation.ProductClassName,
                observation.CategoryName,
                observation.ItemName,
                observation.KindName,
                observation.RankName,
                observation.Unit,
                observation.PriceKrw!.Value,
                observation.PreviousDayPriceKrw))
            .ToArray();
        if (selected.Length == 0)
        {
            return null;
        }

        var sourceUrl = observations
            .Select(observation => observation.SourceUrl)
            .FirstOrDefault(IsPublicHttpUrl);
        return new CommunityAutomatedPostDraft(
            SourceKey,
            latestSurveyDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            CommunityBoardCatalog.InformationPrices.DisplayName,
            "농수산물 가격 정보",
            "자동 정보",
            $"[자동 가격정보] {latestSurveyDate:yyyy-MM-dd} KAMIS 농수산물 관측값",
            BuildBody(latestSurveyDate.Value, selected),
            "홍달 정보봇",
            sourceUrl);
    }

    private static bool IsPublicHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static string BuildBody(DateOnly surveyDate, IReadOnlyList<KamisPriceBriefItem> items)
    {
        var lines = new List<string>
        {
            "[자동 작성 안내] KAMIS에 보관된 공식 관측값 일부를 게시판 형식으로 정리했습니다.",
            $"조사 기준일: {surveyDate:yyyy-MM-dd}",
            "통화: KRW(원)",
            string.Empty
        };
        foreach (var item in items)
        {
            var comparison = BuildPreviousDayComparison(item.PriceKrw, item.PreviousDayPriceKrw);
            var specification = string.Join(" · ", new[] { item.KindName, item.RankName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            lines.Add(
                $"- {item.ItemName}{(specification.Length == 0 ? string.Empty : $" ({specification})")}: " +
                $"{item.PriceKrw:N0}원/{item.Unit} · {item.ProductClassName} · {comparison}");
        }

        lines.AddRange(
        [
            string.Empty,
            "출처: KAMIS 농산물 유통정보",
            "주의: 위 값은 전체 시장 평균이나 판매 권고가 아니라 품종·등급·단위가 붙은 관측 항목입니다. 서로 다른 규격과 조사처의 가격을 같은 가격처럼 직접 비교하지 마세요."
        ]);
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildPreviousDayComparison(decimal price, decimal? previousDayPrice)
    {
        if (!previousDayPrice.HasValue || previousDayPrice.Value <= 0)
        {
            return "전일 비교값 없음";
        }

        var difference = price - previousDayPrice.Value;
        if (difference == 0)
        {
            return "전일 대비 변동 없음";
        }

        var percent = difference / previousDayPrice.Value * 100m;
        return $"전일 대비 {(difference > 0 ? "+" : string.Empty)}{difference:N0}원 ({percent:+0.0;-0.0;0.0}%)";
    }
}

public sealed class CommunityReflectionSource : ICommunityAutomatedPostSource
{
    private static readonly ReflectionEntry[] Entries =
    [
        new(
            "기록이 판단을 돕는 방식",
            "급히 결론을 내리기보다 조건과 이견을 기록하면 다음 사람이 같은 실수를 줄일 수 있습니다.",
            "오늘 진행 중인 일에서 아직 합의되지 않은 조건 하나를 분리해 적어 보세요."),
        new(
            "정보와 선택 사이의 거리",
            "좋은 정보는 답을 대신 정하기보다 각자가 선택할 수 있는 기준을 더 선명하게 만듭니다.",
            "가격·일정·출처 중 빠진 기준이 없는지 한 번 확인해 보세요."),
        new(
            "작은 약속을 보이는 일",
            "공동체의 신뢰는 큰 구호보다 누가 무엇을 언제까지 맡았는지 보이는 작은 약속에서 자랍니다.",
            "다음 인계의 담당자와 확인 시각을 한 줄로 남겨 보세요."),
        new(
            "다른 의견을 남겨 두는 이유",
            "이견을 지우지 않고 결정과 함께 남겨 두면 결과가 달라졌을 때 다시 판단할 근거가 생깁니다.",
            "결정한 내용 옆에 고려했던 다른 선택지 하나도 기록해 보세요.")
    ];

    public string SourceKey => CommunityAutomatedPostSourceKeys.Reflection;

    public Task<CommunityAutomatedPostDraft?> BuildAsync(
        DateOnly publicationDate,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default)
    {
        var entry = Entries[Math.Abs(publicationDate.DayNumber) % Entries.Length];
        var body = string.Join(
            Environment.NewLine + Environment.NewLine,
            "[자동 작성 안내] 홍달의 공개·합의·기록 원칙을 바탕으로 정리한 짧은 성찰문입니다.",
            entry.Message,
            $"오늘의 작은 실천: {entry.Practice}",
            "특정 사상가의 실제 인용문이 아니며, 홍달 시스템이 작성한 운영 성찰문입니다.");
        CommunityAutomatedPostDraft draft = new(
            SourceKey,
            publicationDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            CommunityBoardCatalog.FreeLife.DisplayName,
            "공동체 성찰",
            "자동 정보",
            $"[자동 성찰] {entry.Title}",
            body,
            "홍달 성찰봇");
        return Task.FromResult<CommunityAutomatedPostDraft?>(draft);
    }

    private sealed record ReflectionEntry(string Title, string Message, string Practice);
}

public sealed class CommunityActivityDigestSource : ICommunityAutomatedPostSource
{
    private readonly HongdalContext _db;

    public CommunityActivityDigestSource(HongdalContext db)
    {
        _db = db;
    }

    public string SourceKey => CommunityAutomatedPostSourceKeys.ActivityDigest;

    public async Task<CommunityAutomatedPostDraft?> BuildAsync(
        DateOnly publicationDate,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default)
    {
        var summaryDate = publicationDate.AddDays(-1);
        var rangeStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            summaryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            timeZone);
        var rangeEndUtc = TimeZoneInfo.ConvertTimeToUtc(
            summaryDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            timeZone);
        var workflowTags = await _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => !post.IsDeleted
                           && post.AuthorUserId == CommunityLedgerCompletionPublication.SystemAuthorKey
                           && post.CreatedAtUtc >= rangeStartUtc
                           && post.CreatedAtUtc < rangeEndUtc)
            .Select(post => post.WorkflowTag)
            .ToListAsync(cancellationToken);
        var workflowCounts = workflowTags
            .GroupBy(workflowTag => workflowTag, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ActivityWorkflowCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.WorkflowTag)
            .ToList();
        if (workflowCounts.Count == 0)
        {
            return null;
        }

        var totalCount = workflowCounts.Sum(item => item.Count);
        var bodyLines = new List<string>
        {
            "[자동 작성 안내] 전날 완료 상태로 저장된 비식별 원장 기록을 업무 종류별 건수로만 정리했습니다.",
            $"집계 기준: {summaryDate:yyyy-MM-dd} 00:00~24:00 ({timeZone.Id})",
            $"공개 완료 기록: {totalCount:N0}건",
            string.Empty
        };
        bodyLines.AddRange(workflowCounts.Select(item => $"- {DisplayWorkflow(item.WorkflowTag)}: {item.Count:N0}건"));
        bodyLines.AddRange(
        [
            string.Empty,
            "이 수치는 거래액이나 매출, 플랫폼이 중개한 거래 수를 뜻하지 않습니다.",
            "이름, 연락처, 상세 주소, 금액, 상품·화물 세부값과 증빙 원문은 집계하지 않았습니다."
        ]);

        return new CommunityAutomatedPostDraft(
            SourceKey,
            summaryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            CommunityBoardCatalog.CompletionReview.DisplayName,
            "비식별 완료 기록",
            "자동 시스템 기록",
            $"[자동 활동요약] {summaryDate:yyyy-MM-dd} 공개 완료 기록 {totalCount:N0}건",
            string.Join(Environment.NewLine, bodyLines),
            "홍달 활동 기록");
    }

    private static string DisplayWorkflow(string? workflowTag)
        => string.IsNullOrWhiteSpace(workflowTag) ? "기타 업무" : workflowTag.Trim();

    private sealed record ActivityWorkflowCount(string WorkflowTag, int Count);
}
