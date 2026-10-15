using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.정산;
using 살뜰.도메인.창고;
using 살뜰.도메인.운송;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Application.Settlement;

public interface I주문별원가보수조회Service
{
    Task<주문별원가보수Dto> GetAsync(string rootOrderLedgerId, string actorUserId, CancellationToken ct);
}

/// <summary>Mongo 원주문의 현재 참조와 SQL 전문 원장/정산의 본인 정보만 조합합니다. 원장·재고·금액을 저장하지 않습니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.WorldRolePerspective, SsalddelCodeLayer.Application,
    "원주문의 현재 참조에서 본인 작업 보수와 주문별 입고 원가 귀속을 읽는다.",
    Effects = SsalddelCodeEffect.PersistentRead, ContractType = typeof(주문별원가보수Dto), FlowOrder = 60,
    Boundary = "하위 상태 복제/송금/새 채무/외부 효과 없음. 루트 공개는 타인의 정산·전체 Lot 조회권이 아니다.")]
public sealed class 주문별원가보수조회Service(SsalddelContext db, I커뮤니티원장저장소 ledgers,
    I주문원장공개요청저장소 disclosures, INeighborhoodSettlementSourceResolver sources,
    INeighborhoodSettlementUseCase settlements, ISsalddelExecutionModePolicy execution,
    IOptions<NeighborhoodSettlementOptions> options, TimeProvider clock) : I주문별원가보수조회Service
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Candidate(string Kind, string Id, string LedgerId);
    private static void Require(bool valid, string code, int status = 409)
    { if (!valid) throw new NeighborhoodSettlementException(code, status); }
    private static bool Direct(커뮤니티원장Dto ledger, string actor)
        => ledger.생성자UserId == actor || ledger.참여자목록.Any(x => x.UserId == actor
            && x.ParticipationState is not "철회" and not "탈퇴" and not "종료" and not "취소" and not "거절");
    private static bool Purged(커뮤니티원장Dto ledger)
        => ledger.확장속성.ContainsKey(배송원장개인정보파기Service.Marker);
    private static bool FinancialParty(NeighborhoodSettlementSummaryDto s)
        => s.Agreement.Terms.Lines.Count > 0 || s.Entries.Count > 0 || s.Payments.Count > 0 || s.ActivityReward is not null;

    public async Task<주문별원가보수Dto> GetAsync(string rootOrderLedgerId, string actorUserId, CancellationToken ct)
    {
        Require(options.Value.Enabled && options.Value.Profile == NeighborhoodSettlementOptions.RequiredProfile
            && execution.IsSimulation, "NeighborhoodSettlementDisabled", 404);
        Require(!string.IsNullOrWhiteSpace(actorUserId) && actorUserId == actorUserId.Trim() && actorUserId.Length <= 191,
            "NeighborhoodSettlementAuthenticationRequired", 401);
        Require(!string.IsNullOrWhiteSpace(rootOrderLedgerId) && rootOrderLedgerId == rootOrderLedgerId.Trim()
            && rootOrderLedgerId.Length <= 191, "NeighborhoodSettlementInputInvalid", 400);
        Require(db.Database.IsRelational(), "NeighborhoodSettlementRelationalStorageRequired");
        var root = await ledgers.원장조회Async(rootOrderLedgerId, ct);
        Require(root is not null && !Purged(root) && 주문원장구성정책.주문루트인가(root.원장템플릿Key),
            "NeighborhoodSettlementOrderNotFound", 404);
        var contexts = new List<커뮤니티원장Dto> { root! };
        // 참조가 이미 원주문에 전부 들어 있으므로 200개 목록 조회로 자르거나 전체 원장을 스캔하지 않습니다.
        foreach (var reference in root!.포함원장목록.DistinctBy(x => x.원장Id))
        {
            var child = await ledgers.원장조회Async(reference.원장Id, ct);
            if (child is not null && !Purged(child) && child.커뮤니티Id == root.커뮤니티Id
                && child.원장템플릿Key == reference.원장템플릿Key)
                contexts.Add(child);
        }
        var grants = await disclosures.승인된대상원장Ids조회Async(rootOrderLedgerId, actorUserId,
            contexts.Select(x => x.원장Id), clock.GetUtcNow(), ct);
        var ledgerAccess = contexts.Any(x => Direct(x, actorUserId) || grants.Contains(x.원장Id));
        var candidates = await Candidates(root, contexts, ct);
        var result = new List<주문정산원천Dto>();
        var currentSources = new Dictionary<string, NeighborhoodSettlementSourceSnapshot>(StringComparer.Ordinal);
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates.DistinctBy(x => (x.Kind, x.Id)))
        {
            var source = await sources.ResolveAsync(candidate.Kind, candidate.Id, ct);
            Require(source.SourceKind == candidate.Kind && source.SourceId == candidate.Id,
                "NeighborhoodSettlementOrderSourceMismatch");
            var agreements = await db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking()
                .Where(x => x.SourceKind == candidate.Kind && x.SourceId == candidate.Id)
                .OrderBy(x => x.AgreementId).ToArrayAsync(ct);
            var reads = new List<NeighborhoodSettlementSummaryDto>();
            foreach (var agreement in agreements)
            {
                try
                {
                    // 기존 부담자/수령자·과거 판본의 권한과 지급 시점 판정을 우회하지 않습니다.
                    var read = await settlements.GetAsync(agreement.AgreementId, actorUserId, ct);
                    if (FinancialParty(read)) reads.Add(read);
                }
                catch (NeighborhoodSettlementException e) when (e.Code == "NeighborhoodSettlementNotFound") { }
            }
            var mine = source.CompletedWork.Where(x => x.ActorUserId == actorUserId).ToArray();
            var context = contexts.Single(x => x.원장Id == candidate.LedgerId);
            var currentParty = source.Participants.Any(x => x.UserId == actorUserId);
            var maySeeSource = mine.Length > 0 || reads.Count > 0
                || currentParty && (Direct(context, actorUserId) || grants.Contains(context.원장Id));
            if (!maySeeSource) continue;
            currentSources[candidate.Kind + ":" + candidate.Id] = source;
            result.Add(new()
            {
                SourceKind = source.SourceKind, SourceId = source.SourceId, SourceVersion = source.SourceVersion,
                SourceDigest = source.SourceDigest, ObservedAtUtc = source.ObservedAtUtc, Settlements = reads,
                MyCompletedWork = mine.Select(work => Work(work, reads, actorUserId)).ToArray()
            });
            if (reads.Count == 0) reasons.Add("SettlementAgreementRequired");
        }
        Require(ledgerAccess || result.Any(x => x.MyCompletedWork.Count > 0 || x.Settlements.Count > 0),
            "NeighborhoodSettlementOrderNotFound", 404);
        if (result.Count == 0) reasons.Add("OrderSettlementSourceNotConnected");
        var costs = await Costs(result, currentSources, actorUserId, ct);
        // 참조 삭제·개인정보 파기와 동시 조회했으면 오래된 연결 결과를 반환하지 않습니다.
        foreach (var context in contexts)
        {
            var current = await ledgers.원장조회Async(context.원장Id, ct);
            Require(current is not null && !Purged(current) && current.Revision == context.Revision,
                "NeighborhoodSettlementOrderRequeryRequired");
        }
        var finalGrants = await disclosures.승인된대상원장Ids조회Async(rootOrderLedgerId, actorUserId,
            contexts.Select(x => x.원장Id), clock.GetUtcNow(), ct);
        Require(grants.SetEquals(finalGrants), "NeighborhoodSettlementOrderRequeryRequired");
        return new()
        {
            RootOrderLedgerId = rootOrderLedgerId, RootRevision = root.Revision,
            ObservedAtUtc = clock.GetUtcNow().UtcDateTime, Sources = result,
            InventoryCosts = costs, AmountGroups = Groups(result), ReasonCodes = reasons.Order(StringComparer.Ordinal).ToArray()
        };
    }

    private async Task<IReadOnlyList<Candidate>> Candidates(커뮤니티원장Dto root, IReadOnlyList<커뮤니티원장Dto> contexts, CancellationToken ct)
    {
        var result = new List<Candidate>();
        foreach (var context in contexts)
        {
            var sourceId = context.외부참조.GetValueOrDefault("원천Id");
            if (context.원장템플릿Key == CommunityLedgerTemplateKeys.SsalddelMart
                && (context.원장Id == root.원장Id || root.포함원장목록.Any(r => r.원장Id == context.원장Id && r.역할 == 주문원장포함역할.창고출고))
                && context.외부참조.GetValueOrDefault("원천유형") == "SsalddelMartOrder" && !string.IsNullOrWhiteSpace(sourceId))
            {
                var marts = await db.Set<마트주문>().AsNoTracking().Where(x => x.커뮤니티원장Id == context.원장Id
                    && x.커뮤니티원장템플릿Key == CommunityLedgerTemplateKeys.SsalddelMart && x.주문참조번호 == sourceId).ToArrayAsync(ct);
                Require(marts.Length <= 1, "NeighborhoodSettlementOrderSourceAmbiguous");
                if (marts.SingleOrDefault() is { } mart)
                    result.Add(new("MartOrder", mart.Id.ToString(CultureInfo.InvariantCulture), context.원장Id));
            }
            if (context.원장템플릿Key == CommunityLedgerTemplateKeys.FoodOrder
                && (context.원장Id == root.원장Id || root.포함원장목록.Any(r => r.원장Id == context.원장Id && r.역할 == 주문원장포함역할.판매))
                && context.외부참조.GetValueOrDefault("원천유형") == "FoodOrder" && !string.IsNullOrWhiteSpace(sourceId)
                && context.원장Id == 음식마트원장Mongo동기화Builder.음식주문원장Id생성(sourceId))
                result.Add(new(C.FoodOrder, sourceId, context.원장Id));
            var cargoId = context.외부참조.GetValueOrDefault("화주운송의뢰Id");
            if ((context.원장템플릿Key is CommunityLedgerTemplateKeys.CargoTransport or CommunityLedgerTemplateKeys.SsalddelMart)
                && root.포함원장목록.Any(r => r.원장Id == context.원장Id && r.역할 is 주문원장포함역할.운송 or 주문원장포함역할.배송)
                && !string.IsNullOrWhiteSpace(cargoId))
            {
                var transport = await db.Set<운송원장>().AsNoTracking().Where(x => x.의뢰Id == cargoId
                    && x.배차업무유형 != 상태값.배차업무유형.음식배달).ToArrayAsync(ct);
                Require(transport.Length <= 1, "NeighborhoodSettlementOrderSourceAmbiguous");
                if (transport.SingleOrDefault() is { } row
                    && (row.커뮤니티원장Id == context.원장Id || row.커뮤니티원장Id is null
                        && 운송원장Mongo동기화Builder.원장Id생성(cargoId) == context.원장Id)
                    && row.커뮤니티원장템플릿Key == context.원장템플릿Key
                    && row.원본의뢰유형 == context.외부참조.GetValueOrDefault("원천유형")
                    && row.원본의뢰Id == (sourceId ?? ""))
                    result.Add(new(C.CargoRequest, cargoId, context.원장Id));
            }
        }
        var outboundContexts = contexts.Where(x => (x.원장템플릿Key is CommunityLedgerTemplateKeys.WarehouseOutbound or CommunityLedgerTemplateKeys.SsalddelMart)
            && (x.원장Id == root.원장Id || root.포함원장목록.Any(r => r.원장Id == x.원장Id && r.역할 == 주문원장포함역할.창고출고))).ToArray();
        var ids = outboundContexts.Select(x => x.원장Id).ToArray();
        if (ids.Length == 0) return result;
        var outbounds = await db.Set<출고예정>().AsNoTracking().Where(x => Enumerable.Contains(ids, x.커뮤니티원장Id!)).ToArrayAsync(ct);
        var exact = outbounds.Where(x => outboundContexts.Any(c => c.원장Id == x.커뮤니티원장Id
            && c.원장템플릿Key == x.커뮤니티원장템플릿Key
            && c.외부참조.GetValueOrDefault("원천Id") == x.주문참조번호)).Select(x => x.Id).ToArray();
        var orderIds = await db.Set<분산재고예약Record>().AsNoTracking().Where(x => Enumerable.Contains(exact, x.OutboundPlanId))
            .Select(x => x.OrderId).Distinct().ToArrayAsync(ct);
        var orders = await db.Set<분산배송주문Record>().AsNoTracking().Where(x => Enumerable.Contains(orderIds, x.OrderId)).ToArrayAsync(ct);
        var waveIds = orders.Select(x => x.WaveId).Distinct().ToArray();
        var waves = await db.Set<분산배송WaveRecord>().AsNoTracking().Where(x => Enumerable.Contains(waveIds, x.WaveId)).ToArrayAsync(ct);
        foreach (var order in orders)
        {
            var connection = 분산주문원장Metadata.Connections(waves.Single(x => x.WaveId == order.WaveId)).GetValueOrDefault(order.OrderId);
            if (connection is null || connection.RootOrderLedgerId != root.원장Id || connection.OrderId != order.OrderId
                || connection.CargoRequestId != order.CargoRequestId || connection.SellerUserId != order.SellerUserId
                || connection.CustomerUserId != order.CustomerUserId) continue;
            // 기존 연결 Adapter에서 필수로 묶은 운송 참조가 제거/파기되면 새 통합 탐색으로 우회하지 않습니다.
            if (!result.Any(x => x.Kind == C.CargoRequest && x.Id == order.CargoRequestId)) continue;
            var reservations = await db.Set<분산재고예약Record>().AsNoTracking().Where(x => x.OrderId == order.OrderId)
                .Select(x => x.OutboundPlanId).ToArrayAsync(ct);
            if (reservations.Length == 0 || !reservations.All(exact.Contains)
                || !reservations.Order().SequenceEqual(connection.OutboundPlanIds.Order())) continue;
            var context = outboundContexts.First(x => outbounds.Any(o => o.커뮤니티원장Id == x.원장Id && reservations.Contains(o.Id)));
            result.Add(new(C.DistributedOrder, order.OrderId, context.원장Id));
        }
        return result;
    }

    private async Task<IReadOnlyList<주문귀속원가Dto>> Costs(IReadOnlyList<주문정산원천Dto> reads,
        IReadOnlyDictionary<string, NeighborhoodSettlementSourceSnapshot> current, string actor, CancellationToken ct)
    {
        // 출고 주문의 비용 부담자만 그 주문에 귀속된 비용을 읽습니다. 입고 작업자의 보수는 원천 정산에서 한 번 발생합니다.
        var eligible = reads.Where(x => x.SourceKind == C.DistributedOrder
            && current[x.SourceKind + ":" + x.SourceId].Participants.Any(p => p.UserId == actor
                && p.Roles.Any(r => r is "Owner" or "Seller"))).Select(x => x.SourceId).ToArray();
        if (eligible.Length == 0) return [];
        var rows = await db.Set<NeighborhoodInventoryCostAttributionRecord>().AsNoTracking()
            .Where(x => Enumerable.Contains(eligible, x.OrderId)).OrderBy(x => x.AttributionId).ToArrayAsync(ct);
        var sourceIds = rows.Select(x => x.SourceEntryId).Distinct().ToArray();
        var entries = await db.Set<NeighborhoodSettlementEntryRecord>().AsNoTracking().Where(x => Enumerable.Contains(sourceIds, x.EntryId)).ToArrayAsync(ct);
        var agreementIds = rows.SelectMany(x => new[] { x.AgreementId, x.SourceAgreementId }).Distinct().ToArray();
        var agreements = await db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking()
            .Where(x => Enumerable.Contains(agreementIds, x.AgreementId)).Select(x => new { x.AgreementId, x.SourceKind, x.SourceId }).ToArrayAsync(ct);
        var result = new List<주문귀속원가Dto>();
        foreach (var row in rows)
        {
            var source = entries.SingleOrDefault(x => x.EntryId == row.SourceEntryId);
            if (source is null) continue;
            var entry = JsonSerializer.Deserialize<NeighborhoodSettlementEntryDto>(source.DetailsJson, Json)!;
            if (entry.PayerUserId != actor) continue;
            var detail = JsonSerializer.Deserialize<NeighborhoodInventoryCostAttributionDto>(row.DetailsJson, Json)!;
            Require(detail.OrderId == row.OrderId && detail.SourceEntryId == row.SourceEntryId
                && detail.SourceAgreementId == row.SourceAgreementId && entry.EntryId == source.EntryId
                && entry.AgreementId == source.AgreementId && source.AgreementId == row.SourceAgreementId
                && agreements.Any(x => x.AgreementId == row.AgreementId && x.SourceKind == C.DistributedOrder && x.SourceId == row.OrderId)
                && agreements.Any(x => x.AgreementId == row.SourceAgreementId && x.SourceKind == C.DistributedLot && x.SourceId == detail.LotId),
                "NeighborhoodSettlementOrderCostMismatch");
            result.Add(new() { AttributionId = row.AttributionId, CalculationDigest = row.CalculationDigest,
                Attribution = detail, CostBearerUserId = actor, SourceEntryStatusCode = source.StatusCode,
                CurrentSourceEntryRevision = source.Revision, RecordedAtUtc = row.RecordedAtUtc });
        }
        return result;
    }

    private static 주문작업보수근거Dto Work(NeighborhoodSettlementCompletedWork work,
        IReadOnlyList<NeighborhoodSettlementSummaryDto> reads, string actor)
    {
        // 기존 entry.OriginRevision은 source의 합의 판본(해시이면 0)이고 work 판본은 실제 완료 영수증 판본입니다.
        // 서로 다른 판본을 같다고 보지 않으며 같은 작업 원천과 실제 증빙의 교집합으로 연결합니다.
        var linked = reads.SelectMany(x => x.Entries).Where(x => x.PayeeUserId == actor && x.Kind == work.Kind
            && x.OriginKind == work.OriginKind && x.OriginId == work.OriginId && x.OriginalEntryId is null
            && x.WorkEvidenceReferences.Intersect(work.EvidenceReferences, StringComparer.Ordinal).Any()).ToArray();
        var references = linked.Where(x => x.IncludedInDelivery || x.StatusCode == C.Expected).ToArray();
        var entries = linked.Where(x => !x.IncludedInDelivery && x.StatusCode != C.Expected).ToArray();
        // 정액/무료의 청구 수량 1은 물리 작업 수량이 아닙니다. 같은 완료 작업 전체를 덮는 약정입니다.
        var covered = entries.Any(x => x.Billing is C.Fixed or C.Free) ? work.Quantity
            : Math.Min(work.Quantity, entries.Where(x => x.Billing == C.PerUnit).Sum(x => x.Quantity ?? 0));
        var terms = reads.SelectMany(x => x.Agreement.Terms.Lines).Where(x => x.PayeeUserId == actor && x.Kind == work.Kind).ToArray();
        return new()
        {
            Kind = work.Kind, OriginKind = work.OriginKind, OriginId = work.OriginId, OriginRevision = work.OriginRevision,
            ActorUserId = actor, ConfirmedQuantity = work.Quantity, AccruedQuantity = covered, CompletedAtUtc = work.OccurredAtUtc,
            EvidenceReferences = work.EvidenceReferences, EntryIds = entries.Select(x => x.EntryId).ToArray(),
            ReferenceEntryIds = references.Select(x => x.EntryId).ToArray(),
            CompensationStatusCode = entries.Length > 0 ? entries.Any(x => x.Billing is not C.Fixed and not C.Free and not C.PerUnit)
                ? "AccrualCoverageUnconfirmed" : covered < work.Quantity ? "PartiallyAccrued" : "Accrued"
                : references.Length > 0 ? "OriginalLedgerReference"
                : terms.Length == 0 ? "AgreementRequired"
                : terms.Any(x => x.Billing != C.Free && x.AmountKrw is null && x.UnitPriceKrw is null) ? "RateUnresolved" : "AccrualPending"
        };
    }

    private static IReadOnlyList<주문금액분류Dto> Groups(IReadOnlyList<주문정산원천Dto> sources)
    {
        var reads = sources.SelectMany(x => x.Settlements).ToArray();
        var entries = reads.SelectMany(x => x.Entries).DistinctBy(x => x.EntryId).ToArray();
        var payments = reads.SelectMany(x => x.Payments).DistinctBy(x => x.PaymentId).ToArray();
        var terms = reads.SelectMany(x => x.Agreement.Terms.Lines.Select(line => (x.Agreement.AgreementId, Line: line))).ToArray();
        static string Accounting(bool reference, bool internalEntry, bool transit)
            => reference ? "DeliveryReferenceOnly" : internalEntry ? "InternalTransfer" : transit ? "TransitFunds" : "Payable";
        var groups = entries.Select(x => (x.Kind, AccountingCode: Accounting(x.IncludedInDelivery, x.IsInternal, x.IsTransit)))
            .Concat(terms.Select(x => (x.Line.Kind, AccountingCode: Accounting(x.Line.IncludedInDelivery,
                x.Line.PayerUserId == x.Line.PayeeUserId, x.Line.Kind == C.OwnerRemittance))))
            .Distinct().OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.AccountingCode, StringComparer.Ordinal);
        return groups.Select(group =>
        {
            var selected = entries.Where(x => x.Kind == group.Kind
                && Accounting(x.IncludedInDelivery, x.IsInternal, x.IsTransit) == group.AccountingCode).ToArray();
            var unknown = selected.Count(x => x.AmountKrw is null);
            var pending = terms.Count(t => t.Line.Kind == group.Kind
                && Accounting(t.Line.IncludedInDelivery, t.Line.PayerUserId == t.Line.PayeeUserId, t.Line.Kind == C.OwnerRemittance) == group.AccountingCode
                && !selected.Any(e => e.AgreementId == t.AgreementId && e.LineKey == t.Line.LineKey));
            var reference = group.AccountingCode == "DeliveryReferenceOnly";
            var entryIds = selected.Select(x => x.EntryId).ToHashSet(StringComparer.Ordinal);
            var signs = selected.ToDictionary(x => x.EntryId, x => x.AmountKrw < 0 ? -1m : 1m, StringComparer.Ordinal);
            var allocations = payments.SelectMany(p => p.Allocations.Where(a => entryIds.Contains(a.EntryId))
                .Select(a => new { Payment = p, Allocation = a })).ToArray();
            return new 주문금액분류Dto
            {
                Kind = group.Kind, AccountingCode = group.AccountingCode,
                ReferenceEntryCount = reference ? selected.Length : 0,
                ReferenceAmountKrw = reference && selected.Length > 0 && unknown == 0 ? selected.Sum(x => x.AmountKrw!.Value) : null,
                UnknownReferenceCount = reference ? unknown : 0,
                AccruedEntryCount = reference ? 0 : selected.Length,
                KnownAccruedAmountKrw = reference ? 0 : selected.Sum(x => x.AmountKrw ?? 0),
                CompleteAccruedAmountKrw = !reference && selected.Length > 0 && unknown == 0 ? selected.Sum(x => x.AmountKrw!.Value) : null,
                UnknownAccruedCount = reference ? 0 : unknown, PendingAgreementLineCount = pending,
                ReportedPaymentAmountKrw = allocations.Sum(x => x.Allocation.AmountKrw * signs[x.Allocation.EntryId]),
                // 기존 지급 원장과 같은 저장 배분 순서를 사용하며 비율 추정으로 바꾸지 않습니다.
                RecipientConfirmedAmountKrw = allocations.Sum(x => ConfirmedAllocation(x.Payment, x.Allocation.EntryId) * signs[x.Allocation.EntryId])
            };
        }).ToArray();
    }

    private static decimal ConfirmedAllocation(NeighborhoodSettlementPaymentDto payment, string entryId)
    {
        var remaining = payment.RecipientConfirmedAmountKrw;
        foreach (var allocation in payment.Allocations)
        {
            var confirmed = Math.Min(allocation.AmountKrw, remaining);
            if (allocation.EntryId == entryId) return confirmed;
            remaining -= confirmed;
        }
        return 0;
    }
}
