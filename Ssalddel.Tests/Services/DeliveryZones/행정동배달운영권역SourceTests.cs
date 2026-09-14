using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.External.PublicData.Korea;

namespace Ssalddel.Tests.Services.DeliveryZones;

public sealed class 행정동배달운영권역SourceTests
{
    private const string Revision =
        행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1DataRevision;

    [Fact]
    public async Task 지정한_12개_법정동에서_공식_행정동_30개를_해소한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var source = new Official행정동배달운영권역Source(db);

        var result = await source.조회Async(
            배달운영권역SourceScopes.NortheastSeoulRiderR1);

        Assert.Equal(30, result.Count);
        Assert.Equal(30, result.Select(x => x.AdministrativeAreaStableId).Distinct().Count());
        Assert.Equal(
            12,
            result.SelectMany(x => x.LegalAreas)
                .Select(x => x.LegalAreaStableId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(result, item =>
        {
            Assert.Equal(Revision, item.DataRevision);
            Assert.Equal(
                행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1SourceVersion,
                item.SourceVersion);
            Assert.Equal(대한민국행정동관할CodeDataset.SourceId, item.SourceId);
            Assert.Single(item.LegalAreas);
        });
        var myeonmokThreeEight = Assert.Single(
            result,
            x => x.AdministrativeAreaStableId == "region:kr:hjd:1126057500");
        Assert.Equal("서울특별시 중랑구 면목제3.8동", myeonmokThreeEight.DisplayName);
        Assert.Equal(
            "region:kr:bjd:1126010100",
            Assert.Single(myeonmokThreeEight.LegalAreas).LegalAreaStableId);
        Assert.DoesNotContain(result, x => x.DisplayName.Contains("중국동", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 공식_관할_한_건이_빠지면_부분_후보를_반환하지_않는다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, omittedAdministrativeCode: "1121574000");
        var source = new Official행정동배달운영권역Source(db);

        var exception = await Assert.ThrowsAsync<배달운영권역SourceUnavailableException>(() =>
            source.조회Async(배달운영권역SourceScopes.NortheastSeoulRiderR1));

        Assert.Equal("AdministrativeDongReferenceCountMismatch", exception.Message);
    }

    [Fact]
    public async Task r1_동결_revision에_다른_source_version이_섞이면_거절한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var mixed = await db.NormalizedRecords.FirstAsync();
        mixed.SourceVersion = "jscode20260401.zip";
        await db.SaveChangesAsync();
        var source = new Official행정동배달운영권역Source(db);

        var exception = await Assert.ThrowsAsync<배달운영권역SourceUnavailableException>(() =>
            source.조회Async(배달운영권역SourceScopes.NortheastSeoulRiderR1));

        Assert.Equal("AdministrativeDongReferenceFrozenVintageMismatch", exception.Message);
    }

    private static PublicDataIngestionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
            .UseInMemoryDatabase($"admin-dong-source-{Guid.NewGuid():N}")
            .Options;
        return new PublicDataIngestionDbContext(options);
    }

