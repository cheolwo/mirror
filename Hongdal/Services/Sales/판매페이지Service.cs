using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Sales;
using Hongdal.Services.Content;

namespace 홍달.Services.Sales;

public interface I판매페이지Service
{
    Task<판매페이지초안목록응답> 초안목록Async(string ownerUserId, CancellationToken cancellationToken);
    Task<판매페이지초안응답?> 초안조회Async(string pageId, string ownerUserId, CancellationToken cancellationToken);
    Task<판매페이지초안응답> 초안생성Async(판매페이지초안생성요청 request, string ownerUserId, CancellationToken cancellationToken);
    Task<판매페이지초안응답> 초안수정Async(string pageId, 판매페이지초안수정요청 request, string ownerUserId, CancellationToken cancellationToken);
}

public sealed class 판매페이지Service : I판매페이지Service
{
    private readonly I판매페이지초안저장소 _store;
    private readonly IAmazon상품참고자료Service _amazonResearchService;

    public 판매페이지Service(
        I판매페이지초안저장소 store,
        IAmazon상품참고자료Service amazonResearchService)
    {
        _store = store;
        _amazonResearchService = amazonResearchService;
    }

    public async Task<판매페이지초안목록응답> 초안목록Async(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var owner = Required(ownerUserId, "판매자 인증 정보");
        var items = await _store.목록Async(owner, cancellationToken);
        return new 판매페이지초안목록응답
        {
            Items = items.Select(ToResponse).ToArray()
        };
    }

    public async Task<판매페이지초안응답?> 초안조회Async(
        string pageId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var owner = Required(ownerUserId, "판매자 인증 정보");
        var item = await _store.조회Async(Required(pageId, "페이지Id"), owner, cancellationToken);
        return item is null ? null : ToResponse(item);
    }

    public async Task<판매페이지초안응답> 초안생성Async(
        판매페이지초안생성요청 request,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var owner = Required(ownerUserId, "판매자 인증 정보");
        ValidateOrderModes(request.개별주문허용, request.공동주문허용, request.공동주문최소수량);

        Amazon상품참고자료Dto? reference = null;
        if (!string.IsNullOrWhiteSpace(request.Amazon상품Url))
        {
            reference = await _amazonResearchService.미리보기Async(
                new Amazon상품참고자료조회요청Dto { 상품Url = request.Amazon상품Url },
                cancellationToken);
        }

        var productName = FirstNonEmpty(request.상품명, reference?.상품명)
            ?? throw new InvalidOperationException("상품명 또는 참고할 Amazon 상품 상세 URL이 필요합니다.");
        var now = DateTime.UtcNow;
        var model = new 판매페이지초안저장모델
        {
            페이지Id = $"sales-page-{Guid.NewGuid():N}",
            소유자UserId = owner,
            상태 = 판매페이지상태코드.초안,
            판매자유형 = NormalizeSellerType(request.판매자유형),
            판매자표시명 = Required(request.판매자표시명, "판매자표시명"),
            상품명 = Limit(productName, 300),
            한줄소개 = Limit(FirstNonEmpty(request.한줄소개, reference?.특징목록.FirstOrDefault()) ?? string.Empty, 500),
            상세설명 = Limit(request.상세설명, 5_000),
            원산지표시 = Clean(request.원산지표시, 200),
            출고지표시 = Clean(request.출고지표시, 200),
            판매가 = NormalizePrice(request.판매가),
            통화코드 = NormalizeCurrency(request.통화코드),
            최소주문수량 = NormalizeMinimumQuantity(request.최소주문수량),
            개별주문허용 = request.개별주문허용,
            공동주문허용 = request.공동주문허용,
            공동주문최소수량 = NormalizeGroupMinimum(request.공동주문허용, request.공동주문최소수량),
            이미지Url목록 = reference?.이미지Url목록.Take(10).ToArray() ?? [],
            핵심정보목록 = reference?.특징목록.Take(12).ToArray() ?? [],
            외부참고자료 = reference is null ? null : ToExternalReference(reference),
            생성시각Utc = now,
            수정시각Utc = now
        };

        var saved = await _store.저장Async(model, 0, cancellationToken);
        return ToResponse(saved);
    }

