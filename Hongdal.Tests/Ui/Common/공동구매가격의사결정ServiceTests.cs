using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Tests.Ui.Common;

public sealed class 공동구매가격의사결정ServiceTests
{
    [Fact]
    public async Task 국내공동구매_제안단가를국내도소매평균과비교한다()
    {
        var client = new Fake농수산공공데이터Client
        {
            국내가격응답 = new AgriculturalFisheriesDomesticPriceResponse
            {
                Success = true,
                HsCode = "0701",
                Price = DomesticPrice(retail: 12_000m, wholesale: 8_000m)
            }
        };
        var service = new 공동구매가격의사결정Service(client);

        var result = await service.조회Async(new 공동구매가격의사결정요청(
            공동구매가격의사결정유형코드.국내공동구매,
            "0701.90",
            9_000m));

        Assert.True(result.자료있음);
        Assert.Equal("Complete", result.상태코드);
        Assert.Equal(1, client.국내가격호출수);
        Assert.Equal(0, client.식품가격비교호출수);
        var retail = Assert.Single(result.기준비교목록, item => item.기준코드 == "domestic-retail");
        Assert.Equal(3_000m, retail.차이KrwPerKg);
        Assert.Equal(0.25m, retail.차이율);
        Assert.Equal(공동구매가격판단신호코드.제안가격경쟁력, retail.신호코드);
        var wholesale = Assert.Single(result.기준비교목록, item => item.기준코드 == "domestic-wholesale");
        Assert.Equal(공동구매가격판단신호코드.제안가격주의, wholesale.신호코드);
    }

    [Fact]
    public async Task 공동수입_국내시장수입평균도착원가와해외공공가격을함께제공한다()
    {
        var client = new Fake농수산공공데이터Client
        {
            식품가격비교응답 = new FoodPriceComparisonResponse
            {
                Success = true,
                StatusCode = "Complete",
                HsCode = "080810",
                CountryCode = "US",
                DomesticPrice = DomesticPrice(retail: 15_000m, wholesale: 11_000m),
                ImportPrice = new FoodImportPriceReference
                {
                    AverageCifKrwPerKg = 7_000m,
                    EstimatedLandedCostKrwPerKg = 9_000m
                },
                Summary = "사과 수입 기준가격과 국내 가격을 비교했습니다."
            },
            미국가격응답 = new 미국농수산가격조회응답
            {
                Success = true,
                StatusCode = 미국농수산가격조회상태Codes.완료,
                Items =
                [
                    new 미국농수산가격항목
                    {
                        Commodity = "APPLES",
                        Unit = "$ / CWT",
                        NumericValue = 80m,
                        Year = "2026"
                    }
                ]
            }
        };
        var service = new 공동구매가격의사결정Service(client);

        var result = await service.조회Async(new 공동구매가격의사결정요청(
            공동구매가격의사결정유형코드.공동수입,
            "080810",
            12_000m,
            수출국가코드: "us",
            추가수입비용KrwPerKg: 2_000m,
            해외공공가격품목명: "APPLES"));

        Assert.True(result.자료있음);
        Assert.Equal("Complete", result.상태코드);
        Assert.Equal(0, client.국내가격호출수);
        Assert.Equal(1, client.식품가격비교호출수);
        Assert.Equal(0, client.수입평균단가호출수);
        Assert.Equal(1, client.미국가격호출수);
        Assert.Equal("US", client.마지막식품가격비교요청?.CountryCode);
        Assert.Single(result.해외공공가격!.Items);
        var landed = Assert.Single(
            result.기준비교목록,
            item => item.기준코드 == "import-estimated-landed-cost");
        Assert.Equal(3_000m, landed.차이KrwPerKg);
        Assert.Equal(0.25m, landed.차이율);
        Assert.Equal(공동구매가격판단신호코드.원가여유참고, landed.신호코드);
        Assert.Contains(result.주의사항, notice => notice.Contains("자동 환산 비교하지 않습니다."));
    }

    [Fact]
    public async Task 공동수입_국내품목연결이없어도_HS수입평균단가를별도로조회한다()
    {
        var client = new Fake농수산공공데이터Client
        {
            식품가격비교응답 = new FoodPriceComparisonResponse
            {
                Success = false,
                StatusCode = "MappingRequired",
                HsCode = "121190",
                CountryCode = "CN"
            },
            수입평균단가응답 = new HsCountryImportUnitPriceSimulationResult
            {
                Success = true,
                HsCode = "121190",
                CountryCode = "CN",
                AverageImportUnitValueKrwPerKg = 8_000m,
                ExpectedLandedCostKrwPerKg = 9_000m
            }
        };
        var service = new 공동구매가격의사결정Service(client);

        var result = await service.조회Async(new 공동구매가격의사결정요청(
            공동구매가격의사결정유형코드.공동수입,
            "121190",
            11_000m,
            수출국가코드: "CN"));

        Assert.True(result.자료있음);
        Assert.Equal(1, client.수입평균단가호출수);
        Assert.Equal("121190", client.마지막수입평균단가요청?.HsCode);
        Assert.Equal(6, client.마지막수입평균단가요청?.Month.Length);
        Assert.Equal(11_000m, client.마지막수입평균단가요청?.ExpectedSellingUnitPriceKrwPerKg);
        Assert.Contains(result.기준비교목록, item => item.기준코드 == "import-average-cif");
        Assert.Contains(result.기준비교목록, item => item.기준코드 == "import-estimated-landed-cost");
    }

