using System.Globalization;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.PublicData;

namespace Hongdal.Ui.Common.Areas.App.Services;

public static class 공동구매가격의사결정유형코드
{
    public const string 국내공동구매 = "domestic-group-purchase";
    public const string 공동수입 = "group-import";
}

public static class 공동구매가격판단신호코드
{
    public const string 제안가격경쟁력 = "proposal-price-advantage";
    public const string 시장가유사 = "similar-to-market";
    public const string 제안가격주의 = "proposal-price-caution";
    public const string 원가여유참고 = "cost-spread-reference";
    public const string 원가미달위험 = "below-reference-cost";
}

public sealed record 공동구매가격의사결정요청(
    string 유형코드,
    string HS코드,
    decimal 제안단가KrwPerKg,
    string 수출국가코드 = "",
    int 국내조회기간일수 = 14,
    int 수입조회개월수 = 3,
    decimal? 가정환율KrwPerUsd = null,
    decimal? 추가수입비용KrwPerKg = null,
    string 해외공공가격품목명 = "",
    string 해외공공가격프로그램 = "SURVEY",
    int 해외가격시작연도 = 0,
    int 해외가격종료연도 = 0);

public sealed record 공동구매가격기준비교(
    string 기준코드,
    string 기준명,
    string 자료출처,
    decimal 기준가격KrwPerKg,
    decimal 제안단가KrwPerKg,
    decimal 차이KrwPerKg,
    decimal 차이율,
    string 신호코드,
    string 신호명,
    string 안내);

public sealed class 공동구매가격의사결정결과
{
    public bool 자료있음 { get; init; }
    public string 상태코드 { get; init; } = "Unavailable";
    public string 유형코드 { get; init; } = string.Empty;
    public string HS코드 { get; init; } = string.Empty;
    public decimal 제안단가KrwPerKg { get; init; }
    public AtDomesticFoodPriceLookupResult? 국내시장가격 { get; init; }
    public FoodPriceComparisonResponse? 국내수입가격비교 { get; init; }
    public HsCountryImportUnitPriceSimulationResult? 수입평균단가 { get; init; }
    public 미국농수산가격조회응답? 해외공공가격 { get; init; }
    public IReadOnlyList<공동구매가격기준비교> 기준비교목록 { get; init; } = [];
    public string 요약 { get; init; } = string.Empty;
    public IReadOnlyList<string> 주의사항 { get; init; } = [];
    public DateTimeOffset 조회시각 { get; init; }
}

