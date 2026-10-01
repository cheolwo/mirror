using Hongdal.Domain.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hongdal.Infrastructure.Persistence.Configurations.Community;

public sealed class PlatformCommunityBoardRequestConfiguration : IEntityTypeConfiguration<PlatformCommunityBoardRequest>
{
    public void Configure(EntityTypeBuilder<PlatformCommunityBoardRequest> builder)
    {
        builder.ToTable("platform_community_board_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AppKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.BoardKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.RequestedBy).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RequestReason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OperatorMemo).HasMaxLength(1000);
        builder.Property(x => x.ReviewedByUserId).HasMaxLength(450);

        builder.HasIndex(x => new { x.AppKey, x.Status, x.IsDeleted, x.UpdatedAtUtc });
        builder.HasIndex(x => new { x.AppKey, x.BoardKey, x.IsDeleted });
        builder.HasIndex(x => new { x.RequestedByUserId, x.Status, x.IsDeleted, x.CreatedAtUtc })
            .HasDatabaseName("IX_community_board_requests_requester_status");
    }
}
