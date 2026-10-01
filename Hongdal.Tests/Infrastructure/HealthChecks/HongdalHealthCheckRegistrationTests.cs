using Hongdal.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Infrastructure.HealthChecks;

public sealed class HongdalHealthCheckRegistrationTests
{
    [Fact]
    public void AddHongdalHealthChecks_RegistersSeparateLivenessAndReadinessChecks()
    {
        var services = new ServiceCollection();

        services.AddHongdalHealthChecks();

        using var provider = services.BuildServiceProvider();
        var registrations = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations
            .ToDictionary(registration => registration.Name);

        Assert.Equal(["live"], registrations["self"].Tags);
        Assert.Equal(["ready"], registrations["persistence"].Tags);
        Assert.Equal(HealthStatus.Unhealthy, registrations["persistence"].FailureStatus);
        Assert.Equal(TimeSpan.FromSeconds(15), registrations["persistence"].Timeout);
    }

    [Fact]
    public void DatabaseInitializationOptions_AreSafeForMultiInstanceProductionByDefault()
    {
        var options = new DatabaseInitializationOptions();

        Assert.False(options.RunAtStartup);
        Assert.True(options.FailOnError);
    }
}
