using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.정산;
using R = Ssalddel.Contracts.Common.Settlement.NeighborhoodActivityRewardCodes;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Application.Settlement;

/// <summary>
/// 기본 작업비와 별개인 주문별 공동 성과금입니다. 서버의 수령·행별 기여와 현재 판본 동의를 확인합니다.
/// 경제 입력은 Simulation 당사자 진술이며 운영 수익·은행 확인으로 승격하지 않습니다.
/// </summary>
public sealed partial class NeighborhoodSettlementUseCase
{
    private static readonly 활동성과배분Policy ActivityRewardPolicy = new();

    private static bool RewardParty(NeighborhoodActivityRewardAgreementDto reward, string actor)
        => reward.PayerUserId == actor || reward.ParticipantUserIds.Contains(actor, StringComparer.Ordinal)
            || reward.Economics?.PlatformPayeeUserId == actor;

    private static bool PlatformParty(NeighborhoodSettlementAgreementRequest terms, string actor)
        => terms.ActivityRewardTerms?.Economics?.PlatformPayeeUserId == actor;

    private static void ValidateActivityRewardTerms(NeighborhoodSettlementAgreementRequest terms, NeighborhoodSettlementSourceSnapshot source)
    {
        if (terms.ActivityRewardTerms is not { } reward) return;
        Require(terms.SourceKind == C.DistributedOrder && terms.ActivityKind == C.StoredGoods,
            "ActivityRewardOrderSourceRequired", 400);
        Text(reward.PayerUserId);
        Require(Party(source, reward.PayerUserId) && (Role(source, reward.PayerUserId, "Owner") || Role(source, reward.PayerUserId, "Seller")),
            "ActivityRewardPayerRequired", 403);
        Require(reward.ParticipantUserIds is { Count: > 0 and <= 32 }
            && reward.ParticipantUserIds.Distinct(StringComparer.Ordinal).Count() == reward.ParticipantUserIds.Count,
            "ActivityRewardParticipantsInvalid", 400);
        foreach (var participant in reward.ParticipantUserIds)
        { Text(participant); Require(Party(source, participant), "ActivityRewardParticipantMismatch", 403); }
        Require(reward.Policy is not null && reward.FinancialBasis is not null, "ActivityRewardTermsRequired", 400);
        Require(reward.Policy!.Weights is { Count: > 0 and <= 16 }, "ActivityRewardWeightsRequired", 400);
        var check = ActivityRewardPolicy.Calculate(reward.Policy, reward.FinancialBasis!, []);
        Require(check.StatusCode != R.InvalidInput, "ActivityRewardTermsInvalid", 400);
    }

    public async Task<NeighborhoodActivityRewardPreviewDto> ActivityRewardPreviewAsync(string id, string actor, CancellationToken ct)
    {
        Gate(actor); var row = await Owned(id, actor, ct);
        var reward = Terms(row).ActivityRewardTerms;
        var frozen = await FrozenActivityReward(row, ct);
        if (frozen is not null)
        {
            Require(FrozenRewardParty(frozen, actor), "ActivityRewardNotFound", 404);
            return RewardForActor(frozen, actor);
        }
        Require(reward is not null && RewardParty(reward, actor), "ActivityRewardNotFound", 404);
        var preview = await ActivityRewardPreview(row, ct);
        // 조회 중 발생한 동결 계산도 최신 약정의 새 참여자에게 과거 금융 정보를 열지 않습니다.
        Require(!preview.AlreadyAccrued || FrozenRewardParty(preview, actor), "ActivityRewardNotFound", 404);
        return RewardForActor(preview, actor);
    }

    private static bool FrozenRewardParty(NeighborhoodActivityRewardPreviewDto preview, string actor)
        => preview.FundingPayerUserId == actor || preview.PlatformPayeeUserId == actor
            || preview.Allocations.Any(x => x.ActorUserId == actor);

    private static NeighborhoodActivityRewardPreviewDto RewardForActor(NeighborhoodActivityRewardPreviewDto preview, string actor)
    {
        // 같은 업무에 참여했다는 이유로 다른 작업자의 성과금·사진 근거까지 공개하지 않습니다.
        if (preview.FundingPayerUserId != actor)
        {
            preview.Allocations = preview.Allocations.Where(x => x.ActorUserId == actor).ToArray();
            preview.ContributionShares = preview.ContributionShares.Where(x => x.Contribution.ActorUserId == actor).ToArray();
            preview.CostAttributions = [];
        }
        return preview;
    }

