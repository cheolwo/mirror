using System.Collections.Concurrent;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.Community;

namespace Hongdal.Services.Orderer;

public interface ICommunityProducerMemberDirectory
{
    Task<CommunityProducerMemberDirectorySearchResult> SearchConsentedProducersAsync(
        string? search,
        string? regionCode,
        string? product,
        CancellationToken cancellationToken = default);
}

public sealed record CommunityProducerMemberDirectorySearchResult(
    bool IsConnected,
    IReadOnlyList<DomesticProducerCandidateResponse> Items);

public sealed class UnconnectedCommunityProducerMemberDirectory : ICommunityProducerMemberDirectory
{
    public Task<CommunityProducerMemberDirectorySearchResult> SearchConsentedProducersAsync(
        string? search,
        string? regionCode,
        string? product,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new CommunityProducerMemberDirectorySearchResult(false, []));
}

public interface ICommunityGroupPurchaseRepresentativeDirectory
{
    Task<CommunityGroupPurchaseRepresentativeDirectorySearchResult> SearchConsentedRepresentativesAsync(
        string? search,
        string? operatingAreaCode,
        string? product,
        CancellationToken cancellationToken = default);
}

public sealed record CommunityGroupPurchaseRepresentativeDirectorySearchResult(
    bool IsConnected,
    IReadOnlyList<DomesticGroupPurchaseRepresentativeCandidateResponse> Items);

public sealed class UnconnectedCommunityGroupPurchaseRepresentativeDirectory
    : ICommunityGroupPurchaseRepresentativeDirectory
{
    public Task<CommunityGroupPurchaseRepresentativeDirectorySearchResult> SearchConsentedRepresentativesAsync(
        string? search,
        string? operatingAreaCode,
        string? product,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new CommunityGroupPurchaseRepresentativeDirectorySearchResult(false, []));
}

public interface IDomesticProducerContactRequestDraftStore
{
    Task<DomesticProducerContactRequestDraftResponse> SaveAsync(
        DomesticProducerContactRequestDraftResponse draft,
        CancellationToken cancellationToken = default);

    Task<DomesticProducerContactRequestDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryDomesticProducerContactRequestDraftStore : IDomesticProducerContactRequestDraftStore
{
    private readonly ConcurrentDictionary<Guid, DomesticProducerContactRequestDraftResponse> drafts = new();

    public Task<DomesticProducerContactRequestDraftResponse> SaveAsync(
        DomesticProducerContactRequestDraftResponse draft,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        drafts[draft.DraftId] = draft;
        return Task.FromResult(draft);
    }

    public Task<DomesticProducerContactRequestDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        drafts.TryGetValue(draftId, out var draft);
        return Task.FromResult(draft);
    }
}

public interface IDomesticProducerSupplyOfferDraftStore
{
    Task<DomesticProducerSupplyOfferDraftResponse> SaveAsync(
        DomesticProducerSupplyOfferDraftResponse draft,
        CancellationToken cancellationToken = default);

    Task<DomesticProducerSupplyOfferDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryDomesticProducerSupplyOfferDraftStore : IDomesticProducerSupplyOfferDraftStore
{
    private readonly ConcurrentDictionary<Guid, DomesticProducerSupplyOfferDraftResponse> drafts = new();

    public Task<DomesticProducerSupplyOfferDraftResponse> SaveAsync(
        DomesticProducerSupplyOfferDraftResponse draft,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        drafts[draft.DraftId] = draft;
        return Task.FromResult(draft);
    }

    public Task<DomesticProducerSupplyOfferDraftResponse?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        drafts.TryGetValue(draftId, out var draft);
        return Task.FromResult(draft);
    }
}

public interface IDomesticGroupPurchaseProducerConnectionService
{
    Task<DomesticProducerCandidateQueryResponse> SearchCandidatesAsync(
        string? search,
        string? regionCode,
        string? product,
        CancellationToken cancellationToken = default);

