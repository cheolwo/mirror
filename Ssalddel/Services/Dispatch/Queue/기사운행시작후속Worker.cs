using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.Services.Dispatch.Queue;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Infrastructure,
    "주기적으로 새 scope에서 운행 시작 후속 대기를 재처리한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation,
    FlowOrder = 170,
    StepKey = "infrastructure.work-start-worker",
    DependsOnStepKeys = ["infrastructure.work-start-followup"],
    ExecutionStage = SsalddelCodeExecutionStage.Tick,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "기존 pending 작업만 처리하며 새 운행 시작이나 자동 수신 ON을 만들지 않는다. worker 반복은 실제 인덱스 성공·배차 수락·운영 활성화 증거가 아니다.")]
public sealed class 기사운행시작후속Worker(IServiceScopeFactory scopes, ILogger<기사운행시작후속Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<I기사운행시작후속Service>().대기처리Async(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "근무 인덱스 재처리 조회에 실패했습니다. 다음 주기에 다시 확인합니다."); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
