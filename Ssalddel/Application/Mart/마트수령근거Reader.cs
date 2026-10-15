using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Mart;

/// <summary>서버 내부 전용 판독입니다. 이 결과가 사용자 조회 권한을 부여하지 않습니다.</summary>
public sealed record 마트수령근거Snapshot
{
    public bool OrderFound { get; init; }
    public long MartOrderId { get; init; }
    public string OrderReference { get; init; } = "";
    public string BuyerUserId { get; init; } = "";
    public string SellerUserId { get; init; } = "";
    public string AttemptStableId { get; init; } = "";
    public long AttemptRevision { get; init; }
    public bool CanonicalDeliveryValid { get; init; }
    public bool StoredEvidenceValid { get; init; } = true;
    public bool StoredEvidenceScopeValid { get; init; }
    public bool ConfirmedProofValid { get; init; }
    public bool PaymentReleaseReady { get; init; }
    public string? EffectiveReceiptId { get; init; }
    public long? EffectiveReceiptRevision { get; init; }
    public long? EffectiveConfirmedQuantity { get; init; }
    public DateTime? EffectiveConfirmedAtUtc { get; init; }
    public string WaitingReasonCode { get; init; } = "MartCustomerDeliveryEvidenceRequired";
    public long TotalOrderQuantity { get; init; }
    public 마트고객수령근거Envelope? Envelope { get; init; }
    public 마트고객수령확정근거? ConfirmedProof => Envelope?.ConfirmedProof;
    public 마트고객수령보류근거? Hold => Envelope?.Hold;
    public IReadOnlyList<string> EvidenceReferences { get; init; } = [];
    public string SourceFingerprint { get; init; } = "";
    public string LabelId { get; init; } = "";
    public long LabelRevision { get; init; }
    public DateTime PickedUpAtUtc { get; init; }
    public DateTime DeliveredAtUtc { get; init; }
    public IReadOnlyList<마트고객수령행근거> Lines { get; init; } = [];
}

