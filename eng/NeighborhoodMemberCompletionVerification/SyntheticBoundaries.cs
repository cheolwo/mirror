using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Services.Community;
using Ssalddel.Services.Privacy;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;

namespace NeighborhoodMemberCompletionVerification;

/// <summary>실동의·성년 인증의 증거가 아닌 검증 입력. 로그나 HTTP로 비밀을 배포하지 않는다.</summary>
internal sealed class SyntheticConsent : I신청개인정보동의증적Service
{
    public Guid Id { get; } = Guid.NewGuid();
    public Task 유효한동의요구Async(Guid? id, string work, string source, string actor, CancellationToken ct = default)
    {
        if (id != Id || !actor.EndsWith("-requester", StringComparison.Ordinal)) throw new VerificationFailure("SyntheticConsentBindingMismatch");
        return Task.CompletedTask;
    }
    public Task<신청개인정보동의증적Response> 동의기록Async(신청개인정보동의기록Request request, string actor, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<신청개인정보동의증적Response?> 내증적조회Async(Guid id, string actor, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<신청개인정보동의증적Response> 철회Async(Guid id, 신청개인정보동의철회Request request, string actor, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>명시적 route fixture. IsActual=true는 서버 분기 검증용이며 실제 도로·GPS를 검증하지 않는다.</summary>
internal sealed class SyntheticRoute : I배차추천경로Service
{
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string option, CancellationToken ct)
        => Task.FromResult<배차경로예상결과?>(new(2.5m, TimeSpan.FromMinutes(2), 0, "r46-synthetic-route", true) { RouteOption = option });
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination) => throw new NotSupportedException();
    public Task<배차경로좌표?> ResolveOriginLocationAsync(string actor, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? condition) => throw new NotSupportedException();
    public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string actor, 용달기사? driver, DriverLocationSnapshot? location) => throw new NotSupportedException();
    public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => throw new NotSupportedException();
    public decimal? CalculateDistanceKm(배차경로좌표 origin, 배차경로좌표 destination) => throw new NotSupportedException();
}

internal sealed class FixtureActor(string actor) : ICurrentUserAccessor
{
    public string? UserId => actor;
    public string? Role => "일반회원";
}

internal static class FixtureMemberService
{
    // 표본 SQL 의뢰 등록만 내부 fixture 주체를 사용한다. 수행·수령의 모든 명령은 JWT HTTP다.
    public static NeighborhoodMemberDeliveryUseCase ForActor(IServiceProvider services, string actor) => new(
        services.GetRequiredService<SsalddelContext>(), new FixtureActor(actor),
        services.GetRequiredService<IOptions<NeighborhoodMemberDeliveryOptions>>(),
        services.GetRequiredService<ISsalddelExecutionModePolicy>(), services.GetRequiredService<TimeProvider>(),
        services.GetRequiredService<IPersonalDataEncryptionService>(), services.GetRequiredService<I신청개인정보동의증적Service>(),
        services.GetRequiredService<I배차추천경로Service>(), services.GetRequiredService<IObjectStorageService>(),
        executionOptions: services.GetRequiredService<IOptions<SsalddelExecutionOptions>>(),
        ledgerSync: services.GetRequiredService<I운송원장Mongo동기화Service>(), memberCollaborations: services.GetRequiredService<I생활협업Store>());
}
