using Hongdal.Contracts.Common.Content;

namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed record PlatformCommunityPostDraft(
    string Category,
    string WorkflowTag,
    string Title,
    string Body,
    string SharedLinkUrl,
    string SourceLabel,
    string SourceKind = PlatformCommunityPostDraftSourceKinds.Generic);

public static class PlatformCommunityPostDraftSourceKinds
{
    public const string Generic = "generic";
    public const string PrajnaLecture = "prajna-lecture";
    public const string YouTubeFood = "youtube-food";
}

public sealed class PlatformCommunityPostDraftStateService
{
    private PlatformCommunityPostDraft? _pendingDraft;

    public void Prepare(PlatformCommunityPostDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _pendingDraft = draft;
    }

    public PlatformCommunityPostDraft? Consume()
    {
        var draft = _pendingDraft;
        _pendingDraft = null;
        return draft;
    }
}

public static class PrajnaLectureCommunityShareDraftFactory
{
    public static PlatformCommunityPostDraft Create(
        string channelName,
        string playlistTitle,
        string videoTitle,
        string videoUrl,
        string quote,
        string? reflection,
        string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(quote))
        {
            throw new ArgumentException("공유할 글귀를 입력해야 합니다.", nameof(quote));
        }

        if (!Uri.TryCreate(videoUrl, UriKind.Absolute, out var sourceUri) ||
            (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("영상 주소가 올바르지 않습니다.", nameof(videoUrl));
        }

        var normalizedTimestamp = NormalizeTimestamp(timestamp, out var seconds);
        var sharedLinkUrl = seconds is null ? sourceUri.ToString() : AppendTimestamp(sourceUri, seconds.Value);
        var bodyLines = new List<string>
        {
            $"“{quote.Trim()}”"
        };

        if (!string.IsNullOrWhiteSpace(reflection))
        {
            bodyLines.Add(string.Empty);
            bodyLines.Add(reflection.Trim());
        }

        bodyLines.Add(string.Empty);
        bodyLines.Add("---");
        bodyLines.Add($"함께 본 강의: {videoTitle.Trim()}");
        bodyLines.Add($"재생목록: {playlistTitle.Trim()}");
        bodyLines.Add($"출처: {channelName.Trim()}");
        if (normalizedTimestamp is not null)
        {
            bodyLines.Add($"영상 위치: {normalizedTimestamp}");
        }

        var titlePrefix = "[반야 나눔] ";
        var availableTitleLength = 160 - titlePrefix.Length;
        var normalizedVideoTitle = videoTitle.Trim();
        var title = titlePrefix + (normalizedVideoTitle.Length <= availableTitleLength
            ? normalizedVideoTitle
            : normalizedVideoTitle[..availableTitleLength]);

        return new PlatformCommunityPostDraft(
            "자유",
            "커뮤니티 신뢰",
            title,
            string.Join(Environment.NewLine, bodyLines),
            sharedLinkUrl,
            $"{channelName.Trim()} · {playlistTitle.Trim()}",
            PlatformCommunityPostDraftSourceKinds.PrajnaLecture);
    }

    private static string? NormalizeTimestamp(string? value, out int? totalSeconds)
    {
        totalSeconds = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Trim().Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 3 || parts.Any(part => !int.TryParse(part, out _)))
        {
            throw new ArgumentException("영상 위치는 12:34 또는 1:02:03 형식으로 입력해 주세요.", nameof(value));
        }

        var values = parts.Select(int.Parse).ToArray();
        if (values.Any(part => part < 0) ||
            (values.Length >= 2 && values[^1] >= 60) ||
            (values.Length == 3 && values[^2] >= 60))
        {
            throw new ArgumentException("영상 위치의 분과 초는 0부터 59 사이여야 합니다.", nameof(value));
        }

        totalSeconds = values.Length switch
        {
            1 => values[0],
            2 => checked((values[0] * 60) + values[1]),
            _ => checked((values[0] * 3600) + (values[1] * 60) + values[2])
        };

