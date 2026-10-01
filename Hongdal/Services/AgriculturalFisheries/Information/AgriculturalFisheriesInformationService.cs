using System.Globalization;
using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.PublicData;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public sealed class AgriculturalFisheriesInformationService : IAgriculturalFisheriesInformationService
{
    private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);

    private static readonly IReadOnlyDictionary<string, string> CategoryLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["100"] = "식량작물",
            ["200"] = "채소류",
            ["300"] = "특용작물",
            ["400"] = "과일류",
            ["500"] = "축산물",
            ["600"] = "수산물"
        };

    private readonly IFoodPriceCrosswalkCatalog _crosswalkCatalog;
    private readonly IAtDomesticFoodPriceLookupService _domesticPriceLookupService;
    private readonly PublicDataOptions _options;

    public AgriculturalFisheriesInformationService(
        IFoodPriceCrosswalkCatalog crosswalkCatalog,
        IAtDomesticFoodPriceLookupService domesticPriceLookupService,
        IOptions<PublicDataOptions> options)
    {
        _crosswalkCatalog = crosswalkCatalog;
        _domesticPriceLookupService = domesticPriceLookupService;
        _options = options.Value;
    }

    public AgriculturalFisheriesInformationOverviewResponse GetOverview()
        => AgriculturalFisheriesInformationOverviewFactory.Create(_options);

    public AgriculturalFisheriesItemSearchResponse SearchItems(
        string? query,
        string? categoryCode,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var term = query?.Trim() ?? string.Empty;
        var normalizedTerm = NormalizeHsCode(term);
        var normalizedCategory = categoryCode?.Trim() ?? string.Empty;

        IEnumerable<FoodPriceCrosswalk> entries = _crosswalkCatalog.GetAll();
        if (!string.IsNullOrWhiteSpace(normalizedCategory))
        {
            entries = entries.Where(entry => string.Equals(
                entry.AtCategoryCode,
                normalizedCategory,
                StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            entries = entries.Where(entry =>
                entry.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.AtItemName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(normalizedTerm)
                    && entry.HsPrefix.Contains(normalizedTerm, StringComparison.Ordinal)));
        }

        var filtered = entries
            .OrderBy(entry => entry.AtCategoryCode, StringComparer.Ordinal)
            .ThenBy(entry => entry.ProductName, StringComparer.Ordinal)
            .ThenBy(entry => entry.HsPrefix, StringComparer.Ordinal)
            .ToArray();

        return new AgriculturalFisheriesItemSearchResponse
        {
            Items = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapItem)
                .ToArray(),
            TotalCount = filtered.Length,
            Page = page,
            PageSize = pageSize
        };
    }

    public AgriculturalFisheriesItemResponse? FindItem(string? hsCode)
    {
        var crosswalk = _crosswalkCatalog.Find(hsCode);
        return crosswalk is null ? null : MapItem(crosswalk);
    }

    public async Task<AgriculturalFisheriesDomesticPriceResponse> GetDomesticPriceAsync(
        AgriculturalFisheriesDomesticPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        var hsCode = NormalizeHsCode(request.HsCode);
        if (hsCode.Length < 4)
        {
            return Fail("InvalidRequest", hsCode, "HS 코드를 4자리 이상 입력해 주세요.");
        }

        if (!TryResolveReferenceDate(request.ReferenceDate, out var referenceDate))
        {
            return Fail("InvalidRequest", hsCode, "국내가격 기준일을 yyyyMMdd 형식으로 확인해 주세요.");
        }

        var crosswalk = _crosswalkCatalog.Find(hsCode);
        if (crosswalk is null)
        {
            return Fail(
                "MappingRequired",
                hsCode,
                "현재 정보 카탈로그에 연결되지 않은 HS 코드입니다.",
                "HS 분류와 국내 가격 조사 품목의 검토된 연결이 필요합니다.");
        }

        var item = MapItem(crosswalk);
        var lookbackDays = Math.Clamp(request.LookbackDays <= 0 ? 14 : request.LookbackDays, 1, 31);
        var startDate = referenceDate.AddDays(-(lookbackDays - 1));
        var lookupRequest = new AtDomesticFoodPriceRequest
        {
            CategoryCode = crosswalk.AtCategoryCode,
            ItemCode = crosswalk.AtItemCode,
            StartDate = startDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            EndDate = referenceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            VarietyCodes = crosswalk.AtVarietyCodes,
            ExcludedNameTokens = crosswalk.ExcludedNameTokens
        };

        AtDomesticFoodPriceLookupResult price;
        try
        {
            price = await _domesticPriceLookupService.LookupAsync(lookupRequest, cancellationToken);
        }
        catch (Exception ex) when (
            !cancellationToken.IsCancellationRequested
            && ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            price = new AtDomesticFoodPriceLookupResult
            {
                Success = false,
                ErrorMessage = ex is TaskCanceledException
                    ? "aT 국내가격 조회시간이 초과되었습니다."
                    : "aT 국내가격을 불러오지 못했습니다.",
                CategoryCode = lookupRequest.CategoryCode,
                ItemCode = lookupRequest.ItemCode,
                StartDate = lookupRequest.StartDate,
                EndDate = lookupRequest.EndDate
            };
        }

        var notices = BuildPriceNotices(crosswalk);
        return new AgriculturalFisheriesDomesticPriceResponse
        {
            Success = price.Success,
            StatusCode = price.Success ? "Complete" : "DataUnavailable",
            ErrorMessage = price.ErrorMessage,
            HsCode = hsCode,
            Item = item,
            Price = price,
            Summary = price.Success
                ? $"{item.ProductName}의 최근 국내 중도매·소매 조사 가격입니다."
                : $"{item.ProductName}의 국내가격 자료를 현재 확인하지 못했습니다.",
            Notices = notices
        };
    }

    private static AgriculturalFisheriesItemResponse MapItem(FoodPriceCrosswalk crosswalk)
        => new()
        {
            HsPrefix = crosswalk.HsPrefix,
            ProductName = crosswalk.ProductName,
            CategoryCode = crosswalk.AtCategoryCode,
            CategoryLabel = CategoryLabels.GetValueOrDefault(crosswalk.AtCategoryCode, "기타"),
            AtItemCode = crosswalk.AtItemCode,
            AtItemName = crosswalk.AtItemName,
            AtVarietyCodes = crosswalk.AtVarietyCodes,
            MatchQualityCode = crosswalk.MatchQualityCode,
            MatchQualityLabel = crosswalk.MatchQualityLabel,
            DomesticOriginStatusCode = crosswalk.DomesticOriginStatusCode,
            DomesticOriginStatusLabel = crosswalk.DomesticOriginStatusLabel,
            Note = crosswalk.Note
        };

    private static IReadOnlyList<string> BuildPriceNotices(FoodPriceCrosswalk crosswalk)
    {
        var notices = new List<string>
        {
            "한국농수산식품유통공사(aT)의 조사 가격이며 주문·매입·운송 견적이 아닙니다.",
            "표시 단가는 kg 기준으로 정규화되어 실제 포장단위 가격과 다를 수 있습니다.",
            "품질·등급·산지·포장·신선도 차이를 확인한 뒤 의사결정에 참고해 주세요."
        };

        notices.Add(crosswalk.DomesticOriginStatusCode == "DomesticVariant"
            ? "aT 품종코드와 명칭을 기준으로 수입산 표본을 제외한 국산 품종 정보입니다."
            : "국내시장 조사값이며 모든 표본에 국산 원산지가 명시된 것은 아닙니다.");
        if (crosswalk.MatchQualityCode == "Representative")
        {
            notices.Add("HS 규격과 국내 조사 규격이 달라 대표 품목 가격을 사용합니다.");
        }

        return notices;
    }

    private static AgriculturalFisheriesDomesticPriceResponse Fail(
        string statusCode,
        string hsCode,
        string errorMessage,
        string? summary = null)
        => new()
        {
            Success = false,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            HsCode = hsCode,
            Summary = summary ?? errorMessage,
            Notices = ["이 API는 정보 제공 전용이며 주문·계약·주선 업무를 실행하지 않습니다."]
        };

    private static bool TryResolveReferenceDate(string? value, out DateTime referenceDate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            referenceDate = DateTimeOffset.UtcNow.ToOffset(KoreaOffset).Date;
            return true;
        }

        var digits = NormalizeHsCode(value);
        return DateTime.TryParseExact(
            digits,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out referenceDate);
    }

    private static string NormalizeHsCode(string? value)
        => Regex.Replace(value ?? string.Empty, "[^0-9]", string.Empty);
}
