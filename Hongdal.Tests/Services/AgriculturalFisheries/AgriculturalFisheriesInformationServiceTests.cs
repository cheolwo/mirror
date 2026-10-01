using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class AgriculturalFisheriesInformationServiceTests
{
    [Fact]
    public void 개요는_정보제공단계와_주선기능비활성화를_명시한다()
    {
        var service = CreateService(
            new StubDomesticPriceLookupService(),
            new PublicDataOptions
            {
                AtFoodPrices = new AtFoodPricesOptions { ServiceKey = "configured" },
                UsdaNassQuickStats = new UsdaNassQuickStatsOptions { ApiKey = "configured" }
            });

        var result = service.GetOverview();

        Assert.Equal("InformationFoundation", result.StageCode);
        Assert.True(result.IsReadOnly);
        Assert.True(result.AllowsReadinessRecordWrites);
        Assert.False(result.AllowsTransactionExecution);
        Assert.False(result.IsBrokerageEnabled);
        Assert.Contains("US", result.SupportedMarketCodes);
        Assert.Contains("AU", result.SupportedMarketCodes);
        Assert.Contains("주선", result.BrokerageBoundaryNote, StringComparison.Ordinal);
        Assert.Contains(result.DataSources, source =>
            source.Key == "at-daily-wholesale-retail-food-price"
            && source.IsConfigured
            && source.StatusCode == "Ready");
        Assert.Contains(result.DataSources, source =>
            source.Key == 미국농수산가격출처Keys.UsdaNassQuickStats
            && source.IsConfigured
            && source.StatusCode == "Ready");
        Assert.Contains(result.DataSources, source =>
            source.Key == 호주농수산식품가격출처Keys.AbsConsumerPriceIndex
            && source.IsConfigured
            && source.StatusCode == "IntegratedApi");
        Assert.Contains(result.DataSources, source =>
            source.Key == 호주농수산식품가격출처Keys.AbaresFisheriesAquacultureStatistics
            && !source.IsConfigured
            && source.StatusCode == "DownloadAvailable");
        Assert.Contains(result.Capabilities, capability =>
            capability.Code == "MeatImportReadinessCollaboration" && capability.AvailableNow);
        Assert.Contains(result.Capabilities, capability =>
            capability.Code == "UnitedStatesOperatorInformationSources"
            && capability.AvailableNow
            && capability.Endpoint ==
                "GET /api/v1/agricultural-fisheries/us-operator-information-sources");
        Assert.Contains(result.Capabilities, capability =>
            capability.Code == "AustraliaFoodPriceIndexes"
            && capability.AvailableNow
            && capability.Endpoint ==
                "GET /api/v1/agricultural-fisheries/au-food-price-indexes");
        Assert.Contains(result.Capabilities, capability =>
            capability.Code == "FreightBrokerage" && !capability.AvailableNow);
        Assert.NotEmpty(result.BrokerageReadinessRequirements);
    }

    [Fact]
    public void 품목검색은_수산물분류와_품목명을_함께적용한다()
    {
        var service = CreateService(new StubDomesticPriceLookupService());

        var result = service.SearchItems("오징어", "600", 1, 20);

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Equal("600", item.CategoryCode));
        Assert.All(result.Items, item => Assert.Equal("수산물", item.CategoryLabel));
        Assert.All(result.Items, item => Assert.Contains("오징어", item.ProductName, StringComparison.Ordinal));
        Assert.All(result.Items, item => Assert.True(item.InformationOnly));
    }

    [Fact]
    public async Task 국내가격조회는_HS품목을_aT요청으로변환하고_출처주의를제공한다()
    {
        var lookup = new StubDomesticPriceLookupService
        {
            Result = new AtDomesticFoodPriceLookupResult
            {
                Success = true,
                CategoryCode = "600",
                ItemCode = "611",
                ItemName = "고등어",
                StartDate = "20260701",
                EndDate = "20260714",
                Retail = new AtDomesticFoodPriceAggregate
                {
                    PriceTypeCode = "01",
                    PriceTypeLabel = "국내 소매가격",
                    LatestSurveyDate = "20260714",
                    AverageKrwPerKg = 9_000m,
                    MinimumKrwPerKg = 8_500m,
                    MaximumKrwPerKg = 9_500m,
                    SampleCount = 3
                }
            }
        };
        var service = CreateService(lookup);

        var result = await service.GetDomesticPriceAsync(
            new AgriculturalFisheriesDomesticPriceRequest
            {
                HsCode = "0303.54-0000",
                ReferenceDate = "20260714",
                LookbackDays = 14
            });

        Assert.True(result.Success);
        Assert.Equal("Complete", result.StatusCode);
        Assert.Equal("고등어", result.Item!.ProductName);
        Assert.Equal("600", lookup.LastRequest!.CategoryCode);
        Assert.Equal("611", lookup.LastRequest.ItemCode);
        Assert.Equal("20260701", lookup.LastRequest.StartDate);
        Assert.Equal("20260714", lookup.LastRequest.EndDate);
        Assert.Contains("05", lookup.LastRequest.VarietyCodes);
        Assert.Contains(result.Notices, notice => notice.Contains("주문·매입·운송 견적이 아닙니다", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 연결되지않은_HS코드는_외부조회없이_검토필요를알린다()
    {
        var lookup = new StubDomesticPriceLookupService();
        var service = CreateService(lookup);

        var result = await service.GetDomesticPriceAsync(
            new AgriculturalFisheriesDomesticPriceRequest
            {
                HsCode = "2106909099",
                ReferenceDate = "20260714"
            });

        Assert.False(result.Success);
        Assert.Equal("MappingRequired", result.StatusCode);
        Assert.Null(lookup.LastRequest);
        Assert.Contains("연결", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static AgriculturalFisheriesInformationService CreateService(
        StubDomesticPriceLookupService lookup,
        PublicDataOptions? options = null)
        => new(
            new FoodPriceCrosswalkCatalog(),
            lookup,
            Options.Create(options ?? new PublicDataOptions()));

    private sealed class StubDomesticPriceLookupService : IAtDomesticFoodPriceLookupService
    {
        public AtDomesticFoodPriceLookupResult Result { get; init; } = new()
        {
            Success = false,
            ErrorMessage = "not configured"
        };

        public AtDomesticFoodPriceRequest? LastRequest { get; private set; }

        public Task<AtDomesticFoodPriceLookupResult> LookupAsync(
            AtDomesticFoodPriceRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
