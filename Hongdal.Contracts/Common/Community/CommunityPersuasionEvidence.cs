using System.Globalization;

namespace Hongdal.Contracts.Common.Community;

public static class CommunityEvidenceChartTypeCodes
{
    public const string Bar = "bar";
    public const string Line = "line";
    public const string Donut = "donut";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Bar,
        Line,
        Donut
    };
}

public sealed record CommunityEvidenceChartPoint(
    string Label,
    decimal Value);

public sealed class CommunityEvidenceChartBlock
{
    public string ChartTypeCode { get; init; } = CommunityEvidenceChartTypeCodes.Bar;
    public string Title { get; init; } = string.Empty;
    public string Claim { get; init; } = string.Empty;
    public string SeriesLabel { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string ReferenceDate { get; init; } = string.Empty;
    public string Interpretation { get; init; } = string.Empty;
    public string Limitation { get; init; } = string.Empty;
    public IReadOnlyList<CommunityEvidenceChartPoint> Points { get; init; } = [];
}

public sealed record CommunityEvidenceChartStatistics(
    decimal Total,
    decimal Average,
    decimal Minimum,
    decimal Maximum,
    decimal? FirstToLastChangePercent);

public sealed record CommunityEvidenceChartValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public static class CommunityEvidenceChartPolicy
{
    public const int MinimumPointCount = 2;
    public const int MaximumPointCount = 12;

    public static CommunityEvidenceChartValidationResult Validate(
        CommunityEvidenceChartBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var errors = new List<string>();

        ValidateRequired(block.Title, "그래프 제목", 120, errors);
        ValidateRequired(block.Claim, "그래프로 뒷받침할 주장", 240, errors);
        ValidateRequired(block.SeriesLabel, "수치 계열 이름", 80, errors);
        ValidateRequired(block.Unit, "수치 단위", 30, errors);
        ValidateRequired(block.SourceLabel, "자료 출처", 160, errors);
        ValidateRequired(block.ReferenceDate, "자료 기준일", 40, errors);
        ValidateRequired(block.Interpretation, "수치 해석", 500, errors);
        ValidateRequired(block.Limitation, "자료의 한계", 500, errors);

        if (!CommunityEvidenceChartTypeCodes.All.Contains(block.ChartTypeCode))
        {
            errors.Add("지원하는 그래프 유형은 막대, 선, 도넛입니다.");
        }

        if (!string.IsNullOrWhiteSpace(block.SourceUrl)
            && (!Uri.TryCreate(block.SourceUrl.Trim(), UriKind.Absolute, out var sourceUri)
                || sourceUri.Scheme is not ("http" or "https")))
        {
            errors.Add("자료 원문 주소는 http 또는 https 주소여야 합니다.");
        }

        if (block.Points.Count is < MinimumPointCount or > MaximumPointCount)
        {
            errors.Add($"그래프 데이터는 {MinimumPointCount}개 이상 {MaximumPointCount}개 이하로 입력해야 합니다.");
        }

        foreach (var point in block.Points)
        {
            if (string.IsNullOrWhiteSpace(point.Label) || point.Label.Trim().Length > 40)
            {
                errors.Add("각 데이터 이름은 1자 이상 40자 이하여야 합니다.");
                break;
            }

            if (Math.Abs(point.Value) > 1_000_000_000_000_000m)
            {
                errors.Add("그래프 수치는 절댓값 1,000조 이하여야 합니다.");
                break;
            }
        }

        if (block.Points
            .Where(point => !string.IsNullOrWhiteSpace(point.Label))
            .GroupBy(point => point.Label.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            errors.Add("같은 데이터 이름을 중복해서 사용할 수 없습니다.");
        }

        if (string.Equals(
                block.ChartTypeCode,
                CommunityEvidenceChartTypeCodes.Donut,
                StringComparison.OrdinalIgnoreCase)
            && (block.Points.Any(point => point.Value < 0m)
                || block.Points.Sum(point => point.Value) <= 0m))
        {
            errors.Add("도넛 그래프는 0 이상의 수치와 0보다 큰 합계가 필요합니다.");
        }

        return new CommunityEvidenceChartValidationResult(errors.Count == 0, errors);
    }

    public static CommunityEvidenceChartStatistics CalculateStatistics(
        CommunityEvidenceChartBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Points.Count == 0)
        {
            throw new ArgumentException("통계를 계산할 데이터가 없습니다.", nameof(block));
        }

        var values = block.Points.Select(point => point.Value).ToArray();
        var firstToLastChange = values[0] == 0m
            ? (decimal?)null
            : Math.Round(
                (values[^1] - values[0]) / Math.Abs(values[0]) * 100m,
                2,
                MidpointRounding.AwayFromZero);
        return new CommunityEvidenceChartStatistics(
            Math.Round(values.Sum(), 4, MidpointRounding.AwayFromZero),
            Math.Round(values.Average(), 4, MidpointRounding.AwayFromZero),
            values.Min(),
            values.Max(),
            firstToLastChange);
    }

