using System.Security.Cryptography;
using System.Text;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;

namespace Hongdal.Services.Community;

public partial class CommunityVoteService : ICommunityVoteService
{
    private readonly ICommunityVoteStore _store;
    private readonly ICommunityGroupPurchaseDemandOutboxProcessor _demandOutboxProcessor;
    private readonly I공동구매원장절차Service _ledgerWorkflow;
    private readonly string _operatingMarketCountryCode;

    internal CommunityVoteService(
        ICommunityVoteStore store,
        ICommunityGroupPurchaseDemandOutboxProcessor demandOutboxProcessor,
        I공동구매원장절차Service? ledgerWorkflow = null,
        string? operatingMarketCountryCode = null)
    {
        _store = store;
        _demandOutboxProcessor = demandOutboxProcessor;
        _ledgerWorkflow = ledgerWorkflow ?? 빈공동구매원장절차Service.Instance;
        _operatingMarketCountryCode = CommunityGroupPurchaseTradeRoutePolicy
            .NormalizeOperatingMarketCountryCode(operatingMarketCountryCode);
    }

    public async Task<CommunityVoteResponse> CreateAsync(CommunityVoteCreateRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("투표 제목이 필요합니다.");
        }

        if (request.Options.Count > 0 && request.StructuredOptions.Count > 0)
        {
            throw new InvalidOperationException("문자열 선택지와 구조화 선택지는 동시에 지정할 수 없습니다.");
        }

        var options = request.StructuredOptions.Count > 0
            ? request.StructuredOptions
                .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                .Select((option, index) => new CommunityVoteOptionRecord
                {
                    OptionId = $"option-{index + 1}",
                    Text = option.Text.Trim(),
                    ProductKey = NormalizeOptional(option.ProductKey) ?? string.Empty,
                    HsCode = NormalizeOptional(option.HsCode) ?? string.Empty,
                    TemperatureCode = NormalizeOptional(option.TemperatureCode) ?? string.Empty,
                    LogisticsMode = NormalizeOptional(option.LogisticsMode) ?? string.Empty,
                    QuantityUnit = NormalizeOptional(option.QuantityUnit) ?? string.Empty
                })
                .ToArray()
            : request.Options
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select((text, index) => new CommunityVoteOptionRecord
                {
                    OptionId = $"option-{index + 1}",
                    Text = text.Trim()
                })
                .ToArray();
        if (options.Length < 2)
        {
            throw new InvalidOperationException("투표 선택지는 2개 이상이어야 합니다.");
        }

        var voteKind = Normalize(request.VoteKind, CommunityVoteKindCodes.General);
        if (voteKind is not CommunityVoteKindCodes.General
            and not CommunityVoteKindCodes.CollectiveActionInterest
            and not CommunityVoteKindCodes.GroupPurchaseDemand)
        {
            throw new InvalidOperationException("지원하지 않는 투표 유형입니다.");
        }

        if (voteKind == CommunityVoteKindCodes.CollectiveActionInterest)
        {
            if (request.SourcePostId is null or <= 0)
            {
                throw new InvalidOperationException("참여 관심 투표는 원본 커뮤니티 게시글이 필요합니다.");
            }

            if (!request.AllowMultipleSelection)
            {
                throw new InvalidOperationException("참여 관심 투표는 여러 역할을 함께 선택할 수 있어야 합니다.");
            }

            if (request.ResolutionDocumentEnabled
                || request.SignatureRequired
                || !string.IsNullOrWhiteSpace(request.CommunityLedgerId))
            {
                throw new InvalidOperationException("참여 관심 투표에서는 원장, 결의문 또는 서명을 시작할 수 없습니다.");
            }
        }

        if (request.ClosesAtUtc is not null && request.ClosesAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("투표 마감 시각은 현재보다 이후여야 합니다.");
        }

        var groupPurchase = CreateGroupPurchaseSettings(
            request,
            voteKind,
            _operatingMarketCountryCode);

