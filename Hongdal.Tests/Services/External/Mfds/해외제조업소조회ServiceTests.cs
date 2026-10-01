using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using 홍달.Services.External.Mfds;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.Mfds;

public sealed class 해외제조업소조회ServiceTests
{
    [Fact]
    public async Task 조회Async_JSON단일제조업소와중단주의정보를변환한다()
    {
        Uri? 요청Uri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            요청Uri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "response": {
                        "header": {
                          "resultCode": "00",
                          "resultMsg": "NORMAL SERVICE"
                        },
                        "body": {
                          "numOfRows": 10,
                          "pageNo": 1,
                          "totalCount": 1,
                          "items": {
                            "item": {
                              "OCTR_MNFT_BSSH_CD": "US00001",
                              "OCTR_MNFT_BSSH_NM": "SAMPLE FOODS INC.",
                              "OCTR_MNFT_BSSH_ADDR": "100 SAMPLE ROAD",
                              "FOOD_SE_NM": "가공식품",
                              "NATN_NM": "미국",
                              "FOOD_SAFE_MNG_SYS_CERT_YN": "Y",
                              "CERT_NM": "HACCP",
                              "RTRCN_SUSP_NM": "수입중단",
                              "IPRT_SUSP_NO": "SUSP-1"
                            }
                          }
                        }
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var options = new 해외제조업소조회Options
        {
            ServiceKey = "test-key"
        };
        var service = new 해외제조업소조회Service(
            new HttpClient(handler) { BaseAddress = new Uri($"{options.BaseUrl}/") },
            Options.Create(options));

        var result = await service.조회Async(new 해외제조업소조회요청
        {
            데이터형식 = "json",
            해외제조업소명 = "SAMPLE FOODS INC.",
            식품구분명 = "가공식품",
            국가명 = "미국"
        });

        var item = Assert.Single(result.본문!.아이템!.항목);
        Assert.Equal("US00001", item.해외제조업소코드);
        Assert.Equal("HACCP", item.인증명);
        Assert.True(item.주의필요여부);
        Assert.Contains("수입중단", item.주의사유, StringComparison.Ordinal);
        Assert.Contains("SUSP-1", item.주의사유, StringComparison.Ordinal);

        Assert.NotNull(요청Uri);
        var 요청문자열 = Uri.UnescapeDataString(요청Uri!.PathAndQuery);
        Assert.Contains("OCTR_MNFT_BSSH_NM=SAMPLE FOODS INC.", 요청문자열);
        Assert.Contains("FOOD_SE_NM=가공식품", 요청문자열);
        Assert.Contains("NATN_NM=미국", 요청문자열);
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
