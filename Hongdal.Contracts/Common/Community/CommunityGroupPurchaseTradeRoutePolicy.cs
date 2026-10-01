namespace Hongdal.Contracts.Common.Community;

/// <summary>
/// 공동구매 제안에서 상품의 실제 이동 경로를 판정할 때 사용하는 입력입니다.
/// 판매자 국적이 아니라 상품 출발지, 최종 배송지와 국내 통관 상태를 기준으로 합니다.
/// </summary>
public sealed record CommunityGroupPurchaseTradeRouteInput(
    string? SellerCountryCode,
    string? ShipFromCountryCode,
    string? DeliveryCountryCode,
    string? CustomsClearanceStatusCode,
    string? OperatingMarketCountryCode = null);

public sealed record CommunityGroupPurchaseTradeRouteDecision(
    string RouteCode,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> MissingFieldCodes,
    IReadOnlyList<string> InvalidFieldCodes)
{
    public bool IsGroupImportCandidate
        => CommunityGroupPurchaseTradeRouteCodes.IsGroupImport(RouteCode);

    public bool RequiresManualReview
        => string.Equals(
            RouteCode,
            CommunityGroupPurchaseTradeRouteCodes.ReviewRequired,
            StringComparison.OrdinalIgnoreCase);
}

public static class CommunityGroupPurchaseTradeRoutePolicy
{
    public const string KoreaCountryCode = "KR";

    public const string UnitedStatesCountryCode = "US";

    public const string GroupImportCandidateNotice =
        "공동수입 후보 판정은 제안 단계의 물류 경로 분류입니다. 계약 확정 전 상품 출발국가, 운영 국가의 통관 여부와 HS 코드를 다시 확인하고, 확정된 경우에만 원천 공동구매와 연결된 별도 공동수입 원장으로 인계합니다.";

