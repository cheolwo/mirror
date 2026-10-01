using System.Globalization;
using Hongdal.Contracts.Common.PublicData;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace 홍달.Services.External.PublicData;

public sealed class AtDomesticFoodPriceLookupService : IAtDomesticFoodPriceLookupService
{
    private const string RetailCode = "01";
    private const string WholesaleCode = "02";

    private readonly HttpClient _httpClient;
    private readonly PublicDataOptions _options;

    public AtDomesticFoodPriceLookupService(
        HttpClient httpClient,
        IOptions<PublicDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AtDomesticFoodPriceLookupResult> LookupAsync(
        AtDomesticFoodPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryCode) || string.IsNullOrWhiteSpace(request.ItemCode))
        {
            return Fail(request, "aT 부류코드와 품목코드가 필요합니다.");
        }

        if (!TryNormalizeDate(request.StartDate, out var startDate)
            || !TryNormalizeDate(request.EndDate, out var endDate)
            || string.CompareOrdinal(startDate, endDate) > 0)
        {
            return Fail(request, "국내가격 조회기간을 yyyyMMdd 형식으로 확인해 주세요.");
        }

        var serviceKey = ResolveServiceKey();
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return Fail(request, "PublicData:AtFoodPrices:ServiceKey 또는 PublicData:DataGoKrServiceKey 설정이 필요합니다.");
        }

        var body = await ReadPriceBodyAsync(request, startDate, endDate, serviceKey, cancellationToken);
        var allowedVarieties = request.VarietyCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excludedTokens = request.ExcludedNameTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .ToArray();

        var observations = PublicDataParsing.ReadItems(body)
            .Select(ToObservation)
            .Where(item => item.PriceKrwPerKg > 0)
            .Where(item => allowedVarieties.Count == 0 || allowedVarieties.Contains(item.VarietyCode))
            .Where(item => !ContainsExcludedToken(item, excludedTokens))
            .ToArray();

        var wholesale = Aggregate(observations, WholesaleCode, "국내 중도매가격");
        var retail = Aggregate(observations, RetailCode, "국내 소매가격");
        if (wholesale is null && retail is null)
        {
            return Fail(request, "선택한 HS 품목에 대응하는 최근 국내가격 자료가 없습니다.", startDate, endDate);
        }

        return new AtDomesticFoodPriceLookupResult
        {
            Success = true,
            CategoryCode = request.CategoryCode,
            ItemCode = request.ItemCode,
            ItemName = observations.Select(item => item.ItemName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty,
            StartDate = startDate,
            EndDate = endDate,
            Wholesale = wholesale,
            Retail = retail
        };
    }

    private async Task<string> ReadPriceBodyAsync(
        AtDomesticFoodPriceRequest request,
        string startDate,
        string endDate,
        string serviceKey,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["serviceKey"] = serviceKey,
            ["pageNo"] = "1",
            ["numOfRows"] = "1000",
            ["cond[exmn_ymd::GTE]"] = startDate,
            ["cond[exmn_ymd::LTE]"] = endDate,
            ["cond[ctgry_cd::EQ]"] = request.CategoryCode.Trim(),
            ["cond[item_cd::EQ]"] = request.ItemCode.Trim(),
            ["returnType"] = "JSON"
        };
        var relative = QueryHelpers.AddQueryString(
            _options.AtFoodPrices.DailyPricePath.TrimStart('/'),
            query);

        using var response = await _httpClient.GetAsync(relative, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"aT domestic price request failed. HTTP {(int)response.StatusCode}");
        }

        return body;
    }

    private static AtPriceObservation ToObservation(Dictionary<string, string?> source)
        => new(
            NormalizeDate(PublicDataParsing.FirstValue(source, "exmn_ymd", "surveyDate")),
            PublicDataParsing.FirstValue(source, "se_cd", "priceTypeCode") ?? string.Empty,
            PublicDataParsing.FirstValue(source, "item_nm", "itemName") ?? string.Empty,
            PublicDataParsing.FirstValue(source, "vrty_cd", "varietyCode") ?? string.Empty,
            PublicDataParsing.FirstValue(source, "vrty_nm", "varietyName") ?? string.Empty,
            PublicDataParsing.FirstValue(source, "grd_nm", "gradeName") ?? string.Empty,
            PublicDataParsing.FirstValue(source, "mrkt_nm", "marketName") ?? string.Empty,
            PublicDataParsing.FirstDecimal(source, "exmn_dd_cnvs_prc", "convertedPricePerKg") ?? 0m);

    private static AtDomesticFoodPriceAggregate? Aggregate(
        IReadOnlyList<AtPriceObservation> observations,
        string priceTypeCode,
        string priceTypeLabel)
    {
        var candidates = observations
            .Where(item => string.Equals(item.PriceTypeCode, priceTypeCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var latestDate = candidates
            .Select(item => item.SurveyDate)
            .Where(date => !string.IsNullOrWhiteSpace(date))
            .OrderByDescending(date => date, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(latestDate))
        {
            return null;
        }

        var latestPrices = candidates
            .Where(item => string.Equals(item.SurveyDate, latestDate, StringComparison.Ordinal))
            .Select(item => item.PriceKrwPerKg)
            .Where(price => price > 0)
            .ToArray();
        if (latestPrices.Length == 0)
        {
            return null;
        }

        return new AtDomesticFoodPriceAggregate
        {
            PriceTypeCode = priceTypeCode,
            PriceTypeLabel = priceTypeLabel,
            LatestSurveyDate = latestDate,
            AverageKrwPerKg = decimal.Round(latestPrices.Average(), 0, MidpointRounding.AwayFromZero),
            MinimumKrwPerKg = latestPrices.Min(),
            MaximumKrwPerKg = latestPrices.Max(),
            SampleCount = latestPrices.Length
        };
    }

    private static bool ContainsExcludedToken(AtPriceObservation item, IReadOnlyList<string> excludedTokens)
    {
        if (excludedTokens.Count == 0)
        {
            return false;
        }

        var searchable = string.Join(' ', item.ItemName, item.VarietyName, item.GradeName, item.MarketName);
        return excludedTokens.Any(token => searchable.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveServiceKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.AtFoodPrices.ServiceKey))
        {
            return _options.AtFoodPrices.ServiceKey;
        }

        if (!string.IsNullOrWhiteSpace(_options.DataGoKrServiceKey))
        {
            return _options.DataGoKrServiceKey;
        }

        return _options.ServiceKey;
    }

    private static bool TryNormalizeDate(string? value, out string normalized)
    {
        normalized = NormalizeDate(value);
        return DateTime.TryParseExact(
            normalized,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static string NormalizeDate(string? value)
        => new((value ?? string.Empty).Where(char.IsDigit).Take(8).ToArray());

    private static AtDomesticFoodPriceLookupResult Fail(
        AtDomesticFoodPriceRequest request,
        string message,
        string? startDate = null,
        string? endDate = null)
        => new()
        {
            Success = false,
            ErrorMessage = message,
            CategoryCode = request.CategoryCode,
            ItemCode = request.ItemCode,
            StartDate = startDate ?? NormalizeDate(request.StartDate),
            EndDate = endDate ?? NormalizeDate(request.EndDate)
        };

    private sealed record AtPriceObservation(
        string SurveyDate,
        string PriceTypeCode,
        string ItemName,
        string VarietyCode,
        string VarietyName,
        string GradeName,
        string MarketName,
        decimal PriceKrwPerKg);
}
