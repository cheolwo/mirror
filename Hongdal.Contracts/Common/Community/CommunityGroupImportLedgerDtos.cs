namespace Hongdal.Contracts.Common.Community;

public static class CommunityGroupImportLogisticsRouteCodes
{
    public const string ThreePlWarehouse = "three-pl-warehouse";
    public const string DirectDestination = "direct-destination";
    public const string DedicatedWarehouse = "dedicated-warehouse";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ThreePlWarehouse,
        DirectDestination,
        DedicatedWarehouse
    };
}

public static class CommunityGroupImportLedgerStageCodes
{
    public const string ImportDecision = "import-decision";
    public const string OverseasContract = "overseas-contract";
    public const string OverseasOrder = "overseas-order";
    public const string Shipment = "shipment";
    public const string Customs = "customs";
    public const string Release = "release";
    public const string DomesticLogistics = "domestic-logistics";
    public const string WarehouseInbound = "warehouse-inbound";
    public const string Distribution = "distribution";
    public const string Settlement = "settlement";
    public const string Complete = "complete";

    public static IReadOnlyList<string> Ordered { get; } =
    [
        ImportDecision,
        OverseasContract,
        OverseasOrder,
        Shipment,
        Customs,
        Release,
        DomesticLogistics,
        WarehouseInbound,
        Distribution,
        Settlement,
        Complete
    ];
}

public sealed class CommunityGroupImportLedgerConversionRequest
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string LogisticsRouteCode { get; set; } = CommunityGroupImportLogisticsRouteCodes.DirectDestination;
    public string ProductSummary { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public string InternationalTransportMode { get; set; } = "LCL";
    public string FinalDestinationLabel { get; set; } = string.Empty;
    public string WarehouseReferenceKey { get; set; } = string.Empty;
    public string WarehouseDisplayName { get; set; } = string.Empty;
    public bool WarehouseOperatorConsentConfirmed { get; set; }
    public bool WarehouseSiteVerified { get; set; }
    public bool WarehouseBulkReceivingSupported { get; set; }
    public bool WarehouseStorageSupported { get; set; }
    public bool WarehouseOutboundSupported { get; set; }
    public bool RequiresWarehouseOutbound { get; set; }
    public bool RequiresFinalDestinationDelivery { get; set; }
    public long? ExpectedRevision { get; set; }
}

