using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class CommunityMarketDayProductHandlingProfileCodes
{
    public const string FreshProduce = "fresh-produce";
    public const string FisheriesProducts = "fisheries-products";
    public const string Meat = "meat";
    public const string GeneralGoods = "general-goods";
}

public static class CommunityMarketDayRoleCodes
{
    public const string MarketAssociationCoordinator = "market-association-coordinator";
    public const string FreshProduceMerchant = "fresh-produce-merchant";
    public const string ProduceSortingPackingOperator = "produce-sorting-packing-operator";
    public const string FisheriesMerchant = "fisheries-merchant";
    public const string MarketFoodBusinessIngredientBuyer =
        CommunityMarketIngredientSupplyRoleCodes.MarketFoodBusinessIngredientBuyer;
    public const string LicensedMeatProcessor = "licensed-meat-processor";
    public const string ProductMerchant = "market-day-product-merchant";
    public const string MarketDaySeller = "market-day-seller";
    public const string MarketVisualDesigner = "market-visual-designer";
    public const string LocalDeliveryProvider = "market-day-local-delivery-provider";
}

public enum CommunityMarketDayState
{
    AssociationReview,
    PilotScheduled,
    Open,
    Completed
}

public enum CommunityMarketDayStageState
{
    Completed,
    Current,
    Waiting,
    DirectAcceptanceRequired
}

public sealed record CommunityMarketDayStageSnapshot(
    int Number,
    string Label,
    string Detail,
    CommunityMarketDayStageState State);

public sealed record CommunityMarketDayMerchantRoleSnapshot(
    string RoleCode,
    string Label,
    string Responsibility,
    bool Required,
    bool Accepted,
    string VerificationLabel);

public sealed class CommunityMarketDaySnapshot
{
    public bool IsApplicable { get; init; }
    public CommunityMarketDayState State { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string MarketScopeKey { get; init; } = string.Empty;
    public string MarketName { get; init; } = string.Empty;
    public string CadenceLabel { get; init; } = string.Empty;
    public string ProductLabel { get; init; } = string.Empty;
    public string ProductHandlingProfileCode { get; init; } = string.Empty;
    public string ProductHandlingProfileLabel { get; init; } = string.Empty;
    public string QuantityUnit { get; init; } = "개";
    public decimal ReservedQuantity { get; init; }
    public decimal ConfirmedWalkInSaleQuantity { get; init; }
    public decimal PotentialWalkInSaleQuantity { get; init; }
    public DateTimeOffset? MarketArrivalAt { get; init; }
    public DateTimeOffset? ReservationPickupStartsAt { get; init; }
    public DateTimeOffset? ReservationPickupEndsAt { get; init; }
    public DateTimeOffset? WalkInSaleStartsAt { get; init; }
    public DateTimeOffset? WalkInSaleEndsAt { get; init; }
    public bool ScheduleConfirmed { get; init; }
    public bool AssociationAgreementRequired { get; init; } = true;
    public bool AssociationAgreementConfirmed { get; init; }
    public bool RequiresMerchantDirectAcceptance { get; init; } = true;
    public bool ReservedInventoryProtected { get; init; } = true;
    public bool WalkInInventorySeparated { get; init; } = true;
    public bool PlatformAutomaticallyAssignsMerchants { get; init; }
    public string AnnouncementPolicyLabel { get; init; } = string.Empty;
    public string PublicAudienceLabel { get; init; } = string.Empty;
    public string ReservationProtectionLabel { get; init; } = string.Empty;
    public string UnsoldInventoryPolicyLabel { get; init; } = string.Empty;
    public CommunityDomesticMarketSupplySnapshot DomesticSupply { get; init; } = new();
    public IReadOnlyList<CommunityMarketDayMerchantRoleSnapshot> MerchantRoles { get; init; } = [];
    public IReadOnlyList<CommunityMarketDayStageSnapshot> Stages { get; init; } = [];

    public bool CanAdvertiseWalkInSale
        => ScheduleConfirmed
           && (!AssociationAgreementRequired || AssociationAgreementConfirmed)
           && ConfirmedWalkInSaleQuantity > 0
           && MerchantRoles.Where(role => role.Required).All(role => role.Accepted);

    public decimal UnconfirmedWalkInSaleQuantity
        => Math.Max(0m, PotentialWalkInSaleQuantity - ConfirmedWalkInSaleQuantity);

    public CommunityMarketDayStageSnapshot? CurrentStage
        => Stages.FirstOrDefault(stage => stage.State == CommunityMarketDayStageState.Current)
           ?? Stages.FirstOrDefault(stage => stage.State != CommunityMarketDayStageState.Completed);
}

public static class CommunityMarketDayScenarioFactory
{
    private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);

