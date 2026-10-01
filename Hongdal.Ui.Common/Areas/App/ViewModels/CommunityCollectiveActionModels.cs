using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class CommunityCollectiveActionPageKeys
{
    public const string Gathering = "gathering";
    public const string Conditions = "conditions";
    public const string Party = "party";
    public const string Readiness = "readiness";
    public const string InProgress = "in-progress";
    public const string Completed = "completed";
    public const string Mine = "mine";
    public const string Stories = "stories";
    public const string Professionals = "professionals";

    public static IReadOnlyList<string> Ordered { get; } =
    [
        Gathering,
        Conditions,
        Party,
        Readiness,
        InProgress,
        Completed,
        Mine,
        Stories,
        Professionals
    ];

    public static string Normalize(string? value)
        => Ordered.Contains(value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? Ordered.First(key => string.Equals(key, value?.Trim(), StringComparison.OrdinalIgnoreCase))
            : Gathering;
}

public sealed record CommunityCollectiveActionPageDefinition(
    string Key,
    int Number,
    string Title,
    string ShortTitle,
    string Description,
    string Icon,
    bool IsJourneyStage);

public static class CommunityCollectiveActionPageCatalog
{
    public static IReadOnlyList<CommunityCollectiveActionPageDefinition> All { get; } =
    [
        new(CommunityCollectiveActionPageKeys.Gathering, 1, "마음 모으는 중", "마음 모으기", "게시글에서 시작된 관심과 필요한 수량을 모읍니다.", "groups", true),
        new(CommunityCollectiveActionPageKeys.Conditions, 2, "우리 조건 맞추기", "조건 맞추기", "상품, 수량, 가격, 수령과 거래 조건을 함께 확인합니다.", "tune", true),
        new(CommunityCollectiveActionPageKeys.Party, 3, "함께할 사람", "함께할 사람", "거래 당사자와 전문·현장 역할의 빈자리를 확인합니다.", "diversity_3", true),
        new(CommunityCollectiveActionPageKeys.Readiness, 4, "실행 준비 확인", "실행 준비", "합의, 서명, 역할 수락과 업무 인계를 각각 확인합니다.", "fact_check", true),
        new(CommunityCollectiveActionPageKeys.InProgress, 5, "같이 하는 중", "같이 하는 중", "실행 배치의 진행과 추가 참여 가능 여력을 확인합니다.", "local_shipping", true),
        new(CommunityCollectiveActionPageKeys.Completed, 6, "우리 해냈어요", "완료", "함께 만든 결과와 다음 모임에 남길 기록을 확인합니다.", "celebration", true),
        new(CommunityCollectiveActionPageKeys.Mine, 7, "내가 함께한 일", "내 참여", "관심을 표시했거나 역할을 맡은 일을 한곳에서 봅니다.", "person_book", false),
        new(CommunityCollectiveActionPageKeys.Stories, 8, "성사된 이야기", "성사 이야기", "완료된 모임의 결과와 배운 점을 나눕니다.", "auto_stories", false),
        new(CommunityCollectiveActionPageKeys.Professionals, 9, "전문가 참여 요청함", "전문가 요청", "통관, 운송, 창고와 문서 역할의 참여 요청을 모아봅니다.", "support_agent", false)
    ];

    public static CommunityCollectiveActionPageDefinition Find(string? key)
    {
        var normalized = CommunityCollectiveActionPageKeys.Normalize(key);
        return All.First(page => page.Key == normalized);
    }
}

public static class CommunityCollectiveActionRoutes
{
    public const string Root = "/community/actions";

    public static string Build(string? pageKey, Guid? actionId = null)
    {
        var path = $"{Root}/{CommunityCollectiveActionPageKeys.Normalize(pageKey)}";
        return actionId.HasValue ? $"{path}?campaignId={actionId.Value:D}" : path;
    }

    public static string BuildSourcePost(long? postId)
        => postId is > 0 ? $"/community/posts/{postId.Value}" : "/community";
}

public enum CommunityCollectiveActionDataMode
{
    Live,
    Preview
}

public enum CommunityCapacityEvidenceStatus
{
    Confirmed,
    Pending,
    NotRequired,
    Closed
}

public sealed record CommunityActionConditionSnapshot(
    string Code,
    string Label,
    string Value,
    string StatusLabel,
    bool Confirmed);

public sealed record CommunityActionRoleSlotSnapshot(
    string Category,
    string RoleCode,
    string RoleLabel,
    string Responsibility,
    bool Required,
    string? ParticipantLabel,
    string StatusLabel,
    bool Accepted);

public sealed record CommunityActionReadinessCheckSnapshot(
    string Code,
    string Label,
    string Detail,
    bool Complete,
    bool BlocksExecution);

public sealed record CommunityCapacityEvidenceSnapshot(
    string Code,
    string Label,
    string ResponsibleRole,
    CommunityCapacityEvidenceStatus Status,
    decimal? MaximumTotalQuantity,
    string EvidenceLabel,
    bool Required = true);

public sealed record CommunityActionTimelineItemSnapshot(
    DateTimeOffset At,
    string Title,
    string Detail,
    bool Complete);

public sealed record CommunityActionOutcomeSnapshot(
    string Label,
    string Value,
    string Detail);

public sealed class CommunityCollectiveActionSnapshot
{
    public Guid Id { get; init; }
    public long? SourcePostId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string CommunityScope { get; init; } = string.Empty;
    public string CurrentPageKey { get; init; } = CommunityCollectiveActionPageKeys.Gathering;
    public string StatusLabel { get; init; } = string.Empty;
    public string ProductLabel { get; init; } = string.Empty;
    public string SourceCountryCode { get; init; } = string.Empty;
    public string DestinationCountryCode { get; init; } = string.Empty;
    public int ParticipantCount { get; init; }
    public decimal CurrentCommittedQuantity { get; init; }
    public decimal CurrentPotentialQuantity { get; init; }
    public decimal MinimumOrderQuantity { get; init; }
    public string QuantityUnit { get; init; } = "개";
    public DateTimeOffset? AdditionalParticipationClosesAt { get; init; }
    public decimal? EstimatedCurrentUnitCost { get; init; }
    public decimal? EstimatedSafeMaximumUnitCost { get; init; }
    public bool IsPreview { get; init; }
    public bool IsMine { get; init; }
    public IReadOnlyList<CommunityActionConditionSnapshot> Conditions { get; init; } = [];
    public IReadOnlyList<CommunityActionRoleSlotSnapshot> RoleSlots { get; init; } = [];
    public IReadOnlyList<CommunityActionReadinessCheckSnapshot> ReadinessChecks { get; init; } = [];
    public IReadOnlyList<CommunityCapacityEvidenceSnapshot> CapacityEvidence { get; init; } = [];
    public IReadOnlyList<CommunityActionTimelineItemSnapshot> Timeline { get; init; } = [];
    public IReadOnlyList<CommunityActionOutcomeSnapshot> Outcomes { get; init; } = [];
    public CommunityCollectiveImportDeliverySnapshot Delivery { get; init; } = new();
    public CommunityTraditionalMarketImportedMeatFulfillmentSnapshot TraditionalMarketImportedMeatFulfillment { get; init; } = new();
    public CommunityMarketDaySnapshot MarketDay { get; init; } = new();
}

public interface ICommunityCollectiveActionSource
{
    Task<IReadOnlyList<CommunityVoteResponse>> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed record CommunityCollectiveActionSourceItem(
    CommunityVoteResponse Campaign,
    CommunityActionJourneyResponse? Journey);

public interface ICommunityCollectiveActionSnapshotSource
{
    Task<IReadOnlyList<CommunityCollectiveActionSourceItem>> LoadSnapshotsAsync(
        CancellationToken cancellationToken = default);
}

public sealed class PlatformCommunityCollectiveActionSource(
    PlatformCommunityService communityService) :
    ICommunityCollectiveActionSource,
    ICommunityCollectiveActionSnapshotSource
{
    public async Task<IReadOnlyList<CommunityVoteResponse>> LoadAsync(
        CancellationToken cancellationToken = default)
        => (await communityService.GetGroupPurchaseVotesAsync(cancellationToken: cancellationToken)).Items;

    public async Task<IReadOnlyList<CommunityCollectiveActionSourceItem>> LoadSnapshotsAsync(
        CancellationToken cancellationToken = default)
    {
        var campaigns = await LoadAsync(cancellationToken);
        var items = await Task.WhenAll(campaigns.Select(async campaign =>
        {
            if (campaign.SourcePostId is not long postId)
            {
                return new CommunityCollectiveActionSourceItem(campaign, null);
            }

            try
            {
                var opportunity = await communityService.GetPostOpportunitiesAsync(postId, cancellationToken: cancellationToken);
                return new CommunityCollectiveActionSourceItem(campaign, opportunity?.Journey);
            }
            catch (HttpRequestException)
            {
                return new CommunityCollectiveActionSourceItem(campaign, null);
            }
        }));
        return items;
    }
}

public static class CommunityCollectiveActionSnapshotFactory
{
    public static CommunityCollectiveActionSnapshot FromCampaign(
        CommunityVoteResponse campaign,
        CommunityActionJourneyResponse? journey = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var groupPurchase = campaign.GroupPurchase;
        decimal totalQuantity = groupPurchase?.TotalRequestedQuantity ?? campaign.Options.Sum(option => option.RequestedQuantity);
        decimal minimumQuantity = groupPurchase?.MinimumTotalQuantity ?? 1;
        if (journey?.Economics is { HasPlan: true } economics)
        {
            totalQuantity = economics.CurrentCommittedQuantity > 0
                ? economics.CurrentCommittedQuantity
                : totalQuantity;
            minimumQuantity = economics.MinimumOrderQuantity > 0
                ? economics.MinimumOrderQuantity
                : minimumQuantity;
        }

        var isSigned = string.Equals(
            campaign.ResolutionDocument?.Status,
            CommunityVoteResolutionStatusCodes.Signed,
            StringComparison.OrdinalIgnoreCase);
        var currentPageKey = ResolveCurrentPageKey(campaign, isSigned, journey);
        var productLabel = string.Join(", ", campaign.Options
            .Select(option => option.Text)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(2));
        if (string.IsNullOrWhiteSpace(productLabel))
        {
            productLabel = "상품 조건 확인 중";
        }

        if (!string.IsNullOrWhiteSpace(journey?.Sales.ProductTitle))
        {
            productLabel = journey.Sales.ProductTitle;
        }

        var isImport = groupPurchase?.IsGroupImportCandidate == true;
        var routeReady = groupPurchase is not null
                         && groupPurchase.TradeRouteMissingFieldCodes.Count == 0
                         && groupPurchase.TradeRouteInvalidFieldCodes.Count == 0;
        var minimumReached = groupPurchase?.IsMinimumReached == true || totalQuantity >= minimumQuantity;
        var pickupCapacity = groupPurchase?.PickupPoints
            .Where(point => point.CapacityQuantity.HasValue)
            .Sum(point => point.CapacityQuantity!.Value);
        var isUnitedStatesImport = isImport
                                   && string.Equals(
                                       CommunityGroupPurchaseTradeRoutePolicy
                                           .NormalizeOperatingMarketCountryCode(
                                               groupPurchase?.OperatingMarketCountryCode),
                                       CommunityGroupPurchaseTradeRoutePolicy
                                           .UnitedStatesCountryCode,
                                       StringComparison.OrdinalIgnoreCase)
                                   && string.Equals(
                                       groupPurchase?.DeliveryCountryCode,
                                       CommunityGroupPurchaseTradeRoutePolicy
                                           .UnitedStatesCountryCode,
                                       StringComparison.OrdinalIgnoreCase);
        var isKoreaImportedMeat = CommunityTraditionalMarketImportedMeatScenarioFactory
            .IsApplicable(groupPurchase);
        var roleSlots = journey?.RoleSlots.Count > 0
            ? BuildRoleSlots(journey)
            : BuildRoleSlots(campaign, isImport, isUnitedStatesImport, isKoreaImportedMeat);

        return new CommunityCollectiveActionSnapshot
        {
            Id = campaign.Id,
            SourcePostId = campaign.SourcePostId,
            Title = campaign.Title,
            Summary = campaign.Description,
            CommunityScope = campaign.CommunityScope,
            CurrentPageKey = currentPageKey,
            StatusLabel = string.IsNullOrWhiteSpace(journey?.CurrentStageLabel)
                ? ResolveStatusLabel(currentPageKey)
                : journey.CurrentStageLabel,
            ProductLabel = productLabel,
            SourceCountryCode = groupPurchase?.ShipFromCountryCode ?? string.Empty,
            DestinationCountryCode = groupPurchase?.DeliveryCountryCode ?? string.Empty,
            ParticipantCount = Math.Max(campaign.TotalVoteCount, journey?.ParticipantCount ?? 0),
            CurrentCommittedQuantity = totalQuantity,
            CurrentPotentialQuantity = journey?.Economics.RecommendedQuantity ?? totalQuantity,
            MinimumOrderQuantity = minimumQuantity,
            QuantityUnit = string.IsNullOrWhiteSpace(groupPurchase?.QuantityUnit)
                ? "개"
                : groupPurchase.QuantityUnit,
            AdditionalParticipationClosesAt = campaign.Status == CommunityVoteStatusCodes.Open
                ? ToOffset(campaign.ClosesAtUtc)
                : null,
            EstimatedCurrentUnitCost = journey?.Economics.EstimatedUnitLandedCost,
            Conditions = BuildConditions(campaign, productLabel, minimumReached, routeReady, journey),
            RoleSlots = roleSlots,
            ReadinessChecks = BuildReadinessChecks(campaign, minimumReached, routeReady, isSigned, journey),
            CapacityEvidence = BuildCapacityEvidence(isImport, pickupCapacity),
            Timeline = BuildTimeline(campaign, isSigned, journey),
            Outcomes = isSigned || currentPageKey == CommunityCollectiveActionPageKeys.Completed
                ?
                [
                    new("모인 사람", $"{campaign.TotalVoteCount}명", "현재 결의문 기준 참여 인원"),
                    new("함께 정한 수량", $"{totalQuantity:N0}{groupPurchase?.QuantityUnit ?? "개"}", "서명된 수요 기준"),
                    new("다음 기록", "이행 중", "실제 출고·수령 결과는 업무 원장에서 이어집니다.")
                ]
                : [],
            Delivery = CommunityUnitedStatesCollectiveImportScenarioFactory.Build(
                campaign,
                journey,
                roleSlots),
            TraditionalMarketImportedMeatFulfillment =
                CommunityTraditionalMarketImportedMeatScenarioFactory.Build(campaign),
            MarketDay = CommunityMarketDayScenarioFactory.Build(
                campaign,
                productLabel,
                totalQuantity,
                journey?.Economics.RecommendedQuantity ?? totalQuantity,
                groupPurchase?.QuantityUnit ?? "개")
        };
    }

    private static string ResolveCurrentPageKey(
        CommunityVoteResponse campaign,
        bool isSigned,
        CommunityActionJourneyResponse? journey)
    {
        var projectedPage = journey?.CurrentStageCode switch
        {
            CommunityActionJourneyStageCodes.Conditions => CommunityCollectiveActionPageKeys.Conditions,
            CommunityActionJourneyStageCodes.Party => CommunityCollectiveActionPageKeys.Party,
            CommunityActionJourneyStageCodes.Readiness => CommunityCollectiveActionPageKeys.Readiness,
            CommunityActionJourneyStageCodes.InProgress => CommunityCollectiveActionPageKeys.InProgress,
            CommunityActionJourneyStageCodes.Completed => CommunityCollectiveActionPageKeys.Completed,
            CommunityActionJourneyStageCodes.ProvisionalLedger => CommunityCollectiveActionPageKeys.Conditions,
            CommunityActionJourneyStageCodes.Gathering => CommunityCollectiveActionPageKeys.Gathering,
            _ => null
        };
        if (projectedPage is not null)
        {
            return projectedPage;
        }

        if (isSigned)
        {
            return CommunityCollectiveActionPageKeys.InProgress;
        }

        if (campaign.ResolutionDocument is not null)
        {
            return CommunityCollectiveActionPageKeys.Readiness;
        }

        if (!string.Equals(campaign.Status, CommunityVoteStatusCodes.Open, StringComparison.OrdinalIgnoreCase)
            || campaign.GroupPurchase?.IsMinimumReached == true)
        {
            return CommunityCollectiveActionPageKeys.Conditions;
        }

        return CommunityCollectiveActionPageKeys.Gathering;
    }

    private static string ResolveStatusLabel(string pageKey)
        => pageKey switch
        {
            CommunityCollectiveActionPageKeys.Conditions => "조건 조율 중",
            CommunityCollectiveActionPageKeys.Party => "역할 구성 중",
            CommunityCollectiveActionPageKeys.Readiness => "실행 준비 중",
            CommunityCollectiveActionPageKeys.InProgress => "같이 하는 중",
            CommunityCollectiveActionPageKeys.Completed => "완료",
            _ => "마음 모으는 중"
        };

    private static IReadOnlyList<CommunityActionConditionSnapshot> BuildConditions(
        CommunityVoteResponse campaign,
        string productLabel,
        bool minimumReached,
        bool routeReady,
        CommunityActionJourneyResponse? journey)
    {
        var groupPurchase = campaign.GroupPurchase;
        var serviceArea = string.IsNullOrWhiteSpace(groupPurchase?.ServiceAreaLabel)
            ? campaign.CommunityScope
            : groupPurchase.ServiceAreaLabel;
        List<CommunityActionConditionSnapshot> conditions =
        [
            new("product", "함께 살 것", productLabel, "확인 중", campaign.Options.Count > 0),
            new(
                "quantity",
                "필요한 양",
                $"{groupPurchase?.TotalRequestedQuantity ?? 0:N0}/{groupPurchase?.MinimumTotalQuantity ?? 1:N0}{groupPurchase?.QuantityUnit ?? "개"}",
                minimumReached ? "기준 도달" : "더 모으는 중",
                minimumReached),
            new("area", "수령 범위", serviceArea, "모집 조건", !string.IsNullOrWhiteSpace(serviceArea)),
            new(
                "route",
                "거래 경로",
                groupPurchase?.IsGroupImportCandidate == true ? "해외 조달 검토" : "국내 공동구매",
                routeReady ? "필수값 확인" : "확인 필요",
                routeReady),
            new(
                "agreement",
                "현재 합의",
                campaign.ResolutionDocument?.DocumentTitle ?? "모집 결과를 바탕으로 작성 예정",
                campaign.ResolutionDocument is null ? "초안 전" : "문서 있음",
                campaign.ResolutionDocument is not null)
        ];

        if (journey?.Sales is { HasSalesOffer: true } sales)
        {
            conditions.Insert(1, new CommunityActionConditionSnapshot(
                "supplier",
                "공급 제안",
                $"{sales.AvailableQuantity:N0}{sales.QuantityUnit} · {sales.UnitPrice:N0} {sales.CurrencyCode}",
                sales.AllowsGroupPurchase ? "공동구매 협의 가능" : "개별 판매",
                true));
        }

        if (journey?.Economics is { HasPlan: true } economics)
        {
            var quantity = economics.RecommendedQuantity ?? economics.MinimumViableQuantity;
            conditions.Add(new CommunityActionConditionSnapshot(
                "economics",
                "가격·경제성",
                quantity.HasValue
                    ? $"검토 수량 {quantity:N0}{economics.QuantityUnit}"
                    : "집계 계산 리비전 있음",
                economics.ExecutionReady ? "참여자 확인 완료" : "함께 검토 중",
                economics.CurrentQuantityEconomicallyViable));
        }

        return conditions;
    }

    private static IReadOnlyList<CommunityActionRoleSlotSnapshot> BuildRoleSlots(
        CommunityActionJourneyResponse journey)
        => journey.RoleSlots.Select(slot => new CommunityActionRoleSlotSnapshot(
            CategoryLabel(slot.CategoryCode),
            slot.RoleCode,
            slot.Label,
            slot.Summary,
            slot.IsRequired,
            slot.ConfirmedParticipantCount > 0 ? $"{slot.ConfirmedParticipantCount}명 수락" : null,
            slot.ConfirmedParticipantCount > 0
                ? "역할 수락 기록"
                : slot.ExternalCredentialVerificationRequired ? "관할 자격 확인 필요" : "참여 요청",
            slot.ConfirmedParticipantCount > 0)).ToArray();

    private static string CategoryLabel(string categoryCode)
        => categoryCode switch
        {
            CommunityPartyRoleCategoryCodes.CommercialParty => "거래 당사자",
            CommunityPartyRoleCategoryCodes.CustomsAndDocumentation => "통관·문서",
            CommunityPartyRoleCategoryCodes.TransportationIntermediary => "운송 중개·주선",
            CommunityPartyRoleCategoryCodes.Carrier => "실제 운송",
            _ => "현장 이행"
        };

    private static IReadOnlyList<CommunityActionRoleSlotSnapshot> BuildRoleSlots(
        CommunityVoteResponse campaign,
        bool isImport,
        bool isUnitedStatesImport,
        bool isKoreaImportedMeat)
    {
        var proposerLabel = string.IsNullOrWhiteSpace(campaign.CreatedByDisplayName)
            ? "제안자 확인 필요"
            : campaign.CreatedByDisplayName;
        var slots = new List<CommunityActionRoleSlotSnapshot>
        {
            new("거래 당사자", "buyer", "구매자·참여자", "수량과 수령 조건을 직접 확인합니다.", true, $"{campaign.TotalVoteCount}명 관심", "관심 확인", campaign.TotalVoteCount > 0),
            new("거래 당사자", "representative", "공동구매 대표", "모인 조건을 정리하고 당사자 확인을 요청합니다.", true, proposerLabel, "제안 기록", !string.IsNullOrWhiteSpace(campaign.CreatedByDisplayName)),
            new("거래 당사자", "seller", "판매자·공급자", "공급 가능량, 가격과 출하 조건을 제안합니다.", true, null, "참여 요청", false),
            new("현장 이행", "warehouse", "창고·수령소 운영", "입고, 보관, 분배와 수령 가능량을 확인합니다.", false, null, "필요 시 참여", false),
            new("실제 운송", "carrier", "운송 제공자", "적재 여력과 운송 조건을 자신의 책임으로 제안합니다.", false, null, "필요 시 참여", false)
        };

        if (isImport)
        {
            slots.InsertRange(2,
            [
                new("거래 당사자", "importer", "수입 책임 당사자", "수입 계약과 신고 책임 범위를 직접 수락합니다.", true, null, "참여 요청", false),
                new("거래 당사자", "exporter", "수출 책임 당사자", "수출 계약과 출발국 서류 책임을 확인합니다.", true, null, "참여 요청", false),
                new("통관·문서", "import-customs", "수입 통관 전문가", "도착국 신고와 통관 서류를 별도 수임으로 검토합니다.", false, null, "전문가 요청", false),
                new("통관·문서", "export-customs", "수출 통관 전문가", "출발국 수출 신고와 서류를 별도 수임으로 검토합니다.", false, null, "전문가 요청", false),
                new("운송 중개·주선", "forwarder", "허가·등록된 운송 주선업자", "운송 경로와 제공자 조건을 자신의 권한 안에서 제안합니다.", false, null, "전문가 요청", false)
            ]);
        }

        if (isUnitedStatesImport)
        {
            slots.RemoveAll(slot => slot.RoleCode is "warehouse" or "carrier");
            slots.AddRange(
            [
                new(
                    "현장 이행",
                    CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator,
                    "보세창고·FTZ 운영자",
                    "현재 시설 승인, FIRMS 정보, 공간과 계약을 별도로 확인합니다.",
                    false,
                    null,
                    "업체 후보 확인",
                    false),
                new(
                    "실제 운송",
                    CommunityPostPartyRoleCodes.InBondCarrier,
                    "통관 전 보세운송사",
                    "ACE in-bond 신고, carrier bond와 운송계약을 확인합니다.",
                    false,
                    null,
                    "업체 후보 확인",
                    false),
                new(
                    "현장 이행",
                    CommunityPostPartyRoleCodes.DomesticFulfillmentOperator,
                    "미국 내 풀필먼트 운영자",
                    "반출 화물의 입고·소분·보관·피킹·parcel 인계를 검토합니다.",
                    false,
                    null,
                    "업체 후보 확인",
                    false),
                new(
                    "실제 운송",
                    CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider,
                    "참여자 주소 배송 사업자",
                    "동의된 주소와 서비스 권역 안에서 최종 배송 가능성을 검토합니다.",
                    false,
                    null,
                    "업체 후보 확인",
                    false)
            ]);
        }

        if (isKoreaImportedMeat)
        {
            slots.RemoveAll(slot => slot.RoleCode is "warehouse" or "carrier");
            slots.AddRange(
            [
                new(
                    "현장 이행",
                    CommunityTraditionalMarketImportedMeatRoleCodes.TraditionalMarketHubOperator,
                    "전통시장 육류 입고 거점",
                    "통관 반출된 수입육의 lot, 온도, 이력번호와 작업장 인계를 확인합니다.",
                    true,
                    null,
                    "거점·냉장시설 확인",
                    false),
                new(
                    "현장 이행",
                    CommunityTraditionalMarketImportedMeatRoleCodes.LicensedMeatProcessor,
                    "전통시장 정육·육가공 사업자",
                    "실제 인허가 범위 안에서 발골·정형·절단·소분과 포장을 제안합니다.",
                    true,
                    null,
                    "인허가·처리량 확인",
                    false),
                new(
                    "거래 당사자",
                    CommunityTraditionalMarketImportedMeatRoleCodes.MeatSeller,
                    "식육 판매 책임 사업자",
                    "참여자별 판매 중량, 표시, 이력번호와 소비기한을 확인합니다.",
                    true,
                    null,
                    "판매 범위 확인",
                    false),
                new(
                    "실제 운송",
                    CommunityTraditionalMarketImportedMeatRoleCodes.DomesticColdCarrier,
                    "국내 냉장·냉동 운송 사업자",
                    "통관 반출지부터 시장 작업장까지 보관조건과 온도 인계를 확인합니다.",
                    false,
                    null,
                    "운반 영업·차량 확인",
                    false),
                new(
                    "실제 운송",
                    CommunityTraditionalMarketImportedMeatRoleCodes.NeighborhoodColdDeliveryProvider,
                    "동네 냉장배송 사업자",
                    "참여자 주소 동의 후 생활권 안의 냉장배송과 수령 확인을 맡습니다.",
                    false,
                    null,
                    "배송 범위·계약 확인",
                    false)
            ]);
        }

        return slots;
    }

    private static IReadOnlyList<CommunityActionReadinessCheckSnapshot> BuildReadinessChecks(
        CommunityVoteResponse campaign,
        bool minimumReached,
        bool routeReady,
        bool isSigned,
        CommunityActionJourneyResponse? journey)
        =>
        [
            new("demand", "최소 수요", minimumReached ? "필요한 수량과 인원이 모였습니다." : "최소 수량 또는 인원이 더 필요합니다.", minimumReached, true),
            new("route", "거래 경로", routeReady ? "출발·도착과 거래 방향을 확인했습니다." : "거래 경로 필수값을 확인해야 합니다.", routeReady, true),
            new(
                "roles",
                "필수 역할 수락",
                journey?.RequiredRoleCount > 0
                    ? $"{journey.FilledRequiredRoleCount}/{journey.RequiredRoleCount}개 필수 역할이 확인됐습니다."
                    : "관심 표시는 역할 수락이나 자격 확인을 대신하지 않습니다.",
                journey?.RequiredRoleCount > 0
                && journey.FilledRequiredRoleCount >= journey.RequiredRoleCount,
                true),
            new("resolution", "결의문", campaign.ResolutionDocument is null ? "조건 조율 후 결의문을 작성합니다." : campaign.ResolutionDocument.DocumentTitle, campaign.ResolutionDocument is not null, true),
            new("signature", "현재 리비전 동의", isSigned ? "현재 결의문 서명이 완료됐습니다." : "주요 조건이 확정되면 당사자 동의를 받습니다.", isSigned, true),
            new("capacity", "이행 여력", "공급·창고·운송·서류 담당자가 각자 가능한 범위를 확인합니다.", false, true)
        ];

    private static IReadOnlyList<CommunityCapacityEvidenceSnapshot> BuildCapacityEvidence(
        bool isImport,
        int? pickupCapacity)
        =>
        [
            new("supply", "공급 가능량", "판매자·공급자", CommunityCapacityEvidenceStatus.Pending, null, "공급자 확인 전"),
            new("warehouse", "창고·수령소 처리량", "창고·수령소 운영자", CommunityCapacityEvidenceStatus.Pending, pickupCapacity, pickupCapacity.HasValue ? "수령소 등록값 · 운영자 재확인 필요" : "운영자 확인 전"),
            new("packing", "포장 가능량", "포장·현장 담당자", CommunityCapacityEvidenceStatus.Pending, null, "작업 담당자 확인 전"),
            new("transport", "운송 적재 여력", "운송사·주선업자", CommunityCapacityEvidenceStatus.Pending, null, "운송 제공자 확인 전"),
            new(
                "documents",
                "통관·서류 허용량",
                isImport ? "수출입 책임 당사자·통관 전문가" : "거래 당사자",
                isImport ? CommunityCapacityEvidenceStatus.Pending : CommunityCapacityEvidenceStatus.NotRequired,
                null,
                isImport ? "관할·서류 확인 전" : "국내 거래 기준 해당 없음",
                Required: isImport)
        ];

    private static IReadOnlyList<CommunityActionTimelineItemSnapshot> BuildTimeline(
        CommunityVoteResponse campaign,
        bool isSigned,
        CommunityActionJourneyResponse? journey)
    {
        if (journey?.Timeline.Count > 0)
        {
            return journey.Timeline
                .Select(item => new CommunityActionTimelineItemSnapshot(
                    item.OccurredAtUtc,
                    item.Title,
                    item.Detail,
                    item.IsCompleted))
                .ToArray();
        }

        var items = new List<CommunityActionTimelineItemSnapshot>
        {
            new(ToOffset(campaign.CreatedAtUtc) ?? DateTimeOffset.UtcNow, "제안이 열렸어요", campaign.CreatedByDisplayName, true)
        };
        if (campaign.ClosedAtUtc.HasValue)
        {
            items.Add(new(ToOffset(campaign.ClosedAtUtc)!.Value, "수요 모집을 마쳤어요", $"{campaign.TotalVoteCount}명이 뜻을 모았습니다.", true));
        }

        if (campaign.ResolutionDocument is not null)
        {
            items.Add(new(ToOffset(campaign.ResolutionDocument.CreatedAtUtc) ?? DateTimeOffset.UtcNow, "함께 정한 내용을 문서로 남겼어요", campaign.ResolutionDocument.DocumentTitle, true));
        }

        if (isSigned)
        {
            items.Add(new(DateTimeOffset.UtcNow, "현재 조건에 동의했어요", "실제 이행은 담당 업무 원장에서 이어집니다.", true));
        }

        return items;
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null;
}

public static class CommunityCollectiveActionPreviewCatalog
{
    public static IReadOnlyList<CommunityCollectiveActionSnapshot> Create()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            CreateInProgress(now),
            CreateUnitedStatesBuyerImport(now),
            CreateTraditionalMarketImportedMeat(now),
            CreateFreshProduceMarketDay(now),
            CreateGathering(now),
            CreateReadiness(now),
            CreateCompleted(now)
        ];
    }

    private static CommunityCollectiveActionSnapshot CreateInProgress(DateTimeOffset now)
        => new()
        {
            Id = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Title = "성북 생활모임 올리브오일 공동수입",
            Summary = "한 가정이 부담 없이 쓸 수 있는 수량으로 나누고, 남은 적재 여력만큼 추가 참여를 확인하고 있습니다.",
            CommunityScope = "서울 성북 · 한국/스페인",
            CurrentPageKey = CommunityCollectiveActionPageKeys.InProgress,
            StatusLabel = "같이 하는 중",
            ProductLabel = "엑스트라 버진 올리브오일 1L",
            SourceCountryCode = "ES",
            DestinationCountryCode = "KR",
            ParticipantCount = 18,
            CurrentCommittedQuantity = 42,
            CurrentPotentialQuantity = 44,
            MinimumOrderQuantity = 36,
            QuantityUnit = "박스",
            AdditionalParticipationClosesAt = now.AddDays(2).AddHours(4),
            EstimatedCurrentUnitCost = 148_000m,
            EstimatedSafeMaximumUnitCost = 141_500m,
            IsPreview = true,
            IsMine = true,
            Conditions =
            [
                new("product", "함께 살 것", "엑스트라 버진 올리브오일 1L × 12병", "확정", true),
                new("quantity", "기본 배치", "42박스", "확정", true),
                new("price", "예상 도착원가", "현재 148,000원/박스", "추정값", true),
                new("pickup", "공동 수령", "성북 단지 물류실 · 토요일", "확정", true),
                new("revision", "현재 합의", "계획 리비전 4", "18명 동의", true)
            ],
            RoleSlots =
            [
                new("거래 당사자", "buyer", "구매자", "수량과 수령 조건 확인", true, "18명", "수락", true),
                new("거래 당사자", "seller", "판매자·수출자", "공급·출하 조건 제안", true, "스페인 협동조합", "역할 수락", true),
                new("거래 당사자", "importer", "수입 책임 당사자", "계약·수입 책임 확인", true, "수입 책임 참여자", "검토 중", false),
                new("통관·문서", "import-customs", "수입 통관 전문가", "통관 서류 별도 수임", false, "참여 의향 1명", "자격·수임 확인 중", false),
                new("운송 중개·주선", "forwarder", "해상 운송 주선업자", "운송 조건 별도 제안", false, "참여 의향 1곳", "계약 전", false),
                new("현장 이행", "warehouse", "단지 물류 관리자", "입고·분배 여력 확인", true, "성북 단지 물류실", "역할 수락", true)
            ],
            ReadinessChecks =
            [
                new("demand", "최소 수요", "36박스 기준을 넘었습니다.", true, true),
                new("route", "거래 경로", "스페인 출발·한국 도착을 확인했습니다.", true, true),
                new("roles", "필수 역할 수락", "수입 책임 범위를 최종 확인하고 있습니다.", false, true),
                new("resolution", "결의문", "계획 리비전 4를 문서로 남겼습니다.", true, true),
                new("signature", "현재 리비전 동의", "18명이 현재 조건에 동의했습니다.", true, true),
                new("capacity", "이행 여력", "현재 배치에 8박스의 확인된 여력이 있습니다.", true, true)
            ],
            CapacityEvidence =
            [
                new("supply", "공급 가능량", "판매자·공급자", CommunityCapacityEvidenceStatus.Confirmed, 60, "공급 확인서 · 리비전 2"),
                new("warehouse", "창고·수령소 처리량", "단지 물류 관리자", CommunityCapacityEvidenceStatus.Confirmed, 54, "입고 일정 확인 · 리비전 1"),
                new("packing", "포장 가능량", "현장 포장 담당", CommunityCapacityEvidenceStatus.Confirmed, 56, "포장 작업표 · 리비전 3"),
                new("transport", "운송 적재 여력", "허가·등록된 운송 제공자", CommunityCapacityEvidenceStatus.Confirmed, 52, "적재 여력 확인 · 계약 확정 전"),
                new("documents", "통관·서류 허용량", "수입 책임 당사자·통관 전문가", CommunityCapacityEvidenceStatus.Confirmed, 52, "서류 검토 범위 · 수임 별도")
            ],
            Timeline =
            [
                new(now.AddDays(-18), "게시글에서 마음을 모았어요", "생활 게시판의 올리브오일 이야기에서 시작됐습니다.", true),
                new(now.AddDays(-12), "가원장을 만들었어요", "관심 역할과 참여 의사를 비구속적으로 기록했습니다.", true),
                new(now.AddDays(-5), "현재 조건에 동의했어요", "가격과 수령 조건 리비전 4를 확인했습니다.", true),
                new(now.AddDays(-1), "추가 참여 창이 열렸어요", "확인된 여력 안에서 참여 의향을 더 받습니다.", true),
                new(now.AddDays(2), "포장 수량을 마감해요", "마감 뒤 참여는 다음 배치 대기로 이동합니다.", false)
            ],
            Outcomes =
            [
                new("지금 함께", "18명", "기본 배치 참여자"),
                new("확정 수량", "42박스", "임시 참여 2박스 별도"),
                new("남은 여력", "8박스", "모든 필수 담당자 확인 기준")
            ]
        };

    private static CommunityCollectiveActionSnapshot CreateUnitedStatesBuyerImport(
        DateTimeOffset now)
    {
        IReadOnlyList<CommunityActionRoleSlotSnapshot> roles =
        [
            new("거래 당사자", CommunityPostPartyRoleCodes.Buyer, "미국 구매 참여자", "수량과 배송 조건을 직접 확인합니다.", true, "26명", "관심·수량 확인", true),
            new("거래 당사자", CommunityPostPartyRoleCodes.Seller, "중국 제조사·수출자", "공급과 출하 전 개별포장·라벨 조건을 제안합니다.", true, "중국 제조공장", "전처리 견적 검토", true),
            new("거래 당사자", CommunityPostPartyRoleCodes.Importer, "미국 수입 책임 당사자", "수입 계약과 신고 책임 범위를 직접 수락합니다.", true, "참여자 확인 중", "역할 수락 필요", false),
            new("통관·문서", CommunityPostPartyRoleCodes.ImportCustomsBroker, "미국 수입 통관 전문가", "통관 서류와 관할 요구사항을 별도 수임으로 검토합니다.", false, null, "전문가 요청", false),
            new("현장 이행", CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator, "보세창고·FTZ 운영자", "시설 승인과 공간, 인계 조건을 별도 계약으로 확인합니다.", false, "후보 문의 준비", "권한·계약 확인", false),
            new("실제 운송", CommunityPostPartyRoleCodes.InBondCarrier, "통관 전 보세운송사", "ACE in-bond 이동 권한과 경로를 확인합니다.", false, null, "업체 후보 확인", false),
            new("현장 이행", CommunityPostPartyRoleCodes.DomesticFulfillmentOperator, "미국 내 풀필먼트 운영자", "반출 화물의 입고·소분·피킹·parcel 인계를 맡습니다.", false, "후보 문의 준비", "시설·계약 확인", false),
            new("실제 운송", CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider, "참여자 주소 배송 사업자", "동의된 주소와 서비스 권역 안에서 배송합니다.", false, null, "서비스 권역 확인", false)
        ];
        var campaign = new CommunityVoteResponse
        {
            TotalVoteCount = 26,
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                OperatingMarketCountryCode = CommunityGroupPurchaseTradeRoutePolicy
                    .UnitedStatesCountryCode,
                ShipFromCountryCode = "CN",
                DeliveryCountryCode = "US",
                IsGroupImportCandidate = true,
                MinimumParticipantCount = 20,
                MinimumTotalQuantity = 72,
                TotalRequestedQuantity = 84,
                IsMinimumReached = true,
                ServiceAreaKey = "us-place:3651000",
                ServiceAreaLabel = "New York city"
            }
        };
        var journey = new CommunityActionJourneyResponse
        {
            ProvisionalLedgerId = "preview-us-import-provisional-ledger"
        };

        return new CommunityCollectiveActionSnapshot
        {
            Id = Guid.Parse("55555555-5555-4555-8555-555555555555"),
            Title = "뉴욕 생활모임 중국 공장 생활용품 공동수입",
            Summary = "미국 구매자들이 수량을 모으고 중국 공장에서 개별포장·문서·카톤 작업을 먼저 해 미국 후처리 비용을 줄일 수 있는지 견적을 비교합니다.",
            CommunityScope = "New York city · Census place 배달권",
            CurrentPageKey = CommunityCollectiveActionPageKeys.Party,
            StatusLabel = "수입·물류 역할 구성 중",
            ProductLabel = "중국 공장 생산 스테인리스 밀폐용기 세트",
            SourceCountryCode = "CN",
            DestinationCountryCode = "US",
            ParticipantCount = 26,
            CurrentCommittedQuantity = 84,
            CurrentPotentialQuantity = 92,
            MinimumOrderQuantity = 72,
            QuantityUnit = "상자",
            AdditionalParticipationClosesAt = now.AddDays(4),
            EstimatedCurrentUnitCost = 48.60m,
            IsPreview = true,
            IsMine = true,
            Conditions =
            [
                new("product", "함께 살 것", "중국 공장 생산 스테인리스 밀폐용기 6종", "상품 조건 확인", true),
                new("quantity", "필요한 양", "84/72상자", "기준 도달", true),
                new("scope", "모집 배달권", "New York city · us-place:3651000", "Census 범위", true),
                new("delivery", "수령 방식", "미국 풀필먼트 입고 후 참여자 주소 배송", "개별 주소 비공개", true),
                new("origin-preparation", "출발 전 전처리", "개별포장·영문 원산지 라벨·송장 원천자료", "공장·미국 3PL 견적 비교 전", false),
                new("economics", "예상 도착원가", "48.60 USD/상자 · 추정", "견적 전", false)
            ],
            RoleSlots = roles,
            ReadinessChecks =
            [
                new("demand", "최소 수요", "20명·72상자 기준을 넘었습니다.", true, true),
                new("route", "거래 경로", "중국 공장 출발·미국 반입·참여자 주소 배송을 확인했습니다.", true, true),
                new("origin-preparation", "출발 전 전처리", "공장 전처리비, 추가 국제운임과 미국 후처리 회피비용을 비교해야 합니다.", false, true),
                new("scope", "모집 배달권", "Census place 범위로만 공개하며 개별 주소는 수집하지 않습니다.", true, true),
                new("roles", "필수 역할 수락", "수입자와 물류 역할 담당자의 직접 수락이 더 필요합니다.", false, true),
                new("contracts", "권한·계약", "시설 승인, 통관, 운송과 풀필먼트 계약은 플랫폼 밖에서 별도 확인합니다.", false, true)
            ],
            CapacityEvidence =
            [
                new("supply", "공급 가능량", "중국 제조사·수출자", CommunityCapacityEvidenceStatus.Pending, 120, "공급 견적 재확인 필요"),
                new("origin-preparation", "출발 전 포장·문서 처리량", "중국 제조공장", CommunityCapacityEvidenceStatus.Pending, null, "개별포장·라벨·QC 견적 전"),
                new("bonded", "보세 인계 가능량", "보세시설·보세운송사", CommunityCapacityEvidenceStatus.Pending, null, "시설·권한 확인 전"),
                new("fulfillment", "풀필먼트 처리량", "미국 내 풀필먼트 운영자", CommunityCapacityEvidenceStatus.Pending, null, "계약·SLA 확인 전"),
                new("parcel", "참여자 주소 배송", "parcel·last-mile 사업자", CommunityCapacityEvidenceStatus.Pending, null, "서비스 권역 확인 전")
            ],
            Timeline =
            [
                new(now.AddDays(-9), "미국 구매자들이 이야기를 시작했어요", "개별 주소 대신 New York city 배달권에서 관심을 모았습니다.", true),
                new(now.AddDays(-5), "최소 수량을 넘었어요", "26명이 84상자의 참여 의향을 기록했습니다.", true),
                new(now.AddDays(-3), "비구속 가원장을 만들었어요", "주문·계약 확정 전 공동 조건과 역할 슬롯을 기록했습니다.", true),
                new(now.AddDays(-1), "공장 전처리 견적 비교를 열었어요", "공장 전처리와 미국 3PL 후처리 비용을 같은 조건으로 확인합니다.", true)
            ],
            Delivery = CommunityUnitedStatesCollectiveImportScenarioFactory.Build(
                campaign,
                journey,
                roles)
        };
    }

    private static CommunityCollectiveActionSnapshot CreateTraditionalMarketImportedMeat(
        DateTimeOffset now)
    {
        IReadOnlyList<CommunityActionRoleSlotSnapshot> roles =
        [
            new("거래 당사자", CommunityPostPartyRoleCodes.Buyer, "한국 구매 참여자", "희망 부위, 중량과 냉장 수령 조건을 확인합니다.", true, "34명", "수량 확인", true),
            new("거래 당사자", CommunityPostPartyRoleCodes.Seller, "호주 승인 작업장·수출자", "도축·1차 처리, 제품 lot와 수출 증명을 준비합니다.", true, "호주 공급자 예시", "공급 조건 확인", true),
            new("거래 당사자", CommunityPostPartyRoleCodes.Importer, "한국 수입 책임 당사자", "검역·수입검사·세관 신고와 국내 반출 근거를 확인합니다.", true, "수입 책임 참여자", "역할 수락", true),
            new("통관·문서", CommunityPostPartyRoleCodes.ImportCustomsBroker, "수입 통관 전문가", "품목분류와 신고 서류를 별도 수임 범위에서 검토합니다.", false, "수임 예시", "공식 결과 연결", true),
            new("현장 이행", CommunityTraditionalMarketImportedMeatRoleCodes.TraditionalMarketHubOperator, "전통시장 육류 입고 거점", "통관 반출된 lot와 온도·이력번호를 확인하고 작업장에 인계합니다.", true, "서울 지역 전통시장 후보", "현장·냉장시설 확인", false),
            new("현장 이행", CommunityTraditionalMarketImportedMeatRoleCodes.LicensedMeatProcessor, "전통시장 정육·육가공 사업자", "인허가 범위 안에서 필요한 발골·정형·절단·소분을 맡습니다.", true, "시장 상인 참여 요청", "인허가·처리량 확인", false),
            new("거래 당사자", CommunityTraditionalMarketImportedMeatRoleCodes.MeatSeller, "식육 판매 책임 사업자", "판매 중량, 표시, 이력번호와 소비기한을 확인합니다.", true, null, "판매 범위 확인", false),
            new("실제 운송", CommunityTraditionalMarketImportedMeatRoleCodes.DomesticColdCarrier, "국내 냉장·냉동 운송 사업자", "반출지에서 전통시장까지 냉동 상태와 인계 기록을 유지합니다.", false, null, "차량·계약 확인", false),
            new("실제 운송", CommunityTraditionalMarketImportedMeatRoleCodes.NeighborhoodColdDeliveryProvider, "동네 냉장배송 사업자", "참여자 주소 동의 후 생활권 안에서 냉장배송합니다.", false, "지역 배송 참여 요청", "배송권·계약 확인", false)
        ];
        var campaign = new CommunityVoteResponse
        {
            TotalVoteCount = 34,
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                OperatingMarketCountryCode = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                SellerCountryCode = "AU",
                ShipFromCountryCode = "AU",
                DeliveryCountryCode = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                IsGroupImportCandidate = true,
                CustomsClearanceStatusCode = CommunityGroupPurchaseCustomsClearanceStatusCodes.Cleared,
                HsCode = "020230",
                TemperatureCode = "냉동",
                MinimumParticipantCount = 24,
                MinimumTotalQuantity = 300,
                TotalRequestedQuantity = 420,
                IsMinimumReached = true,
                ServiceAreaKey = "traditional-market:sample-seoul",
                ServiceAreaLabel = "서울 지역 전통시장 생활권"
            }
        };

        return new CommunityCollectiveActionSnapshot
        {
            Id = Guid.Parse("66666666-6666-4666-8666-666666666666"),
            Title = "호주산 소고기 공동수입 · 전통시장 정육 배분",
            Summary = "해외 승인 작업장에서 도축·수출검역된 냉동육을 한국에서 검역·통관한 뒤, 지역 전통시장 정육 사업자가 부위별로 작업하고 동네 냉장배송 사업자가 참여자에게 전달하는 예시입니다.",
            CommunityScope = "서울 전통시장 생활권 · 한국/호주",
            CurrentPageKey = CommunityCollectiveActionPageKeys.Party,
            StatusLabel = "지역 가공·배송 역할 구성 중",
            ProductLabel = "호주산 냉동 뼈 없는 소고기 · HS 020230",
            SourceCountryCode = "AU",
            DestinationCountryCode = "KR",
            ParticipantCount = 34,
            CurrentCommittedQuantity = 420,
            CurrentPotentialQuantity = 470,
            MinimumOrderQuantity = 300,
            QuantityUnit = "kg",
            AdditionalParticipationClosesAt = now.AddDays(5),
            IsPreview = true,
            IsMine = true,
            Conditions =
            [
                new("product", "함께 살 것", "호주산 냉동 뼈 없는 소고기 420kg", "제품 lot 확인", true),
                new("import-release", "국내 반출", "검역·수입검사·세관 수리 참조가 있는 물량", "둘러보기 예시", true),
                new("processing", "지역 2차 가공", "필요한 발골·정형·부위별 소분·소비자 포장", "작업장 인허가 확인 전", false),
                new("traceability", "표시·이력", "원산지·보관조건·축산물이력번호 연결", "작업 lot 설계 중", false),
                new("delivery", "수령 방식", "전통시장 출고 후 참여자 주소 냉장배송", "주소 동의 후", false),
                new("local-value", "지역 사업자 대가", "정육가공비와 동네 냉장배송비를 별도 견적으로 반영", "견적 전", false)
            ],
            RoleSlots = roles,
            ReadinessChecks =
            [
                new("demand", "최소 수요", "24명·300kg 기준을 넘었습니다.", true, true),
                new("import-release", "수입 반출 근거", "예시상 공식 결과 참조가 연결돼 있습니다.", true, true),
                new("market", "전통시장 작업장", "시장 공공정보와 실제 식육 영업 인허가를 별도로 확인해야 합니다.", false, true),
                new("processing", "가공 범위", "포장육 생산·재절단 판매·즉석가공 중 실제 작업 범위를 정해야 합니다.", false, true),
                new("cold-chain", "냉장·냉동 이행", "입고부터 가공·포장·배송까지 처리량과 온도 기록이 필요합니다.", false, true),
                new("contracts", "직접 수락·계약", "플랫폼이 상인이나 배송자를 자동 선정하지 않습니다.", false, true)
            ],
            CapacityEvidence =
            [
                new("supply", "통관 반출 물량", "한국 수입 책임 당사자", CommunityCapacityEvidenceStatus.Confirmed, 500, "예시 공식 참조 · 실제 재확인 필요"),
                new("market-inbound", "시장 냉동 입고량", "전통시장 거점 운영자", CommunityCapacityEvidenceStatus.Pending, null, "냉동시설·일일 처리량 확인 전"),
                new("processing", "정육가공 가능량", "허가·신고된 식육 작업 사업자", CommunityCapacityEvidenceStatus.Pending, null, "부위 규격·수율·인허가 확인 전"),
                new("packing", "참여자 포장 가능량", "식육 판매 책임 사업자", CommunityCapacityEvidenceStatus.Pending, null, "포장·표시·이력 설계 전"),
                new("local-delivery", "동네 냉장배송 가능량", "지역 배송 사업자", CommunityCapacityEvidenceStatus.Pending, null, "생활권·차량·계약 확인 전")
            ],
            Timeline =
            [
                new(now.AddDays(-12), "호주산 소고기 이야기가 시작됐어요", "필요한 부위와 가구별 중량을 게시글에서 모았습니다.", true),
                new(now.AddDays(-7), "최소 수량을 넘었어요", "34명이 420kg의 비구속 참여 의향을 기록했습니다.", true),
                new(now.AddDays(-3), "국내 반출 이후 흐름을 나눴어요", "수입 절차와 전통시장 2차 가공을 서로 다른 책임 단계로 분리했습니다.", true),
                new(now.AddDays(-1), "시장 상인과 배송 역할을 열었어요", "가공비와 지역 배송비를 정당한 서비스 대가로 견적받습니다.", true)
            ],
            TraditionalMarketImportedMeatFulfillment =
                CommunityTraditionalMarketImportedMeatScenarioFactory.Build(campaign),
            MarketDay = CommunityMarketDayScenarioFactory.Build(
                campaign,
                "호주산 냉동 뼈 없는 소고기 · HS 020230",
                420,
                470,
                "kg")
        };
    }

    private static CommunityCollectiveActionSnapshot CreateFreshProduceMarketDay(
        DateTimeOffset now)
        => new()
        {
            Id = Guid.Parse("77777777-7777-4777-8777-777777777777"),
            Title = "성남 국내 제철 채소·과일 공동구매 장날",
            Summary = "국내 생산자 공동 출하 물량을 전통시장으로 곧장 입고해 가정 예약분과 시장 조리 가게 식재료를 나누어 배분합니다. 별도 확정된 여유 물량만 장날 현장에 진열하는 시범 운영입니다.",
            CommunityScope = "경기 성남 · 전통시장 생활권",
            CurrentPageKey = CommunityCollectiveActionPageKeys.InProgress,
            StatusLabel = "공동장날 입고 준비 중",
            ProductLabel = "제철 토마토·사과 꾸러미",
            SourceCountryCode = "KR",
            DestinationCountryCode = "KR",
            ParticipantCount = 48,
            CurrentCommittedQuantity = 80,
            CurrentPotentialQuantity = 96,
            MinimumOrderQuantity = 60,
            QuantityUnit = "상자",
            AdditionalParticipationClosesAt = now.AddDays(3),
            IsPreview = true,
            IsMine = true,
            Conditions =
            [
                new("domestic-source", "국내 산지 공동 출하", "경기 광주·충북 충주 생산자 출하 lot와 100상자 공급 조건", "생산자 직접 수락", true),
                new("direct-route", "전통시장 직입고", "산지 선별·포장 뒤 성남 생활권 전통시장 입고 슬롯으로 직송", "운송·입고 수락", true),
                new("food-business-supply", "시장 조리 가게 공급", "가정 예약 68상자·조리 가게 예약 12상자·현장판매 확정 12상자 분리", "가게 2곳 직접 수락", true),
                new("association", "상인회 공동사업", "시범 장날 일정·공용공간·차량 동선 합의", "예시 합의", true),
                new("reservation", "예약 물량", "공동구매 참여자 80상자 별도 표식·보관", "현장판매와 분리", true),
                new("walk-in", "현장판매 물량", "확정 12상자·추가 후보 4상자", "확정 물량만 공개", true),
                new("produce-handling", "청과·채소 작업", "검수·선별·등급·단순 손질·소분", "참여 상인 수락", true),
                new("origin", "표시", "품목·생산지·중량·판매 상인 표시", "입고 때 재확인", false),
                new("closeout", "잔량 처리", "할인·기부·반품 중 판매 상인이 합의한 기준", "마감 기록 예정", false)
            ],
            RoleSlots =
            [
                new("거래 당사자", CommunityPostPartyRoleCodes.Buyer, "공동구매 참여 주민", "예약 수량과 장날 수령시간을 확인합니다.", true, "48명", "예약 80상자", true),
                new("거래 당사자", CommunityDomesticMarketSupplyRoleCodes.ProducerOrCooperative, "국내 농가·생산자단체", "생산지, 수확일, 출하 가능량, 가격과 인계 조건을 직접 수락합니다.", true, "경기 광주·충북 충주 생산자 예시", "출하 lot·조건 수락", true),
                new("산지 이행", CommunityDomesticMarketSupplyRoleCodes.OriginSortingPackingOperator, "산지 농산물 선별·포장 주체", "시장 검수와 주문별 배분에 맞게 선별·포장하고 상차 기록을 남깁니다.", true, "산지 작업조 예시", "선별·포장 범위", true),
                new("실제 운송", CommunityDomesticMarketSupplyRoleCodes.OriginToMarketCarrier, "국내 산지 직송 운송 주체", "확정 출하 lot를 시장 입고 시간에 맞춰 운송하고 인계합니다.", true, "국내 운송 참여 예시", "차량·적재·인계", true),
                new("현장 이행", CommunityMarketDayRoleCodes.MarketAssociationCoordinator, "전통시장 상인회·시장관리자", "공동장날 일정, 공용공간과 참여 상점 모집을 조정합니다.", true, "시범운영 합의 예시", "공동사업 범위", true),
                new("현장 이행", CommunityMarketDayRoleCodes.FreshProduceMerchant, "청과·채소 상인", "입고 농산물을 검수하고 선별·등급·원산지와 판매 조건을 확인합니다.", true, "참여 상점 2곳", "취급 품목·처리량", true),
                new("현장 이행", CommunityMarketDayRoleCodes.ProduceSortingPackingOperator, "농산물 선별·소분 담당", "예약과 현장판매 물량을 나누어 상자별로 표시합니다.", true, "시장 작업조 1팀", "작업 범위·위생", true),
                new("거래 당사자", CommunityMarketIngredientSupplyRoleCodes.MarketFoodBusinessIngredientBuyer, "시장 조리·식음료 가게", "가정 예약분과 분리된 식재료 수량·가격·보관·사용 조건을 직접 수락합니다.", false, "시장 조리 가게 2곳 예시", "영업 범위·보관·가게별 lot", true),
                new("거래 당사자", CommunityMarketDayRoleCodes.MarketDaySeller, "장날 현장 판매 상인", "확정 여유 물량을 진열하고 표시 가격과 판매 결과를 기록합니다.", true, "참여 상점 2곳", "수량·표시·마감", true),
                new("실제 운송", CommunityMarketDayRoleCodes.LocalDeliveryProvider, "생활권 배송 참여자", "배송 신청 물량만 시장에서 인수해 전달합니다.", false, null, "서비스 권역·계약", false)
            ],
            ReadinessChecks =
            [
                new("source", "국내 산지 공급 조건", "생산자 공동 출하량, 가격, 생산지와 출하 lot를 확인했습니다.", true, true),
                new("direct-transport", "산지에서 시장까지 직송", "운송 주체와 시장 입고 슬롯이 같은 배치에 연결됐습니다.", true, true),
                new("food-business-supply", "조리 가게 식재료 공급", "가정 예약분을 잠근 뒤 가게 2곳이 12상자의 공급 조건과 영업 범위를 직접 확인했습니다.", true, true),
                new("association", "상인회 협의", "시범 장날 공동사업 범위를 합의했습니다.", true, true),
                new("merchants", "참여 상점 수락", "청과·채소 상인과 현장 판매 상인이 직접 수락했습니다.", true, true),
                new("inventory", "예약·현장판매 재고 분리", "80상자와 12상자를 별도 표식으로 관리합니다.", true, true),
                new("arrival", "입고 품질·원산지", "장날 아침 실제 물량을 확인해야 합니다.", false, true),
                new("marketing", "지역 공개", "일정과 확정 현장판매 물량만 게시판과 시장 현장판에 공개합니다.", true, false)
            ],
            CapacityEvidence =
            [
                new("supply", "산지 출하 가능량", "생산자·공급자", CommunityCapacityEvidenceStatus.Confirmed, 100, "출하 확인 예시"),
                new("reserved", "공동구매 예약 물량", "가정·가게 공동구매 참여자", CommunityCapacityEvidenceStatus.Confirmed, 80, "가정 68상자·가게 12상자 예약 원장", false),
                new("food-business", "조리 가게 식재료 배정", "시장 조리·식음료 가게", CommunityCapacityEvidenceStatus.Confirmed, 12, "80상자 예약 안의 가게 공급분", false),
                new("walk-in", "현장판매 확정 물량", "참여 판매 상인", CommunityCapacityEvidenceStatus.Confirmed, 12, "별도 판매 재고", false),
                new("sorting", "선별·소분 처리량", "청과·채소 상인", CommunityCapacityEvidenceStatus.Confirmed, 100, "장날 오전 처리량 예시"),
                new("delivery", "생활권 배송 물량", "지역 배송 참여자", CommunityCapacityEvidenceStatus.Pending, null, "배송 신청 마감 뒤 확인", false)
            ],
            Timeline =
            [
                new(now.AddDays(-14), "제철 농산물 이야기가 시작됐어요", "게시글에서 원하는 꾸러미와 수령 방식을 모았습니다.", true),
                new(now.AddDays(-9), "공동구매 기준을 넘었어요", "48명이 80상자를 예약했습니다.", true),
                new(now.AddDays(-7), "국내 산지 공동 출하를 수락했어요", "생산자와 산지 작업 주체가 출하 lot·가격·포장 단위를 확인했습니다.", true),
                new(now.AddDays(-6), "시장 조리 가게도 식재료 공급에 참여했어요", "가정 예약 68상자를 먼저 잠그고 가게 2곳의 12상자를 별도 배정했습니다.", true),
                new(now.AddDays(-5), "상인회와 시범 장날을 합의했어요", "공용공간, 입고 동선과 참여 상점 모집 범위를 정했습니다.", true),
                new(now.AddDays(-2), "현장판매 물량을 따로 확정했어요", "예약 물량을 건드리지 않는 12상자만 이웃에게 공개합니다.", true)
            ],
            MarketDay = CommunityMarketDayScenarioFactory.CreateFreshProducePilotPreview(now)
        };

    private static CommunityCollectiveActionSnapshot CreateGathering(DateTimeOffset now)
        => new()
        {
            Id = Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Title = "동네 제철 사과를 같이 받아볼까요",
            Summary = "산지 출하일에 맞춰 한 번에 받고 가까운 수령소에서 나누려는 가벼운 제안입니다.",
            CommunityScope = "경기 성남",
            CurrentPageKey = CommunityCollectiveActionPageKeys.Gathering,
            StatusLabel = "마음 모으는 중",
            ProductLabel = "제철 사과 5kg",
            SourceCountryCode = "KR",
            DestinationCountryCode = "KR",
            ParticipantCount = 14,
            CurrentCommittedQuantity = 23,
            CurrentPotentialQuantity = 27,
            MinimumOrderQuantity = 30,
            QuantityUnit = "상자",
            AdditionalParticipationClosesAt = now.AddDays(5),
            IsPreview = true,
            Conditions =
            [
                new("product", "함께 살 것", "제철 사과 5kg", "제안", true),
                new("quantity", "필요한 양", "23/30상자", "7상자 더 필요", false),
                new("pickup", "수령 후보", "단지 관리동 앞", "의견 받는 중", false)
            ],
            RoleSlots =
            [
                new("거래 당사자", "buyer", "구매자", "희망 수량 표시", true, "14명 관심", "관심", true),
                new("거래 당사자", "seller", "생산자·판매자", "공급 조건 제안", true, null, "참여 요청", false),
                new("현장 이행", "pickup", "수령소 운영", "수령 시간과 보관 확인", false, null, "참여 요청", false)
            ],
            ReadinessChecks =
            [
                new("demand", "최소 수요", "7상자가 더 필요합니다.", false, true),
                new("roles", "공급자 참여", "생산자 또는 판매자의 제안이 필요합니다.", false, true)
            ],
            Timeline =
            [
                new(now.AddDays(-2), "게시글이 올라왔어요", "사과를 함께 받고 싶다는 이야기가 시작됐습니다.", true),
                new(now.AddDays(-1), "14명이 관심을 보였어요", "희망 수량은 아직 바꿀 수 있습니다.", true)
            ]
        };

    private static CommunityCollectiveActionSnapshot CreateReadiness(DateTimeOffset now)
        => new()
        {
            Id = Guid.Parse("33333333-3333-4333-8333-333333333333"),
            Title = "공동주택 공용 세제 함께 구매",
            Summary = "관리동 수령과 가구별 분배 조건을 맞추고 현재 리비전의 동의를 확인하고 있습니다.",
            CommunityScope = "인천 송도",
            CurrentPageKey = CommunityCollectiveActionPageKeys.Readiness,
            StatusLabel = "실행 준비 중",
            ProductLabel = "친환경 세탁세제 3L",
            SourceCountryCode = "KR",
            DestinationCountryCode = "KR",
            ParticipantCount = 32,
            CurrentCommittedQuantity = 64,
            CurrentPotentialQuantity = 64,
            MinimumOrderQuantity = 60,
            QuantityUnit = "통",
            IsPreview = true,
            IsMine = true,
            Conditions =
            [
                new("product", "함께 살 것", "친환경 세탁세제 3L", "확정", true),
                new("quantity", "함께 살 양", "64통", "기준 도달", true),
                new("pickup", "공동 수령", "관리동 지하 물류실", "확정", true),
                new("agreement", "현재 합의", "가구별 최대 3통 · 리비전 2", "동의 중", false)
            ],
            RoleSlots =
            [
                new("거래 당사자", "buyer", "구매자", "수량·수령 조건 확인", true, "32가구", "수락 중", false),
                new("거래 당사자", "seller", "판매자", "공급·출하 조건 확인", true, "국내 공급사", "역할 수락", true),
                new("현장 이행", "warehouse", "단지 물류 관리자", "입고·분배 일정 확인", true, "단지 물류 관리자", "역할 수락", true)
            ],
            ReadinessChecks =
            [
                new("demand", "최소 수요", "60통 기준을 넘었습니다.", true, true),
                new("route", "국내 거래 경로", "공급처와 수령소를 확인했습니다.", true, true),
                new("roles", "필수 역할 수락", "판매자와 물류 관리자가 역할을 수락했습니다.", true, true),
                new("resolution", "결의문", "가구별 수량과 수령 시간 리비전 2", true, true),
                new("signature", "현재 리비전 동의", "32가구 중 27가구가 확인했습니다.", false, true),
                new("capacity", "이행 여력", "동의 완료 후 최종 입고 수량을 확인합니다.", false, true)
            ],
            Timeline =
            [
                new(now.AddDays(-10), "수요 모집을 시작했어요", "관리동 게시판과 생활 게시판에서 함께 모았습니다.", true),
                new(now.AddDays(-4), "최소 수량을 넘었어요", "공급 조건 조율을 시작했습니다.", true),
                new(now.AddDays(-1), "리비전 2 동의를 받고 있어요", "주요 조건 변경 시 다시 확인합니다.", true)
            ]
        };

    private static CommunityCollectiveActionSnapshot CreateCompleted(DateTimeOffset now)
        => new()
        {
            Id = Guid.Parse("44444444-4444-4444-8444-444444444444"),
            Title = "마을 축제 다회용 식기 공동 대여",
            Summary = "세 동네가 수량을 합쳐 필요한 식기를 빌리고 반납까지 마쳤습니다.",
            CommunityScope = "전북 전주",
            CurrentPageKey = CommunityCollectiveActionPageKeys.Completed,
            StatusLabel = "우리 해냈어요",
            ProductLabel = "다회용 식기 세트",
            SourceCountryCode = "KR",
            DestinationCountryCode = "KR",
            ParticipantCount = 41,
            CurrentCommittedQuantity = 300,
            CurrentPotentialQuantity = 300,
            MinimumOrderQuantity = 250,
            QuantityUnit = "세트",
            IsPreview = true,
            Conditions =
            [
                new("quantity", "함께 쓴 수량", "300세트", "완료", true),
                new("return", "반납", "300세트 확인", "완료", true)
            ],
            RoleSlots =
            [
                new("거래 당사자", "participants", "행사 참여자", "수량·반납 약속 이행", true, "41명", "완료", true),
                new("현장 이행", "operator", "현장 운영", "배부·회수·검수", true, "3개 동네 운영팀", "완료", true)
            ],
            ReadinessChecks =
            [
                new("completed", "결과 확인", "배부와 반납 수량을 모두 확인했습니다.", true, false)
            ],
            Timeline =
            [
                new(now.AddDays(-21), "세 동네가 마음을 모았어요", "각 행사에서 필요한 수량을 합쳤습니다.", true),
                new(now.AddDays(-7), "식기를 함께 사용했어요", "현장별 담당자가 배부 수량을 확인했습니다.", true),
                new(now.AddDays(-5), "반납을 마쳤어요", "파손 2세트를 기록하고 정산했습니다.", true)
            ],
            Outcomes =
            [
                new("함께한 사람", "41명", "세 동네 행사 참여자"),
                new("사용한 식기", "300세트", "일회용품을 대신했습니다."),
                new("반납 확인", "298+2세트", "정상 298 · 파손 기록 2")
            ]
        };
}
