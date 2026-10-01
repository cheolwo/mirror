using System.Net;
using System.Text;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class UsdaNassQuickStats가격공급자Tests
{
    [Fact]
    public async Task API키가없으면_외부호출없이_설정안됨을반환한다()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("호출되면 안 됩니다."));
        var provider = CreateProvider(handler, apiKey: string.Empty);

        var result = await provider.조회Async(new 미국농수산가격조회요청
        {
            Commodity = "CATFISH",
            YearFrom = 2025,
            YearTo = 2026
        });

        Assert.False(result.Success);
        Assert.Equal(미국농수산가격조회상태Codes.설정안됨, result.StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task 양식수산물가격을_공식필터로조회하고_숫자값을변환한다()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return JsonResponse(
                """
                {
                  "data": [
                    {
                      "commodity_desc": "CATFISH",
                      "class_desc": "FOODSIZE",
                      "short_desc": "CATFISH, FOODSIZE - PRICE RECEIVED, MEASURED IN $ / LB",
                      "sector_desc": "ANIMALS & PRODUCTS",
                      "group_desc": "AQUACULTURE",
                      "statisticcat_desc": "PRICE RECEIVED",
                      "unit_desc": "$ / LB",
                      "Value": "1.25",
                      "source_desc": "SURVEY",
                      "agg_level_desc": "NATIONAL",
                      "state_alpha": "",
                      "state_name": "UNITED STATES",
                      "year": "2025",
                      "freq_desc": "ANNUAL",
                      "reference_period_desc": "YEAR",
                      "load_time": "2026-01-15 15:00:00"
                    }
                  ]
                }
                """);
        });
        var provider = CreateProvider(handler);

        var result = await provider.조회Async(new 미국농수산가격조회요청
        {
            Commodity = " catfish ",
            StatisticCategory = "price received",
            Program = "survey",
            Group = "aquaculture",
            YearFrom = 2024,
            YearTo = 2025
        });

        Assert.True(result.Success);
        Assert.Equal(미국농수산가격조회상태Codes.완료, result.StatusCode);
        var item = Assert.Single(result.Items);
        Assert.Equal("CATFISH", item.Commodity);
        Assert.Equal("AQUACULTURE", item.Group);
        Assert.Equal("$ / LB", item.Unit);
        Assert.Equal(1.25m, item.NumericValue);
        Assert.False(item.IsSuppressed);
        Assert.NotNull(requestedUri);
        var query = Uri.UnescapeDataString(requestedUri!.Query);
        Assert.Contains("commodity_desc=CATFISH", query, StringComparison.Ordinal);
        Assert.Contains("statisticcat_desc=PRICE RECEIVED", query, StringComparison.Ordinal);
        Assert.Contains("source_desc=SURVEY", query, StringComparison.Ordinal);
        Assert.Contains("group_desc=AQUACULTURE", query, StringComparison.Ordinal);
        Assert.Contains("year__GE=2024", query, StringComparison.Ordinal);
        Assert.Contains("year__LE=2025", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 비공개표시는_원문을보존하고_숫자로변환하지않는다()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            """
            {
              "data": [
                {
                  "commodity_desc": "TROUT",
                  "statisticcat_desc": "PRICE RECEIVED",
                  "unit_desc": "$ / LB",
                  "Value": "(D)",
                  "source_desc": "CENSUS",
                  "agg_level_desc": "NATIONAL",
                  "year": "2023"
                }
              ]
            }
            """));
        var provider = CreateProvider(handler);

        var result = await provider.조회Async(new 미국농수산가격조회요청
        {
            Commodity = "TROUT",
            Program = "CENSUS",
            YearFrom = 2023,
            YearTo = 2023
        });

        var item = Assert.Single(result.Items);
        Assert.Equal("(D)", item.RawValue);
        Assert.Null(item.NumericValue);
        Assert.True(item.IsSuppressed);
    }

    private static UsdaNassQuickStats가격공급자 CreateProvider(
        HttpMessageHandler handler,
        string apiKey = "test-key")
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://quickstats.nass.usda.gov/")
        };
        return new UsdaNassQuickStats가격공급자(
            client,
            Options.Create(new PublicDataOptions
            {
                UsdaNassQuickStats = new UsdaNassQuickStatsOptions
                {
                    ApiKey = apiKey,
                    DataPath = "/api/api_GET/"
                }
            }));
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
