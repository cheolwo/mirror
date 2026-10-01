using Hongdal.Application.Operations;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Extensions;
using Hongdal.Services.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Services.Operations;

public sealed class OperatingMarketServiceModuleTests
{
    [Fact]
    public void Registration_DefaultsToKoreaModule()
    {
        var services = RegisterServices(marketCode: null);

        AssertMarketModule<KoreaOperatingMarketServiceModule,
            KoreaRoadAddressLookupAdapter,
            KoreaOperatingMarketFreightWorkflowPolicy>(
            services,
            OperatingMarketCodes.Korea);

        var deployment = GetDeployment(services);
        Assert.Equal(OperatingTimeZoneIds.Korea, deployment.TimeZoneId);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitedStatesAddressGeocoder));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitedStatesDeliveryScopePlanner));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitedStatesDeliveryScopeService));
        AssertDirectoryService<UnavailableThirdPartyLogisticsProviderDirectoryService>(services);
        AssertOutreachService<
            UnavailableThirdPartyLogisticsProviderOutreachPreparationService>(services);
    }

    [Fact]
    public void Registration_SelectsUnitedStatesModuleOnly()
    {
        var services = RegisterServices(OperatingMarketCodes.UnitedStates);

        AssertMarketModule<UnitedStatesOperatingMarketServiceModule,
            UnitedStatesAddressLookupAdapter,
            UnitedStatesOperatingMarketFreightWorkflowPolicy>(
            services,
            OperatingMarketCodes.UnitedStates);

        var deployment = GetDeployment(services);
        Assert.Equal(OperatingTimeZoneIds.CoordinatedUniversal, deployment.TimeZoneId);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitedStatesAddressGeocoder));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitedStatesDeliveryScopePlanner));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitedStatesDeliveryScopeService));
        AssertDirectoryService<UnitedStatesThirdPartyLogisticsProviderDirectoryService>(services);
        AssertOutreachService<
            UnitedStatesThirdPartyLogisticsProviderOutreachPreparationService>(services);
    }

    [Fact]
    public void Registration_ReadsStructuredPartnerVerificationFromServerConfiguration()
    {
        var services = RegisterServices(
            OperatingMarketCodes.UnitedStates,
            includeCompletePartner: true,
            timeZoneId: "America/Chicago");

        var deployment = GetDeployment(services);
        Assert.Equal("broker-participant-1", deployment.VerifiedLicensedBrokerPartnerId);
        Assert.Equal("America/Chicago", deployment.TimeZoneId);

        var registryDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                          typeof(IOperatingMarketFreightServiceProviderRegistry));
        var registry = Assert.IsType<DeploymentOperatingMarketFreightServiceProviderRegistry>(
            registryDescriptor.ImplementationInstance);
        var verification = Assert.IsType<OperatingMarketFreightServiceProviderVerification>(
            registry.Current);

        Assert.Equal("MC-123456", verification.AuthorityReference);
        Assert.Equal(
            FreightServiceProviderRoleCodes.UnitedStatesPropertyBroker,
            verification.ServiceProviderRoleCode);
        Assert.Contains(
            FreightComplianceRequirementCodes.UnitedStatesFinancialSecurityActive,
            verification.SatisfiedRequirementCodes);
        Assert.Equal(
            new DateTimeOffset(2027, 7, 1, 0, 0, 0, TimeSpan.Zero),
            verification.VerificationExpiresAtUtc);
    }

    [Fact]
    public void Registration_LegacyPartnerIdDoesNotInventComplianceEvidence()
    {
        var configuration = CreateConfiguration(
            OperatingMarketCodes.UnitedStates,
            legacyPartnerId: "broker-partner-1");
        var services = new ServiceCollection();

        services.AddHongdalOperatingMarketServices(configuration);

        var registryDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                          typeof(IOperatingMarketFreightServiceProviderRegistry));
        var registry = Assert.IsAssignableFrom<IOperatingMarketFreightServiceProviderRegistry>(
            registryDescriptor.ImplementationInstance);
        var verification = Assert.IsType<OperatingMarketFreightServiceProviderVerification>(
            registry.Current);

        Assert.Equal("broker-partner-1", verification.ServiceProviderParticipantId);
        Assert.Empty(verification.AuthorityReference);
        Assert.Empty(verification.SatisfiedRequirementCodes);
    }

    [Fact]
    public void Registration_RejectsUnsupportedMarket()
    {
        var configuration = CreateConfiguration("JP");
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddHongdalOperatingMarketServices(configuration));

        Assert.Contains("must be KR or US", exception.Message);
    }

    private static ServiceCollection RegisterServices(
        string? marketCode,
        bool includeCompletePartner = false,
        string? timeZoneId = null)
    {
        var services = new ServiceCollection();
        services.AddHongdalOperatingMarketServices(CreateConfiguration(
            marketCode,
            includeCompletePartner,
            timeZoneId));
        return services;
    }

    private static IConfiguration CreateConfiguration(
        string? marketCode,
        bool includeCompletePartner = false,
        string? timeZoneId = null,
        string? legacyPartnerId = null)
    {
        var values = new Dictionary<string, string?>();
        if (marketCode is not null)
        {
            values[$"{OperatingMarketDeploymentOptions.SectionName}:MarketCode"] = marketCode;
        }

        if (timeZoneId is not null)
        {
            values[$"{OperatingMarketDeploymentOptions.SectionName}:TimeZoneId"] = timeZoneId;
        }

        if (legacyPartnerId is not null)
        {
            values[$"{OperatingMarketDeploymentOptions.SectionName}:" +
                   nameof(OperatingMarketDeploymentOptions.VerifiedLicensedBrokerPartnerId)] =
                legacyPartnerId;
        }

        if (includeCompletePartner)
        {
            var prefix =
                $"{OperatingMarketDeploymentOptions.SectionName}:FreightServiceProvider";
            values[$"{prefix}:ParticipantId"] = "broker-participant-1";
            values[$"{prefix}:ParticipantRoleCode"] =
                FreightServiceProviderRoleCodes.UnitedStatesPropertyBroker;
            values[$"{prefix}:AuthorityReference"] = "MC-123456";
            values[$"{prefix}:VerifiedAtUtc"] = "2026-07-01T00:00:00+00:00";
            values[$"{prefix}:VerificationExpiresAtUtc"] = "2027-07-01T00:00:00+00:00";
            values[$"{prefix}:SatisfiedRequirementCodes:0"] =
                FreightComplianceRequirementCodes.UnitedStatesBrokerAuthorityActive;
            values[$"{prefix}:SatisfiedRequirementCodes:1"] =
                FreightComplianceRequirementCodes.UnitedStatesFinancialSecurityActive;
            values[$"{prefix}:SatisfiedRequirementCodes:2"] =
                FreightComplianceRequirementCodes.UnitedStatesProcessAgentDesignationActive;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static IOperatingMarketDeployment GetDeployment(IServiceCollection services)
    {
        var deploymentDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOperatingMarketDeployment));
        return Assert.IsType<OperatingMarketDeployment>(
            deploymentDescriptor.ImplementationInstance);
    }

    private static void AssertDirectoryService<TService>(IServiceCollection services)
    {
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                          typeof(IThirdPartyLogisticsProviderDirectoryService));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<TService>(scope.ServiceProvider
            .GetRequiredService<IThirdPartyLogisticsProviderDirectoryService>());
    }

    private static void AssertOutreachService<TService>(IServiceCollection services)
    {
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                          typeof(IThirdPartyLogisticsProviderOutreachPreparationService));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<TService>(scope.ServiceProvider
            .GetRequiredService<
                IThirdPartyLogisticsProviderOutreachPreparationService>());
    }

    private static void AssertMarketModule<TModule, TAddressAdapter, TFreightPolicy>(
        IServiceCollection services,
        string expectedMarketCode)
    {
        Assert.Equal(expectedMarketCode, GetDeployment(services).MarketCode);

        var moduleDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOperatingMarketServiceModule));
        Assert.IsType<TModule>(moduleDescriptor.ImplementationInstance);

        var addressDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOperatingMarketAddressLookupAdapter));
        Assert.Equal(typeof(TAddressAdapter), addressDescriptor.ImplementationType);

        var policyDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOperatingMarketFreightWorkflowPolicy));
        Assert.NotNull(policyDescriptor.ImplementationFactory);

        var contextDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOperatingMarketContextAccessor));
        Assert.Equal(
            typeof(DeploymentOperatingMarketContextAccessor),
            contextDescriptor.ImplementationType);

        var runtimeProfileDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOperatingMarketRuntimeProfileService));
        Assert.Equal(
            typeof(OperatingMarketRuntimeProfileService),
            runtimeProfileDescriptor.ImplementationType);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<TFreightPolicy>(
            scope.ServiceProvider.GetRequiredService<IOperatingMarketFreightWorkflowPolicy>());
        Assert.Equal(
            expectedMarketCode,
            scope.ServiceProvider
                .GetRequiredService<IOperatingMarketRuntimeProfileService>()
                .GetCurrent()
                .MarketCode);
    }
}
