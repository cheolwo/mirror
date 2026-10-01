using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.CollectiveProcurement;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class CommunityCollectiveImportDeliveryScenarioCodes
{
    public const string UnitedStatesBuyerParticipantAddress =
        "UnitedStatesBuyerParticipantAddress";
}

public static class CommunityCollectiveImportDeliveryStageCodes
{
    public const string DemandAggregation = "DemandAggregation";
    public const string ProvisionalLedger = "ProvisionalLedger";
    public const string RoleFormation = "RoleFormation";
    public const string OriginFactoryPreparation = "OriginFactoryPreparation";
}

public static class CommunityCollectiveImportOriginPreparationTaskCodes
{
    public const string UnitPackagingAndSku = "UnitPackagingAndSku";
    public const string ProductAndOriginLabeling = "ProductAndOriginLabeling";
    public const string CommercialInvoiceSourceData = "CommercialInvoiceSourceData";
    public const string PackingListAndCartonManifest = "PackingListAndCartonManifest";
    public const string ParcelLabelData = "ParcelLabelData";
    public const string QualityAndSupplyChainEvidence = "QualityAndSupplyChainEvidence";
}

public enum CommunityCollectiveImportOriginPreparationTiming
{
    FinalizeBeforeExport,
    PrepareBeforeExportAndFinalizeAfterRouting
}

public sealed record CommunityCollectiveImportOriginPreparationTaskSnapshot(
    string Code,
    string Label,
    string Detail,
    string ResponsibleRoleLabel,
    CommunityCollectiveImportOriginPreparationTiming Timing,
    bool RequiresImporterReview,
    bool CanReduceUnitedStatesHandling);

public enum CommunityCollectiveImportOriginPreparationCostInputKind
{
    CandidateCost,
    ComparisonBaseline,
    RiskReserve
}

public sealed record CommunityCollectiveImportOriginPreparationCostInputSnapshot(
    string CategoryCode,
    string Label,
    string Detail,
    CommunityCollectiveImportOriginPreparationCostInputKind Kind);

public sealed class CommunityCollectiveImportOriginPreparationSnapshot
{
    public bool IsApplicable { get; init; }
    public bool IsChinaOrigin { get; init; }
    public string Title { get; init; } = string.Empty;
    public string BaselineScenarioLabel { get; init; } = string.Empty;
    public string CandidateScenarioLabel { get; init; } = string.Empty;
    public string ComparisonFormula { get; init; } = string.Empty;
    public bool RequiresQuoteComparison { get; init; } = true;
    public bool SavingsConfirmed { get; init; }
    public bool UsesDeMinimisAssumption { get; init; }
    public bool ArtificialShipmentSplittingAllowed { get; init; }
    public bool RequiresImporterAndBrokerReview { get; init; } = true;
    public IReadOnlyList<CommunityCollectiveImportOriginPreparationTaskSnapshot> Tasks { get; init; } = [];
    public IReadOnlyList<CommunityCollectiveImportOriginPreparationCostInputSnapshot> CostInputs { get; init; } = [];
}

public enum CommunityCollectiveImportDeliveryStageState
{
    Completed,
    Current,
    Waiting,
    SeparateContractRequired
}

public sealed record CommunityCollectiveImportDeliveryStageSnapshot(
    int Number,
    string Code,
    string Label,
    string Detail,
    string ResponsibleRoleCode,
    string ResponsibleRoleLabel,
    CommunityCollectiveImportDeliveryStageState State,
    string? CandidateDirectoryEndpoint = null);

public sealed class CommunityCollectiveImportDeliverySnapshot
{
    public bool IsApplicable { get; init; }
    public string ScenarioCode { get; init; } = string.Empty;
    public string OriginCountryCode { get; init; } = string.Empty;
    public string DestinationCountryCode { get; init; } = string.Empty;
    public string RecruitmentScopeKey { get; init; } = string.Empty;
    public string RecruitmentScopeLabel { get; init; } = string.Empty;
    public bool RecruitmentScopeVerified { get; init; }
    public bool IndividualAddressesVisibleToCommunity { get; init; }
    public bool RequiresParticipantAddressConsent { get; init; } = true;
    public bool IndividualAddressCollectionDeferred { get; init; } = true;
    public bool ProviderSelectionIsAutomated { get; init; }
    public bool RequiresSeparateProviderContracts { get; init; } = true;
    public string CandidateDirectoryEndpoint { get; init; } = string.Empty;
    public CommunityCollectiveImportOriginPreparationSnapshot OriginPreparation { get; init; } = new();
    public IReadOnlyList<CommunityCollectiveImportDeliveryStageSnapshot> Stages { get; init; } = [];

