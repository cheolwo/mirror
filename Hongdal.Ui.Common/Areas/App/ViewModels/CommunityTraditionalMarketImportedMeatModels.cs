using Hongdal.Contracts.Common.CollectiveProcurement;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class CommunityTraditionalMarketImportedMeatStageCodes
{
    public const string OverseasApprovedProduction = "OverseasApprovedProduction";
    public const string ExportCertificationAndColdShipment = "ExportCertificationAndColdShipment";
    public const string KoreaQuarantineAndInspection = "KoreaQuarantineAndInspection";
    public const string KoreaCustomsRelease = "KoreaCustomsRelease";
    public const string RefrigeratedTransportToMarket = "RefrigeratedTransportToMarket";
    public const string MarketReceivingAndTraceability = "MarketReceivingAndTraceability";
    public const string LicensedSecondaryProcessing = "LicensedSecondaryProcessing";
    public const string ConsumerPackagingAndLabeling = "ConsumerPackagingAndLabeling";
    public const string ColdOrderHandoff = "ColdOrderHandoff";
    public const string NeighborhoodColdDelivery = "NeighborhoodColdDelivery";
}

public static class CommunityTraditionalMarketImportedMeatRoleCodes
{
    public const string TraditionalMarketHubOperator = "traditional-market-hub-operator";
    public const string LicensedMeatProcessor = "licensed-meat-processor";
    public const string MeatSeller = "meat-seller";
    public const string DomesticColdCarrier = "domestic-cold-carrier";
    public const string NeighborhoodColdDeliveryProvider = "neighborhood-cold-delivery-provider";
}

public static class CommunityTraditionalMarketImportedMeatRequirementCodes
{
    public const string OfficialImportRelease = "official-import-release";
    public const string MarketHubApproval = "market-hub-approval";
    public const string ProcessingBusinessScope = "processing-business-scope";
    public const string TraceabilityAndLabeling = "traceability-and-labeling";
    public const string ColdChainCapacity = "cold-chain-capacity";
    public const string LocalDeliveryScope = "local-delivery-scope";
}

public enum CommunityTraditionalMarketImportedMeatStageState
{
    Completed,
    Current,
    Waiting,
    OfficialResultRequired,
    SeparateContractRequired
}

public sealed record CommunityTraditionalMarketImportedMeatStageSnapshot(
    int Number,
    string Code,
    string Label,
    string Detail,
    string ResponsibleRoleLabel,
    CommunityTraditionalMarketImportedMeatStageState State,
    string EvidenceLabel);

public sealed record CommunityTraditionalMarketImportedMeatRequirementSnapshot(
    string Code,
    string Label,
    string Detail,
    IReadOnlyList<string> CandidateBusinessTypes,
    bool OfficialVerificationRequired);

public sealed record CommunityTraditionalMarketImportedMeatCostInputSnapshot(
    string CategoryCode,
    string Label,
    string Detail,
    bool PaysLocalBusiness);

public sealed class CommunityTraditionalMarketImportedMeatFulfillmentSnapshot
{
    public bool IsApplicable { get; init; }
    public string Title { get; init; } = string.Empty;
    public string SourceCountryCode { get; init; } = string.Empty;
    public string DestinationCountryCode { get; init; } = string.Empty;
    public string HsCode { get; init; } = string.Empty;
    public string TemperatureCode { get; init; } = string.Empty;
    public bool CustomsReleased { get; init; }
    public bool IncludesLiveAnimalHandling { get; init; }
    public bool OverseasSlaughterRemainsOutsideMarketScope { get; init; } = true;
    public bool ProcessingStartsAfterOfficialRelease { get; init; } = true;
    public bool PlatformSelectsProcessor { get; init; }
    public bool RequiresProcessorDirectAcceptance { get; init; } = true;
    public bool RequiresBusinessScopeVerification { get; init; } = true;
    public bool RequiresColdChain { get; init; } = true;
    public bool RequiresTraceability { get; init; } = true;
    public string PricingBasisLabel { get; init; } = string.Empty;
    public string MarketDirectoryEndpoint { get; init; } = string.Empty;
    public string ImportReadinessEndpoint { get; init; } = string.Empty;
    public string BusinessVerificationUrl { get; init; } = string.Empty;
    public IReadOnlyList<CommunityTraditionalMarketImportedMeatRequirementSnapshot> Requirements { get; init; } = [];
    public IReadOnlyList<CommunityTraditionalMarketImportedMeatCostInputSnapshot> CostInputs { get; init; } = [];
    public IReadOnlyList<CommunityTraditionalMarketImportedMeatStageSnapshot> Stages { get; init; } = [];

