using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hongdal.Infrastructure.Persistence.AgriculturalFisheries;

public sealed class AgriculturalFisheriesDatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgriculturalFisheriesDatabaseInitializer> _logger;

    public AgriculturalFisheriesDatabaseInitializer(
        IServiceScopeFactory scopeFactory,
        ILogger<AgriculturalFisheriesDatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("농수산 가격 아카이브 스키마를 확인했습니다.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
