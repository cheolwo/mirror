using Hongdal.Contracts.Common.AgriculturalFisheries;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed class AbsConsumerPriceIndex식품가격공급자 : I호주농수산식품가격공급자
{
    private const string OriginalSeriesCode = "10";
    private const string MonthlyFrequencyCode = "M";

    private readonly HttpClient _httpClient;
    private readonly PublicDataOptions _options;

    public AbsConsumerPriceIndex식품가격공급자(
        HttpClient httpClient,
        IOptions<PublicDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string SourceKey => 호주농수산식품가격출처Keys.AbsConsumerPriceIndex;

    public string ProviderName => "Australian Bureau of Statistics (ABS)";

    public string DocumentationUrl =>
        "https://www.abs.gov.au/statistics/application-programming-interfaces-apis/data-api-user-guide";

    public async Task<호주농수산식품가격조회응답> 조회Async(
        호주농수산식품가격조회요청 request,
        CancellationToken cancellationToken = default)
    {
        var normalized = 호주농수산식품가격조회요청Rules.Normalize(request);
        var validationError = 호주농수산식품가격조회요청Rules.Validate(normalized);
        if (validationError is not null)
        {
            return Fail(normalized, 호주농수산식품가격조회상태Codes.잘못된요청, validationError);
        }

        using var response = await _httpClient.GetAsync(
            BuildRequestPath(normalized),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Fail(
                normalized,
                호주농수산식품가격조회상태Codes.자료조회불가,
                $"ABS 식품 가격지수 조회에 실패했습니다. HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var readResult = await AbsConsumerPriceIndexSdmxReader.ReadAsync(
            stream,
            normalized,
            cancellationToken);
        if (!readResult.IsValid)
        {
            return Fail(
                normalized,
                호주농수산식품가격조회상태Codes.자료조회불가,
                readResult.ErrorMessage
                ?? "ABS가 현재 식품 가격지수 조회 조건을 처리하지 못했습니다.");
        }

        var allItems = readResult.Items;
        var items = allItems.Take(normalized.MaxItems).ToArray();
        var truncated = allItems.Count > items.Length;

        return Success(
            normalized,
            allItems.Count == 0
                ? 호주농수산식품가격조회상태Codes.자료없음
                : 호주농수산식품가격조회상태Codes.완료,
            items,
            allItems.Count,
            truncated,
            readResult.SourcePreparedAtUtc,
            allItems.Count == 0
                ? "조회 조건에 맞는 ABS 식품 가격지수 자료가 없습니다."
                : truncated
                    ? $"ABS 식품 가격지수 {allItems.Count:N0}건 중 {items.Length:N0}건을 제공합니다."
                    : $"ABS 식품 가격지수 {items.Length:N0}건을 제공합니다.");
    }

    private string BuildRequestPath(호주농수산식품가격조회요청 request)
    {
        var seriesKey = string.Join(
            '.',
            request.MeasureCode,
            request.IndexCode,
            OriginalSeriesCode,
            request.RegionCode,
            MonthlyFrequencyCode);
        var path = $"{_options.AbsConsumerPriceIndex.DataPath.Trim('/')}/{seriesKey}";
        return QueryHelpers.AddQueryString(
            path,
            new Dictionary<string, string?>
            {
                ["startPeriod"] = request.StartPeriod,
                ["endPeriod"] = request.EndPeriod,
                ["dimensionAtObservation"] = "TIME_PERIOD",
                ["format"] = "jsondata"
            });
    }

    private 호주농수산식품가격조회응답 Success(
        호주농수산식품가격조회요청 request,
        string statusCode,
        IReadOnlyList<호주농수산식품가격항목> items,
        int totalCount,
        bool isTruncated,
        DateTime? sourcePreparedAtUtc,
        string summary)
        => new()
        {
            Success = statusCode is 호주농수산식품가격조회상태Codes.완료
                or 호주농수산식품가격조회상태Codes.자료없음,
            StatusCode = statusCode,
            SourceKey = SourceKey,
            Provider = ProviderName,
            DocumentationUrl = DocumentationUrl,
            Query = request,
            Items = items,
            TotalCount = totalCount,
            IsTruncated = isTruncated,
            SourcePreparedAtUtc = sourcePreparedAtUtc?.ToUniversalTime(),
            CollectedAtUtc = DateTime.UtcNow,
            Summary = summary,
            Notices = BuildNotices()
        };

    private 호주농수산식품가격조회응답 Fail(
        호주농수산식품가격조회요청 request,
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
            "Based on Australian Bureau of Statistics data.",
            "ABS CPI는 실제 A$/kg 또는 개별 상품 단가가 아니라 소비자 가격 변동을 나타내는 지수입니다.",
            "주도시 지수는 각 도시의 시간에 따른 변화를 보여 주며 도시 간 절대 소매가격 수준을 비교하지 않습니다.",
            "공동구매 매입가·운송비·관세·도착원가와 직접 같은 값으로 사용하지 않습니다.",
            "ABS Data API는 Beta 서비스이므로 데이터흐름과 응답 계약을 주기적으로 재검증합니다."
        ];
}
