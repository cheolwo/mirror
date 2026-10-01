using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Domain.Content;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Community;

/// <summary>
/// 관리자가 공개 승인한 카드와 지식·성찰 채널 영상을 반야 게시판 초안으로 한 건씩 구성합니다.
/// 원본 수집 상태와 커뮤니티 공개 승인은 서로 다른 경계로 유지합니다.
/// </summary>
public sealed class CommunityPrajnaPostSource : ICommunityAutomatedPostSource
{
    private const int CandidatePageSize = 100;
    private readonly HongdalContext _db;

    public CommunityPrajnaPostSource(HongdalContext db)
    {
        _db = db;
    }

    public string SourceKey => CommunityAutomatedPostSourceKeys.Prajna;

    public async Task<CommunityAutomatedPostDraft?> BuildAsync(
        DateOnly publicationDate,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default)
    {
        var cardPrefix = BuildSourcePrefix(CommunityAutomatedPostSourceKeys.PrajnaCard);
        var videoPrefix = BuildSourcePrefix(CommunityAutomatedPostSourceKeys.PrajnaVideo);
        var publishedKeys = await _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => post.AuthorUserId != null
                           && (post.AuthorUserId.StartsWith(cardPrefix)
                               || post.AuthorUserId.StartsWith(videoPrefix)))
            .Select(post => post.AuthorUserId!)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);
        var lastPublishedKey = await _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => post.AuthorUserId != null
                           && (post.AuthorUserId.StartsWith(cardPrefix)
                               || post.AuthorUserId.StartsWith(videoPrefix)))
            .OrderByDescending(post => post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Select(post => post.AuthorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        var card = await FindNextCardAsync(publishedKeys, cancellationToken);
        var video = await FindNextVideoAsync(publishedKeys, cancellationToken);
        var preferVideo = lastPublishedKey?.StartsWith(cardPrefix, StringComparison.Ordinal) == true;

        if (preferVideo)
        {
            return video is not null
                ? BuildVideoDraft(video, publicationDate, timeZone)
                : card is not null
                    ? BuildCardDraft(card, publicationDate)
                    : null;
        }

        return card is not null
            ? BuildCardDraft(card, publicationDate)
            : video is not null
                ? BuildVideoDraft(video, publicationDate, timeZone)
                : null;
    }

    private async Task<HongikHakdangCard?> FindNextCardAsync(
        IReadOnlySet<string> publishedKeys,
        CancellationToken cancellationToken)
    {
        var query = _db.HongikHakdangCards
            .AsNoTracking()
            .Where(card => card.IsActive
                           && card.IsAdminEnabled
                           && card.IsCommunityPublicationApproved
                           && card.Collections.Any(item => item.IsActive
                                                          && item.Collection.IsActive
                                                          && item.Collection.IsAdminEnabled))
            .OrderBy(card => card.Id);
        for (var skip = 0; ; skip += CandidatePageSize)
        {
            var candidates = await query
                .Skip(skip)
                .Take(CandidatePageSize)
                .ToListAsync(cancellationToken);
            var candidate = candidates.FirstOrDefault(card =>
                ResolvePublicSourceUrl(card) is not null
                && !publishedKeys.Contains(CommunityAutomatedPostPublication.BuildSystemAuthorKey(
                    CommunityAutomatedPostSourceKeys.PrajnaCard,
                    card.Id.ToString(CultureInfo.InvariantCulture))));
            if (candidate is not null || candidates.Count < CandidatePageSize)
            {
                return candidate;
            }
        }
    }

    private async Task<YouTube채널영상?> FindNextVideoAsync(
        IReadOnlySet<string> publishedKeys,
        CancellationToken cancellationToken)
    {
        var query = _db.YouTube채널영상
            .AsNoTracking()
            .Include(video => video.감시채널)
            .Where(video => video.공유상태 == YouTube채널영상.공개상태
                            && video.감시채널 != null
                            && video.감시채널.활성화여부
                            && video.감시채널.지식성찰채널여부
                            && video.감시채널.반야게시허용여부)
            .OrderBy(video => video.게시일시Utc)
            .ThenBy(video => video.Id);
        for (var skip = 0; ; skip += CandidatePageSize)
        {
            var candidates = await query
                .Skip(skip)
                .Take(CandidatePageSize)
                .ToListAsync(cancellationToken);
            var candidate = candidates.FirstOrDefault(video =>
                !publishedKeys.Contains(CommunityAutomatedPostPublication.BuildSystemAuthorKey(
                    CommunityAutomatedPostSourceKeys.PrajnaVideo,
                    video.VideoId)));
            if (candidate is not null || candidates.Count < CandidatePageSize)
            {
                return candidate;
            }
        }
    }

    private static CommunityAutomatedPostDraft BuildCardDraft(
        HongikHakdangCard card,
        DateOnly publicationDate)
    {
        var title = TextOrFallback(card.Title, $"홍익학당 카드 {card.Id}");
        var description = LimitText(card.Description, 420);
        var body = new List<string>
        {
            "관리자가 반야 게시 대상으로 선별한 공개 카드입니다.",
            string.Empty
        };
        if (!string.IsNullOrWhiteSpace(description))
        {
            body.Add(description);
            body.Add(string.Empty);
        }

        body.Add($"선별 기준일: {publicationDate:yyyy-MM-dd}");
        body.Add("출처: 홍익학당 공개 웹 자료");
        body.Add("홍익학당은 현재 홍달의 협력기관이 아닙니다. 이 글은 제휴나 공식 추천을 뜻하지 않으며 원문은 출처 링크에서 확인해 주세요.");

        return new CommunityAutomatedPostDraft(
            CommunityAutomatedPostSourceKeys.PrajnaCard,
            card.Id.ToString(CultureInfo.InvariantCulture),
            CommunityBoardCatalog.Prajna.DisplayName,
            "배움·성찰",
            "관리자 선별 콘텐츠",
            LimitText($"[반야 카드] {title}", 180),
            string.Join(Environment.NewLine, body),
            "홍달 반야지기",
            ResolvePublicSourceUrl(card));
    }

    private static CommunityAutomatedPostDraft BuildVideoDraft(
        YouTube채널영상 video,
        DateOnly publicationDate,
        TimeZoneInfo timeZone)
    {
        var title = TextOrFallback(video.제목, video.VideoId);
        var description = LimitText(video.설명, 420);
        var channelName = TextOrFallback(video.감시채널?.채널명, "원 채널");
        var perspective = TextOrFallback(video.감시채널?.관점표시, "지식·성찰");
        var localPublishedAt = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(video.게시일시Utc, DateTimeKind.Utc),
            timeZone);
        var body = new List<string>
        {
            "관리자가 반야 게시 대상으로 선별한 공개 영상입니다.",
            string.Empty
        };
        if (!string.IsNullOrWhiteSpace(description))
        {
            body.Add(description);
            body.Add(string.Empty);
        }

        body.Add($"영상 게시일: {localPublishedAt:yyyy-MM-dd} ({timeZone.Id})");
        body.Add($"선별 기준일: {publicationDate:yyyy-MM-dd}");
        body.Add($"출처: {channelName} YouTube 공개 영상");
        body.Add($"관점: {perspective}");
        body.Add($"{channelName}은(는) 현재 홍달의 협력기관으로 표시되지 않습니다. 이 글은 제휴·교리의 우열·공식 추천을 뜻하지 않으며 영상 내용과 권리는 원 출처에 있습니다.");

        return new CommunityAutomatedPostDraft(
            CommunityAutomatedPostSourceKeys.PrajnaVideo,
            video.VideoId,
            CommunityBoardCatalog.Prajna.DisplayName,
            "배움·성찰",
            LimitText($"관리자 선별 · {perspective}", 40),
            LimitText($"[반야 영상] {title}", 180),
            string.Join(Environment.NewLine, body),
            "홍달 반야지기",
            $"https://www.youtube.com/watch?v={Uri.EscapeDataString(video.VideoId)}");
    }

    private static string BuildSourcePrefix(string sourceKey)
        => $"{CommunityAutomatedPostPublication.SystemAuthorPrefix}{sourceKey}:";

    private static string? ResolvePublicSourceUrl(HongikHakdangCard card)
        => IsPublicHttpUrl(card.RelatedUrl)
            ? card.RelatedUrl!.Trim()
            : IsPublicHttpUrl(card.OriginalImageUrl)
                ? card.OriginalImageUrl.Trim()
                : null;

    private static bool IsPublicHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string TextOrFallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string LimitText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maxLength ? normalized : $"{normalized[..(maxLength - 1)]}…";
    }
}
