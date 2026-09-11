using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Persistence;

public sealed class SimulationSession저장자료Entity
{
    public long Id { get; set; }
    public string SaveStableId { get; set; } = string.Empty;
    public string SessionStableId { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public int SavedWorldTick { get; set; }
    public long SavedWorldRevision { get; set; }
    public string ReplayHashAlgorithmCode { get; set; } = string.Empty;
    public string ReplayHash { get; set; } = string.Empty;
    public int CommandCount { get; set; }
    public string PackageJson { get; set; } = string.Empty;
    public DateTimeOffset StoredAtUtc { get; set; }
}

public sealed class SimulationSessionDbContext(
    DbContextOptions<SimulationSessionDbContext> options) : DbContext(options)
{
    public DbSet<SimulationSession저장자료Entity> SessionSaves =>
        Set<SimulationSession저장자료Entity>();
    public DbSet<SimulationOnlineWorld상태사본Entity> OnlineWorldCheckpoints =>
        Set<SimulationOnlineWorld상태사본Entity>();
    public DbSet<SimulationSession접근원장Entity> SessionAccessLedgers =>
        Set<SimulationSession접근원장Entity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SimulationSession저장자료Configuration());
        modelBuilder.ApplyConfiguration(
            new SimulationOnlineWorld상태사본Configuration());
        modelBuilder.ApplyConfiguration(
            new SimulationSession접근원장Configuration());
    }
}

public sealed class SimulationSession접근원장Entity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string AccessRoleCode { get; set; } = "Owner";
    public long Revision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class SimulationSession접근원장Configuration
    : IEntityTypeConfiguration<SimulationSession접근원장Entity>
{
    public void Configure(
        EntityTypeBuilder<SimulationSession접근원장Entity> builder)
    {
        builder.ToTable("시뮬레이션세션_접근원장");
        builder.HasKey(value => value.SessionStableId);
        builder.HasIndex(value => new { value.SubjectId, value.SessionStableId })
            .IsUnique();
        builder.Property(value => value.SessionStableId)
            .HasColumnName("세션고유식별자").HasMaxLength(200).IsRequired();
        builder.Property(value => value.SubjectId)
            .HasColumnName("로그인주체식별자").HasMaxLength(450).IsRequired();
        builder.Property(value => value.AccessRoleCode)
            .HasColumnName("접근역할코드").HasMaxLength(30).IsRequired();
        builder.Property(value => value.Revision)
            .HasColumnName("개정번호");
        builder.Property(value => value.CreatedAtUtc)
            .HasColumnName("생성시각UTC").IsRequired();
        builder.Property(value => value.UpdatedAtUtc)
            .HasColumnName("수정시각UTC").IsRequired();
    }
}

public sealed class SimulationOnlineWorld상태사본Entity
{
    public string CheckpointStableId { get; set; } = string.Empty;
    public string SchemaCode { get; set; } = string.Empty;
    public long DirectoryRevision { get; set; }
    public string CheckpointHashSha256 { get; set; } = string.Empty;
    public string CheckpointJson { get; set; } = string.Empty;
    public DateTimeOffset StoredAtUtc { get; set; }
}

internal sealed class SimulationOnlineWorld상태사본Configuration
    : IEntityTypeConfiguration<SimulationOnlineWorld상태사본Entity>
{
    public void Configure(
        EntityTypeBuilder<SimulationOnlineWorld상태사본Entity> builder)
    {
        builder.ToTable("시뮬레이션온라인세계_상태사본");
        builder.HasKey(value => value.CheckpointStableId);
        builder.Property(value => value.CheckpointStableId)
            .HasColumnName("상태사본고유식별자").HasMaxLength(100).IsRequired();
        builder.Property(value => value.SchemaCode)
            .HasColumnName("스키마코드").HasMaxLength(100).IsRequired();
        builder.Property(value => value.DirectoryRevision)
            .HasColumnName("세계Directory개정번호");
        builder.Property(value => value.CheckpointHashSha256)
            .HasColumnName("상태사본SHA256").HasMaxLength(64).IsRequired();
        builder.Property(value => value.CheckpointJson)
            .HasColumnName("상태사본JSON").HasColumnType("longtext").IsRequired();
        builder.Property(value => value.StoredAtUtc)
            .HasColumnName("저장시각UTC").IsRequired();
    }
}

