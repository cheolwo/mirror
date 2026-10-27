using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ssalddel.Services.OperationsAlerts;

public static class OperationsAlertsRegistration
{
    public static IServiceCollection AddOperationsAlerts(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OperationsAlertsOptions>(configuration.GetSection(OperationsAlertsOptions.SectionName));
        services.Configure<OperationsBudgetOptions>(configuration.GetSection("OperationsBudget"));
        services.AddScoped<OperationsBudgetGate>(); services.AddTransient<OperationsRouteBudgetHandler>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<OperationsAlertQueryService>();
        services.AddScoped<OperationsAlertCoordinator>(); services.AddScoped<OperationsAlertMonitor>(); services.AddScoped<OperationsAlertDispatcher>();
        services.AddScoped<IOperationsAlertSource, SupportOperationsAlertSource>();
        services.AddScoped<IOperationsAlertSource, FoodOperationsAlertSource>();
        services.AddScoped<IOperationsAlertSource, MemberOperationsAlertSource>();
        services.AddScoped<IOperationsAlertSource, CargoOperationsAlertSource>();
        services.AddScoped<IOperationsAlertSource, MartReceiptOperationsAlertSource>();
        services.AddScoped<IOperationsAlertSource, DistributedSiteOperationsAlertSource>();
        services.AddScoped<IOperationsAlertSource, OperationsBudgetAlertSource>();
        services.AddScoped<IOperationsAlertSource, NotificationOperationsAlertSource>();
        services.AddScoped<IOperationsAlertChannel, OperationsAlertEmailChannel>(); services.AddScoped<IOperationsAlertChannel, OperationsAlertPushChannel>();
        services.AddHostedService<OperationsAlertsWorker>(); return services;
    }
}
public sealed class OperationsAlertsWorker(IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptionsMonitor<OperationsAlertsOptions> options,
    ILogger<OperationsAlertsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.CurrentValue.Enabled)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<OperationsAlertMonitor>().ScanAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<OperationsAlertDispatcher>().SendPendingAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch { logger.LogWarning("OperationsAlertCycleFailed; external availability monitoring remains independent."); }
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.CurrentValue.PollSeconds, 5, 60)), stoppingToken);
        }
    }
}
