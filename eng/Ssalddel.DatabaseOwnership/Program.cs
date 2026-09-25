using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Infrastructure.Persistence.TraditionalMarkets;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

var outputPath = ReadOutputPath(args);
var serverVersion = new MySqlServerVersion(new Version(8, 4, 0));
const string modelConnection =
    "Server=localhost;Database=ssalddel_table_ownership_model;User=root;Password=not-used;";

using var main = new SsalddelContext(
    new DbContextOptionsBuilder<SsalddelContext>()
        .UseMySql(modelConnection, serverVersion)
        .Options,
    new PassThroughPersonalDataEncryptionService());
using var agriculturalFisheries = new AgriculturalFisheriesDbContext(
    new DbContextOptionsBuilder<AgriculturalFisheriesDbContext>()
        .UseMySql(modelConnection, serverVersion)
        .Options);
using var publicData = new PublicDataIngestionDbContext(
    new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
        .UseMySql(modelConnection, serverVersion)
        .Options);
using var traditionalMarkets = new TraditionalMarketDbContext(
    new DbContextOptionsBuilder<TraditionalMarketDbContext>()
        .UseMySql(modelConnection, serverVersion)
        .Options);

var contexts = new[]
{
    Describe(
        nameof(SsalddelContext),
        "Identity·커뮤니티·업무 실행·안정 투영",
        "__EFMigrationsHistory",
        main),
    Describe(
        nameof(AgriculturalFisheriesDbContext),
        "농수산·식품 공공자료 수집 archive",
        "__EFMigrationsHistory_AgriculturalFisheries",
        agriculturalFisheries),
    Describe(
        nameof(PublicDataIngestionDbContext),
        "범용 공공자료 수집·정규화·지역/건축물 근거",
        "__EFMigrationsHistory_PublicDataIngestion",
        publicData),
    Describe(
        nameof(TraditionalMarketDbContext),
        "전통시장 원본·생활권 협의·거점",
        "__EFMigrationsHistory_TraditionalMarkets",
        traditionalMarkets),
};

var duplicateTables = contexts
    .SelectMany(context => context.Tables.Select(table => new { context.Context, table.Table }))
    .GroupBy(item => item.Table, StringComparer.OrdinalIgnoreCase)
    .Where(group => group.Select(item => item.Context).Distinct(StringComparer.Ordinal).Count() > 1)
    .Select(group => new
    {
        table = group.Key,
        contexts = group.Select(item => item.Context).Distinct(StringComparer.Ordinal).Order().ToArray(),
    })
    .OrderBy(item => item.table, StringComparer.Ordinal)
    .ToArray();

var ledger = new
{
    schemaVersion = 1,
    authority = "EF Core runtime model",
    physicalDatabasePolicy = "하나의 MySQL 데이터베이스, Context별 독립 migration history",
    deletionOrMergeIncluded = false,
    contextCount = contexts.Length,
    modeledTableCount = contexts.Sum(context => context.TableCount),
    migrationHistoryTableCount = contexts.Length,
    physicalTableCountExpected = contexts.Sum(context => context.TableCount) + contexts.Length,
    contexts,
    duplicateTables,
};

var fullOutputPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
await File.WriteAllTextAsync(
    fullOutputPath,
    JsonSerializer.Serialize(
        ledger,
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }) + Environment.NewLine);

Console.WriteLine(
    $"테이블 소유권 대장 생성 완료: {fullOutputPath} " +
    $"(모델 테이블 {ledger.modeledTableCount}, 이력 테이블 {ledger.migrationHistoryTableCount}, 중복 {duplicateTables.Length})");

static string ReadOutputPath(string[] arguments)
{
    for (var index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], "--output", StringComparison.OrdinalIgnoreCase))
            return arguments[index + 1];
    }

    return Path.Combine("eng", "execution-ledgers", "relational-table-ownership.json");
}

static ContextOwnership Describe(
    string context,
    string responsibility,
    string migrationHistoryTable,
    DbContext dbContext)
{
    var tables = dbContext.Model.GetEntityTypes()
        .Where(entityType => entityType.GetTableName() is not null)
        .GroupBy(
            entityType => string.IsNullOrWhiteSpace(entityType.GetSchema())
                ? entityType.GetTableName()!
                : $"{entityType.GetSchema()}.{entityType.GetTableName()}",
            StringComparer.OrdinalIgnoreCase)
        .Select(group => new TableOwnership(
            group.Key,
            group.Select(entityType => entityType.ClrType.FullName ?? entityType.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()))
        .OrderBy(table => table.Table, StringComparer.Ordinal)
        .ToArray();

    return new ContextOwnership(
        context,
        responsibility,
        migrationHistoryTable,
        tables.Length,
        tables);
}

internal sealed record ContextOwnership(
    string Context,
    string Responsibility,
    string MigrationHistoryTable,
    int TableCount,
    IReadOnlyList<TableOwnership> Tables);

internal sealed record TableOwnership(string Table, IReadOnlyList<string> EntityTypes);

internal sealed class PassThroughPersonalDataEncryptionService : IPersonalDataEncryptionService
{
    public string? Protect(string? value) => value;
    public string? Unprotect(string? value) => value;
}
