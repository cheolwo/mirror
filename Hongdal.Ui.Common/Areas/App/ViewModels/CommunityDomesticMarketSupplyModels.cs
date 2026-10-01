using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class CommunityDomesticMarketSupplyStageCodes
{
    public const string DemandCommitment = "DemandCommitment";
    public const string SourceAcceptance = "SourceAcceptance";
    public const string LotAndTermsConfirmation = "LotAndTermsConfirmation";
    public const string OriginSortingAndLoading = "OriginSortingAndLoading";
    public const string DirectTransportToMarket = "DirectTransportToMarket";
    public const string MarketReceiving = "MarketReceiving";
    public const string MerchantAllocation = "MerchantAllocation";
    public const string ReservedPickupAndDelivery = "ReservedPickupAndDelivery";
    public const string ConfirmedWalkInSale = "ConfirmedWalkInSale";
    public const string Closeout = "Closeout";
}

public static class CommunityDomesticMarketSupplyRoleCodes
{
    public const string ProducerOrCooperative = "domestic-producer-or-cooperative";
    public const string OriginSortingPackingOperator = "domestic-origin-sorting-packing-operator";
    public const string OriginToMarketCarrier = "domestic-origin-to-market-carrier";
    public const string MarketReceivingCoordinator = "domestic-market-receiving-coordinator";
}

public static class CommunityDomesticMarketSupplyProductCategoryCodes
{
    public const string AgriculturalProducts = "domestic-agricultural-products";
    public const string FisheriesProducts = "domestic-fisheries-products";
}

public enum CommunityDomesticMarketSupplyState
{
    SourceReview,
    ReadyForDispatch,
    InTransit,
    MarketReceived,
    AllocationCompleted
}

public enum CommunityDomesticMarketSupplyStageState
{
    Completed,
    Current,
    Waiting,
    DirectAcceptanceRequired
}

public sealed record CommunityDomesticMarketSupplyStageSnapshot(
    int Number,
    string Code,
    string Label,
    string Detail,
    string ResponsibleRoleLabel,
    CommunityDomesticMarketSupplyStageState State,
    string EvidenceLabel);

public sealed record CommunityDomesticMarketSupplyRoleSnapshot(
    string RoleCode,
    string Label,
    string Responsibility,
    bool Required,
    bool Accepted,
    string VerificationLabel);

