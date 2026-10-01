using FluentResults;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Domain.HsCodes;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Services.External.PublicData;

namespace Hongdal.Application.Customs;

public interface I공동수입HS코드조회UseCase
{
    Task<Result<GroupImportHsCodeSearchResponse>> 검색Async(
        string? query,
        int? businessCategory,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<FoodPriceComparisonResponse>> 식품가격비교Async(
        FoodPriceComparisonRequest request,
        CancellationToken cancellationToken);

    Task<Result<Hs공공데이터묶음응답>> 공공데이터수집Async(
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken);
}

[HongdalApiWorkflow(HongdalWorkflow.GroupPurchaseImport)]
[HongdalUseCase("공동수입 HS 코드 조회", Summary = "주문자가 공동수입 상품 후보를 고를 수 있도록 활성 HS 코드와 통관 주의 태그를 조회합니다.")]
[HongdalUseCaseActor(HongdalActor.Orderer)]
public sealed class 공동수입HS코드조회UseCase : I공동수입HS코드조회UseCase
{
    private readonly HongdalContext _db;
    private readonly IFoodPriceComparisonService _foodPriceComparisonService;
    private readonly IHs공공데이터수집Service _publicDataCollectionService;

    public 공동수입HS코드조회UseCase(
        HongdalContext db,
        IFoodPriceComparisonService foodPriceComparisonService,
        IHs공공데이터수집Service publicDataCollectionService)
    {
        _db = db;
        _foodPriceComparisonService = foodPriceComparisonService;
        _publicDataCollectionService = publicDataCollectionService;
    }

    public async Task<Result<GroupImportHsCodeSearchResponse>> 검색Async(
        string? query,
        int? businessCategory,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 50);

        var hsCodes = _db.HsCodeEntries
            .AsNoTracking()
            .Include(entry => entry.RiskTags)
            .Where(entry => entry.IsActive);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            var normalizedTerm = new string(term.Where(char.IsDigit).ToArray());

            hsCodes = hsCodes.Where(entry =>
                entry.Code.Contains(term) ||
                entry.NormalizedCode.Contains(term) ||
                entry.KoreanName.Contains(term) ||
                entry.EnglishName.Contains(term) ||
                entry.Description.Contains(term) ||
                entry.SearchKeywords.Contains(term) ||
                (!string.IsNullOrWhiteSpace(normalizedTerm) && entry.NormalizedCode.Contains(normalizedTerm)));
        }

        if (businessCategory.HasValue)
        {
            if (!Enum.IsDefined(typeof(HsCodeBusinessCategory), businessCategory.Value))
            {
                return Result.Fail<GroupImportHsCodeSearchResponse>("지원하지 않는 HS 코드 업무 분류입니다.");
            }

            var category = (HsCodeBusinessCategory)businessCategory.Value;
            hsCodes = hsCodes.Where(entry => entry.BusinessCategory == category);
        }

        var totalCount = await hsCodes.CountAsync(cancellationToken);
        var entries = await hsCodes
            .OrderBy(entry => entry.NormalizedCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result.Ok(new GroupImportHsCodeSearchResponse
        {
            Items = entries.Select(Map).ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<FoodPriceComparisonResponse>> 식품가격비교Async(
        FoodPriceComparisonRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _foodPriceComparisonService.CompareAsync(request, cancellationToken);
        return Result.Ok(response);
    }

    public async Task<Result<Hs공공데이터묶음응답>> 공공데이터수집Async(
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken)
    {
        var normalizedHsCode = new string((request.HsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalizedHsCode.Length is < 4 or > 10)
        {
            return Result.Fail<Hs공공데이터묶음응답>("HS 코드는 4~10자리 숫자로 입력해야 합니다.");
        }

        var response = await _publicDataCollectionService.수집Async(request, cancellationToken);
        return Result.Ok(response);
    }

    private static GroupImportHsCodeItemResponse Map(HsCodeEntry entry)
    {
        var activeTags = entry.RiskTags
            .Where(tag => tag.IsActive)
            .OrderBy(tag => (int)tag.TagType)
            .ToArray();

        return new GroupImportHsCodeItemResponse
        {
            Id = entry.Id,
            Code = entry.Code,
            NormalizedCode = entry.NormalizedCode,
            KoreanName = entry.KoreanName,
            EnglishName = entry.EnglishName,
            Description = entry.Description,
            Level = (int)entry.Level,
            LevelLabel = LevelLabel(entry.Level),
            BusinessCategory = (int)entry.BusinessCategory,
            BusinessCategoryLabel = BusinessCategoryLabel(entry.BusinessCategory),
            BrokerReviewRecommended = activeTags.Any(tag => tag.TagType == HsCodeRiskTagType.BrokerReviewRecommended),
            RiskTags = activeTags
                .Select(tag => new GroupImportHsCodeRiskTagResponse
                {
                    TagType = (int)tag.TagType,
                    Label = string.IsNullOrWhiteSpace(tag.Label) ? RiskTagLabel(tag.TagType) : tag.Label
                })
                .ToArray()
        };
    }

    private static string LevelLabel(HsCodeLevel level)
        => level switch
        {
            HsCodeLevel.Chapter => "류",
            HsCodeLevel.Heading => "호",
            HsCodeLevel.Subheading => "소호",
            HsCodeLevel.National => "국가 세번",
            _ => "분류"
        };

    private static string BusinessCategoryLabel(HsCodeBusinessCategory category)
        => category switch
        {
            HsCodeBusinessCategory.Food => "식품 관련",
            HsCodeBusinessCategory.GeneralCargo => "일반 화물",
            HsCodeBusinessCategory.Mixed => "복합",
            _ => "미분류"
        };

    private static string RiskTagLabel(HsCodeRiskTagType tagType)
        => tagType switch
        {
            HsCodeRiskTagType.Food => "식품 관련",
            HsCodeRiskTagType.FoodQuarantine => "검역/식품신고 확인",
            HsCodeRiskTagType.SupplementOrPreparedFoodReview => "조제식품/보충제 검토",
            HsCodeRiskTagType.Textile => "섬유/의류",
            HsCodeRiskTagType.Chemical => "화학물질 확인",
            HsCodeRiskTagType.ElectricalCertification => "전기/인증 확인",
            HsCodeRiskTagType.BatteryIncludedPossible => "배터리 포함 가능",
            HsCodeRiskTagType.Furniture => "가구/생활용품",
            HsCodeRiskTagType.BrokerReviewRecommended => "관세사 검토 권장",
            _ => tagType.ToString()
        };
}
