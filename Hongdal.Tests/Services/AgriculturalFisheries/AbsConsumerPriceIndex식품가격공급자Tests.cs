using System.Net;
using System.Text;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class AbsConsumerPriceIndex식품가격공급자Tests
{
    [Fact]
    public async Task 식품가격지수를_Sdmx조건으로조회하고_기간순으로변환한다()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return JsonResponse(SampleResponse);
        });
        var provider = CreateProvider(handler);

        var result = await provider.조회Async(new 호주농수산식품가격조회요청
        {
            IndexCode = 호주식품가격지수Codes.FishAndOtherSeafood,
            MeasureCode = 호주식품가격지수측정Codes.IndexNumber,
            RegionCode = 호주식품가격지수지역Codes.Australia,
            StartPeriod = "2026-04",
            EndPeriod = "2026-05",
            MaxItems = 10
        });

        Assert.True(result.Success);
        Assert.Equal(호주농수산식품가격조회상태Codes.완료, result.StatusCode);
        Assert.Equal(["2026-04", "2026-05"], result.Items.Select(item => item.ReferencePeriod));
        Assert.Equal([107.02m, 108.57m], result.Items.Select(item => item.NumericValue));
        var first = result.Items[0];
        Assert.Equal("어류·기타 수산물", first.IndexLabel);
        Assert.Equal("Fish and other seafood", first.OfficialIndexLabel);
        Assert.Equal("호주 8개 주도시 가중평균", first.RegionLabel);
        Assert.Equal("IN", first.UnitCode);
        Assert.Equal("Index Numbers", first.UnitLabel);
        Assert.Equal("Sep 2025 = 100.0", first.BasePeriod);
        Assert.False(result.IsActualUnitPrice);
        Assert.Contains(result.Notices, notice => notice.Contains("A$/kg", StringComparison.Ordinal));
        Assert.NotNull(requestedUri);
        Assert.Equal(
            "/rest/data/CPI/1.40015.10.50.M",
            requestedUri!.AbsolutePath);
        var query = Uri.UnescapeDataString(requestedUri.Query);
        Assert.Contains("startPeriod=2026-04", query, StringComparison.Ordinal);
        Assert.Contains("endPeriod=2026-05", query, StringComparison.Ordinal);
        Assert.Contains("format=jsondata", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 지원하지않는지수코드는_외부호출없이_잘못된요청을반환한다()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("호출되면 안 됩니다."));
        var provider = CreateProvider(handler);

        var result = await provider.조회Async(new 호주농수산식품가격조회요청
        {
            IndexCode = "unknown",
            StartPeriod = "2026-04",
            EndPeriod = "2026-05"
        });

        Assert.False(result.Success);
        Assert.Equal(호주농수산식품가격조회상태Codes.잘못된요청, result.StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    private static AbsConsumerPriceIndex식품가격공급자 CreateProvider(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://data.api.abs.gov.au/")
        };
        return new AbsConsumerPriceIndex식품가격공급자(
            client,
            Options.Create(new PublicDataOptions
            {
                AbsConsumerPriceIndex = new AbsConsumerPriceIndexOptions
                {
                    DataPath = "/rest/data/CPI"
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

    private const string SampleResponse =
        """
        {
          "meta": {
            "prepared": "2026-07-18T10:59:30Z"
          },
          "data": {
            "dataSets": [
              {
                "attributes": [0],
                "series": {
                  "0:0:0:0:0": {
                    "attributes": [0],
                    "observations": {
                      "1": [108.57],
                      "0": [107.02]
                    }
                  }
                }
              }
            ],
            "structures": [
              {
                "dimensions": {
                  "series": [
                    { "id": "MEASURE", "values": [{ "id": "1", "name": "Index numbers" }] },
                    { "id": "INDEX", "values": [{ "id": "40015", "name": "Fish and other seafood" }] },
                    { "id": "TSEST", "values": [{ "id": "10", "name": "Original" }] },
                    { "id": "REGION", "values": [{ "id": "50", "name": "Australia" }] },
                    { "id": "FREQ", "values": [{ "id": "M", "name": "Monthly" }] }
                  ],
                  "observation": [
                    {
                      "id": "TIME_PERIOD",
                      "values": [
                        { "id": "2026-04", "name": "2026-04" },
                        { "id": "2026-05", "name": "2026-05" }
                      ]
                    }
                  ]
                },
                "attributes": {
                  "dataSet": [
                    {
                      "id": "BASE_PERIOD",
                      "values": [{ "id": "25", "name": "Sep 2025 = 100.0" }]
                    }
                  ],
                  "series": [
                    {
                      "id": "UNIT_MEASURE",
                      "values": [{ "id": "IN", "name": "Index Numbers" }]
                    }
                  ]
                }
              }
            ]
          },
          "errors": []
        }
        """;
}