    Task<DomesticProducerContactRequestDraftResponse> CreateDraftAsync(
        string requestedByUserId,
        DomesticProducerContactRequestDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticProducerContactRequestDraftResponse?> GetDraftAsync(
        string requestedByUserId,
        Guid draftId,
        CancellationToken cancellationToken = default);

    Task<DomesticGroupPurchaseRepresentativeCandidateQueryResponse> SearchRepresentativesAsync(
        string? search,
        string? operatingAreaCode,
        string? product,
        CancellationToken cancellationToken = default);

    Task<DomesticProducerSupplyOfferDraftResponse> CreateSupplyOfferDraftAsync(
        string offeredByUserId,
        DomesticProducerSupplyOfferDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<DomesticProducerSupplyOfferDraftResponse?> GetSupplyOfferDraftAsync(
        string offeredByUserId,
        Guid draftId,
        CancellationToken cancellationToken = default);

    DomesticGroupPurchaseSupplyCompatibilityPreviewResponse PreviewCompatibility(
        DomesticGroupPurchaseSupplyCompatibilityPreviewRequest request);
}

public sealed class DomesticGroupPurchaseProducerConnectionService : IDomesticGroupPurchaseProducerConnectionService
{
    private readonly ICommunityProducerMemberDirectory producerDirectory;
    private readonly ICommunityGroupPurchaseRepresentativeDirectory representativeDirectory;
    private readonly IDomesticProducerContactRequestDraftStore draftStore;
    private readonly IDomesticProducerSupplyOfferDraftStore supplyOfferDraftStore;
    private readonly I커뮤니티원장저장소? ledgerStore;
    private readonly I공동구매원장절차Service? ledgerWorkflow;

    public DomesticGroupPurchaseProducerConnectionService(
        ICommunityProducerMemberDirectory producerDirectory,
        ICommunityGroupPurchaseRepresentativeDirectory representativeDirectory,
        IDomesticProducerContactRequestDraftStore draftStore,
        IDomesticProducerSupplyOfferDraftStore supplyOfferDraftStore)
        : this(
            producerDirectory,
            representativeDirectory,
            draftStore,
            supplyOfferDraftStore,
            null,
            null)
    {
    }

    public DomesticGroupPurchaseProducerConnectionService(
        ICommunityProducerMemberDirectory producerDirectory,
        ICommunityGroupPurchaseRepresentativeDirectory representativeDirectory,
        IDomesticProducerContactRequestDraftStore draftStore,
        IDomesticProducerSupplyOfferDraftStore supplyOfferDraftStore,
        I커뮤니티원장저장소? ledgerStore,
        I공동구매원장절차Service? ledgerWorkflow)
    {
        this.producerDirectory = producerDirectory;
        this.representativeDirectory = representativeDirectory;
        this.draftStore = draftStore;
        this.supplyOfferDraftStore = supplyOfferDraftStore;
        this.ledgerStore = ledgerStore;
        this.ledgerWorkflow = ledgerWorkflow;
    }

    public async Task<DomesticProducerCandidateQueryResponse> SearchCandidatesAsync(
        string? search,
        string? regionCode,
        string? product,
        CancellationToken cancellationToken = default)
    {
        var directoryResult = await producerDirectory.SearchConsentedProducersAsync(
            search,
            regionCode,
            product,
            cancellationToken);

        return new DomesticProducerCandidateQueryResponse
        {
            IntegrationStatusCode = directoryResult.IsConnected
                ? DomesticProducerDirectoryIntegrationStatuses.Connected
                : DomesticProducerDirectoryIntegrationStatuses.NotConnected,
            IntegrationMessage = directoryResult.IsConnected
                ? "공개 및 연락 요청에 동의한 회원 생산자만 표시합니다."
                : "회원 생산자 디렉터리 연결 전입니다. 동의 정책과 데이터 어댑터를 연결하면 후보가 표시됩니다.",
            ContactDetailsDisclosed = false,
            Items = directoryResult.Items
        };
    }

    public async Task<DomesticProducerContactRequestDraftResponse> CreateDraftAsync(
        string requestedByUserId,
        DomesticProducerContactRequestDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedByUserId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.GroupPurchaseCampaignId == Guid.Empty)
        {
            throw new ArgumentException("공동구매 캠페인 식별자가 필요합니다.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProducerCandidateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequiredPackagingFormCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PackagingUnitSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.QualityGradeSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.QuantityUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        ValidatePackagingFormCode(request.RequiredPackagingFormCode);
        if (request.RequestedQuantity <= 0 || request.MaximumAbsorptionQuantity <= 0)
        {
            throw new ArgumentException("요청 물량과 공동구매 측 최대 인수 물량은 0보다 커야 합니다.", nameof(request));
        }

        if (request.MaximumAbsorptionQuantity < request.RequestedQuantity)
        {
            throw new ArgumentException("공동구매 측 최대 인수 물량은 확정 요청 물량보다 작을 수 없습니다.", nameof(request));
        }

        var draft = new DomesticProducerContactRequestDraftResponse
        {
            DraftId = Guid.NewGuid(),
            GroupPurchaseCampaignId = request.GroupPurchaseCampaignId,
            RequestedByUserId = requestedByUserId.Trim(),
            ProducerCandidateKey = request.ProducerCandidateKey.Trim(),
            ProducerMaskedDisplayName = request.ProducerMaskedDisplayName.Trim(),
            CampaignTitle = request.CampaignTitle.Trim(),
            ProductSummary = request.ProductSummary.Trim(),
            RequestedQuantitySummary = request.RequestedQuantitySummary.Trim(),
            RequiredPackagingFormCode = request.RequiredPackagingFormCode.Trim(),
            PackagingUnitSummary = request.PackagingUnitSummary.Trim(),
            QualityGradeSummary = request.QualityGradeSummary.Trim(),
            RequestedQuantity = request.RequestedQuantity,
            MaximumAbsorptionQuantity = request.MaximumAbsorptionQuantity,
            QuantityUnit = request.QuantityUnit.Trim(),
            CanReceiveSplitShipments = request.CanReceiveSplitShipments,
            Message = request.Message.Trim(),
            StatusCode = DomesticProducerContactRequestStatuses.Draft,
            ContactDetailsDisclosed = false,
            IsDurablyPersisted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            GuidanceMessage = "연락 요청 초안만 서버 메모리에 보관했습니다. 실제 발송과 연락처 공개는 상대 수락 및 영구 저장소 연동 후 활성화됩니다."
        };

        var saved = await draftStore.SaveAsync(draft, cancellationToken);
        await 공동구매원장블록기록Async(
            saved.GroupPurchaseCampaignId,
            new 커뮤니티원장블록Dto
            {
                BlockId = $"producer-contact-{saved.DraftId:N}",
                BlockType = CommunityLedgerBlockTypes.Generic,
                Title = "생산자 연락 요청 초안",
                State = saved.StatusCode,
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DraftId"] = saved.DraftId.ToString("D"),
                    ["RequestedByUserId"] = saved.RequestedByUserId,
                    ["ProducerCandidateKey"] = saved.ProducerCandidateKey,
                    ["ProductSummary"] = saved.ProductSummary,
                    ["RequestedQuantity"] = saved.RequestedQuantity.ToString(),
                    ["MaximumAbsorptionQuantity"] = saved.MaximumAbsorptionQuantity.ToString(),
                    ["QuantityUnit"] = saved.QuantityUnit,
                    ["PackagingFormCode"] = saved.RequiredPackagingFormCode,
                    ["PackagingUnitSummary"] = saved.PackagingUnitSummary,
                    ["QualityGradeSummary"] = saved.QualityGradeSummary,
                    ["Message"] = saved.Message
                }
            },
            CommunityGroupPurchaseLedgerStageCodes.Counterparty,
            "생산자 연락 요청 초안을 공동구매 원장에 기록했습니다.",
            saved.RequestedByUserId,
            cancellationToken);
        return saved;
    }

    public async Task<DomesticProducerContactRequestDraftResponse?> GetDraftAsync(
        string requestedByUserId,
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedByUserId);
        if (draftId == Guid.Empty)
        {
            return null;
        }

        var draft = await draftStore.GetAsync(draftId, cancellationToken);
        return draft is not null
               && string.Equals(draft.RequestedByUserId, requestedByUserId.Trim(), StringComparison.Ordinal)
            ? draft
            : null;
    }

    public async Task<DomesticGroupPurchaseRepresentativeCandidateQueryResponse> SearchRepresentativesAsync(
        string? search,
        string? operatingAreaCode,
        string? product,
        CancellationToken cancellationToken = default)
    {
        var directoryResult = await representativeDirectory.SearchConsentedRepresentativesAsync(
            search,
            operatingAreaCode,
            product,
            cancellationToken);

        return new DomesticGroupPurchaseRepresentativeCandidateQueryResponse
        {
            IntegrationStatusCode = directoryResult.IsConnected
                ? DomesticProducerDirectoryIntegrationStatuses.Connected
                : DomesticProducerDirectoryIntegrationStatuses.NotConnected,
            IntegrationMessage = directoryResult.IsConnected
                ? "대표 역할과 제안 수신에 동의한 공동구매 운영자만 표시합니다."
                : "공동구매 대표 디렉터리 연결 전입니다. 대표 권한과 제안 수신 동의 어댑터를 연결하면 후보가 표시됩니다.",
            ContactDetailsDisclosed = false,
            Items = directoryResult.Items
        };
    }

    public async Task<DomesticProducerSupplyOfferDraftResponse> CreateSupplyOfferDraftAsync(
        string offeredByUserId,
        DomesticProducerSupplyOfferDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(offeredByUserId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.GroupPurchaseCampaignId == Guid.Empty)
        {
            throw new ArgumentException("공동구매 캠페인 식별자가 필요합니다.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.RepresentativeCandidateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProducerMaskedDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProductSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AvailableQuantitySummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.QualityDisclosure);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);
        ValidateOfferReason(request.OfferReasonCode);

        if (request.AvailableQuantity <= 0 || request.MinimumTakeQuantity <= 0)
        {
            throw new ArgumentException("공급 가능 물량과 최소 인수 물량은 0보다 커야 합니다.", nameof(request));
        }

        if (request.MinimumTakeQuantity > request.AvailableQuantity)
        {
            throw new ArgumentException("최소 인수 물량은 전체 공급 가능 물량보다 클 수 없습니다.", nameof(request));
        }

        if (request.SupportedPackagingFormCodes is null or { Count: 0 })
        {
            throw new ArgumentException("생산자가 지원할 수 있는 포장 형태가 하나 이상 필요합니다.", nameof(request));
        }

        foreach (var packagingFormCode in request.SupportedPackagingFormCodes)
        {
            ValidatePackagingFormCode(packagingFormCode);
        }

        if (!request.FoodSafetyConfirmed)
        {
            throw new ArgumentException("농산물 안전 확인 없이는 공급 제안 초안을 만들 수 없습니다.", nameof(request));
        }

        var draft = new DomesticProducerSupplyOfferDraftResponse
        {
            DraftId = Guid.NewGuid(),
            GroupPurchaseCampaignId = request.GroupPurchaseCampaignId,
            OfferedByUserId = offeredByUserId.Trim(),
            RepresentativeCandidateKey = request.RepresentativeCandidateKey.Trim(),
            RepresentativeMaskedDisplayName = request.RepresentativeMaskedDisplayName.Trim(),
            ProducerMaskedDisplayName = request.ProducerMaskedDisplayName.Trim(),
            CampaignTitle = request.CampaignTitle.Trim(),
            ProductSummary = request.ProductSummary.Trim(),
            AvailableQuantitySummary = request.AvailableQuantitySummary.Trim(),
            SupportedPackagingFormCodes = request.SupportedPackagingFormCodes.ToArray(),
            AvailableQuantity = request.AvailableQuantity,
            MinimumTakeQuantity = request.MinimumTakeQuantity,
            QuantityUnit = request.QuantityUnit.Trim(),
            CanSplitShipments = request.CanSplitShipments,
            ExpectedPriceSummary = request.ExpectedPriceSummary.Trim(),
            SupplyDeadlineSummary = request.SupplyDeadlineSummary.Trim(),
            OfferReasonCode = request.OfferReasonCode.Trim(),
            QualityDisclosure = request.QualityDisclosure.Trim(),
            FoodSafetyConfirmed = true,
            Message = request.Message.Trim(),
            StatusCode = DomesticProducerContactRequestStatuses.Draft,
            ContactDetailsDisclosed = false,
            IsDurablyPersisted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            GuidanceMessage = "생산자 공급 제안 초안만 서버 메모리에 보관했습니다. 대표가 수락하기 전에는 연락처가 공개되지 않으며 실제 거래도 확정되지 않습니다."
        };

        var saved = await supplyOfferDraftStore.SaveAsync(draft, cancellationToken);
        await 공동구매원장블록기록Async(
            saved.GroupPurchaseCampaignId,
            new 커뮤니티원장블록Dto
            {
                BlockId = $"producer-supply-offer-{saved.DraftId:N}",
                BlockType = CommunityLedgerBlockTypes.Generic,
                Title = "생산자 공급 제안 초안",
                State = saved.StatusCode,
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DraftId"] = saved.DraftId.ToString("D"),
                    ["OfferedByUserId"] = saved.OfferedByUserId,
                    ["RepresentativeCandidateKey"] = saved.RepresentativeCandidateKey,
                    ["ProductSummary"] = saved.ProductSummary,
                    ["AvailableQuantity"] = saved.AvailableQuantity.ToString(),
                    ["MinimumTakeQuantity"] = saved.MinimumTakeQuantity.ToString(),
                    ["QuantityUnit"] = saved.QuantityUnit,
                    ["PackagingFormCodes"] = string.Join(",", saved.SupportedPackagingFormCodes),
                    ["ExpectedPriceSummary"] = saved.ExpectedPriceSummary,
                    ["SupplyDeadlineSummary"] = saved.SupplyDeadlineSummary,
                    ["OfferReasonCode"] = saved.OfferReasonCode,
                    ["QualityDisclosure"] = saved.QualityDisclosure,
                    ["FoodSafetyConfirmed"] = saved.FoodSafetyConfirmed.ToString(),
                    ["Message"] = saved.Message
                }
            },
            CommunityGroupPurchaseLedgerStageCodes.SupplyNegotiation,
            "생산자 공급 제안 초안을 공동구매 원장에 기록했습니다.",
            saved.OfferedByUserId,
            cancellationToken);
        return saved;
    }

    public async Task<DomesticProducerSupplyOfferDraftResponse?> GetSupplyOfferDraftAsync(
        string offeredByUserId,
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(offeredByUserId);
        if (draftId == Guid.Empty)
        {
            return null;
        }

        var draft = await supplyOfferDraftStore.GetAsync(draftId, cancellationToken);
        return draft is not null
               && string.Equals(draft.OfferedByUserId, offeredByUserId.Trim(), StringComparison.Ordinal)
            ? draft
            : null;
    }

    public DomesticGroupPurchaseSupplyCompatibilityPreviewResponse PreviewCompatibility(
        DomesticGroupPurchaseSupplyCompatibilityPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var packagingReady = !string.IsNullOrWhiteSpace(request.BuyerRequiredPackagingFormCode)
            && request.ProducerSupportedPackagingFormCodes is not null
            && request.ProducerSupportedPackagingFormCodes.Any(code =>
                string.Equals(code, request.BuyerRequiredPackagingFormCode, StringComparison.OrdinalIgnoreCase));
        var producerQuantityReady = request.BuyerRequestedQuantity > 0
            && request.ProducerAvailableQuantity >= request.BuyerRequestedQuantity;
        var buyerMinimumReady = request.ProducerMinimumTakeQuantity > 0
            && request.BuyerMaximumAbsorptionQuantity >= request.ProducerMinimumTakeQuantity;
        var buyerFullOfferReady = request.ProducerAvailableQuantity > 0
            && request.BuyerMaximumAbsorptionQuantity >= request.ProducerAvailableQuantity;
        var splitCanResolve = !buyerFullOfferReady
            && buyerMinimumReady
            && request.BuyerCanReceiveSplitShipments
            && request.ProducerCanSplitShipments;

        var unresolved = new List<string>();
        if (!packagingReady)
        {
            unresolved.Add("생산자가 구매 대표의 필수 포장 형태를 지원하는지 확인해야 합니다.");
        }

        if (!producerQuantityReady)
        {
            unresolved.Add("생산 가능 물량이 공동구매 요청 수량에 미치지 않습니다.");
        }

        if (!buyerMinimumReady)
        {
            unresolved.Add("공동구매 측의 최대 인수 능력이 생산자의 최소 인수 조건에 미치지 않습니다.");
        }

        if (!buyerFullOfferReady && !splitCanResolve)
        {
            unresolved.Add("공동구매 측이 전체 제안 물량을 소화하거나 분할 인수할 조건이 필요합니다.");
        }

        var mutuallyFeasible = packagingReady
            && producerQuantityReady
            && buyerMinimumReady
            && (buyerFullOfferReady || splitCanResolve);

        return new DomesticGroupPurchaseSupplyCompatibilityPreviewResponse
        {
            ProducerCanMeetPackaging = packagingReady,
            ProducerCanMeetRequestedQuantity = producerQuantityReady,
            BuyerMeetsMinimumTakeQuantity = buyerMinimumReady,
            BuyerCanAbsorbFullOffer = buyerFullOfferReady,
            SplitShipmentCanResolveVolumeGap = splitCanResolve,
            IsMutuallyFeasible = mutuallyFeasible,
            UnresolvedConditions = unresolved,
            Summary = mutuallyFeasible
                ? "포장 규격과 양측 물량 조건이 맞아 세부 협의를 시작할 수 있습니다."
                : "아직 맞지 않는 포장 또는 물량 조건이 있어 조정이 필요합니다."
        };
    }

    private async Task 공동구매원장블록기록Async(
        Guid campaignId,
        커뮤니티원장블록Dto block,
        string stageCode,
        string stageMemo,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        if (ledgerStore is null || ledgerWorkflow is null)
        {
            return;
        }

        var progress = await ledgerWorkflow.조회Async(campaignId, cancellationToken)
            ?? throw new InvalidOperationException("생산자 연결 기록을 저장할 공동구매 원장을 찾을 수 없습니다.");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var ledger = await ledgerStore.원장조회Async(progress.CommunityLedgerId, cancellationToken)
                ?? throw new InvalidOperationException("생산자 연결 기록을 저장할 공동구매 원장 상세를 찾을 수 없습니다.");
            var blocks = ledger.블록목록
                .Where(x => !string.Equals(x.BlockId, block.BlockId, StringComparison.OrdinalIgnoreCase))
                .Append(block)
                .ToArray();
            try
            {
                await ledgerStore.원장저장Async(
                    new 커뮤니티원장저장요청
                    {
                        원장Id = ledger.원장Id,
                        기대Revision = ledger.Revision,
                        커뮤니티Id = ledger.커뮤니티Id,
                        원장템플릿Key = ledger.원장템플릿Key,
                        제목 = ledger.제목,
                        원함 = ledger.원함,
                        상태 = ledger.상태,
                        현재단계Key = ledger.현재단계Key,
                        대상OsCode = ledger.대상OsCode,
                        대상OsName = ledger.대상OsName,
                        생성자UserId = ledger.생성자UserId,
                        생성자표시명 = ledger.생성자표시명,
                        블록목록 = blocks,
                        참여자목록 = ledger.참여자목록,
                        포함원장목록 = ledger.포함원장목록,
                        다이어그램스냅샷 = ledger.다이어그램스냅샷,
                        외부참조 = ledger.외부참조,
                        확장속성 = ledger.확장속성
                    },
                    updatedBy,
                    cancellationToken);
                break;
            }
            catch (InvalidOperationException) when (attempt == 0)
            {
                // 동시에 다른 원장 블록이 추가된 경우 최신 Revision으로 한 번 병합 재시도합니다.
            }
        }

        var latestProgress = await ledgerWorkflow.조회Async(campaignId, cancellationToken) ?? progress;
        if (CommunityGroupPurchaseLedgerStageCodes.OrderOf(stageCode)
            > CommunityGroupPurchaseLedgerStageCodes.OrderOf(latestProgress.CurrentStageCode))
        {
            await ledgerWorkflow.진행Async(
                campaignId,
                new CommunityGroupPurchaseLedgerProgressRequest
                {
                    StageCode = stageCode,
                    Memo = stageMemo,
                    ExpectedRevision = latestProgress.Revision
                },
                updatedBy,
                cancellationToken);
        }
    }

    private static void ValidateOfferReason(string reasonCode)
    {
        if (reasonCode is not (
            DomesticProducerSupplyOfferReasonCodes.Overproduction
            or DomesticProducerSupplyOfferReasonCodes.OffGrade
            or DomesticProducerSupplyOfferReasonCodes.ShippingDeadline
            or DomesticProducerSupplyOfferReasonCodes.SalesChannelGap
            or DomesticProducerSupplyOfferReasonCodes.Other))
        {
            throw new ArgumentException("지원하지 않는 공급 제안 사유입니다.", nameof(reasonCode));
        }
    }

    private static void ValidatePackagingFormCode(string packagingFormCode)
    {
        if (packagingFormCode is not (
            DomesticProducePackagingFormCodes.CorrugatedBox
            or DomesticProducePackagingFormCodes.ReusableCrate
            or DomesticProducePackagingFormCodes.Pallet
            or DomesticProducePackagingFormCodes.Bulk
            or DomesticProducePackagingFormCodes.Other))
        {
            throw new ArgumentException("지원하지 않는 출하 포장 형태입니다.", nameof(packagingFormCode));
        }
    }
}
