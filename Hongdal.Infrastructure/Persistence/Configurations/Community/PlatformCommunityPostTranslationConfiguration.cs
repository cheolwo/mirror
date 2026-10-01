using Hongdal.Domain.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hongdal.Infrastructure.Persistence.Configurations.Community;

public sealed class PlatformCommunityPostTranslationConfiguration
    : IEntityTypeConfiguration<PlatformCommunityPostTranslation>
{
    public void Configure(EntityTypeBuilder<PlatformCommunityPostTranslation> builder)
    {
        builder.ToTable("platform_community_post_translations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceLanguageCode).HasMaxLength(16).IsRequired();
        builder.Property(x => x.TargetLanguageCode).HasMaxLength(16).IsRequired();
        builder.Property(x => x.SourceContentHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.TranslatedTitle).HasMaxLength(500).IsRequired();
        builder.Property(x => x.TranslatedBody).HasColumnType("longtext").IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ProviderModelVersion).HasMaxLength(80).IsRequired();

        builder.HasIndex(x => new { x.PostId, x.TargetLanguageCode, x.SourceContentHash })
            .IsUnique()
            .HasDatabaseName("UX_community_post_translation_content");
        builder.HasIndex(x => new { x.PostId, x.CreatedAtUtc })
            .HasDatabaseName("IX_community_post_translation_post_created");
    }
}
