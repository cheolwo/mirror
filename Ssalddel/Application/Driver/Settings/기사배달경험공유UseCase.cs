using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.DeliveryExperience;
using 살뜰.Data;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Driver.Settings;

[SsalddelUseCase("기사 배달 경험 공유", Summary = "본인의 선택 공유를 판본 보호로 변경하며 운영 기능은 기본 비활성입니다.")]
[SsalddelUseCaseActor(SsalddelActor.Driver)]
public sealed class 기사배달경험공유UseCase(SsalddelContext db, 음식배달경험Service service, TimeProvider clock)
{
    public async Task<Result<DeliveryExperienceSharingPreferenceDto>> 조회Async(string? driverId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(driverId)) return 실패("기사 인증 정보가 없습니다.", "DriverAuthenticationRequired", 401);
        var setting = service.Enabled ? await db.Set<음식배달경험공유설정>().AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == driverId, ct) : null;
        return Result.Ok(ToDto(setting));
    }

    public async Task<Result<DeliveryExperienceSharingPreferenceDto>> 저장Async(string? driverId, DeliveryExperienceSharingPreferenceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(driverId)) return 실패("기사 인증 정보가 없습니다.", "DriverAuthenticationRequired", 401);
        if (!service.Enabled) return 실패("배달 경험 공유가 준비 중입니다.", "DeliveryExperienceDisabled", 503);
        if (request.ExpectedRevision < 0 || request.ExpectedRevision == long.MaxValue) return 실패("설정 판본을 확인해 주세요.", "RevisionInvalid", 400);
        var setting = await db.Set<음식배달경험공유설정>().SingleOrDefaultAsync(x => x.기사Id == driverId, ct);
        if ((setting?.Revision ?? 0) != request.ExpectedRevision) return 충돌();
        var created = setting is null;
        if (created) { setting = new() { 기사Id = driverId }; db.Set<음식배달경험공유설정>().Add(setting); }
        setting!.현재관계자공유 = request.ShareWithCurrentParticipants;
        setting.Revision++;
        setting.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.Entry(setting).State = EntityState.Detached; return 충돌(); }
        catch (DbUpdateException ex) when (created && 고유키충돌(ex)) { db.Entry(setting).State = EntityState.Detached; return 충돌(); }
        // 연결/저장 장애는 동시 변경 409로 위장하지 않습니다. 기존 오류 경계가 처리합니다.
        return Result.Ok(ToDto(setting));
    }
    private DeliveryExperienceSharingPreferenceDto ToDto(음식배달경험공유설정? value)
        => new() { Enabled = service.Enabled, ShareWithCurrentParticipants = value?.현재관계자공유 ?? false,
            Revision = value?.Revision ?? 0, UpdatedAtUtc = value?.UpdatedAtUtc };
    private static Result<DeliveryExperienceSharingPreferenceDto> 충돌()
        => 실패("설정을 다시 확인해 주세요.", "DeliveryExperiencePreferenceChanged", 409);
    private static Result<DeliveryExperienceSharingPreferenceDto> 실패(string message, string code, int status)
        => Result.Fail<DeliveryExperienceSharingPreferenceDto>(new Error(message).WithMetadata("Code", code).WithMetadata("StatusCode", status));
    private static bool 고유키충돌(DbUpdateException exception)
    {
        var cause = exception.GetBaseException();
        return (cause.GetType().FullName == "MySqlConnector.MySqlException" && Equals(cause.GetType().GetProperty("Number")?.GetValue(cause), 1062))
            || (cause.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
                && cause.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(cause) is int sqliteCode && sqliteCode is 1555 or 2067);
    }
}
