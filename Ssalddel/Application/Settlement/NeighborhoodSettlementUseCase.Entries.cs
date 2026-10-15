using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.정산;

namespace Ssalddel.Application.Settlement;

public sealed partial class NeighborhoodSettlementUseCase
{
    private static NeighborhoodSettlementEntryDto Entry(NeighborhoodSettlementEntryRecord row)
    {
        var entry = Deserialize<NeighborhoodSettlementEntryDto>(row.DetailsJson);
        entry.StatusCode = row.StatusCode; entry.Revision = row.Revision; return entry;
    }
    private static void SaveEntry(NeighborhoodSettlementEntryRecord row, NeighborhoodSettlementEntryDto entry)
    { row.StatusCode = entry.StatusCode; row.Revision++; entry.Revision = row.Revision; entry.AllowedActions = []; row.DetailsJson = Serialize(entry); }
    private async Task<NeighborhoodSettlementEntryRecord> OwnedEntry(string agreement, string entryId, string actor, CancellationToken ct)
    {
        var row = await db.Set<NeighborhoodSettlementEntryRecord>().SingleOrDefaultAsync(x => x.AgreementId == agreement && x.EntryId == entryId, ct)
            ?? throw new NeighborhoodSettlementException("NeighborhoodSettlementEntryNotFound", 404);
        Require(EntryParty(Entry(row), actor), "NeighborhoodSettlementEntryNotFound", 404); return row;
    }

    public Task<NeighborhoodSettlementReceiptDto> AccrueAsync(string id, string actor, NeighborhoodSettlementAccrualRequest r, CancellationToken ct)
        => Execute(actor, "Accrue", id, r, async () =>
        {
            var row = await Owned(id, actor, ct); Revision(row, r);
            Require(row.StatusCode == NeighborhoodSettlementCodes.Active, "NeighborhoodSettlementLatestConsentRequired");
            var terms = Terms(row); var source = await CurrentSource(row, ct);
            // 구형 혼합 약정도 새 작업의 경계를 우회하는 추가 청구 근거로 사용하지 않습니다.
            Require(terms.ExpiresAtUtc is null || terms.ExpiresAtUtc > Now, "NeighborhoodSettlementTermsExpired");
            Require(r.Lines is { Count: > 0 and <= 64 } && r.Lines.Select(x => x.LineKey).Distinct(StringComparer.Ordinal).Count() == r.Lines.Count,
                "NeighborhoodSettlementAccrualLinesInvalid", 400);
            Text(r.OriginKind, 40); Text(r.OriginId, 120);
            var allRows = await db.Set<NeighborhoodSettlementEntryRecord>().Where(x => x.AgreementId == id).ToArrayAsync(ct);
            var existing = allRows.Select(Entry).ToList(); var created = new List<NeighborhoodSettlementEntryRecord>();
            foreach (var requested in r.Lines.OrderBy(x => terms.Lines.SingleOrDefault(t => t.LineKey == x.LineKey)?.Kind == NeighborhoodSettlementCodes.OwnerRemittance ? 1 : 0))
            {
                var line = terms.Lines.SingleOrDefault(x => x.LineKey == requested.LineKey)
                    ?? throw new NeighborhoodSettlementException("NeighborhoodSettlementLineNotFound", 404);
                Require(LineParty(line, actor), "NeighborhoodSettlementAccrualPartyRequired", 403);
                ValidateWorkflowLines(new() { SourceKind = terms.SourceKind, Lines = [line] });
                Require(Resolved(line, source), "NeighborhoodSettlementAmountUnresolved");
                var entry = await CreateAccrual(row, line, requested, r, source, existing, ct);
                created.Add(entry); existing.Add(Entry(entry));
            }
            // 판매자가 총액을 먼저 수금하는 약정은 동일 총액의 소유자 송금 채무도 같은 처리에서 생성합니다.
            foreach (var sale in created.Where(x => x.Kind == NeighborhoodSettlementCodes.ProductSale).ToArray())
            {
                if (terms.GoodsPaymentRoute != NeighborhoodSettlementCodes.SellerCollect) continue;
                var line = terms.Lines.Single(x => x.Kind == NeighborhoodSettlementCodes.OwnerRemittance);
                if (created.Any(x => x.Kind == NeighborhoodSettlementCodes.OwnerRemittance)) continue;
                var saleEntry = Entry(sale);
                var remittance = await CreateAccrual(row, line, new() { LineKey = line.LineKey, Quantity = saleEntry.Quantity }, r, source, existing, ct);
                created.Add(remittance); existing.Add(Entry(remittance));
            }
            db.AddRange(created); row.Revision++; row.UpdatedAtUtc = Now; AppendHistory(row, actor, "Accrue", Now);
            return Result(row, r.RequestId, "Accrue", created.Select(x => x.EntryId).ToArray());
        }, ct);

