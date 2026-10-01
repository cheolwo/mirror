using Hongdal.Services.AgriculturalFisheries.Information;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class KamisPriceArchiveServiceTests
{
    [Theory]
    [InlineData("당일 (07/15)", "2026-07-15", "2026-07-15")]
    [InlineData("당일 (12/31)", "2026-01-01", "2025-12-31")]
    [InlineData("2026-07-14", "2026-07-15", "2026-07-14")]
    public void 조사일표시를_요청일기준_날짜로변환한다(
        string source,
        string requested,
        string expected)
    {
        var result = KamisPriceValueParser.ParseSurveyDate(source, DateOnly.Parse(requested));

        Assert.Equal(DateOnly.Parse(expected), result);
    }

    [Theory]
    [InlineData("6,744", 6744)]
    [InlineData(" 1,234.5 ", 1234.5)]
    public void 쉼표가있는_가격을_숫자로변환한다(string source, decimal expected)
    {
        Assert.Equal(expected, KamisPriceValueParser.ParsePrice(source));
    }

    [Theory]
    [InlineData("-")]
    [InlineData("")]
    public void 미제공가격은_null로변환한다(string source)
    {
        Assert.Null(KamisPriceValueParser.ParsePrice(source));
    }

    [Theory]
    [InlineData("2025", "07/17", "2025-07-17")]
    [InlineData("2026", "1/02", "2026-01-02")]
    public void 기간가격의_연도와_월일을_조사일로변환한다(
        string year,
        string monthDay,
        string expected)
    {
        var result = KamisPriceValueParser.ParsePeriodSurveyDate(year, monthDay);

        Assert.Equal(DateOnly.Parse(expected), result);
    }

    [Fact]
    public void 기간조회는_직전_1년_범위를허용한다()
    {
        KamisPriceRequestRules.ValidatePeriod(
            new DateOnly(2025, 7, 17),
            new DateOnly(2026, 7, 16));
    }

    [Fact]
    public void 기간조회가_1년을넘으면_거부한다()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KamisPriceRequestRules.ValidatePeriod(
                new DateOnly(2025, 7, 17),
                new DateOnly(2026, 7, 17)));
    }
}