    private async Task<NeighborhoodActivityRewardPreviewDto?> FrozenActivityReward(NeighborhoodSettlementAgreementRecord row, CancellationToken ct)
    {
        // 이미 발생한 성과금은 이후 작업/판본 변경으로 다시 계산하거나 자동 증액하지 않습니다.
        var previous = await (from e in db.Set<NeighborhoodSettlementEntryRecord>().AsNoTracking()
            join a in db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking() on e.AgreementId equals a.AgreementId
            where a.SourceKind == row.SourceKind && a.SourceId == row.SourceId && e.Kind == C.ActivityReward
            select e.AgreementId).FirstOrDefaultAsync(ct);
        if (previous is not null)
        {
            var receipt = await db.Set<NeighborhoodSettlementCommandReceiptRecord>().AsNoTracking()
                .SingleAsync(x => x.AgreementId == previous && x.Kind == "AccrueActivityReward", ct);
            var frozen = Deserialize<NeighborhoodSettlementReceiptDto>(receipt.ResponseJson).ActivityRewardCalculation!;
            frozen.AlreadyAccrued = true; return frozen;
        }
        return null;
    }

    private async Task<NeighborhoodActivityRewardPreviewDto> ActivityRewardPreview(NeighborhoodSettlementAgreementRecord row, CancellationToken ct)
    {
        if (await FrozenActivityReward(row, ct) is { } frozen) return frozen;
        var terms = Terms(row); var reward = terms.ActivityRewardTerms!;
        Require(reward is not null, "ActivityRewardNotFound", 404);
        var current = await sources.ResolveAsync(row.SourceKind, row.SourceId, ct); var original = Source(row);
        if (current.SourceVersion != original.SourceVersion || current.SourceDigest != original.SourceDigest)
            return Waiting("ActivityRewardSourceReconfirmationRequired");
        if (row.StatusCode != C.Active) return Waiting("ActivityRewardLatestConsentRequired");
        if (terms.ExpiresAtUtc is { } expiry && expiry <= Now) return Waiting("ActivityRewardTermsExpired");
        if (current.RewardOutcome is not { ConfirmedQuantity: > 0 } outcome || outcome.EvidenceReferences.Count == 0)
            return Waiting("ActivityRewardCustomerReceiptRequired");
        var costs = terms.Lines.Where(x => x.PayerUserId == reward.PayerUserId && !x.IncludedInDelivery
            && x.Kind is not C.ProductSale and not C.OwnerRemittance).Select(TermTotal).ToArray();
        if (costs.Any(x => x is null)) return Waiting("ActivityRewardBasicWorkCostUnresolved");
        var recorded = (await db.Set<NeighborhoodSettlementEntryRecord>().AsNoTracking()
            .Where(x => x.AgreementId == row.AgreementId).ToArrayAsync(ct)).Select(Entry).ToArray();
        var basic = recorded.Where(x => x.PayerUserId == reward.PayerUserId && !x.IncludedInDelivery
            && x.OriginalEntryId is null && x.Kind is C.Inbound or C.Picking or C.Storage or C.Delivery or C.SalesAgency).ToArray();
        var basicIds = basic.Select(x => x.EntryId).ToHashSet(StringComparer.Ordinal);
        var adjustments = recorded.Where(x => x.OriginalEntryId is not null && basicIds.Contains(x.OriginalEntryId)).ToArray();
        if (basic.Any(x => x.AmountKrw is null) || adjustments.Any(x => x.AmountKrw is null))
            return Waiting("ActivityRewardBasicWorkCostUnresolved");
        if (basic.Any(x => x.StatusCode == C.Disputed) || adjustments.Any(x => x.StatusCode != C.Confirmed))
            return Waiting("ActivityRewardBasicWorkCostDisputed");
        // 새 판본의 미래 단가를 낮춰도 이미 발생한 보수와 확정 조정은 비용에서 사라지지 않습니다.
        var recordedCost = basic.Sum(x => x.AmountKrw!.Value) + adjustments.Sum(x => x.AmountKrw!.Value);
        var minimumCost = Math.Max(costs.Sum(x => x!.Value), recordedCost);
        if (reward.FinancialBasis.BasicWorkCostAmountKrw is { } budgetCost && budgetCost < minimumCost)
            return Waiting("ActivityRewardBasicWorkCostUnderstated");
        var inventory = await InventoryCosts(row, current, ct);
        if (inventory.Reason is { } costReason) return Waiting(costReason);
        var contributions = new List<NeighborhoodActivityRewardContributionDto>();
        foreach (var weight in reward.Policy.Weights.Where(x => x.Weight is null or > 0))
        {
            // 입고·보관의 주문별 귀속을 추정하지 않습니다. 정확한 연결이 없으면 근거 대기로 남깁니다.
            var work = current.CompletedWork.Where(x => x.Kind == weight.Kind && x.RelatedOrderId == row.SourceId
                && x.OriginRevision > 0 && x.Quantity > 0 && x.EvidenceReferences.Count > 0
                && x.OccurredAtUtc is { Kind: DateTimeKind.Utc } at && at <= Now && !string.IsNullOrWhiteSpace(x.ActorUserId)).ToArray();
            if (work.Length == 0) return Waiting("ActivityRewardOrderWorkEvidenceRequired");
            if (weight.UnitCode != R.Unit) return Waiting("ActivityRewardStorageAttributionRequired");
            if (work.Sum(x => x.Quantity) > outcome.ConfirmedQuantity)
                return Waiting("ActivityRewardCompletedQuantityExceeded");
            if (weight.Kind is C.Inbound or C.Picking && work.Sum(x => x.Quantity) != outcome.ConfirmedQuantity)
                return Waiting("ActivityRewardCompletedQuantityIncomplete");
            foreach (var w in work)
            {
                if (!reward.ParticipantUserIds.Contains(w.ActorUserId!, StringComparer.Ordinal))
                    return Waiting("ActivityRewardWorkerConsentRequired");
                if (!Role(current, w.ActorUserId!, weight.Kind switch
                    { C.Inbound => "Receiver", C.Picking => "Picker", C.Delivery => "Driver", C.Storage => "SpaceOwner", _ => "" }))
                    return Waiting("ActivityRewardWorkerRoleChanged");
                contributions.Add(new() { ContributionId = Digest(new { w.Kind, w.OriginKind, w.OriginId, w.ActorUserId, w.RelatedOrderId }),
                    ActorUserId = w.ActorUserId!, Kind = w.Kind, UnitCode = R.Unit, Quantity = w.Quantity,
                    OriginKind = w.OriginKind, OriginId = w.OriginId, OriginRevision = w.OriginRevision, EvidenceReferences = w.EvidenceReferences });
            }
        }
        if (reward.ParticipantUserIds.Any(x => !contributions.Any(c => c.ActorUserId == x)))
            return Waiting("ActivityRewardParticipantContributionRequired");
        var financialBasis = Deserialize<NeighborhoodActivityRewardFinancialBasisDto>(Serialize(reward.FinancialBasis));
        if (reward.Economics is not null && financialBasis.BasicWorkCostAmountKrw is { } outboundCost)
            financialBasis.BasicWorkCostAmountKrw = outboundCost + inventory.Costs.Sum(x => x.AmountKrw);
        var result = ActivityRewardPolicy.Calculate(reward.Policy, financialBasis, contributions);
        result.FundingPayerUserId = reward.PayerUserId;
        if (reward.Economics is { } economics)
        {
            result.PlatformPayeeUserId = economics.PlatformPayeeUserId;
            result.PlatformRetentionRatePercent = economics.PlatformRetentionRatePercent;
            result.PlatformRetentionAmountKrw = result.SurplusAmountKrw - result.PoolAmountKrw;
            result.CostAttributionAmountKrw = inventory.Costs.Sum(x => x.AmountKrw);
            result.CostAttributions = inventory.Costs;
            result.Notice = "Simulation 합의 수익에서 출고 기본 작업 비용·연결 입고/보관 원가·기타 비용·예비금을 별도로 차감한 잔여금 배분입니다. 실제 입금·확정 영업이익 인증이 아닙니다.";
        }
        result.CalculationDigest = Digest(new { row.SourceKind, row.SourceId, row.TermsVersion, original.SourceVersion, original.SourceDigest,
            Reward = reward, Outcome = new { outcome.ConfirmedQuantity,
                EvidenceReferences = outcome.EvidenceReferences.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray() },
            Calculation = result });
        return result;

        NeighborhoodActivityRewardPreviewDto Waiting(string reason) => new()
        { PolicyVersion = reward.Policy.PolicyVersion, FundingPayerUserId = reward.PayerUserId, ReasonCodes = [reason] };
    }

