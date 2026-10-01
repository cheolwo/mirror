namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class CommunityMarketIngredientSupplyRoleCodes
{
    public const string MarketFoodBusinessIngredientBuyer = "market-food-business-ingredient-buyer";
}

public static class CommunityMarketIngredientSupplyStageCodes
{
    public const string HouseholdAllocationLock = "HouseholdAllocationLock";
    public const string BusinessDemandRegistration = "BusinessDemandRegistration";
    public const string DirectAcceptanceAndVerification = "DirectAcceptanceAndVerification";
    public const string ReceivingLotAllocation = "ReceivingLotAllocation";
    public const string KitchenHandoff = "KitchenHandoff";
    public const string SeparateSettlement = "SeparateSettlement";
}

public enum CommunityMarketIngredientSupplyState
{
    InterestReview,
    SupplyConfirmed,
    MarketReceived,
    KitchenHandoffCompleted,
    Settled
}

public enum CommunityMarketIngredientSupplyStageState
{
    Completed,
    Current,
    Waiting,
    DirectAcceptanceRequired
}

public sealed record CommunityMarketIngredientBusinessSnapshot(
    string BusinessReferenceKey,
    string DisplayName,
    string BusinessTypeLabel,
    string IngredientUseLabel,
    decimal RequestedQuantity,
    decimal ConfirmedQuantity,
    bool DirectlyAccepted,
    bool BusinessScopeVerified,
    bool StorageConditionConfirmed,
    string StatusLabel,
    string VerificationLabel);

public sealed record CommunityMarketIngredientSupplyStageSnapshot(
    int Number,
    string Code,
    string Label,
    string Detail,
    CommunityMarketIngredientSupplyStageState State,
    string EvidenceLabel);

public sealed class CommunityMarketIngredientSupplySnapshot
{
    public bool IsApplicable { get; init; }
    public CommunityMarketIngredientSupplyState State { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string ProductLabel { get; init; } = string.Empty;
    public string ProductCategoryLabel { get; init; } = string.Empty;
    public string QuantityUnit { get; init; } = "개";
    public decimal HouseholdReservedQuantity { get; init; }
    public decimal ConfirmedBusinessSupplyQuantity { get; init; }
    public decimal PotentialBusinessSupplyQuantity { get; init; }
    public bool HouseholdReservationProtected { get; init; } = true;
    public bool RequiresBusinessDirectAcceptance { get; init; } = true;
    public bool RequiresBusinessScopeVerification { get; init; } = true;
    public bool RequiresStorageConditionConfirmation { get; init; } = true;
    public bool RequiresSourceLotTraceability { get; init; } = true;
    public bool PricingAndSettlementSeparated { get; init; } = true;
    public bool PlatformAutomaticallyAssignsBusinesses { get; init; }
    public string DemandPolicyLabel { get; init; } = string.Empty;
    public string AllocationPolicyLabel { get; init; } = string.Empty;
    public string PricingPolicyLabel { get; init; } = string.Empty;
    public string TraceabilityPolicyLabel { get; init; } = string.Empty;
    public IReadOnlyList<CommunityMarketIngredientBusinessSnapshot> Businesses { get; init; } = [];
    public IReadOnlyList<CommunityMarketIngredientSupplyStageSnapshot> Stages { get; init; } = [];

    public decimal UnconfirmedBusinessSupplyQuantity
        => Math.Max(0m, PotentialBusinessSupplyQuantity - ConfirmedBusinessSupplyQuantity);

    public bool CanConfirmBusinessSupply
        => HouseholdReservationProtected
           && ConfirmedBusinessSupplyQuantity > 0
           && ConfirmedBusinessSupplyQuantity <= PotentialBusinessSupplyQuantity
           && Businesses.Count > 0
           && Businesses.All(business =>
               business.DirectlyAccepted
               && business.BusinessScopeVerified
               && business.StorageConditionConfirmed)
           && Businesses.Sum(business => business.ConfirmedQuantity) == ConfirmedBusinessSupplyQuantity;

    public CommunityMarketIngredientSupplyStageSnapshot? CurrentStage
        => Stages.FirstOrDefault(stage => stage.State == CommunityMarketIngredientSupplyStageState.Current)
           ?? Stages.FirstOrDefault(stage => stage.State == CommunityMarketIngredientSupplyStageState.DirectAcceptanceRequired)
           ?? Stages.FirstOrDefault(stage => stage.State != CommunityMarketIngredientSupplyStageState.Completed);
}

public static class CommunityMarketIngredientSupplyScenarioFactory
{
    public static CommunityMarketIngredientSupplySnapshot Build(
        string productLabel,
        string productCategoryLabel,
        decimal committedQuantity,
        decimal additionalPotentialQuantity,
        string quantityUnit)
        => new()
        {
            IsApplicable = true,
            State = CommunityMarketIngredientSupplyState.InterestReview,
            Title = "시장 조리 가게 식재료 공급",
            StatusLabel = "가게 수요 확인 전",
            ProductLabel = productLabel,
            ProductCategoryLabel = productCategoryLabel,
            QuantityUnit = NormalizeUnit(quantityUnit),
            HouseholdReservedQuantity = Math.Max(0m, committedQuantity),
            ConfirmedBusinessSupplyQuantity = 0,
            PotentialBusinessSupplyQuantity = Math.Max(0m, additionalPotentialQuantity),
            HouseholdReservationProtected = true,
            RequiresBusinessDirectAcceptance = true,
            RequiresBusinessScopeVerification = true,
            RequiresStorageConditionConfirmation = true,
            RequiresSourceLotTraceability = true,
            PricingAndSettlementSeparated = true,
            PlatformAutomaticallyAssignsBusinesses = false,
            DemandPolicyLabel = "시장 안의 조리·식음료 가게가 필요한 품목·수량·사용 목적을 직접 등록",
            AllocationPolicyLabel = "기존 가정 예약분을 먼저 잠그고 가게가 수락한 물량만 별도 공급 배치로 확정",
            PricingPolicyLabel = "가정 공동구매와 가게 식재료 공급의 단가·세금·결제·반품 조건을 분리",
            TraceabilityPolicyLabel = "산지 lot에서 시장 입고, 가게 인수와 조리 사용 기록까지 참조를 유지",
            Businesses = [],
            Stages = BuildStages(CommunityMarketIngredientSupplyState.InterestReview)
        };

