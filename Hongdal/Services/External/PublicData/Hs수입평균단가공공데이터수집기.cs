using System.Globalization;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.PublicData;

namespace 홍달.Services.External.PublicData;

public sealed class Hs수입평균단가공공데이터수집기 : IHs공공데이터수집기
{
    private const string DocumentationUrl = "https://www.data.go.kr/data/15100475/openapi.do";
    private readonly IHsCountryTradeUnitPriceLookupService _lookupService;

    public Hs수입평균단가공공데이터수집기(IHsCountryTradeUnitPriceLookupService lookupService)
    {
        _lookupService = lookupService;
    }

    public string SourceKey => Hs공공데이터출처Keys.수입평균단가;

    public async Task<Hs공공데이터출처응답> 수집Async(
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CountryCode))
        {
            return Response(
                Hs공공데이터수집상태Codes.적용안됨,
                "국가별 수입단가를 조회하려면 2자리 국가부호가 필요합니다.");
        }

        var result = await _lookupService.SimulateImportUnitPriceAsync(
            new HsCountryMonthlyTradeUnitPriceRequest
            {
                HsCode = request.HsCode,
                CountryCode = request.CountryCode,
                Month = request.ReferenceMonth,
                LookbackMonths = request.LookbackMonths,
                ExpectedFxRateKrwPerUsd = request.ExpectedFxRateKrwPerUsd
            },
            cancellationToken);

        if (!result.Success)
        {
            var statusCode = ResolveFailureStatus(result.ErrorMessage);
            var summary = statusCode switch
            {
                Hs공공데이터수집상태Codes.설정안됨 => "공공데이터포털 인증키가 설정되지 않아 수입단가를 조회하지 못했습니다.",
                Hs공공데이터수집상태Codes.데이터없음 => "해당 HS 코드와 국가의 수입실적을 조회 기간에서 찾지 못했습니다.",
                _ => "관세청 수입실적 조회에 실패했습니다."
            };
            return Response(statusCode, summary);
        }

        var averageUsd = result.AverageImportUnitValueUsdPerKg;
        var averageKrw = result.AverageImportUnitValueKrwPerKg;
        var summaryText = averageUsd.HasValue
            ? $"{result.StartMonth}~{result.EndMonth} 수입금액과 중량의 가중평균은 kg당 USD {averageUsd.Value:N2}입니다."
            : "수입실적은 있으나 중량이 없어 kg당 평균단가를 계산하지 못했습니다.";
        if (averageKrw.HasValue)
        {
            summaryText += $" 입력한 환율 기준 약 {averageKrw.Value:N0}원/kg입니다.";
        }

        var fields = new Dictionary<string, string?>
        {
            ["hsCode"] = result.HsCode,
            ["countryCode"] = result.CountryCode,
            ["startMonth"] = result.StartMonth,
            ["endMonth"] = result.EndMonth,
            ["totalImportWeightKg"] = Format(result.TotalImportWeightKg),
            ["totalImportValueUsd"] = Format(result.TotalImportValueUsd),
            ["averageImportUnitValueUsdPerKg"] = Format(averageUsd),
            ["averageImportUnitValueKrwPerKg"] = Format(averageKrw),
            ["priceSignalCode"] = result.PriceSignalCode
        };

        return Response(
            Hs공공데이터수집상태Codes.성공,
            summaryText,
            [
                new Hs공공데이터정보항목
                {
                    ItemKey = $"{result.HsCode}:{result.CountryCode}:{result.EndMonth}",
                    Title = "국가별 수입 가중평균 단가",
                    Summary = "수입금액 합계를 수입중량 합계로 나눈 CIF 통계 참고값입니다.",
                    Fields = fields
                }
            ]);
    }

    private static string ResolveFailureStatus(string? errorMessage)
    {
        if (errorMessage?.Contains("required", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Hs공공데이터수집상태Codes.설정안됨;
        }

        if (errorMessage?.Contains("No import statistics", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Hs공공데이터수집상태Codes.데이터없음;
        }

        return Hs공공데이터수집상태Codes.오류;
    }

    private static Hs공공데이터출처응답 Response(
        string statusCode,
        string summary,
        IReadOnlyList<Hs공공데이터정보항목>? items = null)
        => new()
        {
            SourceKey = Hs공공데이터출처Keys.수입평균단가,
            Provider = "관세청",
            DisplayName = "품목별 국가별 수입실적",
            StatusCode = statusCode,
            Summary = summary,
            DocumentationUrl = DocumentationUrl,
            CollectedAtUtc = DateTime.UtcNow,
            Items = items ?? []
        };

    private static string? Format(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture);
}
