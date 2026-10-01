using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public interface I공동수입원장전환Service
{
    CommunityGroupImportLedgerPlanResponse 미리보기(
        CommunityGroupImportLedgerConversionRequest request);

    Task<CommunityGroupImportLedgerPlanResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CommunityGroupImportLedgerPlanResponse> 전환Async(
        CommunityGroupImportLedgerConversionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class 공동수입원장전환Service : I공동수입원장전환Service
{
    private readonly I공동구매원장캠페인Store campaignStore;
    private readonly I공동구매원장절차Service groupPurchaseWorkflow;
    private readonly I커뮤니티원장저장소 ledgerStore;

    public 공동수입원장전환Service(
        I공동구매원장캠페인Store campaignStore,
        I공동구매원장절차Service groupPurchaseWorkflow,
        I커뮤니티원장저장소 ledgerStore)
    {
        this.campaignStore = campaignStore;
        this.groupPurchaseWorkflow = groupPurchaseWorkflow;
        this.ledgerStore = ledgerStore;
    }

    public CommunityGroupImportLedgerPlanResponse 미리보기(
        CommunityGroupImportLedgerConversionRequest request)
        => CommunityGroupImportLedgerPlanBuilder.Preview(request);

    public async Task<CommunityGroupImportLedgerPlanResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
        {
            return null;
        }

        var ledger = await ledgerStore.원장조회Async(원장Id생성(campaignId), cancellationToken);
        if (ledger is null
            || !string.Equals(
                ledger.원장템플릿Key,
                CommunityLedgerTemplateKeys.GroupImport,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ToResponse(campaignId, ledger, created: false);
    }

    public async Task<CommunityGroupImportLedgerPlanResponse> 전환Async(
        CommunityGroupImportLedgerConversionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);

        var plan = CommunityGroupImportLedgerPlanBuilder.Preview(request);
        if (!plan.Ready)
        {
            throw new InvalidOperationException(string.Join(" ", plan.Warnings));
        }

        var campaign = await campaignStore.조회Async(request.GroupPurchaseCampaignId, cancellationToken)
            ?? throw new InvalidOperationException("공동수입으로 전환할 공동구매 캠페인을 찾을 수 없습니다.");
        ValidateGroupImportCandidate(campaign);

        var sourceProgress = await groupPurchaseWorkflow.조회Async(request.GroupPurchaseCampaignId, cancellationToken)
            ?? throw new InvalidOperationException("원천 공동구매 원장을 찾을 수 없습니다.");
        if (CommunityGroupPurchaseLedgerStageCodes.OrderOf(sourceProgress.CurrentStageCode)
            < CommunityGroupPurchaseLedgerStageCodes.OrderOf(CommunityGroupPurchaseLedgerStageCodes.FulfillmentPlan))
        {
            throw new InvalidOperationException("결의와 필수 전자서명을 완료한 뒤 공동수입 원장으로 전환할 수 있습니다.");
        }

        var sourceLedger = await ledgerStore.원장조회Async(sourceProgress.CommunityLedgerId, cancellationToken)
            ?? throw new InvalidOperationException("원천 공동구매 원장 상세를 찾을 수 없습니다.");
        var groupImportLedgerId = 원장Id생성(request.GroupPurchaseCampaignId);
        var existing = await ledgerStore.원장조회Async(groupImportLedgerId, cancellationToken);
        if (request.ExpectedRevision.HasValue
            && request.ExpectedRevision.Value != (existing?.Revision ?? 0))
        {
            throw new InvalidOperationException("공동수입 원장이 다른 요청에서 먼저 변경되었습니다.");
        }

        foreach (var node in plan.Nodes.Where(x => !x.IsSourceReference).OrderBy(x => x.Order))
        {
            node.LedgerId = 하위원장Id생성(groupImportLedgerId, node.NodeId);
            await ledgerStore.원장저장Async(
                하위원장저장요청생성(
                    node,
                    request,
                    campaign,
                    sourceLedger,
                    groupImportLedgerId,
                    actorUserId),
                actorUserId,
                cancellationToken);
        }

        var sourceNode = plan.Nodes.Single(x => x.IsSourceReference);
        sourceNode.LedgerId = sourceLedger.원장Id;
        var references = plan.Nodes
            .OrderBy(x => x.Order)
            .Select(x => new 커뮤니티포함원장참조Dto
            {
                원장Id = x.LedgerId,
                원장템플릿Key = x.LedgerTemplateKey,
                역할 = x.RelationRole,
                관계유형 = x.RelationType,
                필수여부 = true,
                표시순서 = x.Order
            })
            .ToArray();

        var saved = await ledgerStore.원장저장Async(
            new 커뮤니티원장저장요청
            {
                원장Id = groupImportLedgerId,
                기대Revision = existing?.Revision,
                커뮤니티Id = sourceLedger.커뮤니티Id,
                원장템플릿Key = CommunityLedgerTemplateKeys.GroupImport,
                제목 = $"{campaign.Title} 공동수입 원장",
                원함 = $"{request.ProductSummary}을(를) {plan.LogisticsRouteLabel} 경로로 수입·인도합니다.",
                상태 = 커뮤니티원장상태.진행중,
                현재단계Key = existing?.현재단계Key ?? CommunityGroupImportLedgerStageCodes.ImportDecision,
                대상OsCode = CommunityLedgerOperatingSystemCodes.GroupPurchaseImport,
                대상OsName = "공동수입 OS",
                생성자UserId = existing?.생성자UserId ?? actorUserId.Trim(),
                생성자표시명 = existing?.생성자표시명 ?? campaign.CreatedByDisplayName,
                블록목록 = BuildBlocks(request, campaign, sourceLedger, plan),
                참여자목록 = existing?.참여자목록 ?? sourceLedger.참여자목록,
                포함원장목록 = references,
                다이어그램스냅샷 = BuildDiagram(groupImportLedgerId, plan),
                외부참조 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GroupPurchaseCampaignId"] = request.GroupPurchaseCampaignId.ToString("D"),
                    ["SourceGroupPurchaseLedgerId"] = sourceLedger.원장Id,
                    ["HsCode"] = campaign.HsCode ?? string.Empty,
                    ["SellerCountryCode"] = campaign.SellerCountryCode ?? string.Empty,
                    ["ShipFromCountryCode"] = campaign.ShipFromCountryCode ?? string.Empty,
                    ["DeliveryCountryCode"] = campaign.DeliveryCountryCode ?? string.Empty,
                    ["OperatingMarketCountryCode"] = campaign.OperatingMarketCountryCode
                                                       ?? CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                    ["LogisticsRouteCode"] = plan.LogisticsRouteCode,
                    ["WarehouseReferenceKey"] = request.WarehouseReferenceKey.Trim()
                },
                확장속성 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WorkflowVersion"] = "1.0",
                    ["StageCatalog"] = string.Join(",", CommunityGroupImportLedgerStageCodes.Ordered),
                    ["LogisticsRouteLabel"] = plan.LogisticsRouteLabel,
                    ["FinalDestinationLabel"] = request.FinalDestinationLabel.Trim()
                }
            },
            actorUserId,
            cancellationToken);

        await groupPurchaseWorkflow.진행Async(
            request.GroupPurchaseCampaignId,
            new CommunityGroupPurchaseLedgerProgressRequest
            {
                StageCode = CommunityGroupPurchaseLedgerStageCodes.Execution,
                Memo = $"별도 공동수입 원장 {saved.원장Id}을 생성하고 {plan.LogisticsRouteLabel} 경로로 인계했습니다."
            },
            actorUserId,
            cancellationToken);

        return ToResponse(request.GroupPurchaseCampaignId, saved, created: existing is null);
    }

    private static void ValidateGroupImportCandidate(공동구매원장캠페인Snapshot campaign)
    {
        if (!CommunityGroupPurchaseTradeRouteCodes.IsGroupImport(campaign.TradeRouteCode))
        {
            throw new InvalidOperationException("해외 출발·운영 국가 반입으로 판정된 공동구매만 공동수입 원장으로 전환할 수 있습니다.");
        }

        var decision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                campaign.SellerCountryCode,
                campaign.ShipFromCountryCode,
                campaign.DeliveryCountryCode,
                campaign.CustomsClearanceStatusCode,
                campaign.OperatingMarketCountryCode));
        if (!decision.IsGroupImportCandidate || decision.RequiresManualReview)
        {
            throw new InvalidOperationException("상품 출발국가, 운영 국가 배송지와 통관 상태를 확정해야 합니다.");
        }

        var normalizedHsCode = new string((campaign.HsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalizedHsCode.Length is < 2 or > 10)
        {
            throw new InvalidOperationException("공동수입 원장을 만들려면 유효한 HS 코드가 필요합니다.");
        }
    }

    private static 커뮤니티원장저장요청 하위원장저장요청생성(
        CommunityGroupImportLedgerPlanNode node,
        CommunityGroupImportLedgerConversionRequest request,
        공동구매원장캠페인Snapshot campaign,
        커뮤니티원장Dto sourceLedger,
        string groupImportLedgerId,
        string actorUserId)
    {
        var template = CommunityLedgerTemplateCatalog.Find(node.LedgerTemplateKey);
        return new 커뮤니티원장저장요청
        {
            원장Id = node.LedgerId,
            커뮤니티Id = sourceLedger.커뮤니티Id,
            원장템플릿Key = node.LedgerTemplateKey,
            제목 = node.Title,
            원함 = $"{request.ProductSummary} {request.PlannedQuantity}{request.QuantityUnit}",
            상태 = 커뮤니티원장상태.초안,
            현재단계Key = "planned",
            대상OsCode = template.TargetOperatingSystemCode,
            대상OsName = template.TargetOperatingSystemName,
            생성자UserId = actorUserId,
            생성자표시명 = node.ResponsiblePartyLabel,
            블록목록 =
            [
                new 커뮤니티원장블록Dto
                {
                    BlockId = $"{node.NodeId}-plan",
                    BlockType = CommunityLedgerBlockTypes.Generic,
                    Title = node.Title,
                    State = "planned",
                    Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["GroupImportLedgerId"] = groupImportLedgerId,
                        ["PlanNodeId"] = node.NodeId,
                        ["ProductSummary"] = request.ProductSummary.Trim(),
                        ["PlannedQuantity"] = request.PlannedQuantity.ToString(),
                        ["QuantityUnit"] = request.QuantityUnit.Trim(),
                        ["LogisticsRouteCode"] = request.LogisticsRouteCode.Trim(),
                        ["WarehouseReferenceKey"] = request.WarehouseReferenceKey.Trim(),
                        ["WarehouseDisplayName"] = request.WarehouseDisplayName.Trim(),
                        ["FinalDestinationLabel"] = request.FinalDestinationLabel.Trim(),
                        ["RequiresWarehouseOutbound"] = request.RequiresWarehouseOutbound.ToString(),
                        ["RequiresFinalDestinationDelivery"] = request.RequiresFinalDestinationDelivery.ToString()
                    }
                }
            ],
            외부참조 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GroupImportLedgerId"] = groupImportLedgerId,
                ["GroupPurchaseCampaignId"] = campaign.CampaignId.ToString("D"),
                ["SourceGroupPurchaseLedgerId"] = sourceLedger.원장Id,
                ["HsCode"] = campaign.HsCode ?? string.Empty,
                ["OperatingMarketCountryCode"] = campaign.OperatingMarketCountryCode
                                                   ?? CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                ["PlanNodeId"] = node.NodeId
            }
        };
    }

    private static IReadOnlyList<커뮤니티원장블록Dto> BuildBlocks(
        CommunityGroupImportLedgerConversionRequest request,
        공동구매원장캠페인Snapshot campaign,
        커뮤니티원장Dto sourceLedger,
        CommunityGroupImportLedgerPlanResponse plan)
        =>
        [
            Block("source-group-purchase", "원천 공동구매", "linked", new()
            {
                ["SourceGroupPurchaseLedgerId"] = sourceLedger.원장Id,
                ["GroupPurchaseCampaignId"] = campaign.CampaignId.ToString("D")
            }),
            Block("import-decision", "공동수입 결정", "confirmed", new()
            {
                ["HsCode"] = campaign.HsCode ?? string.Empty,
                ["SellerCountryCode"] = campaign.SellerCountryCode ?? string.Empty,
                ["ShipFromCountryCode"] = campaign.ShipFromCountryCode ?? string.Empty,
                ["DeliveryCountryCode"] = campaign.DeliveryCountryCode ?? string.Empty,
                ["OperatingMarketCountryCode"] = campaign.OperatingMarketCountryCode
                                                   ?? CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode,
                ["CustomsClearanceStatusCode"] = campaign.CustomsClearanceStatusCode ?? string.Empty
            }),
            Block("overseas-contract", "해외 공급 계약·발주", "planned", new()
            {
                ["ProductSummary"] = request.ProductSummary.Trim(),
                ["PlannedQuantity"] = request.PlannedQuantity.ToString(),
                ["QuantityUnit"] = request.QuantityUnit.Trim()
            }),
            Block("shipment-customs", "해외 선적·통관", "planned", new()
            {
                ["InternationalTransportMode"] = request.InternationalTransportMode.Trim()
            }),
            Block("domestic-logistics", "국내 물류 선택", "confirmed", new()
            {
                ["LogisticsRouteCode"] = plan.LogisticsRouteCode,
                ["LogisticsRouteLabel"] = plan.LogisticsRouteLabel,
                ["WarehouseReferenceKey"] = request.WarehouseReferenceKey.Trim(),
                ["WarehouseDisplayName"] = request.WarehouseDisplayName.Trim(),
                ["FinalDestinationLabel"] = request.FinalDestinationLabel.Trim(),
                ["RequiresWarehouseOutbound"] = request.RequiresWarehouseOutbound.ToString(),
                ["RequiresFinalDestinationDelivery"] = request.RequiresFinalDestinationDelivery.ToString()
            }),
            Block("settlement", "수입 도착원가·정산", "planned", new())
        ];

    private static 커뮤니티원장블록Dto Block(
        string blockId,
        string title,
        string state,
        Dictionary<string, string> data)
        => new()
        {
            BlockId = blockId,
            BlockType = CommunityLedgerBlockTypes.Generic,
            Title = title,
            State = state,
            Data = data
        };

    private static DiagramSnapshotDto BuildDiagram(
        string groupImportLedgerId,
        CommunityGroupImportLedgerPlanResponse plan)
    {
        var nodes = new List<DiagramNodeDto>
        {
            new()
            {
                NodeId = "group-import-root",
                Kind = CommunityLedgerTemplateKeys.GroupImport,
                Title = "공동수입 원장",
                X = 420,
                Y = 40
            }
        };
        nodes.AddRange(plan.Nodes.Select((node, index) => new DiagramNodeDto
        {
            NodeId = node.NodeId,
            Kind = node.LedgerTemplateKey,
            Title = node.Title,
            X = 80 + (index % 3) * 340,
            Y = 220 + (index / 3) * 180,
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LedgerId"] = node.LedgerId,
                ["RelationRole"] = node.RelationRole,
                ["RelationType"] = node.RelationType
            }
        }));

        return new DiagramSnapshotDto
        {
            DiagramId = $"diagram-{groupImportLedgerId}",
            DiagramName = "공동수입 원장 물류 경로",
            LedgerTemplateKey = CommunityLedgerTemplateKeys.GroupImport,
            WorkflowModeKey = CommunityLedgerOperatingSystemCodes.GroupPurchaseImport,
            Nodes = nodes,
            Edges = plan.Nodes.Select(node => new DiagramEdgeDto
            {
                EdgeId = $"edge-group-import-{node.NodeId}",
                FromNodeId = "group-import-root",
                ToNodeId = node.NodeId,
                Label = node.RelationRole,
                MeaningCode = node.RelationType
            }).ToArray()
        };
    }

    private static CommunityGroupImportLedgerPlanResponse ToResponse(
        Guid campaignId,
        커뮤니티원장Dto ledger,
        bool created)
    {
        var routeCode = ledger.외부참조.GetValueOrDefault("LogisticsRouteCode")
                        ?? CommunityGroupImportLogisticsRouteCodes.DirectDestination;
        return new CommunityGroupImportLedgerPlanResponse
        {
            GroupPurchaseCampaignId = campaignId,
            SourceGroupPurchaseLedgerId = ledger.외부참조.GetValueOrDefault("SourceGroupPurchaseLedgerId") ?? string.Empty,
            GroupImportLedgerId = ledger.원장Id,
            LogisticsRouteCode = routeCode,
            LogisticsRouteLabel = ledger.확장속성.GetValueOrDefault("LogisticsRouteLabel")
                                  ?? CommunityGroupImportLedgerPlanBuilder.LabelOf(routeCode),
            Ready = true,
            Created = created,
            Revision = ledger.Revision,
            CurrentStageCode = ledger.현재단계Key ?? CommunityGroupImportLedgerStageCodes.ImportDecision,
            Nodes = ledger.포함원장목록.Select(reference => new CommunityGroupImportLedgerPlanNode
            {
                NodeId = reference.원장Id == ledger.외부참조.GetValueOrDefault("SourceGroupPurchaseLedgerId")
                    ? "source-group-purchase"
                    : reference.원장Id.StartsWith($"{ledger.원장Id}-", StringComparison.OrdinalIgnoreCase)
                        ? reference.원장Id[(ledger.원장Id.Length + 1)..]
                        : reference.원장Id,
                LedgerId = reference.원장Id,
                LedgerTemplateKey = reference.원장템플릿Key,
                RelationRole = reference.역할,
                RelationType = reference.관계유형,
                Title = reference.역할,
                Order = reference.표시순서,
                IsSourceReference = reference.역할 == 공동수입원장관계역할.원천공동구매
            }).ToArray()
        };
    }

    public static string 원장Id생성(Guid campaignId)
        => $"group-import-{campaignId:N}";

    private static string 하위원장Id생성(string groupImportLedgerId, string nodeId)
        => $"{groupImportLedgerId}-{nodeId}";
}
