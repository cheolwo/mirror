using Microsoft.Extensions.Options;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Services.Versioning;

public sealed class VersionFeatureFlagServiceTests
{
    [Fact]
    public void 운영지역장면은_다른업무Flag와분리된_명시적관찰Flag로만연다()
    {
        var closed = CreateService(new VersionFeatureFlagsOptions
        {
            FoodDeliveryWorkflow = true,
            WarehouseFulfillmentWorkflow = true
        });
        var opened = CreateService(new VersionFeatureFlagsOptions
        {
            OperationalWorldObservationWorkflow = true
        });

        Assert.False(closed.IsEnabled(VersionFeatureFlagKeys.OperationalWorldObservationWorkflow));
        Assert.True(opened.IsEnabled(VersionFeatureFlagKeys.OperationalWorldObservationWorkflow));
        Assert.True(opened.GetAll()[VersionFeatureFlagKeys.OperationalWorldObservationWorkflow]);
    }

    [Fact]
    public void 행정동후원표시는_행정동관찰과후원Flag가_모두켜져야연다()
    {
        var sponsorshipOnly = CreateService(new VersionFeatureFlagsOptions
        {
            LocalDioramaSponsorship = true
        });
        var both = CreateService(new VersionFeatureFlagsOptions
        {
            AdministrativeDongDioramaObservation = true,
            LocalDioramaSponsorship = true
        });

        Assert.False(sponsorshipOnly.IsEnabled(VersionFeatureFlagKeys.LocalDioramaSponsorship));
        Assert.True(both.IsEnabled(VersionFeatureFlagKeys.AdministrativeDongDioramaObservation));
        Assert.True(both.IsEnabled(VersionFeatureFlagKeys.LocalDioramaSponsorship));
    }

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

    [Fact]
    public void GroupPurchaseDemand_CanRunWithoutTradeTransportOrFulfillment()
    {
        var service = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = true,
            GroupPurchaseDemandWorkflow = true
        });

        Assert.True(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow));
        Assert.True(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.DomesticTransportWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.HrParticipationWorkflow));
    }

    [Fact]
    public void GroupPurchasePractice_CanRunWithoutDemandOrTrade()
    {
        var service = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = true,
            GroupPurchasePracticeWorkflow = true,
            GroupPurchaseDemandWorkflow = false,
            CustomsAndTradeDataWorkflow = false
        });

        Assert.True(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchasePracticeWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow));
    }

    [Fact]
    public void GroupPurchasePractice_RequiresCommunityFoundation()
    {
        var service = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = false,
            GroupPurchasePracticeWorkflow = true
        });

        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchasePracticeWorkflow));
    }

    [Fact]
    public void LegacyGroupPurchaseImportKey_EnablesDemandOnly()
    {
        var service = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = true,
            GroupPurchaseImportWorkflow = true
        });

        Assert.True(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow));
        Assert.True(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.DomesticTransportWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow));
    }

    [Fact]
    public void GroupPurchaseDemand_RequiresCommunityFoundation()
    {
        var service = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = false,
            GroupPurchaseDemandWorkflow = true
        });

        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.GroupPurchaseImportWorkflow));
    }

    [Fact]
    public void TradeReadiness_RequiresDemandAndDoesNotEnableTransportOrWarehouse()
    {
        var customsOnly = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = true,
            CustomsAndTradeDataWorkflow = true
        });
        var fullReadiness = CreateService(new VersionFeatureFlagsOptions
        {
            CommunityTrustWorkflow = true,
            GroupPurchaseDemandWorkflow = true,
            CustomsAndTradeDataWorkflow = true
        });

        Assert.False(customsOnly.IsEnabled(VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow));
        Assert.True(fullReadiness.IsEnabled(VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow));
        Assert.True(fullReadiness.IsEnabled(VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow));
        Assert.False(fullReadiness.IsEnabled(VersionFeatureFlagKeys.DomesticTransportWorkflow));
        Assert.False(fullReadiness.IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow));
        Assert.False(fullReadiness.IsEnabled(VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow));
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
