using System.Collections.Concurrent;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.Community;

namespace Hongdal.Services.Orderer;

public interface IDomesticGroupPurchaseFulfillmentOrderDraftStore
{
    Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse> SaveAsync(
        DomesticGroupPurchaseFulfillmentOrderDraftResponse draft,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryDomesticGroupPurchaseFulfillmentOrderDraftStore
    : IDomesticGroupPurchaseFulfillmentOrderDraftStore
{
    private readonly ConcurrentDictionary<Guid, DomesticGroupPurchaseFulfillmentOrderDraftResponse> drafts = new();

    public Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse> SaveAsync(
        DomesticGroupPurchaseFulfillmentOrderDraftResponse draft,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        drafts[draft.DraftId] = draft;
        return Task.FromResult(draft);
    }

    public Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        drafts.TryGetValue(draftId, out var draft);
        return Task.FromResult(draft);
    }
}

public interface IDomesticGroupPurchaseFulfillmentPlanService
{
    DomesticGroupPurchaseFulfillmentPlanResponse Preview(
        DomesticGroupPurchaseFulfillmentPlanRequest request);

    Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse> CreateOrderDraftAsync(
        string createdByUserId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> GetOrderDraftAsync(
        string createdByUserId,
        Guid draftId,
        CancellationToken cancellationToken = default);
}

public sealed class DomesticGroupPurchaseFulfillmentPlanService
    : IDomesticGroupPurchaseFulfillmentPlanService
{
    private readonly IDomesticGroupPurchaseFulfillmentOrderDraftStore draftStore;
    private readonly I커뮤니티원장저장소? ledgerStore;
    private readonly I공동구매원장절차Service? ledgerWorkflow;
    private readonly I주문원장통합UseCase? orderLedgerUseCase;

    public DomesticGroupPurchaseFulfillmentPlanService(
        IDomesticGroupPurchaseFulfillmentOrderDraftStore draftStore)
        : this(draftStore, null, null, null)
    {
    }

    public DomesticGroupPurchaseFulfillmentPlanService(
        IDomesticGroupPurchaseFulfillmentOrderDraftStore draftStore,
        I커뮤니티원장저장소? ledgerStore,
        I공동구매원장절차Service? ledgerWorkflow,
        I주문원장통합UseCase? orderLedgerUseCase)
    {
        this.draftStore = draftStore;
        this.ledgerStore = ledgerStore;
        this.ledgerWorkflow = ledgerWorkflow;
        this.orderLedgerUseCase = orderLedgerUseCase;
    }

    public DomesticGroupPurchaseFulfillmentPlanResponse Preview(
        DomesticGroupPurchaseFulfillmentPlanRequest request)
        => DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(request);

    public async Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse> CreateOrderDraftAsync(
        string createdByUserId,
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByUserId);
        var plan = Preview(request);
        if (!plan.OrderPlacementReady)
        {
            throw new ArgumentException(string.Join(" ", plan.PlanningWarnings), nameof(request));
        }

        var draftId = Guid.NewGuid();
        if (ledgerStore is not null && ledgerWorkflow is not null && orderLedgerUseCase is not null)
        {
            await 원장생성및연결Async(
                draftId,
                createdByUserId.Trim(),
                plan,
                ledgerStore,
                ledgerWorkflow,
                orderLedgerUseCase,
                cancellationToken);
        }

        var draft = new DomesticGroupPurchaseFulfillmentOrderDraftResponse
        {
            DraftId = draftId,
            CreatedByUserId = createdByUserId.Trim(),
            StatusCode = DomesticGroupPurchaseFulfillmentDraftStatuses.Draft,
            IsDurablyPersisted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AgreementPolicyCode = plan.AgreementPolicyCode,
            ProposalOriginLegalEffectNotice = plan.ProposalOriginLegalEffectNotice,
            Plan = plan,
            GuidanceMessage = plan.LedgersPersisted
                ? $"발주 주문 원장과 계획된 판매·입고·출고·운송 원장을 생성하고 공동구매 원장에 자동 연결했습니다. 실제 주문 확정 전에는 초안 상태로 유지됩니다. {plan.ProposalOriginLegalEffectNotice}"
                : $"발주와 원장 생성 계획을 서버 메모리 초안으로 보관했습니다. 확정 단계에서는 주문 원장을 먼저 만든 뒤 계획된 판매·창고·운송 원장을 포함 관계로 연결해야 합니다. {plan.ProposalOriginLegalEffectNotice}"
        };

        return await draftStore.SaveAsync(draft, cancellationToken);
    }

    public async Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> GetOrderDraftAsync(
        string createdByUserId,
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByUserId);
        if (draftId == Guid.Empty)
        {
            return null;
        }

        var draft = await draftStore.GetAsync(draftId, cancellationToken);
        return draft is not null
               && string.Equals(draft.CreatedByUserId, createdByUserId.Trim(), StringComparison.Ordinal)
            ? draft
            : null;
    }