public sealed class CommunityGroupImportLedgerPlanResponse
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string SourceGroupPurchaseLedgerId { get; set; } = string.Empty;
    public string GroupImportLedgerId { get; set; } = string.Empty;
    public string LogisticsRouteCode { get; set; } = string.Empty;
    public string LogisticsRouteLabel { get; set; } = string.Empty;
    public bool Ready { get; set; }
    public bool Created { get; set; }
    public long Revision { get; set; }
    public string CurrentStageCode { get; set; } = CommunityGroupImportLedgerStageCodes.ImportDecision;
    public IReadOnlyList<CommunityGroupImportLedgerPlanNode> Nodes { get; set; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public sealed class CommunityGroupImportLedgerPlanNode
{
    public string NodeId { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string LedgerTemplateKey { get; set; } = string.Empty;
    public string RelationRole { get; set; } = string.Empty;
    public string RelationType { get; set; } = CommunityLedgerRelationTypes.Handoff;
    public string Title { get; set; } = string.Empty;
    public string ResponsiblePartyLabel { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsSourceReference { get; set; }
}

public static class CommunityGroupImportLedgerPlanBuilder
{
    public static CommunityGroupImportLedgerPlanResponse Preview(
        CommunityGroupImportLedgerConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var routeCode = NormalizeRoute(request.LogisticsRouteCode);
        var nodes = new List<CommunityGroupImportLedgerPlanNode>
        {
            Node(
                "source-group-purchase",
                CommunityLedgerTemplateKeys.GroupPurchase,
                공동수입원장관계역할.원천공동구매,
                CommunityLedgerRelationTypes.Reference,
                "원천 공동구매 원장",
                "공동구매 대표",
                0,
                isSourceReference: true),
            Node(
                "international-transport",
                CommunityLedgerTemplateKeys.CargoTransport,
                공동수입원장관계역할.국제운송,
                CommunityLedgerRelationTypes.Handoff,
                "해외 선적·국제 운송 원장",
                "해외 공급자·포워더",
                1)
        };

        switch (routeCode)
        {
            case CommunityGroupImportLogisticsRouteCodes.ThreePlWarehouse:
                AddWarehouseRoute(
                    nodes,
                    "three-pl",
                    "3PL",
                    request.RequiresWarehouseOutbound,
                    request.RequiresFinalDestinationDelivery);
                break;
            case CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse:
                AddWarehouseRoute(
                    nodes,
                    "dedicated-warehouse",
                    "전용 창고",
                    request.RequiresWarehouseOutbound,
                    request.RequiresFinalDestinationDelivery);
                break;
            default:
                nodes.Add(Node(
                    "domestic-transport-direct",
                    CommunityLedgerTemplateKeys.CargoTransport,
                    공동수입원장관계역할.국내운송,
                    CommunityLedgerRelationTypes.Handoff,
                    "보세구역·항만 → 최종 도착지 직배송 원장",
                    "국내 운송 담당자",
                    2));
                break;
        }

        var warnings = Validate(request, routeCode);
        return new CommunityGroupImportLedgerPlanResponse
        {
            GroupPurchaseCampaignId = request.GroupPurchaseCampaignId,
            LogisticsRouteCode = routeCode,
            LogisticsRouteLabel = LabelOf(
                routeCode,
                request.RequiresWarehouseOutbound,
                request.RequiresFinalDestinationDelivery),
            Ready = warnings.Count == 0,
            Nodes = nodes,
            Warnings = warnings
        };
    }

    private static void AddWarehouseRoute(
        ICollection<CommunityGroupImportLedgerPlanNode> nodes,
        string prefix,
        string warehouseLabel,
        bool requiresWarehouseOutbound,
        bool requiresFinalDelivery)
    {
        nodes.Add(Node(
            $"domestic-transport-to-{prefix}",
            CommunityLedgerTemplateKeys.CargoTransport,
            공동수입원장관계역할.국내운송,
            CommunityLedgerRelationTypes.Handoff,
            $"보세구역·항만 → {warehouseLabel} 운송 원장",
            "국내 운송 담당자",
            2));
        nodes.Add(Node(
            $"{prefix}-inbound",
            CommunityLedgerTemplateKeys.WarehouseInbound,
            공동수입원장관계역할.물류거점입고,
            CommunityLedgerRelationTypes.Handoff,
            $"{warehouseLabel} 입고·검수 원장",
            $"{warehouseLabel} 운영자",
            3));
        if (requiresWarehouseOutbound)
        {
            nodes.Add(Node(
                $"{prefix}-outbound",
                CommunityLedgerTemplateKeys.WarehouseOutbound,
                공동수입원장관계역할.물류거점출고,
                CommunityLedgerRelationTypes.Flow,
                $"{warehouseLabel} 출고·분배 원장",
                $"{warehouseLabel} 운영자",
                4));
        }
        if (requiresFinalDelivery)
        {
            nodes.Add(Node(
                $"domestic-transport-from-{prefix}",
                CommunityLedgerTemplateKeys.CargoTransport,
                공동수입원장관계역할.국내운송,
                CommunityLedgerRelationTypes.Handoff,
                $"{warehouseLabel} → 최종 도착지 운송 원장",
                "국내 운송 담당자",
                5));
        }
    }

    private static List<string> Validate(
        CommunityGroupImportLedgerConversionRequest request,
        string routeCode)
    {
        var warnings = new List<string>();
        if (request.GroupPurchaseCampaignId == Guid.Empty)
        {
            warnings.Add("공동구매 캠페인 식별자가 필요합니다.");
        }
        if (!CommunityGroupImportLogisticsRouteCodes.All.Contains(request.LogisticsRouteCode?.Trim() ?? string.Empty))
        {
            warnings.Add("지원되는 공동수입 물류 경로를 선택해 주세요.");
        }
        if (string.IsNullOrWhiteSpace(request.ProductSummary))
        {
            warnings.Add("수입 품목 요약이 필요합니다.");
        }
        if (request.PlannedQuantity <= 0 || string.IsNullOrWhiteSpace(request.QuantityUnit))
        {
            warnings.Add("수입 예정 수량과 단위가 필요합니다.");
        }
        if (string.IsNullOrWhiteSpace(request.InternationalTransportMode))
        {
            warnings.Add("FCL, LCL 등 국제 운송 방식이 필요합니다.");
        }
        if (string.IsNullOrWhiteSpace(request.FinalDestinationLabel))
        {
            warnings.Add("최종 도착지 또는 최종 분배 지역이 필요합니다.");
        }

        if (routeCode is CommunityGroupImportLogisticsRouteCodes.ThreePlWarehouse
            or CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse)
        {
            if (string.IsNullOrWhiteSpace(request.WarehouseReferenceKey)
                && string.IsNullOrWhiteSpace(request.WarehouseDisplayName))
            {
                warnings.Add("선택한 창고 또는 3PL의 참조 키나 이름이 필요합니다.");
            }
            if (!request.WarehouseOperatorConsentConfirmed)
            {
                warnings.Add("창고 운영자의 입고 동의가 필요합니다.");
            }
            if (!request.WarehouseSiteVerified)
            {
                warnings.Add("선택한 창고의 현장 또는 계약 검증이 필요합니다.");
            }
            if (!request.WarehouseBulkReceivingSupported || !request.WarehouseStorageSupported)
            {
                warnings.Add("선택한 창고의 일괄 입고와 보관 능력을 확인해야 합니다.");
            }
            if (request.RequiresWarehouseOutbound && !request.WarehouseOutboundSupported)
            {
                warnings.Add("선택한 창고의 후속 출고·분배 지원 여부를 확인해야 합니다.");
            }
            if (request.RequiresFinalDestinationDelivery && !request.RequiresWarehouseOutbound)
            {
                warnings.Add("최종 도착지 배송을 사용하려면 창고 출고·분배 단계도 선택해야 합니다.");
            }
        }

        return warnings;
    }

    private static CommunityGroupImportLedgerPlanNode Node(
        string nodeId,
        string templateKey,
        string role,
        string relationType,
        string title,
        string responsibleParty,
        int order,
        bool isSourceReference = false)
        => new()
        {
            NodeId = nodeId,
            LedgerTemplateKey = templateKey,
            RelationRole = role,
            RelationType = relationType,
            Title = title,
            ResponsiblePartyLabel = responsibleParty,
            Order = order,
            IsSourceReference = isSourceReference
        };

    private static string NormalizeRoute(string? routeCode)
        => CommunityGroupImportLogisticsRouteCodes.All.FirstOrDefault(x =>
               string.Equals(x, routeCode?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? CommunityGroupImportLogisticsRouteCodes.DirectDestination;

    public static string LabelOf(string routeCode)
        => routeCode switch
        {
            CommunityGroupImportLogisticsRouteCodes.ThreePlWarehouse => "3PL 입고 후 출고·분배",
            CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse => "전용 창고 입고 후 출고·분배",
            _ => "보세구역·항만에서 최종 도착지 직배송"
        };

    private static string LabelOf(
        string routeCode,
        bool requiresWarehouseOutbound,
        bool requiresFinalDelivery)
    {
        if (routeCode == CommunityGroupImportLogisticsRouteCodes.DirectDestination)
        {
            return LabelOf(routeCode);
        }

        var warehouseLabel = routeCode == CommunityGroupImportLogisticsRouteCodes.ThreePlWarehouse
            ? "3PL"
            : "전용 창고";
        return requiresFinalDelivery
            ? $"{warehouseLabel} 입고 후 최종 도착지 배송"
            : requiresWarehouseOutbound
                ? $"{warehouseLabel} 입고 후 출고·분배"
                : $"{warehouseLabel} 입고·보관";
    }
}
