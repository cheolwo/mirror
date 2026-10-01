using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum CommunityComposerMessageKind
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record CommunityPostComposerSnapshot
{
    public DateTime SavedAtUtc { get; init; }
    public long? EditingPostId { get; init; }
    public bool IsScheduledPublication { get; init; }
    public DateTime? ScheduledPublishDateLocal { get; init; }
    public TimeSpan? ScheduledPublishTimeLocal { get; init; }
    public string Nickname { get; init; } = string.Empty;
    public bool IsAuthorDisplayCountryPublic { get; init; }
    public string AuthorDisplayCountryCode { get; init; } = string.Empty;
    public string AuthorDisplayCountryName { get; init; } = string.Empty;
    public string Category { get; init; } = PlatformCommunityPostCategories.General;
    public string WorkflowTag { get; init; } = "커뮤니티 신뢰";
    public string RoleTag { get; init; } = "플랫폼 구성원";
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string SharedLinkUrl { get; init; } = string.Empty;
    public bool IsSalesPost { get; init; }
    public string SalesProductTitle { get; init; } = string.Empty;
    public decimal SalesAvailableQuantity { get; init; } = 1;
    public string SalesQuantityUnit { get; init; } = "개";
    public decimal SalesUnitPrice { get; init; }
    public string SalesCurrencyCode { get; init; } = "KRW";
    public bool AcceptsTossPayments { get; init; }
    public bool AcceptsNaverPay { get; init; }
    public bool AcceptsPayPal { get; init; }
    public bool AcceptsDirectCash { get; init; } = true;
    public bool AllowsGroupPurchase { get; init; } = true;
    public string SalesStatus { get; init; } = PlatformCommunitySalesOfferStatuses.Open;
    public string 커뮤니티원장Id { get; init; } = string.Empty;
    public bool IsReportBoardPost { get; init; }
    public string ReporterDisplayName { get; init; } = string.Empty;
    public string ReportedDisplayName { get; init; } = string.Empty;
}

public interface ICommunityPostComposerDraftStore
{
    Task<CommunityPostComposerSnapshot?> LoadAsync(
        string appKey,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string appKey,
        CommunityPostComposerSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        string appKey,
        CancellationToken cancellationToken = default);
}

public sealed class CommunityPostComposerDraftViewModel : ObservableObject
{
    private string _nickname = string.Empty;
    private bool _isAuthorDisplayCountryPublic;
    private string _authorDisplayCountryCode = string.Empty;
    private string _authorDisplayCountryName = string.Empty;
    private string _password = string.Empty;
    private string _category = PlatformCommunityPostCategories.General;
    private string _workflowTag = "커뮤니티 신뢰";
    private string _roleTag = "플랫폼 구성원";
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _sharedLinkUrl = string.Empty;
    private bool _isSalesPost;
    private string _salesProductTitle = string.Empty;
    private decimal _salesAvailableQuantity = 1;
    private string _salesQuantityUnit = "개";
    private decimal _salesUnitPrice;
    private string _salesCurrencyCode = "KRW";
    private bool _acceptsTossPayments;
    private bool _acceptsNaverPay;
    private bool _acceptsPayPal;
    private bool _acceptsDirectCash = true;
    private bool _allowsGroupPurchase = true;
    private string _salesStatus = PlatformCommunitySalesOfferStatuses.Open;
    private string _커뮤니티원장Id = string.Empty;
    private bool _isReportBoardPost;
    private string _reporterDisplayName = string.Empty;
    private string _reportedDisplayName = string.Empty;