    public static CommunityGroupPurchaseTradeRouteDecision Evaluate(
        CommunityGroupPurchaseTradeRouteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var sellerCountryCode = NormalizeCountryCode(input.SellerCountryCode);
        var shipFromCountryCode = NormalizeCountryCode(input.ShipFromCountryCode);
        var deliveryCountryCode = NormalizeCountryCode(input.DeliveryCountryCode);
        var operatingMarketCountryCode = NormalizeOperatingMarketCountryCode(
            input.OperatingMarketCountryCode);
        var customsClearanceStatusCode = NormalizeCustomsClearanceStatusCode(
            input.CustomsClearanceStatusCode);

        var reasons = new List<string>();
        var missingFields = new List<string>();
        var invalidFields = new List<string>();

        AddInvalidCountryCode(
            sellerCountryCode,
            CommunityGroupPurchaseTradeRouteFieldCodes.SellerCountryCode,
            invalidFields);
        AddInvalidCountryCode(
            shipFromCountryCode,
            CommunityGroupPurchaseTradeRouteFieldCodes.ShipFromCountryCode,
            invalidFields);
        AddInvalidCountryCode(
            deliveryCountryCode,
            CommunityGroupPurchaseTradeRouteFieldCodes.DeliveryCountryCode,
            invalidFields);

        if (!string.IsNullOrWhiteSpace(customsClearanceStatusCode)
            && !CommunityGroupPurchaseCustomsClearanceStatusCodes.IsSupported(
                customsClearanceStatusCode))
        {
            invalidFields.Add(CommunityGroupPurchaseTradeRouteFieldCodes.CustomsClearanceStatusCode);
        }

        if (invalidFields.Count > 0)
        {
            reasons.Add(CommunityGroupPurchaseTradeRouteReasonCodes.InvalidTradeRouteInput);
            return ReviewRequired(reasons, missingFields, invalidFields);
        }

        if (string.IsNullOrWhiteSpace(shipFromCountryCode))
        {
            missingFields.Add(CommunityGroupPurchaseTradeRouteFieldCodes.ShipFromCountryCode);
        }

        if (string.IsNullOrWhiteSpace(deliveryCountryCode))
        {
            missingFields.Add(CommunityGroupPurchaseTradeRouteFieldCodes.DeliveryCountryCode);
        }

        if (missingFields.Count > 0)
        {
            reasons.Add(CommunityGroupPurchaseTradeRouteReasonCodes.IncompleteTradeRouteInput);
            return ReviewRequired(reasons, missingFields, invalidFields);
        }

        if (!string.IsNullOrWhiteSpace(sellerCountryCode)
            && !string.Equals(
                sellerCountryCode,
                operatingMarketCountryCode,
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(OperatingMarketReasonCode(
                operatingMarketCountryCode,
                CommunityGroupPurchaseTradeRouteReasonCodes.SellerOutsideKorea,
                CommunityGroupPurchaseTradeRouteReasonCodes.SellerOutsideOperatingMarket));
        }

        if (string.Equals(
                shipFromCountryCode,
                deliveryCountryCode,
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(CommunityGroupPurchaseTradeRouteReasonCodes.SameCountryFulfillment);
            return new CommunityGroupPurchaseTradeRouteDecision(
                string.Equals(
                    deliveryCountryCode,
                    operatingMarketCountryCode,
                    StringComparison.OrdinalIgnoreCase)
                    ? CommunityGroupPurchaseTradeRouteCodes.Domestic
                    : CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder,
                reasons,
                missingFields,
                invalidFields);
        }

        if (!string.Equals(
                deliveryCountryCode,
                operatingMarketCountryCode,
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(OperatingMarketReasonCode(
                operatingMarketCountryCode,
                CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryOutsideKorea,
                CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryOutsideOperatingMarket));
            return new CommunityGroupPurchaseTradeRouteDecision(
                CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder,
                reasons,
                missingFields,
                invalidFields);
        }

        reasons.Add(OperatingMarketReasonCode(
            operatingMarketCountryCode,
            CommunityGroupPurchaseTradeRouteReasonCodes.GoodsShipFromOutsideKorea,
            CommunityGroupPurchaseTradeRouteReasonCodes.GoodsShipFromOutsideOperatingMarket));
        reasons.Add(OperatingMarketReasonCode(
            operatingMarketCountryCode,
            CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryToKorea,
            CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryToOperatingMarket));

        if (string.Equals(
                customsClearanceStatusCode,
                CommunityGroupPurchaseCustomsClearanceStatusCodes.Cleared,
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(CommunityGroupPurchaseTradeRouteReasonCodes.AlreadyCustomsCleared);
            return new CommunityGroupPurchaseTradeRouteDecision(
                CommunityGroupPurchaseTradeRouteCodes.Domestic,
                reasons,
                missingFields,
                invalidFields);
        }

        if (string.Equals(
                customsClearanceStatusCode,
                CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared,
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(CommunityGroupPurchaseTradeRouteReasonCodes.CustomsClearanceRequired);
            return new CommunityGroupPurchaseTradeRouteDecision(
                CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate,
                reasons,
                missingFields,
                invalidFields);
        }

        missingFields.Add(CommunityGroupPurchaseTradeRouteFieldCodes.CustomsClearanceStatusCode);
        reasons.Add(CommunityGroupPurchaseTradeRouteReasonCodes.CustomsClearanceStatusRequired);
        return ReviewRequired(reasons, missingFields, invalidFields);
    }

    public static string NormalizeCountryCode(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();

    public static string NormalizeOperatingMarketCountryCode(string? value)
    {
        var normalized = NormalizeCountryCode(value);
        return IsValidCountryCode(normalized) ? normalized : KoreaCountryCode;
    }

    public static bool IsValidCountryCode(string? value)
    {
        var normalized = NormalizeCountryCode(value);
        return normalized.Length == 2 && normalized.All(character => character is >= 'A' and <= 'Z');
    }

    public static string NormalizeCustomsClearanceStatusCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown;
        }

        var normalized = value.Trim();
        if (string.Equals(
                normalized,
                CommunityGroupPurchaseCustomsClearanceStatusCodes.Cleared,
                StringComparison.OrdinalIgnoreCase))
        {
            return CommunityGroupPurchaseCustomsClearanceStatusCodes.Cleared;
        }

        if (string.Equals(
                normalized,
                CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared,
                StringComparison.OrdinalIgnoreCase))
        {
            return CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared;
        }

        if (string.Equals(
                normalized,
                CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown,
                StringComparison.OrdinalIgnoreCase))
        {
            return CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown;
        }

        return normalized;
    }

    private static void AddInvalidCountryCode(
        string countryCode,
        string fieldCode,
        ICollection<string> invalidFields)
    {
        if (!string.IsNullOrWhiteSpace(countryCode) && !IsValidCountryCode(countryCode))
        {
            invalidFields.Add(fieldCode);
        }
    }

    private static CommunityGroupPurchaseTradeRouteDecision ReviewRequired(
        IReadOnlyList<string> reasons,
        IReadOnlyList<string> missingFields,
        IReadOnlyList<string> invalidFields)
        => new(
            CommunityGroupPurchaseTradeRouteCodes.ReviewRequired,
            reasons,
            missingFields,
            invalidFields);

    private static string OperatingMarketReasonCode(
        string operatingMarketCountryCode,
        string koreaReasonCode,
        string genericReasonCode)
        => string.Equals(
            operatingMarketCountryCode,
            KoreaCountryCode,
            StringComparison.OrdinalIgnoreCase)
            ? koreaReasonCode
            : genericReasonCode;
}

public static class CommunityGroupPurchaseTradeRouteCodes
{
    public const string Domestic = "DomesticGroupPurchase";

    public const string InboundGroupImportCandidate = "InboundGroupImportCandidate";

    public const string OtherCrossBorder = "OtherCrossBorderTrade";

    public const string ReviewRequired = "TradeRouteReviewRequired";

    public static bool IsSupported(string? value)
        => value is Domestic or InboundGroupImportCandidate or OtherCrossBorder or ReviewRequired;

    public static bool IsGroupImport(string? value)
        => string.Equals(
            value,
            InboundGroupImportCandidate,
            StringComparison.OrdinalIgnoreCase);
}

public static class CommunityGroupPurchaseCustomsClearanceStatusCodes
{
    public const string Unknown = "Unknown";

    public const string NotCleared = "NotCleared";

    public const string Cleared = "Cleared";

    public static bool IsSupported(string? value)
        => value is Unknown or NotCleared or Cleared;
}

public static class CommunityGroupPurchaseTradeRouteFieldCodes
{
    public const string SellerCountryCode = "SellerCountryCode";

    public const string ShipFromCountryCode = "ShipFromCountryCode";

    public const string DeliveryCountryCode = "DeliveryCountryCode";

    public const string CustomsClearanceStatusCode = "CustomsClearanceStatusCode";

    public const string HsCode = "HsCode";
}

public static class CommunityGroupPurchaseTradeRouteReasonCodes
{
    public const string SellerOutsideKorea = "SellerOutsideKorea";

    public const string SameCountryFulfillment = "SameCountryFulfillment";

    public const string GoodsShipFromOutsideKorea = "GoodsShipFromOutsideKorea";

    public const string DeliveryToKorea = "DeliveryToKorea";

    public const string DeliveryOutsideKorea = "DeliveryOutsideKorea";

    public const string AlreadyCustomsCleared = "AlreadyCustomsCleared";

    public const string CustomsClearanceRequired = "CustomsClearanceRequired";

    public const string CustomsClearanceStatusRequired = "CustomsClearanceStatusRequired";

    public const string IncompleteTradeRouteInput = "IncompleteTradeRouteInput";

    public const string InvalidTradeRouteInput = "InvalidTradeRouteInput";

    public const string SellerOutsideOperatingMarket = "SellerOutsideOperatingMarket";

    public const string GoodsShipFromOutsideOperatingMarket =
        "GoodsShipFromOutsideOperatingMarket";

    public const string DeliveryToOperatingMarket = "DeliveryToOperatingMarket";

    public const string DeliveryOutsideOperatingMarket = "DeliveryOutsideOperatingMarket";
}