    public async Task<판매페이지초안응답> 초안수정Async(
        string pageId,
        판매페이지초안수정요청 request,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var owner = Required(ownerUserId, "판매자 인증 정보");
        var normalizedPageId = Required(pageId, "페이지Id");
        var existing = await _store.조회Async(normalizedPageId, owner, cancellationToken)
            ?? throw new InvalidOperationException("판매 페이지 초안을 찾을 수 없습니다.");
        if (request.기대Revision <= 0 || request.기대Revision != existing.Revision)
        {
            throw new InvalidOperationException("판매 페이지 초안이 이미 변경되었습니다. 최신 내용을 다시 불러와 주세요.");
        }

        ValidateOrderModes(request.개별주문허용, request.공동주문허용, request.공동주문최소수량);
        existing.판매자유형 = NormalizeSellerType(request.판매자유형);
        existing.판매자표시명 = Required(request.판매자표시명, "판매자표시명");
        existing.상품명 = Limit(Required(request.상품명, "상품명"), 300);
        existing.한줄소개 = Limit(request.한줄소개, 500);
        existing.상세설명 = Limit(request.상세설명, 5_000);
        existing.원산지표시 = Clean(request.원산지표시, 200);
        existing.출고지표시 = Clean(request.출고지표시, 200);
        existing.판매가 = NormalizePrice(request.판매가);
        existing.통화코드 = NormalizeCurrency(request.통화코드);
        existing.최소주문수량 = NormalizeMinimumQuantity(request.최소주문수량);
        existing.개별주문허용 = request.개별주문허용;
        existing.공동주문허용 = request.공동주문허용;
        existing.공동주문최소수량 = NormalizeGroupMinimum(request.공동주문허용, request.공동주문최소수량);
        existing.이미지Url목록 = NormalizeList(request.이미지Url목록, 10, 2_000);
        existing.핵심정보목록 = NormalizeList(request.핵심정보목록, 12, 700);
        existing.수정시각Utc = DateTime.UtcNow;

        var saved = await _store.저장Async(existing, request.기대Revision, cancellationToken);
        return ToResponse(saved);
    }

    private static 판매페이지외부참고저장모델 ToExternalReference(Amazon상품참고자료Dto source)
        => new()
        {
            제공자 = "Apify",
            마켓플레이스 = "Amazon",
            참조키 = source.참조키,
            상품Url = source.원문Url,
            외부상품번호 = source.Asin,
            관측가격 = source.가격.현재가격,
            관측통화코드 = source.가격.통화코드,
            관측재고여부 = source.재고여부,
            관측평점 = source.평점,
            관측리뷰수 = source.리뷰수,
            관측일시Utc = source.관측일시Utc,
            안내문 = "외부 상품 상세에서 관측한 참고자료입니다. Hongdal 판매가·재고·원산지·판매 조건으로 자동 확정되지 않습니다."
        };

    private static 판매페이지초안응답 ToResponse(판매페이지초안저장모델 source)
        => new()
        {
            페이지Id = source.페이지Id,
            상태 = source.상태,
            판매자유형 = source.판매자유형,
            판매자표시명 = source.판매자표시명,
            상품명 = source.상품명,
            한줄소개 = source.한줄소개,
            상세설명 = source.상세설명,
            원산지표시 = source.원산지표시,
            출고지표시 = source.출고지표시,
            판매가 = source.판매가,
            통화코드 = source.통화코드,
            최소주문수량 = source.최소주문수량,
            개별주문허용 = source.개별주문허용,
            공동주문허용 = source.공동주문허용,
            공동주문최소수량 = source.공동주문최소수량,
            이미지Url목록 = source.이미지Url목록,
            핵심정보목록 = source.핵심정보목록,
            외부참고자료 = source.외부참고자료 is null
                ? null
                : new 판매페이지외부참고자료Dto(
                    source.외부참고자료.제공자,
                    source.외부참고자료.마켓플레이스,
                    source.외부참고자료.참조키,
                    source.외부참고자료.상품Url,
                    source.외부참고자료.외부상품번호,
                    source.외부참고자료.관측가격,
                    source.외부참고자료.관측통화코드,
                    source.외부참고자료.관측재고여부,
                    source.외부참고자료.관측평점,
                    source.외부참고자료.관측리뷰수,
                    source.외부참고자료.관측일시Utc,
                    source.외부참고자료.안내문),
            연결된판매상품Id = source.연결된판매상품Id,
            판매준비안내 = source.연결된판매상품Id.HasValue
                ? "연결된 판매상품의 재고·가격·권한을 확인한 뒤 공개 검수를 진행할 수 있습니다."
                : "이 문서는 편집 가능한 초안입니다. 실제 주문을 받기 전에 기존 입고상품 기반 판매상품을 연결해야 합니다.",
            Revision = source.Revision,
            생성시각Utc = source.생성시각Utc,
            수정시각Utc = source.수정시각Utc
        };