    private static void ValidateRequired(
        string? value,
        string label,
        int maximumLength,
        ICollection<string> errors)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            errors.Add($"{label}은 1자 이상 {maximumLength}자 이하여야 합니다.");
        }
    }
}

public static class CommunityEvidenceChartTextCodec
{
    public const string StartMarker = "[[hongdal-evidence-chart:v1]]";
    public const string EndMarker = "[[/hongdal-evidence-chart]]";

    private const string TitlePrefix = "통계 근거 · ";
    private const string ChartTypePrefix = "차트: ";
    private const string ClaimPrefix = "주장: ";
    private const string SeriesPrefix = "계열: ";
    private const string UnitPrefix = "단위: ";
    private const string SourcePrefix = "출처: ";
    private const string SourceUrlPrefix = "원문: ";
    private const string ReferenceDatePrefix = "기준일: ";
    private const string InterpretationPrefix = "해석: ";
    private const string LimitationPrefix = "한계: ";
    private const string DataHeader = "데이터:";

    public static string Encode(CommunityEvidenceChartBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var validation = CommunityEvidenceChartPolicy.Validate(block);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                string.Join(" ", validation.Errors),
                nameof(block));
        }

        var lines = new List<string>
        {
            StartMarker,
            TitlePrefix + SingleLine(block.Title),
            ChartTypePrefix + block.ChartTypeCode.Trim().ToLowerInvariant(),
            ClaimPrefix + SingleLine(block.Claim),
            SeriesPrefix + SingleLine(block.SeriesLabel),
            UnitPrefix + SingleLine(block.Unit),
            SourcePrefix + SingleLine(block.SourceLabel)
        };
        if (!string.IsNullOrWhiteSpace(block.SourceUrl))
        {
            lines.Add(SourceUrlPrefix + block.SourceUrl.Trim());
        }

        lines.Add(ReferenceDatePrefix + SingleLine(block.ReferenceDate));
        lines.Add(DataHeader);
        lines.AddRange(block.Points.Select(point =>
            $"- {SingleLine(point.Label).Replace('|', '/')} | {point.Value.ToString("0.############################", CultureInfo.InvariantCulture)}"));
        lines.Add(InterpretationPrefix + SingleLine(block.Interpretation));
        lines.Add(LimitationPrefix + SingleLine(block.Limitation));
        lines.Add(EndMarker);
        return string.Join(Environment.NewLine, lines);
    }

    public static IReadOnlyList<CommunityEvidenceChartBlock> DecodeAll(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        var lines = SplitLines(body);
        var blocks = new List<CommunityEvidenceChartBlock>();
        for (var index = 0; index < lines.Length; index++)
        {
            if (!string.Equals(lines[index].Trim(), StartMarker, StringComparison.Ordinal))
            {
                continue;
            }

            var endIndex = FindEndMarker(lines, index + 1);
            if (endIndex < 0)
            {
                break;
            }

            var block = DecodeBlock(lines[(index + 1)..endIndex]);
            if (block is not null && CommunityEvidenceChartPolicy.Validate(block).IsValid)
            {
                blocks.Add(block);
            }

            index = endIndex;
        }

        return blocks;
    }

    public static string StripBlocks(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var lines = SplitLines(body);
        var visibleLines = new List<string>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!string.Equals(lines[index].Trim(), StartMarker, StringComparison.Ordinal))
            {
                visibleLines.Add(lines[index]);
                continue;
            }

            var endIndex = FindEndMarker(lines, index + 1);
            if (endIndex < 0)
            {
                visibleLines.Add(lines[index]);
                continue;
            }

            index = endIndex;
        }

        return string.Join(Environment.NewLine, CollapseBlankLines(visibleLines)).Trim();
    }

    public static bool TryReplaceLastBlock(
        string? body,
        CommunityEvidenceChartBlock replacement,
        out string updatedBody)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        updatedBody = body ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var startIndex = body.LastIndexOf(StartMarker, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return false;
        }

        var endMarkerIndex = body.IndexOf(EndMarker, startIndex, StringComparison.Ordinal);
        if (endMarkerIndex < 0)
        {
            return false;
        }

        var endIndex = endMarkerIndex + EndMarker.Length;
        var existingBlockText = body[startIndex..endIndex];
        if (DecodeAll(existingBlockText).Count != 1)
        {
            return false;
        }

        var encodedReplacement = Encode(replacement);
        updatedBody = string.Concat(
            body.AsSpan(0, startIndex),
            encodedReplacement,
            body.AsSpan(endIndex));
        return true;
    }

    private static CommunityEvidenceChartBlock? DecodeBlock(IReadOnlyList<string> lines)
    {
        string title = string.Empty;
        string chartType = string.Empty;
        string claim = string.Empty;
        string series = string.Empty;
        string unit = string.Empty;
        string source = string.Empty;
        string sourceUrl = string.Empty;
        string referenceDate = string.Empty;
        string interpretation = string.Empty;
        string limitation = string.Empty;
        var points = new List<CommunityEvidenceChartPoint>();
        var readingData = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line == DataHeader)
            {
                readingData = true;
                continue;
            }

            if (readingData && line.StartsWith("- ", StringComparison.Ordinal))
            {
                var separatorIndex = line.LastIndexOf('|');
                if (separatorIndex > 2
                    && decimal.TryParse(
                        line[(separatorIndex + 1)..].Trim(),
                        NumberStyles.Number | NumberStyles.AllowExponent,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    points.Add(new CommunityEvidenceChartPoint(
                        line[2..separatorIndex].Trim(),
                        value));
                }

                continue;
            }

            readingData = false;
            title = ReadValue(line, TitlePrefix, title);
            chartType = ReadValue(line, ChartTypePrefix, chartType);
            claim = ReadValue(line, ClaimPrefix, claim);
            series = ReadValue(line, SeriesPrefix, series);
            unit = ReadValue(line, UnitPrefix, unit);
            source = ReadValue(line, SourcePrefix, source);
            sourceUrl = ReadValue(line, SourceUrlPrefix, sourceUrl);
            referenceDate = ReadValue(line, ReferenceDatePrefix, referenceDate);
            interpretation = ReadValue(line, InterpretationPrefix, interpretation);
            limitation = ReadValue(line, LimitationPrefix, limitation);
        }

        if (title.Length == 0)
        {
            return null;
        }

        return new CommunityEvidenceChartBlock
        {
            ChartTypeCode = chartType,
            Title = title,
            Claim = claim,
            SeriesLabel = series,
            Unit = unit,
            SourceLabel = source,
            SourceUrl = sourceUrl,
            ReferenceDate = referenceDate,
            Interpretation = interpretation,
            Limitation = limitation,
            Points = points
        };
    }

    private static string ReadValue(string line, string prefix, string currentValue)
        => line.StartsWith(prefix, StringComparison.Ordinal)
            ? line[prefix.Length..].Trim()
            : currentValue;

    private static int FindEndMarker(IReadOnlyList<string> lines, int startIndex)
    {
        for (var index = startIndex; index < lines.Count; index++)
        {
            if (string.Equals(lines[index].Trim(), EndMarker, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string[] SplitLines(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static IEnumerable<string> CollapseBlankLines(IEnumerable<string> lines)
    {
        var previousWasBlank = false;
        foreach (var line in lines)
        {
            var isBlank = string.IsNullOrWhiteSpace(line);
            if (isBlank && previousWasBlank)
            {
                continue;
            }

            previousWasBlank = isBlank;
            yield return line;
        }
    }

    private static string SingleLine(string value)
        => string.Join(
            " ",
            value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
