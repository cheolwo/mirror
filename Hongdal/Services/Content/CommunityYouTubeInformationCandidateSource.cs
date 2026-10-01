using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Content;

namespace Hongdal.Services.Content;

public sealed class CommunityYouTubeInformationCandidateSource : ICommunityInformationCandidateSource
{
    private const string DocumentationUrl = "https://developers.google.com/youtube/v3/docs/playlistItems/list";
    private readonly IYouTube채널감시저장소 _store;

    public CommunityYouTubeInformationCandidateSource(IYouTube채널감시저장소 store)
    {
        _store = store;
    }

    public CommunityInformationSourceDto Source { get; } = new(
        CommunityInformationSourceKeys.YouTubeChannelVideos,
        CommunityInformationSourceTypes.Video,
        "YouTube",
        "검토 채널의 새 공개 영상",
        CommunityInformationCollectionModes.ScheduledArchive,
        "서버에 설정된 채널 동기화 주기",
        "음식 또는 지식·성찰 채널로 확인된 영상만 후보로 모으고, 운영자 승인 전에는 커뮤니티에 게시하지 않습니다.",
        DocumentationUrl,
        true);

    public async Task<IReadOnlyList<CommunityInformationCandidateDto>> ReadAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var fetchCount = (int)Math.Clamp(Math.Max((long)query.Take * 4, 50), 1, 200);
        var videos = await _store.영상목록조회Async(
            channelId: null,
            신규업로드만: false,
            fetchCount,
            cancellationToken);

        return videos
            .Where(video => IsProjectRelated(video.감시채널))
            .Select(ToCandidate)
            .Where(candidate => string.IsNullOrWhiteSpace(query.CountryCode)
                                || string.Equals(
                                    candidate.CountryCode,
                                    query.CountryCode.Trim(),
                                    StringComparison.OrdinalIgnoreCase))
            .Where(candidate => string.IsNullOrWhiteSpace(query.ReviewState)
                                || string.Equals(
                                    candidate.ReviewState,
                                    query.ReviewState.Trim(),
                                    StringComparison.OrdinalIgnoreCase))
            .Where(candidate => MatchesSearch(candidate, query.SearchText))
            .Where(candidate => !query.StartDate.HasValue
                                || candidate.ReferenceDate >= query.StartDate.Value)
            .Where(candidate => !query.EndDate.HasValue
                                || candidate.ReferenceDate <= query.EndDate.Value)
            .Take(Math.Clamp(query.Take, 1, 100))
            .ToArray();
    }

    private static bool IsProjectRelated(YouTube감시채널? channel)
        => channel is not null
           && (channel.음식채널여부 || channel.지식성찰채널여부);

    private static CommunityInformationCandidateDto ToCandidate(YouTube채널영상 video)
    {
        var channel = video.감시채널!;
        return new CommunityInformationCandidateDto(
            $"youtube:{video.VideoId}",
            CommunityInformationSourceKeys.YouTubeChannelVideos,
            CommunityInformationSourceTypes.Video,
            string.IsNullOrWhiteSpace(channel.채널명) ? "YouTube" : channel.채널명.Trim(),
            NormalizeText(video.제목, 200),
            NormalizeText(video.설명, 500),
            $"https://www.youtube.com/watch?v={Uri.EscapeDataString(video.VideoId)}",
            NormalizeOptional(video.썸네일Url),
            DateTime.SpecifyKind(video.게시일시Utc, DateTimeKind.Utc),
            DateOnly.FromDateTime(video.게시일시Utc),
            DateTime.SpecifyKind(video.최초감지일시Utc, DateTimeKind.Utc),
            YouTube채널수집국가코드.정규화(channel.국가코드),
            string.IsNullOrWhiteSpace(channel.기본언어코드) ? "und" : channel.기본언어코드.Trim(),
            null,
            null,
            ResolveReviewState(video.공유상태),
            BuildTopicTags(channel),
            "YouTube Data API로 받은 공개 영상 메타데이터와 원본 시청 링크입니다.",
            "제목과 설명은 영상 제작자가 작성한 정보이며 상품 사실, 원산지, 가격 또는 수입 가능성을 홍달이 확인했다는 뜻이 아닙니다.");
    }

    private static string ResolveReviewState(string? sharingState)
        => sharingState switch
        {
            YouTube채널영상.공유대기상태 => CommunityInformationReviewStates.PendingReview,
            YouTube채널영상.공개상태 => CommunityInformationReviewStates.Approved,
            YouTube채널영상.숨김상태 => CommunityInformationReviewStates.Excluded,
            _ => CommunityInformationReviewStates.Baseline
        };

    private static IReadOnlyList<string> BuildTopicTags(YouTube감시채널 channel)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (channel.음식채널여부)
        {
            tags.Add("음식");
            AddCommaSeparated(tags, channel.음식콘텐츠분류);
        }

        if (channel.지식성찰채널여부)
        {
            tags.Add("지식·성찰");
            AddCommaSeparated(tags, channel.지식성찰분류);
        }

        return tags.OrderBy(tag => tag).ToArray();
    }

    private static void AddCommaSeparated(ISet<string> tags, string? values)
    {
        foreach (var value in (values ?? string.Empty).Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            tags.Add(value);
        }
    }

    private static bool MatchesSearch(
        CommunityInformationCandidateDto candidate,
        string? searchText)
    {
        var term = searchText?.Trim();
        return string.IsNullOrWhiteSpace(term)
               || candidate.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
               || candidate.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)
               || candidate.Provider.Contains(term, StringComparison.OrdinalIgnoreCase)
               || candidate.TopicTags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        var normalized = string.Join(
            ' ',
            (value ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