/// <summary>현재 전문 SQL 원천과 동결 수령 근거를 함께 검사합니다. 준비 Adapter·외부 조회·상태 변경은 없습니다.</summary>
public static class 마트수령근거Reader
{
    public static async Task<마트수령근거Snapshot> ReadAsync(SsalddelContext db, long martOrderId,
        DateTime nowUtc, CancellationToken ct = default)
    {
        var state = new 마트수령근거Snapshot { MartOrderId = martOrderId };
        if (martOrderId <= 0 || nowUtc.Kind != DateTimeKind.Utc) return state;
        var order = await db.마트주문.AsNoTracking().Include(row => row.상품목록).SingleOrDefaultAsync(row => row.Id == martOrderId, ct);
        if (order is null) return state;
        state = state with { OrderFound = true, OrderReference = order.주문참조번호, BuyerUserId = order.주문자UserId, SellerUserId = order.판매자UserId };
        if (string.IsNullOrWhiteSpace(order.주문참조번호) || string.IsNullOrWhiteSpace(order.주문자UserId)
            || string.IsNullOrWhiteSpace(order.판매자UserId)
            || await db.마트주문.AsNoTracking().CountAsync(row => row.주문참조번호 == order.주문참조번호, ct) != 1)
            return state;
        var attempts = await db.Set<마트배송시도>().AsNoTracking().Where(row => row.주문참조번호 == order.주문참조번호).Take(2).ToArrayAsync(ct);
        if (attempts.Length != 1) return state;
        var attempt = attempts[0];
        var envelope = 마트고객수령근거Envelope.Read(attempt.고객수령근거Json);
        var evidencePresent = attempt.고객수령근거Json is not null;
        var schemaValid = !evidencePresent || envelope is { Version: 1 } && (envelope.ConfirmedProof is not null || envelope.Hold is not null);
        state = state with { AttemptStableId = attempt.시도StableId, AttemptRevision = attempt.Revision, Envelope = envelope,
            StoredEvidenceValid = !evidencePresent };
        state = state with { StoredEvidenceScopeValid = evidencePresent && schemaValid && StoredScopeMatches(state, nowUtc)
            && 마트수령문제해결Policy.ScopeValid(state, nowUtc) };
        if (order.상태 != "전달 완료" || order.현재단계 != "전달완료" || order.상품목록.Count is 0 or > 128) return state;
        if (attempt.상태Code != "Delivered" || attempt.Revision <= 0 || string.IsNullOrWhiteSpace(attempt.기사Id)
            || string.IsNullOrWhiteSpace(attempt.시도StableId) || !attempt.픽업시각Utc.HasValue || !attempt.전달시각Utc.HasValue
            || attempt.수락시각Utc == default || Utc(attempt.수락시각Utc) > Utc(attempt.픽업시각Utc.Value)
            || Utc(attempt.픽업시각Utc.Value) > Utc(attempt.전달시각Utc.Value) || Utc(attempt.전달시각Utc.Value) > nowUtc)
            return state;
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(row => row.의뢰Id == attempt.배차의뢰Id, ct);
        var label = await db.Set<마트포장전표>().AsNoTracking().SingleOrDefaultAsync(row => row.주문참조번호 == order.주문참조번호, ct);
        if (queue is null || queue.원본의뢰유형 != "SsalddelMartPackedOrder" || queue.원본의뢰Id != order.주문참조번호
            || queue.의뢰Id != order.주문참조번호 || queue.일반회원배송전용 || queue.화주Id != order.판매자UserId
            || queue.배차업무유형 != 상태값.배차업무유형.음식배달 || queue.확정기사Id != attempt.기사Id
            || queue.기사_운송자 != attempt.기사Id || queue.상태 != 상태값.배차상태.하차완료 || queue.배차큐단계 != 상태값.배차큐단계.종료
            || !queue.출발_픽업.HasValue || Utc(queue.출발_픽업.Value) != Utc(attempt.픽업시각Utc.Value)
            || !queue.도착.HasValue || Utc(queue.도착.Value) < Utc(attempt.전달시각Utc.Value) || Utc(queue.도착.Value) > nowUtc
            || label is null || label.활성여부 || label.Revision <= 0 || !label.픽업시각Utc.HasValue
            || label.발급시각Utc == default || Utc(label.발급시각Utc) > Utc(label.픽업시각Utc.Value)
            || label.전표Id != attempt.픽업전표Id || label.Revision != attempt.픽업전표Revision
            || Utc(label.픽업시각Utc.Value) != Utc(attempt.픽업시각Utc.Value)) return state;
        var plans = await db.출고예정.AsNoTracking().Where(row => row.주문참조번호 == order.주문참조번호).OrderBy(row => row.Id).ToArrayAsync(ct);
        if (plans.Length != order.상품목록.Count || order.상품목록.Any(row => row.출고예정Id is null or <= 0 || row.수량 <= 0 || row.상태 != "전달 완료")
            || order.상품목록.Select(row => row.출고예정Id).Distinct().Count() != plans.Length
            || plans.Select(row => row.입고상품Id).Distinct().Count() != plans.Length) return state;
        var lines = new List<마트고객수령행근거>();
        foreach (var plan in plans)
        {
            var line = order.상품목록.SingleOrDefault(row => row.출고예정Id == plan.Id);
            if (line is null || plan.상태 != 출고상태.출고완료 || plan.수량 <= 0 || plan.입고상품Id is null or <= 0
                || plan.운송의뢰Id != queue.의뢰Id || plan.주문자UserId != order.주문자UserId || plan.판매자UserId != order.판매자UserId
                || line.수량 != plan.수량 || line.SKU != plan.SKU || line.상품명 != plan.상품명
                || !plan.출고처리일시.HasValue || !DuringPickup(plan.출고처리일시.Value, attempt)) return state;
            var item = await db.입고상품.AsNoTracking().SingleOrDefaultAsync(row => row.Id == plan.입고상품Id, ct);
            if (item is null || item.상태 != "출고완료" || item.가용수량 != 0 || item.예약수량 != 0
                || item.창고Id != plan.출고창고Id || item.SKU != plan.SKU || item.상품명 != plan.상품명) return state;
            var history = await db.재고이력.AsNoTracking().Where(row => row.입고상품Id == item.Id && row.원인유형 == "마트기사픽업" && row.원인Id == plan.Id)
                .Take(2).ToArrayAsync(ct);
            var movement = await db.재고이동.AsNoTracking().Where(row => row.출고예정Id == plan.Id && row.운송의뢰Id == queue.의뢰Id).Take(2).ToArrayAsync(ct);
            if (history.Length != 1 || history[0].이력유형 != "출고" || history[0].변경수량 != -plan.수량 || history[0].변경후수량 != 0
                || history[0].처리UserId != attempt.기사Id || !DuringPickup(history[0].처리일시, attempt)
                || movement.Length != 1 || movement[0].이동유형 != "출고" || movement[0].수량 != plan.수량
                || movement[0].입고상품Id != item.Id || movement[0].창고Id != plan.출고창고Id
                || movement[0].주문참조번호 != order.주문참조번호 || movement[0].SKU != plan.SKU || movement[0].상품명 != plan.상품명
                || movement[0].처리UserId != attempt.기사Id || !DuringPickup(movement[0].발생일시, attempt)) return state;
            lines.Add(new(line.Id, plan.Id, item.Id, plan.출고창고Id, plan.SKU, plan.상품명, plan.수량, history[0].Id, movement[0].Id,
                Utc(plan.출고처리일시.Value), Utc(history[0].처리일시), Utc(movement[0].발생일시)));
        }
        var pickedAt = Utc(attempt.픽업시각Utc.Value); var deliveredAt = Utc(attempt.전달시각Utc.Value);
        var fingerprint = 마트고객수령근거Envelope.Digest(new { order.Id, order.주문참조번호, order.주문자UserId, order.판매자UserId,
            attempt.시도StableId, attempt.배차의뢰Id, attempt.기사Id, PickedUpAtUtc = pickedAt, DeliveredAtUtc = deliveredAt,
            label.전표Id, label.Revision, QueueArrivalAtUtc = Utc(queue.도착.Value), Lines = lines });
        state = state with { CanonicalDeliveryValid = true, TotalOrderQuantity = lines.Sum(row => (long)row.Quantity), Lines = lines,
            LabelId = label.전표Id, LabelRevision = label.Revision, PickedUpAtUtc = pickedAt, DeliveredAtUtc = deliveredAt, SourceFingerprint = fingerprint };
        if (!schemaValid || evidencePresent && !state.StoredEvidenceScopeValid)
            return state with { WaitingReasonCode = "MartCustomerReceiptEvidenceInvalid" };
        if (envelope is null) return state with { StoredEvidenceValid = true, WaitingReasonCode = "MartCustomerReceiptEvidenceRequired" };
        var proof = envelope.ConfirmedProof;
        var proofValid = proof is not null && ProofMatches(state, proof, nowUtc);
        if (proof is not null && !proofValid) return state with { StoredEvidenceValid = false, WaitingReasonCode = "MartCustomerReceiptEvidenceInvalid" };
        if (envelope.Hold is { } hold && !HoldMatches(state, hold, nowUtc))
            return state with { StoredEvidenceValid = false, WaitingReasonCode = "MartCustomerReceiptEvidenceInvalid" };
        state = state with { StoredEvidenceValid = true, ConfirmedProofValid = proofValid };
        var resolved = 마트수령문제해결Policy.FullResolutionValid(state);
        var effectiveId = resolved ? 마트수령문제해결Policy.LatestProposal(state)!.ProposalId : proofValid ? proof!.ReceiptId : null;
        var references = effectiveId is null ? Array.Empty<string>() : new[] { effectiveId, "mart-attempt:" + attempt.시도StableId,
            "mart-label:" + label.전표Id }.Concat(lines.SelectMany(row => new[] { "outbound:" + row.OutboundId,
                "inventory-history:" + row.HistoryId, "inventory-movement:" + row.MovementId })).ToArray();
        return state with { PaymentReleaseReady = resolved || proofValid && envelope.Hold is null,
            EffectiveReceiptId = effectiveId,
            EffectiveReceiptRevision = resolved ? state.AttemptRevision : proofValid ? proof!.ConfirmedRevision : null,
            EffectiveConfirmedQuantity = resolved ? state.TotalOrderQuantity : proofValid ? proof!.ConfirmedQuantity : null,
            EffectiveConfirmedAtUtc = resolved ? envelope.Resolutions.Last().RecordedAtUtc : proofValid ? proof!.ConfirmedAtUtc : null,
            EvidenceReferences = references, WaitingReasonCode = resolved ? "" : envelope.Hold is not null ? "MartCustomerReceiptHeld" : "" };
    }

