using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class AgriculturalFisheriesPriceComparisonViewModelTests
{
    [Fact]
    public async Task Initialize_ComposesCountryCatalogsAndOfficialSources()
    {
        var client = new FakePublicDataClient();
        using var viewModel = CreateViewModel(client);

        var initialized = await viewModel.초기화Async();

        Assert.True(initialized);
        Assert.True(viewModel.초기화됨);
        Assert.Null(viewModel.InitializationMessage);
        Assert.Contains(viewModel.국내.품목, item => item.HsPrefix == "080810");
        Assert.Contains(viewModel.국내.품목, item => item.HsPrefix == "1006");
        Assert.Equal("호주 과일", viewModel.호주.Catalog.Indexes.Single().Label);
        Assert.Equal("official-source", viewModel.VisibleSources.Single().Key);
    }

    [Fact]
    public async Task LoadComparison_QueriesAllCountriesWithoutFlatteningTheirUnits()
    {
        var client = new FakePublicDataClient();
        using var viewModel = CreateViewModel(client);
        await viewModel.초기화Async();
        viewModel.국내.선택HsCode = "080810";
        viewModel.미국.품목명 = " apples ";
        viewModel.호주.선택IndexCode = 호주식품가격지수Codes.Fruit;

        await viewModel.LoadComparisonAsync();

        Assert.Equal("080810", client.DomesticHsCode);
        Assert.Equal("APPLES", client.UnitedStatesCommodity);
        Assert.Equal(호주식품가격지수Codes.Fruit, client.AustraliaRequest?.IndexCode);
        Assert.Equal(12_000m, viewModel.국내.응답?.Price?.Retail?.AverageKrwPerKg);
        Assert.Equal("DOLLARS / CWT", viewModel.미국.응답?.Items.Single().Unit);
        Assert.False(viewModel.호주.응답?.IsActualUnitPrice);
        Assert.Equal("INDEX POINTS", viewModel.호주.응답?.Items.Single().UnitLabel);
    }

    [Fact]
    public async Task Initialize_WhenOverviewIsUnavailable_UsesFallbackSourcesWithoutFailingPage()
    {
        var client = new FakePublicDataClient
        {
            FailOverview = true
        };
        using var viewModel = CreateViewModel(client);

        var initialized = await viewModel.초기화Async();

        Assert.True(initialized);
        Assert.True(viewModel.초기화됨);
        Assert.NotNull(viewModel.InitializationMessage);
        Assert.Equal(3, viewModel.VisibleSources.Count);
        Assert.Contains(
            viewModel.VisibleSources,
            source => source.Key == 호주농수산식품가격출처Keys.AbsConsumerPriceIndex);
    }

    [Fact]
    public async Task LoadComparison_WhenOneCountryFails_PreservesOtherCountryResults()
    {
        var client = new FakePublicDataClient
        {
            FailUnitedStatesPrice = true
        };
        using var viewModel = CreateViewModel(client);
        await viewModel.초기화Async();

        await viewModel.LoadComparisonAsync();

        Assert.NotNull(viewModel.국내.응답);
        Assert.Null(viewModel.미국.응답);
        Assert.Equal("미국 가격 API에 연결하지 못했습니다.", viewModel.미국.오류메시지);
        Assert.NotNull(viewModel.호주.응답);
        Assert.False(viewModel.IsLoading);
    }

    private static 농수산가격비교PageViewModel CreateViewModel(
        I농수산공공데이터Client client)
        => new(
            client,
            new 국내농수산가격조회ViewModel(client),
            new 미국농수산가격조회ViewModel(client),
            new 호주농수산가격조회ViewModel(client));

    private sealed class FakePublicDataClient : I농수산공공데이터Client
    {
        public bool FailOverview { get; init; }

        public bool FailUnitedStatesPrice { get; init; }

        public string? DomesticHsCode { get; private set; }

        public string? UnitedStatesCommodity { get; private set; }

        public 호주농수산식품가격조회요청? AustraliaRequest { get; private set; }

        public Task<AgriculturalFisheriesInformationOverviewResponse> 개요조회Async(
            CancellationToken cancellationToken = default)
        {
            if (FailOverview)
            {
                throw new HttpRequestException("Overview unavailable.");
            }

            return Task.FromResult(new AgriculturalFisheriesInformationOverviewResponse
            {
                DataSources =
                [
                    new AgriculturalFisheriesDataSourceResponse
                    {
                        Key = "official-source",
                        Provider = "공식 기관",
                        DisplayName = "공식 가격 자료"
                    }
                ]
            });
        }

        public Task<AgriculturalFisheriesItemSearchResponse> 국내품목조회Async(
            string? query = null,
            string? categoryCode = null,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AgriculturalFisheriesItemSearchResponse
            {
                Items =
                [
                    new AgriculturalFisheriesItemResponse
                    {
                        HsPrefix = "080810",
                        ProductName = "사과",
                        CategoryLabel = "과일류"
                    }
                ]
            });

        public Task<AgriculturalFisheriesDomesticPriceResponse> 국내가격조회Async(
            string hsCode,
            int lookbackDays = 14,
            CancellationToken cancellationToken = default)
        {
            DomesticHsCode = hsCode;
            return Task.FromResult(new AgriculturalFisheriesDomesticPriceResponse
            {
                Success = true,
                HsCode = hsCode,
                Price = new AtDomesticFoodPriceLookupResult
                {
                    Success = true,
                    Retail = new AtDomesticFoodPriceAggregate
                    {
                        PriceTypeLabel = "소매",
                        AverageKrwPerKg = 12_000m,
                        LatestSurveyDate = "2026-07-17"
                    }
                }
            });
        }

        public Task<미국농수산가격조회응답> 미국가격조회Async(
            string commodity,
            string program,
            int yearFrom,
            int yearTo,
            int maxItems = 100,
            CancellationToken cancellationToken = default)
        {
            if (FailUnitedStatesPrice)
            {
                throw new HttpRequestException("USDA unavailable.");
            }

            UnitedStatesCommodity = commodity;
            return Task.FromResult(new 미국농수산가격조회응답
            {
                Success = true,
                Query = new 미국농수산가격조회요청
                {
                    Commodity = commodity,
                    YearFrom = yearFrom,
                    YearTo = yearTo
                },
                Items =
                [
                    new 미국농수산가격항목
                    {
                        Commodity = commodity,
                        RawValue = "54.20",
                        NumericValue = 54.20m,
                        Unit = "DOLLARS / CWT",
                        Year = "2026"
                    }
                ]
            });
        }

        public Task<호주농수산식품가격Catalog응답> 호주가격원천Catalog조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new 호주농수산식품가격Catalog응답
            {
                Indexes =
                [
                    new 호주식품가격지수선택항목
                    {
                        Code = 호주식품가격지수Codes.Fruit,
                        Label = "호주 과일"
                    }
                ],
                Measures =
                [
                    new 호주식품가격지수선택항목
                    {
                        Code = 호주식품가격지수측정Codes.IndexNumber,
                        Label = "가격지수"
                    }
                ],
                Regions =
                [
                    new 호주식품가격지수선택항목
                    {
                        Code = 호주식품가격지수지역Codes.Australia,
                        Label = "호주"
                    }
                ]
            });

        public Task<호주농수산식품가격조회응답> 호주식품가격지수조회Async(
            호주농수산식품가격조회요청 request,
            CancellationToken cancellationToken = default)
        {
            AustraliaRequest = request;
            return Task.FromResult(new 호주농수산식품가격조회응답
            {
                Success = true,
                Query = request,
                IsActualUnitPrice = false,
                Items =
                [
                    new 호주농수산식품가격항목
                    {
                        IndexCode = request.IndexCode,
                        RawValue = "141.7",
                        NumericValue = 141.7m,
                        UnitLabel = "INDEX POINTS",
                        ReferencePeriod = "2026-06"
                    }
                ]
            });
        }

        public Task<FoodPriceComparisonResponse> 식품가격비교Async(
            FoodPriceComparisonRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<HsCountryImportUnitPriceSimulationResult> 수입평균단가조회Async(
            HsCountryMonthlyTradeUnitPriceRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
