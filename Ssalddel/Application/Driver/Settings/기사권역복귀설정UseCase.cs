using FluentResults;
using Microsoft.AspNetCore.Http;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Driver.Settings;

namespace Ssalddel.Application.Driver.Settings;

public interface I기사권역복귀설정UseCase
{
    Task<Result<기사권역복귀설정응답>> 조회Async(string? driverId, CancellationToken cancellationToken = default);
    Task<Result<기사권역복귀설정응답>> 저장Async(string? driverId, 기사권역복귀설정수정요청? request, CancellationToken cancellationToken = default);
}

[SsalddelUseCase("기사 권역 복귀 설정", Summary = "현재 위치와 별도로 기사 본인의 비공개 복귀 기준점과 사용 방식을 저장합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Driver)]
public sealed class 기사권역복귀설정UseCase(SsalddelContext db, I기사권역복귀설정Store store) : I기사권역복귀설정UseCase
{
    public async Task<Result<기사권역복귀설정응답>> 조회Async(string? driverId, CancellationToken cancellationToken = default)
    {
        var error = await 기사검증Async(driverId, cancellationToken);
        if (error is not null) return Result.Fail<기사권역복귀설정응답>(error);
        return 응답(driverId!, await store.조회Async(driverId!, cancellationToken));
    }

    public async Task<Result<기사권역복귀설정응답>> 저장Async(string? driverId, 기사권역복귀설정수정요청? request, CancellationToken cancellationToken = default)
    {
        var error = await 기사검증Async(driverId, cancellationToken);
        if (error is not null) return Result.Fail<기사권역복귀설정응답>(error);
        if (request is null || request.ExpectedRevision == long.MaxValue || !기사권역복귀설정검증.유효(request.설정, request.ExpectedRevision))
            return 실패("기준점과 반경, 사용 방식을 확인해 주세요.", "DriverReturnAreaSettingsInvalid", StatusCodes.Status400BadRequest);
        try { return 응답(driverId!, await store.저장Async(driverId!, request.설정!, request.ExpectedRevision, cancellationToken)); }
        catch (기사권역복귀설정ConflictException)
        { return 실패("다른 화면에서 설정이 변경됐습니다. 다시 조회한 뒤 저장해 주세요.", "DriverReturnAreaSettingsRevisionConflict", StatusCodes.Status409Conflict); }
    }

    private async Task<Error?> 기사검증Async(string? driverId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(driverId))
            return new Error("기사 인증 정보가 없습니다.").WithMetadata("StatusCode", StatusCodes.Status401Unauthorized).WithMetadata("Code", "DriverAuthenticationRequired");
        if (!await db.용달기사.AsNoTracking().AnyAsync(x => x.기사Id == driverId, cancellationToken)
            && !await db.배달기사.AsNoTracking().AnyAsync(x => x.기사Id == driverId, cancellationToken))
            return new Error("기사 프로필을 먼저 등록해 주세요.").WithMetadata("StatusCode", StatusCodes.Status404NotFound).WithMetadata("Code", "DriverProfileRequired");
        return null;
    }

    private static 기사권역복귀설정응답 응답(string driverId, 기사권역복귀설정Snapshot snapshot)
        => new() { DriverId = driverId, 설정 = snapshot.설정, 활성여부 = snapshot.활성여부, 위치판정시각Utc = snapshot.위치판정시각Utc };
    private static Result<기사권역복귀설정응답> 실패(string message, string code, int status)
        => Result.Fail<기사권역복귀설정응답>(new Error(message).WithMetadata("Code", code).WithMetadata("StatusCode", status));
}
