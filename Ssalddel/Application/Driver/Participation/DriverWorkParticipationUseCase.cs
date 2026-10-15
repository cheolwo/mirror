using System.Data;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.도메인.기사;

namespace Ssalddel.Application.Driver.Participation;

public interface IDriverWorkParticipationUseCase
{
    Task<Result<DriverWorkParticipationResponse>> GetAsync(string? driverId, CancellationToken ct = default);
    Task<Result<DriverWorkParticipationResponse>> UpdateAsync(string? driverId, DriverWorkParticipationUpdate? request, CancellationToken ct = default);
}

public static class DriverWorkParticipationValidation
{
    public static bool IsValid(DriverWorkParticipationUpdate? request, int operationalMax)
    {
        if (request?.Settings is not { } s || request.ExpectedRevision < 0 || request.ExpectedRevision == long.MaxValue
            || s.Revision != request.ExpectedRevision || operationalMax is < 1 or > 10
            || s.MaxConcurrentJobs < 1 || s.MaxConcurrentJobs > operationalMax
            || !DriverVehicleKinds.IsKnown(s.VehicleKind)
            || (s.DirectPicking && !s.QuickDelivery && !s.CargoTransport)) return false;
        return DriverWorkParticipationSettingsRules.ValidMeasure(s.CapacityKg, DriverWorkParticipationSettingsRules.MaxCapacityKg)
            && DriverWorkParticipationSettingsRules.ValidMeasure(s.CapacityLitres, DriverWorkParticipationSettingsRules.MaxCapacityLitres);
    }

    public static DriverWorkParticipationSettings Read(DriverWorkParticipationRecord? row) => row is null ? new() : new()
    {
        Revision = row.Revision, FoodDelivery = row.FoodDelivery, QuickDelivery = row.QuickDelivery,
        CargoTransport = row.CargoTransport, DirectPicking = row.DirectPicking, VehicleKind = row.VehicleKind,
        MaxConcurrentJobs = row.MaxConcurrentJobs, CapacityKg = row.CapacityKg, CapacityLitres = row.CapacityLitres,
        FoodCargoSeparationConfirmed = row.FoodCargoSeparationConfirmed, ReceiveAdditionalOffers = row.ReceiveAdditionalOffers
    };

    public static DriverWorkParticipationRecord New(string userId, DriverWorkParticipationSettings s, DateTime now) => new()
    {
        UserId = userId, Revision = 1, WorkLockRevision = 0, FoodDelivery = s.FoodDelivery, QuickDelivery = s.QuickDelivery,
        CargoTransport = s.CargoTransport, DirectPicking = s.DirectPicking, VehicleKind = s.VehicleKind,
        MaxConcurrentJobs = s.MaxConcurrentJobs, CapacityKg = s.CapacityKg, CapacityLitres = s.CapacityLitres,
        FoodCargoSeparationConfirmed = s.FoodCargoSeparationConfirmed, ReceiveAdditionalOffers = s.ReceiveAdditionalOffers,
        UpdatedAtUtc = now
    };
}