public sealed class CommunityDomesticMarketSupplySnapshot
{
    public bool IsApplicable { get; init; }
    public CommunityDomesticMarketSupplyState State { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string ProductCategoryCode { get; init; } = string.Empty;
    public string ProductCategoryLabel { get; init; } = string.Empty;
    public string ProductLabel { get; init; } = string.Empty;
    public string SourceAreaLabel { get; init; } = string.Empty;
    public string DestinationMarketLabel { get; init; } = string.Empty;
    public string HarvestOrLandingLabel { get; init; } = string.Empty;
    public string TransportConditionLabel { get; init; } = string.Empty;
    public string OriginTraceabilityLabel { get; init; } = string.Empty;
    public string QuantityUnit { get; init; } = "개";
    public decimal CommittedQuantity { get; init; }
    public decimal AdditionalPotentialQuantity { get; init; }
    public bool SourceAcceptanceConfirmed { get; init; }
    public bool LotAndTermsConfirmed { get; init; }
    public bool CarrierAcceptanceConfirmed { get; init; }
    public bool MarketReceivingConfirmed { get; init; }
    public bool ScheduleConfirmed { get; init; }
    public bool RequiresColdChain { get; init; }
    public bool RequiresCustomsClearance { get; init; }
    public bool PlatformAutomaticallySelectsSuppliers { get; init; }
    public bool PlatformAutomaticallyAssignsCarriers { get; init; }
    public bool ReservedAllocationProtected { get; init; } = true;
    public string SupplierSelectionPolicyLabel { get; init; } = string.Empty;
    public string ReceivingPolicyLabel { get; init; } = string.Empty;
    public string AllocationPolicyLabel { get; init; } = string.Empty;
    public CommunityMarketIngredientSupplySnapshot MarketIngredientSupply { get; init; } = new();
    public IReadOnlyList<CommunityDomesticMarketSupplyRoleSnapshot> Roles { get; init; } = [];
    public IReadOnlyList<CommunityDomesticMarketSupplyStageSnapshot> Stages { get; init; } = [];

    public bool CanDispatchDirectlyToMarket
        => SourceAcceptanceConfirmed
           && LotAndTermsConfirmed
           && CarrierAcceptanceConfirmed
           && MarketReceivingConfirmed
           && ScheduleConfirmed
           && CommittedQuantity > 0;

    public CommunityDomesticMarketSupplyStageSnapshot? CurrentStage
        => Stages.FirstOrDefault(stage => stage.State == CommunityDomesticMarketSupplyStageState.Current)
           ?? Stages.FirstOrDefault(stage => stage.State == CommunityDomesticMarketSupplyStageState.DirectAcceptanceRequired)
           ?? Stages.FirstOrDefault(stage => stage.State != CommunityDomesticMarketSupplyStageState.Completed);
}

public static class CommunityDomesticMarketSupplyScenarioFactory
{
    public static CommunityDomesticMarketSupplySnapshot Build(
        CommunityVoteResponse campaign,
        string productLabel,
        string destinationMarketLabel,
        decimal committedQuantity,
        decimal potentialQuantity,
        string quantityUnit)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var groupPurchase = campaign.GroupPurchase;
        var categoryCode = ResolveProductCategoryCode(groupPurchase?.HsCode);
        if (!IsApplicable(groupPurchase, categoryCode))
        {
            return new CommunityDomesticMarketSupplySnapshot();
        }

        return new CommunityDomesticMarketSupplySnapshot
        {
            IsApplicable = true,
            State = CommunityDomesticMarketSupplyState.SourceReview,
            Title = "국내 산지 공동출하·전통시장 직입고",
            StatusLabel = "생산자·운송 주체 직접 수락 전",
            ProductCategoryCode = categoryCode,
            ProductCategoryLabel = ResolveProductCategoryLabel(categoryCode),
            ProductLabel = productLabel,
            SourceAreaLabel = "국내 산지·생산자 협의 전",
            DestinationMarketLabel = destinationMarketLabel,
            HarvestOrLandingLabel = ResolveHarvestOrLandingLabel(categoryCode, confirmed: false),
            TransportConditionLabel = ResolveTransportConditionLabel(categoryCode),
            OriginTraceabilityLabel = "생산지·생산자 또는 어획·양식 정보와 출하 lot 확인 전",
            QuantityUnit = NormalizeUnit(quantityUnit),
            CommittedQuantity = Math.Max(0m, committedQuantity),
            AdditionalPotentialQuantity = Math.Max(0m, potentialQuantity - committedQuantity),
            SourceAcceptanceConfirmed = false,
            LotAndTermsConfirmed = false,
            CarrierAcceptanceConfirmed = false,
            MarketReceivingConfirmed = false,
            ScheduleConfirmed = false,
            RequiresColdChain = IsFisheries(categoryCode),
            RequiresCustomsClearance = false,
            PlatformAutomaticallySelectsSuppliers = false,
            PlatformAutomaticallyAssignsCarriers = false,
            SupplierSelectionPolicyLabel = "생산자·생산자단체·산지 공급 주체가 물량과 가격을 직접 제안·수락",
            ReceivingPolicyLabel = "상인회와 품목 상인이 입고 시간·처리량·검수 기준을 수락한 물량만 시장으로 직송",
            AllocationPolicyLabel = "가정·가게 예약 물량을 용도별로 먼저 배정하고 별도 확정된 여유 물량만 장날 판매",
            MarketIngredientSupply = CommunityMarketIngredientSupplyScenarioFactory.Build(
                productLabel,
                ResolveProductCategoryLabel(categoryCode),
                committedQuantity,
                Math.Max(0m, potentialQuantity - committedQuantity),
                quantityUnit),
            Roles = BuildRoles(categoryCode, accepted: false),
            Stages = BuildStages(CommunityDomesticMarketSupplyState.SourceReview, categoryCode)
        };
    }