    private async Task<NeighborhoodSettlementEntryRecord> CreateAccrual(NeighborhoodSettlementAgreementRecord row,
        NeighborhoodSettlementTermLine line, NeighborhoodSettlementAccrualLine requested, NeighborhoodSettlementAccrualRequest request,
        NeighborhoodSettlementSourceSnapshot source, IReadOnlyList<NeighborhoodSettlementEntryDto> existing, CancellationToken ct)
    {
        var units = requested.Quantity ?? 1m;
        Require(Quantity(units), "NeighborhoodSettlementQuantityInvalid", 400);
        // 같은 실제 완료 근거를 다른 합의/조건 판본으로 다시 청구하지 않습니다.
        var globallyRecorded = (await (from e in db.Set<NeighborhoodSettlementEntryRecord>().AsNoTracking()
            join a in db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking() on e.AgreementId equals a.AgreementId
            where e.Kind == line.Kind && (a.SourceKind == source.SourceKind && a.SourceId == source.SourceId
                || e.OriginKind == request.OriginKind && e.OriginId == request.OriginId)
            select e).ToArrayAsync(ct)).Select(Entry);
        var combined = existing.Concat(globallyRecorded).DistinctBy(x => x.EntryId).ToArray();
        var same = combined.Where(x => x.Kind == line.Kind && (x.PayeeUserId == line.PayeeUserId || line.Kind == NeighborhoodSettlementCodes.ProductSale
                || source.SourceKind == NeighborhoodSettlementCodes.MartOrder && x.OriginKind == request.OriginKind && x.OriginId == request.OriginId)
            && x.OriginalEntryId is null).ToArray();
        var originSame = same.Where(x => x.OriginKind == request.OriginKind && x.OriginId == request.OriginId).ToArray();
        IReadOnlyList<string> evidence; DateTime? start = null, end = null; decimal amount;
        if (line.Billing == NeighborhoodSettlementCodes.StoragePeriod)
        {
            Utc(requested.PeriodStartUtc); Utc(requested.PeriodEndUtc); start = requested.PeriodStartUtc; end = requested.PeriodEndUtc;
            Require(start.HasValue && end.HasValue && start < end && start == line.PeriodStartUtc && end == line.PeriodEndUtc,
                "NeighborhoodSettlementStoragePeriodRequired", 400);
            Require(source.StorageStartedAtUtc.HasValue && source.StorageStartedAtUtc.Value.Kind == DateTimeKind.Utc
                && source.ObservedAtUtc.Kind == DateTimeKind.Utc && start >= source.StorageStartedAtUtc && end <= Now && end <= source.ObservedAtUtc
                && StorageEvidence(source).Count > 0,
                "NeighborhoodSettlementStoragePeriodNotCompleted");
            Require(source.StorageIntervals.Any(x => x.StartUtc.Kind == DateTimeKind.Utc && (x.EndUtc is null || x.EndUtc.Value.Kind == DateTimeKind.Utc)
                && x.StartUtc <= start && (x.EndUtc is null || x.EndUtc >= end) && x.MinimumQuantity >= units),
                "NeighborhoodSettlementStoragePeriodEvidenceRequired");
            Require(request.OriginKind == source.SourceKind && request.OriginId == source.SourceId, "NeighborhoodSettlementOriginMismatch");
            Require(!same.Any(x => x.PeriodStartUtc < end && x.PeriodEndUtc > start), "NeighborhoodSettlementStoragePeriodOverlap");
            Require(source.ActualCompletedUnits > 0 && units <= source.ActualCompletedUnits
                && (line.Quantity is null || units <= line.Quantity), "NeighborhoodSettlementCompletedQuantityExceeded");
            amount = line.AmountKrw!.Value; evidence = StorageEvidence(source);
        }
        else if (line.Kind == NeighborhoodSettlementCodes.Storage && line.Billing == NeighborhoodSettlementCodes.Free)
        {
            Require(requested.PeriodStartUtc is null && requested.PeriodEndUtc is null, "NeighborhoodSettlementUnexpectedPeriod", 400);
            Require(source.SourceKind == NeighborhoodSettlementCodes.DistributedLot
                && request.OriginKind == source.SourceKind && request.OriginId == source.SourceId,
                "NeighborhoodSettlementOriginMismatch");
            Require(source.ObservedAtUtc.Kind == DateTimeKind.Utc && source.ObservedAtUtc <= Now
                && StorageEvidence(source).Count > 0
                && source.StorageIntervals.Any(x => x.StartUtc.Kind == DateTimeKind.Utc && x.StartUtc <= source.ObservedAtUtc
                    && (x.EndUtc is null || x.EndUtc.Value.Kind == DateTimeKind.Utc && x.EndUtc > x.StartUtc && x.EndUtc <= source.ObservedAtUtc)
                    && x.MinimumQuantity >= units && x.MinimumQuantity >= (line.Quantity ?? units)),
                "NeighborhoodSettlementStoragePeriodEvidenceRequired");
            Require((line.Quantity is null || units <= line.Quantity) && !same.Any(), "NeighborhoodSettlementCompletedQuantityExceeded");
            amount = 0m; evidence = StorageEvidence(source);
        }
        else
        {
            Require(requested.PeriodStartUtc is null && requested.PeriodEndUtc is null, "NeighborhoodSettlementUnexpectedPeriod", 400);
            var workKind = line.Kind is NeighborhoodSettlementCodes.OwnerRemittance or NeighborhoodSettlementCodes.SalesAgency
                || line.Kind == NeighborhoodSettlementCodes.Storage && line.Billing == NeighborhoodSettlementCodes.PerUnit
                ? NeighborhoodSettlementCodes.ProductSale : line.Kind;
            if (line.Kind == NeighborhoodSettlementCodes.Storage && line.Billing == NeighborhoodSettlementCodes.PerUnit
                && Terms(row).WorkflowKind == NeighborhoodSettlementWorkflowCodes.InboundStorage)
                workKind = NeighborhoodSettlementCodes.Inbound;
            var works = source.CompletedWork.Where(x => x.Kind == workKind && x.OriginKind == request.OriginKind && x.OriginId == request.OriginId
                && (x.OccurredAtUtc.HasValue ? x.OccurredAtUtc.Value.Kind == DateTimeKind.Utc && x.OccurredAtUtc <= Now
                    : workKind == NeighborhoodSettlementCodes.ProductSale && source.ObservedAtUtc.Kind == DateTimeKind.Utc && source.ObservedAtUtc <= Now)
                && x.Quantity > 0 && x.EvidenceReferences.Count > 0).ToArray();
            if (line.Kind is NeighborhoodSettlementCodes.Inbound or NeighborhoodSettlementCodes.Picking or NeighborhoodSettlementCodes.Packing)
                works = works.Where(x => x.ActorUserId == line.PayeeUserId
                    && (!Role(source, line.PayerUserId, "Owner") || x.GoodsOwnerUserId == line.PayerUserId)
                    && (!Role(source, line.PayerUserId, "Seller") || x.SellerUserId == line.PayerUserId)).ToArray();
            if (line.Kind is NeighborhoodSettlementCodes.ProductSale or NeighborhoodSettlementCodes.OwnerRemittance)
                works = works.Where(x => x.GoodsOwnerUserId == Terms(row).GoodsOwnerUserId && !string.IsNullOrWhiteSpace(x.GoodsOwnerUserId)
                    && (line.Kind != NeighborhoodSettlementCodes.OwnerRemittance || x.SellerUserId == line.PayerUserId)).ToArray();
            if (line.Kind == NeighborhoodSettlementCodes.Storage && line.Billing == NeighborhoodSettlementCodes.PerUnit)
                works = works.Where(x => x.SpaceOwnerUserId == line.PayeeUserId
                    && (!Role(source, line.PayerUserId, "Owner") || x.GoodsOwnerUserId == line.PayerUserId)
                    && (!Role(source, line.PayerUserId, "Seller") || x.SellerUserId == line.PayerUserId)).ToArray();
            if (line.Kind == NeighborhoodSettlementCodes.SalesAgency)
                works = works.Where(x => x.SellerUserId == line.PayeeUserId && x.GoodsOwnerUserId == line.PayerUserId).ToArray();
            Require(works.Length > 0, "NeighborhoodSettlementCompletedWorkRequired");
            if (source.SourceKind == NeighborhoodSettlementCodes.MartOrder)
            {
                var consents = Deserialize<List<NeighborhoodSettlementConsentDto>>(row.ConsentsJson);
                // 포장은 실제 시작이 별도 기록되지 않으므로 선행 피킹 시작까지 동의한 단가만 적용합니다.
                // 완료 후 새 판본·철회 후 재동의로 이미 한 일을 새 고가 단가에 소급 청구하지 않습니다.
                Require(works.All(work => work.AgreementDeadlineAtUtc is { Kind: DateTimeKind.Utc } deadline
                    && deadline <= Now && work.OccurredAtUtc is { Kind: DateTimeKind.Utc } completed && deadline <= completed
                    && RequiredConsents(Terms(row)).All(user => consents.Any(consent => consent.UserId == user
                        && consent.TermsVersion == row.TermsVersion && consent.Granted
                        && consent.RecordedAtUtc.Kind == DateTimeKind.Utc && consent.RecordedAtUtc <= deadline))),
                    "NeighborhoodSettlementWorkPriorConsentRequired");
                // 실제 작업 전체를 덮는 정액/무료 보수는 일부 수량 청구와 혼합하여 중복 청구하지 않습니다.
                if (line.Billing != NeighborhoodSettlementCodes.PerUnit)
                    Require(!originSame.Any(), "NeighborhoodSettlementFixedChargeAlreadyAccrued");
                else
                    Require(!originSame.Any(entry => entry.Billing != NeighborhoodSettlementCodes.PerUnit),
                        "NeighborhoodSettlementCompletedQuantityExceeded");
            }
            var completed = works.Sum(x => x.Quantity); evidence = works.SelectMany(x => x.EvidenceReferences).Distinct(StringComparer.Ordinal).ToArray();
            if (line.Billing == NeighborhoodSettlementCodes.PerUnit)
            {
                Require(requested.Quantity.HasValue && units <= line.Quantity
                    && same.Sum(x => x.Quantity ?? 0m) + units <= line.Quantity
                    && originSame.Sum(x => x.Quantity ?? 0m) + units <= completed,
                    "NeighborhoodSettlementCompletedQuantityExceeded");
                amount = decimal.Round(line.UnitPriceKrw!.Value * units, 2);
            }
            else
            {
                Require(units == 1m && !same.Any(), "NeighborhoodSettlementFixedChargeAlreadyAccrued");
                Require(completed >= (line.Quantity ?? 1m), "NeighborhoodSettlementCompletedQuantityExceeded");
                amount = line.Billing == NeighborhoodSettlementCodes.Free ? 0m : line.AmountKrw!.Value;
            }
        }
        Require(Money(amount), "NeighborhoodSettlementAmountInvalid", 400);
        if (line.Kind == NeighborhoodSettlementCodes.OwnerRemittance)
        {
            var saleTerms = Terms(row).Lines.Single(x => x.LineKey == line.OriginalLineKey);
            var sales = existing.Where(x => x.Kind == NeighborhoodSettlementCodes.ProductSale && x.LineKey == saleTerms.LineKey
                && x.OriginKind == request.OriginKind && x.OriginId == request.OriginId).Sum(x => x.AmountKrw ?? 0);
            Require(originSame.Sum(x => x.AmountKrw ?? 0) + amount <= sales, "NeighborhoodSettlementOwnerRemittanceGrossExceeded");
        }
        var originalDelivery = line.Kind == NeighborhoodSettlementCodes.Delivery && !string.IsNullOrWhiteSpace(source.CanonicalDeliveryOrigin);
        if (originalDelivery)
            Require(!await db.Set<NeighborhoodSettlementEntryRecord>().AnyAsync(x => x.CanonicalDeliveryOrigin == source.CanonicalDeliveryOrigin, ct),
                "NeighborhoodSettlementOriginalDeliveryAlreadyReferenced");
        var key = Digest(new { source.SourceKind, source.SourceId, line.Kind, line.PayeeUserId, request.OriginKind, request.OriginId,
            Start = start, End = end, Offset = originSame.Sum(x => x.Quantity ?? 0m), Units = units });
        var entry = new NeighborhoodSettlementEntryDto
        {
            EntryId = NewId("nse"), AgreementId = row.AgreementId, TermsVersion = row.TermsVersion, LineKey = line.LineKey,
            Kind = line.Kind, Billing = line.Billing, AmountKrw = amount, UnitPriceKrw = line.UnitPriceKrw, Quantity = units,
            PeriodStartUtc = start, PeriodEndUtc = end, PayerUserId = line.PayerUserId, PayeeUserId = line.PayeeUserId,
            BeneficiaryRole = line.BeneficiaryRole, OriginKind = request.OriginKind, OriginId = request.OriginId,
            OriginRevision = long.TryParse(source.SourceVersion, out var version) ? version : 0,
            CalculationBasis = originalDelivery ? $"기존 배송 원장 참고 · 신규 지급채무 아님 · 기존 상태: {source.OriginalExternalPaymentStatus ?? "미확인"}"
                : line.Billing == NeighborhoodSettlementCodes.PerUnit ? $"합의 단가 {line.UnitPriceKrw} × 확인된 처리량 {units}"
                : line.Billing == NeighborhoodSettlementCodes.StoragePeriod ? $"실제 보관 시작 이후 경과한 합의 기간 {start:O} ~ {end:O}"
                : line.Billing == NeighborhoodSettlementCodes.Free ? "당사자가 명시적으로 합의한 무료" : "합의 고정금액 · 원본 업무 완료 근거 대조",
            IsInternal = line.PayerUserId == line.PayeeUserId, IsTransit = line.Kind == NeighborhoodSettlementCodes.OwnerRemittance,
            IncludedInDelivery = line.IncludedInDelivery || originalDelivery, WorkEvidenceReferences = evidence,
            StatusCode = originalDelivery ? NeighborhoodSettlementCodes.Expected : NeighborhoodSettlementCodes.Accrued, Revision = 1, RecordedAtUtc = Now
        };
        return new() { EntryId = entry.EntryId, AgreementId = row.AgreementId, TermsVersion = row.TermsVersion, LineKey = line.LineKey,
            Kind = line.Kind, OriginKind = request.OriginKind, OriginId = request.OriginId, OriginRevision = entry.OriginRevision,
            AccrualKey = key, CanonicalDeliveryOrigin = originalDelivery ? source.CanonicalDeliveryOrigin : null,
            DetailsJson = Serialize(entry), StatusCode = entry.StatusCode, RecordedAtUtc = Now };
    }

