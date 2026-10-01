using Hongdal.Domain.AgriculturalFisheries;
using Microsoft.EntityFrameworkCore;

namespace Hongdal.Infrastructure.Persistence.AgriculturalFisheries;

public sealed class AgriculturalFisheriesDbContext : DbContext
{
    public AgriculturalFisheriesDbContext(DbContextOptions<AgriculturalFisheriesDbContext> options)
        : base(options)
    {
    }

    public DbSet<UsdaNassPriceCollectionRun> CollectionRuns => Set<UsdaNassPriceCollectionRun>();

    public DbSet<UsdaNassPriceObservation> PriceObservations => Set<UsdaNassPriceObservation>();

    public DbSet<HsUsdaCommodityMapping> HsCommodityMappings => Set<HsUsdaCommodityMapping>();

    public DbSet<KamisPriceCollectionRun> KamisCollectionRuns => Set<KamisPriceCollectionRun>();

    public DbSet<KamisPriceObservation> KamisPriceObservations => Set<KamisPriceObservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UsdaNassPriceCollectionRunConfiguration());
        modelBuilder.ApplyConfiguration(new UsdaNassPriceObservationConfiguration());
        modelBuilder.ApplyConfiguration(new HsUsdaCommodityMappingConfiguration());
        modelBuilder.ApplyConfiguration(new KamisPriceCollectionRunConfiguration());
        modelBuilder.ApplyConfiguration(new KamisPriceObservationConfiguration());
    }
}