public interface I공동구매가격의사결정Service
{
    Task<공동구매가격의사결정결과> 조회Async(
        공동구매가격의사결정요청 request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 공동구매 제안단가를 국내 aT 도·소매가 및 관세청 수입 평균단가와 비교합니다.
/// 해외 공공가격은 통화·단위가 일치하지 않을 수 있어 원문 참고자료로만 함께 제공합니다.
/// </summary>
public sealed class 공동구매가격의사결정Service(
    I농수산공공데이터Client publicDataClient) : I공동구매가격의사결정Service
{
    private const decimal 유사판정범위 = 0.05m;

    public async Task<공동구매가격의사결정결과> 조회Async(
        공동구매가격의사결정요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hsCode = 숫자만(request.HS코드);
        if (request.유형코드 is not 공동구매가격의사결정유형코드.국내공동구매
            and not 공동구매가격의사결정유형코드.공동수입)
        {
            throw new ArgumentException("지원하는 공동구매 가격 의사결정 유형이 아닙니다.", nameof(request));
        }

        if (hsCode.Length is < 4 or > 10)
        {
            throw new ArgumentException("가격 조회용 HS 코드는 4~10자리 숫자여야 합니다.", nameof(request));
        }

        if (request.제안단가KrwPerKg <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "비교할 공동구매 제안단가는 0원/kg보다 커야 합니다.");
        }

        var countryCode = string.Empty;
        if (request.유형코드 == 공동구매가격의사결정유형코드.공동수입)
        {
            countryCode = 국가코드정규화(request.수출국가코드);
            if (countryCode.Length is < 2 or > 3)
            {
                throw new ArgumentException("공동수입 가격 조회에는 영문 2~3자리 수출국 코드가 필요합니다.", nameof(request));
            }
        }

        var overseasTask = 해외공공가격조회Async(request, cancellationToken);
        AgriculturalFisheriesDomesticPriceResponse? domesticResponse = null;
        FoodPriceComparisonResponse? importResponse = null;
        HsCountryImportUnitPriceSimulationResult? importUnitPrice = null;

        if (request.유형코드 == 공동구매가격의사결정유형코드.공동수입)
        {
            importResponse = await 안전한수입가격비교Async(
                new FoodPriceComparisonRequest
                {
                    HsCode = hsCode,
                    CountryCode = countryCode,
                    DomesticLookbackDays = Math.Clamp(request.국내조회기간일수, 1, 31),
                    ImportLookbackMonths = Math.Clamp(request.수입조회개월수, 1, 12),
                    FxRateKrwPerUsd = request.가정환율KrwPerUsd,
                    EstimatedImportAdditionalCostKrwPerKg = request.추가수입비용KrwPerKg
                },
                cancellationToken);
            if (importResponse.ImportPrice is null)
            {
                importUnitPrice = await 안전한수입평균단가조회Async(
                    new HsCountryMonthlyTradeUnitPriceRequest
                    {
                        HsCode = hsCode,
                        CountryCode = countryCode,
                        Month = DateTimeOffset.Now.ToString("yyyyMM", CultureInfo.InvariantCulture),
                        LookbackMonths = Math.Clamp(request.수입조회개월수, 1, 12),
                        ExpectedFxRateKrwPerUsd = request.가정환율KrwPerUsd,
                        ExpectedDomesticLogisticsCostKrwPerKg = request.추가수입비용KrwPerKg,
                        ExpectedSellingUnitPriceKrwPerKg = request.제안단가KrwPerKg
                    },
                    cancellationToken);
            }
        }
        else
        {
            domesticResponse = await 안전한국내가격조회Async(
                hsCode,
                request.국내조회기간일수,
                cancellationToken);
        }

        var overseasResponse = await overseasTask;
        var domesticPrice = domesticResponse?.Price ?? importResponse?.DomesticPrice;
        var importReference = importResponse?.ImportPrice ?? 수입가격기준변환(importUnitPrice);
        var comparisons = new List<공동구매가격기준비교>();
        국내가격비교추가(comparisons, domesticPrice, request.제안단가KrwPerKg);
        수입가격비교추가(comparisons, importReference, request.제안단가KrwPerKg);

        var hasOverseasData = overseasResponse?.Items.Count > 0;
        var hasRequestedOverseasData = !string.IsNullOrWhiteSpace(request.해외공공가격품목명);
        var hasMainData = comparisons.Count > 0
            || domesticPrice?.Success == true
            || importResponse?.ImportPrice is not null
            || importUnitPrice?.Success == true;
        var statusCode = hasMainData && (!hasRequestedOverseasData || hasOverseasData)
            ? "Complete"
            : hasMainData || hasOverseasData
                ? "Partial"
                : "Unavailable";

        return new 공동구매가격의사결정결과
        {
            자료있음 = hasMainData || hasOverseasData,
            상태코드 = statusCode,
            유형코드 = request.유형코드,
            HS코드 = hsCode,
            제안단가KrwPerKg = request.제안단가KrwPerKg,
            국내시장가격 = domesticPrice,
            국내수입가격비교 = importResponse,
            수입평균단가 = importUnitPrice,
            해외공공가격 = overseasResponse,
            기준비교목록 = comparisons,
            요약 = 요약생성(
                request,
                comparisons,
                importResponse,
                importUnitPrice,
                overseasResponse),
            주의사항 = 주의사항생성(
                request,
                domesticResponse,
                importResponse,
                importUnitPrice,
                overseasResponse),
            조회시각 = DateTimeOffset.Now
        };
    }

    private async Task<AgriculturalFisheriesDomesticPriceResponse> 안전한국내가격조회Async(
        string hsCode,
        int lookbackDays,
        CancellationToken cancellationToken)
    {
        try
        {
            return await publicDataClient.국내가격조회Async(
                hsCode,
                Math.Clamp(lookbackDays, 1, 31),
                cancellationToken);
        }
        catch (Exception ex) when (조회예외(ex, cancellationToken))
        {
            return new AgriculturalFisheriesDomesticPriceResponse
            {
                Success = false,
                StatusCode = "Unavailable",
                ErrorMessage = "국내 도·소매 가격정보를 불러오지 못했습니다.",
                HsCode = hsCode,
                Summary = "국내 가격 자료를 현재 확인할 수 없습니다."
            };
        }
    }

    private async Task<FoodPriceComparisonResponse> 안전한수입가격비교Async(
        FoodPriceComparisonRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await publicDataClient.식품가격비교Async(request, cancellationToken);
        }
        catch (Exception ex) when (조회예외(ex, cancellationToken))
        {
            return new FoodPriceComparisonResponse
            {
                Success = false,
                StatusCode = "Unavailable",
                ErrorMessage = "국내 가격과 수입 평균단가를 불러오지 못했습니다.",
                HsCode = request.HsCode,
                CountryCode = request.CountryCode,
                Summary = "가격 비교 자료를 현재 확인할 수 없습니다."
            };
        }
    }

