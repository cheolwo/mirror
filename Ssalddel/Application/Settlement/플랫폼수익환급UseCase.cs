using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.PlatformProfit;
using 살뜰.Services.Options;
using 살뜰.Services.Settlement;

namespace Ssalddel.Application.Settlement;

public interface I플랫폼수익환급UseCase
{
    Task<Result<PlatformRevenueEntryResponse>> 수익기록Async(PlatformRevenueEntryRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformProfitReturnPolicyResponse>> 정책생성Async(PlatformProfitReturnPolicyRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformProfitReturnPlanResponse>> 스케줄생성Async(PlatformProfitReturnScheduleCreateRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformProfitReturnScheduleListResponse>> 스케줄목록Async(string? participantUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.HrParticipation)]
[SsalddelUseCase("플랫폼 수익 환급", Summary = "플랫폼 수익을 참여자에게 환급하거나 정산할 정책과 지급 일정을 관리합니다.")]
[SsalddelUseCaseActor(SsalddelActor.PlatformOperator)]
[SsalddelUseCaseActor(SsalddelActor.Worker, SsalddelUseCaseActorRole.Supporting)]
public sealed class 플랫폼수익환급UseCase : I플랫폼수익환급UseCase
{
    private readonly IPlatformProfitReturnService _profitReturnService;
    private readonly ISsalddelExecutionModePolicy _executionMode;

    public 플랫폼수익환급UseCase(
        IPlatformProfitReturnService profitReturnService,
        ISsalddelExecutionModePolicy executionMode)
    {
        _profitReturnService = profitReturnService;
        _executionMode = executionMode;
    }

    public async Task<Result<PlatformRevenueEntryResponse>> 수익기록Async(
        PlatformRevenueEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!_executionMode.IsSimulation)
        {
            return Result.Fail<PlatformRevenueEntryResponse>(
                "수동 플랫폼 수익 입력은 Simulation 실행 모드에서만 허용됩니다.");
        }

        return Result.Ok(await _profitReturnService.RecordRevenueAsync(request, cancellationToken));
    }

    public async Task<Result<PlatformProfitReturnPolicyResponse>> 정책생성Async(
        PlatformProfitReturnPolicyRequest request,
        CancellationToken cancellationToken)
    {
        return Result.Ok(await _profitReturnService.CreatePolicyAsync(request, cancellationToken));
    }

    public async Task<Result<PlatformProfitReturnPlanResponse>> 스케줄생성Async(
        PlatformProfitReturnScheduleCreateRequest request,
        CancellationToken cancellationToken)
    {
        return Result.Ok(await _profitReturnService.CreateReturnSchedulesAsync(request, cancellationToken));
    }

    public async Task<Result<PlatformProfitReturnScheduleListResponse>> 스케줄목록Async(
        string? participantUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var items = await _profitReturnService.ListSchedulesAsync(participantUserId, from, to, cancellationToken);
        return Result.Ok(new PlatformProfitReturnScheduleListResponse { Items = items });
    }
}
