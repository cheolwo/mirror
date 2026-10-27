using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

/// <summary>기존 입고 행에서 공급 협의·양측 동의·건별 공간 예약을 처리합니다. 실제 재고는 사진 수령만 확정합니다.</summary>
public sealed partial class 분산물류UseCase
{
    public Task<DistributedCommandResponse> SiteAcceptancePolicyAsync(DistributedActor actor, string id, DistributedSiteAcceptancePolicyRequest r, CancellationToken ct)
        => Execute(actor, "SiteAcceptancePolicy", id, r, async () =>
        {
            var site = await Site(id, ct); Require(site.OwnerUserId == actor.UserId, "DistributedSpaceOwnerRequired", 403); Revision(site.Revision, r);
            Require(분산입고수용Policy.Validate(r.Policy).Count == 0, "DistributedAcceptancePolicyInvalid", 400);
            site.AcceptancePolicyJson = Serialize(r.Policy); site.AcceptancePolicyRevision++; site.Revision++;
            return (id, site.Revision, site.StatusCode, (string?)null, 0);
        }, ct);

    public Task<DistributedCommandResponse> InboundProposalAsync(DistributedActor actor, string id, string lineKey, DistributedInboundProposalRequest r, CancellationToken ct)
        => Execute(actor, "InboundProposal", id + "/" + lineKey, r, async () =>
        {
            var work = await InboundLine(actor, id, lineKey, r, ct); RequireInboundParty(actor, work.Lot, work.Site);
            Require(!ClosedInbound(work.Lot), "DistributedInboundClosed");
            Require(work.Lot.ReceivedQuantity < work.Lot.ExpectedQuantity, "DistributedInboundAlreadyReceived");
            Require(work.Site.AcceptancePolicyJson is not null, "DistributedAcceptancePolicyRequired");
            await ValidateInboundTerms(work.Lot, work.Site, r.Terms, requireActive: false, ct);
            var state = Collaboration(work.Lot) ?? new();
            Require(state.History.Count(x => x.Kind == "Proposed") < 50, "DistributedInboundProposalLimitReached");
            if (work.Lot.ReceivedQuantity > 0)
            {
                var fixedTerms = state.ReservedTerms ?? state.Terms;
                Require(r.Terms.Quantity >= work.Lot.ReceivedQuantity && SameGoods(r.Terms.Goods, fixedTerms.Goods)
                    && r.Terms.SupplierUserId == fixedTerms.SupplierUserId, "DistributedInboundReceivedGoodsImmutable");
            }
            state.TermsVersion++; state.Terms = Deserialize<DistributedInboundCollaborationTerms>(Serialize(r.Terms));
            state.ProposedPolicyRevision = work.Site.AcceptancePolicyRevision;
            state.ProposedByUserId = actor.UserId; state.StatusCode = "Proposed";
            state.SpaceOwnerConsented = false; state.GoodsOwnerConsented = false; state.ApprovedAtUtc = null; state.ApprovedPolicyRevision = null;
            AddInboundHistory(state, actor, "Proposed", "", true); SaveCollaboration(work.Lot, state); work.Plan.Revision++; work.Site.Revision++;
            return (id, work.Plan.Revision, state.StatusCode, (string?)null, 0);
        }, ct);

