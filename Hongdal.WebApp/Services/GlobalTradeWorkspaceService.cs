using System.Globalization;
using System.Text;
using Hongdal.WebApp.Models;

namespace Hongdal.WebApp.Services;

public sealed class GlobalTradeWorkspaceService
{
    private readonly List<GlobalTradeProduct> _products =
    [
        new(
            1,
            "portuguese-cork-desk-organizer",
            "Modular Cork Desk Organizer",
            "Vale Cork Studio",
            "Portugal",
            "PT",
            "Home & Living",
            "A lightweight modular organizer made from locally sourced cork.",
            "The modules can be combined for desks, store counters, and compact workspaces. The supplier is looking for a Korean importer for a small pilot order.",
            8.40m,
            "EUR",
            120,
            true,
            "4503.90",
            "FOB Porto",
            "Material declaration available",
            "#c46d3b",
            "CORK",
            null,
            GlobalTradeReviewStatus.Published,
            DateTimeOffset.UtcNow.AddDays(-12)),
        new(
            2,
            "indonesian-rattan-storage-basket",
            "Handwoven Rattan Storage Basket",
            "Nusa Craft Collective",
            "Indonesia",
            "ID",
            "Interior Goods",
            "Handwoven storage baskets produced by a small artisan collective.",
            "A natural-fiber basket line suitable for lifestyle stores and community group purchases. Sample shipment and mixed-size cartons are available.",
            11.20m,
            "USD",
            80,
            true,
            "4602.12",
            "FOB Surabaya",
            "Origin and material documents available",
            "#28786f",
            "RATTAN",
            null,
            GlobalTradeReviewStatus.Published,
            DateTimeOffset.UtcNow.AddDays(-8)),
        new(
            3,
            "mexican-recycled-glass-vase",
            "Recycled Glass Table Vase",
            "Luz del Taller",
            "Mexico",
            "MX",
            "Interior Goods",
            "Small-batch table vases made with recycled glass.",
            "Each batch has a slightly different color tone. The supplier can provide protective export packaging and is seeking a Korean retail partner.",
            9.75m,
            "USD",
            144,
            true,
            "7013.99",
            "FCA Guadalajara",
            "Packing specification available",
            "#365f8d",
            "GLASS",
            null,
            GlobalTradeReviewStatus.Published,
            DateTimeOffset.UtcNow.AddDays(-4))
    ];

    private readonly List<GlobalImportInterestRequest> _importRequests =
    [
        new(
            1,
            2,
            "indonesian-rattan-storage-basket",
            "Handwoven Rattan Storage Basket",
            "Nusa Craft Collective",
            "김소연",
            "마을상점 협동조합",
            "sample@hongdal.local",
            160,
            "공동구매용으로 혼합 사이즈 견적을 먼저 받고 싶습니다.",
            "신규",
            DateTimeOffset.UtcNow.AddHours(-5))
    ];

    private readonly List<GlobalTradeCommunityThread> _communityThreads =
    [
        CreateSampleTradeThread()
    ];

    private readonly List<GlobalImportOrderLedger> _importOrders = [];

    public IReadOnlyList<GlobalTradeProduct> Products => _products;
    public IReadOnlyList<GlobalImportInterestRequest> ImportRequests => _importRequests;
    public IReadOnlyList<GlobalTradeCommunityThread> CommunityThreads => _communityThreads;
    public IReadOnlyList<GlobalImportOrderLedger> ImportOrders => _importOrders;

