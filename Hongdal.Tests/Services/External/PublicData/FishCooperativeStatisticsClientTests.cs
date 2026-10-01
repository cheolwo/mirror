using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.PublicData;

public sealed class FishCooperativeStatisticsClientTests
{
    [Fact]
    public async Task FetchGeneralStatisticsAsync_기준월과서비스키를전달하고임직원통계를변환한다()
    {
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return JsonResponse(
                """
                {
                  "response": {
                    "header": { "resultCode": "00", "resultMsg": "NORMAL SERVICE" },
                    "body": {
                      "items": {
                        "item": [
                          {
                            "basYm": "202605",
                            "title": "수협_일반현황_임직원현황",
                            "fncoCd": "001",
                            "fncoNm": "통영수산업협동조합",
                            "xcsmCnt": "127",
                            "xcsmDcd": "A",
                            "xcsmDcdNm": "총임직원"
                          }
                        ]
                      },
                      "totalCount": 1
                    }
                  }
                }
                """);
        });
        var sut = CreateClient(handler, "test-key");

        var result = await sut.FetchGeneralStatisticsAsync(new DateOnly(2026, 5, 1));

        var item = Assert.Single(result);
        Assert.Equal("202605", item.BaseYearMonth);
        Assert.Equal("통영수산업협동조합", item.FinancialCompanyName);
        Assert.Equal(127m, item.EmployeeCount);
        Assert.Equal("총임직원", item.EmployeeClassificationName);
        Assert.Contains("basYm=202605", requestUri!.Query);
        Assert.Contains("resultType=json", requestUri.Query);
        Assert.Contains("serviceKey=test-key", requestUri.Query);
        Assert.Contains("title=", requestUri.Query);
        Assert.Equal(
            "/1160100/service/GetFishCoopInfoService/getFishCoopGeneInfo",
            requestUri.AbsolutePath);
    }

    [Fact]
    public async Task FetchGeneralStatisticsAsync_공공데이터오류코드를성공으로숨기지않는다()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            """
            {
              "response": {
                "header": {
                  "resultCode": "30",
                  "resultMsg": "SERVICE KEY IS NOT REGISTERED ERROR"
                }
              }
            }
            """));
        var sut = CreateClient(handler, "invalid-key");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.FetchGeneralStatisticsAsync(new DateOnly(2026, 5, 1)));

        Assert.Contains("30", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SERVICE KEY", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FetchGeneralStatisticsAsync_서비스키가없으면외부호출전에실패한다()
    {
        var requested = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requested = true;
            return JsonResponse("{}");
        });
        var sut = CreateClient(handler, string.Empty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.FetchGeneralStatisticsAsync(new DateOnly(2026, 5, 1)));

        Assert.False(requested);
        Assert.Contains("DataGoKrServiceKey", exception.Message, StringComparison.Ordinal);
    }

    private static FishCooperativeStatisticsClient CreateClient(
        HttpMessageHandler handler,
        string serviceKey)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://apis.data.go.kr/")
        };
        return new FishCooperativeStatisticsClient(
            client,
            Options.Create(new PublicDataOptions
            {
                DataGoKrServiceKey = serviceKey,
                FishCooperativeStatistics = new FishCooperativeStatisticsOptions
                {
                    GeneralStatisticsPath =
                        "/1160100/service/GetFishCoopInfoService/getFishCoopGeneInfo",
                    PageSize = 1000
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
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
