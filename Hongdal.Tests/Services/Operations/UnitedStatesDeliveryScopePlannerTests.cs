using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;
using Microsoft.Extensions.Options;

namespace Hongdal.Tests.Services.Operations;

public sealed class UnitedStatesDeliveryScopePlannerTests
{
    [Fact]
    public void Build_PrefersPlaceAndKeepsZctaSeparateFromMailingZip()
    {
        var sut = CreateSut();
        var address = CreateAddress(
            postalCode: "20500",
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.State,
                "11",
                "District of Columbia"),
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.County,
                "11001",
                "District of Columbia"),
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.IncorporatedPlace,
                "1150000",
                "Washington city"),
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.ZipCodeTabulationArea,
                "20006",
                "20006"));

        var result = sut.Build(address, participantCount: 5);

        Assert.True(result.Success);
        Assert.Equal("us-place:1150000", result.RecommendedScopeKey);
        Assert.Equal("us-place:1150000", result.RecommendedDemandConsolidationScopeKey);
        Assert.Equal("Public_AR_Current", result.ProviderDatasetVersion);
        Assert.Equal("Current_Current", result.ProviderGeographyVintage);
        Assert.Equal(
            OperatingDeliveryScopeLogisticsPolicyCodes.ConsolidateDemandThenValidateFulfillment,
            result.LogisticsEfficiencyPolicyCode);
        var place = Assert.Single(
            result.Items,
            item => item.ScopeTypeCode == OperatingDeliveryScopeTypeCodes.PlaceRecruitment);
        Assert.True(place.IsRecommendedRecruitmentScope);
        Assert.True(place.IsRecommendedDemandConsolidationScope);
        Assert.True(place.CanPublishForParticipantCount);
        Assert.Equal(
            OperatingDeliveryScopeLogisticsRoleCodes.UrbanHubConsolidation,
            place.LogisticsRoleCode);
        Assert.True(place.SupportsLastMileBatching);
        var zcta = Assert.Single(
            result.Items,
            item => item.ScopeTypeCode == OperatingDeliveryScopeTypeCodes.ZctaRecruitment);
        Assert.Equal("us-zcta:20006", zcta.ScopeKey);
        Assert.Equal(
            OperatingDeliveryScopeLogisticsRoleCodes.LastMileStopConsolidation,
            zcta.LogisticsRoleCode);
        Assert.True(zcta.SupportsLastMileBatching);
        Assert.DoesNotContain(address.PostalCode, zcta.ScopeKey, StringComparison.Ordinal);
        Assert.False(zcta.CanPublishForParticipantCount);
        Assert.Equal(10, zcta.MinimumParticipantsForPublicDisplay);
        Assert.All(result.Items, item => Assert.True(item.RequiresLogisticsFeasibilityValidation));
        Assert.All(result.Items, item => Assert.True(item.RequiresOperationalRouteValidation));
        var state = Assert.Single(
            result.Items,
            item => item.ScopeTypeCode == OperatingDeliveryScopeTypeCodes.StateDiscovery);
        Assert.Equal(
            OperatingDeliveryScopeLogisticsRoleCodes.RegionalInboundConsolidation,
            state.LogisticsRoleCode);
        Assert.False(state.SupportsLastMileBatching);
    }

    [Fact]
    public void Build_UsesCountyWhenAddressHasNoCensusPlace()
    {
        var sut = CreateSut();
        var address = CreateAddress(
            postalCode: "59001",
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.State,
                "30",
                "Montana"),
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.County,
                "30095",
                "Stillwater County"),
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.ZipCodeTabulationArea,
                "59001",
                "59001"));

        var result = sut.Build(address, participantCount: 3);

        Assert.True(result.Success);
        Assert.Equal("us-county:30095", result.RecommendedScopeKey);
        Assert.Equal("us-county:30095", result.RecommendedDemandConsolidationScopeKey);
        var county = Assert.Single(
            result.Items,
            item => item.ScopeTypeCode == OperatingDeliveryScopeTypeCodes.CountyRecruitment);
        Assert.True(county.IsRecommendedRecruitmentScope);
        Assert.True(county.IsRecommendedDemandConsolidationScope);
        Assert.True(county.CanPublishForParticipantCount);
        Assert.Equal(
            OperatingDeliveryScopeLogisticsRoleCodes.RuralRouteConsolidation,
            county.LogisticsRoleCode);
        Assert.True(county.SupportsLastMileBatching);
    }

    [Fact]
    public void Build_RejectsAddressWithoutCensusState()
    {
        var sut = CreateSut();
        var address = CreateAddress(
            postalCode: "00000",
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.County,
                "00000",
                "Unknown County"));

        var result = sut.Build(address);

        Assert.False(result.Success);
        Assert.Contains("state geography", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_ConnectsAddressLookupToDeliveryScopePlan()
    {
        var address = CreateAddress(
            "20746",
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.State,
                "24",
                "Maryland"),
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.County,
                "24033",
                "Prince George's County"),
            new GeographicAreaSeed(
                OperatingGeographicAreaTypeCodes.CensusDesignatedPlace,
                "2475725",
                "Suitland CDP"));
        var sut = new UnitedStatesDeliveryScopeService(
            new StubAddressLookupService(address),
            CreateSut());

        var result = await sut.ResolveAsync(
            "4600 Silver Hill Rd, Washington, DC 20233",
            participantCount: 5);

        Assert.True(result.Success);
        Assert.Equal("us-place:2475725", result.RecommendedScopeKey);
    }

    private static UnitedStatesDeliveryScopePlanner CreateSut()
        => new(Options.Create(new UnitedStatesAddressOptions()));

    private static OperatingMarketAddressCandidate CreateAddress(
        string postalCode,
        params GeographicAreaSeed[] areas)
        => new()
        {
            MarketCode = OperatingMarketCodes.UnitedStates,
            CountryCode = OperatingMarketCodes.UnitedStates,
            PostalCode = postalCode,
            ProviderCode = OperatingAddressProviderCodes.UnitedStatesCensusGeocoder,
            ProviderDatasetVersion = "Public_AR_Current",
            ProviderGeographyVintage = "Current_Current",
            GeographicAreas = areas.Select(area => new OperatingMarketGeographicArea
            {
                AreaTypeCode = area.AreaTypeCode,
                Code = area.Code,
                Name = area.Name
            }).ToArray()
        };

    private sealed record GeographicAreaSeed(
        string AreaTypeCode,
        string Code,
        string Name);

    private sealed class StubAddressLookupService(
        OperatingMarketAddressCandidate candidate) : IOperatingMarketAddressLookupService
    {
        public Task<OperatingMarketAddressSearchResult> SearchAsync(
            OperatingMarketAddressSearchRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new OperatingMarketAddressSearchResult
            {
                Success = true,
                ProviderConfigured = true,
                MarketCode = OperatingMarketCodes.UnitedStates,
                ProviderCode = OperatingAddressProviderCodes.UnitedStatesCensusGeocoder,
                Page = 1,
                PageSize = 1,
                TotalCount = 1,
                Items = [candidate]
            });
    }
}