    public Task<NeighborhoodSettlementReceiptDto> AccrueActivityRewardAsync(string id, string actor,
        NeighborhoodSettlementActivityRewardAccrualRequest r, CancellationToken ct)
        => Execute(actor, "AccrueActivityReward", id, r, async () =>
        {
            var row = await Owned(id, actor, ct); Revision(row, r); var terms = Terms(row);
            Require(terms.ActivityRewardTerms is { } reward && reward.PayerUserId == actor, "ActivityRewardPayerRequired", 403);
            await CurrentSource(row, ct);
            var preview = await ActivityRewardPreview(row, ct);
            Require(!preview.AlreadyAccrued, "ActivityRewardAlreadyAccrued");
            Require(preview.StatusCode == R.Ready, "ActivityRewardEvidenceNotReady");
            Require(preview.CalculationDigest.Length > 0 && r.ExpectedCalculationDigest == preview.CalculationDigest,
                "ActivityRewardCalculationChanged");
            await AccrueInventoryCosts(row, preview, actor, ct);
            var created = new List<string>();
            foreach (var allocation in preview.Allocations.OrderBy(x => x.ActorUserId, StringComparer.Ordinal))
            {
                var entry = new NeighborhoodSettlementEntryDto
                {
                    EntryId = NewId("nse"), AgreementId = id, TermsVersion = row.TermsVersion,
                    LineKey = "activity-reward:" + allocation.ActorUserId, Kind = C.ActivityReward, Billing = C.Fixed,
                    AmountKrw = allocation.AmountKrw, PayerUserId = actor, PayeeUserId = allocation.ActorUserId,
                    BeneficiaryRole = "ActivityContributor", OriginKind = row.SourceKind, OriginId = row.SourceId,
                    OriginRevision = row.Revision, IsInternal = actor == allocation.ActorUserId,
                    CalculationBasis = "사전 합의 가중치 × 주문별 완료 기여 · 정수 원 잔여 배분 · Simulation 경제 진술",
                    ActivityRewardCalculationDigest = preview.CalculationDigest,
                    WorkEvidenceReferences = preview.ContributionShares.Where(x => x.Contribution.ActorUserId == allocation.ActorUserId)
                        .SelectMany(x => x.Contribution.EvidenceReferences).Distinct(StringComparer.Ordinal).ToArray(),
                    StatusCode = C.Accrued, Revision = 1, RecordedAtUtc = Now
                };
                db.Add(new NeighborhoodSettlementEntryRecord { EntryId = entry.EntryId, AgreementId = id, TermsVersion = row.TermsVersion,
                    LineKey = entry.LineKey, Kind = C.ActivityReward, OriginKind = row.SourceKind, OriginId = row.SourceId,
                    OriginRevision = row.Revision, AccrualKey = Digest(new { Kind = C.ActivityReward, row.SourceKind, row.SourceId,
                        // 첫 항목은 수령자나 정책 판본이 달라도 동일 원주문의 재발생을 막는 고유 키입니다.
                        Participant = created.Count == 0 ? "" : allocation.ActorUserId }),
                    DetailsJson = Serialize(entry), StatusCode = C.Accrued, RecordedAtUtc = Now });
                created.Add(entry.EntryId);
            }
            Require(created.Count > 0, "ActivityRewardContributionRequired");
            if (await AccruePlatformShare(row, preview, ct) is { } platformEntryId) created.Add(platformEntryId);
            row.Revision++; row.UpdatedAtUtc = Now; AppendHistory(row, actor, "AccrueActivityReward", Now);
            var receipt = Result(row, r.RequestId, "AccrueActivityReward", created);
            receipt.ActivityRewardCalculation = preview; return receipt;
        }, ct);
}
