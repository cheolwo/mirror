using Hongdal.Domain.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hongdal.Infrastructure.Persistence.Configurations.Content;

public sealed class YouTube감시채널Configuration : IEntityTypeConfiguration<YouTube감시채널>
{
    public void Configure(EntityTypeBuilder<YouTube감시채널> builder)
    {
        builder.ToTable("youtube_watched_channels");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChannelId).HasColumnName("channel_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.채널명).HasColumnName("channel_name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.UploadsPlaylistId).HasColumnName("uploads_playlist_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.썸네일Url).HasColumnName("thumbnail_url").HasMaxLength(1000);
        builder.Property(x => x.음식채널여부).HasColumnName("is_food_channel");
        builder.Property(x => x.Handle).HasColumnName("channel_handle").HasMaxLength(100);
        builder.Property(x => x.국가코드).HasColumnName("country_code").HasMaxLength(2).IsRequired();
        builder.Property(x => x.기본언어코드).HasColumnName("default_language_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.음식콘텐츠분류).HasColumnName("food_category_codes").HasMaxLength(300).IsRequired();
        builder.Property(x => x.구매발견점수).HasColumnName("purchase_discovery_score");
        builder.Property(x => x.수입발견점수).HasColumnName("import_discovery_score");
        builder.Property(x => x.조사근거Url).HasColumnName("research_source_url").HasMaxLength(1000);
        builder.Property(x => x.조사메모).HasColumnName("research_note").HasMaxLength(1000);
        builder.Property(x => x.조사확인일시Utc).HasColumnName("research_verified_at_utc");
        builder.Property(x => x.지식성찰채널여부).HasColumnName("is_knowledge_reflection_channel");
        builder.Property(x => x.지식성찰분류).HasColumnName("knowledge_reflection_category_codes").HasMaxLength(300).IsRequired();
        builder.Property(x => x.관점표시).HasColumnName("perspective_label").HasMaxLength(200).IsRequired();
        builder.Property(x => x.공식출처Url).HasColumnName("official_source_url").HasMaxLength(1000);
        builder.Property(x => x.자료확인일시Utc).HasColumnName("source_verified_at_utc");
        builder.Property(x => x.반야게시허용여부).HasColumnName("is_prajna_publication_allowed");
        builder.Property(x => x.활성화여부).HasColumnName("is_active");
        builder.Property(x => x.초기동기화완료여부).HasColumnName("initial_sync_completed");
        builder.Property(x => x.마지막동기화일시Utc).HasColumnName("last_synced_at_utc");
        builder.Property(x => x.마지막영상Id).HasColumnName("latest_video_id").HasMaxLength(100);
        builder.Property(x => x.마지막영상게시일시Utc).HasColumnName("latest_video_published_at_utc");
        builder.Property(x => x.생성일시Utc).HasColumnName("created_at_utc");
        builder.Property(x => x.수정일시Utc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.ChannelId).IsUnique();
        builder.HasIndex(x => new { x.국가코드, x.활성화여부, x.마지막동기화일시Utc })
            .HasDatabaseName("IX_youtube_watched_channels_country_active_sync");
        builder.HasIndex(x => new { x.음식채널여부, x.구매발견점수, x.수입발견점수 });
        builder.HasIndex(x => new { x.지식성찰채널여부, x.반야게시허용여부, x.활성화여부 })
            .HasDatabaseName("IX_youtube_watched_channels_knowledge_prajna_active");
        builder.HasIndex(x => new { x.활성화여부, x.마지막동기화일시Utc });
    }
}

public sealed class YouTube채널영상Configuration : IEntityTypeConfiguration<YouTube채널영상>
{
    public void Configure(EntityTypeBuilder<YouTube채널영상> builder)
    {
        builder.ToTable("youtube_channel_videos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.YouTube감시채널Id).HasColumnName("watched_channel_id");
        builder.Property(x => x.VideoId).HasColumnName("video_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChannelId).HasColumnName("channel_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.제목).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(x => x.설명).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(x => x.게시일시Utc).HasColumnName("published_at_utc");
        builder.Property(x => x.썸네일Url).HasColumnName("thumbnail_url").HasMaxLength(1000);
        builder.Property(x => x.신규업로드여부).HasColumnName("is_new_upload");
        builder.Property(x => x.공유상태).HasColumnName("sharing_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.최초감지일시Utc).HasColumnName("first_detected_at_utc");

        builder.HasOne(x => x.감시채널)
            .WithMany(x => x.영상)
            .HasForeignKey(x => x.YouTube감시채널Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VideoId).IsUnique();
        builder.HasIndex(x => new { x.ChannelId, x.게시일시Utc });
        builder.HasIndex(x => new { x.신규업로드여부, x.공유상태, x.최초감지일시Utc });
    }
}

public sealed class YouTube영상상품후보Configuration : IEntityTypeConfiguration<YouTube영상상품후보>
{
    public void Configure(EntityTypeBuilder<YouTube영상상품후보> builder)
    {
        builder.ToTable("youtube_video_product_candidates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.YouTube채널영상Id).HasColumnName("youtube_channel_video_id");
        builder.Property(x => x.상품키).HasColumnName("product_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.상품명).HasColumnName("product_name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.브랜드명).HasColumnName("brand_name").HasMaxLength(200);
        builder.Property(x => x.원산지국가코드).HasColumnName("origin_country_code").HasMaxLength(2);
        builder.Property(x => x.HS코드후보).HasColumnName("hs_code_candidate").HasMaxLength(20);
        builder.Property(x => x.온도코드).HasColumnName("temperature_code").HasMaxLength(30).IsRequired();
        builder.Property(x => x.물류방식).HasColumnName("logistics_mode").HasMaxLength(30).IsRequired();
        builder.Property(x => x.후보유형).HasColumnName("candidate_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.영상구간초).HasColumnName("video_timestamp_seconds");
        builder.Property(x => x.발견근거).HasColumnName("discovery_evidence").HasColumnType("text").IsRequired();
        builder.Property(x => x.추출방식).HasColumnName("extraction_method").HasMaxLength(40).IsRequired();
        builder.Property(x => x.신뢰도).HasColumnName("confidence").HasPrecision(5, 4);
        builder.Property(x => x.검수상태).HasColumnName("review_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.협찬표시상태).HasColumnName("sponsorship_disclosure_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.허용의향유형).HasColumnName("allowed_intent_types").HasMaxLength(200).IsRequired();
        builder.Property(x => x.공식구매Url).HasColumnName("official_purchase_url").HasMaxLength(1000);
        builder.Property(x => x.검수메모).HasColumnName("review_note").HasMaxLength(1000);
        builder.Property(x => x.검수자UserId).HasColumnName("reviewer_user_id").HasMaxLength(450);
        builder.Property(x => x.검수일시Utc).HasColumnName("reviewed_at_utc");
        builder.Property(x => x.생성일시Utc).HasColumnName("created_at_utc");
        builder.Property(x => x.수정일시Utc).HasColumnName("updated_at_utc");

        builder.HasOne(x => x.영상)
            .WithMany(x => x.상품후보)
            .HasForeignKey(x => x.YouTube채널영상Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.YouTube채널영상Id, x.상품키 }).IsUnique();
        builder.HasIndex(x => new { x.검수상태, x.수정일시Utc });
        builder.HasIndex(x => new { x.후보유형, x.검수상태 });
    }
}
