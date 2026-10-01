namespace Hongdal.Contracts.Common.Community;

public static class CommunityDynamicTopicDomainCodes
{
    public const string Warehouse = "warehouse";
    public const string Order = "order";
    public const string Sales = "sales";
    public const string Transport = "transport";

    public static IReadOnlyList<string> All { get; } = [Warehouse, Order, Sales, Transport];
}

public static class CommunityDynamicTopicCodes
{
    public const string WarehouseInbound = "warehouse-inbound";
    public const string WarehouseOutbound = "warehouse-outbound";
    public const string IndividualOrder = "order-individual";
    public const string GroupOrder = "order-group";
    public const string Food = "sales-food";
    public const string Cargo = "sales-cargo";
    public const string TransportLoading = "transport-loading";
    public const string TransportUnloading = "transport-unloading";

    public const string LegacyFood = "food";
    public const string LegacyCargo = "cargo";

    public static IReadOnlyList<string> All { get; } =
    [
        WarehouseInbound,
        WarehouseOutbound,
        IndividualOrder,
        GroupOrder,
        Food,
        Cargo,
        TransportLoading,
        TransportUnloading
    ];

    public static bool IsSupported(string? value)
        => Normalize(value) is not null;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();
        if (string.Equals(candidate, LegacyFood, StringComparison.OrdinalIgnoreCase))
        {
            return Food;
        }

        if (string.Equals(candidate, LegacyCargo, StringComparison.OrdinalIgnoreCase))
        {
            return Cargo;
        }

