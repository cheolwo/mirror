using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Domain.PublicData;

namespace Ssalddel.Infrastructure.Persistence.PublicData;

internal sealed class 외부데이터수집RunConfiguration
    : IEntityTypeConfiguration<외부데이터수집Run>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<외부데이터수집Run> builder)
    {
        builder.ToTable("public_data_ingestion_runs");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.RunKey).IsUnique();
        builder.HasIndex(item => new { item.SourceId, item.DatasetId, item.StartedAtUtc });
        builder.Property(item => item.RunKey).HasMaxLength(80).IsRequired();
        builder.Property(item => item.SourceId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.DatasetId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.StatusCode).HasMaxLength(32).IsRequired();
        builder.Property(item => item.SourceVersion).HasMaxLength(200);
        builder.Property(item => item.DataRevision).HasMaxLength(200);
        builder.Property(item => item.ErrorCode).HasMaxLength(80);
        builder.Property(item => item.ErrorSummary).HasMaxLength(500);
    }
}

internal sealed class 외부데이터RawSnapshotConfiguration
    : IEntityTypeConfiguration<외부데이터RawSnapshot>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<외부데이터RawSnapshot> builder)
    {
        builder.ToTable("public_data_raw_snapshots");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.SourceId, item.DatasetId, item.ContentHashSha256 }).IsUnique();
        builder.Property(item => item.SourceId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.DatasetId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.SourceVersion).HasMaxLength(200);
        builder.Property(item => item.ContentHashSha256).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(160);
        builder.Property(item => item.OriginalFileName).HasMaxLength(255);
        builder.Property(item => item.StorageContainer).HasMaxLength(200).IsRequired();
        builder.Property(item => item.StorageObjectName).HasMaxLength(1024).IsRequired();
        builder.Property(item => item.StorageLocation).HasMaxLength(2048).IsRequired();
        builder.HasOne(item => item.FirstCollectionRun)
            .WithMany()
            .HasForeignKey(item => item.FirstCollectionRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class 외부데이터정규화RecordConfiguration
    : IEntityTypeConfiguration<외부데이터정규화Record>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<외부데이터정규화Record> builder)
    {
        builder.ToTable("public_data_normalized_records");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.RecordKey).IsUnique();
        builder.HasIndex(item => new
        {
            item.RegionStableId,
            item.MetricCode,
            item.EvidenceAsOfUtc
        });
        builder.Property(item => item.RecordKey).HasMaxLength(64).IsRequired();
        builder.Property(item => item.StableId).HasMaxLength(240).IsRequired();
        builder.Property(item => item.SourceId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.DatasetId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.RegionStableId).HasMaxLength(240).IsRequired();
        builder.Property(item => item.MetricCode).HasMaxLength(160).IsRequired();
        builder.Property(item => item.NumericValue).HasPrecision(28, 10);
        builder.Property(item => item.TextValue).HasMaxLength(2000);
        builder.Property(item => item.UnitCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.SpatialPrecisionCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.TemporalPrecisionCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.QualityCode).HasMaxLength(80);
        builder.Property(item => item.LimitationCode).HasMaxLength(240);
        builder.Property(item => item.DimensionKey).HasMaxLength(500);
        builder.Property(item => item.SourceVersion).HasMaxLength(200);
        builder.Property(item => item.DataRevision).HasMaxLength(200).IsRequired();
        builder.HasOne(item => item.RawSnapshot)
            .WithMany()
            .HasForeignKey(item => item.RawSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class 외부지역CodeMappingConfiguration
    : IEntityTypeConfiguration<외부지역CodeMapping>, IDedicatedDbContextConfiguration
{
    public void Configure(EntityTypeBuilder<외부지역CodeMapping> builder)
    {
        builder.ToTable("public_data_region_mappings");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.SourceId, item.ExternalRegionCode }).IsUnique();
        builder.Property(item => item.SourceId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.ExternalRegionCode).HasMaxLength(240).IsRequired();
        builder.Property(item => item.RegionStableId).HasMaxLength(240).IsRequired();
        builder.Property(item => item.SpatialPrecisionCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.MappingRevision).HasMaxLength(200).IsRequired();
        builder.HasData(
            new 외부지역CodeMapping
            {
                Id = -1001,
                SourceId = "world-bank-indicators",
                ExternalRegionCode = "KOR",
                RegionStableId = "country:kr",
                SpatialPrecisionCode = "country",
                MappingRevision = "iso3166-1-alpha3-v2026-08",
            },
            new 외부지역CodeMapping
            {
                Id = -1002,
                SourceId = "world-bank-indicators",
                ExternalRegionCode = "USA",
                RegionStableId = "country:us",
                SpatialPrecisionCode = "country",
                MappingRevision = "iso3166-1-alpha3-v2026-08",
            },
            new 외부지역CodeMapping
            {
                Id = -1003,
                SourceId = "world-bank-indicators",
                ExternalRegionCode = "CHN",
                RegionStableId = "country:cn",
                SpatialPrecisionCode = "country",
                MappingRevision = "iso3166-1-alpha3-v2026-08",
            });
    }
}
