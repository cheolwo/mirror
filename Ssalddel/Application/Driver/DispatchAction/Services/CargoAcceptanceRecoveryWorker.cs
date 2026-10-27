using Microsoft.Extensions.Options;
using Ssalddel.Infrastructure.BackgroundJobs;

namespace Ssalddel.Application.Driver.DispatchAction;

/// <summary>기본 OFF. 기존 운영 모드/화물 기능 관문을 통과한 경우에만 영속 수락 후속을 재개합니다.</summary>
public sealed class CargoAcceptanceRecoveryWorker(IServiceScopeFactory scopes,
    IOptions<CargoAcceptanceRecoveryOptions> options, ILogger<CargoAcceptanceRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(options.Value.IntervalSeconds, 5, 300)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (scope.ServiceProvider.GetRequiredService<ISsalddelBackgroundJobActivationPolicy>()
                    .Evaluate(SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch).IsEnabled)
                    await scope.ServiceProvider.GetRequiredService<ICargoAcceptanceRecoveryService>()
                        .ProcessPendingAsync(options.Value.BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Cargo acceptance recovery sweep failed; persisted jobs remain pending."); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
