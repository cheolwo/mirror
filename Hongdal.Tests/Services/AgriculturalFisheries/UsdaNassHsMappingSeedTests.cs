using Hongdal.Domain.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.Information;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class UsdaNassHsMappingSeedTests
{
    [Fact]
    public void Create_ProvidesUniqueReviewableHs6Mappings()
    {
        var mappings = UsdaNassHsMappingSeed.Create();

        Assert.Equal(24, mappings.Count);
        Assert.Equal(mappings.Count, mappings.Select(item => item.MappingKey).Distinct().Count());
        Assert.All(mappings, mapping =>
        {
            Assert.Matches("^[0-9]{6}$", mapping.HsCode6);
            Assert.Equal(HsUsdaMappingReviewStatusCodes.NeedsReview, mapping.ReviewStatusCode);
            Assert.NotEmpty(mapping.UsdaCommodityDesc);
            Assert.NotEmpty(mapping.SourceUrl);
        });
        Assert.Contains(mappings, item => item.HsCode6 == "100590" && item.UsdaCommodityDesc == "CORN");
        Assert.Contains(mappings, item => item.HsCode6 == "070200" && item.UsdaCommodityDesc == "TOMATOES");
        Assert.Contains(mappings, item => item.HsCode6 == "080810" && item.UsdaCommodityDesc == "APPLES");
    }
}