    private async Task<HsCountryImportUnitPriceSimulationResult> 안전한수입평균단가조회Async(
        HsCountryMonthlyTradeUnitPriceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await publicDataClient.수입평균단가조회Async(request, cancellationToken);
        }
        catch (Exception ex) when (조회예외(ex, cancellationToken))
        {
            return new HsCountryImportUnitPriceSimulationResult
            {
                Success = false,
                ErrorMessage = "HS 코드·수출국 기준 수입 평균단가를 불러오지 못했습니다.",
                HsCode = request.HsCode,
                CountryCode = request.CountryCode
            };
        }
    }

    private async Task<미국농수산가격조회응답?> 해외공공가격조회Async(
        공동구매가격의사결정요청 request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.해외공공가격품목명))
        {
            return null;
        }

        var currentYear = DateTime.UtcNow.Year;
        var yearTo = request.해외가격종료연도 > 0
            ? request.해외가격종료연도
            : currentYear;
        var yearFrom = request.해외가격시작연도 > 0
            ? request.해외가격시작연도
            : yearTo - 3;
        try
        {
            return await publicDataClient.미국가격조회Async(
                request.해외공공가격품목명.Trim(),
                string.IsNullOrWhiteSpace(request.해외공공가격프로그램)
                    ? "SURVEY"
                    : request.해외공공가격프로그램.Trim(),
                Math.Min(yearFrom, yearTo),
                Math.Max(yearFrom, yearTo),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (조회예외(ex, cancellationToken))
        {
            return new 미국농수산가격조회응답
            {
                Success = false,
                StatusCode = 미국농수산가격조회상태Codes.자료조회불가,
                ErrorMessage = "해외 농수산 공공가격을 불러오지 못했습니다.",
                Summary = "해외 공공가격 자료를 현재 확인할 수 없습니다."
            };
        }
    }

    private static void 국내가격비교추가(
        ICollection<공동구매가격기준비교> comparisons,
        AtDomesticFoodPriceLookupResult? domestic,
        decimal proposalPrice)
    {
        if (domestic?.Retail is { AverageKrwPerKg: > 0 } retail)
        {
            comparisons.Add(시장가격비교(
                "domestic-retail",
                "국내 소매 평균가격",
                domestic.DataSource,
                retail.AverageKrwPerKg,
                proposalPrice));
        }

        if (domestic?.Wholesale is { AverageKrwPerKg: > 0 } wholesale)
        {
            comparisons.Add(시장가격비교(
                "domestic-wholesale",
                "국내 중도매 평균가격",
                domestic.DataSource,
                wholesale.AverageKrwPerKg,
                proposalPrice));
        }
    }

    private static 공동구매가격기준비교 시장가격비교(
        string code,
        string label,
        string source,
        decimal referencePrice,
        decimal proposalPrice)
    {
        var difference = decimal.Round(referencePrice - proposalPrice, 0, MidpointRounding.AwayFromZero);
        var rate = decimal.Round(difference / referencePrice, 4, MidpointRounding.AwayFromZero);
        var (signalCode, signalName, guidance) = rate switch
        {
            > 유사판정범위 => (
                공동구매가격판단신호코드.제안가격경쟁력,
                "제안가격이 낮음",
                $"공동구매 제안단가가 {label}보다 약 {rate:P0} 낮습니다."),
            < -유사판정범위 => (
                공동구매가격판단신호코드.제안가격주의,
                "제안가격이 높음",
                $"공동구매 제안단가가 {label}보다 약 {-rate:P0} 높습니다."),
            _ => (
                공동구매가격판단신호코드.시장가유사,
                "시장가와 유사",
                $"공동구매 제안단가와 {label}이 비슷한 수준입니다.")
        };

        return new 공동구매가격기준비교(
            code,
            label,
            source,
            referencePrice,
            proposalPrice,
            difference,
            rate,
            signalCode,
            signalName,
            guidance);
    }

    private static void 수입가격비교추가(
        ICollection<공동구매가격기준비교> comparisons,
        FoodImportPriceReference? import,
        decimal proposalPrice)
    {
        if (import?.AverageCifKrwPerKg is > 0)
        {
            comparisons.Add(수입원가비교(
                "import-average-cif",
                "수입 신고 CIF 평균단가",
                import.DataSource,
                import.AverageCifKrwPerKg.Value,
                proposalPrice));
        }

        if (import?.EstimatedLandedCostKrwPerKg is > 0)
        {
            comparisons.Add(수입원가비교(
                "import-estimated-landed-cost",
                "추정 국내 도착원가",
                import.DataSource,
                import.EstimatedLandedCostKrwPerKg.Value,
                proposalPrice));
        }
    }

    private static FoodImportPriceReference? 수입가격기준변환(
        HsCountryImportUnitPriceSimulationResult? import)
    {
        if (import is not { Success: true })
        {
            return null;
        }

        return new FoodImportPriceReference
        {
            StartMonth = import.StartMonth,
            EndMonth = import.EndMonth,
            TotalImportWeightKg = import.TotalImportWeightKg,
            AverageCifUsdPerKg = import.AverageImportUnitValueUsdPerKg,
            AverageCifKrwPerKg = import.AverageImportUnitValueKrwPerKg,
            EstimatedLandedCostKrwPerKg = import.ExpectedLandedCostKrwPerKg
        };
    }

    private static 공동구매가격기준비교 수입원가비교(
        string code,
        string label,
        string source,
        decimal referenceCost,
        decimal proposalPrice)
    {
        var spread = decimal.Round(proposalPrice - referenceCost, 0, MidpointRounding.AwayFromZero);
        var rate = decimal.Round(spread / proposalPrice, 4, MidpointRounding.AwayFromZero);
        var belowCost = spread < 0;
        return new 공동구매가격기준비교(
            code,
            label,
            source,
            referenceCost,
            proposalPrice,
            spread,
            rate,
            belowCost
                ? 공동구매가격판단신호코드.원가미달위험
                : 공동구매가격판단신호코드.원가여유참고,
            belowCost ? "기준원가 미달 위험" : "원가 차이 참고",
            belowCost
                ? $"제안단가가 {label}보다 약 {-spread:N0}원/kg 낮아 비용 조건을 다시 확인해야 합니다."
                : $"제안단가와 {label} 사이에 약 {spread:N0}원/kg 차이가 있습니다. 세금·검역·물류비 누락 여부를 확인해 주세요.");
    }

    private static string 요약생성(
        공동구매가격의사결정요청 request,
        IReadOnlyList<공동구매가격기준비교> comparisons,
        FoodPriceComparisonResponse? import,
        HsCountryImportUnitPriceSimulationResult? importUnitPrice,
        미국농수산가격조회응답? overseas)
    {
        var market = comparisons.FirstOrDefault(item => item.기준코드 == "domestic-retail")
            ?? comparisons.FirstOrDefault(item => item.기준코드 == "domestic-wholesale");
        var cost = comparisons.FirstOrDefault(item => item.기준코드 == "import-estimated-landed-cost")
            ?? comparisons.FirstOrDefault(item => item.기준코드 == "import-average-cif");
        var parts = new List<string>();
        if (market is not null)
        {
            parts.Add(market.안내);
        }

        if (request.유형코드 == 공동구매가격의사결정유형코드.공동수입)
        {
            if (cost is not null)
            {
                parts.Add(cost.안내);
            }

            if (!string.IsNullOrWhiteSpace(import?.Summary))
            {
                parts.Add(import.Summary);
            }

            if (import?.ImportPrice is null
                && !string.IsNullOrWhiteSpace(importUnitPrice?.Summary))
            {
                parts.Add(importUnitPrice.Summary);
            }
        }

        if (overseas?.Items.Count > 0)
        {
            parts.Add($"해외 공공가격 {overseas.Items.Count}건을 원문 단위로 함께 제공합니다.");
        }

        return parts.Count > 0
            ? string.Join(" ", parts)
            : "현재 조건에서 비교 가능한 가격 자료를 찾지 못했습니다.";
    }

    private static IReadOnlyList<string> 주의사항생성(
        공동구매가격의사결정요청 request,
        AgriculturalFisheriesDomesticPriceResponse? domestic,
        FoodPriceComparisonResponse? import,
        HsCountryImportUnitPriceSimulationResult? importUnitPrice,
        미국농수산가격조회응답? overseas)
    {
        var notices = new List<string>
        {
            "가격 자료는 의사결정 참고값이며 품질·등급·산지·포장·신선도와 거래 조건을 함께 확인해야 합니다.",
            "제안가격 비교는 모든 가격을 원/kg 기준으로 맞춘 경우에만 의미가 있습니다."
        };
        notices.AddRange(domestic?.Notices ?? []);
        notices.AddRange(import?.Notices ?? []);
        notices.AddRange(overseas?.Notices ?? []);
        if (request.유형코드 == 공동구매가격의사결정유형코드.공동수입
            && request.가정환율KrwPerUsd is > 0)
        {
            notices.Add($"수입단가 원화 환산에는 사용자가 확인할 가정 환율 {request.가정환율KrwPerUsd.Value:N0}원/USD를 적용했습니다.");
        }

        if (importUnitPrice is { Success: true })
        {
            notices.Add("수입 평균단가는 관세청 CIF 신고금액을 순중량으로 나눈 통계값이며 개별 판매자 견적이 아닙니다.");
            if (importUnitPrice.ExpectedLandedCostKrwPerKg is not > 0)
            {
                notices.Add("CIF 평균단가에는 관세·부가세·검역·통관·국내 물류비와 판매마진이 포함되지 않았습니다.");
            }
        }

        if (overseas is not null)
        {
            notices.Add("해외 공공가격은 통화와 단위가 다를 수 있어 국내 가격이나 제안가격과 자동 환산 비교하지 않습니다.");
        }

        return notices
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool 조회예외(Exception ex, CancellationToken cancellationToken)
        => !cancellationToken.IsCancellationRequested
           && ex is HttpRequestException
               or System.Text.Json.JsonException
               or TaskCanceledException
               or InvalidOperationException;

    private static string 숫자만(string value)
        => new(value.Where(char.IsDigit).ToArray());

    private static string 국가코드정규화(string value)
        => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
