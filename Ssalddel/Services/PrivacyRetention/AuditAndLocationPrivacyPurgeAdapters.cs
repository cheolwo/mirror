using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.PrivacyRetention;
using 살뜰.Data;

namespace Ssalddel.Services.PrivacyRetention;

public sealed class 접속감사개인정보파기Adapter(SsalddelContext db, IOptions<개인정보보존Options> options, TimeProvider clock) : I개인정보파기Adapter
{
    public string SourceCode => 개인정보파기원천Codes.AccessAudit;
    public async Task<IReadOnlyList<개인정보파기대상>> DiscoverAsync(DateTime cutoff, int limit, CancellationToken ct, long afterSequence = 0)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddYears(-options.Value.AuditYears);
        var rows = await db.사용자행위로그.AsNoTracking().Where(x => x.Id > afterSequence && x.OccurredAtUtc <= expires)
            .OrderBy(x => x.Id).Take(limit).ToListAsync(ct);
        return rows.Select(x => new 개인정보파기대상(SourceCode, x.Id.ToString(CultureInfo.InvariantCulture), x.OccurredAtUtc, x.UserId, x.Id)).ToArray();
    }
    public async Task<개인정보파기대상?> FindAsync(string recordId, CancellationToken ct)
    {
        if (!long.TryParse(recordId, out var id)) return null;
        var x = await db.사용자행위로그.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return x is null ? null : new(SourceCode, recordId, x.OccurredAtUtc, x.UserId);
    }
    public async Task<개인정보파기Adapter결과> PurgeAsync(개인정보파기Job job, DateTime now, CancellationToken ct)
    {
        var id = long.Parse(job.RecordId, CultureInfo.InvariantCulture);
        var row = await db.사용자행위로그.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return new(true, string.Empty, ["RDB.UserActivityAudit"]);
        if (row.OccurredAtUtc > now.AddYears(-options.Value.AuditYears)) return new(false, "AuditRetentionNotExpired", []);
        db.사용자행위로그.Remove(row); await db.SaveChangesAsync(ct);
        return new(true, string.Empty, ["RDB.UserActivityAudit"]);
    }
}

public sealed class 기사위치개인정보파기Adapter(SsalddelContext db, IOptions<개인정보보존Options> options, TimeProvider clock) : I개인정보파기Adapter
{
    public string SourceCode => 개인정보파기원천Codes.DriverLocation;
    public async Task<IReadOnlyList<개인정보파기대상>> DiscoverAsync(DateTime cutoff, int limit, CancellationToken ct, long afterSequence = 0)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddDays(-Math.Clamp(options.Value.DriverLocationDays, 1, 30));
        var rows = await db.기사위치기록.AsNoTracking().Where(x => x.Id > afterSequence && x.기록시각 <= expires
            && db.기사위치기록.Any(newer => newer.기사Id == x.기사Id && newer.Id > x.Id))
            .OrderBy(x => x.Id).Take(limit).ToListAsync(ct);
        return rows.Select(x => new 개인정보파기대상(SourceCode, x.Id.ToString(CultureInfo.InvariantCulture), x.기록시각, x.기사Id, x.Id)).ToArray();
    }
    public async Task<개인정보파기대상?> FindAsync(string recordId, CancellationToken ct)
    {
        if (!long.TryParse(recordId, out var id)) return null;
        var x = await db.기사위치기록.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return x is null ? null : new(SourceCode, recordId, x.기록시각, x.기사Id);
    }
    public async Task<개인정보파기Adapter결과> PurgeAsync(개인정보파기Job job, DateTime now, CancellationToken ct)
    {
        var id = long.Parse(job.RecordId, CultureInfo.InvariantCulture);
        var row = await db.기사위치기록.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return new(true, string.Empty, ["RDB.DriverLocation"]);
        if (row.기록시각 > now.AddDays(-Math.Clamp(options.Value.DriverLocationDays, 1, 30))
            || !await db.기사위치기록.AnyAsync(x => x.기사Id == row.기사Id && x.Id > row.Id, ct))
            return new(false, "CurrentDriverLocationPreserved", []);
        db.기사위치기록.Remove(row); await db.SaveChangesAsync(ct);
        return new(true, string.Empty, ["RDB.DriverLocation"]);
    }
}
