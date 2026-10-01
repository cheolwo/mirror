using System.Net;
using System.Text;
using Hongdal.Contracts.Common.PublicData;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.PublicData;

public sealed class HsCountryTradeUnitPriceLookupServiceTests
{
    [Fact]
    public async Task 수입평균단가조회_신고금액과중량의가중평균을원화로환산한다()
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
                      "response": {
                        "body": {
                          "items": {
                            "item": [
                              { "year": "202601", "impWgt": "100", "impDlr": "200" },
                              { "year": "202602", "impWgt": "300", "impDlr": "900" }
                            ]
                          }
                        }
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://apis.data.go.kr/")
        };
        var service = new HsCountryTradeUnitPriceLookupService(
            httpClient,
            Options.Create(new PublicDataOptions
            {
                CustomsTradeStatistics = new CustomsTradeStatisticsOptions
                {
                    ServiceKey = "test-key",
                    HsCountryMonthlyPath = "/1220000/nitemtrade/getNitemtradeList"
                }
            }));

        var result = await service.SimulateImportUnitPriceAsync(new HsCountryMonthlyTradeUnitPriceRequest
        {
            HsCode = "0901.21",
            CountryCode = "cn",
            Month = "202603",
            LookbackMonths = 3,
            ExpectedFxRateKrwPerUsd = 1350m
        });

        Assert.True(result.Success);
        Assert.Equal("090121", result.HsCode);
        Assert.Equal("CN", result.CountryCode);
        Assert.Equal("202601", result.StartMonth);
        Assert.Equal("202603", result.EndMonth);
        Assert.Equal(400m, result.TotalImportWeightKg);
        Assert.Equal(1100m, result.TotalImportValueUsd);
        Assert.Equal(2.75m, result.AverageImportUnitValueUsdPerKg);
        Assert.Equal(3713m, result.AverageImportUnitValueKrwPerKg);
        Assert.NotNull(requestedUri);
        Assert.Contains("strtYymm=202601", requestedUri!.Query, StringComparison.Ordinal);
        Assert.Contains("endYymm=202603", requestedUri.Query, StringComparison.Ordinal);
        Assert.Contains("hsSgn=090121", requestedUri.Query, StringComparison.Ordinal);
        Assert.Contains("cntyCd=CN", requestedUri.Query, StringComparison.Ordinal);
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
