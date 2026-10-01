using System.Net;
using System.Text;
using Hongdal.Contracts.Common.Customs;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.PublicData;

public sealed class 세관장확인대상물품공공데이터수집기Tests
{
    [Fact]
    public async Task 수집_10자리HSK의법령승인기관과구비요건을반환한다()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return XmlResponse(
                """
                <response>
                  <header>
                    <resultCode>00</resultCode>
                    <resultMsg>정상</resultMsg>
                  </header>
                  <body>
                    <items>
                      <item>
                        <hsSgn>3307903000</hsSgn>
                        <dcerCfrmLworCd>11</dcerCfrmLworCd>
                        <dcerCfrmLworNm>화장품법</dcerCfrmLworNm>
                        <reqApreIttCd>1471000</reqApreIttCd>
                        <reqApreIttNm>식품의약품안전처</reqApreIttNm>
                        <reqCfrmIstmNm>표준통관예정보고서</reqCfrmIstmNm>
                        <aplyStrtDt>20260101</aplyStrtDt>
                        <bfhnAffcRtmTpcd>1</bfhnAffcRtmTpcd>
                      </item>
                    </items>
                  </body>
                </response>
                """);
        });
        var service = new 세관장확인대상물품공공데이터수집기(
            new HttpClient(handler) { BaseAddress = new Uri("https://apis.data.go.kr/") },
            Options.Create(new PublicDataOptions
            {
                CustomsRequirements = new CustomsRequirementsOptions
                {
                    ServiceKey = "test-key"
                }
            }));

        var result = await service.수집Async(new Hs공공데이터수집요청
        {
            HsCode = "3307903000"
        });

        Assert.Equal(Hs공공데이터수집상태Codes.성공, result.StatusCode);
        var item = Assert.Single(result.Items);
        Assert.True(item.AttentionRequired);
        Assert.Equal("화장품법 · 식품의약품안전처", item.Title);
        Assert.Equal("표준통관예정보고서", item.Fields["requiredConfirmationDocument"]);
        Assert.NotNull(requestedUri);
        Assert.Contains("hsSgn=3307903000", requestedUri!.Query, StringComparison.Ordinal);
        Assert.Contains("imexTpcd=2", requestedUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 수집_6자리HS코드는외부호출없이10자리코드를안내한다()
    {
        var callCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            return XmlResponse("<response />");
        });
        var service = new 세관장확인대상물품공공데이터수집기(
            new HttpClient(handler) { BaseAddress = new Uri("https://apis.data.go.kr/") },
            Options.Create(new PublicDataOptions()));

        var result = await service.수집Async(new Hs공공데이터수집요청
        {
            HsCode = "090121"
        });

        Assert.Equal(Hs공공데이터수집상태Codes.적용안됨, result.StatusCode);
        Assert.Contains("10자리", result.Summary, StringComparison.Ordinal);
        Assert.Equal(0, callCount);
    }

    private static HttpResponseMessage XmlResponse(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/xml")
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
