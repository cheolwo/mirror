using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Services.AgriculturalFisheries.Information;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class UnitedStatesAgriculturalFisheriesOperatorSourceServiceTests
{
    [Fact]
    public void 카탈로그는_통합명부가아닌_공개목적과비공개경계를_구분한다()
    {
        var sources = UnitedStatesAgriculturalFisheriesOperatorSourceCatalog.Sources;

        Assert.Equal(10, sources.Count);
        Assert.Equal(
            sources.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            sources);
        Assert.Equal(
            sources.Count,
            sources.Select(item => item.SourceKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        foreach (var source in sources)
        {
            Assert.NotEmpty(source.SectorCodes);
            Assert.NotEmpty(source.AccessModeCodes);
            Assert.False(source.IsComprehensiveRegistry);
            Assert.False(source.CanVerifyTransactionAuthority);
            Assert.False(source.CanAutoInvite);
            Assert.False(source.CanAutoSelectForOperations);
            Assert.True(source.RequiresLiveRecheck);
            Assert.StartsWith("https://", source.OfficialUrl, StringComparison.Ordinal);
            Assert.NotEmpty(source.AllowedPlatformUses);
            Assert.NotEmpty(source.ProhibitedPlatformUses);
            Assert.NotEmpty(source.Limitations);
            Assert.All(source.Evidence, evidence =>
            {
                Assert.Equal(
                    UnitedStatesAgriculturalFisheriesOperatorSourceCatalog
                        .SnapshotReviewedOn,
                    evidence.ReviewedOn);
                Assert.StartsWith(
                    "https://",
                    evidence.SourceUrl,
                    StringComparison.Ordinal);
            });
        }

        var restricted = sources.Single(item =>
            item.SourceKey == "usda-fsa-producer-program-records");
        Assert.Equal(
            미국농어업경영체정보공개범위Codes.RestrictedIndividualRecords,
            restricted.PublicAccessCode);
        Assert.Equal(
            미국농어업경영체정보통합상태Codes.DoNotIngest,
            restricted.IntegrationStatusCode);
        Assert.False(restricted.CanDiscoverBusinesses);

        var census = sources.Single(item =>
            item.SourceKey == "usda-nass-census-agriculture");
        Assert.Equal(
            미국농어업경영체정보공개범위Codes.PublicAggregateOnly,
            census.PublicAccessCode);
        Assert.False(census.CanDiscoverBusinesses);
        Assert.False(census.ContainsPotentialPersonalData);
    }

    [Fact]
    public void 조회는_공개범위와통합상태를_함께필터링한다()
    {
        var service = new 미국농어업경영체정보원천Service();

        var response = service.Search(new 미국농어업경영체정보원천조회요청
        {
            PublicAccessCode =
                미국농어업경영체정보공개범위Codes.PublicBusinessDirectory,
            IntegrationStatusCode =
                미국농어업경영체정보통합상태Codes.BulkIntegrationCandidate
        });

        Assert.True(response.Success);
        Assert.False(response.HasUnifiedPublicOperatorRegistry);
        Assert.True(response.IndividualOperationRecordsGenerallyConfidential);
        Assert.True(response.DiscoveryOnly);
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(
            new[]
            {
                "usda-fsis-inspection-directory",
                "usda-local-food-directories"
            },
            response.Items.Select(item => item.SourceKey));
        Assert.All(response.Items, item =>
        {
            Assert.True(item.CanDiscoverBusinesses);
            Assert.False(item.CanAutoInvite);
            Assert.False(item.CanAutoSelectForOperations);
        });
    }

    [Fact]
    public void 조회는_수산업분야와검색어와페이지를_적용한다()
    {
        var service = new 미국농어업경영체정보원천Service();

        var fisheries = service.Search(new 미국농어업경영체정보원천조회요청
        {
            SectorCode = 미국농어업경영체정보분야Codes.WildCaptureFisheries
        });
        var searched = service.Search(new 미국농어업경영체정보원천조회요청
        {
            SearchText = "FDA",
            Page = 0,
            PageSize = 500
        });

        Assert.Equal(2, fisheries.TotalCount);
        Assert.Contains(fisheries.Items, item =>
            item.SourceKey == "noaa-commercial-landings");
        Assert.Contains(fisheries.Items, item =>
            item.SourceKey == "noaa-greater-atlantic-permits");

        Assert.Equal(1, searched.Page);
        Assert.Equal(100, searched.PageSize);
        Assert.Equal(
            "fda-interstate-certified-shellfish-shippers",
            Assert.Single(searched.Items).SourceKey);
        Assert.Contains(
            미국농어업경영체정보기록유형Codes.ConfidentialAdministrativeRecords,
            searched.AvailableRecordTypeCodes);
    }
}