    public string Nickname { get => _nickname; set => SetProperty(ref _nickname, value); }
    public bool IsAuthorDisplayCountryPublic { get => _isAuthorDisplayCountryPublic; set => SetProperty(ref _isAuthorDisplayCountryPublic, value); }
    public string AuthorDisplayCountryCode { get => _authorDisplayCountryCode; set => SetProperty(ref _authorDisplayCountryCode, value); }
    public string AuthorDisplayCountryName { get => _authorDisplayCountryName; set => SetProperty(ref _authorDisplayCountryName, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string Category
    {
        get => _category;
        set => SetProperty(
            ref _category,
            _isSalesPost ? PlatformCommunityPostCategories.Sales : value);
    }
    public string WorkflowTag { get => _workflowTag; set => SetProperty(ref _workflowTag, value); }
    public string RoleTag { get => _roleTag; set => SetProperty(ref _roleTag, value); }
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string Body { get => _body; set => SetProperty(ref _body, value); }
    public string SharedLinkUrl { get => _sharedLinkUrl; set => SetProperty(ref _sharedLinkUrl, value); }
    public bool IsSalesPost
    {
        get => _isSalesPost;
        set
        {
            if (!SetProperty(ref _isSalesPost, value) || !value)
            {
                return;
            }

            Category = PlatformCommunityPostCategories.Sales;
            IsReportBoardPost = false;
        }
    }
    public string SalesProductTitle { get => _salesProductTitle; set => SetProperty(ref _salesProductTitle, value); }
    public decimal SalesAvailableQuantity { get => _salesAvailableQuantity; set => SetProperty(ref _salesAvailableQuantity, value); }
    public string SalesQuantityUnit { get => _salesQuantityUnit; set => SetProperty(ref _salesQuantityUnit, value); }
    public decimal SalesUnitPrice { get => _salesUnitPrice; set => SetProperty(ref _salesUnitPrice, value); }
    public string SalesCurrencyCode { get => _salesCurrencyCode; set => SetProperty(ref _salesCurrencyCode, value); }
    public bool AcceptsTossPayments { get => _acceptsTossPayments; set => SetProperty(ref _acceptsTossPayments, value); }
    public bool AcceptsNaverPay { get => _acceptsNaverPay; set => SetProperty(ref _acceptsNaverPay, value); }
    public bool AcceptsPayPal { get => _acceptsPayPal; set => SetProperty(ref _acceptsPayPal, value); }
    public bool AcceptsDirectCash { get => _acceptsDirectCash; set => SetProperty(ref _acceptsDirectCash, value); }
    public bool AllowsGroupPurchase { get => _allowsGroupPurchase; set => SetProperty(ref _allowsGroupPurchase, value); }
    public string SalesStatus { get => _salesStatus; set => SetProperty(ref _salesStatus, value); }
    public string 커뮤니티원장Id { get => _커뮤니티원장Id; set => SetProperty(ref _커뮤니티원장Id, value); }
    public bool IsReportBoardPost
    {
        get => _isReportBoardPost;
        set => SetProperty(ref _isReportBoardPost, IsSalesPost ? false : value);
    }
    public string ReporterDisplayName { get => _reporterDisplayName; set => SetProperty(ref _reporterDisplayName, value); }
    public string ReportedDisplayName { get => _reportedDisplayName; set => SetProperty(ref _reportedDisplayName, value); }

    public bool HasContent
        => !string.IsNullOrWhiteSpace(Title)
           || !string.IsNullOrWhiteSpace(Body)
           || !string.IsNullOrWhiteSpace(SharedLinkUrl)
           || IsSalesPost
           || !string.IsNullOrWhiteSpace(커뮤니티원장Id);

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Nickname)
            || string.IsNullOrWhiteSpace(Category)
            || string.IsNullOrWhiteSpace(WorkflowTag)
            || string.IsNullOrWhiteSpace(RoleTag)
            || string.IsNullOrWhiteSpace(Title)
            || (string.IsNullOrWhiteSpace(Body) && string.IsNullOrWhiteSpace(SharedLinkUrl) && !IsSalesPost))
        {
            return "닉네임, 게시판/분류, 워크플로우 태그, 역할 태그, 제목과 본문·링크·판매 정보 중 하나를 입력하세요.";
        }

        if (Nickname.Trim().Length > 40)
        {
            return "닉네임은 1자 이상 40자 이하로 입력하세요.";
        }

        if (string.IsNullOrWhiteSpace(Password)
            || Password.Trim().Length is < 4 or > 100)
        {
            return "글 비밀번호는 4자 이상 100자 이하로 입력하세요.";
        }

        if (Title.Trim().Length > 160)
        {
            return "제목은 1자 이상 160자 이하로 입력하세요.";
        }

        if (Body.Trim().Length > 4000)
        {
            return "본문은 4,000자 이하로 입력하세요.";
        }