internal sealed class SimulationSession저장자료Configuration
    : IEntityTypeConfiguration<SimulationSession저장자료Entity>
{
    public void Configure(EntityTypeBuilder<SimulationSession저장자료Entity> builder)
    {
        builder.ToTable("시뮬레이션세션_저장자료");
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.SaveStableId).IsUnique();
        builder.HasIndex(value => new
        {
            value.SessionStableId,
            value.SavedWorldRevision,
        });
        builder.Property(value => value.Id).HasColumnName("식별번호");
        builder.Property(value => value.SaveStableId)
            .HasColumnName("저장자료고유식별자").HasMaxLength(200).IsRequired();
        builder.Property(value => value.SessionStableId)
            .HasColumnName("세션고유식별자").HasMaxLength(200).IsRequired();
        builder.Property(value => value.SchemaVersion)
            .HasColumnName("스키마버전").HasMaxLength(60).IsRequired();
        builder.Property(value => value.SavedWorldTick)
            .HasColumnName("저장WorldTick");
        builder.Property(value => value.SavedWorldRevision)
            .HasColumnName("저장World개정번호");
        builder.Property(value => value.ReplayHashAlgorithmCode)
            .HasColumnName("재생Hash알고리즘코드").HasMaxLength(40).IsRequired();
        builder.Property(value => value.ReplayHash)
            .HasColumnName("재생SHA256").HasMaxLength(64).IsRequired();
        builder.Property(value => value.CommandCount)
            .HasColumnName("명령기록수");
        builder.Property(value => value.PackageJson)
            .HasColumnName("저장자료JSON").HasColumnType("longtext").IsRequired();
        builder.Property(value => value.StoredAtUtc)
            .HasColumnName("저장시각UTC").IsRequired();
    }
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationSaveReplay,
    SsalddelCodeLayer.Infrastructure,
    "검증된 세션 저장 자료 JSON과 재생 hash를 Simulation 전용 DB에 보관한다.",
    StepKey = "infrastructure.save-store",
    DependsOnStepKeys = new string[] { "domain.save-package" },
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    ReadsFrom = SsalddelCodeDataScope.SimulationState,
    WritesTo = SsalddelCodeDataScope.SimulationState,
    FlowOrder = 50,
    Boundary = "공유 공공데이터 DB가 아니라 별도 SimulationSession DB만 읽고 쓴다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "결정성·Save/Replay 또는 회귀 검증 책임을 제공한다.",
    Boundary = "저장 구현 존재만으로 상위 E 증거를 승격하지 않는다.")]
