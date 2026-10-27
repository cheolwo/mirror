using System.Text.Json;
using Ssalddel.Contracts.Common.AgriculturalFisheries;
using Ssalddel.Services.AgriculturalFisheries.Information;
using 살뜰.Services.External.PublicData;
using static Ssalddel.Services.AgriculturalFisheries.Information.Kamis중심UsdaAms가격Projector;

namespace Ssalddel.Tests.Services.AgriculturalFisheries;

public sealed class Kamis중심UsdaAms가격ProjectorTests
{
    private static readonly DateOnly Date = new(2026, 7, 29);

    [Fact]
    public void 원자료Point의모든필드는_통화포장단위와결측표시를변환없이보존한다()
    {
        var kamis = Assert.Single(BuildKamisPoints([Kamis(1, 1234m, true)], 1));
        var expectedKamis = new Kamis중심가격Point응답(
            "Daily", "02", "도매", Date, "01", "kind", "01", "rank", "원 단위", "10개", "1kg",
            "SourceKg", "source basis", 1234m, true);
        Assert.Equal(JsonSerializer.Serialize(expectedKamis), JsonSerializer.Serialize(kamis));

        var ams = Assert.Single(BuildAmsStages(["Apples"], [Ams("a", "var", 10m)], 1, default)
            .Single(stage => stage.MarketStageCode == 농수산시세시장단계Codes.도매터미널).PricePoints);
        var expectedAms = new Kamis중심UsdaAms가격Point응답(
            "a", "source", 도매단계, Date, "Apples", "var", "grade", "raw package", "large", "Y", "US",
            "New York", "NY", null, null, 1m, 99m, 10m, 7, "USD", "package: cartons");
        Assert.Equal(JsonSerializer.Serialize(expectedAms), JsonSerializer.Serialize(ams));
    }

    [Fact]
    public void 가격대는_KRW의missing과null을제외하고_0과원포장비교단위를구분한다()
    {
        var points = BuildKamisPoints([
            Kamis(1, 100m), Kamis(2, 999m, true), Kamis(3, null), Kamis(4, 0m),
            new() { Id = 5, ProductClassCode = "02", Unit = "box", PriceKrw = 5m, SourcePackageLabel = "box" }
        ], 50);
        var bands = BuildDistributionStagePriceBands(points, []);
        var kilogram = Assert.Single(bands, band => band.OriginalUnit == "1kg");
        Assert.Equal("KRW", kilogram.CurrencyCode);
        Assert.Equal(0m, kilogram.LowObservedPrice);
        Assert.Equal(100m, kilogram.HighObservedPrice);
        Assert.Equal(4, kilogram.ObservationCount);
        Assert.Equal(new[] { "10개" }, kilogram.SourcePackageLabels);
        Assert.Equal("SourceKg", kilogram.PriceNormalizationCode);
        Assert.Equal("source basis", kilogram.PriceNormalizationBasis);
        Assert.Equal(5m, Assert.Single(bands, band => band.OriginalUnit == "box").LowObservedPrice);
        var emptyRetail = Assert.Single(bands, band => band.SourceMarketStageCode == "01");
        Assert.Equal("", emptyRetail.CurrencyCode);
        Assert.Null(emptyRetail.LowObservedPrice);
        Assert.All(bands, band => Assert.False(band.AllowsDirectComparison));
    }