    public Task<NeighborhoodSettlementReceiptDto> ConfirmEntryAsync(string id, string actor, NeighborhoodSettlementEntryConfirmRequest r, CancellationToken ct)
        => Execute(actor, "ConfirmEntry", id, r, async () =>
        {
            var agreement = await Owned(id, actor, ct); Revision(agreement, r);
            var row = await OwnedEntry(id, r.EntryId, actor, ct); var entry = Entry(row);
            Require(!entry.IncludedInDelivery && entry.AmountKrw.HasValue, "NeighborhoodSettlementEntryNotConfirmable");
            if (entry.OriginalEntryId is null)
            {
                var adjustments = (await db.Set<NeighborhoodSettlementEntryRecord>().Where(x => x.OriginalEntryId == entry.EntryId).ToArrayAsync(ct)).Select(Entry);
                Require(!adjustments.Any(x => x.AmountKrw < 0 && x.StatusCode != NeighborhoodSettlementCodes.Confirmed),
                    "NeighborhoodSettlementAdjustmentConfirmationRequired");
            }
            Require(entry.StatusCode is NeighborhoodSettlementCodes.Accrued or NeighborhoodSettlementCodes.Confirmed or NeighborhoodSettlementCodes.Disputed,
                "NeighborhoodSettlementEntryStateConflict");
            entry.ConfirmationUserIds = entry.ConfirmationUserIds.Append(actor).Distinct(StringComparer.Ordinal).ToArray();
            var parties = new[] { entry.PayerUserId, entry.PayeeUserId }.Distinct(StringComparer.Ordinal).ToArray();
            if (parties.All(x => entry.ConfirmationUserIds.Contains(x, StringComparer.Ordinal)))
            { entry.StatusCode = NeighborhoodSettlementCodes.Confirmed; entry.DisputeReason = null; }
            SaveEntry(row, entry); agreement.Revision++; agreement.UpdatedAtUtc = Now; AppendHistory(agreement, actor, "ConfirmEntry", Now);
            return Result(agreement, r.RequestId, "ConfirmEntry", [entry.EntryId]);
        }, ct);

