using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed class DistributedPhotoHoldService(SsalddelContext db) : I분산사진보존연결
{
    private IQueryable<분산사진보존정지Record> Holds => db.Set<분산사진보존정지Record>();
    private static void Validate(string caseId, string kind, string id)
    {
        if (string.IsNullOrWhiteSpace(caseId) || caseId.Length > 100 || string.IsNullOrWhiteSpace(id) || id.Length > 64
            || kind is not ("distributed-inbound" or "distributed-wave"))
            throw new DistributedFulfillmentException("DistributedPhotoHoldSourceInvalid", 400);
    }
    private static void Same(분산사진보존정지Record r, string kind, string id)
    { if (r.SourceKind != kind || r.SourceId != id) throw new DistributedFulfillmentException("DistributedPhotoHoldSourceConflict"); }
    public async Task AcquireOpenCaseAsync(string caseId, string sourceKind, string sourceId, CancellationToken ct = default)
    {
        Validate(caseId, sourceKind, sourceId);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await Holds.SingleOrDefaultAsync(x => x.CaseId == caseId, ct);
        if (row is null)
        {
            db.Add(new 분산사진보존정지Record { CaseId = caseId, SourceKind = sourceKind, SourceId = sourceId,
                Active = true, LegalBasisJson = JsonSerializer.Serialize("진행 중인 공간 업무 문제 검토") });
            await db.SaveChangesAsync(ct);
        }
        else
        {
            Same(row, sourceKind, sourceId);
            if (!row.Active) { row.Active = true; row.Revision++; row.ReviewAtUtc = null; await db.SaveChangesAsync(ct); }
        }
        await tx.CommitAsync(ct);
    }
    public Task 보존정지Async(string caseId, string kind, string id, string legalBasis, DateTime reviewAtUtc, CancellationToken ct = default)
        => 보존상태적용Async(caseId, kind, id, 0, true, legalBasis, reviewAtUtc, ct);
    public Task 보존정지해제Async(string caseId, string kind, string id, CancellationToken ct = default)
        => 보존상태적용Async(caseId, kind, id, 0, false, "", null, ct);
    public async Task<DateTime?> 보존정지조회Async(string caseId, string kind, string id, CancellationToken ct = default)
    {
        var value = await 보존적용조회Async(caseId, kind, id, ct);
        // null review date still represents an active hold; do not interpret an expired review as automatic release.
        return value is { Hold: true } ? value.ReviewAtUtc ?? DateTime.MaxValue : null;
    }
    public async Task<bool> 보존정지해제확인Async(string caseId, string kind, string id, CancellationToken ct = default)
        => await 보존적용조회Async(caseId, kind, id, ct) is { Hold: false };
    public async Task<보호지원보존적용?> 보존적용조회Async(string caseId, string kind, string id, CancellationToken ct = default)
    {
        Validate(caseId, kind, id); var row = await Holds.AsNoTracking().SingleOrDefaultAsync(x => x.CaseId == caseId, ct);
        if (row is null) return null; Same(row, kind, id); return new(row.Sequence, row.Active, row.ReviewAtUtc);
    }
    public async Task 보존상태적용Async(string caseId, string kind, string id, long sequence, bool hold, string legalBasis,
        DateTime? reviewAtUtc, CancellationToken ct = default)
    {
        Validate(caseId, kind, id);
        if (sequence < 0 || legalBasis.Length > 4000 || (hold && string.IsNullOrWhiteSpace(legalBasis)))
            throw new DistributedFulfillmentException("DistributedPhotoHoldInvalid", 400);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await Holds.SingleOrDefaultAsync(x => x.CaseId == caseId, ct);
        var basis = JsonSerializer.Serialize(legalBasis);
        if (row is not null)
        {
            Same(row, kind, id);
            if (row.Sequence > sequence) { await tx.CommitAsync(ct); return; }
            if (row.Sequence == sequence)
            {
                if (row.Active != hold || row.ReviewAtUtc != reviewAtUtc || row.LegalBasisJson != basis)
                    throw new DistributedFulfillmentException("DistributedPhotoHoldSequenceConflict");
                await tx.CommitAsync(ct); return;
            }
            row.Sequence = sequence; row.Active = hold; row.LegalBasisJson = basis; row.ReviewAtUtc = reviewAtUtc; row.Revision++;
        }
        else db.Add(new 분산사진보존정지Record { CaseId = caseId, SourceKind = kind, SourceId = id,
            Sequence = sequence, Active = hold, LegalBasisJson = basis, ReviewAtUtc = reviewAtUtc });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
