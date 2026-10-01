using System.Net;
using System.Text;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Tests.Ui.Common;

public sealed class 농수산공공데이터ClientTests
{
    [Fact]
    public async Task 미국가격조회는_공식필터를전송하고_503응답본문도보존한다()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(
                    """
                    {
                      "success": false,
                      "statusCode": "NotConfigured",
                      "errorMessage": "USDA NASS Quick Stats API 키가 설정되지 않았습니다.",
                      "sourceKey": "usda-nass-quickstats",
                      "query": {
                        "commodity": "CATFISH",
                        "program": "SURVEY",
                        "yearFrom": 2023,
                        "yearTo": 2026
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new 농수산공공데이터Client(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.hongdal.test/")
        });

        var result = await client.미국가격조회Async("catfish", "SURVEY", 2023, 2026);

        Assert.False(result.Success);
        Assert.Equal(미국농수산가격조회상태Codes.설정안됨, result.StatusCode);
        Assert.NotNull(requestedUri);
        var query = Uri.UnescapeDataString(requestedUri!.Query);
        Assert.Contains("commodity=catfish", query, StringComparison.Ordinal);
        Assert.Contains("statisticCategory=PRICE RECEIVED", query, StringComparison.Ordinal);
        Assert.Contains("aggregationLevel=NATIONAL", query, StringComparison.Ordinal);
        Assert.Contains("yearFrom=2023", query, StringComparison.Ordinal);
        Assert.Contains("yearTo=2026", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 국내가격조회는_HS코드와조회기간을안전하게전송한다()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "success": true,
                      "statusCode": "Complete",
                      "hsCode": "080810",
                      "summary": "사과 국내가격"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new 농수산공공데이터Client(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.hongdal.test/")
        });

        var result = await client.국내가격조회Async("080810", lookbackDays: 45);

        Assert.True(result.Success);
        Assert.Equal("080810", result.HsCode);
        Assert.NotNull(requestedUri);
        Assert.Equal(
            "/api/v1/agricultural-fisheries/items/080810/domestic-price",
            requestedUri!.AbsolutePath);
        Assert.Contains("lookbackDays=31", requestedUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 호주식품가격지수조회는_허용차원과기간을전송한다()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "success": true,
                      "statusCode": "Complete",
                      "sourceKey": "abs-cpi-food-price-index",
                      "summary": "ABS 식품 가격지수 2건을 제공합니다."
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new 농수산공공데이터Client(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.hongdal.test/")
        });

        var result = await client.호주식품가격지수조회Async(new 호주농수산식품가격조회요청
        {
            IndexCode = 호주식품가격지수Codes.FishAndOtherSeafood,
            MeasureCode = 호주식품가격지수측정Codes.PreviousYearPercentageChange,
            RegionCode = 호주식품가격지수지역Codes.Melbourne,
            StartPeriod = "2025-01",
            EndPeriod = "2026-05",
            MaxItems = 500
        });

        Assert.True(result.Success);
        Assert.NotNull(requestedUri);
        Assert.Equal(
            "/api/v1/agricultural-fisheries/au-food-price-indexes",
            requestedUri!.AbsolutePath);
        var query = Uri.UnescapeDataString(requestedUri.Query);
        Assert.Contains("indexCode=40015", query, StringComparison.Ordinal);
        Assert.Contains("measureCode=3", query, StringComparison.Ordinal);
        Assert.Contains("regionCode=2", query, StringComparison.Ordinal);
        Assert.Contains("startPeriod=2025-01", query, StringComparison.Ordinal);
        Assert.Contains("endPeriod=2026-05", query, StringComparison.Ordinal);
        Assert.Contains("maxItems=120", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 식품가격비교는_HS국가환율추가비용을조회조건으로전송한다()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "success": true,
                      "statusCode": "Complete",
                      "hsCode": "080810",
                      "countryCode": "US",
                      "summary": "가격 비교 완료"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new 농수산공공데이터Client(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.hongdal.test/")
        });

        var result = await client.식품가격비교Async(new FoodPriceComparisonRequest
        {
            HsCode = "0808.10",
            CountryCode = "US",
            DomesticLookbackDays = 45,
            ImportLookbackMonths = 24,
            FxRateKrwPerUsd = 1_375.5m,
            EstimatedImportAdditionalCostKrwPerKg = 2_000m
        });

        Assert.True(result.Success);
        Assert.NotNull(requestedUri);
        Assert.Equal(
            "/api/v1/customs/hs-codes/080810/food-price-comparison",
            requestedUri!.AbsolutePath);
        var query = Uri.UnescapeDataString(requestedUri.Query);
        Assert.Contains("countryCode=US", query, StringComparison.Ordinal);
        Assert.Contains("domesticLookbackDays=31", query, StringComparison.Ordinal);
        Assert.Contains("importLookbackMonths=12", query, StringComparison.Ordinal);
        Assert.Contains("fxRateKrwPerUsd=1375.5", query, StringComparison.Ordinal);
        Assert.Contains("estimatedImportAdditionalCostKrwPerKg=2000", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 수입평균단가조회는_시뮬레이션요청을POST로전송한다()
    {
        HttpMethod? requestedMethod = null;
        Uri? requestedUri = null;
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedMethod = request.Method;
            requestedUri = request.RequestUri;
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "success": true,
                      "hsCode": "080810",
                      "countryCode": "US",
                      "averageImportUnitValueKrwPerKg": 7000
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new 농수산공공데이터Client(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.hongdal.test/")
        });

        var result = await client.수입평균단가조회Async(new HsCountryMonthlyTradeUnitPriceRequest
        {
            HsCode = "080810",
            CountryCode = "US",
            Month = "202606",
            LookbackMonths = 3,
            ExpectedSellingUnitPriceKrwPerKg = 12_000m
        });

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Post, requestedMethod);
        Assert.Equal(
            "/api/v1/orderer/public-data/customs/hs-country-import-unit-price-simulation",
            requestedUri?.AbsolutePath);
        Assert.Contains("\"hsCode\":\"080810\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"expectedSellingUnitPriceKrwPerKg\":12000", requestBody, StringComparison.Ordinal);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
