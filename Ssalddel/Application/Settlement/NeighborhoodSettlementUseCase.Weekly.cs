using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.정산;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Application.Settlement;

public sealed partial class NeighborhoodSettlementUseCase
{
    /// <summary>Wednesday completion week in KST, independent of later accrual/receipt/payout dates. Read-only and existing Simulation gate.</summary>
    public async Task<WorkerWeeklyStatementDto> WeeklyStatementAsync(string actor, DateOnly? completionDateKst, CancellationToken ct)
    {
        Gate(actor);
        var date = completionDateKst ?? DateOnly.FromDateTime(Now.AddHours(9));
        Require(date.Year is >= 2000 and <= 2100, "WorkerWeeklyDateInvalid", 400);
        var (week, start, end) = WorkerWeeklyStatementProjector.CompletionWeek(date);
        var lines = new List<WorkerWeeklyStatementLineDto>();
        var food = await db.음식주문기사정산.AsNoTracking().Where(x => x.기사Id == actor
            && x.전달완료시각Utc >= start && x.전달완료시각Utc < end).ToArrayAsync(ct);
        foreach (var row in food)
            lines.Add(new()
            {
                StableId = "food:" + row.정산StableId, SourceKind = C.FoodOrder, SourceId = row.주문번호,
                ActivityKind = C.Delivery, CompletedAtUtc = row.전달완료시각Utc, AccruedAmountKrw = row.세전대금,
                NetAmountKrw = row.수령액, ReadinessCode = row.정산상태Code, WaitingReason = row.보류사유,
                PaymentConditionReady = row.정산상태Code == 음식주문기사정산상태Code.모의검증준비,
                PaymentStatusCode = row.지급상태Code, IsActualBankVerified = false
            });

        var transports = await db.운송원장.AsNoTracking().Where(x => x.배차업무유형 == 상태값.배차업무유형.용달운송
            && (x.확정기사Id == actor || (x.확정기사Id == null || x.확정기사Id == "") && x.기사_운송자 == actor)
            && x.상태 == 상태값.배차상태.인수완료 && x.도착 >= start && x.도착 < end).ToArrayAsync(ct);
        var account = await db.Set<기사정산계좌>().AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == actor, ct);
        foreach (var transport in transports)
        {
            var id = string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id;
            var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == id, ct);
            if (request?.모의처리처수행 == true) continue;
            var fare = request is null ? null : await db.운임구성.AsNoTracking()
                .SingleOrDefaultAsync(x => x.의뢰Id == id && x.Id == request.운임구성Id, ct);
            var amount = fare?.기사지급예정운임;
            if (!string.IsNullOrWhiteSpace(fare?.합의요금표SnapshotJson)
                && 기사지급준비UseCase.저장합의조회(fare, request, transport) is null) amount = null;
            var held = await db.비정상운송사건.AsNoTracking().AnyAsync(x => x.운송의뢰Id == id
                && (x.정산보류적용여부 || x.상태Code == "OperationsReviewPending"), ct);
            var readiness = Ssalddel.Application.Settlement.기사지급준비Policy.판정(request, amount, account is not null,
                account?.확인상태 == 기사정산계좌확인상태.확인완료, held);
            var payout = await db.기사운송대금지급요청.AsNoTracking().Where(x => x.운송Id == transport.Id && x.기사Id == actor)
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            var batches = await db.Set<기사운송대금정산배치행>().AsNoTracking()
                .Where(x => x.운송Id == transport.Id && x.정산배치.기사Id == actor)
                .Select(x => x.정산배치.PayoutScheduledAtUtc).Distinct().ToArrayAsync(ct);
            lines.Add(new()
            {
                StableId = "cargo:" + transport.Id, SourceKind = C.CargoRequest, SourceId = id, ActivityKind = C.Delivery,
                CompletedAtUtc = transport.도착!.Value, AccruedAmountKrw = amount,
                PaymentConditionReady = readiness.IsReady, ReadinessCode = readiness.Code, WaitingReason = readiness.Message,
                PaymentStatusCode = payout?.상태코드 ?? "NotRequested", PayoutScheduledAtUtc = batches.Length == 1 ? batches[0] : null,
                IsActualBankVerified = false
            });
        }

        // The existing party-filtered projection checks latest source/receipt conditions. Cost attribution is never income.
        var summaries = await MySummaryAsync(actor, ct);
        var excluded = 0; var unassigned = 0;
        foreach (var summary in summaries)
        {
            var source = await sources.ResolveAsync(summary.Agreement.SourceKind, summary.Agreement.SourceId, ct);
            var versions = await db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking()
                .Where(x => x.AgreementId == summary.Agreement.AgreementId).ToArrayAsync(ct);
            foreach (var entry in summary.Entries.Where(x => x.PayeeUserId == actor && !x.IsInternal && !x.IsTransit))
            {
                var original = entry.OriginalEntryId is { } originalId ? summary.Entries.SingleOrDefault(x => x.EntryId == originalId) : entry;
                if (original is null) { unassigned++; continue; }
                var workKind = original.Kind;
                if (original.IncludedInDelivery || workKind == C.Delivery &&
                    (summary.Agreement.SourceKind is C.FoodOrder or C.CargoRequest || source.CanonicalDeliveryOrigin is not null))
                { excluded++; continue; }
                if (workKind is not (C.Picking or C.Packing or C.Inbound or C.Storage or C.Delivery or C.ActivityReward or C.SalesAgency)) continue;
                var historical = versions.SingleOrDefault(x => x.TermsVersion == original.TermsVersion);
                var workflow = historical is null ? null : Deserialize<NeighborhoodSettlementAgreementRequest>(historical.TermsJson).WorkflowKind;
                // Match the same specialist event and beneficiary used by the original accrual, without using the accrual date.
                var completedKind = workKind == C.SalesAgency ? C.ProductSale
                    : workKind == C.Storage && original.Billing == C.PerUnit
                        ? workflow == NeighborhoodSettlementWorkflowCodes.InboundStorage ? C.Inbound : C.ProductSale : workKind;
                var at = original.PeriodEndUtc ?? source.CompletedWork.Where(x => x.OriginKind == original.OriginKind
                        && x.OriginId == original.OriginId && x.Kind == completedKind
                        && (workKind == C.SalesAgency ? x.SellerUserId == actor
                            : workKind == C.Storage ? x.SpaceOwnerUserId == actor : x.ActorUserId == actor))
                    .Select(x => x.OccurredAtUtc).Max();
                // No created/updated/accrual timestamp fallback: an unknown completion date is not this week's income.
                if (at is null) { unassigned++; continue; }
                if (at < start || at >= end) continue;
                var reports = summary.Payments.Where(x => x.PayeeUserId == actor || x.PayerUserId == actor).ToArray();
                var sign = entry.AmountKrw < 0 ? -1m : 1m;
                lines.Add(new()
                {
                    StableId = "activity:" + entry.EntryId, SourceKind = summary.Agreement.SourceKind,
                    SourceId = summary.Agreement.SourceId, ActivityKind = entry.Kind, AgreementId = summary.Agreement.AgreementId,
                    CompletedAtUtc = at.Value, AccruedAmountKrw = entry.AmountKrw,
                    PaymentConditionReady = entry.StatusCode == C.Confirmed && entry.PaymentReleaseReady == true,
                    ReadinessCode = entry.StatusCode, WaitingReason = entry.PaymentWaitingReasonCode ?? "",
                    PaymentStatusCode = "PartyStatementsOnly",
                    ReportedPaymentAmountKrw = sign * reports.SelectMany(x => x.Allocations).Where(x => x.EntryId == entry.EntryId).Sum(x => x.AmountKrw),
                    RecipientConfirmedAmountKrw = sign * reports.Sum(x => ConfirmedAllocation(x, entry.EntryId)), IsActualBankVerified = false
                });
            }
        }
        var statement = WorkerWeeklyStatementProjector.Project(actor, week, start, end, lines, unassigned, excluded);
        statement.ObservedAtUtc = Now;
        return statement;
    }
}
