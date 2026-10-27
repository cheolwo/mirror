using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Community;

public static class 커뮤니티세계지도원장보호FieldGroupCodes
{
    public const string PersonalIdentity = "personal-identity";
    public const string Contact = "contact";
    public const string PreciseLocation = "precise-location";
    public const string IndividualQuantity = "individual-quantity";
    public const string CommercialTerms = "commercial-terms";
    public const string InventoryAndLot = "inventory-and-lot";
    public const string RawLedgerData = "raw-ledger-data";

    public static IReadOnlyList<string> All { get; } =
    [
        PersonalIdentity,
        Contact,
        PreciseLocation,
        IndividualQuantity,
        CommercialTerms,
        InventoryAndLot,
        RawLedgerData
    ];
}

/// <summary>
/// 원장 template별 지도 projection의 최대 공개 범위입니다.
/// 실제 응답은 현재 상태, viewer 권한, 동의와 집계 임계값을 다시 확인해 이 범위보다 더 축소할 수 있습니다.
/// </summary>
public sealed record 커뮤니티세계지도원장ProjectionPolicyRule(
    string LedgerTemplateKey,
    bool AllowsPublicProjection,
    string PublicLocationModeCode,
    int? MinimumPublicAggregateCount,
    IReadOnlyList<string> PublicStatusCodes,
    IReadOnlyList<string> PublicActionCodes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MaximumActionCodesByViewerScope,
    IReadOnlyList<string> ForbiddenFieldGroupCodes);

public static class 커뮤니티세계지도원장ProjectionPolicy
{
    private static readonly IReadOnlyList<string> 공개집계상태 =
    [
        커뮤니티세계지도원장공개상태Codes.Proposed,
        커뮤니티세계지도원장공개상태Codes.Active,
        커뮤니티세계지도원장공개상태Codes.OnHold,
        커뮤니티세계지도원장공개상태Codes.Completed
    ];

    private static readonly IReadOnlyList<string> 운영완료집계상태 =
    [
        커뮤니티세계지도원장공개상태Codes.Active,
        커뮤니티세계지도원장공개상태Codes.OnHold,
        커뮤니티세계지도원장공개상태Codes.Completed
    ];

    private static readonly IReadOnlyList<string> 완료집계상태 =
    [
        커뮤니티세계지도원장공개상태Codes.Completed
    ];

    private static readonly IReadOnlyList<string> 공개근거Action =
    [
        커뮤니티세계지도원장ActionCodes.ViewEvidence
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> 비공개Viewer별최대Action =
        BuildViewer별최대Action([]);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> 공개Viewer별최대Action =
        BuildViewer별최대Action(공개근거Action);

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildViewer별최대Action(
        IReadOnlyList<string> publicActionCodes)
        => new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [커뮤니티세계지도원장ViewerScopeCodes.Public] = publicActionCodes,
            [커뮤니티세계지도원장ViewerScopeCodes.Owner] =
            [
                커뮤니티세계지도원장ActionCodes.ViewEvidence,
                커뮤니티세계지도원장ActionCodes.ViewLedger,
                커뮤니티세계지도원장ActionCodes.ContinueDraft,
                커뮤니티세계지도원장ActionCodes.ReviewConsent,
                커뮤니티세계지도원장ActionCodes.Submit,
                커뮤니티세계지도원장ActionCodes.Withdraw
            ],
            [커뮤니티세계지도원장ViewerScopeCodes.Participant] =
            [
                커뮤니티세계지도원장ActionCodes.ViewEvidence,
                커뮤니티세계지도원장ActionCodes.ViewLedger
            ],
            [커뮤니티세계지도원장ViewerScopeCodes.Operator] =
            [
                커뮤니티세계지도원장ActionCodes.ViewEvidence,
                커뮤니티세계지도원장ActionCodes.ViewLedger
            ],
            [커뮤니티세계지도원장ViewerScopeCodes.Reviewer] =
            [
                커뮤니티세계지도원장ActionCodes.ViewEvidence,
                커뮤니티세계지도원장ActionCodes.ViewLedger,
                커뮤니티세계지도원장ActionCodes.ReviewConsent
            ]
        };