        return values.Length switch
        {
            1 => $"{totalSeconds.Value / 60}:{totalSeconds.Value % 60:00}",
            2 => $"{values[0]}:{values[1]:00}",
            _ => $"{values[0]}:{values[1]:00}:{values[2]:00}"
        };
    }

    private static string AppendTimestamp(Uri sourceUri, int totalSeconds)
    {
        var builder = new UriBuilder(sourceUri);
        var query = builder.Query.TrimStart('?');
        builder.Query = string.IsNullOrWhiteSpace(query)
            ? $"t={totalSeconds}s"
            : $"{query}&t={totalSeconds}s";
        return builder.Uri.ToString();
    }
}

public static class YouTubeFoodCommunityShareDraftFactory
{
    private const string TitlePrefix = "[음식 발견] ";

    public static PlatformCommunityPostDraft Create(YouTube음식커뮤니티공유후보Dto candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!Uri.TryCreate(candidate.YouTube시청Url, UriKind.Absolute, out var sourceUri)
            || (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("YouTube 영상 주소가 올바르지 않습니다.", nameof(candidate));
        }

        var host = sourceUri.Host.TrimStart('.');
        if (!host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
            && !host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("YouTube 도메인의 영상만 공유할 수 있습니다.", nameof(candidate));
        }

        var productName = Required(candidate.상품명, "상품명");
        var videoTitle = Required(candidate.영상제목, "영상 제목");
        var channelName = Required(candidate.채널명, "채널명");
        var titleText = $"{productName} · {videoTitle}";
        var title = TitlePrefix + (titleText.Length <= 160 - TitlePrefix.Length
            ? titleText
            : titleText[..(160 - TitlePrefix.Length)]);

        var bodyLines = new List<string>
        {
            $"영상에서 발견한 음식: {productName}",
            $"영상: {videoTitle}",
            $"채널: {channelName} · {NormalizeCountryCode(candidate.채널국가코드)}"
        };

        if (!string.IsNullOrWhiteSpace(candidate.브랜드명))
        {
            bodyLines.Add($"브랜드: {candidate.브랜드명.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(candidate.원산지국가코드))
        {
            bodyLines.Add($"원산지 후보: {candidate.원산지국가코드.Trim().ToUpperInvariant()}");
        }

        if (!string.IsNullOrWhiteSpace(candidate.발견근거))
        {
            bodyLines.Add(string.Empty);
            bodyLines.Add(TrimTo(candidate.발견근거, 1600));
        }

        bodyLines.Add(string.Empty);
        bodyLines.Add("어떻게 먹거나 구할 수 있는지, 같이 알아볼 사람과 이야기해 보고 싶습니다.");
        bodyLines.Add("관심 있는 분은 게시글의 함께하기에서 구매자·공급자·운송·통관·창고 역할 중 가능한 것을 표시해 주세요.");
        bodyLines.Add("관심이 모이면 공동구매 또는 공동수입 검토를 위한 비구속적 가원장으로 조건을 함께 살펴봅니다.");
        bodyLines.Add("참여 표시는 주문·계약·결제·배차·운송 주선을 확정하지 않습니다.");
        bodyLines.Add(string.Empty);
        bodyLines.Add("---");
        bodyLines.Add("이 글은 영상 정보 공유이며 구매·수입·계약 제안이 아닙니다.");

        return new PlatformCommunityPostDraft(
            "자유",
            "공동구매",
            title,
            string.Join(Environment.NewLine, bodyLines),
            BuildTimestampUrl(sourceUri, candidate.영상구간초),
            $"{channelName} · {productName}",
            PlatformCommunityPostDraftSourceKinds.YouTubeFood);
    }

    private static string Required(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{name}이(가) 필요합니다.", nameof(value))
            : value.Trim();

    private static string NormalizeCountryCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? "국가 미확인" : value.Trim().ToUpperInvariant();

    private static string TrimTo(string value, int maxLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string BuildTimestampUrl(Uri sourceUri, int? seconds)
    {
        if (seconds is null or < 1)
        {
            return sourceUri.ToString();
        }

        var builder = new UriBuilder(sourceUri);
        var query = builder.Query.TrimStart('?');
        builder.Query = string.IsNullOrWhiteSpace(query)
            ? $"t={seconds.Value}s"
            : $"{query}&t={seconds.Value}s";
        return builder.Uri.ToString();
    }
}
