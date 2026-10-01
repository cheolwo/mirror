using Microsoft.Extensions.Options;
using 홍달.Services.Options;
using 홍달.Services.Versioning;

namespace Hongdal.Tests.Services.Versioning;

public sealed class VersionFeatureFlagServiceTests
{
    [Fact]
    public void CommunityFoundation_CanRunWithoutDomesticTransport()
    {
        var service = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = true,
            DomesticTransportWorkflow = false,
            CargoYongdalV1 = false
        });

        Assert.True(service.IsEnabled(VersionFeatureFlagKeys.CommunityTrustWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.DomesticTransportWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.CargoYongdalV1));
    }

    [Fact]
    public void DomesticTransport_RequiresCommunityFoundation()
    {
        var service = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = false,
            DomesticTransportWorkflow = true,
            CargoYongdalV1 = true
        });

        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.CommunityTrustWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.DomesticTransportWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.CargoYongdalV1));
    }

    private static VersionFeatureFlagService CreateService(VersionFeatureFlagsOptions options)
        => new(new StaticOptionsMonitor<VersionFeatureFlagsOptions>(options));

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
