using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Mart;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using W = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementWorkflowCodes;

namespace Ssalddel.Application.Settlement;

public sealed partial class NeighborhoodSettlementSourceResolver
{
    private async Task<NeighborhoodSettlementSourceSnapshot> Mart(string id, CancellationToken ct)
    {
        if (!long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0
            || number.ToString(CultureInfo.InvariantCulture) != id)
            throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceInvalid", 400);
        var order = await Query<마트주문>().Include(row => row.상품목록).SingleOrDefaultAsync(row => row.Id == number, ct)
            ?? throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceNotFound", 404);
        if (string.IsNullOrWhiteSpace(order.주문참조번호) || string.IsNullOrWhiteSpace(order.판매자UserId)
            || string.IsNullOrWhiteSpace(order.주문자UserId)
            || await Query<마트주문>().CountAsync(row => row.주문참조번호 == order.주문참조번호, ct) != 1)
            throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceEvidenceInvalid");

        var plans = await Query<출고예정>().Where(plan => plan.주문참조번호 == order.주문참조번호).ToArrayAsync(ct);
        if (plans.Length == 0 || plans.Length != order.상품목록.Count
            || order.상품목록.Select(line => line.출고예정Id).Distinct().Count() != order.상품목록.Count
            || order.상품목록.Any(line => !plans.Any(plan => plan.Id == line.출고예정Id && plan.수량 == line.수량
                && plan.SKU == line.SKU && plan.상품명 == line.상품명))
            || plans.Any(plan => plan.판매자UserId != order.판매자UserId
                || plan.주문자UserId != order.주문자UserId || plan.수량 <= 0 || plan.입고상품Id is null or <= 0))
            throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceEvidenceInvalid");
        var inventoryIds = plans.Select(plan => plan.입고상품Id!.Value).Distinct().ToList();
        var inventories = await Query<입고상품>().Where(item => inventoryIds.Contains(item.Id)).ToArrayAsync(ct);
        var tasks = await Query<피킹포장작업>().Where(task => task.주문참조번호 == order.주문참조번호
            && task.상태 != 피킹포장작업상태.취소).OrderBy(task => task.Id).ToArrayAsync(ct);
        var parties = new Parties();
        parties.Add(order.주문자UserId, NeighborhoodSettlementRoles.Buyer);
        parties.Add(order.판매자UserId, NeighborhoodSettlementRoles.Seller);
        foreach (var item in inventories) parties.Add(item.소유자UserId, NeighborhoodSettlementRoles.Owner);
        // 실제 완료의 진전으로 당사자 판본이 달라지지 않도록 사전에 배정된 당사자만 합의에 참여합니다.
        foreach (var task in tasks)
        {
            var role = task.작업유형 == 피킹포장작업유형.피킹 ? NeighborhoodSettlementRoles.Picker
                : task.작업유형 == 피킹포장작업유형.포장 ? NeighborhoodSettlementRoles.Packer : null;
            if (role is not null) parties.Add(task.작업자UserId, role);
        }
        var works = new List<NeighborhoodSettlementCompletedWork>();
        string? workHold = null;
        foreach (var task in tasks)
        {
            var proof = WarehouseActivityCompletionEvidence.Read(task.완료근거Json);
            if (task.상태 == 피킹포장작업상태.완료 && proof is null) workHold ??= "MartCompletionEvidenceRequired";
            if (proof is not null && proof.ActorUserId != task.작업자UserId) workHold = "MartActualActorAssignmentMismatch";
            if (proof is not null && proof.AgreementDeadlineAtUtc is null) workHold ??= "MartWorkAgreementDeadlineRequired";
            var plan = plans.SingleOrDefault(plan => plan.Id == task.출고예정Id);
            var item = inventories.SingleOrDefault(item => item.Id == task.입고상품Id);
            if (proof is null || plan is null || item is null || plan.상태 == 출고상태.취소
                || tasks.Where(row => row.출고예정Id == plan.Id && row.작업유형 == task.작업유형).Sum(row => (long)row.수량) != plan.수량
                || !MartCompletionMatches(task, proof, plan, item, order.주문참조번호))
            {
                if (task.상태 == 피킹포장작업상태.완료) workHold ??= "MartWorkEvidenceRequired";
                continue;
            }
            var evidence = new List<string> { $"warehouse-task:{task.Id}", $"outbound:{plan.Id}" };
            if (task.작업유형 == 피킹포장작업유형.포장)
            {
                var history = await MartPackingHistory(task, proof, ct);
                if (history is null) { workHold ??= "MartPackingEvidenceRequired"; continue; }
                evidence.Add($"inventory-history:{history.Id}");
                evidence.Add(proof.PackingBatchKey!);
            }
            else if (task.작업유형 != 피킹포장작업유형.피킹) continue;
            works.Add(new(task.작업유형 == 피킹포장작업유형.피킹 ? C.Picking : C.Packing,
                "WarehouseTask", task.Id.ToString(CultureInfo.InvariantCulture), proof.Quantity, proof.CompletedAtUtc,
                evidence, proof.ActorUserId, item.소유자UserId, SellerUserId: order.판매자UserId,
                RelatedOrderId: id, AgreementDeadlineAtUtc: proof.AgreementDeadlineAtUtc));
        }
        var lotIds = await Query<분산재고LotRecord>().Where(lot => inventoryIds.Contains(lot.InboundItemId))
            .Select(lot => lot.LotId).Distinct().OrderBy(lot => lot).ToArrayAsync(ct);
        var receipt = await 마트수령근거Reader.ReadAsync(db, order.Id, Now, ct);
        var goodsQuantity = plans.Sum(plan => (long)plan.수량);
        var warehouseComplete = works.Where(work => work.Kind == C.Picking).Sum(work => work.Quantity) == goodsQuantity
            && works.Where(work => work.Kind == C.Packing).Sum(work => work.Quantity) == goodsQuantity;
        var ready = receipt.PaymentReleaseReady && workHold is null && warehouseComplete
            && receipt.EffectiveConfirmedQuantity is > 0 && receipt.EffectiveReceiptRevision is > 0
            && !string.IsNullOrWhiteSpace(receipt.EffectiveReceiptId) && receipt.EffectiveConfirmedAtUtc.HasValue
            && receipt.EffectiveConfirmedQuantity == goodsQuantity && receipt.TotalOrderQuantity == goodsQuantity;
        NeighborhoodSettlementCustomerReceiptDto? customerReceipt = ready ? new()
        {
            SourceId = id, ReceiptId = receipt.EffectiveReceiptId!,
            Revision = receipt.EffectiveReceiptRevision!.Value, ConfirmedQuantity = goodsQuantity,
            ConfirmedAtUtc = receipt.EffectiveConfirmedAtUtc!.Value, EvidenceReferences = receipt.EvidenceReferences
        } : null;
        return Snapshot(C.MartOrder, id, C.StoredGoods, parties, works, works.Sum(work => work.Quantity), customerReceipt: customerReceipt, workflow: new()
        {
            Kind = W.OutboundDelivery, StateCode = ready ? "Received" : receipt.Hold is not null ? "ReceiptProblemReported"
                : works.Count == 0 ? "WaitingForWorkEvidence" : "WarehouseWorkRecorded",
            // 작업 보수 근거가 있다는 것과 고객 수령·최종 정산·송금은 별도입니다.
            CompletionConfirmed = ready, PaymentReleaseReady = ready,
            WaitingReasonCode = ready ? null : workHold ?? (receipt.PaymentReleaseReady && !warehouseComplete
                ? "MartWorkEvidenceRequired" : receipt.WaitingReasonCode ?? "MartCustomerReceiptEvidenceRequired"), InventoryLotIds = lotIds
        });
    }

