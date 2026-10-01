using Hongdal.Contracts.Common.Customs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace 홍달.Services.External.PublicData;

public sealed class 세관장확인대상물품공공데이터수집기 : IHs공공데이터수집기
{
    private const string DocumentationUrl = "https://www.data.go.kr/data/15101589/openapi.do";
    private readonly HttpClient _httpClient;
    private readonly PublicDataOptions _options;

    public 세관장확인대상물품공공데이터수집기(
        HttpClient httpClient,
        IOptions<PublicDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string SourceKey => Hs공공데이터출처Keys.세관장확인대상물품;

    public async Task<Hs공공데이터출처응답> 수집Async(
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken = default)
    {
        if (request.HsCode.Length != 10)
        {
            return Response(
                Hs공공데이터수집상태Codes.적용안됨,
                "세관장확인대상물품 조회에는 10자리 HSK 코드가 필요합니다.");
        }

        var serviceKey = ResolveServiceKey();
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return Response(
                Hs공공데이터수집상태Codes.설정안됨,
                "공공데이터포털 인증키가 설정되지 않아 수입요건을 조회하지 못했습니다.");
        }

        var relativeUrl = QueryHelpers.AddQueryString(
            _options.CustomsRequirements.LookupPath.TrimStart('/'),
            new Dictionary<string, string?>
            {
                ["serviceKey"] = serviceKey,
                ["hsSgn"] = request.HsCode,
                ["imexTpcd"] = "2"
            });
        using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"KCS customs requirements request failed. HTTP {(int)response.StatusCode}");
        }

        var resultCode = PublicDataParsing.ReadResultCode(body);
        if (!IsSuccessfulResultCode(resultCode))
        {
            return Response(
                Hs공공데이터수집상태Codes.오류,
                $"관세청 수입요건 조회가 실패했습니다. {PublicDataParsing.ReadResultMessage(body) ?? resultCode}".Trim());
        }

        var items = PublicDataParsing.ReadItems(body)
            .Select(Map)
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) || item.Fields.Count > 0)
            .GroupBy(item => item.ItemKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (items.Length == 0)
        {
            return Response(
                Hs공공데이터수집상태Codes.데이터없음,
                "해당 HSK 코드의 세관장확인대상물품 조회 결과가 없습니다. 요건 면제를 뜻하지는 않습니다.");
        }

        return Response(
            Hs공공데이터수집상태Codes.성공,
            $"관세청에서 확인 법령·승인기관·구비요건 {items.Length}건을 조회했습니다.",
            items);
    }

    private static Hs공공데이터정보항목 Map(Dictionary<string, string?> source)
    {
        var hsCode = Value(source, "hsSgn", "hsCd") ?? string.Empty;
        var lawCode = Value(source, "dcerCfrmLworCd") ?? string.Empty;
        var lawName = Value(source, "dcerCfrmLworNm") ?? "확인 법령";
        var agencyCode = Value(source, "reqApreIttCd") ?? string.Empty;
        var agencyName = Value(source, "reqApreIttNm") ?? "승인기관 확인 필요";
        var requirement = Value(source, "reqCfrmIstmNm") ?? "구비요건 확인 필요";
        var startDate = Value(source, "aplyStrtDt") ?? string.Empty;

        return new Hs공공데이터정보항목
        {
            ItemKey = string.Join(':', hsCode, lawCode, agencyCode, startDate),
            Title = $"{lawName} · {agencyName}",
            Summary = requirement,
            AttentionRequired = true,
            Fields = new Dictionary<string, string?>
            {
                ["hsCode"] = hsCode,
                ["declarantConfirmationLawCode"] = lawCode,
                ["declarantConfirmationLawName"] = lawName,
                ["approvalAgencyCode"] = agencyCode,
                ["approvalAgencyName"] = agencyName,
                ["requiredConfirmationDocument"] = requirement,
                ["applicationStartDate"] = startDate,
                ["beforeAfterClearanceTypeCode"] = Value(source, "bfhnAffcRtmTpcd")
            }
        };
    }

    private string ResolveServiceKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.CustomsRequirements.ServiceKey))
        {
            return _options.CustomsRequirements.ServiceKey;
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
            SourceKey = Hs공공데이터출처Keys.세관장확인대상물품,
            Provider = "관세청",
            DisplayName = "세관장확인대상물품",
            StatusCode = statusCode,
            Summary = summary,
            DocumentationUrl = DocumentationUrl,
            CollectedAtUtc = DateTime.UtcNow,
            Items = items ?? []
        };
}