    public Task<DistributedCommandResponse> InboundConsentAsync(DistributedActor actor, string id, string lineKey, DistributedInboundConsentRequest r, CancellationToken ct)
        => Execute(actor, "InboundConsent", id + "/" + lineKey, r, async () =>
        {
            var work = await InboundLine(actor, id, lineKey, r, ct); var state = RequireCollaboration(work.Lot, r.TermsVersion);
            Require(actor.UserId == work.Lot.OwnerUserId || actor.UserId == work.Site.OwnerUserId, "DistributedInboundConsentPartyRequired", 403);
            Require(!ClosedInbound(work.Lot), "DistributedInboundClosed");
            if (!r.Granted)
            {
                Require(actor.UserId == work.Lot.OwnerUserId && state.GoodsOwnerConsented || actor.UserId == work.Site.OwnerUserId && state.SpaceOwnerConsented,
                    "DistributedInboundConsentAlreadyWithdrawn");
                if (actor.UserId == work.Lot.OwnerUserId) state.GoodsOwnerConsented = false;
                if (actor.UserId == work.Site.OwnerUserId) state.SpaceOwnerConsented = false;
                state.StatusCode = "Proposed"; state.ApprovedAtUtc = null; state.ApprovedPolicyRevision = null;
                AddInboundHistory(state, actor, "ConsentWithdrawn", ""); SaveCollaboration(work.Lot, state); work.Plan.Revision++; work.Site.Revision++;
                return (id, work.Plan.Revision, state.StatusCode, (string?)null, 0);
            }
            Require(work.Lot.ReceivedQuantity < work.Lot.ExpectedQuantity, "DistributedInboundAlreadyReceived");
            Require(state.ProposedPolicyRevision == work.Site.AcceptancePolicyRevision, "DistributedInboundPolicyChanged");
            Require(!(actor.UserId == work.Lot.OwnerUserId && state.GoodsOwnerConsented)
                && !(actor.UserId == work.Site.OwnerUserId && state.SpaceOwnerConsented), "DistributedInboundConsentAlreadyGranted");
            await ValidateInboundTerms(work.Lot, work.Site, state.Terms, requireActive: true, ct);
            Require(work.Site.AcceptancePolicyJson is not null, "DistributedAcceptancePolicyRequired");
            var policy = Deserialize<DistributedSiteAcceptancePolicy>(work.Site.AcceptancePolicyJson!);
            var decision = RemainingCapacityDecision(policy, state.Terms.Goods, state.Terms.Quantity - work.Lot.ReceivedQuantity,
                await SpaceUsage(work.Site, work.Lot.LotId, policy, ct));
            Require(decision.IsAllowed, decision.ReasonCodes.FirstOrDefault() ?? "DistributedAcceptanceReviewRequired");
            if (actor.UserId == work.Lot.OwnerUserId) state.GoodsOwnerConsented = r.Granted;
            if (actor.UserId == work.Site.OwnerUserId) state.SpaceOwnerConsented = r.Granted;
            state.StatusCode = !r.Granted ? "Proposed" : "AwaitingOtherParty";
            if (state.GoodsOwnerConsented && state.SpaceOwnerConsented)
            {
                await SourceAllocationAvailable(work.Lot, state.Terms.Quantity, ct);
                var agreement = await Agreement(state.Terms.AgreementId, ct);
                await InboundAgreementCapacity(agreement, work.Lot, state.Terms.Quantity - work.Lot.ReceivedQuantity, ct);
                state.StatusCode = "Confirmed"; state.ApprovedPolicyRevision = work.Site.AcceptancePolicyRevision; state.ApprovedAtUtc = Now;
                state.ReservedTerms = Deserialize<DistributedInboundCollaborationTerms>(Serialize(state.Terms)); state.ReservedTermsVersion = state.TermsVersion;
                work.Lot.ExpectedQuantity = state.Terms.Quantity;
                var target = await db.입고요청.SingleAsync(x => x.Id == work.Lot.TargetInboundId, ct);
                target.예정수량 = state.Terms.Quantity;
                if (target.예정수량 == work.Lot.ReceivedQuantity)
                {
                    // 이미 사진으로 확인한 실물 범위로 예정 수량을 합의 축소합니다. 새 수령/작업 근거를 만들지 않습니다.
                    target.상태 = "입고완료"; target.입고완료일시 = Now;
                }
                agreement.Revision++;
            }
            else { state.ApprovedAtUtc = null; state.ApprovedPolicyRevision = null; }
            AddInboundHistory(state, actor, state.StatusCode == "Confirmed" ? "Confirmed" : "Granted", "", state.StatusCode == "Confirmed");
            SaveCollaboration(work.Lot, state); work.Plan.Revision++; work.Site.Revision++;
            await RefreshInboundCompletion(work.Plan, ct);
            return (id, work.Plan.Revision, state.StatusCode, (string?)null, 0);
        }, ct);

