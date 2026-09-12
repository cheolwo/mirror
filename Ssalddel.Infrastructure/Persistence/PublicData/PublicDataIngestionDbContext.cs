using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Domain.PublicData.Korea;

namespace Ssalddel.Infrastructure.Persistence.PublicData;

public sealed class PublicDataIngestionDbContext : DbContext
{
    public PublicDataIngestionDbContext(DbContextOptions<PublicDataIngestionDbContext> options)
        : base(options)
    {
    }

    public DbSet<외부데이터수집Run> IngestionRuns => Set<외부데이터수집Run>();
    public DbSet<외부데이터RawSnapshot> RawSnapshots => Set<외부데이터RawSnapshot>();
    public DbSet<외부데이터정규화Record> NormalizedRecords => Set<외부데이터정규화Record>();
    public DbSet<외부지역CodeMapping> RegionMappings => Set<외부지역CodeMapping>();
    public DbSet<건축물용도CategoryDefinition> BuildingCategoryDefinitions => Set<건축물용도CategoryDefinition>();
    public DbSet<건축물대장표제부Record> BuildingRegisterTitles => Set<건축물대장표제부Record>();
    public DbSet<건축물행정구역Assignment> BuildingRegionAssignments => Set<건축물행정구역Assignment>();
    public DbSet<건축물용도CategoryAssignment> BuildingCategoryAssignments => Set<건축물용도CategoryAssignment>();
    public DbSet<행정동건축물CategoryAggregate> AdministrativeBuildingCategoryAggregates => Set<행정동건축물CategoryAggregate>();
    public DbSet<건축물형태Profile> 건축물형태Profiles => Set<건축물형태Profile>();
    public DbSet<건축물시각구성계획> 건축물시각구성계획들 => Set<건축물시각구성계획>();
    public DbSet<공개인허가사업장Record> 공개인허가사업장Records => Set<공개인허가사업장Record>();
    public DbSet<공개사업장건축물Assignment> 공개사업장건축물Assignments => Set<공개사업장건축물Assignment>();
    public DbSet<건축물공개사업장Aggregate> 건축물공개사업장Aggregates => Set<건축물공개사업장Aggregate>();
    public DbSet<지역사업장표시Claim> 지역사업장표시Claims => Set<지역사업장표시Claim>();
    public DbSet<지역디오라마후원Campaign> 지역디오라마후원Campaigns => Set<지역디오라마후원Campaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new 외부데이터수집RunConfiguration());
        modelBuilder.ApplyConfiguration(new 외부데이터RawSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new 외부데이터정규화RecordConfiguration());
        modelBuilder.ApplyConfiguration(new 외부지역CodeMappingConfiguration());
        modelBuilder.ApplyConfiguration(new 건축물용도CategoryDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new 건축물대장표제부RecordConfiguration());
        modelBuilder.ApplyConfiguration(new 건축물행정구역AssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new 건축물용도CategoryAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new 행정동건축물CategoryAggregateConfiguration());
        modelBuilder.ApplyConfiguration(new 건축물형태ProfileConfiguration());
        modelBuilder.ApplyConfiguration(new 건축물시각구성계획Configuration());
        modelBuilder.ApplyConfiguration(new 공개인허가사업장RecordConfiguration());
        modelBuilder.ApplyConfiguration(new 공개사업장건축물AssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new 건축물공개사업장AggregateConfiguration());
        modelBuilder.ApplyConfiguration(new 지역사업장표시ClaimConfiguration());
        modelBuilder.ApplyConfiguration(new 지역디오라마후원CampaignConfiguration());
    }
}