        if (!string.IsNullOrWhiteSpace(SharedLinkUrl)
            && (SharedLinkUrl.Trim().Length > 1000
                || !Uri.TryCreate(SharedLinkUrl.Trim(), UriKind.Absolute, out var sharedUri)
                || (sharedUri.Scheme != Uri.UriSchemeHttp
                    && sharedUri.Scheme != Uri.UriSchemeHttps)))
        {
            return "공유 링크는 http 또는 https 주소로 입력하세요.";
        }

        if (IsSalesPost)
        {
            if (string.IsNullOrWhiteSpace(SalesProductTitle)
                || SalesProductTitle.Trim().Length > 160)
            {
                return "판매할 상품명은 1자 이상 160자 이하로 입력하세요.";
            }

            if (SalesAvailableQuantity is <= 0 or > 1_000_000
                || string.IsNullOrWhiteSpace(SalesQuantityUnit)
                || SalesQuantityUnit.Trim().Length > 20)
            {
                return "판매 가능 수량은 1,000,000 이하이고 단위는 20자 이하여야 합니다.";
            }

            if (SalesUnitPrice is <= 0 or > 1_000_000_000)
            {
                return "상품 가격은 0보다 크고 1,000,000,000 이하여야 합니다.";
            }

            var currencyCode = SalesCurrencyCode?.Trim() ?? string.Empty;
            if (currencyCode.Length != 3 || currencyCode.Any(character => !char.IsAsciiLetter(character)))
            {
                return "통화 코드는 KRW, USD처럼 영문 세 자리로 입력하세요.";
            }

            if (!AcceptsTossPayments && !AcceptsNaverPay && !AcceptsPayPal && !AcceptsDirectCash)
            {
                return "협의 가능한 결제 방법을 하나 이상 선택하세요.";
            }

        }

        if (IsAuthorDisplayCountryPublic
            && (AuthorDisplayCountryCode.Trim().Length != 2
                || AuthorDisplayCountryCode.Any(character => !char.IsAsciiLetter(character))
                || string.IsNullOrWhiteSpace(AuthorDisplayCountryName)
                || AuthorDisplayCountryName.Trim().Length > 80))
        {
            return "활동 국가를 공개하려면 ISO 알파-2 영문 국가 코드와 80자 이하의 국가 이름을 입력하세요.";
        }

