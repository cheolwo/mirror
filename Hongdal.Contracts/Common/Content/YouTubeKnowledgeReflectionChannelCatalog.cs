namespace Hongdal.Contracts.Common.Content;

public static class YouTube지식성찰주제코드
{
    public const string 자기계발 = "self-development";
    public const string 철학 = "philosophy";
    public const string 심리 = "psychology";
    public const string 윤리 = "ethics";
    public const string 마음챙김 = "mindfulness";
    public const string 종교교육 = "religious-education";
    public const string 아이디어 = "ideas";

    public static IReadOnlyList<string> 전체 { get; } =
    [
        자기계발,
        철학,
        심리,
        윤리,
        마음챙김,
        종교교육,
        아이디어
    ];
}

public sealed record YouTube지식성찰채널Catalog항목(
    string Key,
    string? ChannelId,
    string? Handle,
    string 표시이름,
    string 국가코드,
    string 기본언어코드,
    IReadOnlyList<string> 주제코드목록,
    string 관점표시,
    string 공식출처Url,
    DateTime 자료확인일시Utc,
    string 메모);

/// <summary>
/// 순위나 교리의 우열을 뜻하지 않는 대표 수집 후보입니다.
/// 설정에서 명시적으로 시드를 켠 경우에만 공식 handle을 YouTube Data API로 해석해 감시 채널로 추가합니다.
/// 반야 게시 허용은 이 카탈로그와 별개로 관리자가 채널별 승인해야 합니다.
/// </summary>
public static class YouTube지식성찰채널Catalog
{
    public static IReadOnlyList<YouTube지식성찰채널Catalog항목> 항목 { get; } =
    [
        new(
            "hongik-hakdang",
            "UCI8HW08rOSlvweOjJ9Gp2Ng",
            null,
            "홍익학당",
            "KR",
            "ko",
            [YouTube지식성찰주제코드.철학, YouTube지식성찰주제코드.윤리, YouTube지식성찰주제코드.자기계발],
            "홍익·양심 공부",
            "https://www.youtube.com/channel/UCI8HW08rOSlvweOjJ9Gp2Ng",
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            "기존 홍익학당 수집 모듈을 지식·성찰 공통 채널 모델로 연결합니다."),
        new(
            "ted",
            null,
            "@TED",
            "TED",
            "US",
            "en",
            [YouTube지식성찰주제코드.아이디어, YouTube지식성찰주제코드.자기계발],
            "아이디어·강연",
            "https://www.youtube.com/@TED",
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            "대표 수집 후보이며 인기 순위나 공식 추천을 의미하지 않습니다."),
        new(
            "big-think",
            null,
            "@bigthink",
            "Big Think",
            "US",
            "en",
            [YouTube지식성찰주제코드.철학, YouTube지식성찰주제코드.심리, YouTube지식성찰주제코드.아이디어],
            "철학·과학·사고법",
            "https://www.youtube.com/@bigthink",
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            "대표 수집 후보이며 인기 순위나 공식 추천을 의미하지 않습니다."),
        new(
            "school-of-life",
            null,
            "@theschooloflifetv",
            "The School of Life",
            "GB",
            "en",
            [YouTube지식성찰주제코드.철학, YouTube지식성찰주제코드.심리, YouTube지식성찰주제코드.자기계발],
            "철학·관계·자기이해",
            "https://www.youtube.com/@theschooloflifetv",
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            "대표 수집 후보이며 인기 순위나 공식 추천을 의미하지 않습니다."),
        new(
            "bible-project",
            null,
            "@bibleproject",
            "BibleProject",
            "US",
            "en",
            [YouTube지식성찰주제코드.종교교육],
            "기독교 성서 교육",
            "https://www.youtube.com/@bibleproject",
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            "특정 종교의 우월성을 뜻하지 않는 교육 자료 수집 후보입니다."),
        new(
            "plum-village",
            null,
            "@plumvillageapp",
            "Plum Village App",
            "FR",
            "en",
            [YouTube지식성찰주제코드.마음챙김, YouTube지식성찰주제코드.종교교육],
            "불교·마음챙김",
            "https://www.youtube.com/@plumvillageapp",
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            "특정 종교의 우월성을 뜻하지 않는 교육 자료 수집 후보입니다."),
        new(
            "sadhguru",
            null,
            "@sadhguru",
            "Sadhguru",
            "IN",
            "en",
            [YouTube지식성찰주제코드.자기계발, YouTube지식성찰주제코드.마음챙김, YouTube지식성찰주제코드.종교교육],
            "영성·자기계발",
            "https://www.youtube.com/@sadhguru",
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            "대표 수집 후보이며 인기 순위나 공식 추천을 의미하지 않습니다.")
    ];

    public static YouTube지식성찰채널Catalog항목? 찾기(string? channelId, string? handle = null)
        => 항목.FirstOrDefault(item =>
            (!string.IsNullOrWhiteSpace(channelId)
             && string.Equals(item.ChannelId, channelId.Trim(), StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(handle)
                && string.Equals(item.Handle, NormalizeHandle(handle), StringComparison.OrdinalIgnoreCase)));

    public static string NormalizeHandle(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        return normalized.StartsWith('@') ? normalized : $"@{normalized}";
    }
}
