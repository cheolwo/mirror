using System.Net;
using System.Text;
using Hongdal.Contracts.Common.Customs;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.PublicData;

public sealed class 관세환율공공데이터수집기Tests
{
    [Fact]
    public async Task 수집_요청국가의수입관세환율만반환한다()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <response>
                      <header><resultCode>00</resultCode><resultMsg>정상</resultMsg></header>
                      <body>
                        <items>
                          <item>
                            <cntySgn>CN</cntySgn>
                            <mtryUtNm>Chinese Yuan</mtryUtNm>
                            <fxrt>190.42</fxrt>
                            <currSgn>CNY</currSgn>
                            <aplyBgnDt>20260712</aplyBgnDt>
                            <imexTp>2</imexTp>
                          </item>
                          <item>
                            <cntySgn>US</cntySgn>
                            <mtryUtNm>US Dollar</mtryUtNm>
                            <fxrt>1375.50</fxrt>
                            <currSgn>USD</currSgn>
                            <aplyBgnDt>20260712</aplyBgnDt>
                            <imexTp>2</imexTp>
                          </item>
                        </items>
                      </body>
                    </response>
                    """,
                    Encoding.UTF8,
                    "application/xml")
            };
        });
        var service = new 관세환율공공데이터수집기(
            new HttpClient(handler) { BaseAddress = new Uri("https://apis.data.go.kr/") },
            Options.Create(new PublicDataOptions
            {
                CustomsExchangeRate = new CustomsExchangeRateOptions
                {
                    ServiceKey = "test-key"
                }
            }));

        var result = await service.수집Async(new Hs공공데이터수집요청
        {
            HsCode = "0901210000",
            CountryCode = "CN",
            ReferenceDate = "20260715"
        });

        Assert.Equal(Hs공공데이터수집상태Codes.성공, result.StatusCode);
        var item = Assert.Single(result.Items);
        Assert.Equal("CNY", item.Fields["currencyCode"]);
        Assert.Equal("190.42", item.Fields["exchangeRate"]);
        Assert.NotNull(requestedUri);
        Assert.Contains("aplyBgnDt=20260715", requestedUri!.Query, StringComparison.Ordinal);
        Assert.Contains("weekFxrtTpcd=2", requestedUri.Query, StringComparison.Ordinal);
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