    public static CommunityDomesticMarketSupplySnapshot CreateFreshProducePilotPreview(
        DateTimeOffset marketArrivalAt)
        => new()
        {
            IsApplicable = true,
            State = CommunityDomesticMarketSupplyState.ReadyForDispatch,
            Title = "국내 산지 공동출하·전통시장 직입고",
            StatusLabel = "산지 출하 배치 확정",
            ProductCategoryCode = CommunityDomesticMarketSupplyProductCategoryCodes.AgriculturalProducts,
            ProductCategoryLabel = "국내 농산물",
            ProductLabel = "제철 토마토·사과 꾸러미",
            SourceAreaLabel = "경기 광주·충북 충주 생산자 공동 출하",
            DestinationMarketLabel = "성남 생활권 전통시장 예시",
            HarvestOrLandingLabel = "출하 전날 수확·선별 lot 확정",
            TransportConditionLabel = "품목별 적정 온도·통풍 조건을 확인한 국내 직송",
            OriginTraceabilityLabel = "생산자·생산지·수확일·출하 상자 표식을 시장 입고 기록과 연결",
            QuantityUnit = "상자",
            CommittedQuantity = 80,
            AdditionalPotentialQuantity = 16,
            SourceAcceptanceConfirmed = true,
            LotAndTermsConfirmed = true,
            CarrierAcceptanceConfirmed = true,
            MarketReceivingConfirmed = true,
            ScheduleConfirmed = true,
            RequiresColdChain = false,
            RequiresCustomsClearance = false,
            PlatformAutomaticallySelectsSuppliers = false,
            PlatformAutomaticallyAssignsCarriers = false,
            ReservedAllocationProtected = true,
            SupplierSelectionPolicyLabel = "생산자 공동 출하 조건과 상인 매입·판매 조건을 당사자가 직접 수락",
            ReceivingPolicyLabel = $"{marketArrivalAt:MM월 dd일 HH:mm} 시장 입고 슬롯과 100상자 처리 한도를 확인",
            AllocationPolicyLabel = "예약 80상자를 가정 68상자·조리 가게 12상자로 나누고 현장판매 물량은 별도 표식",
            MarketIngredientSupply = CommunityMarketIngredientSupplyScenarioFactory.CreateFreshProducePilotPreview(),
            Roles = BuildRoles(
                CommunityDomesticMarketSupplyProductCategoryCodes.AgriculturalProducts,
                accepted: true),
            Stages = BuildStages(
                CommunityDomesticMarketSupplyState.ReadyForDispatch,
                CommunityDomesticMarketSupplyProductCategoryCodes.AgriculturalProducts)
        };