        return null;
    }

    public void Reset(string defaultRoleTag)
    {
        Nickname = string.Empty;
        IsAuthorDisplayCountryPublic = false;
        AuthorDisplayCountryCode = string.Empty;
        AuthorDisplayCountryName = string.Empty;
        Password = string.Empty;
        IsSalesPost = false;
        Category = PlatformCommunityPostCategories.General;
        WorkflowTag = "커뮤니티 신뢰";
        RoleTag = defaultRoleTag;
        Title = string.Empty;
        Body = string.Empty;
        SharedLinkUrl = string.Empty;
        SalesProductTitle = string.Empty;
        SalesAvailableQuantity = 1;
        SalesQuantityUnit = "개";
        SalesUnitPrice = 0;
        SalesCurrencyCode = "KRW";
        AcceptsTossPayments = false;
        AcceptsNaverPay = false;
        AcceptsPayPal = false;
        AcceptsDirectCash = true;
        AllowsGroupPurchase = true;
        SalesStatus = PlatformCommunitySalesOfferStatuses.Open;
        커뮤니티원장Id = string.Empty;
        IsReportBoardPost = false;
        ReporterDisplayName = string.Empty;
        ReportedDisplayName = string.Empty;
    }

    public void Apply(PlatformCommunityPostResponse post)
    {
        Nickname = post.Nickname;
        IsAuthorDisplayCountryPublic = post.IsAuthorDisplayCountryPublic;
        AuthorDisplayCountryCode = post.AuthorDisplayCountryCode ?? string.Empty;
        AuthorDisplayCountryName = post.AuthorDisplayCountryName ?? string.Empty;
        Password = string.Empty;
        IsSalesPost = false;
        Category = post.Category;
        WorkflowTag = post.WorkflowTag;
        RoleTag = post.RoleTag;
        Title = post.Title;
        Body = post.Body;
        SharedLinkUrl = post.SharedLinkUrl ?? string.Empty;
        Apply(post.SalesOffer);
        커뮤니티원장Id = post.커뮤니티원장Id ?? string.Empty;
        IsReportBoardPost = string.Equals(post.Category, "신고/분쟁", StringComparison.OrdinalIgnoreCase)
                            || post.IsReportBoardPost;
        ReporterDisplayName = post.ReporterDisplayName;
        ReportedDisplayName = post.ReportedDisplayName;
    }

    public void Apply(CommunityPostComposerSnapshot snapshot)
    {
        Nickname = snapshot.Nickname;
        IsAuthorDisplayCountryPublic = snapshot.IsAuthorDisplayCountryPublic;
        AuthorDisplayCountryCode = snapshot.AuthorDisplayCountryCode;
        AuthorDisplayCountryName = snapshot.AuthorDisplayCountryName;
        Password = string.Empty;
        IsSalesPost = false;
        Category = snapshot.Category;
        WorkflowTag = snapshot.WorkflowTag;
        RoleTag = snapshot.RoleTag;
        Title = snapshot.Title;
        Body = snapshot.Body;
        SharedLinkUrl = snapshot.SharedLinkUrl;
        IsSalesPost = snapshot.IsSalesPost;
        SalesProductTitle = snapshot.SalesProductTitle;
        SalesAvailableQuantity = snapshot.SalesAvailableQuantity;
        SalesQuantityUnit = snapshot.SalesQuantityUnit;
        SalesUnitPrice = snapshot.SalesUnitPrice;
        SalesCurrencyCode = snapshot.SalesCurrencyCode;
        AcceptsTossPayments = snapshot.AcceptsTossPayments;
        AcceptsNaverPay = snapshot.AcceptsNaverPay;
        AcceptsPayPal = snapshot.AcceptsPayPal;
        AcceptsDirectCash = snapshot.AcceptsDirectCash;
        AllowsGroupPurchase = snapshot.AllowsGroupPurchase;
        SalesStatus = snapshot.SalesStatus;
        커뮤니티원장Id = snapshot.커뮤니티원장Id;
        IsReportBoardPost = snapshot.IsReportBoardPost;
        ReporterDisplayName = snapshot.ReporterDisplayName;
        ReportedDisplayName = snapshot.ReportedDisplayName;
    }

    public CommunityPostComposerSnapshot CreateSnapshot(DateTime savedAtUtc)
        => new()
        {
            SavedAtUtc = savedAtUtc,
            Nickname = Nickname,
            IsAuthorDisplayCountryPublic = IsAuthorDisplayCountryPublic,
            AuthorDisplayCountryCode = AuthorDisplayCountryCode,
            AuthorDisplayCountryName = AuthorDisplayCountryName,
            Category = PlatformCommunityPostCategoryPolicy.Resolve(Category, IsSalesPost),
            WorkflowTag = WorkflowTag,
            RoleTag = RoleTag,
            Title = Title,
            Body = Body,
            SharedLinkUrl = SharedLinkUrl,
            IsSalesPost = IsSalesPost,
            SalesProductTitle = SalesProductTitle,
            SalesAvailableQuantity = SalesAvailableQuantity,
            SalesQuantityUnit = SalesQuantityUnit,
            SalesUnitPrice = SalesUnitPrice,
            SalesCurrencyCode = SalesCurrencyCode,
            AcceptsTossPayments = AcceptsTossPayments,
            AcceptsNaverPay = AcceptsNaverPay,
            AcceptsPayPal = AcceptsPayPal,
            AcceptsDirectCash = AcceptsDirectCash,
            AllowsGroupPurchase = AllowsGroupPurchase,
            SalesStatus = SalesStatus,
            커뮤니티원장Id = 커뮤니티원장Id,
            IsReportBoardPost = !IsSalesPost && IsReportBoardPost,
            ReporterDisplayName = ReporterDisplayName,
            ReportedDisplayName = ReportedDisplayName
        };

    public PlatformCommunityPostCreateRequest CreateRequest(string appKey)
        => new()
        {
            AppKey = appKey,
            Category = PlatformCommunityPostCategoryPolicy.Resolve(Category, IsSalesPost),
            WorkflowTag = WorkflowTag,
            RoleTag = RoleTag,
            Title = Title,
            Body = Body,
            SharedLinkUrl = SharedLinkUrl,
            SalesOffer = CreateSalesOfferRequest(),
            커뮤니티원장Id = 커뮤니티원장Id,
            Nickname = Nickname,
            IsAuthorDisplayCountryPublic = IsAuthorDisplayCountryPublic,
            AuthorDisplayCountryCode = AuthorDisplayCountryCode,
            AuthorDisplayCountryName = AuthorDisplayCountryName,
            IsReportBoardPost = !IsSalesPost && IsReportBoardPost,
            ReporterDisplayName = ReporterDisplayName,
            ReportedDisplayName = ReportedDisplayName,
            Password = Password
        };

    public PlatformCommunityPostUpdateRequest CreateUpdateRequest()
        => new()
        {
            Category = PlatformCommunityPostCategoryPolicy.Resolve(Category, IsSalesPost),
            WorkflowTag = WorkflowTag,
            RoleTag = RoleTag,
            Title = Title,
            Body = Body,
            SharedLinkUrl = SharedLinkUrl,
            SalesOffer = CreateSalesOfferRequest(),
            커뮤니티원장Id = 커뮤니티원장Id,
            Nickname = Nickname,
            IsAuthorDisplayCountryPublic = IsAuthorDisplayCountryPublic,
            AuthorDisplayCountryCode = AuthorDisplayCountryCode,
            AuthorDisplayCountryName = AuthorDisplayCountryName,
            IsReportBoardPost = !IsSalesPost && IsReportBoardPost,
            ReporterDisplayName = ReporterDisplayName,
            ReportedDisplayName = ReportedDisplayName,
            Password = Password
        };

    private void Apply(PlatformCommunityPostSalesOfferResponse? salesOffer)
    {
        IsSalesPost = salesOffer is not null;
        SalesProductTitle = salesOffer?.ProductTitle ?? string.Empty;
        SalesAvailableQuantity = salesOffer?.AvailableQuantity ?? 1;
        SalesQuantityUnit = salesOffer?.QuantityUnit ?? "개";
        SalesUnitPrice = salesOffer?.UnitPrice ?? 0;
        SalesCurrencyCode = salesOffer?.CurrencyCode ?? "KRW";
        AcceptsTossPayments = AcceptsPaymentMethod(salesOffer, PlatformCommunitySalesPaymentMethodCodes.TossPayments);
        AcceptsNaverPay = AcceptsPaymentMethod(salesOffer, PlatformCommunitySalesPaymentMethodCodes.NaverPay);
        AcceptsPayPal = AcceptsPaymentMethod(salesOffer, PlatformCommunitySalesPaymentMethodCodes.PayPal);
        AcceptsDirectCash = salesOffer is null
            || AcceptsPaymentMethod(salesOffer, PlatformCommunitySalesPaymentMethodCodes.DirectCash);
        AllowsGroupPurchase = salesOffer?.AllowsGroupPurchase ?? true;
        SalesStatus = salesOffer?.Status ?? PlatformCommunitySalesOfferStatuses.Open;
    }

    private PlatformCommunityPostSalesOfferRequest? CreateSalesOfferRequest()
    {
        if (!IsSalesPost)
        {
            return null;
        }

        var paymentMethods = new List<string>();
        if (AcceptsTossPayments) paymentMethods.Add(PlatformCommunitySalesPaymentMethodCodes.TossPayments);
        if (AcceptsNaverPay) paymentMethods.Add(PlatformCommunitySalesPaymentMethodCodes.NaverPay);
        if (AcceptsPayPal) paymentMethods.Add(PlatformCommunitySalesPaymentMethodCodes.PayPal);
        if (AcceptsDirectCash) paymentMethods.Add(PlatformCommunitySalesPaymentMethodCodes.DirectCash);

        return new PlatformCommunityPostSalesOfferRequest
        {
            ProductTitle = SalesProductTitle,
            AvailableQuantity = SalesAvailableQuantity,
            QuantityUnit = SalesQuantityUnit,
            UnitPrice = SalesUnitPrice,
            CurrencyCode = SalesCurrencyCode,
            AcceptedPaymentMethods = paymentMethods,
            AllowsGroupPurchase = AllowsGroupPurchase,
            Status = SalesStatus
        };
    }

    private static bool AcceptsPaymentMethod(
        PlatformCommunityPostSalesOfferResponse? salesOffer,
        string paymentMethod)
        => salesOffer?.AcceptedPaymentMethods.Contains(paymentMethod, StringComparer.OrdinalIgnoreCase) == true;
}