    public static CommunityMarketIngredientSupplySnapshot CreateFreshProducePilotPreview()
        => new()
        {
            IsApplicable = true,
            State = CommunityMarketIngredientSupplyState.SupplyConfirmed,
            Title = "시장 조리 가게 식재료 공급",
            StatusLabel = "가게 공급 12상자 확정",
            ProductLabel = "제철 토마토·사과 꾸러미",
            ProductCategoryLabel = "국내 농산물",
            QuantityUnit = "상자",
            HouseholdReservedQuantity = 68,
            ConfirmedBusinessSupplyQuantity = 12,
            PotentialBusinessSupplyQuantity = 12,
            HouseholdReservationProtected = true,
            RequiresBusinessDirectAcceptance = true,
            RequiresBusinessScopeVerification = true,
            RequiresStorageConditionConfirmation = true,
            RequiresSourceLotTraceability = true,
            PricingAndSettlementSeparated = true,
            PlatformAutomaticallyAssignsBusinesses = false,
            DemandPolicyLabel = "상인회가 자동 배정하지 않고 조리 가게가 필요한 식재료와 사용량을 직접 제안",
            AllocationPolicyLabel = "가정 예약 68상자를 잠근 뒤 가게 예약 12상자를 입고 lot에서 별도 표식",
            PricingPolicyLabel = "가정용 꾸러미와 가게용 식재료의 규격·단가·증빙·정산 조건을 서로 분리",
            TraceabilityPolicyLabel = "생산지·수확일·출하 lot를 가게 인수와 실제 조리 사용 배치까지 연결",
            Businesses =
            [
                new(
                    "market-food-business:sample-side-dish",
                    "시장 반찬·도시락 가게 예시",
                    "일반음식점 등 실제 조리·판매 범위 확인",
                    "토마토를 반찬·도시락 조리에 사용",
                    7,
                    7,
                    true,
                    true,
                    true,
                    "공급 조건 직접 수락",
                    "영업 범위·조리시설·보관 조건 확인 예시"),
                new(
                    "market-food-business:sample-juice-dessert",
                    "시장 음료·디저트 가게 예시",
                    "휴게음식점 등 실제 조리·판매 범위 확인",
                    "사과를 음료·디저트 조리에 사용",
                    5,
                    5,
                    true,
                    true,
                    true,
                    "공급 조건 직접 수락",
                    "영업 범위·조리시설·보관 조건 확인 예시")
            ],
            Stages = BuildStages(CommunityMarketIngredientSupplyState.SupplyConfirmed)
        };

