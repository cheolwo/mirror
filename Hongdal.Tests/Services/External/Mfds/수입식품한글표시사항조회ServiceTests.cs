using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using 홍달.Services.External.Mfds;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.Mfds;

public sealed class 수입식품한글표시사항조회ServiceTests
{
    [Fact]
    public async Task 조회Async_JSON응답과공식검색필드를변환한다()
    {
        Uri? 요청Uri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            요청Uri = request.RequestUri;
            return 응답생성(
                """
                {
                  "response": {
                    "header": {
                      "resultCode": "00",
                      "resultMsg": "NORMAL SERVICE"
                    },
                    "body": {
                      "numOfRows": 100,
                      "pageNo": 1,
                      "totalCount": 1,
                      "items": {
                        "item": [
                          {
                            "DCL_PRDUCT_SE_CD_NM": "가공식품",
                            "BSN_OFC_NAME": "홍달수입",
                            "PRDUCT_KOREAN_NM": "토마토 소스",
                            "PRDUCT_NM": "TOMATO SAUCE",
                            "EXPIRDE_DTM": "제조일로부터 24개월",
                            "PROCS_DTM": "20260718",
                            "OVSMNFST_NM": "SAMPLE FOODS INC.",
                            "ITM_NM": "소스",
                            "XPORT_NTNCD_NM": "미국",
                            "MNF_NTNCD_NM": "미국",
                            "KORLABEL": "제품명: 토마토 소스",
                            "IRDNT_NM": "토마토, 정제수, 소금",
                            "EXPIRDE_BEGIN_DTM": "20260701",
                            "EXPIRDE_END_DTM": "20280630"
                          }
                        ]
                      }
                    }
                  }
                }
                """,
                "application/json");
        });
        var service = 서비스생성(handler);

        var result = await service.조회Async(new 수입식품한글표시사항조회요청DTO
        {
            페이지번호 = 0,
            한페이지결과수 = 1000,
            데이터형식 = "JSON",
            한글제품명 = "토마토 소스",
            해외제조업소명 = "SAMPLE FOODS INC.",
            제조국명 = "미국",
            원재료명 = "토마토",
            처리일자검색시작 = "20260101",
            처리일자검색종료 = "20261231"
        });

        var item = Assert.Single(result.항목목록);
        Assert.Equal("00", result.결과코드);
        Assert.Equal(1, result.페이지번호);
        Assert.Equal(100, result.한페이지결과수);
        Assert.Equal("토마토 소스", item.한글제품명);
        Assert.Equal("TOMATO SAUCE", item.영문제품명);
        Assert.Equal("토마토, 정제수, 소금", item.원재료명);
        Assert.Equal("SAMPLE FOODS INC.", item.해외제조업소명);

        Assert.NotNull(요청Uri);
        var 요청문자열 = Uri.UnescapeDataString(요청Uri!.PathAndQuery);
        Assert.StartsWith("/1471000/IprtFoodPrdtKoreanLabelingItem/getIprtFoodPrdtKoreanLabelingItem?", 요청문자열);
        Assert.Contains("pageNo=1", 요청문자열);
        Assert.Contains("numOfRows=100", 요청문자열);
        Assert.Contains("type=json", 요청문자열);
        Assert.Contains("prductKoreanNm=토마토 소스", 요청문자열);
        Assert.Contains("ovsmnfstNm=SAMPLE FOODS INC.", 요청문자열);
        Assert.Contains("mnfNtncdNm=미국", 요청문자열);
        Assert.Contains("irdntNm=토마토", 요청문자열);
        Assert.Contains("procsDtmStart=20260101", 요청문자열);
        Assert.Contains("procsDtmEnd=20261231", 요청문자열);
    }

    [Fact]
    public async Task 조회Async_XML단일항목응답을변환한다()
    {
        var handler = new StubHttpMessageHandler(_ => 응답생성(
            """
            <response>
              <header>
                <resultCode>00</resultCode>
                <resultMsg>NORMAL SERVICE</resultMsg>
              </header>
              <body>
                <numOfRows>10</numOfRows>
                <pageNo>1</pageNo>
                <totalCount>1</totalCount>
                <items>
                  <item>
                    <PRDUCT_KOREAN_NM>올리브 오일</PRDUCT_KOREAN_NM>
                    <PRDUCT_NM>OLIVE OIL</PRDUCT_NM>
                    <OVSMNFST_NM>OLIVE FARM</OVSMNFST_NM>
                    <MNF_NTNCD_NM>이탈리아</MNF_NTNCD_NM>
                    <IRDNT_NM>올리브유 100%</IRDNT_NM>
                  </item>
                </items>
              </body>
            </response>
            """,
            "application/xml"));
        var service = 서비스생성(handler);

        var result = await service.조회Async(new 수입식품한글표시사항조회요청DTO());

        var item = Assert.Single(result.항목목록);
        Assert.Equal("올리브 오일", item.한글제품명);
        Assert.Equal("OLIVE FARM", item.해외제조업소명);
        Assert.Equal("이탈리아", item.제조국명);
        Assert.Equal("올리브유 100%", item.원재료명);
    }

    private static 수입식품한글표시사항조회Service 서비스생성(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://apis.data.go.kr/1471000/IprtFoodPrdtKoreanLabelingItem/")
        };

        return new 수입식품한글표시사항조회Service(
            httpClient,
            Options.Create(new 수입식품한글표시사항조회Options
            {
                ServiceKey = "test-key"
            }));
    }

    private static HttpResponseMessage 응답생성(string content, string mediaType)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType)
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