    private static void ValidateOrderModes(bool individualAllowed, bool groupAllowed, int? groupMinimum)
    {
        if (!individualAllowed && !groupAllowed)
        {
            throw new InvalidOperationException("개별주문 또는 공동주문 중 하나 이상을 허용해야 합니다.");
        }

        if (groupAllowed && groupMinimum is <= 1)
        {
            throw new InvalidOperationException("공동주문 최소 수량은 2개 이상이어야 합니다.");
        }
    }

    private static string NormalizeSellerType(string? value)
    {
        var normalized = value?.Trim();
        if (string.Equals(normalized, 판매자유형코드.농가생산자, StringComparison.OrdinalIgnoreCase)) return 판매자유형코드.농가생산자;
        if (string.Equals(normalized, 판매자유형코드.수출업자, StringComparison.OrdinalIgnoreCase)) return 판매자유형코드.수출업자;
        if (string.Equals(normalized, 판매자유형코드.제조자, StringComparison.OrdinalIgnoreCase)) return 판매자유형코드.제조자;
        if (string.Equals(normalized, 판매자유형코드.협동조합, StringComparison.OrdinalIgnoreCase)) return 판매자유형코드.협동조합;
        if (string.Equals(normalized, 판매자유형코드.기타, StringComparison.OrdinalIgnoreCase)) return 판매자유형코드.기타;
        return 판매자유형코드.일반판매자;
    }

    private static decimal? NormalizePrice(decimal? value)
        => value is null ? null : value > 0 ? decimal.Round(value.Value, 2) : throw new InvalidOperationException("판매가는 0보다 커야 합니다.");

    private static string NormalizeCurrency(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "KRW" : value.Trim().ToUpperInvariant();
        return normalized.Length is >= 3 and <= 10
            ? normalized
            : throw new InvalidOperationException("통화코드 형식이 올바르지 않습니다.");
    }

    private static int NormalizeMinimumQuantity(int value)
        => value <= 0 ? 1 : Math.Min(value, 1_000_000);

    private static int? NormalizeGroupMinimum(bool allowed, int? value)
        => allowed ? Math.Min(value ?? 2, 1_000_000) : null;

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values, int maxCount, int maxLength)
        => values?
            .Select(value => Clean(value, maxLength))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(maxCount)
            .ToArray() ?? [];

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string Required(string? value, string fieldName)
        => !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{fieldName}이(가) 필요합니다.");

    private static string Limit(string? value, int maxLength)
        => Clean(value, maxLength) ?? string.Empty;

    private static string? Clean(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public sealed class 판매페이지초안저장모델
{
    public string 페이지Id { get; set; } = string.Empty;
    public string 소유자UserId { get; set; } = string.Empty;
    public string 상태 { get; set; } = 판매페이지상태코드.초안;
    public string 판매자유형 { get; set; } = 판매자유형코드.일반판매자;
    public string 판매자표시명 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string 한줄소개 { get; set; } = string.Empty;
    public string 상세설명 { get; set; } = string.Empty;
    public string? 원산지표시 { get; set; }
    public string? 출고지표시 { get; set; }
    public decimal? 판매가 { get; set; }
    public string 통화코드 { get; set; } = "KRW";
    public int 최소주문수량 { get; set; } = 1;
    public bool 개별주문허용 { get; set; }
    public bool 공동주문허용 { get; set; }
    public int? 공동주문최소수량 { get; set; }
    public IReadOnlyList<string> 이미지Url목록 { get; set; } = [];
    public IReadOnlyList<string> 핵심정보목록 { get; set; } = [];
    public 판매페이지외부참고저장모델? 외부참고자료 { get; set; }
    public long? 연결된판매상품Id { get; set; }
    public long Revision { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 판매페이지외부참고저장모델
{
    public string 제공자 { get; set; } = string.Empty;
    public string 마켓플레이스 { get; set; } = string.Empty;
    public string 참조키 { get; set; } = string.Empty;
    public string 상품Url { get; set; } = string.Empty;
    public string? 외부상품번호 { get; set; }
    public decimal? 관측가격 { get; set; }
    public string? 관측통화코드 { get; set; }
    public bool? 관측재고여부 { get; set; }
    public decimal? 관측평점 { get; set; }
    public int? 관측리뷰수 { get; set; }
    public DateTime 관측일시Utc { get; set; }
    public string 안내문 { get; set; } = string.Empty;
}
