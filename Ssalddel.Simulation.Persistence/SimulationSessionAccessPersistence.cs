using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace Ssalddel.Simulation.Persistence;

public sealed record SimulationSessionAccessReservation(
    string SubjectId,
    string SessionStableId,
    bool Granted,
    bool Created);

public interface ISimulationSessionAccessLedger
{
    Task<SimulationSessionAccessReservation> ReserveAsync(
        string subjectId,
        string sessionStableId,
        CancellationToken cancellationToken);

    Task<bool> OwnsAsync(
        string subjectId,
        string sessionStableId,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        SimulationSessionAccessReservation reservation,
        CancellationToken cancellationToken);
}

public sealed class InMemorySimulationSessionAccessLedger
    : ISimulationSessionAccessLedger
{
    private readonly ConcurrentDictionary<string, string> owners =
        new(StringComparer.Ordinal);

    public Task<SimulationSessionAccessReservation> ReserveAsync(
        string subjectId,
        string sessionStableId,
        CancellationToken cancellationToken)
    {
        Validate(subjectId, sessionStableId);
        cancellationToken.ThrowIfCancellationRequested();
        var created = owners.TryAdd(sessionStableId, subjectId);
        var granted = created || string.Equals(
            owners[sessionStableId],
            subjectId,
            StringComparison.Ordinal);
        return Task.FromResult(new SimulationSessionAccessReservation(
            subjectId,
            sessionStableId,
            granted,
            created));
    }

    public Task<bool> OwnsAsync(
        string subjectId,
        string sessionStableId,
        CancellationToken cancellationToken)
    {
        Validate(subjectId, sessionStableId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(owners.TryGetValue(sessionStableId, out var owner)
            && string.Equals(owner, subjectId, StringComparison.Ordinal));
    }

    public Task ReleaseAsync(
        SimulationSessionAccessReservation reservation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        cancellationToken.ThrowIfCancellationRequested();
        if (reservation.Created)
        {
            if (owners.TryGetValue(reservation.SessionStableId, out var owner)
                && string.Equals(owner, reservation.SubjectId, StringComparison.Ordinal))
            {
                owners.TryRemove(reservation.SessionStableId, out _);
            }
        }
        return Task.CompletedTask;
    }

    private static void Validate(string subjectId, string sessionStableId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("로그인 주체 식별자가 필요합니다.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(sessionStableId))
            throw new ArgumentException("Simulation 세션 식별자가 필요합니다.", nameof(sessionStableId));
    }
}

public sealed class EfSimulationSessionAccessLedger(
    IDbContextFactory<SimulationSessionDbContext> dbContextFactory)
    : ISimulationSessionAccessLedger
{
    public async Task<SimulationSessionAccessReservation> ReserveAsync(
        string subjectId,
        string sessionStableId,
        CancellationToken cancellationToken)
    {
        Validate(subjectId, sessionStableId);
        await using var db = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        var existing = await db.SessionAccessLedgers.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.SessionStableId == sessionStableId,
                cancellationToken);
        if (existing is not null)
            return Existing(subjectId, sessionStableId, existing);

        var now = DateTimeOffset.UtcNow;
        db.SessionAccessLedgers.Add(new SimulationSession접근원장Entity
        {
            SessionStableId = sessionStableId,
            SubjectId = subjectId,
            AccessRoleCode = "Owner",
            Revision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new SimulationSessionAccessReservation(
                subjectId,
                sessionStableId,
                Granted: true,
                Created: true);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            existing = await db.SessionAccessLedgers.AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.SessionStableId == sessionStableId,
                    cancellationToken);
            if (existing is null)
                throw;
            return Existing(subjectId, sessionStableId, existing);
        }
    }

    public async Task<bool> OwnsAsync(
        string subjectId,
        string sessionStableId,
        CancellationToken cancellationToken)
    {
        Validate(subjectId, sessionStableId);
        await using var db = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        return await db.SessionAccessLedgers.AsNoTracking().AnyAsync(
            value => value.SessionStableId == sessionStableId
                     && value.SubjectId == subjectId,
            cancellationToken);
    }

    public async Task ReleaseAsync(
        SimulationSessionAccessReservation reservation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!reservation.Created)
            return;

        await using var db = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        var entity = await db.SessionAccessLedgers.SingleOrDefaultAsync(
            value => value.SessionStableId == reservation.SessionStableId
                     && value.SubjectId == reservation.SubjectId,
            cancellationToken);
        if (entity is null)
            return;
        db.SessionAccessLedgers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static SimulationSessionAccessReservation Existing(
        string subjectId,
        string sessionStableId,
        SimulationSession접근원장Entity existing)
        => new(
            subjectId,
            sessionStableId,
            string.Equals(existing.SubjectId, subjectId, StringComparison.Ordinal),
            Created: false);

    private static void Validate(string subjectId, string sessionStableId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("로그인 주체 식별자가 필요합니다.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(sessionStableId))
            throw new ArgumentException("Simulation 세션 식별자가 필요합니다.", nameof(sessionStableId));
    }
}
