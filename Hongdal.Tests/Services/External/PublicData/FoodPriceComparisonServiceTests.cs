using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Services.AgriculturalFisheries.Information;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.External.PublicData;

public sealed class FoodPriceComparisonServiceTests
{
    [Fact]
    public void 품목연결표_국산품종과대표품목을구분한다()
    {
        var catalog = new FoodPriceCrosswalkCatalog();

        var garlic = catalog.Find("0703.20-1000");
        var beef = catalog.Find("0202.30-0000");

        Assert.NotNull(garlic);
        Assert.Equal("258", garlic!.AtItemCode);
        Assert.Equal("ExactCommodity", garlic.MatchQualityCode);
        Assert.Equal("DomesticVariant", garlic.DomesticOriginStatusCode);
        Assert.NotNull(beef);
        Assert.Equal("512", beef!.AtItemCode);
        Assert.Equal("Representative", beef.MatchQualityCode);
        Assert.Null(catalog.Find("2106.90-9099"));
    }

    [Fact]
    public async Task 가격비교_국내소매가와추정도착원가의차이를쉬운문장으로제공한다()
    {
        var domestic = new StubDomesticPriceLookupService
        {
            Result = new AtDomesticFoodPriceLookupResult
            {
                Success = true,
                CategoryCode = "200",
                ItemCode = "258",
                ItemName = "깐마늘(국산)",
                Retail = Aggregate("01", "국내 소매가격", 12_000m),
                Wholesale = Aggregate("02", "국내 중도매가격", 10_000m)
            }
        };
        var import = new StubImportPriceLookupService
        {
            Result = new HsCountryImportUnitPriceSimulationResult
            {
                Success = true,
                HsCode = "0703201000",
                CountryCode = "CN",
                StartMonth = "202604",
                EndMonth = "202606",
                TotalImportWeightKg = 1_000m,
                AverageImportUnitValueUsdPerKg = 6m,
                AverageImportUnitValueKrwPerKg = 6_000m,
                ExpectedLandedCostKrwPerKg = 7_000m
            }
        };
        var service = CreateService(domestic, import);

        var result = await service.CompareAsync(new FoodPriceComparisonRequest
        {
            HsCode = "0703.20-1000",
            CountryCode = "cn",
            ReferenceDate = "20260714",
            ReferenceMonth = "202606",
            FxRateKrwPerUsd = 1_000m,
            EstimatedImportAdditionalCostKrwPerKg = 1_000m
        });

        Assert.True(result.Success);
        Assert.Equal("Complete", result.StatusCode);
        Assert.Equal("마늘", result.ProductName);
        Assert.Equal("CN", result.CountryCode);
        Assert.Equal("DomesticVariant", result.Match!.DomesticOriginStatusCode);
        Assert.Equal(2, result.Comparisons.Count);
        Assert.NotNull(result.PrimaryComparison);
        Assert.Equal("Retail", result.PrimaryComparison!.BasisCode);
        Assert.Equal(12_000m, result.PrimaryComparison.DomesticPriceKrwPerKg);
        Assert.Equal(7_000m, result.PrimaryComparison.ImportReferencePriceKrwPerKg);
        Assert.Equal(5_000m, result.PrimaryComparison.DifferenceKrwPerKg);
        Assert.Equal(0.4167m, result.PrimaryComparison.DifferenceRate);
        Assert.Equal("ImportReferenceLower", result.PrimaryComparison.SignalCode);
        Assert.Contains("약 42% 낮습니다", result.Summary, StringComparison.Ordinal);

        Assert.NotNull(domestic.LastRequest);
        Assert.Equal("258", domestic.LastRequest!.ItemCode);
        Assert.Contains("01", domestic.LastRequest.VarietyCodes);
        Assert.NotNull(import.LastRequest);
        Assert.Equal(3, import.LastRequest!.LookbackMonths);
        Assert.Equal(1_000m, import.LastRequest.ExpectedDomesticLogisticsCostKrwPerKg);
    }

    [Fact]
    public async Task 가격비교_연결되지않은가공식품은외부호출없이안내한다()
    {
        var domestic = new StubDomesticPriceLookupService();
        var import = new StubImportPriceLookupService();
        var service = CreateService(domestic, import);

        var result = await service.CompareAsync(new FoodPriceComparisonRequest
        {
            HsCode = "2106909099",
            CountryCode = "US",
            ReferenceDate = "20260714"
        });

        Assert.False(result.Success);
        Assert.Equal("MappingRequired", result.StatusCode);
        Assert.Null(domestic.LastRequest);
        Assert.Null(import.LastRequest);
        Assert.Contains("연결되지 않은", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static FoodPriceComparisonService CreateService(
        StubDomesticPriceLookupService domestic,
        StubImportPriceLookupService import)
    {
        var options = Options.Create(new PublicDataOptions
        {
            AtFoodPrices = new AtFoodPricesOptions
            {
                DefaultSimulationFxRateKrwPerUsd = 1_350m
            }
        });
        var informationService = new AgriculturalFisheriesInformationService(
            new FoodPriceCrosswalkCatalog(),
            domestic,
            options);

        return new FoodPriceComparisonService(informationService, import, options);
    }

    private static AtDomesticFoodPriceAggregate Aggregate(string code, string label, decimal price)
        => new()
        {
            PriceTypeCode = code,
            PriceTypeLabel = label,
            LatestSurveyDate = "20260714",
            AverageKrwPerKg = price,
            MinimumKrwPerKg = price,
            MaximumKrwPerKg = price,
            SampleCount = 1
        };

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

    private sealed class StubImportPriceLookupService : IHsCountryTradeUnitPriceLookupService
    {
        public HsCountryImportUnitPriceSimulationResult Result { get; init; } = new()
        {
            Success = false,
            ErrorMessage = "not configured"
        };

        public HsCountryMonthlyTradeUnitPriceRequest? LastRequest { get; private set; }

        public Task<HsCountryImportUnitPriceSimulationResult> SimulateImportUnitPriceAsync(
            HsCountryMonthlyTradeUnitPriceRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