        return All.FirstOrDefault(code => string.Equals(code, candidate, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record CommunityDynamicTopicDefinition(
    string DomainKey,
    string DomainDisplayName,
    string TopicKey,
    string DisplayName,
    string Summary,
    int SortOrder);

public static class CommunityDynamicTopicCatalog
{
    public static IReadOnlyList<CommunityDynamicTopicDefinition> All { get; } =
    [
        new(CommunityDynamicTopicDomainCodes.Warehouse, "창고", CommunityDynamicTopicCodes.WarehouseInbound,
            "입고", "입고 예정·접수·검수·입고 원장에 관한 글을 모아봅니다.", 10),
        new(CommunityDynamicTopicDomainCodes.Warehouse, "창고", CommunityDynamicTopicCodes.WarehouseOutbound,
            "출고", "출고 예정·피킹·포장·출고 원장에 관한 글을 모아봅니다.", 20),
        new(CommunityDynamicTopicDomainCodes.Order, "주문", CommunityDynamicTopicCodes.IndividualOrder,
            "개별주문", "한 주문자의 개별 주문과 수령 흐름에 관한 글을 모아봅니다.", 30),
        new(CommunityDynamicTopicDomainCodes.Order, "주문", CommunityDynamicTopicCodes.GroupOrder,
            "공동주문", "여러 개별 주문이 합쳐지는 공동주문·공동구매 흐름의 글을 모아봅니다.", 40),
        new(CommunityDynamicTopicDomainCodes.Sales, "판매", CommunityDynamicTopicCodes.Food,
            "음식", "음식·식당·식재료 판매에 관한 글과 동의 기반 주변 정보를 모아봅니다.", 50),
        new(CommunityDynamicTopicDomainCodes.Sales, "판매", CommunityDynamicTopicCodes.Cargo,
            "화물", "판매·양도 또는 운송이 필요한 화물에 관한 공개 글을 모아봅니다.", 60),
        new(CommunityDynamicTopicDomainCodes.Transport, "운송", CommunityDynamicTopicCodes.TransportLoading,
            "상차", "픽업·상차 예정·상차 확인에 관한 글을 모아봅니다.", 70),
        new(CommunityDynamicTopicDomainCodes.Transport, "운송", CommunityDynamicTopicCodes.TransportUnloading,
            "하차", "도착·하차 예정·인수 확인에 관한 글을 모아봅니다.", 80)
    ];

    public static CommunityDynamicTopicDefinition? Find(string? topicKey)
    {
        var normalized = CommunityDynamicTopicCodes.Normalize(topicKey);
        return normalized is null
            ? null
            : All.First(definition => definition.TopicKey == normalized);
    }
}

public sealed class CommunityDynamicTopicCatalogResponse
{
    public string GenerationPolicy { get; set; } = string.Empty;
    public IReadOnlyList<CommunityDynamicTopicDomainResponse> Domains { get; set; } = [];
}

public sealed class CommunityDynamicTopicDomainResponse
{
    public string DomainKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<CommunityDynamicTopicResponse> Topics { get; set; } = [];
}

public sealed class CommunityDynamicTopicResponse
{
    public string DomainKey { get; set; } = string.Empty;
    public string DomainDisplayName { get; set; } = string.Empty;
    public string TopicKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsDerivedFromPost { get; set; } = true;
    public string FeedEndpoint { get; set; } = string.Empty;
    public IReadOnlyList<string> MatchedSignals { get; set; } = [];
}

public sealed class CommunityDynamicTopicFeedResponse
{
    public string DomainKey { get; set; } = string.Empty;
    public string DomainDisplayName { get; set; } = string.Empty;
    public string TopicKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GenerationPolicy { get; set; } = string.Empty;
    public IReadOnlyList<CommunityDynamicTopicFeedItemResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class CommunityDynamicTopicFeedItemResponse
{
    public long PostId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyList<string> MatchedSignals { get; set; } = [];
}

public sealed class CommunityPostContextDiscoveryRequest
{
    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }
    public decimal RadiusKm { get; set; } = 7m;
    public bool ConfirmTransientLocationUse { get; set; }
}

public sealed class CommunityPostContextDiscoveryResponse
{
    public long PostId { get; set; }
    public IReadOnlyList<CommunityDynamicTopicResponse> DynamicTopics { get; set; } = [];
    public CommunityTransientLocationPolicyResponse LocationPolicy { get; set; } = new();
    public IReadOnlyList<CommunityNearbyRestaurantCandidateResponse> NearbyRestaurants { get; set; } = [];
    public IReadOnlyList<CommunityFreightProviderCandidateResponse> FreightProviderCandidates { get; set; } = [];
    public IReadOnlyList<CommunityPublicFreightCandidateResponse> PublicFreightCandidates { get; set; } = [];
    public bool InformationOnly { get; set; } = true;
    public bool IsBrokerageEnabled { get; set; }
    public bool AutomaticallySelectsProvider { get; set; }
    public bool AutomaticallyDispatchesFreight { get; set; }
    public string FacilitatorBoundaryNotice { get; set; } = string.Empty;
}

public sealed class CommunityTransientLocationPolicyResponse
{
    public decimal MaximumRadiusKm { get; set; } = 7m;
    public decimal AppliedRadiusKm { get; set; } = 7m;
    public bool RequiresExplicitConsent { get; set; } = true;
    public bool ConsentConfirmed { get; set; }
    public bool LocationProvided { get; set; }
    public bool LocationPersisted { get; set; }
    public bool RestaurantSourceAvailable { get; set; }
    public bool RestaurantSourceIsSimulation { get; set; } = true;
    public string Notice { get; set; } = string.Empty;
}

public sealed class CommunityNearbyRestaurantCandidateResponse
{
    public long RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AreaSummary { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool OrderAvailable { get; set; }
    public string SourceCode { get; set; } = string.Empty;
}

public sealed class CommunityFreightProviderCandidateResponse
{
    public string CandidateKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public bool PlatformRoleVerified { get; set; }
    public bool ExternalLicenseVerificationRequired { get; set; } = true;
    public string VerificationNotice { get; set; } = string.Empty;
}

public sealed class CommunityPublicFreightCandidateResponse
{
    public string CandidateKey { get; set; } = string.Empty;
    public string CargoType { get; set; } = string.Empty;
    public decimal? CargoWeightKg { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public string PickupAreaSummary { get; set; } = string.Empty;
    public string DropoffAreaSummary { get; set; } = string.Empty;
    public DateTime? PickupWindowStartUtc { get; set; }
    public bool IsExplicitPublicDispatch { get; set; }
    public string Notice { get; set; } = string.Empty;
}