    public Task<DistributedCommandResponse> InboundDecisionAsync(DistributedActor actor, string id, string lineKey, DistributedInboundDecisionRequest r, CancellationToken ct)
        => Execute(actor, "InboundDecision", id + "/" + lineKey, r, async () =>
        {
            var work = await InboundLine(actor, id, lineKey, r, ct); RequireInboundParty(actor, work.Lot, work.Site);
            var state = RequireCollaboration(work.Lot, r.TermsVersion, allowDraft: true);
            Require(!ClosedInbound(work.Lot), "DistributedInboundClosed"); Require(work.Lot.ReceivedQuantity == 0, "DistributedInboundPhysicalStockRequiresReview");
            Require(r.Action is "Reject" or "Cancel", "DistributedInboundDecisionInvalid", 400);
            if (r.Action == "Reject") Require(actor.UserId == work.Site.OwnerUserId, "DistributedSpaceOwnerRequired", 403);
            state.StatusCode = r.Action == "Reject" ? "Rejected" : "Cancelled"; state.SpaceOwnerConsented = false; state.GoodsOwnerConsented = false;
            state.ApprovedPolicyRevision = null; state.ApprovedAtUtc = null;
            state.ReservedTerms = null; state.ReservedTermsVersion = null;
            AddInboundHistory(state, actor, state.StatusCode, Text(r.Reason, 500)); SaveCollaboration(work.Lot, state); work.Plan.Revision++; work.Site.Revision++;
            (await db.입고요청.SingleAsync(x => x.Id == work.Lot.TargetInboundId, ct)).상태 = "입고취소";
            await RefreshInboundCompletion(work.Plan, ct);
            return (id, work.Plan.Revision, state.StatusCode, (string?)null, 0);
        }, ct);

    public Task<DistributedCommandResponse> InboundNoteAsync(DistributedActor actor, string id, string lineKey, DistributedInboundNoteRequest r, CancellationToken ct)
        => Execute(actor, "InboundNote", id + "/" + lineKey, r, async () =>
        {
            var work = await InboundLine(actor, id, lineKey, r, ct); RequireInboundParty(actor, work.Lot, work.Site);
            var state = RequireCollaboration(work.Lot, r.TermsVersion, allowDraft: true);
            AddInboundHistory(state, actor, "Note", Text(r.Note, 1000)); SaveCollaboration(work.Lot, state); work.Plan.Revision++;
            return (id, work.Plan.Revision, state.StatusCode, (string?)null, 0);
        }, ct);