    private static bool ProofMatches(마트수령근거Snapshot state, 마트고객수령확정근거 proof, DateTime now)
        => proof.MartOrderId == state.MartOrderId && proof.OrderReference == state.OrderReference && proof.BuyerUserId == state.BuyerUserId
            && proof.AttemptStableId == state.AttemptStableId && proof.ClientRequestId != Guid.Empty
            && proof.ReceiptId == $"mart-customer-receipt:{state.MartOrderId}:{proof.ClientRequestId:N}"
            && proof.DeliveredRevision > 0 && proof.DeliveredRevision < long.MaxValue && proof.ConfirmedRevision == proof.DeliveredRevision + 1
            && (state.Hold is null ? 마트수령문제해결Policy.OriginalRevision(state) == proof.ConfirmedRevision
                : 마트수령문제해결Policy.OriginalRevision(state) == proof.ConfirmedRevision + 1)
            && proof.ConfirmedAtUtc.Kind == DateTimeKind.Utc && proof.ConfirmedAtUtc >= state.DeliveredAtUtc && proof.ConfirmedAtUtc <= now
            && proof.DeliveredAtUtc == state.DeliveredAtUtc && proof.DeliveredAtUtc.Kind == DateTimeKind.Utc
            && proof.PickedUpAtUtc == state.PickedUpAtUtc && proof.PickedUpAtUtc.Kind == DateTimeKind.Utc
            && proof.LabelId == state.LabelId && proof.LabelRevision == state.LabelRevision
            && proof.ConfirmedQuantity == state.TotalOrderQuantity && proof.SourceFingerprint == state.SourceFingerprint
            && proof.Lines is not null && proof.Lines.SequenceEqual(state.Lines)
            && proof.InputDigest == 마트고객수령근거Envelope.ConfirmDigest(state.MartOrderId, proof.ClientRequestId,
                proof.AttemptStableId, proof.DeliveredRevision, true);