    public Task<NeighborhoodSettlementReceiptDto> DisputeAsync(string id, string actor, NeighborhoodSettlementDisputeRequest r, CancellationToken ct)
        => Execute(actor, "Dispute", id, r, async () =>
        {
            var agreement = await Owned(id, actor, ct); Revision(agreement, r);
            var row = await OwnedEntry(id, r.EntryId, actor, ct); var entry = Entry(row);
            Require(!entry.IncludedInDelivery, "NeighborhoodSettlementOriginalLedgerReadOnly");
            if (entry.OriginalEntryId is not null)
            {
                var payments = (await db.Set<NeighborhoodSettlementPaymentRecord>().Where(x => x.AgreementId == id).ToArrayAsync(ct)).Select(Payment);
                Require(!payments.Any(x => x.Allocations.Any(a => a.EntryId == entry.EntryId)), "NeighborhoodSettlementPaidAdjustmentRequiresCorrection");
            }
            entry.DisputeReason = Text(r.Reason, 1000); entry.StatusCode = NeighborhoodSettlementCodes.Disputed;
            entry.ConfirmationUserIds = []; SaveEntry(row, entry);
            agreement.Revision++; agreement.UpdatedAtUtc = Now; AppendHistory(agreement, actor, "Dispute", Now);
            return Result(agreement, r.RequestId, "Dispute", [entry.EntryId]);
        }, ct);

