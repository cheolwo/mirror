using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ssalddel.Simulation.Persistence;

namespace Ssalddel.Simulation.Hosting;

public sealed class SsalddelSimulationReadinessHealthCheck(
    IServiceScopeFactory scopeFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var checks = new List<string>();
        var probes = scope.ServiceProvider
            .GetServices<ISimulationDatabaseReadinessProbe>();
        foreach (var probe in probes)
        {
            if (!await probe.연결가능Async(cancellationToken))
                return HealthCheckResult.Unhealthy(
                    probe.데이터베이스이름 + "에 연결할 수 없습니다.");
            checks.Add(probe.데이터베이스이름);
        }

        return checks.Count == 0
            ? HealthCheckResult.Healthy("외부 DB 연결이 비활성인 Simulation host입니다.")
            : HealthCheckResult.Healthy(string.Join(", ", checks) + " 연결을 확인했습니다.");
    }
}