    private static bool HoldMatches(마트수령근거Snapshot state, 마트고객수령보류근거 hold, DateTime now)
        => hold.MartOrderId == state.MartOrderId && hold.OrderReference == state.OrderReference && hold.BuyerUserId == state.BuyerUserId
            && hold.AttemptStableId == state.AttemptStableId && hold.ClientRequestId != Guid.Empty && ValidReason(hold.ReasonCode)
            && hold.ExpectedRevision > 0 && hold.ExpectedRevision < long.MaxValue && hold.ResultRevision == hold.ExpectedRevision + 1
            && hold.ResultRevision == 마트수령문제해결Policy.OriginalRevision(state)
            && (state.ConfirmedProof is null || hold.ExpectedRevision == state.ConfirmedProof.ConfirmedRevision
                && hold.ClientRequestId != state.ConfirmedProof.ClientRequestId && hold.RecordedAtUtc >= state.ConfirmedProof.ConfirmedAtUtc)
            && hold.RecordedAtUtc.Kind == DateTimeKind.Utc && hold.RecordedAtUtc >= state.DeliveredAtUtc && hold.RecordedAtUtc <= now
            && hold.InputDigest == 마트고객수령근거Envelope.IssueDigest(state.MartOrderId, hold.ClientRequestId,
                hold.AttemptStableId, hold.ExpectedRevision, hold.ReasonCode);

    private static bool StoredScopeMatches(마트수령근거Snapshot state, DateTime now)
    {
        var proof = state.ConfirmedProof; var hold = state.Hold;
        if (proof is not null && (proof.MartOrderId != state.MartOrderId || proof.OrderReference != state.OrderReference
            || proof.BuyerUserId != state.BuyerUserId || proof.AttemptStableId != state.AttemptStableId || proof.ClientRequestId == Guid.Empty
            || proof.ReceiptId != $"mart-customer-receipt:{state.MartOrderId}:{proof.ClientRequestId:N}"
            || proof.DeliveredRevision <= 0 || proof.DeliveredRevision == long.MaxValue || proof.ConfirmedRevision != proof.DeliveredRevision + 1
            || proof.ConfirmedAtUtc.Kind != DateTimeKind.Utc || proof.ConfirmedAtUtc == default || proof.ConfirmedAtUtc > now
            || proof.InputDigest != 마트고객수령근거Envelope.ConfirmDigest(state.MartOrderId, proof.ClientRequestId,
                proof.AttemptStableId, proof.DeliveredRevision, true))) return false;
        return hold is null || hold.MartOrderId == state.MartOrderId && hold.OrderReference == state.OrderReference
            && hold.BuyerUserId == state.BuyerUserId && hold.AttemptStableId == state.AttemptStableId && hold.ClientRequestId != Guid.Empty
            && ValidReason(hold.ReasonCode) && hold.ExpectedRevision > 0 && hold.ExpectedRevision < long.MaxValue
            && hold.ResultRevision == hold.ExpectedRevision + 1 && hold.RecordedAtUtc.Kind == DateTimeKind.Utc
            && hold.RecordedAtUtc != default && hold.RecordedAtUtc <= now
            && (proof is null || hold.ExpectedRevision == proof.ConfirmedRevision && hold.ClientRequestId != proof.ClientRequestId
                && hold.RecordedAtUtc >= proof.ConfirmedAtUtc)
            && hold.InputDigest == 마트고객수령근거Envelope.IssueDigest(state.MartOrderId, hold.ClientRequestId,
                hold.AttemptStableId, hold.ExpectedRevision, hold.ReasonCode);
    }

    public static bool ValidReason(string? reason) => reason is "Partial" or "Missing" or "Damaged";
    private static bool DuringPickup(DateTime value, 마트배송시도 attempt)
        => value != default && Utc(value) >= Utc(attempt.수락시각Utc) && Utc(value) <= Utc(attempt.픽업시각Utc!.Value);
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