    private static async Task 원장생성및연결Async(
        Guid draftId,
        string createdByUserId,
        DomesticGroupPurchaseFulfillmentPlanResponse plan,
        I커뮤니티원장저장소 ledgerStore,
        I공동구매원장절차Service ledgerWorkflow,
        I주문원장통합UseCase orderLedgerUseCase,
        CancellationToken cancellationToken)
    {
        var sourceProgress = await ledgerWorkflow.조회Async(plan.GroupPurchaseCampaignId, cancellationToken)
            ?? throw new InvalidOperationException("발주 원장을 연결할 공동구매 원장을 찾을 수 없습니다.");
        var sourceLedger = await ledgerStore.원장조회Async(sourceProgress.CommunityLedgerId, cancellationToken)
            ?? throw new InvalidOperationException("발주 원장을 연결할 공동구매 원장 상세를 찾을 수 없습니다.");
        var rootNode = plan.LedgerNodes.Single(x => x.IsOrderRoot);
        var rootLedgerId = 원장Id생성(sourceProgress.CommunityLedgerId, draftId, rootNode.NodeId);

        foreach (var node in plan.LedgerNodes.Where(x => !x.IsOrderRoot).OrderBy(x => x.StageOrder))
        {
            node.LedgerId = 원장Id생성(sourceProgress.CommunityLedgerId, draftId, node.NodeId);
            await ledgerStore.원장저장Async(
                원장저장요청생성(
                    node,
                    node.LedgerId,
                    sourceLedger,
                    draftId,
                    createdByUserId,
                    includedLedgers: null,
                    rootLedgerId),
                createdByUserId,
                cancellationToken);
        }

        rootNode.LedgerId = rootLedgerId;
        var includedLedgers = plan.LedgerNodes
            .Where(x => !x.IsOrderRoot)
            .OrderBy(x => x.StageOrder)
            .Select(x => new 커뮤니티포함원장참조Dto
            {
                원장Id = x.LedgerId,
                원장템플릿Key = x.LedgerTemplateKey,
                역할 = x.IncludedLedgerRole,
                필수여부 = x.Required,
                표시순서 = x.StageOrder
            })
            .ToArray();
        await ledgerStore.원장저장Async(
            원장저장요청생성(
                rootNode,
                rootLedgerId,
                sourceLedger,
                draftId,
                createdByUserId,
                includedLedgers,
                rootLedgerId),
            createdByUserId,
            cancellationToken);

        var linked = await orderLedgerUseCase.하위원장연결Async(
            sourceLedger.원장Id,
            new 주문하위원장연결요청
            {
                하위원장Id = rootLedgerId,
                역할 = 주문원장포함역할.개별주문,
                필수여부 = true,
                표시순서 = sourceLedger.포함원장목록.Count
            },
            createdByUserId,
            cancellationToken);
        if (linked.IsFailed)
        {
            throw new InvalidOperationException(string.Join(" ", linked.Errors.Select(x => x.Message)));
        }

        plan.OrderLedgerId = rootLedgerId;
        plan.LedgersPersisted = true;
        await ledgerWorkflow.진행Async(
            plan.GroupPurchaseCampaignId,
            new CommunityGroupPurchaseLedgerProgressRequest
            {
                StageCode = CommunityGroupPurchaseLedgerStageCodes.Execution,
                Memo = $"발주 주문 원장과 후속 원장 {includedLedgers.Length}개를 생성하고 자동 연결했습니다."
            },
            createdByUserId,
            cancellationToken);
    }

    private static 커뮤니티원장저장요청 원장저장요청생성(
        DomesticGroupPurchaseFulfillmentLedgerNode node,
        string ledgerId,
        커뮤니티원장Dto sourceLedger,
        Guid draftId,
        string createdByUserId,
        IReadOnlyList<커뮤니티포함원장참조Dto>? includedLedgers,
        string rootLedgerId)
    {
        var template = CommunityLedgerTemplateCatalog.Find(node.LedgerTemplateKey);
        return new 커뮤니티원장저장요청
        {
            원장Id = ledgerId,
            커뮤니티Id = sourceLedger.커뮤니티Id,
            원장템플릿Key = node.LedgerTemplateKey,
            제목 = node.Title,
            원함 = node.StageSummary,
            상태 = 커뮤니티원장상태.초안,
            현재단계Key = "planned",
            대상OsCode = template.TargetOperatingSystemCode,
            대상OsName = template.TargetOperatingSystemName,
            생성자UserId = createdByUserId,
            생성자표시명 = node.ResponsiblePartyLabel,
            포함원장목록 = includedLedgers,
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
                        ["StageSummary"] = node.StageSummary,
                        ["ResponsiblePartyLabel"] = node.ResponsiblePartyLabel,
                        ["StageOrder"] = node.StageOrder.ToString()
                    }
                }
            ],
            외부참조 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GroupPurchaseCampaignId"] = sourceLedger.외부참조.TryGetValue("GroupPurchaseCampaignId", out var campaignId)
                    ? campaignId
                    : string.Empty,
                ["SourceGroupPurchaseLedgerId"] = sourceLedger.원장Id,
                ["FulfillmentDraftId"] = draftId.ToString("D"),
                ["PlanNodeId"] = node.NodeId,
                ["OrderRootLedgerId"] = rootLedgerId
            }
        };
    }

    private static string 원장Id생성(string sourceLedgerId, Guid draftId, string nodeId)
        => $"{sourceLedgerId}-{draftId:N}-{nodeId}";
}