    [Fact]
    public void AMS가격대는_원통화단위원천별로분리하고_가중평균만LowHigh대체값으로쓴다()
    {
        var stages = BuildAmsStages(["Apples"], [
            Ams("a", "one", 10m),
            Ams("b", "two", null, low: 5m, high: 15m),
            Ams("c", "three", 2m, source: "other-source"),
            Ams("d", "four", 3m, unit: "lb"),
            Ams("e", "five", 4m, currency: "")
        ], 20, default);
        var bands = BuildDistributionStagePriceBands([], stages);
        var original = Assert.Single(bands, band => band.SourceKey == "source"
            && band.CurrencyCode == "USD" && band.OriginalUnit == "package: cartons");
        Assert.Equal(5m, original.LowObservedPrice);
        Assert.Equal(15m, original.HighObservedPrice);
        Assert.Equal(2, original.ObservationCount);
        Assert.Equal(2m, Assert.Single(bands, band => band.SourceKey == "other-source").LowObservedPrice);
        Assert.Equal(3m, Assert.Single(bands, band => band.OriginalUnit == "lb").HighObservedPrice);
        Assert.Equal(4m, Assert.Single(bands, band => band.SourceKey == "source" && band.CurrencyCode == "").HighObservedPrice);
        Assert.All(bands, band => Assert.False(band.AllowsDirectComparison));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void KAMIS동률은_Id역순과기존Take를보존한다(int take)
    {
        var observations = Enumerable.Range(1, 51).Select(id => Kamis(id, id)).ToArray();
        var points = BuildKamisPoints(observations, take);
        Assert.Equal(take, points.Count);
        Assert.Equal(Enumerable.Range(0, take).Select(index => (decimal?)(51 - index)), points.Select(point => point.PriceKrw));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void AMS시장단계의Take와동률RecordKey선택은_기존identity를유지한다(int take)
    {
        var observations = Enumerable.Range(0, 22)
            .Select(index => Ams($"r{index:00}", $"v{index:00}", index)).ToList();
        observations.Add(Ams("z", "v00", 999m, source: "excluded-source-difference"));
        var points = BuildAmsStages(["apples"], observations, take, default)
            .Single(stage => stage.MarketStageCode == 도매단계).PricePoints;
        Assert.Equal(take, points.Count);
        Assert.Equal(Enumerable.Range(0, take).Select(index => $"r{index:00}"), points.Select(point => point.RecordKey));
        Assert.Equal(0m, points[0].WeightedAveragePrice);
    }

    [Fact]
    public void AMSidentity는_원래빠진필드를추가하지않고_시장단계옵션을유지한다()
    {
        var first = Ams("a", "var", 1m);
        var different = new AmsLatestObservation
        {
            RecordKey = "z", SourceKey = "different", MarketStageCode = 도매단계, ReportBeginDate = Date.AddDays(1),
            Commodity = "Apples", OriginalUnit = "package: cartons", Variety = "var", Grade = "grade", Organic = "Y",
            Origin = "US", MarketLocationName = "New York", ItemSize = "small", MarketLocationState = "NJ", LowPrice = 999m
        };
        Assert.Equal(BuildAmsPointIdentity(first, true), BuildAmsPointIdentity(different, true));
        Assert.StartsWith(도매단계 + "\u001f", BuildAmsPointIdentity(first, true));
        Assert.StartsWith("\u001f", BuildAmsPointIdentity(first, false));
    }

    [Fact]
    public void 빈매핑도_세시장단계와다섯빈가격대를유지하고_국가세번을확정하지않는다()
    {
        var anchor = new PreparedAnchor(new("100", "곡물", "111", "쌀", Date),
            Kamis중심UsdaAms품목MappingCatalog.Resolve("111", []));
        var connection = BuildProductCodeConnection(anchor, []);
        Assert.Equal("kamis:100:111", connection.InternalProductKey);
        Assert.Empty(connection.HsClassificationCandidates);
        Assert.Empty(connection.UsdaAmsCommodityCandidates);
        Assert.Equal(new[] { "HSK10", "HTSUS10" }, connection.NationalTariffReviews.Select(review => review.CodeScheme));
        Assert.All(connection.NationalTariffReviews, review =>
        {
            Assert.Null(review.Code);
            Assert.Equal(Kamis중심상품코드연결상태Codes.전문가검토필요, review.RelationStatusCode);
        });
        var stages = BuildAmsStages([], [], 5, default);
        Assert.Equal(new[] { 농수산시세시장단계Codes.산지출하, 도매단계, 농수산시세시장단계Codes.소매광고 }, stages.Select(stage => stage.MarketStageCode));
        Assert.All(stages, stage => { Assert.Empty(stage.PricePoints); Assert.Null(stage.LatestReferenceDate); });
        var bands = BuildDistributionStagePriceBands([], stages);
        Assert.Equal(5, bands.Count);
        Assert.All(bands, band =>
        {
            Assert.Equal(농수산유통가격대상태Codes.관측값없음, band.DataStatusCode);
            Assert.Equal("", band.CurrencyCode);
            Assert.Null(band.LowObservedPrice);
            Assert.Null(band.HighObservedPrice);
            Assert.False(band.AllowsDirectComparison);
        });
    }

    [Fact]
    public void HS후보는_현재문자열순서와접두길이를유지하고_다른품목을연결하지않는다()
    {
        var anchor = new PreparedAnchor(new("400", "과일", "411", "사과", Date),
            Kamis중심UsdaAms품목MappingCatalog.Resolve("411", ["Apples"]));
        var connection = BuildProductCodeConnection(anchor, [Crosswalk("080810", "411"), Crosswalk("0808", "411"), Crosswalk("000000", "other")]);
        Assert.Equal(new[] { "0808", "080810" }, connection.HsClassificationCandidates.Select(item => item.Code));
        Assert.Equal(new[] { "HS4", "HS6" }, connection.HsClassificationCandidates.Select(item => item.CodeScheme));
        Assert.All(connection.HsClassificationCandidates, item => Assert.Equal(Kamis중심상품코드연결상태Codes.후보, item.RelationStatusCode));
    }

    [Theory]
    [InlineData(" CAT ", " 411 ", true, " APPLES ", true)]
    [InlineData("cat", "411", false, "", false)]
    [InlineData("CAT", "412", false, "", false)]
    [InlineData("", "", false, " 사과 ", true)]
    [InlineData("", "", false, " ", true)]
    [InlineData("", "", false, "missing", false)]
    public void 검색은_명시코드Ordinal과텍스트Trim대소문자무시를구분한다(string category, string item, bool mapped, string text, bool expected)
    {
        var anchor = new PreparedAnchor(new("CAT", "종류", "411", "사과", Date),
            Kamis중심UsdaAms품목MappingCatalog.Resolve("411", ["Apples"]));
        Assert.Equal(expected, MatchesQuery(anchor, new() { CategoryCode = category, ItemCode = item, OnlyMapped = mapped, Query = text }));
        var unmapped = new PreparedAnchor(anchor.Anchor, Kamis중심UsdaAms품목MappingCatalog.Resolve("111", []));
        Assert.False(MatchesQuery(unmapped, new() { OnlyMapped = true }));
    }

    [Fact]
    public void 이미취소된호출은_빈매핑이어도_단계조립전에취소를검사한다()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = Assert.Throws<OperationCanceledException>(() => BuildAmsStages([], [], 5, cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private const string 도매단계 = 농수산시세시장단계Codes.도매터미널;

    private static KamisLatestObservation Kamis(long id, decimal? price, bool missing = false) => new()
    {
        Id = id, ItemCode = "411", FrequencyCode = "Daily", ProductClassCode = "02", ProductClassName = "도매", SurveyDate = Date,
        KindCode = "01", KindName = "kind", RankCode = "01", RankName = "rank", Unit = "원 단위", SourcePackageLabel = "10개",
        ComparisonUnit = "1kg", PriceNormalizationCode = "SourceKg", PriceNormalizationBasis = "source basis", PriceKrw = price, IsPriceMissing = missing
    };

    private static AmsLatestObservation Ams(string key, string variety, decimal? average, string source = "source", string unit = "package: cartons",
        string currency = "USD", decimal? low = null, decimal? high = null) => new()
    {
        RecordKey = key, SourceKey = source, MarketStageCode = 도매단계, ReportBeginDate = Date, Commodity = "Apples", Variety = variety,
        Grade = "grade", Package = "raw package", ItemSize = "large", Organic = "Y", Origin = "US", MarketLocationName = "New York", MarketLocationState = "NY",
        LowPrice = low, HighPrice = high, MostlyLowPrice = 1m, MostlyHighPrice = 99m, WeightedAveragePrice = average, StoreCount = 7, CurrencyCode = currency, OriginalUnit = unit
    };

    private static FoodPriceCrosswalk Crosswalk(string prefix, string itemCode)
        => new(prefix, "product", "400", itemCode, "사과", [], "candidate", "후보", "unknown", "미확인", "note", []);
}
