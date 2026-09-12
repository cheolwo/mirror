using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Domain.PublicData.Korea;

namespace Ssalddel.Infrastructure.Persistence.PublicData;

internal sealed class 지역사업장표시ClaimConfiguration : IEntityTypeConfiguration<지역사업장표시Claim>
{
    public void Configure(EntityTypeBuilder<지역사업장표시Claim> builder)
    {
        builder.ToTable("regional_business_display_claims");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.ClaimStableId).IsUnique();
        builder.HasIndex(item => new { item.PublicBusinessRecordId, item.StatusCode });
        builder.HasIndex(item => new { item.AdministrativeRegionStableId, item.StatusCode });
        builder.Property(item => item.ClaimStableId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.AdministrativeRegionStableId).HasMaxLength(80).IsRequired();
        builder.Property(item => item.SemanticPlaceStableId).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RequestedDisplayName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.CategoryCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.RequestedByUserStableId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.StatusCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.Property(item => item.ReviewedByUserStableId).HasMaxLength(160);
        builder.Property(item => item.ReviewReasonCode).HasMaxLength(80);
        builder.HasOne(item => item.PublicBusinessRecord)
            .WithMany()
            .HasForeignKey(item => item.PublicBusinessRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class 지역디오라마후원CampaignConfiguration : IEntityTypeConfiguration<지역디오라마후원Campaign>
{
    public void Configure(EntityTypeBuilder<지역디오라마후원Campaign> builder)
    {
        builder.ToTable("regional_diorama_sponsorship_campaigns");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.CampaignStableId).IsUnique();
        builder.HasIndex(item => new { item.BusinessDisplayClaimId, item.StatusCode, item.StartsAtUtc, item.EndsAtUtc });
        builder.Property(item => item.CampaignStableId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.BadgeText).HasMaxLength(40).IsRequired();
        builder.Property(item => item.DetailCardText).HasMaxLength(500).IsRequired();
        builder.Property(item => item.StatusCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.Property(item => item.RequestedByUserStableId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.ReviewedByUserStableId).HasMaxLength(160);
        builder.Property(item => item.ReviewReasonCode).HasMaxLength(80);
        builder.HasOne(item => item.BusinessDisplayClaim)
            .WithMany()
            .HasForeignKey(item => item.BusinessDisplayClaimId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
