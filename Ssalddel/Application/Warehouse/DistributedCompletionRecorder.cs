using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.도메인.창고;
using static Ssalddel.Application.Warehouse.분산물류UseCase;

namespace Ssalddel.Application.Warehouse;

/// <summary>실제 재고이력 ID와 사진 완료행을 같은 DbContext에 기록합니다. 최종 receipt와 commit은 실행기가 소유합니다.</summary>
internal sealed class DistributedCompletionRecorder(SsalddelContext db, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    internal async Task<재고이력> InventoryHistory(long itemId, string actor, string kind, int quantity, string evidence, CancellationToken ct)
    {
        var history = new 재고이력 { 입고상품Id = itemId, 이력유형 = kind, 변경수량 = quantity,
            변경후수량 = await db.입고상품.Where(x => x.Id == itemId).Select(x => x.가용수량).SingleAsync(ct),
            원인유형 = "분산물류실물영수증", 처리UserId = actor, 메모 = evidence, 처리일시 = Now };
        db.재고이력.Add(history); return history;
    }

    internal static 분산물류완료행 CompletionLine(DistributedActor actor, string workKind, string lineKey,
        분산재고LotRecord lot, int quantity, IReadOnlyList<string> photos,
        분산재고예약Record? reservation = null, 분산배송WaveRecord? wave = null)
        => new() { LineKey = lineKey, WorkKind = workKind, ActorUserId = actor.UserId, LotId = lot.LotId,
            InboundItemId = lot.InboundItemId, ReservationId = reservation?.ReservationId, OrderId = reservation?.OrderId,
            RootOrderLedgerId = reservation is not null && wave is not null
                ? 분산주문원장Metadata.Connections(wave).GetValueOrDefault(reservation.OrderId)?.RootOrderLedgerId : null,
            Quantity = quantity, PhotoEvidenceIds = photos.Order(StringComparer.Ordinal).ToArray() };
    internal async Task SaveCompletionLines(DistributedActor actor, DistributedCommandRequest request,
        IReadOnlyList<DistributedCompletionLineDraft> lines, CancellationToken ct)
    {
        Require(lines.Count > 0 && lines.Select(x => x.Line.LineKey).Distinct(StringComparer.Ordinal).Count() == lines.Count,
            "DistributedCompletionLinesInvalid");
        // 기존 재고이력의 실제 ID를 사용합니다. 별도 작업 ID나 과거 행의 추정 연결을 만들지 않습니다.
        await db.SaveChangesAsync(ct);
        Require(!db.ChangeTracker.Entries<분산물류CommandReceipt>().Any(x => x.Entity.ActorUserId == actor.UserId
            && x.Entity.RequestId == request.RequestId), "DistributedCompletionEvidenceAlreadyRecorded");
        db.Add(new 분산물류CommandReceipt { ActorUserId = actor.UserId, RequestId = request.RequestId,
            CompletionLinesJson = Serialize(new 분산물류완료근거(1,
                lines.Select(x => x.Line with { InventoryHistoryId = x.History?.Id }).ToArray())) });
    }
}

internal sealed record DistributedCompletionLineDraft(분산물류완료행 Line, 재고이력? History = null);