    public int CompletedStageCount
        => Stages.Count(stage => stage.State == CommunityTraditionalMarketImportedMeatStageState.Completed);

    public CommunityTraditionalMarketImportedMeatStageSnapshot? CurrentStage
        => Stages.FirstOrDefault(stage => stage.State == CommunityTraditionalMarketImportedMeatStageState.Current)
           ?? Stages.FirstOrDefault(stage => stage.State != CommunityTraditionalMarketImportedMeatStageState.Completed);
}

public static class CommunityTraditionalMarketImportedMeatScenarioFactory
{
    public static CommunityTraditionalMarketImportedMeatFulfillmentSnapshot Build(
        CommunityVoteResponse campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var groupPurchase = campaign.GroupPurchase;
        if (!IsApplicable(groupPurchase))
        {
            return new CommunityTraditionalMarketImportedMeatFulfillmentSnapshot();
        }

        var sourceCountryCode = groupPurchase!.ShipFromCountryCode.Trim().ToUpperInvariant();
        var customsReleased = string.Equals(
            groupPurchase.CustomsClearanceStatusCode,
            CommunityGroupPurchaseCustomsClearanceStatusCodes.Cleared,
            StringComparison.OrdinalIgnoreCase);

        return new CommunityTraditionalMarketImportedMeatFulfillmentSnapshot
        {
            IsApplicable = true,
            Title = "수입육 전통시장 2차 가공·동네 배송",
            SourceCountryCode = sourceCountryCode,
            DestinationCountryCode = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
            HsCode = NormalizeHsCode(groupPurchase.HsCode),
            TemperatureCode = string.IsNullOrWhiteSpace(groupPurchase.TemperatureCode)
                ? "냉장·냉동 조건 확인"
                : groupPurchase.TemperatureCode.Trim(),
            CustomsReleased = customsReleased,
            IncludesLiveAnimalHandling = false,
            OverseasSlaughterRemainsOutsideMarketScope = true,
            ProcessingStartsAfterOfficialRelease = true,
            PlatformSelectsProcessor = false,
            RequiresProcessorDirectAcceptance = true,
            RequiresBusinessScopeVerification = true,
            RequiresColdChain = true,
            RequiresTraceability = true,
            PricingBasisLabel = "판매가능 중량 기준 도착원가 + 지역 정육가공비 + 포장·표시비 + 동네 냉장배송비",
            MarketDirectoryEndpoint = "/api/v1/traditional-market-logistics-hubs",
            ImportReadinessEndpoint = "/api/v1/agricultural-fisheries/import-readiness/diagram",
            BusinessVerificationUrl = "https://www.foodsafetykorea.go.kr/portal/mfds_cn_busn_popup.html",
            Requirements = BuildRequirements(),
            CostInputs = BuildCostInputs(),
            Stages = BuildStages(customsReleased)
        };
    }

