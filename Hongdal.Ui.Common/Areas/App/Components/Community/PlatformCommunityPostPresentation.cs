using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

internal static class PlatformCommunityPostPresentation
{
    public static string ResolveVisibleBody(string? body)
        => CommunityEvidenceChartTextCodec.StripBlocks(body);

    public static bool IsDiagramPost(string? category, string? body)
        => (!string.IsNullOrWhiteSpace(category)
            && category.Contains("다이어그램", StringComparison.OrdinalIgnoreCase))
           || (!string.IsNullOrWhiteSpace(body)
               && (body.Contains("->", StringComparison.Ordinal)
                   || body.Contains("-->", StringComparison.Ordinal)
                   || body.Contains("```mermaid", StringComparison.OrdinalIgnoreCase)));

    public static IReadOnlyList<string> BuildDiagramPreviewNodes(string? body, string fallbackLabel)
    {
        var nodes = (body ?? string.Empty)
            .Replace("-->", "->", StringComparison.Ordinal)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains("->", StringComparison.Ordinal))
            .SelectMany(line => line.Split("->", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(NormalizeDiagramNodeLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        return nodes.Length > 1
            ? nodes
            : [fallbackLabel, "사람 확인", "업무 처리", "상태 공유"];
    }

    public static bool IsYouTubeSharedLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.TrimStart('.');
        return host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
               || host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)
               || host.Equals("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFoodYouTubePost(PlatformCommunityPostResponse post)
        => post.Title.StartsWith("[음식 발견]", StringComparison.OrdinalIgnoreCase);

    public static string ResolveSharedVideoEyebrow(PlatformCommunityPostResponse post)
        => IsFoodYouTubePost(post)
            ? "영상에서 발견한 음식"
            : post.Title.StartsWith("[반야 나눔]", StringComparison.OrdinalIgnoreCase)
                ? "영상과 함께 나눈 글귀"
                : "커뮤니티에 공유한 영상";

    public static string ResolveSharedVideoTitle(string title)
    {
        foreach (var prefix in new[] { "[반야 나눔] ", "[음식 발견] " })
        {
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return title[prefix.Length..];
            }
        }

        return title;
    }

    private static string NormalizeDiagramNodeLabel(string value)
    {
        var label = value.Trim().Trim('-', '>', '`', '*', '#', ' ', ';');
        var openBracket = label.IndexOf('[');
        var closeBracket = label.LastIndexOf(']');
        if (openBracket >= 0 && closeBracket > openBracket)
        {
            label = label[(openBracket + 1)..closeBracket];
        }

        return label.Length > 24 ? label[..24] + "…" : label;
    }
}