    private static async Task SeedAsync(
        PublicDataIngestionDbContext db,
        string? omittedAdministrativeCode = null)
    {
        var evidenceAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var sequence = 1L;
        foreach (var mapping in Mappings.Where(x => x.AdministrativeCode != omittedAdministrativeCode))
        {
            var administrativeId = $"region:kr:hjd:{mapping.AdministrativeCode}";
            var legalId = $"region:kr:bjd:{mapping.LegalCode}";
            db.NormalizedRecords.Add(new 외부데이터정규화Record
            {
                Id = sequence,
                RawSnapshotId = 1,
                RecordKey = $"admin-{sequence}",
                StableId = administrativeId,
                SourceId = 대한민국행정동관할CodeDataset.SourceId,
                DatasetId = 대한민국행정동관할CodeDataset.DatasetId,
                RegionStableId = administrativeId,
                MetricCode = 대한민국행정동관할CodeDataset.AdministrativeMetricCode,
                TextValue = mapping.AdministrativeName,
                UnitCode = "text",
                EvidenceAsOfUtc = evidenceAt,
                CollectedAtUtc = evidenceAt,
                SpatialPrecisionCode = "administrative-dong",
                TemporalPrecisionCode = "effective-date",
                QualityCode = "official-reference",
                DimensionKey = "status=active",
                SourceVersion = 행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1SourceVersion,
                DataRevision = Revision,
                FirstSeenAtUtc = evidenceAt,
                LastSeenAtUtc = evidenceAt
            });
            sequence++;
            db.NormalizedRecords.Add(new 외부데이터정규화Record
            {
                Id = sequence,
                RawSnapshotId = 1,
                RecordKey = $"relation-{sequence}",
                StableId = $"region:kr:hjd-bjd:{mapping.AdministrativeCode}-{mapping.LegalCode}",
                SourceId = 대한민국행정동관할CodeDataset.SourceId,
                DatasetId = 대한민국행정동관할CodeDataset.DatasetId,
                RegionStableId = administrativeId,
                MetricCode = 대한민국행정동관할CodeDataset.JurisdictionMetricCode,
                TextValue = legalId,
                UnitCode = "text",
                EvidenceAsOfUtc = evidenceAt,
                CollectedAtUtc = evidenceAt,
                SpatialPrecisionCode = "administrative-legal-crosswalk",
                TemporalPrecisionCode = "effective-date",
                QualityCode = "official-reference",
                DimensionKey = $"administrative={administrativeId}|legal={legalId}|status=active",
                SourceVersion = 행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1SourceVersion,
                DataRevision = Revision,
                FirstSeenAtUtc = evidenceAt,
                LastSeenAtUtc = evidenceAt
            });
            sequence++;
        }
        await db.SaveChangesAsync();
    }

    private static readonly (string LegalCode, string AdministrativeCode, string AdministrativeName)[] Mappings =
    [
        ("1121510100", "1121574000", "서울특별시 광진구 중곡제1동"),
        ("1121510100", "1121575000", "서울특별시 광진구 중곡제2동"),
        ("1121510100", "1121576000", "서울특별시 광진구 중곡제3동"),
        ("1121510100", "1121577000", "서울특별시 광진구 중곡제4동"),
        ("1123010400", "1123056000", "서울특별시 동대문구 전농제1동"),
        ("1123010400", "1123057000", "서울특별시 동대문구 전농제2동"),
        ("1123010500", "1123060000", "서울특별시 동대문구 답십리제1동"),
        ("1123010500", "1123061000", "서울특별시 동대문구 답십리제2동"),
        ("1123010600", "1123065000", "서울특별시 동대문구 장안제1동"),
        ("1123010600", "1123066000", "서울특별시 동대문구 장안제2동"),
        ("1123010900", "1123072000", "서울특별시 동대문구 휘경제1동"),
        ("1123010900", "1123073000", "서울특별시 동대문구 휘경제2동"),
        ("1123011000", "1123074000", "서울특별시 동대문구 이문제1동"),
        ("1123011000", "1123075000", "서울특별시 동대문구 이문제2동"),
        ("1126010100", "1126052000", "서울특별시 중랑구 면목제2동"),
        ("1126010100", "1126054000", "서울특별시 중랑구 면목제4동"),
        ("1126010100", "1126055000", "서울특별시 중랑구 면목제5동"),
        ("1126010100", "1126056500", "서울특별시 중랑구 면목본동"),
        ("1126010100", "1126057000", "서울특별시 중랑구 면목제7동"),
        ("1126010100", "1126057500", "서울특별시 중랑구 면목제3.8동"),
        ("1126010200", "1126058000", "서울특별시 중랑구 상봉제1동"),
        ("1126010200", "1126059000", "서울특별시 중랑구 상봉제2동"),
        ("1126010300", "1126060000", "서울특별시 중랑구 중화제1동"),
        ("1126010300", "1126061000", "서울특별시 중랑구 중화제2동"),
        ("1126010400", "1126062000", "서울특별시 중랑구 묵제1동"),
        ("1126010400", "1126063000", "서울특별시 중랑구 묵제2동"),
        ("1126010500", "1126065500", "서울특별시 중랑구 망우본동"),
        ("1126010500", "1126066000", "서울특별시 중랑구 망우제3동"),
        ("1126010600", "1126068000", "서울특별시 중랑구 신내1동"),
        ("1126010600", "1126069000", "서울특별시 중랑구 신내2동")
    ];
}