    public int CompletedStageCount
        => Stages.Count(stage => stage.State == CommunityCollectiveImportDeliveryStageState.Completed);

    public CommunityCollectiveImportDeliveryStageSnapshot? CurrentStage
        => Stages.FirstOrDefault(stage => stage.State == CommunityCollectiveImportDeliveryStageState.Current)
           ?? Stages.FirstOrDefault(stage => stage.State != CommunityCollectiveImportDeliveryStageState.Completed);
}

public static class CommunityUnitedStatesCollectiveImportScenarioFactory
{
    private static readonly string[] RequiredPartyRoleCodes =
    [
        CommunityPostPartyRoleCodes.Seller,
        CommunityPostPartyRoleCodes.Importer,
        CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator,
        CommunityPostPartyRoleCodes.InBondCarrier,
        CommunityPostPartyRoleCodes.DomesticFulfillmentOperator,
        CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider
    ];

    public static CommunityCollectiveImportDeliverySnapshot Build(
        CommunityVoteResponse campaign,
        CommunityActionJourneyResponse? journey,
        IReadOnlyList<CommunityActionRoleSlotSnapshot> roleSlots)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(roleSlots);

        var groupPurchase = campaign.GroupPurchase;
        if (!IsApplicable(groupPurchase))
        {
            return new CommunityCollectiveImportDeliverySnapshot();
        }

        var hasDemand = campaign.TotalVoteCount > 0
                        || (groupPurchase?.TotalRequestedQuantity ?? 0) > 0;
        var minimumReached = groupPurchase?.IsMinimumReached == true
                             || campaign.TotalVoteCount >= Math.Max(
                                 1,
                                 groupPurchase?.MinimumParticipantCount ?? 1)
                             && (groupPurchase?.TotalRequestedQuantity ?? 0) >= Math.Max(
                                 1,
                                 groupPurchase?.MinimumTotalQuantity ?? 1);
        var hasProvisionalLedger = !string.IsNullOrWhiteSpace(journey?.ProvisionalLedgerId);
        var acceptedLogisticsRoles = roleSlots
            .Where(slot => slot.Accepted)
            .Select(slot => slot.RoleCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredPartyReady = RequiredPartyRoleCodes.All(acceptedLogisticsRoles.Contains);

        var originCountryCode = groupPurchase?.ShipFromCountryCode?.Trim().ToUpperInvariant()
                                ?? string.Empty;
        var originPreparation = BuildOriginPreparation(originCountryCode);

        var stages = BuildStages(
            hasDemand,
            minimumReached,
            hasProvisionalLedger,
            requiredPartyReady,
            originPreparation);
        var scopeKey = groupPurchase?.ServiceAreaKey?.Trim() ?? string.Empty;
        var scopeLabel = groupPurchase?.ServiceAreaLabel?.Trim() ?? string.Empty;

        return new CommunityCollectiveImportDeliverySnapshot
        {
            IsApplicable = true,
            ScenarioCode = CommunityCollectiveImportDeliveryScenarioCodes
                .UnitedStatesBuyerParticipantAddress,
            OriginCountryCode = originCountryCode,
            DestinationCountryCode = CommunityGroupPurchaseTradeRoutePolicy
                .UnitedStatesCountryCode,
            RecruitmentScopeKey = scopeKey,
            RecruitmentScopeLabel = string.IsNullOrWhiteSpace(scopeLabel)
                ? "미국 배달권 확인 중"
                : scopeLabel,
            RecruitmentScopeVerified = IsUnitedStatesScopeKey(scopeKey),
            IndividualAddressesVisibleToCommunity = false,
            RequiresParticipantAddressConsent = true,
            IndividualAddressCollectionDeferred = true,
            ProviderSelectionIsAutomated = false,
            RequiresSeparateProviderContracts = true,
            CandidateDirectoryEndpoint =
                "/api/v1/operations/third-party-logistics/providers/bonded-to-door",
            OriginPreparation = originPreparation,
            Stages = stages
        };
    }

