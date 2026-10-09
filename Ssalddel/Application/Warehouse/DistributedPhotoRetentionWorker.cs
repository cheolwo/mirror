using Microsoft.Extensions.Options;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Services.Options;

namespace Ssalddel.Application.Warehouse;

public sealed class DistributedPhotoRetentionWorker(IServiceScopeFactory scopes, ILogger<DistributedPhotoRetentionWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope(); var services = scope.ServiceProvider;
                var policy = services.GetRequiredService<IOptions<DistributedFulfillmentOptions>>().Value;
                if (!policy.Enabled || policy.Profile != Ssalddel.Contracts.Common.Warehouse.DistributedFulfillmentCodes.Profile
                    || !services.GetRequiredService<ISsalddelExecutionModePolicy>().IsSimulation) continue;
                await services.GetRequiredService<DistributedPhotoEvidenceService>().SweepAsync(services.GetRequiredService<I보호지원Store>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("분산 업무 사진 정리를 완료하지 못했습니다. 다음 주기에 다시 확인합니다."); }
        }
    }
}
