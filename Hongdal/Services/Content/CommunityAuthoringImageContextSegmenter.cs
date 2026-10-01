using System.Text;
using Hongdal.Contracts.Common.Metadata;

namespace Hongdal.Services.Content;

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.Application,
    "제목·소제목·문단을 연속된 이미지 문맥 그룹으로 결정론적으로 분할",
    FlowOrder = 41,
    Boundary = "입력 텍스트만 읽는 순수 변환이며 외부 서비스에 내용을 보내지 않습니다.")]
internal static class CommunityAuthoringImageContextSegmenter
{
    public static CommunityAuthoringImageContextPlan Create(
        string articleTitle,
        string body,
        int maxImages)
    {
        var sections = ParseSections(articleTitle, body);
        var groupedSections = GroupContiguousSections(sections, maxImages);
        var groups = groupedSections
            .Select((group, index) => new CommunityAuthoringImageContextGroup(
                BuildSegmentTitle(group, index + 1),
                BuildContext(group)))
            .ToArray();
        return new CommunityAuthoringImageContextPlan(sections.Count, groups);
    }

    private static IReadOnlyList<SourceSection> ParseSections(string articleTitle, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [new SourceSection(articleTitle, articleTitle)];
        }

        var lines = body.Split('\n');
        var sections = new List<SourceSection>();
        var content = new List<string>();
        string? heading = null;
        var previousWasBlank = true;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (heading is null && content.Count > 0)
                {
                    AddSection(sections, null, content);
                }

                previousWasBlank = true;
                continue;
            }

            if (TryReadHeading(lines, index, previousWasBlank, out var nextHeading))
            {
                AddSection(sections, heading, content);
                heading = nextHeading;
                previousWasBlank = false;
                continue;
            }

            content.Add(line);
            previousWasBlank = false;
        }

        AddSection(sections, heading, content);
        if (sections.Count == 0)
        {
            sections.Add(new SourceSection(articleTitle, body));
        }

        for (var index = 0; index < sections.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(sections[index].Title))
            {
                continue;
            }

            sections[index] = sections[index] with
            {
                Title = index == 0 ? "도입" : $"문맥 {index + 1}"
            };
        }

        return sections;
    }

    private static bool TryReadHeading(
        IReadOnlyList<string> lines,
        int index,
        bool previousWasBlank,
        out string heading)
    {
        var line = lines[index].Trim();
        var markdownHeading = line.TrimStart('#').Trim();
        if (line.StartsWith('#') && !string.IsNullOrWhiteSpace(markdownHeading))
        {
            heading = TrimTo(markdownHeading, 120);
            return true;
        }

        if (!previousWasBlank
            || line.Length is < 2 or > 80
            || StartsWithBullet(line)
            || Uri.TryCreate(line, UriKind.Absolute, out _)
            || EndsLikeSentence(line)
            || !HasFollowingContent(lines, index + 1))
        {
            heading = string.Empty;
            return false;
        }

        heading = TrimTo(line.TrimEnd(':', '：'), 120);
        return true;
    }

    private static bool StartsWithBullet(string line)
        => line.StartsWith("- ", StringComparison.Ordinal)
           || line.StartsWith("* ", StringComparison.Ordinal)
           || line.StartsWith("+ ", StringComparison.Ordinal)
           || line.StartsWith("•", StringComparison.Ordinal);

    private static bool EndsLikeSentence(string line)
        => line.EndsWith('.')
           || line.EndsWith('!')
           || line.EndsWith('?')
           || line.EndsWith('。');

    private static bool HasFollowingContent(IReadOnlyList<string> lines, int startIndex)
    {
        for (var index = startIndex; index < lines.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddSection(
        ICollection<SourceSection> sections,
        string? heading,
        ICollection<string> content)
    {
        if (string.IsNullOrWhiteSpace(heading) && content.Count == 0)
        {
            return;
        }

        sections.Add(new SourceSection(heading ?? string.Empty, string.Join('\n', content).Trim()));
        content.Clear();
    }

    private static IReadOnlyList<IReadOnlyList<SourceSection>> GroupContiguousSections(
        IReadOnlyList<SourceSection> sections,
        int maxImages)
    {
        var groupCount = Math.Min(sections.Count, maxImages);
        var minimumGroupSize = sections.Count / groupCount;
        var largerGroupCount = sections.Count % groupCount;
        var groups = new List<IReadOnlyList<SourceSection>>(groupCount);
        var offset = 0;

        for (var index = 0; index < groupCount; index++)
        {
            var size = minimumGroupSize + (index < largerGroupCount ? 1 : 0);
            groups.Add(sections.Skip(offset).Take(size).ToArray());
            offset += size;
        }

        return groups;
    }

    private static string BuildSegmentTitle(IReadOnlyList<SourceSection> sections, int sequence)
    {
        var titles = sections
            .Select(section => section.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (titles.Length == 0)
        {
            return $"문맥 {sequence}";
        }

        return titles.Length == 1
            ? TrimTo(titles[0], 120)
            : TrimTo($"{titles[0]} - {titles[^1]}", 120);
    }

    private static string BuildContext(IReadOnlyList<SourceSection> sections)
    {
        var builder = new StringBuilder();
        foreach (var section in sections)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                builder.Append('[').Append(section.Title).AppendLine("]");
            }

            builder.Append(string.IsNullOrWhiteSpace(section.Content) ? section.Title : section.Content);
        }

        return builder.ToString().Trim();
    }

    private static string TrimTo(string value, int maximumLength)
        => value.Length <= maximumLength ? value : $"{value[..(maximumLength - 3)].TrimEnd()}...";

    private sealed record SourceSection(string Title, string Content);
}

internal sealed record CommunityAuthoringImageContextPlan(
    int SourceSectionCount,
    IReadOnlyList<CommunityAuthoringImageContextGroup> Groups);

internal sealed record CommunityAuthoringImageContextGroup(
    string Title,
    string Context);
