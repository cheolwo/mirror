namespace Hongdal.Services.Community;

public interface I커뮤니티게시글음성본문분할기
{
    IReadOnlyList<string> 분할(string 제목, string 본문, int 최대문자수);
}

public sealed class 커뮤니티게시글음성본문분할기 : I커뮤니티게시글음성본문분할기
{
    private static readonly char[] Boundaries = ['\n', '.', '!', '?', '。', ' '];

    public IReadOnlyList<string> 분할(string 제목, string 본문, int 최대문자수)
    {
        최대문자수 = Math.Clamp(최대문자수, 100, 2000);
        var cleanTitle = Normalize(제목);
        var cleanBody = Normalize(본문);
        var text = cleanTitle.Length == 0
            ? cleanBody
            : cleanBody.Length == 0 ? cleanTitle : $"{cleanTitle}. {cleanBody}";
        if (text.Length == 0)
        {
            return [];
        }

        var segments = new List<string>();
        var offset = 0;
        while (offset < text.Length)
        {
            var remaining = text.Length - offset;
            if (remaining <= 최대문자수)
            {
                segments.Add(text[offset..].Trim());
                break;
            }

            var window = text.AsSpan(offset, 최대문자수);
            var minimumBreak = 최대문자수 / 2;
            var breakAt = FindBreak(window, minimumBreak);
            var length = breakAt >= minimumBreak ? breakAt + 1 : 최대문자수;
            var segment = text.Substring(offset, length).Trim();
            if (segment.Length > 0)
            {
                segments.Add(segment);
            }

            offset += length;
            while (offset < text.Length && char.IsWhiteSpace(text[offset]))
            {
                offset++;
            }
        }

        return segments;
    }

    private static int FindBreak(ReadOnlySpan<char> window, int minimumBreak)
    {
        for (var i = window.Length - 1; i >= minimumBreak; i--)
        {
            if (Boundaries.Contains(window[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Normalize(string value)
        => string.Join(" ", value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