public sealed class SimulationSessionSaveStore(
    IDbContextFactory<SimulationSessionDbContext> dbContextFactory)
    : ISimulationSessionSaveStore
{
    public const string CorruptedCode = "SimulationSessionSavePersistenceCorrupted";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public SimulationSessionSavePackage SaveOrGet(
        SimulationSessionSavePackage package)
    {
        if (package == null) throw new ArgumentNullException(nameof(package));
        var candidate = SimulationSaveReplayCloner.ClonePackage(package);
        SimulationSessionReplay.Restore(candidate);

        using var db = dbContextFactory.CreateDbContext();
        var existing = db.SessionSaves.AsNoTracking().SingleOrDefault(value =>
            value.SaveStableId == candidate.SaveStableId);
        if (existing != null)
            return ExistingOrConflict(existing, candidate);

        db.SessionSaves.Add(new SimulationSession저장자료Entity
        {
            SaveStableId = candidate.SaveStableId,
            SessionStableId = candidate.SessionStableId,
            SchemaVersion = candidate.SchemaVersion,
            SavedWorldTick = candidate.SavedWorldTick,
            SavedWorldRevision = candidate.SavedWorldRevision,
            ReplayHashAlgorithmCode = candidate.ReplayHashAlgorithmCode,
            ReplayHash = candidate.ReplayHash,
            CommandCount = candidate.CommandLog.Length,
            PackageJson = JsonSerializer.Serialize(candidate, JsonOptions),
            StoredAtUtc = DateTimeOffset.UtcNow,
        });
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            existing = db.SessionSaves.AsNoTracking().SingleOrDefault(value =>
                value.SaveStableId == candidate.SaveStableId);
            if (existing == null) throw;
            return ExistingOrConflict(existing, candidate);
        }

        return SimulationSaveReplayCloner.ClonePackage(candidate);
    }

    public SimulationSessionSavePackage? Find(string saveStableId)
    {
        if (string.IsNullOrWhiteSpace(saveStableId)) return null;
        using var db = dbContextFactory.CreateDbContext();
        var entity = db.SessionSaves.AsNoTracking().SingleOrDefault(value =>
            value.SaveStableId == saveStableId.Trim());
        return entity == null ? null : DeserializeAndValidate(entity);
    }

    private static SimulationSessionSavePackage ExistingOrConflict(
        SimulationSession저장자료Entity existing,
        SimulationSessionSavePackage candidate)
    {
        if (!string.Equals(existing.SessionStableId, candidate.SessionStableId,
                StringComparison.Ordinal)
            || !string.Equals(existing.ReplayHash, candidate.ReplayHash,
                StringComparison.Ordinal))
        {
            throw new SimulationConflictException(
                "SimulationSaveStableIdConflict");
        }

        return DeserializeAndValidate(existing);
    }

    private static SimulationSessionSavePackage DeserializeAndValidate(
        SimulationSession저장자료Entity entity)
    {
        SimulationSessionSavePackage package;
        try
        {
            package = JsonSerializer.Deserialize<SimulationSessionSavePackage>(
                entity.PackageJson, JsonOptions)
                ?? throw new InvalidOperationException(CorruptedCode);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException(CorruptedCode, error);
        }

        if (!string.Equals(entity.SaveStableId, package.SaveStableId,
                StringComparison.Ordinal)
            || !string.Equals(entity.SessionStableId, package.SessionStableId,
                StringComparison.Ordinal)
            || !string.Equals(entity.SchemaVersion, package.SchemaVersion,
                StringComparison.Ordinal)
            || entity.SavedWorldTick != package.SavedWorldTick
            || entity.SavedWorldRevision != package.SavedWorldRevision
            || !string.Equals(entity.ReplayHashAlgorithmCode,
                package.ReplayHashAlgorithmCode, StringComparison.Ordinal)
            || !string.Equals(entity.ReplayHash, package.ReplayHash,
                StringComparison.Ordinal)
            || entity.CommandCount != package.CommandLog.Length)
        {
            throw new InvalidOperationException(CorruptedCode);
        }

        try
        {
            SimulationSessionReplay.Restore(package);
        }
        catch (Exception error) when (error is SimulationContractException
            || error is SimulationConflictException
            || error is ArgumentException
            || error is InvalidOperationException)
        {
            throw new InvalidOperationException(CorruptedCode, error);
        }
        return SimulationSaveReplayCloner.ClonePackage(package);
    }
}

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E2,
    "Simulation Session DB에 온라인 세계 상태 사본을 저장·복원하는 Adapter를 제공한다.",
    Boundary = "DB 코드와 EF 시험은 실제 migration 적용·재기동 또는 운영 배포 증거가 아니다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