    public static CommunityMarketDaySnapshot Build(
        CommunityVoteResponse campaign,
        string productLabel,
        decimal reservedQuantity,
        decimal potentialQuantity,
        string quantityUnit)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var groupPurchase = campaign.GroupPurchase;
        if (groupPurchase is null
            || !IsTraditionalMarketScope(groupPurchase.ServiceAreaKey)
            || !string.Equals(
                CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
                    groupPurchase.DeliveryCountryCode),
                CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return new CommunityMarketDaySnapshot();
        }

        var profileCode = ResolveProfileCode(groupPurchase.HsCode);
        var walkInInterest = Math.Max(0m, potentialQuantity - reservedQuantity);
        return new CommunityMarketDaySnapshot
        {
            IsApplicable = true,
            State = CommunityMarketDayState.AssociationReview,
            Title = "공동구매 장날 제안",
            StatusLabel = "상인회 협의 전",
            MarketScopeKey = groupPurchase.ServiceAreaKey!.Trim(),
            MarketName = string.IsNullOrWhiteSpace(groupPurchase.ServiceAreaLabel)
                ? "전통시장 생활권"
                : groupPurchase.ServiceAreaLabel.Trim(),
            CadenceLabel = "시장 운영일과 협의",
            ProductLabel = productLabel,
            ProductHandlingProfileCode = profileCode,
            ProductHandlingProfileLabel = ResolveProfileLabel(profileCode),
            QuantityUnit = NormalizeUnit(quantityUnit),
            ReservedQuantity = Math.Max(0m, reservedQuantity),
            ConfirmedWalkInSaleQuantity = 0,
            PotentialWalkInSaleQuantity = walkInInterest,
            ScheduleConfirmed = false,
            AssociationAgreementConfirmed = false,
            PlatformAutomaticallyAssignsMerchants = false,
            AnnouncementPolicyLabel = "상인회와 일정·현장판매 물량을 확정한 뒤 공개",
            PublicAudienceLabel = "생활권 게시판 공개 범위 협의 전",
            ReservationProtectionLabel = "공동구매 예약 물량은 현장판매 물량과 분리",
            UnsoldInventoryPolicyLabel = "판매 주체와 반품·기부·다음 배치 처리 방식을 별도 합의",
            DomesticSupply = CommunityDomesticMarketSupplyScenarioFactory.Build(
                campaign,
                productLabel,
                string.IsNullOrWhiteSpace(groupPurchase.ServiceAreaLabel)
                    ? "전통시장 생활권"
                    : groupPurchase.ServiceAreaLabel.Trim(),
                reservedQuantity,
                potentialQuantity,
                quantityUnit),
            MerchantRoles = BuildMerchantRoles(profileCode, associationAccepted: false, merchantsAccepted: false),
            Stages = BuildStages(CommunityMarketDayState.AssociationReview)
        };
    }

    public static CommunityMarketDaySnapshot CreateFreshProducePilotPreview(
        DateTimeOffset now)
    {
        var arrival = BuildKoreaLocalTime(now, daysToAdd: 8, hour: 7, minute: 30);
        return new CommunityMarketDaySnapshot
        {
            IsApplicable = true,
            State = CommunityMarketDayState.PilotScheduled,
            Title = "제철 채소·과일 공동구매 장날",
            StatusLabel = "시범 장날 일정 확정",
            MarketScopeKey = "traditional-market:sample-seongnam",
            MarketName = "성남 생활권 전통시장 예시",
            CadenceLabel = "5일장형 시범 운영",
            ProductLabel = "제철 토마토·사과 꾸러미",
            ProductHandlingProfileCode = CommunityMarketDayProductHandlingProfileCodes.FreshProduce,
            ProductHandlingProfileLabel = "청과·채소 선별·소분·현장판매",
            QuantityUnit = "상자",
            ReservedQuantity = 80,
            ConfirmedWalkInSaleQuantity = 12,
            PotentialWalkInSaleQuantity = 16,
            MarketArrivalAt = arrival,
            ReservationPickupStartsAt = arrival.AddHours(2.5),
            ReservationPickupEndsAt = arrival.AddHours(6.5),
            WalkInSaleStartsAt = arrival.AddHours(3.5),
            WalkInSaleEndsAt = arrival.AddHours(8.5),
            ScheduleConfirmed = true,
            AssociationAgreementConfirmed = true,
            PlatformAutomaticallyAssignsMerchants = false,
            AnnouncementPolicyLabel = "일정 확정 뒤 지역 게시판·시장 현장판·구독 알림에 공개",
            PublicAudienceLabel = "공동구매 미참여 주민도 확정 현장판매 물량 안에서 구매",
            ReservationProtectionLabel = "예약 80상자는 가정 68상자·조리 가게 12상자로 표식하고 현장판매 12상자와 섞지 않음",
            UnsoldInventoryPolicyLabel = "현장판매 잔량은 판매 상인이 합의한 할인·기부·반품 기준으로 마감",
            DomesticSupply = CommunityDomesticMarketSupplyScenarioFactory.CreateFreshProducePilotPreview(arrival),
            MerchantRoles = BuildMerchantRoles(
                CommunityMarketDayProductHandlingProfileCodes.FreshProduce,
                associationAccepted: true,
                merchantsAccepted: true),
            Stages = BuildStages(CommunityMarketDayState.PilotScheduled)
        };
    }

    private static IReadOnlyList<CommunityMarketDayMerchantRoleSnapshot> BuildMerchantRoles(
        string profileCode,
        bool associationAccepted,
        bool merchantsAccepted)
    {
        var roles = new List<CommunityMarketDayMerchantRoleSnapshot>
        {
            new(
                CommunityMarketDayRoleCodes.MarketAssociationCoordinator,
                "전통시장 상인회·시장관리자",
                "공동장날 일정, 공용공간, 차량 동선과 참여 상점 모집 범위를 합의합니다.",
                true,
                associationAccepted,
                "공동사업 합의·공용시설 사용 범위")
        };

        if (profileCode == CommunityMarketDayProductHandlingProfileCodes.FreshProduce)
        {
            roles.Add(new(
                CommunityMarketDayRoleCodes.FreshProduceMerchant,
                "청과·채소 상인",
                "품질검수, 선별·등급분류, 원산지 확인과 판매를 직접 수락합니다.",
                true,
                merchantsAccepted,
                "취급 품목·처리량·원산지 표시"));
            roles.Add(new(
                CommunityMarketDayRoleCodes.ProduceSortingPackingOperator,
                "농산물 선별·소분 담당",
                "단순 선별·다듬기·소분과 세척·절단 등 추가 가공을 구분해 맡습니다.",
                true,
                merchantsAccepted,
                "작업 범위·시설·위생 조건"));
        }
        else if (profileCode == CommunityMarketDayProductHandlingProfileCodes.FisheriesProducts)
        {
            roles.Add(new(
                CommunityMarketDayRoleCodes.FisheriesMerchant,
                "수산물 판매·처리 사업자",
                "선도·보관온도·원산지와 직접 맡을 손질·소분·판매 범위를 수락합니다.",
                true,
                merchantsAccepted,
                "수산물 취급 범위·시설·온도·원산지 표시"));
        }
        else if (profileCode == CommunityMarketDayProductHandlingProfileCodes.Meat)
        {
            roles.Add(new(
                CommunityMarketDayRoleCodes.LicensedMeatProcessor,
                "허가·신고된 식육 작업 사업자",
                "신고된 범위 안에서 정형·절단·소분과 소비자 포장을 맡습니다.",
                true,
                merchantsAccepted,
                "식육 영업 종류·시설·처리량"));
        }
        else
        {
            roles.Add(new(
                CommunityMarketDayRoleCodes.ProductMerchant,
                "품목 취급 상인",
                "상품 검수·분류·소분·판매 중 직접 맡을 범위를 제안합니다.",
                true,
                merchantsAccepted,
                "품목별 영업 범위·처리량"));
        }

        if (profileCode is CommunityMarketDayProductHandlingProfileCodes.FreshProduce
            or CommunityMarketDayProductHandlingProfileCodes.FisheriesProducts)
        {
            roles.Add(new(
                CommunityMarketDayRoleCodes.MarketFoodBusinessIngredientBuyer,
                "시장 조리·식음료 가게",
                "가정 예약분을 침해하지 않는 식재료 물량의 규격·수량·가격·보관·조리 사용 조건을 직접 수락합니다.",
                false,
                merchantsAccepted,
                "가게 참조키·영업 범위·조리시설·보관 조건"));
        }

        roles.Add(new(
            CommunityMarketDayRoleCodes.MarketDaySeller,
            "장날 현장 판매 상인",
            "확정된 공개판매 물량, 가격·표시와 판매 마감 결과를 기록합니다.",
            true,
            merchantsAccepted,
            "현장 판매 수량·가격·표시 책임"));
        roles.Add(new(
            CommunityMarketDayRoleCodes.MarketVisualDesigner,
            "시장 꾸미기 디자이너",
            "시장 이야기·색·표식을 꾸미기 팩으로 제안하고 저작권과 사용 범위를 확인합니다.",
            false,
            associationAccepted && merchantsAccepted,
            "저작권·라이선스·상인회 사용 승인"));
        roles.Add(new(
            CommunityMarketDayRoleCodes.LocalDeliveryProvider,
            "생활권 배송 참여자",
            "배송 신청분만 별도 인수하고 공개 게시판에 개인 주소를 노출하지 않습니다.",
            false,
            false,
            "서비스 권역·보관 조건·직접 계약"));
        return roles;
    }

    private static IReadOnlyList<CommunityMarketDayStageSnapshot> BuildStages(
        CommunityMarketDayState state)
    {
        var scheduled = state is CommunityMarketDayState.PilotScheduled
            or CommunityMarketDayState.Open
            or CommunityMarketDayState.Completed;
        var open = state is CommunityMarketDayState.Open or CommunityMarketDayState.Completed;
        var completed = state == CommunityMarketDayState.Completed;

        return
        [
            new(1, "공동구매 수요와 예약 물량 구분", "참여자의 예약 수량과 추가 관심 수량을 분리합니다.", CommunityMarketDayStageState.Completed),
            new(2, "상인회·시장관리자 공동사업 합의", "장날 일정, 공간, 동선, 공용시설과 책임 범위를 서면으로 남깁니다.", scheduled ? CommunityMarketDayStageState.Completed : CommunityMarketDayStageState.Current),
            new(3, "품목별 참여 상점 직접 수락", "취급 품목, 작업 범위, 처리량과 비용을 각 상인이 확인합니다.", scheduled ? CommunityMarketDayStageState.Completed : CommunityMarketDayStageState.Waiting),
            new(4, "입고일·판매시간·공개 물량 확정", "예약 재고와 현장판매 재고를 분리해 일정과 함께 공지합니다.", scheduled ? CommunityMarketDayStageState.Completed : CommunityMarketDayStageState.Waiting),
            new(5, "장날 입고·검수·상점별 인계", "확정 물량만 시장으로 들이고 품질·수량·표시를 확인합니다.", scheduled && !open ? CommunityMarketDayStageState.Current : open ? CommunityMarketDayStageState.Completed : CommunityMarketDayStageState.Waiting),
            new(6, "공동구매 예약 수령", "예약 물량은 참여자 표식과 수령 기록을 유지합니다.", open ? CommunityMarketDayStageState.Completed : CommunityMarketDayStageState.Waiting),
            new(7, "여유 물량 현장판매", "미참여 주민에게는 별도로 확정된 물량 안에서만 판매합니다.", state == CommunityMarketDayState.Open ? CommunityMarketDayStageState.Current : completed ? CommunityMarketDayStageState.Completed : CommunityMarketDayStageState.Waiting),
            new(8, "마감·정산·다음 장날 기록", "판매량, 잔량, 지역 상인 보상과 주민 반응을 다음 공동구매에 남깁니다.", completed ? CommunityMarketDayStageState.Completed : CommunityMarketDayStageState.Waiting)
        ];
    }

    private static bool IsTraditionalMarketScope(string? serviceAreaKey)
        => !string.IsNullOrWhiteSpace(serviceAreaKey)
           && serviceAreaKey.StartsWith("traditional-market:", StringComparison.OrdinalIgnoreCase);

    private static string ResolveProfileCode(string? hsCode)
    {
        var normalizedHsCode = string.Concat((hsCode ?? string.Empty).Where(char.IsDigit));
        if (normalizedHsCode.StartsWith("03", StringComparison.Ordinal))
        {
            return CommunityMarketDayProductHandlingProfileCodes.FisheriesProducts;
        }

        if (normalizedHsCode.StartsWith("02", StringComparison.Ordinal))
        {
            return CommunityMarketDayProductHandlingProfileCodes.Meat;
        }

        if (normalizedHsCode.StartsWith("07", StringComparison.Ordinal)
            || normalizedHsCode.StartsWith("08", StringComparison.Ordinal))
        {
            return CommunityMarketDayProductHandlingProfileCodes.FreshProduce;
        }

        return CommunityMarketDayProductHandlingProfileCodes.GeneralGoods;
    }

    private static string ResolveProfileLabel(string profileCode)
        => profileCode switch
        {
            CommunityMarketDayProductHandlingProfileCodes.FreshProduce => "청과·채소 선별·소분·현장판매",
            CommunityMarketDayProductHandlingProfileCodes.FisheriesProducts => "수산물 온도·선도·원산지 확인·현장판매",
            CommunityMarketDayProductHandlingProfileCodes.Meat => "허가된 식육 2차 가공·현장판매",
            _ => "품목별 검수·분류·소분·현장판매"
        };

    private static DateTimeOffset BuildKoreaLocalTime(
        DateTimeOffset now,
        int daysToAdd,
        int hour,
        int minute)
    {
        var localDate = now.ToOffset(KoreaOffset).Date.AddDays(daysToAdd);
        return new DateTimeOffset(
            localDate.Year,
            localDate.Month,
            localDate.Day,
            hour,
            minute,
            0,
            KoreaOffset);
    }

    private static string NormalizeUnit(string? quantityUnit)
        => string.IsNullOrWhiteSpace(quantityUnit) ? "개" : quantityUnit.Trim();
}