    private bool MartCompletionMatches(피킹포장작업 task, WarehouseActivityCompletionEvidence proof,
        출고예정 plan, 입고상품 item, string orderReference)
        => proof.Version == 1 && task.상태 == 피킹포장작업상태.완료 && task.완료일시Utc.HasValue
            && proof.TaskId == task.Id && proof.TaskKey == task.작업Key && proof.WorkKind == task.작업유형
            && proof.OrderReference == orderReference && proof.OutboundId == plan.Id && proof.InventoryItemId == item.Id
            && proof.WarehouseId == task.창고Id && task.창고Id == plan.출고창고Id && item.창고Id == task.창고Id
            && item.SKU == plan.SKU && item.상품명 == plan.상품명 && task.SKU == item.SKU && task.상품명 == item.상품명
            && proof.Quantity > 0 && proof.Quantity == task.수량 && proof.Quantity <= plan.수량
            && !string.IsNullOrWhiteSpace(proof.ActorUserId) && proof.ActorUserId == task.작업자UserId
            && proof.CompletedAtUtc.Kind == DateTimeKind.Utc && proof.CompletedAtUtc <= Now
            && proof.CompletedAtUtc == MartUtc(task.완료일시Utc.Value)
            && proof.AgreementDeadlineAtUtc is { Kind: DateTimeKind.Utc } deadline
            && deadline <= proof.CompletedAtUtc && deadline <= Now
            && (task.작업유형 != 피킹포장작업유형.피킹 || proof.StartedAtUtc is { Kind: DateTimeKind.Utc } started
                && task.시작일시Utc.HasValue && started == MartUtc(task.시작일시Utc.Value) && deadline == started);

