using Hongdal.Contracts.Common.Community;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Hongdal.Contracts.Common.Orderer;

public static class DomesticGroupPurchaseFulfillmentRouteCodes
{
    public const string TraditionalMarketHub = "traditional-market-hub";
    public const string ThirdPartyLogistics = "third-party-logistics";
    public const string DedicatedWarehouse = "dedicated-warehouse";
    public const string DirectCollectionPoint = "direct-collection-point";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        TraditionalMarketHub,
        ThirdPartyLogistics,
        DedicatedWarehouse,
        DirectCollectionPoint
    };
}

public static class DomesticGroupPurchaseFulfillmentDraftStatuses
{
    public const string Preview = "preview";
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";
}

public sealed class DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot
{
    public bool HasOperatorConsent { get; set; }
    public bool SiteVerified { get; set; }
    public bool SupportsBulkReceiving { get; set; }
    public bool SupportsSorting { get; set; }
    public bool SupportsStorage { get; set; }
    public bool SupportsLastMileHandoff { get; set; }
    public decimal HandlingCapacity { get; set; }
    public string CapacityUnit { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseFulfillmentPlanRequest
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string RouteCode { get; set; } = DomesticGroupPurchaseFulfillmentRouteCodes.DirectCollectionPoint;
    public string ProducerDisplayName { get; set; } = string.Empty;
    public string ProductSummary { get; set; } = string.Empty;
    public string QuantitySummary { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public string DestinationLabel { get; set; } = string.Empty;
    public string HubReferenceKey { get; set; } = string.Empty;
    public string HubDisplayName { get; set; } = string.Empty;
    public bool RequiresSorting { get; set; }
    public bool RequiresStorage { get; set; }
    public bool RequiresLastMileDelivery { get; set; } = true;
    public bool ProducerTermsAccepted { get; set; }
    public bool BuyerRepresentativeTermsAccepted { get; set; }
    public bool SupplyCompatibilityConfirmed { get; set; }
    public DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot HubCapabilities { get; set; } = new();
}

public sealed class DomesticGroupPurchaseFulfillmentLedgerNode
{
    public string NodeId { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string LedgerTemplateKey { get; set; } = string.Empty;
    public string IncludedLedgerRole { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ResponsiblePartyLabel { get; set; } = string.Empty;
    public string StageSummary { get; set; } = string.Empty;
    public int StageOrder { get; set; }
    public bool IsOrderRoot { get; set; }
    public bool Required { get; set; } = true;
}

public sealed class DomesticGroupPurchaseFulfillmentLedgerEdge
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string RelationType { get; set; } = CommunityLedgerRelationTypes.Flow;
    public string Label { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseFulfillmentPlanResponse
{
    public string PlanVersion { get; set; } = "1.1";
    public string PlanFingerprint { get; set; } = string.Empty;
    public string AgreementPolicyCode { get; set; } = CommunityGroupPurchaseAgreementPolicy.PolicyCode;
    public string ProposalOriginLegalEffectNotice { get; set; }
        = CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice;
    public Guid GroupPurchaseCampaignId { get; set; }
    public string RouteCode { get; set; } = string.Empty;
    public string RouteLabel { get; set; } = string.Empty;
    public string OrderLedgerNodeId { get; set; } = "order-root";
    public string OrderLedgerId { get; set; } = string.Empty;
    public bool OrderPlacementReady { get; set; }
    public bool LedgersPersisted { get; set; }
    public bool OrderPlaced { get; set; }
    public IReadOnlyList<DomesticGroupPurchaseFulfillmentLedgerNode> LedgerNodes { get; set; } = [];
    public IReadOnlyList<DomesticGroupPurchaseFulfillmentLedgerEdge> LedgerEdges { get; set; } = [];
    public IReadOnlyList<string> PlanningWarnings { get; set; } = [];
    public DomesticGroupPurchaseFulfillmentPlanRequest RequestSnapshot { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseFulfillmentOrderDraftResponse
{
    public Guid DraftId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = DomesticGroupPurchaseFulfillmentDraftStatuses.Draft;
    public bool IsDurablyPersisted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string AgreementPolicyCode { get; set; } = CommunityGroupPurchaseAgreementPolicy.PolicyCode;
    public string ProposalOriginLegalEffectNotice { get; set; }
        = CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice;
    public DomesticGroupPurchaseFulfillmentPlanResponse Plan { get; set; } = new();
    public string GuidanceMessage { get; set; } = string.Empty;
}

public static class DomesticGroupPurchaseFulfillmentPlanBuilder
{
    public static DomesticGroupPurchaseFulfillmentPlanResponse Preview(
        DomesticGroupPurchaseFulfillmentPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var routeRecognized = DomesticGroupPurchaseFulfillmentRouteCodes.All.Contains(request.RouteCode?.Trim() ?? string.Empty);
        var routeCode = NormalizeRouteCode(request.RouteCode);
        var nodes = new List<DomesticGroupPurchaseFulfillmentLedgerNode>
        {
            Node(
                "order-root",
                CommunityLedgerTemplateKeys.Order,
                string.Empty,
                "발주 주문 원장",
                "공동구매 대표",
                $"{request.ProductSummary} {request.QuantitySummary}".Trim(),
                0,
                isOrderRoot: true),
            Node(
                "producer-sale",
                CommunityLedgerTemplateKeys.LocalSale,
                주문원장포함역할.판매,
                "생산자 공급·판매 원장",
                string.IsNullOrWhiteSpace(request.ProducerDisplayName) ? "생산자" : request.ProducerDisplayName.Trim(),
                "품목·수량·포장·가격 조건 확정",
                1)
        };

        switch (routeCode)
        {
            case DomesticGroupPurchaseFulfillmentRouteCodes.TraditionalMarketHub:
                AddTransport(nodes, "transport-to-hub", "생산지 → 전통시장 거점 운송", 2);
                nodes.Add(Node(
                    "market-hub-inbound",
                    CommunityLedgerTemplateKeys.WarehouseInbound,
                    주문원장포함역할.창고입고,
                    "전통시장 거점 입고·인계 원장",
                    ResolveHubLabel(request, "전통시장 공동물류 거점"),
                    request.RequiresSorting ? "거점 입고, 검수와 공동구매별 선별" : "거점 입고와 인수 확인",
                    3));
                if (request.RequiresSorting || request.RequiresStorage)
                {
                    nodes.Add(Node(
                        "market-hub-outbound",
                        CommunityLedgerTemplateKeys.WarehouseOutbound,
                        주문원장포함역할.창고출고,
                        "전통시장 거점 분류·출고 원장",
                        ResolveHubLabel(request, "전통시장 공동물류 거점"),
                        request.RequiresStorage ? "임시 보관 뒤 공동 수령 단위 출고" : "공동 수령 단위 선별·출고",
                        4));
                }

                if (request.RequiresLastMileDelivery)
                {
                    AddTransport(nodes, "transport-to-destination", "전통시장 거점 → 공동 수령지 운송", 5);
                }
                break;

            case DomesticGroupPurchaseFulfillmentRouteCodes.ThirdPartyLogistics:
                AddTransport(nodes, "transport-to-3pl", "생산지 → 3PL 운송", 2);
                nodes.Add(Node(
                    "third-party-inbound",
                    CommunityLedgerTemplateKeys.WarehouseInbound,
                    주문원장포함역할.창고입고,
                    "3PL 입고·검수 원장",
                    ResolveHubLabel(request, "3PL 업체"),
                    "입고, 수량 검수와 보관 위치 확정",
                    3));
                nodes.Add(Node(
                    "third-party-outbound",
                    CommunityLedgerTemplateKeys.WarehouseOutbound,
                    주문원장포함역할.창고출고,
                    "3PL 피킹·출고 원장",
                    ResolveHubLabel(request, "3PL 업체"),
                    request.RequiresSorting ? "참여자·수령지 단위 피킹과 출고" : "공동 수령 단위 출고",
                    4));
                if (request.RequiresLastMileDelivery)
                {
                    AddTransport(nodes, "transport-to-destination", "3PL → 공동 수령지 운송", 5);
                }
                break;

            case DomesticGroupPurchaseFulfillmentRouteCodes.DedicatedWarehouse:
                AddTransport(nodes, "transport-to-dedicated-warehouse", "생산지 → 전용 창고 운송", 2);
                nodes.Add(Node(
                    "dedicated-warehouse-inbound",
                    CommunityLedgerTemplateKeys.WarehouseInbound,
                    주문원장포함역할.창고입고,
                    "전용 창고 입고·검수 원장",
                    ResolveHubLabel(request, "전용 창고"),
                    "전용 보관 장소 입고, 수량 검수와 재고 인계",
                    3));
                if (request.RequiresSorting || request.RequiresLastMileDelivery)
                {
                    nodes.Add(Node(
                        "dedicated-warehouse-outbound",
                        CommunityLedgerTemplateKeys.WarehouseOutbound,
                        주문원장포함역할.창고출고,
                        "전용 창고 출고 원장",
                        ResolveHubLabel(request, "전용 창고"),
                        request.RequiresSorting ? "참여자·수령지 단위 분류와 출고" : "공동 수령 단위 출고",
                        4));
                }
                if (request.RequiresLastMileDelivery)
                {
                    AddTransport(nodes, "transport-from-dedicated-warehouse", "전용 창고 → 최종 도착지 운송", 5);
                }
                break;

            default:
                AddTransport(nodes, "transport-direct", "생산지 → 공동 수령지 직송", 2);
                break;
        }

        var orderedFlow = nodes.Where(x => !x.IsOrderRoot).OrderBy(x => x.StageOrder).ToArray();
        var edges = new List<DomesticGroupPurchaseFulfillmentLedgerEdge>();
        edges.AddRange(nodes.Where(x => !x.IsOrderRoot).Select(x => new DomesticGroupPurchaseFulfillmentLedgerEdge
        {
            FromNodeId = "order-root",
            ToNodeId = x.NodeId,
            RelationType = CommunityLedgerRelationTypes.Contains,
            Label = string.IsNullOrWhiteSpace(x.IncludedLedgerRole) ? "포함" : x.IncludedLedgerRole
        }));
        for (var index = 0; index < orderedFlow.Length - 1; index++)
        {
            edges.Add(new DomesticGroupPurchaseFulfillmentLedgerEdge
            {
                FromNodeId = orderedFlow[index].NodeId,
                ToNodeId = orderedFlow[index + 1].NodeId,
                RelationType = CommunityLedgerRelationTypes.Flow,
                Label = "업무 인계"
            });
        }

        var warnings = BuildWarnings(request, routeCode, routeRecognized);
        var ready = warnings.Count == 0;
        return new DomesticGroupPurchaseFulfillmentPlanResponse
        {
            PlanVersion = "1.1",
            PlanFingerprint = CreateFingerprint(request, routeCode),
            AgreementPolicyCode = CommunityGroupPurchaseAgreementPolicy.PolicyCode,
            ProposalOriginLegalEffectNotice = CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice,
            GroupPurchaseCampaignId = request.GroupPurchaseCampaignId,
            RouteCode = routeCode,
            RouteLabel = GetRouteLabel(routeCode),
            OrderPlacementReady = ready,
            LedgersPersisted = false,
            OrderPlaced = false,
            LedgerNodes = nodes,
            LedgerEdges = edges,
            PlanningWarnings = warnings,
            RequestSnapshot = CloneRequest(request, routeCode),
            Summary = ready
                ? $"발주 주문 원장을 중심으로 {nodes.Count - 1}개의 후속 원장을 생성할 준비가 되었습니다."
                : "발주와 원장 생성 전에 빠진 거점 또는 주문 조건을 확인해야 합니다."
        };
    }

    private static void AddTransport(
        ICollection<DomesticGroupPurchaseFulfillmentLedgerNode> nodes,
        string nodeId,
        string title,
        int order)
        => nodes.Add(Node(
            nodeId,
            CommunityLedgerTemplateKeys.CargoTransport,
            주문원장포함역할.운송,
            title,
            "국내 운송 주체",
            "상차, 운송, 하차와 인수 증빙",
            order));

    private static DomesticGroupPurchaseFulfillmentLedgerNode Node(
        string nodeId,
        string templateKey,
        string role,
        string title,
        string responsibleParty,
        string summary,
        int order,
        bool isOrderRoot = false)
        => new()
        {
            NodeId = nodeId,
            LedgerTemplateKey = templateKey,
            IncludedLedgerRole = role,
            Title = title,
            ResponsiblePartyLabel = responsibleParty,
            StageSummary = summary,
            StageOrder = order,
            IsOrderRoot = isOrderRoot,
            Required = true
        };

    private static List<string> BuildWarnings(
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        string routeCode,
        bool routeRecognized)
    {
        var warnings = new List<string>();
        if (!routeRecognized)
        {
            warnings.Add("지원하지 않는 이행 경로이므로 발주할 수 없습니다.");
        }
        if (request.GroupPurchaseCampaignId == Guid.Empty)
        {
            warnings.Add("공동구매 캠페인 식별자가 필요합니다.");
        }
        if (string.IsNullOrWhiteSpace(request.ProductSummary))
        {
            warnings.Add("발주 품목이 필요합니다.");
        }
        if (string.IsNullOrWhiteSpace(request.QuantitySummary))
        {
            warnings.Add("발주 수량이 필요합니다.");
        }
        if (request.PlannedQuantity <= 0 || string.IsNullOrWhiteSpace(request.QuantityUnit))
        {
            warnings.Add("거점 처리 능력과 비교할 수치형 발주 수량과 단위가 필요합니다.");
        }
        if (string.IsNullOrWhiteSpace(request.DestinationLabel))
        {
            warnings.Add("최종 공동 수령지 또는 집합지가 필요합니다.");
        }
        if (routeCode != DomesticGroupPurchaseFulfillmentRouteCodes.DirectCollectionPoint
            && string.IsNullOrWhiteSpace(request.HubReferenceKey)
            && string.IsNullOrWhiteSpace(request.HubDisplayName))
        {
            warnings.Add("선택한 중간 거점의 참조 또는 이름이 필요합니다.");
        }
        if (!request.ProducerTermsAccepted)
        {
            warnings.Add("생산자의 공급·출하 조건 수락이 필요합니다.");
        }
        if (!request.BuyerRepresentativeTermsAccepted)
        {
            warnings.Add("공동구매 대표의 인수·발주 조건 수락이 필요합니다.");
        }
        if (!request.SupplyCompatibilityConfirmed)
        {
            warnings.Add("포장 규격과 양측 물량 조건의 상호 적합 확인이 필요합니다.");
        }

        if (routeCode != DomesticGroupPurchaseFulfillmentRouteCodes.DirectCollectionPoint)
        {
            var capabilities = request.HubCapabilities ?? new DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot();
            if (!capabilities.HasOperatorConsent)
            {
                warnings.Add("중간 거점 운영자의 사용 동의가 필요합니다.");
            }
            if (!capabilities.SiteVerified)
            {
                warnings.Add("중간 거점의 현장 확인이 필요합니다.");
            }
            if (!capabilities.SupportsBulkReceiving)
            {
                warnings.Add("중간 거점의 공동구매 물량 일괄 입고 능력을 확인해야 합니다.");
            }
            if (request.RequiresSorting && !capabilities.SupportsSorting)
            {
                warnings.Add("선택한 거점이 요구된 선별·재포장을 지원하지 않습니다.");
            }
            if (request.RequiresStorage && !capabilities.SupportsStorage)
            {
                warnings.Add("선택한 거점이 요구된 임시 보관을 지원하지 않습니다.");
            }
            if (request.RequiresLastMileDelivery && !capabilities.SupportsLastMileHandoff)
            {
                warnings.Add("선택한 거점이 후속 운송 인계를 지원하지 않습니다.");
            }
            if (capabilities.HandlingCapacity <= 0 || string.IsNullOrWhiteSpace(capabilities.CapacityUnit))
            {
                warnings.Add("중간 거점의 일일 처리 가능 물량과 단위가 필요합니다.");
            }
            else if (!string.Equals(capabilities.CapacityUnit.Trim(), request.QuantityUnit.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("발주 수량과 거점 처리 능력의 단위가 달라 환산 확인이 필요합니다.");
            }
            else if (capabilities.HandlingCapacity < request.PlannedQuantity)
            {
                warnings.Add("중간 거점의 일일 처리 능력이 발주 물량보다 작습니다.");
            }
        }
        return warnings;
    }

    private static DomesticGroupPurchaseFulfillmentPlanRequest CloneRequest(
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        string routeCode)
        => new()
        {
            GroupPurchaseCampaignId = request.GroupPurchaseCampaignId,
            CampaignTitle = request.CampaignTitle,
            RouteCode = routeCode,
            ProducerDisplayName = request.ProducerDisplayName,
            ProductSummary = request.ProductSummary,
            QuantitySummary = request.QuantitySummary,
            PlannedQuantity = request.PlannedQuantity,
            QuantityUnit = request.QuantityUnit,
            DestinationLabel = request.DestinationLabel,
            HubReferenceKey = request.HubReferenceKey,
            HubDisplayName = request.HubDisplayName,
            RequiresSorting = request.RequiresSorting,
            RequiresStorage = request.RequiresStorage,
            RequiresLastMileDelivery = request.RequiresLastMileDelivery,
            ProducerTermsAccepted = request.ProducerTermsAccepted,
            BuyerRepresentativeTermsAccepted = request.BuyerRepresentativeTermsAccepted,
            SupplyCompatibilityConfirmed = request.SupplyCompatibilityConfirmed,
            HubCapabilities = new DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot
            {
                HasOperatorConsent = request.HubCapabilities?.HasOperatorConsent == true,
                SiteVerified = request.HubCapabilities?.SiteVerified == true,
                SupportsBulkReceiving = request.HubCapabilities?.SupportsBulkReceiving == true,
                SupportsSorting = request.HubCapabilities?.SupportsSorting == true,
                SupportsStorage = request.HubCapabilities?.SupportsStorage == true,
                SupportsLastMileHandoff = request.HubCapabilities?.SupportsLastMileHandoff == true,
                HandlingCapacity = request.HubCapabilities?.HandlingCapacity ?? 0,
                CapacityUnit = request.HubCapabilities?.CapacityUnit ?? string.Empty
            }
        };

    private static string CreateFingerprint(
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        string routeCode)
    {
        var capabilities = request.HubCapabilities ?? new DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot();
        var canonical = string.Join('|',
            request.GroupPurchaseCampaignId.ToString("N"),
            CommunityGroupPurchaseAgreementPolicy.PolicyCode,
            NormalizeFingerprintPart(routeCode),
            NormalizeFingerprintPart(request.ProducerDisplayName),
            NormalizeFingerprintPart(request.ProductSummary),
            NormalizeFingerprintPart(request.QuantitySummary),
            request.PlannedQuantity.ToString("0.####", CultureInfo.InvariantCulture),
            NormalizeFingerprintPart(request.QuantityUnit),
            NormalizeFingerprintPart(request.DestinationLabel),
            NormalizeFingerprintPart(request.HubReferenceKey),
            NormalizeFingerprintPart(request.HubDisplayName),
            request.RequiresSorting,
            request.RequiresStorage,
            request.RequiresLastMileDelivery,
            request.ProducerTermsAccepted,
            request.BuyerRepresentativeTermsAccepted,
            request.SupplyCompatibilityConfirmed,
            capabilities.HasOperatorConsent,
            capabilities.SiteVerified,
            capabilities.SupportsBulkReceiving,
            capabilities.SupportsSorting,
            capabilities.SupportsStorage,
            capabilities.SupportsLastMileHandoff,
            capabilities.HandlingCapacity.ToString("0.####", CultureInfo.InvariantCulture),
            NormalizeFingerprintPart(capabilities.CapacityUnit));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string NormalizeFingerprintPart(string? value)
        => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string NormalizeRouteCode(string? routeCode)
        => DomesticGroupPurchaseFulfillmentRouteCodes.All.FirstOrDefault(code =>
               string.Equals(code, routeCode?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? DomesticGroupPurchaseFulfillmentRouteCodes.DirectCollectionPoint;

    private static string GetRouteLabel(string routeCode)
        => routeCode switch
        {
            DomesticGroupPurchaseFulfillmentRouteCodes.TraditionalMarketHub => "전통시장 공동물류 거점",
            DomesticGroupPurchaseFulfillmentRouteCodes.ThirdPartyLogistics => "3PL 입출고 거점",
            DomesticGroupPurchaseFulfillmentRouteCodes.DedicatedWarehouse => "전용 창고 입고·보관",
            _ => "공동 수령·집합지 직송"
        };

    private static string ResolveHubLabel(
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        string fallback)
        => string.IsNullOrWhiteSpace(request.HubDisplayName)
            ? fallback
            : request.HubDisplayName.Trim();
}