    public GlobalTradeProduct? FindProduct(string? slug)
        => _products.FirstOrDefault(product =>
            string.Equals(product.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public GlobalTradeProduct SubmitSupplierProduct(GlobalSupplierProductDraft draft)
    {
        var id = _products.Count == 0 ? 1 : _products.Max(product => product.Id) + 1;
        var product = new GlobalTradeProduct(
            id,
            BuildUniqueSlug(draft.ProductName, id),
            draft.ProductName.Trim(),
            draft.SupplierName.Trim(),
            draft.CountryName.Trim(),
            CountryCodeFrom(draft.CountryName),
            draft.Category.Trim(),
            draft.Summary.Trim(),
            draft.Summary.Trim(),
            draft.SupplyPrice,
            draft.CurrencyCode.Trim().ToUpperInvariant(),
            draft.MinimumOrderQuantity,
            draft.SampleAvailable,
            draft.SuggestedHsCode.Trim(),
            draft.Incoterm.Trim(),
            string.IsNullOrWhiteSpace(draft.CertificationSummary)
                ? "Documents to be reviewed"
                : draft.CertificationSummary.Trim(),
            "#596b4d",
            "NEW",
            null,
            GlobalTradeReviewStatus.PendingReview,
            DateTimeOffset.UtcNow);

        _products.Insert(0, product);
        return product;
    }

    public GlobalImportInterestRequest SubmitImportInterest(
        GlobalTradeProduct product,
        GlobalImportInterestDraft draft)
    {
        var id = _importRequests.Count == 0 ? 1 : _importRequests.Max(request => request.Id) + 1;
        var request = new GlobalImportInterestRequest(
            id,
            product.Id,
            product.Slug,
            product.ProductName,
            product.SupplierName,
            draft.RequesterName.Trim(),
            draft.CompanyName.Trim(),
            draft.Email.Trim(),
            draft.ExpectedQuantity,
            draft.Note.Trim(),
            "신규",
            DateTimeOffset.UtcNow);

        _importRequests.Insert(0, request);
        return request;
    }

    public GlobalTradeCommunityThread? FindCommunityThread(long threadId)
        => _communityThreads.FirstOrDefault(thread => thread.Id == threadId);

    public GlobalImportOrderLedger? FindImportOrder(string? orderCode)
        => _importOrders.FirstOrDefault(order =>
            string.Equals(order.OrderCode, orderCode, StringComparison.OrdinalIgnoreCase));

    public GlobalImportOrderLedger? FindImportOrderByThread(long threadId)
        => _importOrders.FirstOrDefault(order => order.SourceCommunityThreadId == threadId);

    public GlobalImportOrderLedger? FindImportOrderByRequest(long requestId)
        => _importOrders.FirstOrDefault(order => order.SourceImportRequestId == requestId);

    public GlobalTradeCommunityComment AddCommunityComment(
        long threadId,
        string authorName,
        string authorRole,
        string language,
        string text,
        string? ledgerKey = null)
    {
        var thread = FindCommunityThread(threadId)
            ?? throw new InvalidOperationException($"Community trade thread {threadId} was not found.");
        var nextId = _communityThreads.SelectMany(item => item.Comments).Select(comment => comment.Id).DefaultIfEmpty().Max() + 1;
        var comment = new GlobalTradeCommunityComment(
            nextId,
            authorName.Trim(),
            authorRole.Trim(),
            language.Trim().ToLowerInvariant(),
            text.Trim(),
            null,
            string.IsNullOrWhiteSpace(ledgerKey) ? null : ledgerKey,
            DateTimeOffset.UtcNow);

        thread.Comments.Add(comment);
        return comment;
    }

    public GlobalImportOrderLedger CreateImportOrderFromThread(long threadId, GlobalImportOrderDraft draft)
    {
        var existing = FindImportOrderByThread(threadId);
        if (existing is not null)
        {
            return existing;
        }

        var thread = FindCommunityThread(threadId)
            ?? throw new InvalidOperationException($"Community trade thread {threadId} was not found.");
        var product = FindProductById(thread.ProductId)
            ?? throw new InvalidOperationException($"Product {thread.ProductId} was not found.");

        return CreateImportOrder(product, draft, threadId, null);
    }

    public GlobalImportOrderLedger CreateImportOrderFromRequest(long requestId)
    {
        var existing = FindImportOrderByRequest(requestId);
        if (existing is not null)
        {
            return existing;
        }

        var request = _importRequests.FirstOrDefault(item => item.Id == requestId)
            ?? throw new InvalidOperationException($"Import request {requestId} was not found.");
        var product = FindProductById(request.ProductId)
            ?? throw new InvalidOperationException($"Product {request.ProductId} was not found.");
        var draft = new GlobalImportOrderDraft
        {
            ImporterName = string.IsNullOrWhiteSpace(request.CompanyName) ? request.RequesterName : request.CompanyName,
            ImporterContact = request.Email,
            OrderQuantity = request.ExpectedQuantity,
            Incoterm = product.Incoterm.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "FOB"
        };

        return CreateImportOrder(product, draft, null, requestId);
    }

    private GlobalTradeProduct? FindProductById(long productId)
        => _products.FirstOrDefault(product => product.Id == productId);

    private GlobalImportOrderLedger CreateImportOrder(
        GlobalTradeProduct product,
        GlobalImportOrderDraft draft,
        long? sourceCommunityThreadId,
        long? sourceImportRequestId)
    {
        var id = _importOrders.Select(order => order.Id).DefaultIfEmpty().Max() + 1;
        var orderCode = $"KOR-IMP-{DateTimeOffset.Now:yyyy}-{id:D4}";
        var order = new GlobalImportOrderLedger(
            id,
            orderCode,
            product.Id,
            product.Slug,
            product.ProductName,
            draft.ImporterName.Trim(),
            draft.ImporterContact.Trim(),
            product.SupplierName,
            product.CountryName,
            "KR",
            draft.OrderQuantity,
            product.SupplyPrice,
            product.CurrencyCode,
            draft.Incoterm.Trim(),
            draft.TargetInboundDate,
            GlobalImportOrderStatus.DraftReview,
            sourceCommunityThreadId,
            sourceImportRequestId,
            BuildLinkedLedgers(product, draft.OrderQuantity),
            DateTimeOffset.UtcNow);

        _importOrders.Insert(0, order);
        return order;
    }

    private static IReadOnlyList<GlobalLinkedLedgerNode> BuildLinkedLedgers(GlobalTradeProduct product, int quantity)
        =>
        [
            new("product", "상품·공급자 원장", "상품 자료 확인", "해외 공급자", "자료 등록", $"{product.SupplierName}의 상품, MOQ, 샘플, 인증 자료를 확인합니다.", "teal", "inventory", GlobalTradeRoutes.Product(product.Slug)),
            new("demand", "수요 원장", "한국 수요 확인", "한국 수입자", $"{quantity:N0}개 관심", "커뮤니티 댓글과 수입 요청을 주문 수량의 근거로 연결합니다.", "green", "groups", GlobalTradeRoutes.ImportRequests),
            new("quote", "견적·계약 원장", "견적·거래조건", "수입자·공급자", "초안", $"{product.CurrencyCode} 단가와 {product.Incoterm} 조건을 협의합니다.", "amber", "description", null),
            new("customs", "HS·통관 원장", "HS·수입요건", "관세사", "검토 대기", $"후보 HS {product.SuggestedHsCode}와 한국 수입요건을 확인합니다.", "slate", "fact-check", ShipperRoutes.CustomsHsReviews),
            new("payment", "결제·정산 원장", "해외 결제·정산", "수입자", "주문 확정 후", "계약금, 잔금, 환율과 수수료를 주문원장에 결합합니다.", "purple", "payments", null),
            new("shipment", "해외 선적 원장", "선적·국제운송", "포워더", "발주 후", "포장, 선적 문서, FCL/LCL 판단과 도착 일정을 관리합니다.", "blue", "shipping", ShipperRoutes.FclLclPlanner),
            new("warehouse", "창고 입고 원장", "통관 후 입고", "창고 관리자", "통관 후", "한국 반출 이후 검수, 적재와 판매 가능 재고 전환을 관리합니다.", "olive", "warehouse", ShipperRoutes.WarehouseWorkspace),
            new("distribution", "국내 분배 원장", "공동구매·국내 분배", "커뮤니티 운영자", "입고 후", "참여자별 분배와 국내 배송을 연결합니다.", "orange", "local-shipping", "/community/group-import")
        ];

    private static GlobalTradeCommunityThread CreateSampleTradeThread()
    {
        var thread = new GlobalTradeCommunityThread
        {
            Id = 101,
            ProductId = 2,
            Title = "Looking for a Korean partner for handwoven rattan baskets",
            AuthorName = "Ayu Pranata",
            AuthorRole = "Nusa Craft Collective · Indonesia",
            OriginalLanguage = "en",
            OriginalBody = "We are a small artisan collective producing handwoven rattan storage baskets. We can prepare mixed-size samples and are looking for a Korean importer or community-buying partner.",
            TranslatedBody = "저희는 수공예 라탄 수납 바구니를 만드는 소규모 장인 협동체입니다. 혼합 크기 샘플을 준비할 수 있으며 한국 수입업자 또는 공동구매 파트너를 찾고 있습니다.",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
        };

        thread.Comments.AddRange(
        [
            new(1, "김소연", "한국 공동구매 운영자", "ko", "160개 정도로 먼저 공동구매를 열어보고 싶습니다. 혼합 사이즈 포장이 가능한가요?", "We would like to test a community purchase of around 160 units. Can you pack mixed sizes?", null, DateTimeOffset.UtcNow.AddDays(-2).AddHours(-4)),
            new(2, "Ayu Pranata", "해외 공급자", "en", "Yes. We can mix three sizes in one export carton and send two sample sets before the main order.", "네. 수출용 상자 하나에 세 가지 크기를 혼합할 수 있고 본 주문 전에 샘플 두 세트를 보낼 수 있습니다.", null, DateTimeOffset.UtcNow.AddDays(-2).AddHours(-2)),
            new(3, "박관세사", "통관 조언자", "ko", "라탄 재질과 가공 상태에 따라 검역·품목분류 확인이 필요합니다. 샘플 사진과 소재 확인서를 먼저 받아보면 좋겠습니다.", "Quarantine and classification review may be needed depending on the rattan material and processing. Please obtain sample photos and a material statement first.", "customs", DateTimeOffset.UtcNow.AddDays(-1))
        ]);

        return thread;
    }

    private string BuildUniqueSlug(string productName, long id)
    {
        var normalized = productName.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var candidate = builder.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = $"supplier-product-{id}";
        }

        return _products.Any(product => string.Equals(product.Slug, candidate, StringComparison.OrdinalIgnoreCase))
            ? $"{candidate}-{id}"
            : candidate;
    }

    private static string CountryCodeFrom(string countryName)
    {
        var letters = new string(countryName.Where(char.IsLetter).Take(2).ToArray());
        return string.IsNullOrWhiteSpace(letters) ? "GL" : letters.ToUpperInvariant();
    }
}
