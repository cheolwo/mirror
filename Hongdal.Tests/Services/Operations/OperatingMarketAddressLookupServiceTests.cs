using Hongdal.Contracts.Common.Operations;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Services.Operations;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Operations;

public sealed class OperatingMarketAddressLookupServiceTests
{
    [Fact]
    public async Task SearchAsync_UsesCurrentMarketWhenRequestOmitsMarket()
    {
        var adapter = new RecordingAdapter(OperatingMarketCodes.UnitedStates);
        var service = new OperatingMarketAddressLookupService(
            new OperatingMarketDeployment(OperatingMarketCodes.UnitedStates),
            [adapter]);

        var result = await service.SearchAsync(new OperatingMarketAddressSearchRequest
        {
            Query = "1600 Amphitheatre Parkway",
            Page = 0,
            PageSize = 100
        });

        Assert.True(result.Success);
        Assert.Equal(OperatingMarketCodes.UnitedStates, adapter.LastRequest?.MarketCode);
        Assert.Equal(1, adapter.LastRequest?.Page);
        Assert.Equal(30, adapter.LastRequest?.PageSize);
    }

    [Fact]
    public async Task SearchAsync_RejectsUnsupportedMarket()
    {
        var service = new OperatingMarketAddressLookupService(
            new OperatingMarketDeployment(OperatingMarketCodes.Korea),
            [new RecordingAdapter(OperatingMarketCodes.Korea)]);

        var result = await service.SearchAsync(new OperatingMarketAddressSearchRequest
        {
            MarketCode = "JP",
            Query = "Tokyo"
        });

        Assert.False(result.Success);
        Assert.Equal(OperatingMarketAddressErrorCodes.UnsupportedMarket, result.ErrorCode);
    }

    [Fact]
    public async Task SearchAsync_RejectsMarketFromAnotherDeployment()
    {
        var service = new OperatingMarketAddressLookupService(
            new OperatingMarketDeployment(OperatingMarketCodes.Korea),
            [new RecordingAdapter(OperatingMarketCodes.Korea)]);

        var result = await service.SearchAsync(new OperatingMarketAddressSearchRequest
        {
            MarketCode = OperatingMarketCodes.UnitedStates,
            Query = "1600 Amphitheatre Parkway"
        });

        Assert.False(result.Success);
        Assert.Equal(
            OperatingMarketAddressErrorCodes.MarketNotAvailableInDeployment,
            result.ErrorCode);
    }

    [Fact]
    public async Task KoreaAdapter_MapsRoadAddressProviderResponse()
    {
        var adapter = new KoreaRoadAddressLookupAdapter(
            new StubRoadAddressLookupService(),
            Options.Create(new PublicDataOptions { ServiceKey = "configured" }));

        var result = await adapter.SearchAsync(new OperatingMarketAddressSearchRequest
        {
            MarketCode = OperatingMarketCodes.Korea,
            Query = "Sejong-daero 110",
            Page = 1,
            PageSize = 10
        });

        var item = Assert.Single(result.Items);
        Assert.True(result.Success);
        Assert.True(result.ProviderConfigured);
        Assert.Equal(OperatingAddressProviderCodes.KoreaRoadNameAddress, result.ProviderCode);
        Assert.Equal("04524", item.PostalCode);
        Assert.Equal("building-1", item.ProviderReference);
    }

