using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public interface I공동구매원장절차Service
{
    Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
        Guid campaignId,
        CommunityGroupPurchaseLedgerProgressRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default);
}

public sealed record 공동구매원장캠페인Snapshot(
    Guid CampaignId,
    string VoteKind,
    string CommunityScope,
    string Title,
    string Description,
    long? SourcePostId,
    string CreatedByDisplayName,
    string Status,
    string? ResolutionStatus,
    string? CommunityLedgerId,
    string? TradeRouteCode = null,
    string? HsCode = null,
    string? SellerCountryCode = null,
    string? ShipFromCountryCode = null,
    string? DeliveryCountryCode = null,
    string? CustomsClearanceStatusCode = null,
    decimal TotalRequestedQuantity = 0,
    string? QuantityUnit = null,
    string? OperatingMarketCountryCode = null);

public interface I공동구매원장캠페인Store
{
    Task<공동구매원장캠페인Snapshot?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task 원장연결Async(
        Guid campaignId,
        string ledgerId,
        CancellationToken cancellationToken = default);
}

internal sealed class CommunityVote공동구매원장캠페인Store(ICommunityVoteStore store)
    : I공동구매원장캠페인Store
{
    public async Task<공동구매원장캠페인Snapshot?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var vote = await store.GetAsync(campaignId, cancellationToken);
        return vote is null
            ? null
            : new 공동구매원장캠페인Snapshot(
                vote.Id,
                vote.VoteKind,
                vote.CommunityScope,
                vote.Title,
                vote.Description,
                vote.SourcePostId,
                vote.CreatedByDisplayName,
                vote.Status,
                vote.ResolutionDocument?.Status,
                vote.CommunityLedgerId,
                vote.GroupPurchase?.TradeRouteCode,
                vote.GroupPurchase?.HsCode
                    ?? vote.Options.Select(x => x.HsCode).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                vote.GroupPurchase?.SellerCountryCode,
                vote.GroupPurchase?.ShipFromCountryCode,
                vote.GroupPurchase?.DeliveryCountryCode,
                vote.GroupPurchase?.CustomsClearanceStatusCode,
                vote.Votes.Sum(x => x.RequestedQuantity),
                vote.GroupPurchase?.QuantityUnit,
                vote.GroupPurchase?.OperatingMarketCountryCode);
    }

    public async Task 원장연결Async(
        Guid campaignId,
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        var vote = await store.GetAsync(campaignId, cancellationToken)
            ?? throw new InvalidOperationException("공동구매 캠페인을 찾을 수 없습니다.");
        if (string.Equals(vote.CommunityLedgerId, ledgerId, StringComparison.Ordinal))
        {
            return;
        }

        var expectedRevision = vote.Revision;
        vote.CommunityLedgerId = ledgerId;
        vote.Revision++;
        if (!await store.ReplaceAsync(vote, expectedRevision, cancellationToken))
        {
            var latest = await store.GetAsync(campaignId, cancellationToken);
            if (!string.Equals(latest?.CommunityLedgerId, ledgerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("공동구매 캠페인과 원장을 연결하는 동안 다른 변경이 발생했습니다.");
            }
        }
    }
}

public sealed class 공동구매원장절차Service : I공동구매원장절차Service
{
    private readonly I공동구매원장캠페인Store _campaignStore;
    private readonly I커뮤니티원장저장소 _ledgerStore;

    public 공동구매원장절차Service(
        I공동구매원장캠페인Store campaignStore,
        I커뮤니티원장저장소 ledgerStore)
    {
        _campaignStore = campaignStore;
        _ledgerStore = ledgerStore;
    }

    public async Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var ensured = await 원장확보Async(campaignId, cancellationToken);
        if (ensured is null)
        {
            return null;
        }

        var (vote, ledger) = ensured.Value;
        var inferredStage = 진행단계추론(vote);
        if (CommunityGroupPurchaseLedgerStageCodes.OrderOf(inferredStage)
            > CommunityGroupPurchaseLedgerStageCodes.OrderOf(ledger.현재단계Key))
        {
            ledger = await 단계저장Async(
                    ledger,
                    inferredStage,
                    "공동구매 서버 상태에서 절차 단계를 복원했습니다.",
                    "system:group-purchase-reconciliation",
                    expectedRevision: ledger.Revision,
                    cancellationToken)
                ?? ledger;
        }