    private static DistributedInboundCollaborationDto? Collaboration(분산재고LotRecord lot)
        => lot.InboundCollaborationJson is null ? null : Deserialize<DistributedInboundCollaborationDto>(lot.InboundCollaborationJson);
    internal static bool ClosedInbound(분산재고LotRecord lot) => Collaboration(lot)?.StatusCode is "Rejected" or "Cancelled";
    private static DistributedInboundCollaborationDto RequireCollaboration(분산재고LotRecord lot, int version, bool allowDraft = false)
    {
        var state = Collaboration(lot); Require(state is not null && (allowDraft || state.TermsVersion > 0), "DistributedInboundProposalRequired");
        Require(state!.TermsVersion == version, "DistributedInboundTermsVersionMismatch"); return state;
    }
    private static void SaveCollaboration(분산재고LotRecord lot, DistributedInboundCollaborationDto state)
    { lot.InboundCollaborationJson = Serialize(state); lot.Revision++; }
    private static bool SameGoods(DistributedGoodsFacts left, DistributedGoodsFacts right)
    {
        if (left?.CategoryCodes is null || right?.CategoryCodes is null) return false;
        var a = Deserialize<DistributedGoodsFacts>(Serialize(left)); var b = Deserialize<DistributedGoodsFacts>(Serialize(right));
        a.CategoryCodes = a.CategoryCodes.Order(StringComparer.Ordinal).ToArray(); b.CategoryCodes = b.CategoryCodes.Order(StringComparer.Ordinal).ToArray();
        return Serialize(a) == Serialize(b);
    }
    private void AddInboundHistory(DistributedInboundCollaborationDto state, DistributedActor actor, string kind, string note, bool terms = false)
    {
        if (kind == "Note") Require(state.History.Count(x => x.Kind == "Note") < 50, "DistributedInboundNoteLimitReached");
        // 당사자 대화·철회 근거를 삭제하지 않습니다. 두 당사자 철회와 마지막 취소에 필요한 세 칸을 남깁니다.
        if (kind is not "ConsentWithdrawn" and not "Cancelled" and not "Rejected")
            Require(state.History.Count < 197, "DistributedInboundHistoryLimitReached");
        state.History = [.. state.History, new DistributedInboundCollaborationHistoryDto(kind, actor.UserId, state.TermsVersion, Now, note)
            { Terms = terms ? Deserialize<DistributedInboundCollaborationTerms>(Serialize(state.Terms)) : null }];
    }
    private static bool InboundParty(DistributedActor actor, 분산재고LotRecord lot, 분산거점Record site)
        => actor.UserId == lot.SellerUserId || actor.UserId == lot.OwnerUserId || actor.UserId == site.OwnerUserId;
    private static void RequireInboundParty(DistributedActor actor, 분산재고LotRecord lot, 분산거점Record site)
        => Require(InboundParty(actor, lot, site), "DistributedInboundPartyRequired", 403);
    private async Task<InboundLineWork> InboundLine(DistributedActor actor, string id, string lineKey, DistributedCommandRequest request, CancellationToken ct)
    {
        var plan = await Query<분산입고계획Record>().SingleOrDefaultAsync(x => x.PlanId == id, ct)
            ?? throw new DistributedFulfillmentException("DistributedInboundNotFound", 404);
        Revision(plan.Revision, request);
        var lot = await Query<분산재고LotRecord>().SingleOrDefaultAsync(x => x.PlanId == id && x.LineKey == lineKey, ct)
            ?? throw new DistributedFulfillmentException("DistributedLotNotFound", 404);
        var site = await Site(lot.SiteId, ct); RequireInboundParty(actor, lot, site); return new(plan, lot, site);
    }
    private async Task ValidateInboundTerms(분산재고LotRecord lot, 분산거점Record site, DistributedInboundCollaborationTerms terms, bool requireActive, CancellationToken ct)
    {
        Require(site.StatusCode == "Active", "DistributedConsentRequired");
        Require(terms is not null && terms.Goods?.CategoryCodes is not null && terms.SupplierTypeCode is { Length: <= 80 }
            && (terms.SupplierUserId == lot.SellerUserId || terms.SupplierUserId == lot.OwnerUserId) && terms.SellerUserId == lot.SellerUserId
            && terms.GoodsOwnerUserId == lot.OwnerUserId && terms.Quantity is > 0 and <= 1000000 && terms.Quantity >= lot.ReceivedQuantity, "DistributedInboundTermsInvalid", 400);
        Require(terms!.ArrivalStartUtc.Kind == DateTimeKind.Utc && terms.ArrivalEndUtc.Kind == DateTimeKind.Utc && terms.StorageEndsAtUtc.Kind == DateTimeKind.Utc
            && terms.ArrivalStartUtc < terms.ArrivalEndUtc && terms.ArrivalEndUtc < terms.StorageEndsAtUtc && terms.ArrivalEndUtc > Now,
            "DistributedInboundScheduleInvalid", 400);
        Require(terms.AccessHoursConfirmed, "DistributedInboundAccessHoursConfirmationRequired");
        Text(terms.SupplyPriceOrFreeTerms, 500); Text(terms.SupplierResponsibilityTerms, 1000); Text(terms.ReturnTerms, 1000);
        var agreement = await Agreement(terms.AgreementId, ct);
        Require(agreement.SiteId == site.SiteId && agreement.LocationId == lot.LocationId && agreement.GoodsOwnerUserId == lot.OwnerUserId
            && agreement.SpaceOwnerUserId == site.OwnerUserId && terms.AgreementTermsVersion == agreement.TermsVersion,
            "DistributedInboundAgreementVersionMismatch");
        var storage = Deserialize<DistributedStorageTerms>(agreement.TermsJson);
        Require(storage.AllowedSkus.Contains(lot.Sku) && agreement.StartsAtUtc <= terms.ArrivalStartUtc && terms.StorageEndsAtUtc <= agreement.EndsAtUtc,
            "DistributedInboundContractScopeMismatch");
        if (requireActive) Require(agreement.StatusCode == "Active" && Now < agreement.EndsAtUtc && agreement.SpaceOwnerConsented && agreement.GoodsOwnerConsented,
            "DistributedStorageAgreementRequired");
        if (site.AcceptancePolicyJson is { } json)
        {
            var policy = Deserialize<DistributedSiteAcceptancePolicy>(json);
            Require(policy.MaximumStorageDays is null || terms.StorageEndsAtUtc - terms.ArrivalStartUtc <= TimeSpan.FromDays(policy.MaximumStorageDays.Value),
                "DistributedInboundStoragePeriodExceeded");
        }
    }
    private async Task SourceAllocationAvailable(분산재고LotRecord lot, int quantity, CancellationToken ct)
    {
        var source = await db.입고요청.SingleAsync(x => x.Id == lot.SourceInboundId, ct);
        var others = await Query<분산재고LotRecord>().Where(x => x.SourceInboundId == lot.SourceInboundId && x.LotId != lot.LotId).ToArrayAsync(ct);
        Require(others.Where(x => !ClosedInbound(x)).Sum(x => x.ExpectedQuantity) + quantity <= source.예정수량, "DistributedInboundSourceOverallocated");
        // 같은 원천의 서로 다른 공간 예약도 같은 행의 CAS에 참여합니다.
        var updated = Now > source.UpdatedAt ? Now : source.UpdatedAt.AddMilliseconds(1);
        Require(await db.입고요청.Where(x => x.Id == source.Id && x.UpdatedAt == source.UpdatedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, updated), ct) == 1, "DistributedInboundSourceChanged");
    }
    private static DistributedInboundCollaborationTerms? ReservedInboundTerms(분산재고LotRecord lot)
    {
        var state = Collaboration(lot); if (state is null || ClosedInbound(lot)) return null;
        return state.StatusCode == "Confirmed" ? state.Terms : state.ReservedTerms;
    }
    private async Task<DistributedCapacityUsage> SpaceUsage(분산거점Record site, string? excludingLotId, DistributedSiteAcceptancePolicy policy, CancellationToken ct)
    {
        var lots = await Query<분산재고LotRecord>().Where(x => x.SiteId == site.SiteId).ToArrayAsync(ct);
        var stocks = await db.입고상품.AsNoTracking().Where(x => x.창고Id == site.WarehouseId && (x.가용수량 > 0 || x.예약수량 > 0)).ToArrayAsync(ct);
        var usage = new DistributedCapacityUsage { IsKnown = true, CapacityUnit = policy.CapacityUnit, WeightKg = 0, VolumeM3 = 0 };
        foreach (var stock in stocks)
        {
            var lot = lots.SingleOrDefault(x => x.InboundItemId == stock.Id); var facts = lot is null ? null : ReservedInboundTerms(lot)?.Goods;
            if (facts is null || facts.CapacityUnit != policy.CapacityUnit || facts.CapacityPerItem <= 0
                || policy.MaximumWeightKg.HasValue && !facts.WeightKgPerItem.HasValue || policy.MaximumVolumeM3.HasValue && !facts.VolumeM3PerItem.HasValue)
            { usage.IsKnown = false; return usage; }
            AddUsage(usage, facts, stock.가용수량 + stock.예약수량);
        }
        foreach (var lot in lots.Where(x => x.LotId != excludingLotId))
        {
            var terms = ReservedInboundTerms(lot); if (terms is null) continue;
            if (terms.Goods.CapacityUnit != policy.CapacityUnit || terms.Goods.CapacityPerItem <= 0
                || policy.MaximumWeightKg.HasValue && !terms.Goods.WeightKgPerItem.HasValue || policy.MaximumVolumeM3.HasValue && !terms.Goods.VolumeM3PerItem.HasValue)
            { usage.IsKnown = false; return usage; }
            AddUsage(usage, terms.Goods, Math.Max(0, terms.Quantity - lot.ReceivedQuantity));
        }
        return usage;
    }
    private static void AddUsage(DistributedCapacityUsage usage, DistributedGoodsFacts facts, int quantity)
    {
        try
        {
            usage.Capacity = checked(usage.Capacity + facts.CapacityPerItem * quantity);
            usage.WeightKg = usage.WeightKg.HasValue && facts.WeightKgPerItem.HasValue ? checked(usage.WeightKg.Value + facts.WeightKgPerItem.Value * quantity) : null;
            usage.VolumeM3 = usage.VolumeM3.HasValue && facts.VolumeM3PerItem.HasValue ? checked(usage.VolumeM3.Value + facts.VolumeM3PerItem.Value * quantity) : null;
        }
        catch (OverflowException) { throw new DistributedFulfillmentException("CapacityOverflow"); }
    }
    private static DistributedAcceptanceDecision RemainingCapacityDecision(DistributedSiteAcceptancePolicy policy, DistributedGoodsFacts goods, int remaining, DistributedCapacityUsage usage)
    {
        if (remaining > 0) return 분산입고수용Policy.Evaluate(policy, goods, remaining, usage);
        var product = 분산입고수용Policy.EvaluateGoods(policy, goods, 1);
        if (!product.IsAllowed) return product;
        if (!usage.IsKnown) return new(false, true, ["ExistingCapacityUnknown"], 0, 0, 0);
        var reasons = new List<string>();
        if (usage.CapacityUnit != policy.CapacityUnit) reasons.Add("ExistingCapacityUnitMismatch");
        if (usage.Capacity > policy.MaximumCapacity) reasons.Add("CapacityExceeded");
        if (policy.MaximumWeightKg.HasValue && (usage.WeightKg is null || usage.WeightKg > policy.MaximumWeightKg)) reasons.Add("WeightExceeded");
        if (policy.MaximumVolumeM3.HasValue && (usage.VolumeM3 is null || usage.VolumeM3 > policy.MaximumVolumeM3)) reasons.Add("VolumeExceeded");
        return new(reasons.Count == 0, reasons.Count > 0, reasons, 0, 0, 0);
    }
    private async Task InboundAgreementCapacity(분산보관합의Record agreement, 분산재고LotRecord current, int quantity, CancellationToken ct)
    {
        var lots = await Query<분산재고LotRecord>().Where(x => x.LocationId == agreement.LocationId && x.OwnerUserId == agreement.GoodsOwnerUserId).ToArrayAsync(ct);
        var ids = lots.Select(x => x.InboundItemId).ToArray();
        var stored = await db.입고상품.Where(x => Enumerable.Contains(ids, x.Id)).SumAsync(x => (int?)(x.가용수량 + x.예약수량), ct) ?? 0;
        var pending = lots.Where(x => x.LotId != current.LotId).Sum(x => Math.Max(0, (ReservedInboundTerms(x)?.Quantity ?? 0) - x.ReceivedQuantity));
        Require(stored + pending + quantity <= Deserialize<DistributedStorageTerms>(agreement.TermsJson).MaximumStoredQuantity, "DistributedStorageAgreementCapacityExceeded");
    }
    private async Task CheckInboundApproval(분산재고LotRecord lot, 분산거점Record site, CancellationToken ct)
    {
        var state = Collaboration(lot);
        if (site.AcceptancePolicyJson is null && state is null) return;
        Require(state is not null && state.StatusCode == "Confirmed" && state.GoodsOwnerConsented && state.SpaceOwnerConsented,
            "DistributedInboundApprovalRequired");
        Require(state!.ApprovedPolicyRevision == site.AcceptancePolicyRevision && state.ProposedPolicyRevision == site.AcceptancePolicyRevision, "DistributedInboundPolicyChanged");
        await ValidateInboundTerms(lot, site, state.Terms, requireActive: true, ct);
        Require(Now >= state.Terms.ArrivalStartUtc && Now <= state.Terms.ArrivalEndUtc, "DistributedInboundArrivalWindowClosed");
        var policy = Deserialize<DistributedSiteAcceptancePolicy>(site.AcceptancePolicyJson!);
        var decision = 분산입고수용Policy.Evaluate(policy, state.Terms.Goods, Math.Max(0, state.Terms.Quantity - lot.ReceivedQuantity),
            await SpaceUsage(site, lot.LotId, policy, ct));
        Require(decision.IsAllowed, decision.ReasonCodes.FirstOrDefault() ?? "DistributedAcceptanceReviewRequired");
    }
    private IReadOnlyList<string> InboundApprovalReasons(분산재고LotRecord lot, 분산거점Record site, IReadOnlyList<분산보관합의Record> agreements)
    {
        var state = Collaboration(lot); if (state is null && site.AcceptancePolicyJson is null) return [];
        if (state is null || state.TermsVersion == 0) return ["DistributedInboundProposalRequired"];
        if (state.StatusCode is "Rejected" or "Cancelled") return ["DistributedInboundClosed"];
        var reasons = new List<string>();
        if (state.ProposedPolicyRevision != site.AcceptancePolicyRevision) reasons.Add("DistributedInboundPolicyChanged");
        if (site.AcceptancePolicyJson is { } json)
        {
            var policy = Deserialize<DistributedSiteAcceptancePolicy>(json);
            reasons.AddRange(분산입고수용Policy.EvaluateGoods(policy, state.Terms.Goods, state.Terms.Quantity).ReasonCodes);
            reasons.AddRange(분산입고수용Policy.EvaluateSchedule(policy, state.Terms.ArrivalStartUtc, state.Terms.ArrivalEndUtc, state.Terms.StorageEndsAtUtc, state.Terms.AccessHoursConfirmed));
        }
        var agreement = agreements.SingleOrDefault(x => x.AgreementId == state.Terms.AgreementId);
        if (agreement is null || agreement.TermsVersion != state.Terms.AgreementTermsVersion) reasons.Add("DistributedInboundAgreementVersionMismatch");
        else if (agreement.StatusCode != "Active" || Now >= agreement.EndsAtUtc) reasons.Add("DistributedStorageAgreementRequired");
        if (state.StatusCode != "Confirmed" || !state.SpaceOwnerConsented || !state.GoodsOwnerConsented) reasons.Add("DistributedInboundApprovalRequired");
        else if (state.ApprovedPolicyRevision != site.AcceptancePolicyRevision) reasons.Add("DistributedInboundPolicyChanged");
        if (Now < state.Terms.ArrivalStartUtc || Now > state.Terms.ArrivalEndUtc) reasons.Add("DistributedInboundArrivalWindowClosed");
        return reasons.Distinct().ToArray();
    }
    private DistributedInboundLineDto MapInboundLine(DistributedActor actor, 분산재고LotRecord lot, 분산거점Record site, IReadOnlyList<분산보관합의Record> agreements)
    {
        var state = Collaboration(lot); var actions = new List<string>(); var party = InboundParty(actor, lot, site);
        if (party && site.AcceptancePolicyJson is not null && lot.ReceivedQuantity < lot.ExpectedQuantity && !ClosedInbound(lot)) actions.Add("propose-inbound");
        if (party && state is not null)
        {
            actions.Add("note-inbound");
            if (!ClosedInbound(lot) && lot.ReceivedQuantity < lot.ExpectedQuantity)
            {
                if (lot.ReceivedQuantity == 0) { actions.Add("cancel-inbound"); if (actor.UserId == site.OwnerUserId) actions.Add("reject-inbound"); }
                if (state.TermsVersion > 0 && state.ProposedPolicyRevision == site.AcceptancePolicyRevision && (actor.UserId == lot.OwnerUserId && !state.GoodsOwnerConsented
                    || actor.UserId == site.OwnerUserId && !state.SpaceOwnerConsented)) actions.Add("consent-inbound");
            }
        }
        var reasons = InboundApprovalReasons(lot, site, agreements);
        if (Manages(actor, site) && site.StatusCode == "Active" && lot.ReceivedQuantity < lot.ExpectedQuantity && reasons.Count == 0) actions.Add("receive");
        var view = state is null ? null : Deserialize<DistributedInboundCollaborationDto>(Serialize(state));
        if (!party && view is not null)
        {
            // 현장 수령 담당자는 상품/도착 조건만 필요합니다. 계약 대금과 당사자 대화는 공개하지 않습니다.
            view.Terms.SupplyPriceOrFreeTerms = ""; view.Terms.SupplierResponsibilityTerms = ""; view.Terms.ReturnTerms = "";
            view.History = []; view.ReservedTerms = null;
        }
        var next = lot.ReceivedQuantity == lot.ExpectedQuantity ? "입고 완료" : state is null || state.TermsVersion == 0 ? "공급자 제안" : ClosedInbound(lot) ? "다른 공간·새 입고 협의"
            : reasons.Contains("DistributedInboundPolicyChanged") || reasons.Contains("DistributedInboundAgreementVersionMismatch") ? "공급자·공간 제공자 조건 재협의"
            : !state.GoodsOwnerConsented ? "물품 소유자 동의" : !state.SpaceOwnerConsented ? "공간 제공자 승인"
            : lot.ReceivedQuantity < lot.ExpectedQuantity ? "현장 입고 담당자" : "입고 완료";
        return new(lot.LineKey, lot.SiteId, lot.LocationId, lot.OwnerUserId, lot.Sku, lot.LotCode, lot.ExpectedQuantity, lot.ReceivedQuantity)
        { SellerUserId = lot.SellerUserId, InboundCollaboration = view, AllowedActions = actions, WaitingReasonCodes = reasons, NextActorLabel = next };
    }
    private static IReadOnlyList<string> AgreementActions(DistributedActor actor, 분산보관합의Record agreement)
    {
        if (actor.UserId != agreement.SpaceOwnerUserId && actor.UserId != agreement.GoodsOwnerUserId) return [];
        var actions = new List<string> { "change-terms", (actor.UserId == agreement.SpaceOwnerUserId && agreement.SpaceOwnerConsented)
            || (actor.UserId == agreement.GoodsOwnerUserId && agreement.GoodsOwnerConsented) ? "revoke" : "consent" };
        if (agreement.PendingTermsJson is not null && (actor.UserId == agreement.SpaceOwnerUserId && !agreement.PendingSpaceOwnerConsented
            || actor.UserId == agreement.GoodsOwnerUserId && !agreement.PendingGoodsOwnerConsented)) actions.Add("consent-pending-terms");
        return actions;
    }
    private sealed record InboundLineWork(분산입고계획Record Plan, 분산재고LotRecord Lot, 분산거점Record Site);
}
