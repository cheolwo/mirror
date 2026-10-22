using Ssalddel.Contracts.Common.Home;
using 살뜰.Services.Options;

namespace Ssalddel.Application.Home;

public interface I플랫폼참여현황조회UseCase
{
    Task<HomeParticipationSummaryResponse> 조회Async(CancellationToken cancellationToken);
}

/// <summary>전문 원장의 읽기 집계를 공개 숫자로 제한합니다. 금액·완료 상태·권한을 새로 확정하지 않습니다.</summary>
public sealed class 플랫폼참여현황조회UseCase(SsalddelContext db, ISsalddelExecutionModePolicy execution,
    TimeProvider clock) : I플랫폼참여현황조회UseCase
{
    public Task<HomeParticipationSummaryResponse> 조회Async(CancellationToken cancellationToken)
        => HomeParticipationAggregateQuery.ReadSummaryAsync(db, execution, clock, cancellationToken);
}
