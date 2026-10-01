using Hongdal.Contracts.Common.Localization;

namespace Hongdal.Tests.Contracts.Common.Localization;

public sealed class DisplayLanguageCodesTests
{
    [Theory]
    [InlineData("ko", DisplayLanguageCodes.Korean)]
    [InlineData("ko-KR", DisplayLanguageCodes.Korean)]
    [InlineData("en", DisplayLanguageCodes.English)]
    [InlineData("en-US", DisplayLanguageCodes.English)]
    [InlineData("EN-gb", DisplayLanguageCodes.English)]
    public void Normalize_UsesSupportedDisplayLanguage(string source, string expected)
        => Assert.Equal(expected, DisplayLanguageCodes.Normalize(source));

    [Fact]
    public void TryNormalize_UnsupportedLanguage_DoesNotReportSuccess()
    {
        var found = DisplayLanguageCodes.TryNormalize("ja-JP", out var languageCode);

        Assert.False(found);
        Assert.Equal(DisplayLanguageCodes.Korean, languageCode);
    }
}