    public static bool IsApplicable(
        CommunityGroupPurchaseVoteResponse? groupPurchase,
        string? productCategoryCode = null)
    {
        if (groupPurchase is null
            || string.IsNullOrWhiteSpace(productCategoryCode ?? ResolveProductCategoryCode(groupPurchase.HsCode)))
        {
            return false;
        }

        var sourceCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            groupPurchase.ShipFromCountryCode);
        var destinationCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            groupPurchase.DeliveryCountryCode);
        return string.Equals(
                   sourceCountryCode,
                   CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   destinationCountryCode,
                   CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                   StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(groupPurchase.ServiceAreaKey)
               && groupPurchase.ServiceAreaKey.StartsWith(
                   "traditional-market:",
                   StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveProductCategoryCode(string? hsCode)
    {
        var chapter = NormalizeHsCode(hsCode);
        if (chapter.StartsWith("03", StringComparison.Ordinal))
        {
            return CommunityDomesticMarketSupplyProductCategoryCodes.FisheriesProducts;
        }

        if (chapter.Length >= 2
            && int.TryParse(chapter[..2], out var chapterNumber)
            && chapterNumber is >= 6 and <= 14)
        {
            return CommunityDomesticMarketSupplyProductCategoryCodes.AgriculturalProducts;
        }

        return string.Empty;
    }

    private static IReadOnlyList<CommunityDomesticMarketSupplyRoleSnapshot> BuildRoles(
        string categoryCode,
        bool accepted)
    {
        var fisheries = IsFisheries(categoryCode);
        return
        [
            new(
                CommunityDomesticMarketSupplyRoleCodes.ProducerOrCooperative,
                fisheries ? "어업인·양식업자·산지 공급 주체" : "농가·생산자단체·산지 공급 주체",
                fisheries
                    ? "어획·양식 정보, 출하 가능량, 가격과 인계 조건을 직접 제안·수락합니다."
                    : "생산지, 수확 시점, 출하 가능량, 가격과 인계 조건을 직접 제안·수락합니다.",
                true,
                accepted,
                fisheries ? "생산·공급 주체·어획 또는 양식 정보" : "생산자·생산지·출하 가능량"),
            new(
                CommunityDomesticMarketSupplyRoleCodes.OriginSortingPackingOperator,
                fisheries ? "산지 수산물 선별·포장 주체" : "산지 농산물 선별·포장 주체",
                fisheries
                    ? "선도·크기·품질을 확인하고 보관온도와 lot가 유지되는 포장·상차 범위를 수락합니다."
                    : "품질·등급을 확인하고 시장 입고와 주문별 배분에 맞는 선별·포장 범위를 수락합니다.",
                true,
                accepted,
                fisheries ? "산지 처리 범위·포장·온도" : "선별 기준·포장 단위·작업 범위"),
            new(
                CommunityDomesticMarketSupplyRoleCodes.OriginToMarketCarrier,
                fisheries ? "국내 냉장·냉동 운송 주체" : "국내 산지 직송 운송 주체",
                "확정된 산지 lot와 수량을 시장 입고 슬롯에 맞춰 운송하고 인계 기록을 남깁니다.",
                true,
                accepted,
                fisheries ? "차량·온도대·상하차·운송 계약" : "차량·적재 조건·운송 계약"),
            new(
                CommunityDomesticMarketSupplyRoleCodes.MarketReceivingCoordinator,
                "전통시장 입고·배분 조정 주체",
                "상인회와 품목 상인이 입고 시간, 검수, 처리량과 가정·가게 예약 물량 배분 책임을 직접 수락합니다.",
                true,
                accepted,
                fisheries ? "시장 냉장시설·처리량·참여 상인 수락" : "시장 하역 동선·처리량·참여 상인 수락")
        ];
    }

    private static IReadOnlyList<CommunityDomesticMarketSupplyStageSnapshot> BuildStages(
        CommunityDomesticMarketSupplyState state,
        string categoryCode)
    {
        var readyForDispatch = state is CommunityDomesticMarketSupplyState.ReadyForDispatch
            or CommunityDomesticMarketSupplyState.InTransit
            or CommunityDomesticMarketSupplyState.MarketReceived
            or CommunityDomesticMarketSupplyState.AllocationCompleted;
        var inTransit = state is CommunityDomesticMarketSupplyState.InTransit
            or CommunityDomesticMarketSupplyState.MarketReceived
            or CommunityDomesticMarketSupplyState.AllocationCompleted;
        var marketReceived = state is CommunityDomesticMarketSupplyState.MarketReceived
            or CommunityDomesticMarketSupplyState.AllocationCompleted;
        var allocationCompleted = state == CommunityDomesticMarketSupplyState.AllocationCompleted;
        var fisheries = IsFisheries(categoryCode);

        return
        [
            Stage(1, CommunityDomesticMarketSupplyStageCodes.DemandCommitment,
                "공동구매 수요·최소 출하량 확정",
                "예약 수량과 추가 관심 수량을 구분하고 산지에서 출하할 최소 단위를 확인합니다.",
                "공동구매 참여자·대표", CommunityDomesticMarketSupplyStageState.Completed, "예약 원장·최소 출하량"),
            Stage(2, CommunityDomesticMarketSupplyStageCodes.SourceAcceptance,
                fisheries ? "국내 수산물 공급 주체 직접 수락" : "국내 생산자·산지 공급 주체 직접 수락",
                "플랫폼이 공급자를 자동 선정하지 않고 당사자가 품목, 물량, 가격과 책임 범위를 직접 수락합니다.",
                fisheries ? "어업인·양식업자·산지 공급 주체" : "농가·생산자단체·산지 공급 주체",
                readyForDispatch ? CommunityDomesticMarketSupplyStageState.Completed : CommunityDomesticMarketSupplyStageState.DirectAcceptanceRequired,
                "공급 제안·수락 리비전"),
            Stage(3, CommunityDomesticMarketSupplyStageCodes.LotAndTermsConfirmation,
                fisheries ? "어획·양식 정보·lot·조건 확정" : "생산지·수확일·lot·조건 확정",
                "출하 원산지, 품질·규격, 수량, 단가와 반품·감량 기준을 같은 리비전에 남깁니다.",
                "산지 공급 주체·공동구매 대표",
                readyForDispatch ? CommunityDomesticMarketSupplyStageState.Completed : CommunityDomesticMarketSupplyStageState.Waiting,
                fisheries ? "어획·양식·원산지·출하 lot" : "생산지·수확일·출하 lot"),
            Stage(4, CommunityDomesticMarketSupplyStageCodes.OriginSortingAndLoading,
                "산지 선별·포장·상차",
                fisheries
                    ? "선도와 보관온도를 확인하고 시장 검수와 주문별 배분에 맞는 단위로 포장·상차합니다."
                    : "품질·등급을 확인하고 시장 검수와 주문별 배분에 맞는 단위로 포장·상차합니다.",
                fisheries ? "산지 수산물 선별·포장 주체" : "산지 농산물 선별·포장 주체",
                state == CommunityDomesticMarketSupplyState.ReadyForDispatch
                    ? CommunityDomesticMarketSupplyStageState.Current
                    : inTransit
                        ? CommunityDomesticMarketSupplyStageState.Completed
                        : CommunityDomesticMarketSupplyStageState.Waiting,
                fisheries ? "선도·포장·온도 기록" : "선별·포장·상차 기록"),
            Stage(5, CommunityDomesticMarketSupplyStageCodes.DirectTransportToMarket,
                "산지에서 전통시장으로 직송",
                "중간 보관을 임의로 추가하지 않고 수락된 운송 주체가 확정 lot를 시장 입고 슬롯에 맞춰 인계합니다.",
                fisheries ? "국내 냉장·냉동 운송 주체" : "국내 산지 직송 운송 주체",
                state == CommunityDomesticMarketSupplyState.InTransit
                    ? CommunityDomesticMarketSupplyStageState.Current
                    : marketReceived
                        ? CommunityDomesticMarketSupplyStageState.Completed
                        : CommunityDomesticMarketSupplyStageState.Waiting,
                fisheries ? "차량·온도·상하차·인계 기록" : "차량·적재·도착·인계 기록"),
            Stage(6, CommunityDomesticMarketSupplyStageCodes.MarketReceiving,
                "전통시장 입고·수량·품질 검수",
                "품목 상인이 원산지, 수량, 품질, 포장 상태를 확인하고 차이가 있으면 주문별 배분 전에 기록합니다.",
                "상인회·시장관리자·품목 상인",
                state == CommunityDomesticMarketSupplyState.MarketReceived
                    ? CommunityDomesticMarketSupplyStageState.Current
                    : allocationCompleted
                        ? CommunityDomesticMarketSupplyStageState.Completed
                        : CommunityDomesticMarketSupplyStageState.Waiting,
                fisheries ? "입고 수량·선도·온도·원산지" : "입고 수량·품질·등급·원산지"),
            Stage(7, CommunityDomesticMarketSupplyStageCodes.MerchantAllocation,
                "품목 상인 검수·소분·용도별 배정",
                "상인이 직접 수락한 작업 범위 안에서 가정 예약, 조리 가게 공급과 현장판매 물량을 분리해 표식을 유지합니다.",
                fisheries ? "수산물 판매·처리 사업자" : "농산물 취급 상인·선별 담당",
                allocationCompleted ? CommunityDomesticMarketSupplyStageState.Completed : CommunityDomesticMarketSupplyStageState.Waiting,
                "작업 lot·가정/가게/현장판매 배정표"),
            Stage(8, CommunityDomesticMarketSupplyStageCodes.ReservedPickupAndDelivery,
                "예약 수령·동의한 생활권 배송",
                "참여자는 시장에서 직접 수령하거나 주소 제공에 동의한 주문만 별도 배송 주체에게 인계합니다.",
                "판매 상인·수령 거점·지역 배송 주체",
                allocationCompleted ? CommunityDomesticMarketSupplyStageState.Current : CommunityDomesticMarketSupplyStageState.Waiting,
                "수령권·배송 동의·인계 기록"),
            Stage(9, CommunityDomesticMarketSupplyStageCodes.ConfirmedWalkInSale,
                "확정 여유 물량 장날 판매",
                "공동구매 예약 물량을 침해하지 않는 별도 확정 재고만 시장 방문 주민에게 공개합니다.",
                "장날 판매 상인", CommunityDomesticMarketSupplyStageState.Waiting, "현장판매 확정 재고·표시"),
            Stage(10, CommunityDomesticMarketSupplyStageCodes.Closeout,
                "정산·산지와 시장 보상·다음 회차 기록",
                "생산자 대금, 운송비, 상인 작업·판매 보상, 수령률과 잔량을 공개 가능한 집계로 남깁니다.",
                "거래 당사자·상인회", CommunityDomesticMarketSupplyStageState.Waiting, "정산·수령·잔량·재참여 집계")
        ];
    }

    private static CommunityDomesticMarketSupplyStageSnapshot Stage(
        int number,
        string code,
        string label,
        string detail,
        string responsibleRoleLabel,
        CommunityDomesticMarketSupplyStageState state,
        string evidenceLabel)
        => new(number, code, label, detail, responsibleRoleLabel, state, evidenceLabel);

    private static string ResolveProductCategoryLabel(string categoryCode)
        => IsFisheries(categoryCode) ? "국내 수산물" : "국내 농산물";

    private static string ResolveHarvestOrLandingLabel(string categoryCode, bool confirmed)
        => IsFisheries(categoryCode)
            ? confirmed ? "어획·양식·출하 시점 확인" : "어획·양식·출하 시점 확인 전"
            : confirmed ? "수확·출하 시점 확인" : "수확·출하 시점 확인 전";

    private static string ResolveTransportConditionLabel(string categoryCode)
        => IsFisheries(categoryCode)
            ? "품목별 냉장·냉동 온도와 상하차 조건 확인 필요"
            : "품목별 적정 온도·통풍·적재 조건 확인 필요";

    private static bool IsFisheries(string categoryCode)
        => string.Equals(
            categoryCode,
            CommunityDomesticMarketSupplyProductCategoryCodes.FisheriesProducts,
            StringComparison.Ordinal);

    private static string NormalizeHsCode(string? value)
        => string.Concat((value ?? string.Empty).Where(char.IsDigit));

    private static string NormalizeUnit(string? value)
        => string.IsNullOrWhiteSpace(value) ? "개" : value.Trim();
}