    [Fact]
    public async Task 일부공공데이터장애_예외대신조회불가결과를제공한다()
    {
        var client = new Fake농수산공공데이터Client
        {
            국내가격예외 = new HttpRequestException("public data unavailable")
        };
        var service = new 공동구매가격의사결정Service(client);

        var result = await service.조회Async(new 공동구매가격의사결정요청(
            공동구매가격의사결정유형코드.국내공동구매,
            "070190",
            9_000m));

        Assert.False(result.자료있음);
        Assert.Equal("Unavailable", result.상태코드);
        Assert.Contains("비교 가능한 가격 자료", result.요약);
    }

    private static AtDomesticFoodPriceLookupResult DomesticPrice(
        decimal retail,
        decimal wholesale)
        => new()
        {
            Success = true,
            ItemName = "테스트 품목",
            Retail = new AtDomesticFoodPriceAggregate
            {
                PriceTypeCode = "Retail",
                AverageKrwPerKg = retail
            },
            Wholesale = new AtDomesticFoodPriceAggregate
            {
                PriceTypeCode = "Wholesale",
                AverageKrwPerKg = wholesale
            }
        };

    private sealed class Fake농수산공공데이터Client : I농수산공공데이터Client
    {
        public AgriculturalFisheriesDomesticPriceResponse 국내가격응답 { get; set; } = new();
        public FoodPriceComparisonResponse 식품가격비교응답 { get; set; } = new();
        public 미국농수산가격조회응답 미국가격응답 { get; set; } = new();
        public HsCountryImportUnitPriceSimulationResult 수입평균단가응답 { get; set; } = new();
        public Exception? 국내가격예외 { get; set; }
        public int 국내가격호출수 { get; private set; }
        public int 식품가격비교호출수 { get; private set; }
        public int 미국가격호출수 { get; private set; }
        public int 수입평균단가호출수 { get; private set; }
        public FoodPriceComparisonRequest? 마지막식품가격비교요청 { get; private set; }
        public HsCountryMonthlyTradeUnitPriceRequest? 마지막수입평균단가요청 { get; private set; }

        public Task<AgriculturalFisheriesInformationOverviewResponse> 개요조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AgriculturalFisheriesInformationOverviewResponse());

        public Task<AgriculturalFisheriesItemSearchResponse> 국내품목조회Async(
            string? query = null,
            string? categoryCode = null,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AgriculturalFisheriesItemSearchResponse());

        public Task<AgriculturalFisheriesDomesticPriceResponse> 국내가격조회Async(
            string hsCode,
            int lookbackDays = 14,
            CancellationToken cancellationToken = default)
        {
            국내가격호출수++;
            return 국내가격예외 is null
                ? Task.FromResult(국내가격응답)
                : Task.FromException<AgriculturalFisheriesDomesticPriceResponse>(국내가격예외);
        }

        public Task<미국농수산가격조회응답> 미국가격조회Async(
            string commodity,
            string program,
            int yearFrom,
            int yearTo,
            int maxItems = 100,
            CancellationToken cancellationToken = default)
        {
            미국가격호출수++;
            return Task.FromResult(미국가격응답);
        }

        public Task<호주농수산식품가격Catalog응답> 호주가격원천Catalog조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new 호주농수산식품가격Catalog응답());

        public Task<호주농수산식품가격조회응답> 호주식품가격지수조회Async(
            호주농수산식품가격조회요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new 호주농수산식품가격조회응답());

        public Task<FoodPriceComparisonResponse> 식품가격비교Async(
            FoodPriceComparisonRequest request,
            CancellationToken cancellationToken = default)
        {
            식품가격비교호출수++;
            마지막식품가격비교요청 = request;
            return Task.FromResult(식품가격비교응답);
        }

        public Task<HsCountryImportUnitPriceSimulationResult> 수입평균단가조회Async(
            HsCountryMonthlyTradeUnitPriceRequest request,
            CancellationToken cancellationToken = default)
        {
            수입평균단가호출수++;
            마지막수입평균단가요청 = request;
            return Task.FromResult(수입평균단가응답);
        }
    }
}