    public Task<NeighborhoodSettlementReceiptDto> AdjustmentAsync(string id, string actor, NeighborhoodSettlementAdjustmentRequest r, CancellationToken ct)
        => Execute(actor, "Adjustment", id, r, async () =>
        {
            var agreement = await Owned(id, actor, ct); Revision(agreement, r);
            var originalRow = await OwnedEntry(id, r.OriginalEntryId, actor, ct); var original = Entry(originalRow);
            Require(!original.IncludedInDelivery && original.OriginalEntryId is null && original.AmountKrw.HasValue,
                "NeighborhoodSettlementOriginalLedgerReadOnly");
            Require(r.AmountKrw != 0 && Money(decimal.Abs(r.AmountKrw)), "NeighborhoodSettlementAdjustmentAmountInvalid", 400);
            var previous = await db.Set<NeighborhoodSettlementEntryRecord>().Where(x => x.OriginalEntryId == original.EntryId).ToArrayAsync(ct);
            Require(original.AmountKrw + previous.Select(Entry).Where(x => x.AmountKrw < 0 || x.StatusCode == NeighborhoodSettlementCodes.Confirmed)
                .Sum(x => x.AmountKrw ?? 0m) + r.AmountKrw >= 0,
                "NeighborhoodSettlementAdjustmentExceedsOriginal");
            var entry = new NeighborhoodSettlementEntryDto
            {
                EntryId = NewId("nse"), AgreementId = id, TermsVersion = original.TermsVersion, LineKey = original.LineKey,
                Kind = NeighborhoodSettlementCodes.Adjustment, Billing = NeighborhoodSettlementCodes.Fixed, AmountKrw = r.AmountKrw,
                PayerUserId = original.PayerUserId, PayeeUserId = original.PayeeUserId, BeneficiaryRole = original.BeneficiaryRole,
                OriginKind = original.OriginKind, OriginId = original.OriginId, OriginRevision = original.OriginRevision,
                OriginalEntryId = original.EntryId, IsInternal = original.IsInternal, IsTransit = original.IsTransit,
                CalculationBasis = "원 금액을 덮어쓰지 않는 정정: " + Text(r.Reason, 1000),
                WorkEvidenceReferences = original.WorkEvidenceReferences, StatusCode = NeighborhoodSettlementCodes.Accrued,
                ActivityRewardCalculationDigest = original.ActivityRewardCalculationDigest,
                Revision = 1, RecordedAtUtc = Now
            };
            db.Add(new NeighborhoodSettlementEntryRecord { EntryId = entry.EntryId, AgreementId = id, TermsVersion = entry.TermsVersion,
                LineKey = entry.LineKey, Kind = entry.Kind, OriginKind = entry.OriginKind, OriginId = entry.OriginId,
                OriginRevision = entry.OriginRevision, OriginalEntryId = original.EntryId,
                AccrualKey = Digest(new { r.RequestId, OriginalEntryId = original.EntryId }), DetailsJson = Serialize(entry), RecordedAtUtc = Now });
            // 어느 한쪽이 감액을 제안한 동안 추가 지급을 진행하지 않습니다. 양측 확인 후에만 금액에 반영합니다.
            if (r.AmountKrw < 0 && original.StatusCode != NeighborhoodSettlementCodes.Confirmed)
            { original.StatusCode = NeighborhoodSettlementCodes.Disputed; original.ConfirmationUserIds = []; original.DisputeReason = "감액 정정 확인 대기"; SaveEntry(originalRow, original); }
            agreement.Revision++; agreement.UpdatedAtUtc = Now; AppendHistory(agreement, actor, "Adjustment", Now);
            return Result(agreement, r.RequestId, "Adjustment", [entry.EntryId]);
        }, ct);
}
