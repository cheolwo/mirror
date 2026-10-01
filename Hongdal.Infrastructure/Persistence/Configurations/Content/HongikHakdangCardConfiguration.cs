using Hongdal.Domain.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hongdal.Infrastructure.Persistence.Configurations.Content;

public sealed class HongikHakdangCardCollectionConfiguration
    : IEntityTypeConfiguration<HongikHakdangCardCollection>
{
    public void Configure(EntityTypeBuilder<HongikHakdangCardCollection> builder)
    {
        builder.ToTable("hongik_hakdang_card_collections");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.IsAdminEnabled).HasColumnName("is_admin_enabled");
        builder.Property(x => x.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.SourceKey)
            .HasDatabaseName("IX_hh_card_collections_source_key")
            .IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.SortOrder })
            .HasDatabaseName("IX_hh_card_collections_active_order");
    }
}

public sealed class HongikHakdangCardConfiguration : IEntityTypeConfiguration<HongikHakdangCard>
{
    public void Configure(EntityTypeBuilder<HongikHakdangCard> builder)
    {
        builder.ToTable("hongik_hakdang_cards");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(500);
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.OriginalImageUrl).HasColumnName("original_image_url").HasMaxLength(1500).IsRequired();
        builder.Property(x => x.ThumbnailImageUrl).HasColumnName("thumbnail_image_url").HasMaxLength(1500);
        builder.Property(x => x.RelatedUrl).HasColumnName("related_url").HasMaxLength(1500);
        builder.Property(x => x.LocalImagePath).HasColumnName("local_image_path").HasMaxLength(1000);
        builder.Property(x => x.ImageContentType).HasColumnName("image_content_type").HasMaxLength(100);
        builder.Property(x => x.ImageSizeBytes).HasColumnName("image_size_bytes");
        builder.Property(x => x.ImageSha256).HasColumnName("image_sha256").HasMaxLength(64);
        builder.Property(x => x.ImageDownloadStatus).HasColumnName("image_download_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ImageDownloadError).HasColumnName("image_download_error").HasMaxLength(1000);
        builder.Property(x => x.ImageDownloadedAtUtc).HasColumnName("image_downloaded_at_utc");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.IsAdminEnabled).HasColumnName("is_admin_enabled");
        builder.Property(x => x.IsCommunityPublicationApproved)
            .HasColumnName("is_community_publication_approved");
        builder.Property(x => x.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.SourceKey)
            .HasDatabaseName("IX_hh_cards_source_key")
            .IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.LastSeenAtUtc })
            .HasDatabaseName("IX_hh_cards_active_last_seen");
        builder.HasIndex(x => x.ImageDownloadStatus)
            .HasDatabaseName("IX_hh_cards_download_status");
        builder.HasIndex(x => new { x.IsCommunityPublicationApproved, x.IsActive, x.IsAdminEnabled })
            .HasDatabaseName("IX_hh_cards_community_publication");
    }
}

public sealed class HongikHakdangCardCollectionItemConfiguration
    : IEntityTypeConfiguration<HongikHakdangCardCollectionItem>
{
    public void Configure(EntityTypeBuilder<HongikHakdangCardCollectionItem> builder)
    {
        builder.ToTable("hongik_hakdang_card_collection_items");
        builder.HasKey(x => new { x.CollectionId, x.CardId });

        builder.Property(x => x.CollectionId).HasColumnName("collection_id");
        builder.Property(x => x.CardId).HasColumnName("card_id");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.LastSeenAtUtc).HasColumnName("last_seen_at_utc");

        builder.HasOne(x => x.Collection)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CollectionId)
            .HasConstraintName("FK_hh_card_items_collections")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Card)
            .WithMany(x => x.Collections)
            .HasForeignKey(x => x.CardId)
            .HasConstraintName("FK_hh_card_items_cards")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CollectionId, x.IsActive, x.SortOrder })
            .HasDatabaseName("IX_hh_card_items_collection_active_order");
        builder.HasIndex(x => new { x.CardId, x.IsActive })
            .HasDatabaseName("IX_hh_card_items_card_active");
    }
}