        var voteId = Guid.NewGuid();
        var vote = new CommunityVoteRecord
        {
            Id = voteId,
            AppKey = Normalize(request.AppKey, "platform"),
            CommunityScope = Normalize(request.CommunityScope, "platform"),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            VoteKind = voteKind,
            SourcePostId = request.SourcePostId,
            CommunityLedgerId = NormalizeOptional(request.CommunityLedgerId)
                ?? (groupPurchase is null ? null : 공동구매원장절차Service.원장Id생성(voteId)),
            AllowMultipleSelection = request.AllowMultipleSelection,
            ResolutionDocumentEnabled = request.ResolutionDocumentEnabled,
            SignatureRequired = request.SignatureRequired,
            CreatedByDisplayName = Normalize(request.CreatedByDisplayName, "익명 참여자"),
            CreatedAtUtc = DateTime.UtcNow,
            ClosesAtUtc = request.ClosesAtUtc,
            Options = options,
            GroupPurchase = groupPurchase
        };

        await _store.AddAsync(vote, cancellationToken);
        if (groupPurchase is not null)
        {
            await _ledgerWorkflow.조회Async(vote.Id, cancellationToken);
        }
        return ToResponse(vote);
    }

    public async Task<CommunityVoteListResponse> ListAsync(
        string? appKey,
        string? communityScope,
        string? hsCode,
        CancellationToken cancellationToken)
    {
        var normalizedHsCode = CommunityVoteHsCode.NormalizeOptional(hsCode);
        var items = await _store.ListAsync(appKey, communityScope, normalizedHsCode, cancellationToken);
        return new CommunityVoteListResponse
        {
            Items = items.Select(ToResponse).ToArray()
        };
    }

    public async Task<CommunityVoteResponse?> GetAsync(Guid voteId, CancellationToken cancellationToken)
    {
        var vote = await _store.GetAsync(voteId, cancellationToken);
        return vote is null ? null : ToResponse(vote);
    }

    public async Task<CommunityInterestVotePromotionSnapshot?> GetInterestPromotionSnapshotAsync(
        Guid voteId,
        long sourcePostId,
        CancellationToken cancellationToken)
    {
        var vote = await _store.GetAsync(voteId, cancellationToken);
        if (vote is null)
        {
            return null;
        }

        EnsureInterestVoteSource(vote, sourcePostId);
        return ToPromotionSnapshot(vote);
    }

    public async Task<CommunityInterestVotePromotionSnapshot?> AttachProvisionalLedgerAsync(
        Guid voteId,
        long sourcePostId,
        string communityLedgerId,
        int minimumParticipantCount,
        string promotedByDisplayName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityLedgerId);
        if (minimumParticipantCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumParticipantCount));
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var vote = await _store.GetAsync(voteId, cancellationToken);
            if (vote is null)
            {
                return null;
            }

            EnsureInterestVoteSource(vote, sourcePostId);
            if (vote.Votes.Count < minimumParticipantCount)
            {
                throw new InvalidOperationException(
                    $"가원장은 서로 다른 관심 참여자 {minimumParticipantCount}명 이상이 모인 뒤 만들 수 있습니다.");
            }

            var normalizedLedgerId = communityLedgerId.Trim();
            if (!string.IsNullOrWhiteSpace(vote.CommunityLedgerId))
            {
                if (!string.Equals(vote.CommunityLedgerId, normalizedLedgerId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("이 관심 모집은 이미 다른 가원장에 연결되어 있습니다.");
                }

                return ToPromotionSnapshot(vote);
            }

            vote.CommunityLedgerId = normalizedLedgerId;
            vote.Status = CommunityVoteStatusCodes.Closed;
            vote.ClosedAtUtc ??= DateTime.UtcNow;
            vote.ClosedByDisplayName = Normalize(promotedByDisplayName, "게시글 작성자");
            var expectedRevision = vote.Revision++;
            if (await _store.ReplaceAsync(vote, expectedRevision, cancellationToken))
            {
                return ToPromotionSnapshot(vote);
            }
        }

        throw new InvalidOperationException("다른 참여자가 관심 모집을 먼저 변경했습니다. 최신 상태를 확인한 뒤 다시 시도해 주세요.");
    }

    public async Task<CommunityVoteListResponse> ListBySourcePostAsync(
        long sourcePostId,
        CancellationToken cancellationToken)
    {
        if (sourcePostId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePostId));
        }

        var items = await _store.ListBySourcePostAsync(sourcePostId, cancellationToken);
        return new CommunityVoteListResponse
        {
            Items = items.Select(ToResponse).ToArray()
        };
    }

    public async Task<CommunityVoteResponse?> CastVoteAsync(Guid voteId, CommunityVoteCastRequest request, CancellationToken cancellationToken)
    {
        var vote = await _store.GetAsync(voteId, cancellationToken);
        if (vote is null)
        {
            return null;
        }

        EnsureOpen(vote);
        var selectedOptionIds = request.OptionIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (selectedOptionIds.Length == 0)
            {
                throw new InvalidOperationException("선택한 투표 항목이 없습니다.");
            }

            if (!vote.AllowMultipleSelection && selectedOptionIds.Length > 1)
            {
                throw new InvalidOperationException("이 투표는 하나의 항목만 선택할 수 있습니다.");
            }

            var validOptionIds = vote.Options.Select(x => x.OptionId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (selectedOptionIds.Any(x => !validOptionIds.Contains(x)))
            {
                throw new InvalidOperationException("존재하지 않는 투표 항목이 포함되어 있습니다.");
            }

        var authenticatedUserId = NormalizeOptional(request.AuthenticatedUserId);
        var voterIdentity = authenticatedUserId is null
            ? Normalize(request.VoterKey, request.VoterDisplayName)
            : $"authenticated-user:{authenticatedUserId}";
        var voterHash = Hash(voterIdentity);
        var groupPurchaseParticipation = ValidateGroupPurchaseParticipation(vote, request, voterHash);
        vote.Votes.RemoveAll(x => string.Equals(x.VoterHash, voterHash, StringComparison.Ordinal));
        vote.Votes.Add(new CommunityVoteCastRecord
        {
            VoterHash = voterHash,
            VoterUserId = authenticatedUserId,
            VoterDisplayName = Normalize(request.VoterDisplayName, "익명 참여자"),
            OptionIds = selectedOptionIds,
            RequestedQuantity = groupPurchaseParticipation.RequestedQuantity,
            ParticipationMethodCode = groupPurchaseParticipation.ParticipationMethodCode,
            PickupPointId = groupPurchaseParticipation.PickupPointId,
            AllowNearbyPickupPointFallback = request.AllowNearbyPickupPointFallback,
            VotedAtUtc = DateTime.UtcNow
        });

        var demandOutboxId = QueueGroupPurchaseDemand(
            vote,
            request,
            selectedOptionIds[0],
            voterHash,
            groupPurchaseParticipation);
        await SaveMutationAsync(vote, cancellationToken);
        if (demandOutboxId is not null)
        {
            await _demandOutboxProcessor.ProcessAsync(vote.Id, demandOutboxId, cancellationToken);
            vote = await _store.GetAsync(vote.Id, cancellationToken) ?? vote;
        }

        return ToResponse(vote);
    }

    public async Task<CommunityVoteResponse?> CloseAsync(Guid voteId, CommunityVoteCloseRequest request, CancellationToken cancellationToken)
    {
        var vote = await _store.GetAsync(voteId, cancellationToken);
        if (vote is null)
        {
            return null;
        }

        vote.Status = CommunityVoteStatusCodes.Closed;
        vote.ClosedAtUtc = DateTime.UtcNow;
        vote.ClosedByDisplayName = Normalize(request.ClosedByDisplayName, "운영자");
        await SaveMutationAsync(vote, cancellationToken);
        if (vote.GroupPurchase is not null)
        {
            await _ledgerWorkflow.진행Async(
                vote.Id,
                new CommunityGroupPurchaseLedgerProgressRequest
                {
                    StageCode = CommunityGroupPurchaseLedgerStageCodes.Counterparty,
                    Memo = "수요 모집을 마감하고 거래 상대 연결 단계로 진행했습니다."
                },
                vote.ClosedByDisplayName,
                cancellationToken);
        }
        return ToResponse(vote);
    }

    private async Task SaveMutationAsync(CommunityVoteRecord vote, CancellationToken cancellationToken)
    {
        var expectedRevision = vote.Revision;
        vote.Revision++;
        if (!await _store.ReplaceAsync(vote, expectedRevision, cancellationToken))
        {
            throw new InvalidOperationException("다른 참여자가 투표를 먼저 변경했습니다. 최신 결과를 확인한 뒤 다시 시도해 주세요.");
        }
    }

    private static void EnsureOpen(CommunityVoteRecord vote)
    {
        if (vote.Status != CommunityVoteStatusCodes.Open || vote.ClosesAtUtc is not null && vote.ClosesAtUtc <= DateTime.UtcNow)
        {
            vote.Status = CommunityVoteStatusCodes.Closed;
            vote.ClosedAtUtc ??= DateTime.UtcNow;
            throw new InvalidOperationException("마감된 투표입니다.");
        }
    }

    private static CommunityVoteResponse ToResponse(CommunityVoteRecord vote)
    {
        var counts = vote.Votes
            .SelectMany(x => x.OptionIds)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var requestedQuantities = vote.Votes
            .SelectMany(voteCast => voteCast.OptionIds.Select(optionId => new
            {
                OptionId = optionId,
                voteCast.RequestedQuantity
            }))
            .GroupBy(x => x.OptionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.RequestedQuantity), StringComparer.OrdinalIgnoreCase);
        var maxCount = counts.Count == 0 ? 0 : counts.Values.Max();

        return new CommunityVoteResponse
        {
            Id = vote.Id,
            AppKey = vote.AppKey,
            CommunityScope = vote.CommunityScope,
            Title = vote.Title,
            Description = vote.Description,
            VoteKind = vote.VoteKind,
            SourcePostId = vote.SourcePostId,
            CommunityLedgerId = vote.CommunityLedgerId,
            Status = vote.Status,
            AllowMultipleSelection = vote.AllowMultipleSelection,
            ResolutionDocumentEnabled = vote.ResolutionDocumentEnabled,
            SignatureRequired = vote.SignatureRequired,
            CreatedByDisplayName = vote.CreatedByDisplayName,
            CreatedAtUtc = vote.CreatedAtUtc,
            ClosesAtUtc = vote.ClosesAtUtc,
            ClosedAtUtc = vote.ClosedAtUtc,
            TotalVoteCount = vote.Votes.Count,
            Options = vote.Options.Select(x =>
            {
                counts.TryGetValue(x.OptionId, out var count);
                requestedQuantities.TryGetValue(x.OptionId, out var requestedQuantity);
                return new CommunityVoteOptionResponse
                {
                    OptionId = x.OptionId,
                    Text = x.Text,
                    ProductKey = x.ProductKey,
                    HsCode = x.HsCode,
                    TemperatureCode = x.TemperatureCode,
                    LogisticsMode = x.LogisticsMode,
                    QuantityUnit = x.QuantityUnit,
                    VoteCount = count,
                    RequestedQuantity = requestedQuantity,
                    IsWinningOption = count > 0 && count == maxCount
                };
            }).ToArray(),
            GroupPurchase = ToGroupPurchaseResponse(vote),
            ResolutionDocument = vote.ResolutionDocument is null ? null : ToResolutionResponse(vote.ResolutionDocument)
        };
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private const string GenericLegalEffectNotice =
        "이 문서는 커뮤니티 투표 결과와 전자서명 증적을 정리한 플랫폼 결의문입니다. 실제 법적 효력과 제출 가능 여부는 문서 종류, 당사자 권한, 고지/동의, 상대 기관 기준, 관련 법령 검토가 필요합니다.";

    private sealed record GroupPurchaseParticipation(
        int RequestedQuantity,
        string ParticipationMethodCode,
        string? PickupPointId);
}
