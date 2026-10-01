using Hongdal.Domain.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hongdal.Infrastructure.Persistence.Configurations.Community;

public sealed class PlatformCommunityPostConfiguration : IEntityTypeConfiguration<PlatformCommunityPost>
{
    public void Configure(EntityTypeBuilder<PlatformCommunityPost> builder)
    {
        builder.ToTable("platform_community_posts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AppKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(60).IsRequired();
        builder.Property(x => x.WorkflowTag).HasMaxLength(60).IsRequired();
        builder.Property(x => x.RoleTag).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.OriginalLanguageCode).HasMaxLength(16);
        builder.Property(x => x.SharedLinkUrl).HasMaxLength(1000);
        builder.Property(x => x.SalesOfferJson).HasColumnType("longtext");
        builder.Property(x => x.커뮤니티원장Id).HasMaxLength(120);
        builder.Property(x => x.AuthorUserId).HasMaxLength(450);
        builder.Property(x => x.Nickname).HasMaxLength(40).IsRequired();
        builder.Property(x => x.AuthorDisplayCountryCode).HasMaxLength(2);
        builder.Property(x => x.AuthorDisplayCountryName).HasMaxLength(80);
        builder.Property(x => x.ReporterDisplayName).HasMaxLength(40);
        builder.Property(x => x.ReportedDisplayName).HasMaxLength(40);
        builder.Property(x => x.CommunityMomentumCode).HasMaxLength(40);
        builder.Property(x => x.CommunityMomentumMessage).HasMaxLength(240);
        builder.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PublicationStatusCode)
            .HasMaxLength(30)
            .HasDefaultValue(PlatformCommunityPostPublicationStatusCodes.Published)
            .IsRequired();
        builder.Property(x => x.PublicationLastError).HasMaxLength(1000);

        builder.HasQueryFilter(x =>
            x.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Published);

        builder.HasIndex(x => new { x.AppKey, x.IsDeleted, x.IsOperatorPinned, x.OperatorPinnedAtUtc, x.RecommendationCount, x.LastEngagedAtUtc, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.Category, x.IsDeleted, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.WorkflowTag, x.RoleTag, x.IsDeleted, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.IsReportBoardPost, x.IsDeleted, x.CreatedAtUtc });
        builder.HasIndex(x => x.커뮤니티원장Id);
        builder.HasIndex(x => x.AuthorUserId);
        builder.HasIndex(x => new
        {
            x.PublicationStatusCode,
            x.PublicationNextAttemptAtUtc,
            x.PublicationClaimedAtUtc
        }).HasDatabaseName("IX_platform_community_posts_publication_due");

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.Post)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Comments)
            .WithOne(x => x.Post)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Recommendations)
            .WithOne(x => x.Post)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Translations)
            .WithOne(x => x.Post)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