public sealed class SimulationOnlineWorldCheckpointStore(
    IDbContextFactory<SimulationSessionDbContext> dbContextFactory)
    : ISimulationOnlineWorldCheckpointStore
{
    private const string StableId = "online-world-checkpoint:current";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public SimulationOnlineWorldCheckpointSnapshot? Find()
    {
        using var db = dbContextFactory.CreateDbContext();
        var entity = db.OnlineWorldCheckpoints.AsNoTracking().SingleOrDefault(
            value => value.CheckpointStableId == StableId);
        if (entity == null) return null;
        SimulationOnlineWorldCheckpointSnapshot checkpoint;
        try
        {
            checkpoint = JsonSerializer.Deserialize<
                SimulationOnlineWorldCheckpointSnapshot>(entity.CheckpointJson,
                JsonOptions) ?? throw new InvalidOperationException(
                    "SimulationOnlineWorldCheckpointCorrupted");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException(
                "SimulationOnlineWorldCheckpointCorrupted", error);
        }
        if (entity.SchemaCode != checkpoint.SchemaCode
            || entity.DirectoryRevision != checkpoint.DirectoryRevision
            || entity.CheckpointHashSha256 != checkpoint.CheckpointHashSha256
            || checkpoint.CheckpointHashSha256 !=
                SimulationOnlineWorldCoordinator.CalculateCheckpointHash(
                    checkpoint))
            throw new InvalidOperationException(
                "SimulationOnlineWorldCheckpointCorrupted");
        return checkpoint;
    }

    public void Save(SimulationOnlineWorldCheckpointSnapshot checkpoint)
    {
        if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
        if (checkpoint.CheckpointHashSha256 !=
            SimulationOnlineWorldCoordinator.CalculateCheckpointHash(checkpoint))
            throw new SimulationConflictException(
                "SimulationOnlineWorldCheckpointInvalid");
        using var db = dbContextFactory.CreateDbContext();
        var entity = db.OnlineWorldCheckpoints.SingleOrDefault(value =>
            value.CheckpointStableId == StableId);
        if (entity == null)
        {
            entity = new SimulationOnlineWorld상태사본Entity
            {
                CheckpointStableId = StableId,
            };
            db.OnlineWorldCheckpoints.Add(entity);
        }
        else if (checkpoint.DirectoryRevision < entity.DirectoryRevision)
        {
            throw new SimulationConflictException(
                "SimulationOnlineWorldCheckpointRevisionRegressed");
        }

        entity.SchemaCode = checkpoint.SchemaCode;
        entity.DirectoryRevision = checkpoint.DirectoryRevision;
        entity.CheckpointHashSha256 = checkpoint.CheckpointHashSha256;
        entity.CheckpointJson = JsonSerializer.Serialize(checkpoint, JsonOptions);
        entity.StoredAtUtc = DateTimeOffset.UtcNow;
        db.SaveChanges();
    }
}

internal sealed class SimulationSessionDatabaseReadinessProbe(
    IDbContextFactory<SimulationSessionDbContext> dbContextFactory)
    : ISimulationDatabaseReadinessProbe
{
    public string 데이터베이스이름 => "Simulation Session DB";

    public async Task<bool> 연결가능Async(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        return await db.Database.CanConnectAsync(cancellationToken);
    }
}

public static class SimulationSessionPersistenceRegistration
{
    public static IServiceCollection AddSimulationSessionPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddPooledDbContextFactory<SimulationSessionDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 4, 0)),
                mysql =>
                {
                    mysql.MigrationsAssembly("Ssalddel.Simulation.Persistence");
                    mysql.MigrationsHistoryTable(
                        "__EF마이그레이션이력_시뮬레이션세션");
                }));
        services.AddSingleton<ISimulationSessionSaveStore,
            SimulationSessionSaveStore>();
        services.AddSingleton<ISimulationOnlineWorldCheckpointStore,
            SimulationOnlineWorldCheckpointStore>();
        services.AddSingleton<ISimulationDatabaseReadinessProbe,
            SimulationSessionDatabaseReadinessProbe>();
        return services;
    }
}