    public static IReadOnlyList<커뮤니티세계지도원장ProjectionPolicyRule> All { get; } =
    [
        Private(CommunityLedgerTemplateKeys.IndividualDemand),
        Private(CommunityLedgerTemplateKeys.Order),
        Private(CommunityLedgerTemplateKeys.IndividualImport),
        Private(CommunityLedgerTemplateKeys.IndividualExport),
        Aggregate(CommunityLedgerTemplateKeys.GroupOrder, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 공개집계상태),
        Aggregate(CommunityLedgerTemplateKeys.CargoTransport, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 운영완료집계상태),
        Aggregate(CommunityLedgerTemplateKeys.FoodOrder, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 완료집계상태, 10),
        Aggregate(CommunityLedgerTemplateKeys.FoodDelivery, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 완료집계상태, 10),
        Aggregate(CommunityLedgerTemplateKeys.SsalddelMart, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 공개집계상태),
        Aggregate(CommunityLedgerTemplateKeys.WarehouseOutbound, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 운영완료집계상태, 10),
        Aggregate(CommunityLedgerTemplateKeys.WarehouseInbound, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 운영완료집계상태, 10),
        Aggregate(CommunityLedgerTemplateKeys.LocalSale, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 공개집계상태),
        Aggregate(CommunityLedgerTemplateKeys.GroupPurchase, 커뮤니티세계지도원장위치공개ModeCodes.AdministrativeRegion, 공개집계상태),
        Aggregate(CommunityLedgerTemplateKeys.GroupImport, 커뮤니티세계지도원장위치공개ModeCodes.Country, 공개집계상태),
        Aggregate(CommunityLedgerTemplateKeys.GroupExport, 커뮤니티세계지도원장위치공개ModeCodes.Country, 공개집계상태),
        Aggregate(CommunityLedgerTemplateKeys.ForeignFoodFacilityProfile, 커뮤니티세계지도원장위치공개ModeCodes.Country, 공개집계상태),
        Aggregate(CommunityLedgerTemplateKeys.MeatImportReadiness, 커뮤니티세계지도원장위치공개ModeCodes.Country, 공개집계상태),
        Private(CommunityLedgerTemplateKeys.Errand),
        Private(CommunityLedgerTemplateKeys.EducationFieldExperience),
        // 공개 모집·소개 동의는 별도 전용 조회의 범위입니다. 보호된 조건·참여·회차 원장을
        // 일반 지도 집계 대상으로 자동 공개하지 않습니다.
        Private(CommunityLedgerTemplateKeys.SpaceCooperation)
    ];

    private static readonly IReadOnlyDictionary<string, 커뮤니티세계지도원장ProjectionPolicyRule> ByTemplate =
        All.ToDictionary(rule => rule.LedgerTemplateKey, StringComparer.OrdinalIgnoreCase);

    public static 커뮤니티세계지도원장ProjectionPolicyRule Find(string ledgerTemplateKey)
    {
        if (string.IsNullOrWhiteSpace(ledgerTemplateKey)
            || !ByTemplate.TryGetValue(ledgerTemplateKey.Trim(), out var rule))
        {
            throw new KeyNotFoundException($"지도 원장 projection 정책이 없습니다: {ledgerTemplateKey}");
        }

        return rule;
    }

    public static bool TryFind(
        string? ledgerTemplateKey,
        out 커뮤니티세계지도원장ProjectionPolicyRule? rule)
    {
        if (string.IsNullOrWhiteSpace(ledgerTemplateKey))
        {
            rule = null;
            return false;
        }

        return ByTemplate.TryGetValue(ledgerTemplateKey.Trim(), out rule);
    }

    private static 커뮤니티세계지도원장ProjectionPolicyRule Private(string ledgerTemplateKey)
        => new(
            ledgerTemplateKey,
            AllowsPublicProjection: false,
            커뮤니티세계지도원장위치공개ModeCodes.None,
            MinimumPublicAggregateCount: null,
            PublicStatusCodes: [],
            PublicActionCodes: [],
            비공개Viewer별최대Action,
            커뮤니티세계지도원장보호FieldGroupCodes.All);

    private static 커뮤니티세계지도원장ProjectionPolicyRule Aggregate(
        string ledgerTemplateKey,
        string locationModeCode,
        IReadOnlyList<string> publicStatusCodes,
        int minimumPublicAggregateCount = 커뮤니티활동공개Policy.최소공개활동수)
        => new(
            ledgerTemplateKey,
            AllowsPublicProjection: true,
            locationModeCode,
            minimumPublicAggregateCount,
            publicStatusCodes,
            공개근거Action,
            공개Viewer별최대Action,
            커뮤니티세계지도원장보호FieldGroupCodes.All);
}
