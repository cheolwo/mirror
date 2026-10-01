using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.AgriculturalFisheries.Information;
using Hongdal.Services.Content;

namespace Hongdal.Tests.Services.Content;

public sealed class CommunityAbsFoodPriceIndexInformationCandidateSourceTests
{
    [Fact]
    public async Task ReadAsync_UsesCalendarRangeAndSearchToSelectOfficialFoodIndex()
    {
        var service = new StubAustralianPriceService();
        var source = new CommunityAbsFoodPriceIndexInformationCandidateSource(service);

        var items = await source.ReadAsync(new CommunityInformationCollectionQuery
        {
            CountryCode = "AU",
            SearchText = "쇠고기",
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2026, 7, 10),
            Take = 20
        });

        var request = Assert.IsType<호주농수산식품가격조회요청>(service.LastRequest);
        Assert.Equal(호주식품가격지수Codes.BeefAndVeal, request.IndexCode);
        Assert.Equal("2026-06", request.StartPeriod);
        Assert.Equal("2026-07", request.EndPeriod);
        var item = Assert.Single(items);
        Assert.Equal(CommunityInformationSourceKeys.AbsFoodPriceIndex, item.SourceKey);
        Assert.Equal("AU", item.CountryCode);
        Assert.Equal(new DateOnly(2026, 7, 1), item.ReferenceDate);
        Assert.Equal(new DateOnly(2026, 7, 31), item.ReferencePeriodEndDate);
        Assert.Equal(123.4m, item.NumericValue);
        Assert.Equal("Index points", item.Unit);
        Assert.Contains("쇠고기", item.MetricSeriesLabel, StringComparison.Ordinal);
        Assert.Contains("실제 A$/kg 가격이 아니라", item.Limitations, StringComparison.Ordinal);
    }

    private sealed class StubAustralianPriceService : I호주농수산식품가격조회Service
    {
        public 호주농수산식품가격조회요청? LastRequest { get; private set; }

        public 호주농수산식품가격Catalog응답 GetCatalog()
            => new()
            {
                Indexes =
                [
                    new 호주식품가격지수선택항목
                    {
                        Code = 호주식품가격지수Codes.FoodAndNonAlcoholicBeverages,
                        Label = "식품 및 비알코올 음료",
                        OfficialLabel = "Food and non-alcoholic beverages"
                    },
                    new 호주식품가격지수선택항목
                    {
                        Code = 호주식품가격지수Codes.BeefAndVeal,
                        Label = "쇠고기·송아지고기",
                        OfficialLabel = "Beef and veal"
                    }
                ]
            };

        public Task<호주농수산식품가격조회응답> 조회Async(
            호주농수산식품가격조회요청 request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new 호주농수산식품가격조회응답
            {
                Success = true,
                StatusCode = 호주농수산식품가격조회상태Codes.완료,
                CollectedAtUtc = new DateTime(2026, 7, 19, 1, 0, 0, DateTimeKind.Utc),
                Items =
                [
                    new 호주농수산식품가격항목
                    {
                        IndexCode = request.IndexCode,
                        IndexLabel = "쇠고기·송아지고기",
                        OfficialIndexLabel = "Beef and veal",
                        MeasureCode = request.MeasureCode,
                        MeasureLabel = "가격지수",
                        RegionCode = request.RegionCode,
                        RegionLabel = "호주 8개 주도시 가중평균",
                        ReferencePeriod = "2026-07",
                        NumericValue = 123.4m,
                        UnitCode = "INDEX",
                        UnitLabel = "Index points",
                        BasePeriod = "2011-12 = 100.0"
                    }
                ]
            });
        }
    }
}