    public static bool IsApplicable(CommunityGroupPurchaseVoteResponse? groupPurchase)
    {
        if (groupPurchase?.IsGroupImportCandidate != true)
        {
            return false;
        }

        var destination = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            groupPurchase.DeliveryCountryCode);
        var source = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            groupPurchase.ShipFromCountryCode);
        return string.Equals(
                   destination,
                   CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                   StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(source)
               && !string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)
               && NormalizeHsCode(groupPurchase.HsCode).StartsWith("02", StringComparison.Ordinal);
    }

    private static IReadOnlyList<CommunityTraditionalMarketImportedMeatStageSnapshot> BuildStages(
        bool customsReleased)
    {
        var beforeRelease = customsReleased
            ? CommunityTraditionalMarketImportedMeatStageState.Completed
            : CommunityTraditionalMarketImportedMeatStageState.OfficialResultRequired;
        var quarantineState = customsReleased
            ? CommunityTraditionalMarketImportedMeatStageState.Completed
            : CommunityTraditionalMarketImportedMeatStageState.Current;
        var customsState = customsReleased
            ? CommunityTraditionalMarketImportedMeatStageState.Completed
            : CommunityTraditionalMarketImportedMeatStageState.Waiting;
        var domesticEntryState = customsReleased
            ? CommunityTraditionalMarketImportedMeatStageState.Current
            : CommunityTraditionalMarketImportedMeatStageState.Waiting;
        var domesticWorkState = customsReleased
            ? CommunityTraditionalMarketImportedMeatStageState.SeparateContractRequired
            : CommunityTraditionalMarketImportedMeatStageState.Waiting;

        return
        [
            Stage(1, CommunityTraditionalMarketImportedMeatStageCodes.OverseasApprovedProduction,
                "해외 승인 작업장 도축·1차 처리",
                "한국 수출이 가능한 해외 작업장에서 도축과 1차 절단·포장을 마칩니다. 전통시장 업무에는 살아 있는 가축 도축이 포함되지 않습니다.",
                "해외 작업장·수출자", beforeRelease, "해외작업장 번호·제품 lot"),
            Stage(2, CommunityTraditionalMarketImportedMeatStageCodes.ExportCertificationAndColdShipment,
                "수출검역·냉장 선적",
                "수출국 위생·검역증명, 봉인과 선적 온도 기록을 제품 lot에 연결합니다.",
                "수출자·수출국 기관·국제운송사", beforeRelease, "위생증명·봉인·온도 기록"),
            Stage(3, CommunityTraditionalMarketImportedMeatStageCodes.KoreaQuarantineAndInspection,
                "한국 검역·수입검사",
                "검역본부 검역과 식약처 수입신고·검사 결과를 공식 참조번호로 확인합니다.",
                "한국 수입자·검역본부·식약처", quarantineState, "검역·수입검사 결과"),
            Stage(4, CommunityTraditionalMarketImportedMeatStageCodes.KoreaCustomsRelease,
                "세관 통관·국내 반출",
                "검역·검사와 세관 수리가 모두 확인된 물량만 국내 가공 거점으로 넘깁니다.",
                "한국 수입자·통관 담당", customsState, "수입신고 수리·반출 근거"),
            Stage(5, CommunityTraditionalMarketImportedMeatStageCodes.RefrigeratedTransportToMarket,
                "전통시장으로 냉장·냉동 운송",
                "제품 보관조건을 유지할 수 있는 운송 주체가 lot와 온도 기록을 함께 인계합니다.",
                "국내 냉장운송 사업자", domesticEntryState, "차량·상하차·온도 인계"),
            Stage(6, CommunityTraditionalMarketImportedMeatStageCodes.MarketReceivingAndTraceability,
                "시장 거점 입고·이력 확인",
                "허가된 작업장이 수량, 포장 상태, 온도, 소비기한과 축산물 이력번호를 확인합니다.",
                "전통시장 거점·정육 사업자", domesticWorkState, "입고검수·이력번호"),
            Stage(7, CommunityTraditionalMarketImportedMeatStageCodes.LicensedSecondaryProcessing,
                "발골·정형·부위별 소분",
                "수입 상태와 주문 규격에 따라 필요한 경우 발골하고 정형·절단·분할합니다. 실제 작업 범위에 맞는 영업 인허가를 먼저 확인합니다.",
                "허가·신고된 식육 작업 사업자", domesticWorkState, "작업지시·수율·위생점검"),
            Stage(8, CommunityTraditionalMarketImportedMeatStageCodes.ConsumerPackagingAndLabeling,
                "참여자 단위 포장·표시",
                "판매가능 중량으로 포장하고 원산지, 제품·보관 정보, 이력번호와 작업 lot를 연결합니다.",
                "식육포장처리·판매 사업자", domesticWorkState, "포장 lot·표시·이력"),
            Stage(9, CommunityTraditionalMarketImportedMeatStageCodes.ColdOrderHandoff,
                "주문별 피킹·냉장 인계",
                "주소 동의가 끝난 주문만 냉장 상태로 피킹하고 배송 담당자에게 인계합니다.",
                "시장 출고 담당·배송 사업자", domesticWorkState, "주문·인계·온도 기록"),
            Stage(10, CommunityTraditionalMarketImportedMeatStageCodes.NeighborhoodColdDelivery,
                "동네 냉장배송·수령",
                "지역 배송 사업자가 공개 생활권 안에서 배송하고 개별 주소와 수령 증빙은 권한 있는 당사자만 확인합니다.",
                "지역 냉장배송 사업자", domesticWorkState, "배송권·온도·수령 증빙")
        ];
    }

    private static IReadOnlyList<CommunityTraditionalMarketImportedMeatRequirementSnapshot>
        BuildRequirements()
        =>
        [
            new(CommunityTraditionalMarketImportedMeatRequirementCodes.OfficialImportRelease,
                "공식 국내 반출 근거",
                "검역·수입검사·세관 수리 결과를 플랫폼 추정이 아닌 공식 참조로 확인합니다.",
                ["수입식품등 수입·판매업", "수입 통관 책임 당사자"], true),
            new(CommunityTraditionalMarketImportedMeatRequirementCodes.MarketHubApproval,
                "시장 거점과 작업장 확인",
                "전통시장 공공정보와 실제 작업장 인허가·냉장시설·운영 동의를 별도로 확인합니다.",
                ["전통시장 물류 거점 운영자", "축산물보관업"], true),
            new(CommunityTraditionalMarketImportedMeatRequirementCodes.ProcessingBusinessScope,
                "실제 작업에 맞는 식육 영업 범위",
                "포장육 생산, 재절단·분할 판매, 즉석 가공 중 실제 맡을 작업과 판매대상에 맞는 영업 범위를 관할 관청과 확인합니다.",
                ["식육포장처리업", "식육판매업", "식육즉석판매가공업"], true),
            new(CommunityTraditionalMarketImportedMeatRequirementCodes.TraceabilityAndLabeling,
                "원산지·표시·축산물이력",
                "원 포장 lot에서 참여자 포장까지 이력번호, 원산지와 표시사항이 끊기지 않게 연결합니다.",
                ["식육포장처리업", "식육판매업"], true),
            new(CommunityTraditionalMarketImportedMeatRequirementCodes.ColdChainCapacity,
                "냉장·냉동 처리 용량",
                "입고, 작업, 임시 보관과 출고 전 구간에서 제품별 보관조건과 일일 처리량을 확인합니다.",
                ["축산물보관업", "축산물운반업", "식육 영업자"], true),
            new(CommunityTraditionalMarketImportedMeatRequirementCodes.LocalDeliveryScope,
                "동네 배송 영업·계약 범위",
                "판매자의 직접 배송인지 별도 운반 사업자인지 구분하고 차량, 보관온도, 서비스 반경과 책임을 확인합니다.",
                ["축산물운반업", "식육판매업 등의 적법한 직접 배송"], true)
        ];

    private static IReadOnlyList<CommunityTraditionalMarketImportedMeatCostInputSnapshot>
        BuildCostInputs()
        =>
        [
            new(CollectiveProcurementCostCategoryCodes.Goods,
                "통관 후 수입육 원가", "관세·검사·통관과 국내 반출까지 확인된 lot 원가", false),
            new(CollectiveProcurementCostCategoryCodes.DestinationHandling,
                "시장까지 냉장운송", "반출지에서 전통시장 작업장까지 온도 유지 운송비", true),
            new(CollectiveProcurementCostCategoryCodes.DomesticValueAddedProcessing,
                "지역 정육가공비", "발골·정형·절단·소분 작업과 작업장 위생관리의 정당한 대가", true),
            new(CollectiveProcurementCostCategoryCodes.PackagingLabelingAndTraceability,
                "포장·표시·이력비", "소비자 단위 포장재, 라벨과 이력번호 관리 비용", true),
            new(CollectiveProcurementCostCategoryCodes.LocalColdChainDelivery,
                "동네 냉장배송비", "지역 배송 사업자의 피킹 인수, 냉장배송과 수령 확인 비용", true),
            new(CollectiveProcurementCostCategoryCodes.ProcessingYieldLoss,
                "수율·폐기 위험준비금", "정형 손실, 불량 포장과 온도 이탈 등 판매불가 위험을 판매가능 중량 기준으로 반영", false)
        ];

    private static CommunityTraditionalMarketImportedMeatStageSnapshot Stage(
        int number,
        string code,
        string label,
        string detail,
        string responsibleRoleLabel,
        CommunityTraditionalMarketImportedMeatStageState state,
        string evidenceLabel)
        => new(number, code, label, detail, responsibleRoleLabel, state, evidenceLabel);

    private static string NormalizeHsCode(string? value)
        => string.Concat((value ?? string.Empty).Where(char.IsDigit));
}