    private static bool IsApplicable(CommunityGroupPurchaseVoteResponse? groupPurchase)
        => groupPurchase?.IsGroupImportCandidate == true
           && string.Equals(
               CommunityGroupPurchaseTradeRoutePolicy.NormalizeOperatingMarketCountryCode(
                   groupPurchase.OperatingMarketCountryCode),
               CommunityGroupPurchaseTradeRoutePolicy.UnitedStatesCountryCode,
               StringComparison.OrdinalIgnoreCase)
           && string.Equals(
               groupPurchase.DeliveryCountryCode,
               CommunityGroupPurchaseTradeRoutePolicy.UnitedStatesCountryCode,
               StringComparison.OrdinalIgnoreCase)
           && !string.Equals(
               groupPurchase.ShipFromCountryCode,
               groupPurchase.DeliveryCountryCode,
               StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<CommunityCollectiveImportDeliveryStageSnapshot> BuildStages(
        bool hasDemand,
        bool minimumReached,
        bool hasProvisionalLedger,
        bool requiredPartyReady,
        CommunityCollectiveImportOriginPreparationSnapshot originPreparation)
    {
        var demandState = minimumReached
            ? CommunityCollectiveImportDeliveryStageState.Completed
            : CommunityCollectiveImportDeliveryStageState.Current;
        var provisionalState = hasProvisionalLedger
            ? CommunityCollectiveImportDeliveryStageState.Completed
            : minimumReached
                ? CommunityCollectiveImportDeliveryStageState.Current
                : CommunityCollectiveImportDeliveryStageState.Waiting;
        var partyState = requiredPartyReady
            ? CommunityCollectiveImportDeliveryStageState.Completed
            : hasProvisionalLedger
                ? CommunityCollectiveImportDeliveryStageState.Current
                : CommunityCollectiveImportDeliveryStageState.Waiting;

        return
        [
            Stage(
                1,
                CommunityCollectiveImportDeliveryStageCodes.DemandAggregation,
                "미국 구매자 수요 모으기",
                hasDemand
                    ? "공개 배달권 단위로 인원과 수량을 집계합니다."
                    : "게시글에서 참여 의향과 필요한 수량을 모읍니다.",
                CommunityPostPartyRoleCodes.Buyer,
                "구매 참여자",
                demandState),
            Stage(
                2,
                CommunityCollectiveImportDeliveryStageCodes.ProvisionalLedger,
                "비구속 가원장",
                "가격·수량·수령 조건을 기록하되 주문이나 계약을 확정하지 않습니다.",
                CommunityGroupPurchaseProposerRoleCodes.GroupPurchaseRepresentative,
                "공동구매 대표",
                provisionalState),
            Stage(
                3,
                CommunityCollectiveImportDeliveryStageCodes.RoleFormation,
                "수입·물류 역할 구성",
                "수입자, 통관, 보세시설, 운송, 풀필먼트와 배송 역할을 각각 수락합니다.",
                CommunityPostPartyRoleCodes.Importer,
                "수입 책임 당사자",
                partyState),
            Stage(
                4,
                CommunityCollectiveImportDeliveryStageCodes.OriginFactoryPreparation,
                originPreparation.Title,
                "개별포장, 제품·원산지 라벨, 상업송장 원천자료와 카톤 식별을 준비하고 QC 증빙을 남깁니다. 최종 배송라벨은 주소와 운송사가 확정된 뒤 발급합니다.",
                CommunityPostPartyRoleCodes.Seller,
                "제조사·판매자·수출자",
                CommunityCollectiveImportDeliveryStageState.SeparateContractRequired),
            LogisticsStage(
                5,
                BondedToDoorLogisticsStageCodes.CustomsControlledStorage,
                "보세창고·FTZ 인계",
                "정확한 시설 승인과 FIRMS 정보, 공간 및 계약을 별도로 확인합니다.",
                CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator,
                "보세창고·FTZ 운영자"),
            LogisticsStage(
                6,
                BondedToDoorLogisticsStageCodes.InBondTransportation,
                "통관 전 in-bond 이동",
                "ACE 신고, carrier bond, 이동 경로와 운송계약을 확인합니다.",
                CommunityPostPartyRoleCodes.InBondCarrier,
                "보세운송사"),
            LogisticsStage(
                7,
                BondedToDoorLogisticsStageCodes.CustomsWithdrawalAndRelease,
                "수입 신고·반출",
                "수입자 또는 별도 수임한 통관 전문가가 신고, 관세, 허가와 반출 근거를 확인합니다.",
                CommunityPostPartyRoleCodes.Importer,
                "수입 책임 당사자·통관 담당"),
            LogisticsStage(
                8,
                BondedToDoorLogisticsStageCodes.FulfillmentWarehouseInbound,
                "미국 내 풀필먼트 입고",
                "반출된 화물을 일괄 입고하고 수량·상태를 검수합니다.",
                CommunityPostPartyRoleCodes.DomesticFulfillmentOperator,
                "풀필먼트 운영자"),
            LogisticsStage(
                9,
                BondedToDoorLogisticsStageCodes.BreakPackKittingAndRelabeling,
                "미국 내 예외 소분·재라벨",
                "공장 전처리에서 끝내지 못했거나 검수 중 발견된 예외만 참여자별 주문 단위로 보완합니다.",
                CommunityPostPartyRoleCodes.DomesticFulfillmentOperator,
                "풀필먼트 운영자"),
            LogisticsStage(
                10,
                BondedToDoorLogisticsStageCodes.ParticipantOrderPickPackAndParcelTender,
                "참여자별 피킹·parcel 인계",
                "동의된 개별 주문만 피킹·포장하고 배송사에 인계합니다.",
                CommunityPostPartyRoleCodes.DomesticFulfillmentOperator,
                "풀필먼트 운영자"),
            LogisticsStage(
                11,
                BondedToDoorLogisticsStageCodes.ParticipantAddressFinalMileDelivery,
                "참여자 주소 배송",
                "배송 동의와 서비스 권역을 확인한 뒤 각 참여자에게 전달합니다.",
                CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider,
                "참여자 주소 배송 사업자")
        ];
    }

    private static CommunityCollectiveImportOriginPreparationSnapshot BuildOriginPreparation(
        string originCountryCode)
    {
        var isChinaOrigin = string.Equals(
            originCountryCode,
            "CN",
            StringComparison.OrdinalIgnoreCase);

        return new CommunityCollectiveImportOriginPreparationSnapshot
        {
            IsApplicable = true,
            IsChinaOrigin = isChinaOrigin,
            Title = isChinaOrigin
                ? "중국 공장 출발 전 전처리"
                : "출발지 공장 전처리",
            BaselineScenarioLabel = "미국에서 소분·라벨·재포장하는 견적",
            CandidateScenarioLabel = "공장에서 출발 전에 전처리하는 견적",
            ComparisonFormula =
                "미국 후처리 회피비용 - (공장 전처리비 + 추가 국제운임 + 설정·검수비 + 재작업 위험준비금)",
            RequiresQuoteComparison = true,
            SavingsConfirmed = false,
            UsesDeMinimisAssumption = false,
            ArtificialShipmentSplittingAllowed = false,
            RequiresImporterAndBrokerReview = true,
            Tasks = BuildOriginPreparationTasks(),
            CostInputs =
            [
                new(
                    CollectiveProcurementCostCategoryCodes.OriginPackagingAndLabeling,
                    "공장 전처리",
                    "개별포장·라벨·카톤 작업의 고정비와 단위당 견적",
                    CommunityCollectiveImportOriginPreparationCostInputKind.CandidateCost),
                new(
                    CollectiveProcurementCostCategoryCodes.InternationalFreight,
                    "추가 국제운임",
                    "개별포장으로 늘어난 실중량·부피중량과 적재 구간 비용",
                    CommunityCollectiveImportOriginPreparationCostInputKind.CandidateCost),
                new(
                    CollectiveProcurementCostCategoryCodes.OriginPreparation,
                    "설정·검수비",
                    "포장 사양 설정, 샘플 작업, 초도 검수와 출발 전 QC 비용",
                    CommunityCollectiveImportOriginPreparationCostInputKind.CandidateCost),
                new(
                    CollectiveProcurementCostCategoryCodes.DestinationHandling,
                    "미국 후처리 회피비용",
                    "미국 3PL의 소분·키팅·라벨·재포장 견적 중 줄어드는 금액",
                    CommunityCollectiveImportOriginPreparationCostInputKind.ComparisonBaseline),
                new(
                    CollectiveProcurementCostCategoryCodes.ReworkRisk,
                    "재작업 위험준비금",
                    "주소·운송사·규제 라벨 변경과 불량 포장의 보완 비용",
                    CommunityCollectiveImportOriginPreparationCostInputKind.RiskReserve)
            ]
        };
    }

    private static IReadOnlyList<CommunityCollectiveImportOriginPreparationTaskSnapshot>
        BuildOriginPreparationTasks()
        =>
        [
            new(
                CommunityCollectiveImportOriginPreparationTaskCodes.UnitPackagingAndSku,
                "SKU·개별포장 규격",
                "참여자 주문 단위, 구성품, 완충재와 포장 치수·중량을 확정합니다.",
                "제조사·판매자",
                CommunityCollectiveImportOriginPreparationTiming.FinalizeBeforeExport,
                false,
                true),
            new(
                CommunityCollectiveImportOriginPreparationTaskCodes.ProductAndOriginLabeling,
                "제품·원산지 라벨",
                "영문 원산지 표시와 제품별 필수 라벨을 수입자 검토 후 부착합니다.",
                "제조사·수입자",
                CommunityCollectiveImportOriginPreparationTiming.FinalizeBeforeExport,
                true,
                true),
            new(
                CommunityCollectiveImportOriginPreparationTaskCodes.CommercialInvoiceSourceData,
                "상업송장 초안·원천자료",
                "정확한 품명, 거래가격·통화, 수량, 거래조건, 포장비와 당사자 정보를 준비합니다. 판매자·수출자가 송장을 확정하고 신고가격과 품목분류는 수입자·통관 담당자가 검토합니다.",
                "판매자·수출자",
                CommunityCollectiveImportOriginPreparationTiming.FinalizeBeforeExport,
                true,
                true),
            new(
                CommunityCollectiveImportOriginPreparationTaskCodes.PackingListAndCartonManifest,
                "패킹리스트·카톤 manifest",
                "포장 ID별 SKU, 수량, 중량, 치수와 카톤·팔레트 식별을 연결합니다.",
                "제조사·수출자",
                CommunityCollectiveImportOriginPreparationTiming.FinalizeBeforeExport,
                false,
                true),
            new(
                CommunityCollectiveImportOriginPreparationTaskCodes.ParcelLabelData,
                "참여자 배정·배송라벨 데이터",
                "포장 ID와 참여자 주문은 미리 연결하되 실제 배송라벨 발급은 주소 동의, 미국 배송사와 서비스가 확정된 뒤 처리합니다.",
                "풀필먼트·배송 사업자",
                CommunityCollectiveImportOriginPreparationTiming.PrepareBeforeExportAndFinalizeAfterRouting,
                true,
                true),
            new(
                CommunityCollectiveImportOriginPreparationTaskCodes.QualityAndSupplyChainEvidence,
                "QC·공급망 추적 증빙",
                "수량·중량·포장 사진, lot와 원재료·생산 경로 자료를 원장 리비전에 남깁니다.",
                "제조사·수입자",
                CommunityCollectiveImportOriginPreparationTiming.FinalizeBeforeExport,
                true,
                true)
        ];

    private static CommunityCollectiveImportDeliveryStageSnapshot LogisticsStage(
        int number,
        string code,
        string label,
        string detail,
        string responsibleRoleCode,
        string responsibleRoleLabel)
        => Stage(
            number,
            code,
            label,
            detail,
            responsibleRoleCode,
            responsibleRoleLabel,
            CommunityCollectiveImportDeliveryStageState.SeparateContractRequired,
            $"/api/v1/operations/third-party-logistics/providers/bonded-to-door?stageCode={Uri.EscapeDataString(code)}");

    private static CommunityCollectiveImportDeliveryStageSnapshot Stage(
        int number,
        string code,
        string label,
        string detail,
        string responsibleRoleCode,
        string responsibleRoleLabel,
        CommunityCollectiveImportDeliveryStageState state,
        string? candidateDirectoryEndpoint = null)
        => new(
            number,
            code,
            label,
            detail,
            responsibleRoleCode,
            responsibleRoleLabel,
            state,
            candidateDirectoryEndpoint);

    private static bool IsUnitedStatesScopeKey(string scopeKey)
        => scopeKey.StartsWith("us-state:", StringComparison.OrdinalIgnoreCase)
           || scopeKey.StartsWith("us-county:", StringComparison.OrdinalIgnoreCase)
           || scopeKey.StartsWith("us-place:", StringComparison.OrdinalIgnoreCase)
           || scopeKey.StartsWith("us-zcta:", StringComparison.OrdinalIgnoreCase);
}
