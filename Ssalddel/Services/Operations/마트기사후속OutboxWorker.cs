namespace Ssalddel.Services.Operations;

/// <summary>기본 OFF. 명시 Simulation 관문이 열린 호스트에서만 SQL 확정 후 후속 사본을 갱신합니다.</summary>
public sealed class 마트기사후속OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<마트기사후속OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                if (scope.ServiceProvider.GetRequiredService<I마트기사업무Gate>().Enabled)
                    await scope.ServiceProvider.GetRequiredService<I마트기사후속OutboxService>().처리Async(50, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("살뜰마트 기사 후속 처리 실패. SQL 원본은 유지하며 재시도를 기다립니다."); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