    private async Task<재고이력?> MartPackingHistory(피킹포장작업 task,
        WarehouseActivityCompletionEvidence proof, CancellationToken ct)
    {
        var shares = proof.PackingBatchShares;
        if (string.IsNullOrWhiteSpace(proof.PackingBatchKey) || proof.PackingBatchQuantity is not > 0
            || shares.Count is 0 or > 64 || shares.Select(share => share.TaskId).Distinct().Count() != shares.Count
            || shares.Any(share => share.Quantity <= 0) || shares.Sum(share => (long)share.Quantity) != proof.PackingBatchQuantity
            || !shares.Any(share => share.TaskId == task.Id && share.Quantity == proof.Quantity)) return null;
        var taskIds = shares.Select(share => share.TaskId).ToList();
        var batch = await Query<피킹포장작업>().Where(row => taskIds.Contains(row.Id)).ToArrayAsync(ct);
        if (batch.Length != shares.Count || batch.Any(row =>
            {
                var other = WarehouseActivityCompletionEvidence.Read(row.완료근거Json);
                return other is null || other.PackingBatchKey != proof.PackingBatchKey || other.ActorUserId != proof.ActorUserId
                    || other.CompletedAtUtc != proof.CompletedAtUtc || other.InventoryItemId != proof.InventoryItemId
                    || other.TaskId != row.Id || other.Quantity != row.수량 || row.상태 != 피킹포장작업상태.완료
                    || row.작업유형 != 피킹포장작업유형.포장 || row.입고상품Id != proof.InventoryItemId
                    || !other.PackingBatchShares.SequenceEqual(shares);
            })) return null;
        var histories = await Query<재고이력>().Where(history => history.입고상품Id == proof.InventoryItemId
            && history.이력유형 == "포장" && history.원인유형 == "포장작업" && history.처리UserId == proof.ActorUserId
            && history.변경후수량 == proof.PackingBatchQuantity && history.처리일시 == proof.CompletedAtUtc).Take(2).ToArrayAsync(ct);
        if (histories.Length != 1) return null;
        var starts = await Query<피킹포장작업>().Where(row => row.출고예정Id == task.출고예정Id
            && row.작업유형 == 피킹포장작업유형.피킹 && row.상태 != 피킹포장작업상태.취소).ToArrayAsync(ct);
        if (starts.Length == 0 || starts.Any(row => row.상태 != 피킹포장작업상태.완료 || !row.시작일시Utc.HasValue)
            || MartUtc(starts.Min(row => row.시작일시Utc!.Value)) != proof.AgreementDeadlineAtUtc) return null;
        return histories[0];
    }

    private static DateTime MartUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