    [Fact]
    public async Task UnitedStatesAdapter_MapsCensusAddressRangeMatch()
    {
        var adapter = new UnitedStatesAddressLookupAdapter(
            new StubUnitedStatesAddressGeocoder());

        var result = await adapter.SearchAsync(new OperatingMarketAddressSearchRequest
        {
            MarketCode = OperatingMarketCodes.UnitedStates,
            Query = "1600 Pennsylvania Ave NW, Washington, DC 20500"
        });

        var item = Assert.Single(result.Items);
        Assert.True(result.Success);
        Assert.True(result.ProviderConfigured);
        Assert.Equal(
            OperatingAddressProviderCodes.UnitedStatesCensusGeocoder,
            result.ProviderCode);
        Assert.Equal("WASHINGTON", item.Locality);
        Assert.Equal("DC", item.AdministrativeAreaCode);
        Assert.Equal("20500", item.PostalCode);
        Assert.Equal(OperatingAddressMatchPrecisionCodes.AddressRange, item.MatchPrecisionCode);
        Assert.Equal("Public_AR_Current", item.ProviderDatasetVersion);
        Assert.Equal("Current_Current", item.ProviderGeographyVintage);
        Assert.Equal(38.89869893252d, item.Latitude);
        Assert.Equal(-77.03518753691d, item.Longitude);
        Assert.Contains(
            item.GeographicAreas,
            area => area.AreaTypeCode == OperatingGeographicAreaTypeCodes.County &&
                    area.Code == "11001");
    }

    [Fact]
    public void Constructor_RejectsAdapterWithUnsupportedMarket()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new OperatingMarketAddressLookupService(
                new OperatingMarketDeployment(OperatingMarketCodes.Korea),
                [new RecordingAdapter("JP")]));
    }

    [Fact]
    public void Constructor_RejectsAdapterForAnotherDeployment()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new OperatingMarketAddressLookupService(
                new OperatingMarketDeployment(OperatingMarketCodes.Korea),
                [new RecordingAdapter(OperatingMarketCodes.UnitedStates)]));
    }

    private sealed class RecordingAdapter(string marketCode) : IOperatingMarketAddressLookupAdapter
    {
        public string MarketCode { get; } = marketCode;

        public string ProviderCode => "TestProvider";

        public OperatingMarketAddressSearchRequest? LastRequest { get; private set; }

        public Task<OperatingMarketAddressSearchResult> SearchAsync(
            OperatingMarketAddressSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new OperatingMarketAddressSearchResult
            {
                Success = true,
                ProviderConfigured = true,
                MarketCode = MarketCode,
                ProviderCode = ProviderCode,
                Page = request.Page,
                PageSize = request.PageSize
            });
        }
    }

    private sealed class StubRoadAddressLookupService : IRoadAddressLookupService
    {
        public Task<PublicDataLookupResponse<RoadAddressItem>> SearchAsync(
            RoadAddressSearchRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PublicDataLookupResponse<RoadAddressItem>
            {
                Success = true,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = 1,
                Items =
                [
                    new RoadAddressItem
                    {
                        RoadAddress = "110 Sejong-daero, Jung-gu, Seoul",
                        JibunAddress = "31 Taepyeongno 1-ga, Jung-gu, Seoul",
                        ZipCode = "04524",
                        AdministrativeCode = "1114010300",
                        BuildingManagementNo = "building-1",
                        RoadNameManagementNo = "road-1"
                    }
                ]
            });
    }

    private sealed class StubUnitedStatesAddressGeocoder : IUnitedStatesAddressGeocoder
    {
        public Task<UnitedStatesAddressGeocodeResult> GeocodeAsync(
            string address,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new UnitedStatesAddressGeocodeResult
            {
                Success = true,
                ProviderConfigured = true,
                DatasetVersion = "Public_AR_Current",
                GeographyVintage = "Current_Current",
                Items =
                [
                    new UnitedStatesAddressGeocodeCandidate
                    {
                        MatchedAddress =
                            "1600 PENNSYLVANIA AVE NW, WASHINGTON, DC, 20500",
                        City = "WASHINGTON",
                        StateCode = "DC",
                        PostalCode = "20500",
                        Latitude = 38.89869893252d,
                        Longitude = -77.03518753691d,
                        TigerLineId = "76225813",
                        GeographicAreas =
                        [
                            new OperatingMarketGeographicArea
                            {
                                AreaTypeCode = OperatingGeographicAreaTypeCodes.State,
                                Code = "11",
                                Name = "District of Columbia"
                            },
                            new OperatingMarketGeographicArea
                            {
                                AreaTypeCode = OperatingGeographicAreaTypeCodes.County,
                                Code = "11001",
                                Name = "District of Columbia"
                            }
                        ]
                    }
                ]
            });
    }
}
