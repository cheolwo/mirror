using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace 홍달.Services.External.PublicData;

public interface IFishCooperativeStatisticsClient
{
    Task<IReadOnlyList<FishCooperativeGeneralStatisticsItem>> FetchGeneralStatisticsAsync(
        DateOnly baseMonth,
        CancellationToken cancellationToken = default);
}

public sealed class FishCooperativeGeneralStatisticsItem
{
    public string BaseYearMonth { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string FinancialCompanyCode { get; init; } = string.Empty;

    public string FinancialCompanyName { get; init; } = string.Empty;

    public decimal? EmployeeCount { get; init; }

    public string EmployeeClassificationCode { get; init; } = string.Empty;

    public string EmployeeClassificationName { get; init; } = string.Empty;
}

public sealed class FishCooperativeStatisticsClient : IFishCooperativeStatisticsClient
{
    private const int MaximumPageCount = 5;
    private readonly HttpClient _httpClient;
    private readonly PublicDataOptions _options;

    public FishCooperativeStatisticsClient(
        HttpClient httpClient,
        IOptions<PublicDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<FishCooperativeGeneralStatisticsItem>> FetchGeneralStatisticsAsync(
        DateOnly baseMonth,
        CancellationToken cancellationToken = default)
    {
        var serviceKey = ResolveServiceKey();
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            throw new InvalidOperationException(
                "PublicData:FishCooperativeStatistics:ServiceKey 또는 PublicData:DataGoKrServiceKey 설정이 필요합니다.");
        }

        var pageSize = Math.Clamp(_options.FishCooperativeStatistics.PageSize, 10, 1000);
        var results = new List<FishCooperativeGeneralStatisticsItem>();
        for (var page = 1; page <= MaximumPageCount; page++)
        {
            var response = await FetchPageAsync(
                baseMonth,
                page,
                pageSize,
                serviceKey,
                cancellationToken);
            results.AddRange(response.Items);

            if (response.ReturnedCount < pageSize
                || response.TotalCount.HasValue && results.Count >= response.TotalCount.Value)
            {
                break;
            }
        }

        return results;
    }

    private async Task<PageResponse> FetchPageAsync(
        DateOnly baseMonth,
        int page,
        int pageSize,
        string serviceKey,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["serviceKey"] = serviceKey,
            ["resultType"] = "json",
            ["pageNo"] = page.ToString(CultureInfo.InvariantCulture),
            ["numOfRows"] = pageSize.ToString(CultureInfo.InvariantCulture),
            ["title"] = _options.FishCooperativeStatistics.GeneralStatisticsTitle,
            ["basYm"] = baseMonth.ToString("yyyyMM", CultureInfo.InvariantCulture)
        };
        var relativePath = QueryHelpers.AddQueryString(
            _options.FishCooperativeStatistics.GeneralStatisticsPath.TrimStart('/'),
            query);

        using var response = await _httpClient.GetAsync(relativePath, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"수산업협동조합 공공데이터 호출 실패: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        EnsureSuccessfulPublicDataResponse(body);
        var responseItems = PublicDataParsing.ReadItems(body);
        var items = responseItems
            .Select(ToItem)
            .Where(item => !string.IsNullOrWhiteSpace(item.FinancialCompanyName)
                           && item.EmployeeCount.HasValue)
            .ToArray();
        return new PageResponse(
            items,
            PublicDataParsing.ReadTotalCount(body),
            responseItems.Count);
    }

    private static FishCooperativeGeneralStatisticsItem ToItem(
        Dictionary<string, string?> values)
        => new()
        {
            BaseYearMonth = Value(values, "basYm"),
            Title = Value(values, "title"),
            FinancialCompanyCode = Value(values, "fncoCd"),
            FinancialCompanyName = Value(values, "fncoNm"),
            EmployeeCount = PublicDataParsing.FirstDecimal(values, "xcsmCnt"),
            EmployeeClassificationCode = Value(values, "xcsmDcd"),
            EmployeeClassificationName = Value(values, "xcsmDcdNm")
        };

    private static string Value(Dictionary<string, string?> values, params string[] keys)
        => PublicDataParsing.FirstValue(values, keys)?.Trim() ?? string.Empty;

    private static void EnsureSuccessfulPublicDataResponse(string body)
    {
        var resultCode = PublicDataParsing.ReadResultCode(body)?.Trim();
        if (string.IsNullOrWhiteSpace(resultCode)
            || resultCode is "00" or "0" or "0000" or "NORMAL_SERVICE")
        {
            return;
        }

        var resultMessage = PublicDataParsing.ReadResultMessage(body)?.Trim();
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(resultMessage)
                ? $"수산업협동조합 공공데이터 응답 오류: {resultCode}"
                : $"수산업협동조합 공공데이터 응답 오류: {resultCode} {resultMessage}");
    }

    private string ResolveServiceKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.FishCooperativeStatistics.ServiceKey))
        {
            return _options.FishCooperativeStatistics.ServiceKey;
        }

        if (!string.IsNullOrWhiteSpace(_options.DataGoKrServiceKey))
        {
            return _options.DataGoKrServiceKey;
        }

        return _options.ServiceKey;
    }

    private sealed record PageResponse(
        IReadOnlyList<FishCooperativeGeneralStatisticsItem> Items,
        int? TotalCount,
        int ReturnedCount);
}
