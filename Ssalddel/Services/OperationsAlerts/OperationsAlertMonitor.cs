using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Domain.Notifications;
using 살뜰.Data;

namespace Ssalddel.Services.OperationsAlerts;

public sealed class OperationsAlertMonitor(SsalddelContext db, OperationsAlertCoordinator coordinator,
    IEnumerable<IOperationsAlertSource> sources, IOptionsMonitor<OperationsAlertsOptions> options)
{
    public async Task ScanAsync(CancellationToken ct = default)
    {
        if (!options.CurrentValue.Enabled) return;
        var startup = await db.Set<OperationsAlertCheckpoint>().SingleOrDefaultAsync(x => x.Id == "startup", ct);
        if (startup is null) { startup = new() { Id = "startup", StartedAtUtc = coordinator.Now }; db.Add(startup); await db.SaveChangesAsync(ct); }
        var allCaughtUp = true;
        foreach (var source in sources)
        {
            var checkpoint = await db.Set<OperationsAlertCheckpoint>().SingleOrDefaultAsync(x => x.Id == source.Key, ct);
            if (checkpoint is null) { checkpoint = new() { Id = source.Key, CursorUtc = DateTime.UnixEpoch }; db.Add(checkpoint); await db.SaveChangesAsync(ct); }
            try
            {
                for (var page = 0; page < Math.Clamp(options.CurrentValue.PagesPerCycle, 1, 10); page++)
                {
                    var take = Math.Clamp(options.CurrentValue.PageSize, 1, 100);
                    var observations = await source.ReadChangesAsync(checkpoint.CursorUtc, checkpoint.CursorId, take, ct);
                    foreach (var observation in observations) await coordinator.ObserveAsync(observation, startup.StartedAtUtc!.Value, ct);
                    var groups = observations.GroupBy(x => new { x.ChangedAtUtc, x.SourceId }).ToArray();
                    if (groups.Length > 0)
                    {
                        var last = groups.Last().Key; checkpoint.CursorUtc = last.ChangedAtUtc; checkpoint.CursorId = last.SourceId;
                    }
                    checkpoint.Revision++;
                    if (groups.Length < take)
                    {
                        checkpoint.StartedAtUtc ??= coordinator.Now;
                        if (source.RepeatActiveSweep) { checkpoint.CursorUtc = DateTime.UnixEpoch; checkpoint.CursorId = ""; }
                    }
                    await db.SaveChangesAsync(ct);
                    if (groups.Length < take) break;
                }
                await coordinator.ObserveAsync(new("monitor", source.Key, "SourceReadFailed", "Warning", false, coordinator.Now), startup.StartedAtUtc!.Value, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                // Failed/partial reads never prove an incident resolved; raw exceptions are not persisted.
                db.ChangeTracker.Clear();
                await coordinator.ObserveAsync(new("monitor", source.Key, "SourceReadFailed", "Warning", true, coordinator.Now), startup.StartedAtUtc!.Value, ct);
                allCaughtUp = false;
            }
            allCaughtUp &= checkpoint.StartedAtUtc is not null;
        }
        // Fresh reread of active sources, even when completion removed them from active scan.
        var active = await db.Set<OperationsAlert>().AsNoTracking().Where(x => x.Status != "Resolved" && x.SourceKind != "monitor")
            .OrderBy(x => x.LastVerifiedAtUtc).Take(100).ToListAsync(ct);
        foreach (var row in active)
        {
            var source = sources.FirstOrDefault(x => x.Key == row.SourceKind);
            if (source is null) continue;
            try { foreach (var current in await source.ReadCurrentAsync(row.SourceId, ct)) await coordinator.ObserveAsync(current, startup.StartedAtUtc!.Value, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { db.ChangeTracker.Clear(); }
        }
        startup = await db.Set<OperationsAlertCheckpoint>().SingleAsync(x => x.Id == "startup", ct);
        if (allCaughtUp && startup.CursorId != "notified")
        { startup.CursorId = "notified"; startup.Revision++; coordinator.Queue(null, 0, "Startup", startup.StartedAtUtc!.Value); await db.SaveChangesAsync(ct); }
        await coordinator.ScheduleAsync(ct);
    }
}
