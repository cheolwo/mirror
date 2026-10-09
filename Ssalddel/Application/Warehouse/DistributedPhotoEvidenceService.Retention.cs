using System.Data;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.PrivacySupport;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class DistributedPhotoEvidenceService
{
    public async Task<int> SweepAsync(I보호지원Store support, CancellationToken ct)
    {
        Gate("photo-retention"); var deleted = 0;
        var ids = await Photos.AsNoTracking().Where(x => x.StatusCode != "Deleted").OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.EvidenceId).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            // The same source-index read/lock is used by support-case creation before Mongo writes.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var row = await Photos.AsNoTracking().SingleAsync(x => x.EvidenceId == id, ct);
            var d = Details(row); var b = d.Binding;
            var lots = d.ImportedLotIds ?? b.LotIds.ToArray();
            var sources = lots.Select(x => (Kind: "distributed-inbound", Id: x))
                .Concat(b.OrderIds.Select(x => (Kind: "distributed-wave", Id: x))).Distinct().ToArray();
            var blocked = false;
            foreach (var source in sources)
            {
                if (await db.Set<분산사진보존정지Record>().AnyAsync(x => x.SourceKind == source.Kind && x.SourceId == source.Id && x.Active, ct))
                { blocked = true; break; }
                try { if (await support.HasOpenCaseAsync(source.Kind, source.Id, ct) != false) { blocked = true; break; } }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { blocked = true; break; }
            }
            if (blocked) { await tx.CommitAsync(ct); continue; }
            var orphan = row.BoundAtUtc is null;
            if (orphan && row.CreatedAtUtc > Now.AddHours(-24)) { await tx.CommitAsync(ct); continue; }
            if (!orphan)
            {
                var terminal = await Terminal(row.Kind, lots, b.OrderIds, ct);
                if (!terminal)
                {
                    if (row.CustodyEndedAtUtc is not null)
                        await Photos.Where(x => x.EvidenceId == id && x.Revision == row.Revision)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustodyEndedAtUtc, (DateTime?)null)
                                .SetProperty(x => x.DeleteAfterUtc, (DateTime?)null).SetProperty(x => x.StatusCode, "Bound")
                                .SetProperty(x => x.Revision, x => x.Revision + 1), ct);
                    await tx.CommitAsync(ct); continue;
                }
                if (row.CustodyEndedAtUtc is null)
                {
                    await Photos.Where(x => x.EvidenceId == id && x.Revision == row.Revision)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustodyEndedAtUtc, Now).SetProperty(x => x.DeleteAfterUtc, Now.AddDays(30))
                            .SetProperty(x => x.Revision, x => x.Revision + 1), ct);
                    await tx.CommitAsync(ct); continue;
                }
                if (row.DeleteAfterUtc is null || row.DeleteAfterUtc > Now) { await tx.CommitAsync(ct); continue; }
            }
            // Interrupted upload cannot safely infer a storage container. Keep the row for recovery instead of guessing a path.
            if (string.IsNullOrWhiteSpace(d.Container)) { await tx.CommitAsync(ct); continue; }
            var claimed = await Photos.Where(x => x.EvidenceId == id && x.Revision == row.Revision && x.StatusCode != "Deleted")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StatusCode, "DeletionPending").SetProperty(x => x.Revision, x => x.Revision + 1), ct);
            if (claimed != 1) { await tx.CommitAsync(ct); continue; }
            var result = await storage.DeleteAsync(d.Container, d.ObjectName, OwnedPrefix, ct);
            await Photos.Where(x => x.EvidenceId == id && x.Revision == row.Revision + 1)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StatusCode, result.ActiveObjectAbsent ? "Deleted" : "DeletionPending")
                    .SetProperty(x => x.ActiveObjectAbsent, result.ActiveObjectAbsent)
                    .SetProperty(x => x.HistoricalCopiesVerifiedAbsent, result.HistoricalCopiesVerifiedAbsent)
                    .SetProperty(x => x.DeletionLimitationCode, result.LimitationCode)
                    .SetProperty(x => x.DeletedAtUtc, result.ActiveObjectAbsent ? Now : (DateTime?)null)
                    .SetProperty(x => x.Revision, x => x.Revision + 1), ct);
            await tx.CommitAsync(ct); if (result.ActiveObjectAbsent) deleted++;
        }
        return deleted;
    }
    private async Task<bool> Terminal(string kind, IReadOnlyList<string> lotIds, IReadOnlyList<string> orderIds, CancellationToken ct)
    {
        if (kind is "Receive" or "ImportLot" or "Return")
        {
            if (lotIds.Count == 0) return false;
            var lots = await db.Set<분산재고LotRecord>().AsNoTracking().Where(x => Enumerable.Contains(lotIds, x.LotId)).ToArrayAsync(ct);
            if (lots.Length != lotIds.Count) return false;
            var stockIds = lots.Select(x => x.InboundItemId).ToArray();
            if (await db.입고상품.CountAsync(x => Enumerable.Contains(stockIds, x.Id), ct) != stockIds.Distinct().Count()) return false;
            if (await db.입고상품.AnyAsync(x => Enumerable.Contains(stockIds, x.Id)
                && (x.가용수량 > 0 || x.예약수량 > 0 || x.불량수량 > 0), ct)) return false;
            if (await db.Set<분산재고예약Record>().AnyAsync(x => Enumerable.Contains(lotIds, x.LotId)
                && x.PickedQuantity > x.DeliveredQuantity + x.ReturnedQuantity, ct)) return false;
        }
        if (orderIds.Count > 0)
        {
            var orders = await db.Set<분산배송주문Record>().AsNoTracking().Where(x => Enumerable.Contains(orderIds, x.OrderId)).ToArrayAsync(ct);
            if (orders.Length != orderIds.Count || orders.Any(x => x.StatusCode is not ("Delivered" or "Cancelled"))) return false;
            if (await db.Set<분산재고예약Record>().AnyAsync(x => Enumerable.Contains(orderIds, x.OrderId)
                && x.PickedQuantity > x.DeliveredQuantity + x.ReturnedQuantity, ct)) return false;
        }
        else if (kind is "Pick" or "Deliver") return false;
        return true;
    }
}
