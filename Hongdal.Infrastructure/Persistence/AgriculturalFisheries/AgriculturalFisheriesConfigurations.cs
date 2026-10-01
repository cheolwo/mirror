using Hongdal.Domain.AgriculturalFisheries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hongdal.Infrastructure.Persistence.AgriculturalFisheries;

internal sealed class UsdaNassPriceCollectionRunConfiguration
    : IEntityTypeConfiguration<UsdaNassPriceCollectionRun>
{
    public void Configure(EntityTypeBuilder<UsdaNassPriceCollectionRun> builder)
    {
        builder.ToTable("agri_usda_nass_collection_runs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RunKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.QuerySummary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000).IsRequired();

        builder.HasIndex(x => x.RunKey).IsUnique();
        builder.HasIndex(x => new { x.StatusCode, x.StartedAtUtc });
    }
}

internal sealed class UsdaNassPriceObservationConfiguration
    : IEntityTypeConfiguration<UsdaNassPriceObservation>
{
    public void Configure(EntityTypeBuilder<UsdaNassPriceObservation> builder)
    {
        builder.ToTable("agri_usda_nass_price_observations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecordKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceDesc).HasMaxLength(60).IsRequired();
        builder.Property(x => x.SectorDesc).HasMaxLength(60).IsRequired();
        builder.Property(x => x.GroupDesc).HasMaxLength(80).IsRequired();
        builder.Property(x => x.CommodityDesc).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ClassDesc).HasMaxLength(180).IsRequired();
        builder.Property(x => x.UtilPracticeDesc).HasMaxLength(180).IsRequired();
        builder.Property(x => x.ProductionPracticeDesc).HasMaxLength(180).IsRequired();
        builder.Property(x => x.StatisticCategoryDesc).HasMaxLength(80).IsRequired();
        builder.Property(x => x.UnitDesc).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ShortDesc).HasMaxLength(512).IsRequired();
        builder.Property(x => x.DomainDesc).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DomainCategoryDesc).HasMaxLength(512).IsRequired();
        builder.Property(x => x.AggregationLevelDesc).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CountryCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.CountryName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.FrequencyDesc).HasMaxLength(30).IsRequired();
        builder.Property(x => x.BeginCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.EndCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.ReferencePeriodDesc).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ValueRaw).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NumericValue).HasPrecision(24, 6);
        builder.Property(x => x.CvPercentRaw).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RawJson).HasColumnType("json").IsRequired();

        builder.HasOne(x => x.FirstCollectionRun)
            .WithMany(x => x.NewObservations)
            .HasForeignKey(x => x.FirstCollectionRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RecordKey).IsUnique();
        builder.HasIndex(x => new
        {
            x.CommodityDesc,
            x.Year,
            x.FrequencyDesc,
            x.ReferencePeriodDesc
        });
        builder.HasIndex(x => x.SourceLoadTimeUtc);
        builder.HasIndex(x => x.LastSeenAtUtc);
    }
}

internal sealed class HsUsdaCommodityMappingConfiguration
    : IEntityTypeConfiguration<HsUsdaCommodityMapping>
{
    public void Configure(EntityTypeBuilder<HsUsdaCommodityMapping> builder)
    {
        builder.ToTable("agri_hs_usda_commodity_mappings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MappingKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.HsCode6).HasMaxLength(6).IsRequired();
        builder.Property(x => x.ProductNameKo).HasMaxLength(100).IsRequired();
        builder.Property(x => x.HsDescriptionEn).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UsdaCommodityDesc).HasMaxLength(80).IsRequired();
        builder.Property(x => x.UsdaClassDesc).HasMaxLength(180).IsRequired();
        builder.Property(x => x.UsdaUtilPracticeDesc).HasMaxLength(180).IsRequired();
        builder.Property(x => x.UsdaProductionPracticeDesc).HasMaxLength(180).IsRequired();
        builder.Property(x => x.MatchQualityCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ReviewStatusCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ReviewOwnerUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ReviewNote).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();

        builder.HasIndex(x => x.MappingKey).IsUnique();
        builder.HasIndex(x => new { x.HsCode6, x.IsActive });
        builder.HasIndex(x => new { x.UsdaCommodityDesc, x.IsActive });
        builder.HasIndex(x => new { x.ReviewStatusCode, x.IsActive });
    }
}

internal sealed class KamisPriceCollectionRunConfiguration
    : IEntityTypeConfiguration<KamisPriceCollectionRun>
{
    public void Configure(EntityTypeBuilder<KamisPriceCollectionRun> builder)
    {
        builder.ToTable("agri_kamis_price_collection_runs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RunKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RequestedDate).HasColumnType("date");
        builder.Property(x => x.LatestSurveyDate).HasColumnType("date");
        builder.Property(x => x.QuerySummary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000).IsRequired();

        builder.HasIndex(x => x.RunKey).IsUnique();
        builder.HasIndex(x => new { x.StatusCode, x.StartedAtUtc });
        builder.HasIndex(x => x.RequestedDate);
    }
}

internal sealed class KamisPriceObservationConfiguration
    : IEntityTypeConfiguration<KamisPriceObservation>
{
    public void Configure(EntityTypeBuilder<KamisPriceObservation> builder)
    {
        builder.ToTable("agri_kamis_price_observations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecordKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProductClassCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.ProductClassName).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CategoryCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CategoryName).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CountryCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.CountryName).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RequestedDate).HasColumnType("date");
        builder.Property(x => x.SurveyDate).HasColumnType("date");
        builder.Property(x => x.FrequencyCode)
            .HasMaxLength(20)
            .HasDefaultValue("Daily")
            .IsRequired();
        builder.Property(x => x.ItemName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ItemCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.KindName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.KindCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RankName).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RankCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PriceRaw).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PriceKrw).HasPrecision(20, 4);
        builder.Property(x => x.PreviousDayLabel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PreviousDayPriceKrw).HasPrecision(20, 4);
        builder.Property(x => x.OneWeekAgoLabel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.OneWeekAgoPriceKrw).HasPrecision(20, 4);
        builder.Property(x => x.TwoWeeksAgoLabel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TwoWeeksAgoPriceKrw).HasPrecision(20, 4);
        builder.Property(x => x.OneMonthAgoLabel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.OneMonthAgoPriceKrw).HasPrecision(20, 4);
        builder.Property(x => x.OneYearAgoLabel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.OneYearAgoPriceKrw).HasPrecision(20, 4);
        builder.Property(x => x.NormalYearLabel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.NormalYearPriceKrw).HasPrecision(20, 4);
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RawJson).HasColumnType("json").IsRequired();

        builder.HasOne(x => x.FirstCollectionRun)
            .WithMany(x => x.NewObservations)
            .HasForeignKey(x => x.FirstCollectionRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RecordKey).IsUnique();
        builder.HasIndex(x => new
        {
            x.SurveyDate,
            x.FrequencyCode,
            x.ProductClassCode,
            x.CategoryCode,
            x.ItemCode
        });
        builder.HasIndex(x => new { x.ItemName, x.SurveyDate });
        builder.HasIndex(x => x.LastSeenAtUtc);
    }
}