[SsalddelUseCase("기사 업무 선택", Summary = "본인의 희망 업무와 적재 한도를 저장하며 실제 수행 권한은 별도로 확인합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Driver)]
public sealed class DriverWorkParticipationUseCase(SsalddelContext db, IOptions<SharedDriverWorkOptions> options, TimeProvider clock,
    ISharedDriverWorkAdmissionService admission)
    : IDriverWorkParticipationUseCase
{
    public async Task<Result<DriverWorkParticipationResponse>> GetAsync(string? driverId, CancellationToken ct = default)
    {
        var eligible = await EligibilityAsync(driverId, ct);
        if (eligible.IsFailed) return Result.Fail<DriverWorkParticipationResponse>(eligible.Errors);
        return Response(driverId!, await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == driverId, ct), eligible.Value);
    }

    public async Task<Result<DriverWorkParticipationResponse>> UpdateAsync(string? driverId, DriverWorkParticipationUpdate? request, CancellationToken ct = default)
    {
        var eligible = await EligibilityAsync(driverId, ct);
        if (eligible.IsFailed) return Result.Fail<DriverWorkParticipationResponse>(eligible.Errors);
        if (!DriverWorkParticipationValidation.IsValid(request, options.Value.OperationalMaxConcurrentJobs))
            return Fail("업무 종류, 차량, 동시 업무 수와 적재 한도를 확인해 주세요.", "DriverWork.SettingsInvalid", 400);
        var input = request!.Settings; // Immutable request snapshot; no role or work state is modified.
        using var lease = await admission.EnterAsync(driverId!, ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            foreach (var entry in db.ChangeTracker.Entries<DriverWorkParticipationRecord>().Where(x => x.Entity.UserId == driverId).ToArray())
                entry.State = EntityState.Detached;
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var rows = db.Set<DriverWorkParticipationRecord>();
            var now = clock.GetUtcNow().UtcDateTime;
            if (request.ExpectedRevision == 0)
            {
                var added = DriverWorkParticipationValidation.New(driverId!, input, now);
                rows.Add(added);
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    db.Entry(added).State = EntityState.Detached;
                    if (tx is not null) await tx.RollbackAsync(ct);
                    return Conflict();
                }
            }
            else if (db.Database.IsRelational())
            {
                // UPDATE acquires the same row lock as every admission path. CAS also protects concurrent editors.
                var changed = await rows.Where(x => x.UserId == driverId && x.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Revision, x => x.Revision + 1)
                        .SetProperty(x => x.WorkLockRevision, x => x.WorkLockRevision + 1)
                        .SetProperty(x => x.FoodDelivery, input.FoodDelivery).SetProperty(x => x.QuickDelivery, input.QuickDelivery)
                        .SetProperty(x => x.CargoTransport, input.CargoTransport).SetProperty(x => x.DirectPicking, input.DirectPicking)
                        .SetProperty(x => x.VehicleKind, input.VehicleKind).SetProperty(x => x.MaxConcurrentJobs, input.MaxConcurrentJobs)
                        .SetProperty(x => x.CapacityKg, input.CapacityKg).SetProperty(x => x.CapacityLitres, input.CapacityLitres)
                        .SetProperty(x => x.FoodCargoSeparationConfirmed, input.FoodCargoSeparationConfirmed)
                        .SetProperty(x => x.ReceiveAdditionalOffers, input.ReceiveAdditionalOffers).SetProperty(x => x.UpdatedAtUtc, now), ct);
                if (changed != 1) return Conflict();
            }
            else
            {
                var tracked = await rows.SingleOrDefaultAsync(x => x.UserId == driverId, ct);
                if (tracked is null || tracked.Revision != request.ExpectedRevision) return Conflict();
                var replacement = DriverWorkParticipationValidation.New(driverId!, input, now);
                replacement.Revision = tracked.Revision + 1; replacement.WorkLockRevision = tracked.WorkLockRevision + 1;
                db.Entry(tracked).CurrentValues.SetValues(replacement);
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateConcurrencyException) { db.Entry(tracked).State = EntityState.Detached; return Conflict(); }
            }
            if (tx is not null) await tx.CommitAsync(ct);
            var persisted = await rows.AsNoTracking().SingleAsync(x => x.UserId == driverId, ct);
            return Response(driverId!, persisted, eligible.Value);
        });
    }

    private async Task<Result<(bool Food, bool Cargo)>> EligibilityAsync(string? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return Result.Fail<(bool, bool)>(Error("기사 인증 정보가 없습니다.", "DriverAuthenticationRequired", 401));
        var food = await db.배달기사.AsNoTracking().AnyAsync(x => x.기사Id == id && x.상태 == "활동중", ct);
        var cargo = await db.용달기사.AsNoTracking().AnyAsync(x => x.기사Id == id && x.상태 == "활동중", ct);
        if (!food && !cargo) return Result.Fail<(bool, bool)>(Error("활동 가능한 기사 프로필을 먼저 확인해 주세요.", "DriverProfileRequired", 403));
        return Result.Ok((food, cargo));
    }

    private DriverWorkParticipationResponse Response(string id, DriverWorkParticipationRecord? row, (bool Food, bool Cargo) eligible) => new()
    {
        DriverId = id, Settings = DriverWorkParticipationValidation.Read(row), SharedAdmissionEnabled = options.Value.Enabled,
        FoodEligible = eligible.Food, CargoEligible = eligible.Cargo, OperationalMaxConcurrentJobs = options.Value.OperationalMaxConcurrentJobs
    };
    private static Error Error(string message, string code, int status) => new Error(message).WithMetadata("Code", code).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status);
    private static Result<DriverWorkParticipationResponse> Fail(string message, string code, int status) => Result.Fail<DriverWorkParticipationResponse>(Error(message, code, status));
    private static Result<DriverWorkParticipationResponse> Conflict() => Fail("다른 화면에서 설정이 변경됐습니다. 다시 확인해 주세요.", "DriverWork.SettingsRevisionConflict", 409);
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var cause = ex.GetBaseException();
        return cause.GetType().FullName == "MySqlConnector.MySqlException" && Equals(cause.GetType().GetProperty("Number")?.GetValue(cause), 1062)
            || cause.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
            && cause.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(cause) is int code && code is 1555 or 2067;
    }
}