        return ToResponse(vote.CampaignId, ledger);
    }

    public async Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
        Guid campaignId,
        CommunityGroupPurchaseLedgerProgressRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CommunityGroupPurchaseLedgerStageCodes.IsSupported(request.StageCode))
        {
            throw new InvalidOperationException($"지원하지 않는 공동구매 절차 단계입니다: {request.StageCode}");
        }

        var ensured = await 원장확보Async(campaignId, cancellationToken);
        if (ensured is null)
        {
            return null;
        }

        var (vote, ledger) = ensured.Value;
        var targetOrder = CommunityGroupPurchaseLedgerStageCodes.OrderOf(request.StageCode);
        var currentOrder = CommunityGroupPurchaseLedgerStageCodes.OrderOf(ledger.현재단계Key);
        if (targetOrder < currentOrder)
        {
            throw new InvalidOperationException(
                $"공동구매 원장 절차는 이전 단계로 되돌릴 수 없습니다: {ledger.현재단계Key} -> {request.StageCode}");
        }

        if (targetOrder == currentOrder)
        {
            return ToResponse(vote.CampaignId, ledger);
        }

        ledger = await 단계저장Async(
                ledger,
                request.StageCode.Trim(),
                request.Memo,
                updatedBy,
                request.ExpectedRevision,
                cancellationToken)
            ?? throw new InvalidOperationException("공동구매 원장 절차 단계를 저장하지 못했습니다.");
        return ToResponse(vote.CampaignId, ledger);
    }

    private async Task<(공동구매원장캠페인Snapshot Vote, 커뮤니티원장Dto Ledger)?> 원장확보Async(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var vote = await _campaignStore.조회Async(campaignId, cancellationToken);
        if (vote is null || vote.VoteKind != CommunityVoteKindCodes.GroupPurchaseDemand)
        {
            return null;
        }

        var ledgerId = string.IsNullOrWhiteSpace(vote.CommunityLedgerId)
            ? 원장Id생성(campaignId)
            : vote.CommunityLedgerId.Trim();
        var ledger = await _ledgerStore.원장조회Async(ledgerId, cancellationToken);
        if (ledger is null)
        {
            var initialStage = 진행단계추론(vote);
            ledger = await _ledgerStore.원장저장Async(
                new 커뮤니티원장저장요청
                {
                    원장Id = ledgerId,
                    커뮤니티Id = string.IsNullOrWhiteSpace(vote.CommunityScope)
                        ? "platform"
                        : vote.CommunityScope,
                    원장템플릿Key = CommunityLedgerTemplateKeys.GroupPurchase,
                    제목 = vote.Title,
                    원함 = vote.Description,
                    상태 = 커뮤니티원장상태.진행중,
                    현재단계Key = initialStage,
                    대상OsCode = CommunityLedgerOperatingSystemCodes.CommunityTrust,
                    대상OsName = "커뮤니티 신뢰 OS",
                    생성자표시명 = vote.CreatedByDisplayName,
                    외부참조 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["GroupPurchaseCampaignId"] = vote.CampaignId.ToString("D"),
                        ["CommunityVoteId"] = vote.CampaignId.ToString("D"),
                        ["SourcePostId"] = vote.SourcePostId?.ToString() ?? string.Empty
                    },
                    확장속성 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["WorkflowVersion"] = "1.0",
                        ["StageCatalog"] = string.Join(",", CommunityGroupPurchaseLedgerStageCodes.Ordered),
                        ["AutoLinked"] = bool.TrueString
                    }
                },
                "system:group-purchase-ledger",
                cancellationToken);
        }

        await _campaignStore.원장연결Async(campaignId, ledger.원장Id, cancellationToken);

        return (vote, ledger);
    }

    private async Task<커뮤니티원장Dto?> 단계저장Async(
        커뮤니티원장Dto ledger,
        string stageCode,
        string? memo,
        string updatedBy,
        long? expectedRevision,
        CancellationToken cancellationToken)
        => await _ledgerStore.원장상태변경Async(
            new 커뮤니티원장상태변경요청
            {
                원장Id = ledger.원장Id,
                기대Revision = expectedRevision ?? ledger.Revision,
                상태 = 커뮤니티원장상태.진행중,
                이전상태 = ledger.상태,
                현재단계Key = stageCode,
                메모 = string.IsNullOrWhiteSpace(memo)
                    ? $"공동구매 절차가 {stageCode} 단계로 진행되었습니다."
                    : memo.Trim()
            },
            string.IsNullOrWhiteSpace(updatedBy) ? "system:group-purchase" : updatedBy.Trim(),
            cancellationToken);

    private static string 진행단계추론(공동구매원장캠페인Snapshot vote)
    {
        var documentStatus = vote.ResolutionStatus;
        if (documentStatus == CommunityVoteResolutionStatusCodes.Signed)
        {
            return CommunityGroupPurchaseLedgerStageCodes.FulfillmentPlan;
        }

        if (documentStatus is CommunityVoteResolutionStatusCodes.ReadyToSign
            or CommunityVoteResolutionStatusCodes.PartiallySigned)
        {
            return CommunityGroupPurchaseLedgerStageCodes.Signature;
        }

        if (vote.ResolutionStatus is not null)
        {
            return CommunityGroupPurchaseLedgerStageCodes.Resolution;
        }

        return vote.Status == CommunityVoteStatusCodes.Open
            ? CommunityGroupPurchaseLedgerStageCodes.Recruitment
            : CommunityGroupPurchaseLedgerStageCodes.Counterparty;
    }

    private static CommunityGroupPurchaseLedgerProgressResponse ToResponse(
        Guid campaignId,
        커뮤니티원장Dto ledger)
        => new()
        {
            GroupPurchaseCampaignId = campaignId,
            CommunityLedgerId = ledger.원장Id,
            Revision = ledger.Revision,
            LedgerStatus = ledger.상태,
            CurrentStageCode = CommunityGroupPurchaseLedgerStageCodes.IsSupported(ledger.현재단계Key)
                ? ledger.현재단계Key!
                : CommunityGroupPurchaseLedgerStageCodes.Proposal,
            AutomaticallyLinked = ledger.확장속성.TryGetValue("AutoLinked", out var linked)
                                  && bool.TryParse(linked, out var parsed)
                                  && parsed,
            History = ledger.상태이력
                .Where(item => CommunityGroupPurchaseLedgerStageCodes.IsSupported(item.현재단계Key))
                .Select(item => new CommunityGroupPurchaseLedgerStageHistoryItem
                {
                    StageCode = item.현재단계Key!,
                    LedgerStatus = item.상태,
                    Memo = item.메모 ?? string.Empty,
                    ChangedBy = item.변경자,
                    ChangedAtUtc = item.변경시각Utc
                })
                .ToArray()
        };

    public static string 원장Id생성(Guid campaignId)
        => $"group-purchase-{campaignId:N}";
}

internal sealed class 빈공동구매원장절차Service : I공동구매원장절차Service
{
    public static 빈공동구매원장절차Service Instance { get; } = new();

    public Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(null);

    public Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
        Guid campaignId,
        CommunityGroupPurchaseLedgerProgressRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default)
        => Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(null);
}
