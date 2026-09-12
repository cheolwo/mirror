using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData.Korea;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

namespace Ssalddel.Tests.Services.WorldProjection;

public sealed class 지역디오라마표시LedgerServiceTests
{
    private const string AreaId = "region:kr:hjd:1126057500";

    [Fact]
    public async Task 검증된사업장Claim과승인된기간의후원만_표시Overlay가된다()
    {
        await using var db = CreateDb();
        var business = Business();
        db.공개인허가사업장Records.Add(business);
        await db.SaveChangesAsync();
        var service = new 지역디오라마표시LedgerService(db);
        var now = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

        var claim = await service.SubmitClaimAsync(new 지역사업장표시ClaimRequest(
            "claim:restaurant:1", business.Id, AreaId, "place:restaurant:1", "동네 식당", "restaurant",
            "user:owner:1", now), CancellationToken.None);
        await service.ReviewClaimAsync(claim.ClaimStableId, 1, true, "user:reviewer:1", "OwnershipVerified", now,
            CancellationToken.None);
        var campaign = await service.SubmitCampaignAsync(new 지역디오라마후원CampaignRequest(
            "campaign:restaurant:1", claim.ClaimStableId, "후원", "이 지역 디오라마를 후원합니다.",
            now.AddHours(-1), now.AddHours(1), "user:owner:1", now), CancellationToken.None);
        await service.ReviewCampaignAsync(campaign.CampaignStableId, 1, true, "user:reviewer:1", "ContentApproved",
            now, CancellationToken.None);

        var overlays = await service.FindActiveAsync(AreaId, now.UtcDateTime, CancellationToken.None);

        var overlay = Assert.Single(overlays);
        Assert.Equal("sponsorship:campaign:restaurant:1", overlay.OverlayStableId);
        Assert.Equal("후원", overlay.BadgeText);
        Assert.True(overlay.AdvertisementDisclosureRequired);
        Assert.DoesNotContain("user:owner:1", System.Text.Json.JsonSerializer.Serialize(overlay));
    }

    [Fact]
    public async Task 광고고지가없는후원신청은_원장에저장하지않는다()
    {
        await using var db = CreateDb();
        var business = Business();
        db.공개인허가사업장Records.Add(business);
        db.지역사업장표시Claims.Add(new 지역사업장표시Claim
        {
            Id = Guid.NewGuid(),
            ClaimStableId = "claim:verified",
            PublicBusinessRecordId = business.Id,
            AdministrativeRegionStableId = AreaId,
            SemanticPlaceStableId = "place:restaurant:1",
            RequestedDisplayName = "동네 식당",
            CategoryCode = "restaurant",
            RequestedByUserStableId = "user:owner:1",
            StatusCode = 지역사업장표시ClaimStatusCodes.Verified,
            RequestedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new 지역디오라마표시LedgerService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitCampaignAsync(
            new 지역디오라마후원CampaignRequest(
                "campaign:bad", "claim:verified", "추천", "고지 없는 표시",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), "user:owner:1", DateTimeOffset.UtcNow),
            CancellationToken.None));

        Assert.Empty(db.지역디오라마후원Campaigns);
    }

    private static PublicDataIngestionDbContext CreateDb()
        => new(new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
            .UseInMemoryDatabase("regional-diorama-" + Guid.NewGuid().ToString("N"))
            .Options);

    private static 공개인허가사업장Record Business()
        => new()
        {
            Id = Guid.NewGuid(),
            SourceId = "source:local-data",
            SourceDatasetId = "dataset:licensed-business",
            OpenServiceId = "restaurant",
            ManagementNumber = "test-management-number",
            BusinessName = "동네 식당",
            SourceRevision = "2026-09",
            SourceHashSha256 = new string('a', 64),
            ObservedAtUtc = DateTimeOffset.UtcNow
        };
}