    private static IReadOnlyList<CommunityMarketIngredientSupplyStageSnapshot> BuildStages(
        CommunityMarketIngredientSupplyState state)
    {
        var supplyConfirmed = state is CommunityMarketIngredientSupplyState.SupplyConfirmed
            or CommunityMarketIngredientSupplyState.MarketReceived
            or CommunityMarketIngredientSupplyState.KitchenHandoffCompleted
            or CommunityMarketIngredientSupplyState.Settled;
        var marketReceived = state is CommunityMarketIngredientSupplyState.MarketReceived
            or CommunityMarketIngredientSupplyState.KitchenHandoffCompleted
            or CommunityMarketIngredientSupplyState.Settled;
        var handoffCompleted = state is CommunityMarketIngredientSupplyState.KitchenHandoffCompleted
            or CommunityMarketIngredientSupplyState.Settled;
        var settled = state == CommunityMarketIngredientSupplyState.Settled;

        return
        [
            Stage(1, CommunityMarketIngredientSupplyStageCodes.HouseholdAllocationLock,
                "가정 공동구매 예약분 잠금",
                "먼저 모인 가정 예약 수량을 가게 공급이나 현장판매 재고로 자동 전환하지 않습니다.",
                CommunityMarketIngredientSupplyStageState.Completed,
                "가정 예약 수량·수령권"),
            Stage(2, CommunityMarketIngredientSupplyStageCodes.BusinessDemandRegistration,
                "시장 조리 가게 식재료 수요 등록",
                "가게가 필요한 품목, 규격, 수량, 사용 목적, 수령 시점과 보관 조건을 직접 제안합니다.",
                supplyConfirmed
                    ? CommunityMarketIngredientSupplyStageState.Completed
                    : CommunityMarketIngredientSupplyStageState.Current,
                "가게 참조키·수요·사용 목적"),
            Stage(3, CommunityMarketIngredientSupplyStageCodes.DirectAcceptanceAndVerification,
                "가게 직접 수락·영업 범위 확인",
                "가게가 공급 가격과 인수 책임을 수락하고 실제 조리·판매 영업 범위와 시설 조건을 별도로 확인합니다.",
                supplyConfirmed
                    ? CommunityMarketIngredientSupplyStageState.Completed
                    : CommunityMarketIngredientSupplyStageState.DirectAcceptanceRequired,
                "수락 리비전·영업 범위·보관 조건"),
            Stage(4, CommunityMarketIngredientSupplyStageCodes.ReceivingLotAllocation,
                "시장 입고 시 가게 공급 lot 분리",
                "품질·수량·원산지를 검수한 뒤 가정 예약분과 가게 공급분을 각각 표식하고 인수 수량을 확정합니다.",
                state == CommunityMarketIngredientSupplyState.SupplyConfirmed
                    ? CommunityMarketIngredientSupplyStageState.Current
                    : marketReceived
                        ? CommunityMarketIngredientSupplyStageState.Completed
                        : CommunityMarketIngredientSupplyStageState.Waiting,
                "입고 lot·가정/가게 배정표"),
            Stage(5, CommunityMarketIngredientSupplyStageCodes.KitchenHandoff,
                "가게 조리장 인계·사용 기록",
                "가게가 식재료를 인수하고 산지 lot, 수량, 보관 상태와 조리 사용 배치를 내부 기록에 연결합니다.",
                state == CommunityMarketIngredientSupplyState.MarketReceived
                    ? CommunityMarketIngredientSupplyStageState.Current
                    : handoffCompleted
                        ? CommunityMarketIngredientSupplyStageState.Completed
                        : CommunityMarketIngredientSupplyStageState.Waiting,
                "가게 인수·보관·사용 배치"),
            Stage(6, CommunityMarketIngredientSupplyStageCodes.SeparateSettlement,
                "가게 공급 별도 정산·다음 수요 기록",
                "산지 대금, 선별·운송·시장 작업과 가게 공급 조건을 가정 공동구매 정산과 구분해 마감합니다.",
                settled
                    ? CommunityMarketIngredientSupplyStageState.Completed
                    : handoffCompleted
                        ? CommunityMarketIngredientSupplyStageState.Current
                        : CommunityMarketIngredientSupplyStageState.Waiting,
                "가게 공급 거래·정산·재참여 의향")
        ];
    }

    private static CommunityMarketIngredientSupplyStageSnapshot Stage(
        int number,
        string code,
        string label,
        string detail,
        CommunityMarketIngredientSupplyStageState state,
        string evidenceLabel)
        => new(number, code, label, detail, state, evidenceLabel);

    private static string NormalizeUnit(string? value)
        => string.IsNullOrWhiteSpace(value) ? "개" : value.Trim();
}
