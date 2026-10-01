using System.Globalization;
using Hongdal.Contracts.Common.Customs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace 홍달.Services.External.PublicData;

public sealed class 관세환율공공데이터수집기 : IHs공공데이터수집기
{
    private const string DocumentationUrl = "https://www.data.go.kr/data/15101230/openapi.do";
    private readonly HttpClient _httpClient;
    private readonly PublicDataOptions _options;

    public 관세환율공공데이터수집기(
        HttpClient httpClient,
        IOptions<PublicDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string SourceKey => Hs공공데이터출처Keys.관세환율;

    public async Task<Hs공공데이터출처응답> 수집Async(
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken = default)
    {
        if (request.CountryCode.Length != 2)
        {
            return Response(
                Hs공공데이터수집상태Codes.적용안됨,
                "관세환율을 조회하려면 ISO 2자리 국가부호가 필요합니다.");
        }

        var serviceKey = ResolveServiceKey();
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return Response(
                Hs공공데이터수집상태Codes.설정안됨,
                "공공데이터포털 인증키가 설정되지 않아 관세환율을 조회하지 못했습니다.");
        }

        var relativeUrl = QueryHelpers.AddQueryString(
            _options.CustomsExchangeRate.LookupPath.TrimStart('/'),
            new Dictionary<string, string?>
            {
                ["serviceKey"] = serviceKey,
                ["aplyBgnDt"] = request.ReferenceDate,
                ["weekFxrtTpcd"] = "2"
            });
        using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"KCS customs exchange-rate request failed. HTTP {(int)response.StatusCode}");
        }

        var resultCode = PublicDataParsing.ReadResultCode(body);
        if (!IsSuccessfulResultCode(resultCode))
        {
            return Response(
                Hs공공데이터수집상태Codes.오류,
                $"관세청 관세환율 조회가 실패했습니다. {PublicDataParsing.ReadResultMessage(body) ?? resultCode}".Trim());
        }

        var items = PublicDataParsing.ReadItems(body)
            .Where(source => string.Equals(
                Value(source, "cntySgn", "countryCode"),
                request.CountryCode,
                StringComparison.OrdinalIgnoreCase))
            .Select(Map)
            .Where(item => item is not null)
            .Cast<Hs공공데이터정보항목>()
            .ToArray();
        if (items.Length == 0)
        {
            return Response(
                Hs공공데이터수집상태Codes.데이터없음,
                $"{request.CountryCode} 국가의 {request.ReferenceDate} 적용 관세환율을 찾지 못했습니다.");
        }

        return Response(
            Hs공공데이터수집상태Codes.성공,
            $"{request.CountryCode} 국가의 수입 관세환율 {items.Length}건을 조회했습니다.",
            items);
    }

    private static Hs공공데이터정보항목? Map(Dictionary<string, string?> source)
    {
        var rate = PublicDataParsing.FirstDecimal(source, "fxrt", "exchangeRate");
        if (!rate.HasValue)
        {
            return null;
        }

        var countryCode = Value(source, "cntySgn", "countryCode") ?? string.Empty;
        var currencyCode = Value(source, "currSgn", "currencyCode") ?? string.Empty;
        var currencyName = Value(source, "mtryUtNm", "currencyName") ?? currencyCode;
        var applicationStartDate = Value(source, "aplyBgnDt", "applicationStartDate") ?? string.Empty;

        return new Hs공공데이터정보항목
        {
            ItemKey = string.Join(':', countryCode, currencyCode, applicationStartDate),
            Title = $"{currencyName} ({currencyCode})",
            Summary = $"관세청 수입 과세환율 {rate.Value.ToString(CultureInfo.InvariantCulture)}",
            Fields = new Dictionary<string, string?>
            {
                ["countryCode"] = countryCode,
                ["currencyCode"] = currencyCode,
                ["currencyName"] = currencyName,
                ["exchangeRate"] = rate.Value.ToString(CultureInfo.InvariantCulture),
                ["applicationStartDate"] = applicationStartDate,
                ["importExportType"] = Value(source, "imexTp")
            }
        };
    }

    private string ResolveServiceKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.CustomsExchangeRate.ServiceKey))
        {
            return _options.CustomsExchangeRate.ServiceKey;
        }

        return !string.IsNullOrWhiteSpace(_options.DataGoKrServiceKey)
            ? _options.DataGoKrServiceKey
            : _options.ServiceKey;
    }

    private static string? Value(Dictionary<string, string?> source, params string[] names)
        => PublicDataParsing.FirstValue(source, names);

    private static bool IsSuccessfulResultCode(string? resultCode)
        => string.IsNullOrWhiteSpace(resultCode)
            || string.Equals(resultCode, "00", StringComparison.OrdinalIgnoreCase)
            || string.Equals(resultCode, "0", StringComparison.OrdinalIgnoreCase);

    private static Hs공공데이터출처응답 Response(
        string statusCode,
        string summary,
        IReadOnlyList<Hs공공데이터정보항목>? items = null)
        => new()
        {
            SourceKey = Hs공공데이터출처Keys.관세환율,
            Provider = "관세청",
            DisplayName = "관세환율정보",
            StatusCode = statusCode,
            Summary = summary,
            DocumentationUrl = DocumentationUrl,
            CollectedAtUtc = DateTime.UtcNow,
            Items = items ?? []
        };
}
