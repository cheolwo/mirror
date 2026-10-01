using System.Globalization;
using System.Text.Json;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed class UsdaNassQuickStats가격공급자 : I미국농수산가격공급자
{
    private const string NassAttribution =
        "This product uses the NASS API but is not endorsed or certified by NASS.";

    private readonly HttpClient _httpClient;
    private readonly PublicDataOptions _options;

    public UsdaNassQuickStats가격공급자(
        HttpClient httpClient,
        IOptions<PublicDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string SourceKey => 미국농수산가격출처Keys.UsdaNassQuickStats;

    public string ProviderName => "USDA National Agricultural Statistics Service (NASS)";

    public string DocumentationUrl => "https://quickstats.nass.usda.gov/api";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.UsdaNassQuickStats.ApiKey);

    public async Task<미국농수산가격조회응답> 조회Async(
        미국농수산가격조회요청 request,
        CancellationToken cancellationToken = default)
    {
        var normalized = 미국농수산가격조회요청Rules.Normalize(request);
        var validationError = 미국농수산가격조회요청Rules.Validate(normalized);
        if (validationError is not null)
        {
            return Fail(normalized, 미국농수산가격조회상태Codes.잘못된요청, validationError);
        }

        if (!IsConfigured)
        {
            return Fail(
                normalized,
                미국농수산가격조회상태Codes.설정안됨,
                "USDA NASS Quick Stats API 키가 설정되지 않았습니다.");
        }

        var requestPath = BuildRequestPath(normalized);
        using var response = await _httpClient.GetAsync(
            requestPath,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Fail(
                normalized,
                미국농수산가격조회상태Codes.자료조회불가,
                $"USDA NASS 자료 조회에 실패했습니다. HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (TryGetProperty(root, "error", out _))
        {
            return Fail(
                normalized,
                미국농수산가격조회상태Codes.자료조회불가,
                "USDA NASS가 현재 조회 조건을 처리하지 못했습니다. 조건을 더 좁혀 주세요.");
        }

        if (!TryGetProperty(root, "data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return Fail(
                normalized,
                미국농수산가격조회상태Codes.자료조회불가,
                "USDA NASS 응답에서 가격 자료를 확인하지 못했습니다.");
        }

        var totalCount = data.GetArrayLength();
        if (totalCount == 0)
        {
            return Success(
                normalized,
                미국농수산가격조회상태Codes.자료없음,
                [],
                0,
                false,
                "조회 조건에 맞는 USDA NASS 자료가 없습니다.");
        }

        var items = data
            .EnumerateArray()
            .Take(normalized.MaxItems)
            .Select(MapItem)
            .ToArray();
        var truncated = totalCount > items.Length;

        return Success(
            normalized,
            미국농수산가격조회상태Codes.완료,
            items,
            totalCount,
            truncated,
            truncated
                ? $"USDA NASS 자료 {totalCount:N0}건 중 {items.Length:N0}건을 제공합니다."
                : $"USDA NASS 자료 {items.Length:N0}건을 제공합니다.");
    }

    private string BuildRequestPath(미국농수산가격조회요청 request)
    {
        var query = new Dictionary<string, string?>
        {
            ["key"] = _options.UsdaNassQuickStats.ApiKey,
            ["commodity_desc"] = request.Commodity,
            ["statisticcat_desc"] = request.StatisticCategory,
            ["source_desc"] = request.Program,
            ["sector_desc"] = request.Sector,
            ["group_desc"] = request.Group,
            ["agg_level_desc"] = request.AggregationLevel,
            ["state_alpha"] = request.StateAlpha,
            ["domain_desc"] = request.Domain,
            ["freq_desc"] = request.Frequency,
            ["year__GE"] = request.YearFrom.ToString(CultureInfo.InvariantCulture),
            ["year__LE"] = request.YearTo?.ToString(CultureInfo.InvariantCulture),
            ["format"] = "JSON"
        };

        return QueryHelpers.AddQueryString(
            _options.UsdaNassQuickStats.DataPath.TrimStart('/'),
            query);
    }

    private static 미국농수산가격항목 MapItem(JsonElement source)
    {
        var rawValue = ReadString(source, "Value");
        var numericValue = ParseDecimal(rawValue);

        return new 미국농수산가격항목
        {
            Commodity = ReadString(source, "commodity_desc"),
            Class = ReadString(source, "class_desc"),
            ShortDescription = ReadString(source, "short_desc"),
            Sector = ReadString(source, "sector_desc"),
            Group = ReadString(source, "group_desc"),
            StatisticCategory = ReadString(source, "statisticcat_desc"),
            Unit = ReadString(source, "unit_desc"),
            RawValue = rawValue,
            NumericValue = numericValue,
            IsSuppressed = numericValue is null && !string.IsNullOrWhiteSpace(rawValue),
            Program = ReadString(source, "source_desc"),
            AggregationLevel = ReadString(source, "agg_level_desc"),
            StateAlpha = ReadString(source, "state_alpha"),
            StateName = ReadString(source, "state_name"),
            Year = ReadString(source, "year"),
            Frequency = ReadString(source, "freq_desc"),
            ReferencePeriod = ReadString(source, "reference_period_desc"),
            LoadTime = ReadString(source, "load_time")
        };
    }

    private 미국농수산가격조회응답 Success(
        미국농수산가격조회요청 request,
        string statusCode,
        IReadOnlyList<미국농수산가격항목> items,
        int totalCount,
        bool isTruncated,
        string summary)
        => new()
        {
            Success = true,
            StatusCode = statusCode,
            SourceKey = SourceKey,
            Provider = ProviderName,
            DocumentationUrl = DocumentationUrl,
            Query = request,
            Items = items,
            TotalCount = totalCount,
            IsTruncated = isTruncated,
            CollectedAtUtc = DateTime.UtcNow,
            Summary = summary,
            Notices = BuildNotices()
        };

    private 미국농수산가격조회응답 Fail(
        미국농수산가격조회요청 request,
        string statusCode,
        string errorMessage)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            SourceKey = SourceKey,
            Provider = ProviderName,
            DocumentationUrl = DocumentationUrl,
            Query = request,
            CollectedAtUtc = DateTime.UtcNow,
            Summary = errorMessage,
            Notices = BuildNotices()
        };

    private static IReadOnlyList<string> BuildNotices()
        =>
        [
            NassAttribution,
            "미국 공식 집계 통계이며 개별 주문·거래·운송 견적이 아닙니다.",
            "단위, 품질, 유통 단계와 조사 기준이 국내 aT 가격과 다르므로 직접 비교할 때 주의해야 합니다.",
            "(D), (Z) 등 비공개·극소량 표시는 원문을 유지하며 숫자 값으로 변환하지 않습니다."
        ];

    private static string ReadString(JsonElement source, string propertyName)
    {
        if (!TryGetProperty(source, propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };
    }

    private static bool TryGetProperty(
        JsonElement source,
        string propertyName,
        out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static decimal? ParseDecimal(string value)
    {
        var normalized = value.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }
}
