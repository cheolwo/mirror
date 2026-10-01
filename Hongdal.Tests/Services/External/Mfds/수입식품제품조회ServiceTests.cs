using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using 홍달.Services.External.Mfds;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.Mfds;

public sealed class 수입식품제품조회ServiceTests
{
    [Fact]
    public async Task 조회Async_현재공식경로와지원검색필드만사용한다()
    {
        Uri? 요청Uri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            요청Uri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <response>
                      <header><resultCode>00</resultCode><resultMsg>NORMAL SERVICE</resultMsg></header>
                      <body><numOfRows>10</numOfRows><pageNo>1</pageNo><totalCount>0</totalCount><items /></body>
                    </response>
                    """,
                    Encoding.UTF8,
                    "application/xml")
            };
        });
        var options = new 수입식품제품조회Options
        {
            ServiceKey = "test-key"
        };
        var service = new 수입식품제품조회Service(
            new HttpClient(handler) { BaseAddress = new Uri($"{options.BaseUrl}/") },
            Options.Create(options));

        await service.조회Async(new 수입식품제품조회요청DTO
        {
            신고제품구분명 = "가공식품",
            제조국가명 = "미국",
            제품명 = "소스",
            품목명 = "소스류"
        });

        Assert.NotNull(요청Uri);
        var 요청문자열 = Uri.UnescapeDataString(요청Uri!.PathAndQuery);
        Assert.StartsWith("/1471000/IprtFoodPrdtDBService02/getIprtFoodPrdtDBInq02?", 요청문자열);
        Assert.Contains("DCLR_PRDT_DIVS_NM=가공식품", 요청문자열);
        Assert.Contains("MNFT_NATN_NM=미국", 요청문자열);
        Assert.Contains("PRDT_NM=소스", 요청문자열);
        Assert.Contains("PRDLST_NM=소스류", 요청문자열);
        Assert.DoesNotContain("DCLR_PRDT_DIVS_CD", 요청문자열);
        Assert.DoesNotContain("MEAT_PRDLST_CD", 요청문자열);
    }

    [Fact]
    public async Task 조회Async_Json빈항목객체를결과항목으로만들지않는다()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "response": {
                        "header": { "resultCode": "00", "resultMsg": "NORMAL SERVICE" },
                        "body": {
                          "numOfRows": 10,
                          "pageNo": 1,
                          "totalCount": 0,
                          "items": {}
                        }
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        var options = new 수입식품제품조회Options
        {
            ServiceKey = "test-key"
        };
        var service = new 수입식품제품조회Service(
            new HttpClient(handler) { BaseAddress = new Uri($"{options.BaseUrl}/") },
            Options.Create(options));

        var 결과 = await service.조회Async(new 수입식품제품조회요청DTO
        {
            데이터형식 = "JSON"
        });

        Assert.NotNull(결과.본문?.아이템);
        Assert.Empty(결과.본문.아이템.항목);
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
