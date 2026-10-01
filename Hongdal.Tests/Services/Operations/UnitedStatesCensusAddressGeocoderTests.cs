using System.Net;
using System.Text;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Hongdal.Tests.Services.Operations;

public sealed class UnitedStatesCensusAddressGeocoderTests
{
    [Fact]
    public async Task GeocodeAsync_MapsOfficialCensusResponse()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(CensusResponseWithMatch)
        });
        var sut = CreateSut(handler);

        var result = await sut.GeocodeAsync(
            "1600 Pennsylvania Ave NW, Washington, DC 20500");

        var item = Assert.Single(result.Items);
        Assert.True(result.Success);
        Assert.True(result.ProviderConfigured);
        Assert.Equal("Public_AR_Current", result.DatasetVersion);
        Assert.Equal("Current_Current", result.GeographyVintage);
        Assert.Equal(
            "1600 PENNSYLVANIA AVE NW, WASHINGTON, DC, 20500",
            item.MatchedAddress);
        Assert.Equal("WASHINGTON", item.City);
        Assert.Equal("DC", item.StateCode);
        Assert.Equal("20500", item.PostalCode);
        Assert.Equal("76225813", item.TigerLineId);
        Assert.Equal(38.89869893252d, item.Latitude);
        Assert.Equal(-77.03518753691d, item.Longitude);
        Assert.Contains(
            item.GeographicAreas,
            area => area.AreaTypeCode == OperatingGeographicAreaTypeCodes.State &&
                    area.Code == "11");
        Assert.Contains(
            item.GeographicAreas,
            area => area.AreaTypeCode ==
                        OperatingGeographicAreaTypeCodes.ZipCodeTabulationArea &&
                    area.Code == "20006");

        Assert.Equal(
            "/geocoder/geographies/onelineaddress",
            handler.RequestUri?.AbsolutePath);
        var query = QueryHelpers.ParseQuery(handler.RequestUri?.Query ?? string.Empty);
        Assert.Equal(
            "1600 Pennsylvania Ave NW, Washington, DC 20500",
            query["address"].ToString());
        Assert.Equal("Public_AR_Current", query["benchmark"].ToString());
        Assert.Equal("Current_Current", query["vintage"].ToString());
        Assert.Equal("2,28,30,80,82", query["layers"].ToString());
        Assert.Equal("json", query["format"].ToString());
    }

    [Fact]
    public async Task GeocodeAsync_NoMatchIsSuccessfulEmptyResult()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                """
                {
                  "result": {
                    "input": {
                      "benchmark": { "benchmarkName": "Public_AR_Current" }
                    },
                    "addressMatches": []
                  }
                }
                """)
        });
        var sut = CreateSut(handler);

        var result = await sut.GeocodeAsync("1 Unknown Road, Nowhere, ZZ 00000");

        Assert.True(result.Success);
        Assert.True(result.ProviderConfigured);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GeocodeAsync_DisabledProviderDoesNotSendRequest()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException());
        var sut = CreateSut(handler, enabled: false);

        var result = await sut.GeocodeAsync(
            "1600 Pennsylvania Ave NW, Washington, DC 20500");

        Assert.False(result.Success);
        Assert.False(result.ProviderConfigured);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GeocodeAsync_HttpFailureDoesNotExposeAddressInError()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var sut = CreateSut(handler);

        var result = await sut.GeocodeAsync(
            "1600 Pennsylvania Ave NW, Washington, DC 20500");

        Assert.False(result.Success);
        Assert.True(result.ProviderConfigured);
        Assert.Contains("HTTP 503", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Pennsylvania", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static UnitedStatesCensusAddressGeocoder CreateSut(
        HttpMessageHandler handler,
        bool enabled = true)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://geocoding.geo.census.gov/")
        };
        var options = Options.Create(new UnitedStatesAddressOptions
        {
            CensusGeocoder = new UnitedStatesCensusGeocoderOptions
            {
                Enabled = enabled
            }
        });

        return new UnitedStatesCensusAddressGeocoder(httpClient, options);
    }

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }

    private const string CensusResponseWithMatch =
        """
        {
          "result": {
            "input": {
              "address": {
                "address": "1600 Pennsylvania Ave NW, Washington, DC 20500"
              },
              "benchmark": {
                "isDefault": true,
                "benchmarkDescription": "Public Address Ranges - Current Benchmark",
                "id": "4",
                "benchmarkName": "Public_AR_Current"
              },
              "vintage": {
                "isDefault": true,
                "id": "4",
                "vintageName": "Current_Current",
                "vintageDescription": "Current Vintage - Current Benchmark"
              }
            },
            "addressMatches": [
              {
                "tigerLine": {
                  "side": "L",
                  "tigerLineId": "76225813"
                },
                "coordinates": {
                  "x": -77.03518753691,
                  "y": 38.89869893252
                },
                "addressComponents": {
                  "zip": "20500",
                  "streetName": "PENNSYLVANIA",
                  "city": "WASHINGTON",
                  "state": "DC",
                  "suffixType": "AVE",
                  "suffixDirection": "NW"
                },
                "geographies": {
                  "States": [
                    {
                      "NAME": "District of Columbia",
                      "GEOID": "11"
                    }
                  ],
                  "2020 Census ZIP Code Tabulation Areas": [
                    {
                      "NAME": "ZCTA5 20006",
                      "BASENAME": "20006",
                      "GEOID": "20006",
                      "ZCTA5": "20006"
                    }
                  ],
                  "Incorporated Places": [
                    {
                      "NAME": "Washington city",
                      "GEOID": "1150000"
                    }
                  ],
                  "Counties": [
                    {
                      "NAME": "District of Columbia",
                      "GEOID": "11001"
                    }
                  ]
                },
                "matchedAddress": "1600 PENNSYLVANIA AVE NW, WASHINGTON, DC, 20500"
              }
            ]
          }
        }
        """;
}
