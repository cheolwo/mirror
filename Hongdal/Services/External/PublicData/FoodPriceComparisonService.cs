using System.Globalization;
using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace 홍달.Services.External.PublicData;

public sealed class FoodPriceComparisonService : IFoodPriceComparisonService
{
    private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);

    private readonly IAgriculturalFisheriesInformationService _informationService;
    private readonly IHsCountryTradeUnitPriceLookupService _importPriceLookupService;
    private readonly PublicDataOptions _options;

    public FoodPriceComparisonService(
        IAgriculturalFisheriesInformationService informationService,
        IHsCountryTradeUnitPriceLookupService importPriceLookupService,
        IOptions<PublicDataOptions> options)
    {
        _informationService = informationService;
        _importPriceLookupService = importPriceLookupService;
        _options = options.Value;
    }

    public async Task<FoodPriceComparisonResponse> CompareAsync(
        FoodPriceComparisonRequest request,
        CancellationToken cancellationToken = default)
    {
        var hsCode = NormalizeHsCode(request.HsCode);
        var countryCode = NormalizeCountryCode(request.CountryCode);
        if (hsCode.Length < 4)
        {
            return Invalid(request, hsCode, countryCode, "HS 코드를 4자리 이상 입력해 주세요.");
        }

        if (countryCode.Length is < 2 or > 3)
        {
            return Invalid(request, hsCode, countryCode, "수출국 코드를 영문 2~3자로 입력해 주세요.");
        }

        if (!TryResolveReferenceDate(request.ReferenceDate, out var referenceDate))
        {
            return Invalid(request, hsCode, countryCode, "국내가격 기준일을 yyyyMMdd 형식으로 확인해 주세요.");
        }

        var referenceMonth = ResolveReferenceMonth(request.ReferenceMonth, referenceDate);
        if (referenceMonth is null)
        {
            return Invalid(request, hsCode, countryCode, "수입통계 기준월을 yyyyMM 형식으로 확인해 주세요.");
        }

        var item = _informationService.FindItem(hsCode);
        if (item is null)
        {
            return new FoodPriceComparisonResponse
            {
                Success = false,
                StatusCode = "MappingRequired",
                ErrorMessage = "현재 자동 가격비교 품목에 아직 연결되지 않은 HS 코드입니다.",
                HsCode = hsCode,
                CountryCode = countryCode,
                Summary = "국내 가격 품목코드를 연결한 뒤 비교할 수 있습니다.",
                Notices =
                [
                    "HS 식품 분류와 국내 가격 조사 품목은 코드체계가 달라 검토된 연결표가 필요합니다."
                ]
            };
        }

        var lookbackDays = Math.Clamp(request.DomesticLookbackDays <= 0 ? 14 : request.DomesticLookbackDays, 1, 31);
        var fxRate = request.FxRateKrwPerUsd is > 0
            ? request.FxRateKrwPerUsd.Value
            : _options.AtFoodPrices.DefaultSimulationFxRateKrwPerUsd;

        var domesticTask = _informationService.GetDomesticPriceAsync(
            new AgriculturalFisheriesDomesticPriceRequest
            {
                HsCode = hsCode,
                ReferenceDate = referenceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                LookbackDays = lookbackDays
            },
            cancellationToken);
        var importTask = LookupImportSafelyAsync(
            new HsCountryMonthlyTradeUnitPriceRequest
            {
                HsCode = hsCode,
                CountryCode = countryCode,
                Month = referenceMonth,
                LookbackMonths = Math.Clamp(request.ImportLookbackMonths <= 0 ? 3 : request.ImportLookbackMonths, 1, 12),
                ExpectedFxRateKrwPerUsd = fxRate > 0 ? fxRate : null,
                ExpectedDomesticLogisticsCostKrwPerKg = request.EstimatedImportAdditionalCostKrwPerKg
            },
            cancellationToken);

        await Task.WhenAll(domesticTask, importTask);
        var domesticInformation = await domesticTask;
        var domestic = domesticInformation.Price ?? new AtDomesticFoodPriceLookupResult
        {
            Success = false,
            ErrorMessage = domesticInformation.ErrorMessage ?? "국내가격을 확인하지 못했습니다.",
            CategoryCode = item.CategoryCode,
            ItemCode = item.AtItemCode
        };
        var import = await importTask;
        var importReference = MapImportReference(import, request, fxRate);
        var importComparisonPrice = request.EstimatedImportAdditionalCostKrwPerKg is > 0
            ? importReference?.EstimatedLandedCostKrwPerKg
            : importReference?.AverageCifKrwPerKg;
        var comparisonPriceLabel = request.EstimatedImportAdditionalCostKrwPerKg is > 0
            ? "추정 국내 도착원가"
            : "수입 신고 CIF 기준가격";
        var comparisons = BuildComparisons(domestic, importComparisonPrice, comparisonPriceLabel);
        var primaryComparison = comparisons.FirstOrDefault(item => item.BasisCode == "Retail")
            ?? comparisons.FirstOrDefault();
        var complete = domestic.Success && import.Success && primaryComparison is not null;
        var statusCode = complete
            ? "Complete"
            : domestic.Success
                ? "DomesticOnly"
                : import.Success
                    ? "ImportOnly"
                    : "Unavailable";

        return new FoodPriceComparisonResponse
        {
            Success = complete,
            StatusCode = statusCode,
            ErrorMessage = complete ? null : BuildPartialError(domestic, import),
            HsCode = hsCode,
            ProductName = item.ProductName,
            CountryCode = countryCode,
            Match = MapMatch(item),
            ImportPrice = importReference,
            DomesticPrice = domestic,
            PrimaryComparison = primaryComparison,
            Comparisons = comparisons,
            Summary = BuildSummary(primaryComparison, domestic, import, item),
            Notices = BuildNotices(item, request, fxRate)
        };
    }

    private async Task<HsCountryImportUnitPriceSimulationResult> LookupImportSafelyAsync(
        HsCountryMonthlyTradeUnitPriceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _importPriceLookupService.SimulateImportUnitPriceAsync(request, cancellationToken);
        }
        catch (Exception ex) when (
            !cancellationToken.IsCancellationRequested
            && ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return new HsCountryImportUnitPriceSimulationResult
            {
                Success = false,
                ErrorMessage = ex is TaskCanceledException
                    ? "관세청 수입통계 조회시간이 초과되었습니다."
                    : "관세청 수입통계를 불러오지 못했습니다.",
                HsCode = request.HsCode,
                CountryCode = request.CountryCode,
                EndMonth = request.Month
            };
        }
    }

    private static FoodImportPriceReference? MapImportReference(
        HsCountryImportUnitPriceSimulationResult import,
        FoodPriceComparisonRequest request,
        decimal fxRate)
    {
        if (!import.Success)
        {
            return null;
        }

        return new FoodImportPriceReference
        {
            StartMonth = import.StartMonth,
            EndMonth = import.EndMonth,
            TotalImportWeightKg = import.TotalImportWeightKg,
            AverageCifUsdPerKg = import.AverageImportUnitValueUsdPerKg,
            FxRateKrwPerUsd = fxRate > 0 ? fxRate : null,
            AverageCifKrwPerKg = import.AverageImportUnitValueKrwPerKg,
            EstimatedLandedCostKrwPerKg = request.EstimatedImportAdditionalCostKrwPerKg is > 0
                ? import.ExpectedLandedCostKrwPerKg
                : null
        };
    }

    private static IReadOnlyList<FoodPriceGapResponse> BuildComparisons(
        AtDomesticFoodPriceLookupResult domestic,
        decimal? importReferencePrice,
        string importPriceLabel)
    {
        if (!domestic.Success || importReferencePrice is not > 0)
        {
            return [];
        }

        var results = new List<FoodPriceGapResponse>(2);
        if (domestic.Retail is { AverageKrwPerKg: > 0 } retail)
        {
            results.Add(BuildGap("Retail", "국내 소매가격", retail.AverageKrwPerKg, importReferencePrice.Value, importPriceLabel));
        }

        if (domestic.Wholesale is { AverageKrwPerKg: > 0 } wholesale)
        {
            results.Add(BuildGap("Wholesale", "국내 중도매가격", wholesale.AverageKrwPerKg, importReferencePrice.Value, importPriceLabel));
        }

        return results;
    }

    private static FoodPriceGapResponse BuildGap(
        string basisCode,
        string domesticLabel,
        decimal domesticPrice,
        decimal importPrice,
        string importPriceLabel)
    {
        var difference = decimal.Round(domesticPrice - importPrice, 0, MidpointRounding.AwayFromZero);
        var rate = decimal.Round(difference / domesticPrice, 4, MidpointRounding.AwayFromZero);
        var (signalCode, signalLabel, summary) = rate switch
        {
            > 0.05m => ("ImportReferenceLower", "수입 기준가격이 낮음", $"{importPriceLabel}이 {domesticLabel}보다 약 {rate:P0} 낮습니다."),
            < -0.05m => ("DomesticLower", "국내 가격이 낮음", $"{domesticLabel}이 {importPriceLabel}보다 약 {-rate:P0} 낮습니다."),
            _ => ("Similar", "비슷한 수준", $"{domesticLabel}과 {importPriceLabel}이 비슷한 수준입니다.")
        };

        return new FoodPriceGapResponse
        {
            BasisCode = basisCode,
            BasisLabel = $"{domesticLabel} 대 {importPriceLabel}",
            DomesticPriceKrwPerKg = domesticPrice,
            ImportReferencePriceKrwPerKg = importPrice,
            DifferenceKrwPerKg = difference,
            DifferenceRate = rate,
            SignalCode = signalCode,
            SignalLabel = signalLabel,
            PlainLanguageSummary = summary
        };
    }

    private static FoodPriceMatchResponse MapMatch(AgriculturalFisheriesItemResponse item)
        => new()
        {
            MatchQualityCode = item.MatchQualityCode,
            MatchQualityLabel = item.MatchQualityLabel,
            DomesticOriginStatusCode = item.DomesticOriginStatusCode,
            DomesticOriginStatusLabel = item.DomesticOriginStatusLabel,
            AtCategoryCode = item.CategoryCode,
            AtItemCode = item.AtItemCode,
            AtItemName = item.AtItemName,
            Note = item.Note
        };

    private static string BuildSummary(
        FoodPriceGapResponse? primaryComparison,
        AtDomesticFoodPriceLookupResult domestic,
        HsCountryImportUnitPriceSimulationResult import,
        AgriculturalFisheriesItemResponse item)
    {
        if (primaryComparison is not null)
        {
            return $"{item.ProductName}: {primaryComparison.PlainLanguageSummary}";
        }

        if (domestic.Success)
        {
            return $"{item.ProductName} 국내가격은 확인했지만 수입 기준가격을 계산하지 못했습니다.";
        }

        if (import.Success)
        {
            return $"{item.ProductName} 수입 기준가격은 확인했지만 국내가격을 찾지 못했습니다.";
        }

        return $"{item.ProductName} 가격 자료를 현재 불러오지 못했습니다.";
    }

    private static IReadOnlyList<string> BuildNotices(
        AgriculturalFisheriesItemResponse item,
        FoodPriceComparisonRequest request,
        decimal fxRate)
    {
        var notices = new List<string>
        {
            "수입 기준가격은 관세청 CIF 신고금액을 순중량으로 나눈 통계값이며 실제 판매가나 개별 견적이 아닙니다.",
            "품질·등급·포장·신선도 차이 때문에 동일 상품의 완전한 가격비교가 아닐 수 있습니다."
        };

        if (request.EstimatedImportAdditionalCostKrwPerKg is not > 0)
        {
            notices.Add("수입 기준가격에는 관세·부가세·검역·통관·국내 물류비와 판매마진이 포함되지 않았습니다.");
        }

        if (request.FxRateKrwPerUsd is not > 0)
        {
            notices.Add($"원화 환산에는 서버의 가정 환율 {fxRate:N0}원/USD를 사용했습니다.");
        }

        notices.Add(item.DomesticOriginStatusCode == "DomesticVariant"
            ? "aT 품종코드에서 수입산 표본을 제외하고 국산 품종을 선별했습니다."
            : "국내 가격은 aT 국내시장 조사값이며 모든 표본에 국산 원산지가 명시된 것은 아닙니다.");
        if (item.MatchQualityCode == "Representative")
        {
            notices.Add("HS 품목과 국내 조사품목의 규격이 달라 대표 품목 가격을 사용했습니다.");
        }

        return notices;
    }

    private static string BuildPartialError(
        AtDomesticFoodPriceLookupResult domestic,
        HsCountryImportUnitPriceSimulationResult import)
    {
        if (!domestic.Success && !import.Success)
        {
            return string.Join(" ", domestic.ErrorMessage, import.ErrorMessage).Trim();
        }

        return domestic.Success
            ? import.ErrorMessage ?? "수입 기준가격을 확인하지 못했습니다."
            : domestic.ErrorMessage ?? "국내가격을 확인하지 못했습니다.";
    }

    private static FoodPriceComparisonResponse Invalid(
        FoodPriceComparisonRequest request,
        string hsCode,
        string countryCode,
        string message)
        => new()
        {
            Success = false,
            StatusCode = "InvalidRequest",
            ErrorMessage = message,
            HsCode = hsCode,
            CountryCode = countryCode,
            Summary = message
        };

    private static bool TryResolveReferenceDate(string? value, out DateTime referenceDate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            referenceDate = DateTimeOffset.UtcNow.ToOffset(KoreaOffset).Date;
            return true;
        }

        var digits = Regex.Replace(value, "[^0-9]", string.Empty);
        return DateTime.TryParseExact(
            digits,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out referenceDate);
    }

    private static string? ResolveReferenceMonth(string? value, DateTime referenceDate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return referenceDate.ToString("yyyyMM", CultureInfo.InvariantCulture);
        }

        var digits = Regex.Replace(value, "[^0-9]", string.Empty);
        return DateTime.TryParseExact(
            digits + "01",
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _)
            ? digits
            : null;
    }

    private static string NormalizeHsCode(string? value)
        => Regex.Replace(value ?? string.Empty, "[^0-9]", string.Empty);

    private static string NormalizeCountryCode(string? value)
        => Regex.Replace(value ?? string.Empty, "[^0-9A-Za-z]", string.Empty).ToUpperInvariant();
}
