using FluentResults;
using Hongdal.Contracts.Common.Community;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Hongdal.Services.Community;

internal static class CommunityPostWritePolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? ValidatePost(
        string? nickname,
        string? password,
        string? title,
        string? body,
        string? sharedLinkUrl,
        PlatformCommunityPostSalesOfferRequest? salesOffer,
        bool requiresSuppliedNickname)
    {
        if ((requiresSuppliedNickname && string.IsNullOrWhiteSpace(nickname))
            || (!string.IsNullOrWhiteSpace(nickname) && nickname.Trim().Length > 40))
        {
            return "닉네임은 1자 이상 40자 이하로 입력해야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(password)
            || password.Trim().Length < 4
            || password.Trim().Length > 100)
        {
            return "비밀번호는 4자 이상 100자 이하로 입력해야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160)
        {
            return "제목은 1자 이상 160자 이하로 입력해야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(body)
            && string.IsNullOrWhiteSpace(sharedLinkUrl)
            && salesOffer is null)
        {
            return "본문, 공유 링크 또는 판매 정보 중 하나는 입력해야 합니다.";
        }

        if (!string.IsNullOrWhiteSpace(body) && body.Trim().Length > 4000)
        {
            return "본문은 1자 이상 4000자 이하로 입력해야 합니다.";
        }

        if (!string.IsNullOrWhiteSpace(sharedLinkUrl)
            && (!Uri.TryCreate(sharedLinkUrl.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || sharedLinkUrl.Trim().Length > 1000))
        {
            return "공유 링크는 http 또는 https URL로 입력해야 합니다.";
        }

        return ValidateSalesOffer(salesOffer);
    }

    public static string? ValidateAuthorDisplayCountry(
        bool isPublic,
        string? countryCode,
        string? countryName)
    {
        if (!isPublic)
        {
            return null;
        }

        var code = countryCode?.Trim() ?? string.Empty;
        if (code.Length != 2 || code.Any(character => !char.IsAsciiLetter(character)))
        {
            return "활동 국가 코드는 ISO 알파-2 영문 두 자리로 입력해야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(countryName) || countryName.Trim().Length > 80)
        {
            return "공개할 활동 국가 이름은 1자 이상 80자 이하로 입력해야 합니다.";
        }

        return null;
    }

    public static Result<T> WriteRejected<T>(string category, string? userId)
    {
        var board = CommunityBoardCatalog.Find(category);
        var loginRequired = board?.RequiresAuthenticatedPosting == true || board is null;
        if (loginRequired && string.IsNullOrWhiteSpace(userId))
        {
            return Result.Fail<T>(new Error(
                    "이 게시판은 로그인한 사용자만 글을 작성할 수 있습니다. 공개 화면에는 실명 대신 닉네임이 표시됩니다.")
                .WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));
        }

        return Result.Fail<T>(
            "사용자 작성이 허용된 기본 게시판 또는 운영자가 승인한 사용자 게시판에만 글을 작성할 수 있습니다.");
    }

    public static string ResolvePostCategory(
        string? requestedCategory,
        PlatformCommunityPostSalesOfferRequest? salesOffer)
        => Normalize(
            PlatformCommunityPostCategoryPolicy.Resolve(requestedCategory, salesOffer is not null),
            PlatformCommunityPostCategories.General,
            60);

    public static string? SerializeSalesOffer(PlatformCommunityPostSalesOfferRequest? source)
    {
        if (source is null)
        {
            return null;
        }

        var normalized = new PlatformCommunityPostSalesOfferResponse
        {
            ProductTitle = Normalize(source.ProductTitle, string.Empty, 160),
            AvailableQuantity = source.AvailableQuantity,
            QuantityUnit = Normalize(source.QuantityUnit, "개", 20),
            UnitPrice = source.UnitPrice,
            CurrencyCode = source.CurrencyCode.Trim().ToUpperInvariant(),
            AcceptedPaymentMethods = (source.AcceptedPaymentMethods ?? [])
                .Where(method => !string.IsNullOrWhiteSpace(method))
                .Select(method => method.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AllowsGroupPurchase = source.AllowsGroupPurchase,
            Status = source.Status.Trim().ToLowerInvariant()
        };
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static string Normalize(string? value, string fallback, int maxLength)
        => CommunityPostingIdentityPolicy.Normalize(value, fallback, maxLength);

    public static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    public static string? NormalizeOptionalUrl(string? value)
        => NormalizeOptional(value, 1000);

    public static string NormalizeCountryCode(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsReportCategory(string? category)
        => !string.IsNullOrWhiteSpace(category)
           && (category.Contains("신고", StringComparison.OrdinalIgnoreCase)
               || category.Contains("분쟁", StringComparison.OrdinalIgnoreCase)
               || category.Contains("report", StringComparison.OrdinalIgnoreCase));

    public static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static string? ValidateSalesOffer(PlatformCommunityPostSalesOfferRequest? salesOffer)
    {
        if (salesOffer is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(salesOffer.ProductTitle)
            || salesOffer.ProductTitle.Trim().Length > 160)
        {
            return "판매 상품명은 1자 이상 160자 이하로 입력해야 합니다.";
        }

        if (salesOffer.AvailableQuantity <= 0 || salesOffer.AvailableQuantity > 1_000_000)
        {
            return "판매 가능 수량은 0보다 크고 1,000,000 이하여야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(salesOffer.QuantityUnit)
            || salesOffer.QuantityUnit.Trim().Length > 20)
        {
            return "수량 단위는 1자 이상 20자 이하로 입력해야 합니다.";
        }

        if (salesOffer.UnitPrice <= 0 || salesOffer.UnitPrice > 1_000_000_000)
        {
            return "판매 가격은 0보다 크고 1,000,000,000 이하여야 합니다.";
        }

        var currencyCode = salesOffer.CurrencyCode?.Trim() ?? string.Empty;
        if (currencyCode.Length != 3 || currencyCode.Any(character => !char.IsAsciiLetter(character)))
        {
            return "통화 코드는 KRW, USD처럼 ISO 영문 세 자리로 입력해야 합니다.";
        }

        var paymentMethods = (salesOffer.AcceptedPaymentMethods ?? [])
            .Where(method => !string.IsNullOrWhiteSpace(method))
            .Select(method => method.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paymentMethods.Length == 0)
        {
            return "협의 가능한 결제 방법을 하나 이상 선택해야 합니다.";
        }

        if (paymentMethods.Any(method =>
                !PlatformCommunitySalesPaymentMethodCodes.All.Contains(method, StringComparer.OrdinalIgnoreCase)))
        {
            return "지원하지 않는 결제 방법이 포함되어 있습니다.";
        }

        if (string.IsNullOrWhiteSpace(salesOffer.Status)
            || !PlatformCommunitySalesOfferStatuses.All.Contains(
                salesOffer.Status,
                StringComparer.OrdinalIgnoreCase))
        {
            return "판매 상태가 올바르지 않습니다.";
        }

        return null;
    }
}
