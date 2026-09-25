using Ssalddel.Domain.AgriculturalFisheries;
using Ssalddel.Domain.Community;
using Ssalddel.Domain.TraditionalMarkets;
using Ssalddel.Infrastructure.Persistence;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Infrastructure.Persistence.TraditionalMarkets;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class SsalddelContextModelIsolationTests
{
    [Fact]
    public void MainContext_ExcludesEntitiesOwnedByDedicatedContexts()
    {
        using var context = CreateMainContext();
        using var agriculturalFisheriesContext = CreateAgriculturalFisheriesContext();
        using var publicDataContext = CreatePublicDataContext();
        using var traditionalMarketContext = CreateTraditionalMarketContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(PlatformCommunityPost)));
        Assert.All(
            agriculturalFisheriesContext.Model.GetEntityTypes(),
            entityType => Assert.Null(context.Model.FindEntityType(entityType.ClrType)));
        Assert.All(
            publicDataContext.Model.GetEntityTypes(),
            entityType => Assert.Null(context.Model.FindEntityType(entityType.ClrType)));
        Assert.All(
            traditionalMarketContext.Model.GetEntityTypes(),
            entityType => Assert.Null(context.Model.FindEntityType(entityType.ClrType)));
    }

    [Fact]
    public void DedicatedContextConfigurations_DeclareTheirOwnership()
    {
        var dedicatedNamespaces = new[]
        {
            typeof(AgriculturalFisheriesDbContext).Namespace!,
            typeof(PublicDataIngestionDbContext).Namespace!,
            typeof(TraditionalMarketDbContext).Namespace!
        };
        var unmarkedConfigurations = typeof(AgriculturalFisheriesDbContext).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && dedicatedNamespaces.Any(namespaceName =>
                    string.Equals(type.Namespace, namespaceName, StringComparison.Ordinal)
                    || type.Namespace?.StartsWith(namespaceName + ".", StringComparison.Ordinal) == true)
                && type.GetInterfaces().Any(@interface =>
                    @interface.IsGenericType
                    && @interface.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .Where(type => !typeof(IDedicatedDbContextConfiguration).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unmarkedConfigurations);
    }

    [Fact]
    public void DbContexts_DoNotOwnTheSameRelationalTable()
    {
        using var mainContext = CreateMainContext();
        using var agriculturalFisheriesContext = CreateAgriculturalFisheriesContext();
        using var publicDataContext = CreatePublicDataContext();
        using var traditionalMarketContext = CreateTraditionalMarketContext();

        var owners = new[]
        {
            CreateTableOwners(nameof(SsalddelContext), mainContext),
            CreateTableOwners(nameof(AgriculturalFisheriesDbContext), agriculturalFisheriesContext),
            CreateTableOwners(nameof(PublicDataIngestionDbContext), publicDataContext),
            CreateTableOwners(nameof(TraditionalMarketDbContext), traditionalMarketContext)
        };
        var duplicateOwners = owners
            .SelectMany(items => items)
            .GroupBy(item => item.Table, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.Context).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => new
            {
                Table = group.Key,
                Contexts = group.Select(item => item.Context).Distinct(StringComparer.Ordinal).Order().ToArray()
            })
            .ToArray();

        Assert.Empty(duplicateOwners);
    }

    private static IReadOnlyList<(string Context, string Table)> CreateTableOwners(
        string contextName,
        DbContext context)
        => context.Model.GetEntityTypes()
            .Select(entityType => new
            {
                Schema = entityType.GetSchema(),
                Table = entityType.GetTableName()
            })
            .Where(item => item.Table is not null)
            .Select(item => (
                contextName,
                string.IsNullOrWhiteSpace(item.Schema)
                    ? item.Table!
                    : $"{item.Schema}.{item.Table}"))
            .Distinct()
            .ToArray();

    private static SsalddelContext CreateMainContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql(
                "Server=localhost;Database=ssalddel_model_boundary_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private static AgriculturalFisheriesDbContext CreateAgriculturalFisheriesContext()
    {
        var options = new DbContextOptionsBuilder<AgriculturalFisheriesDbContext>()
            .UseMySql(
                "Server=localhost;Database=ssalddel_model_boundary_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        return new AgriculturalFisheriesDbContext(options);
    }

    private static TraditionalMarketDbContext CreateTraditionalMarketContext()
    {
        var options = new DbContextOptionsBuilder<TraditionalMarketDbContext>()
            .UseMySql(
                "Server=localhost;Database=ssalddel_model_boundary_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        return new TraditionalMarketDbContext(options);
    }

    private static PublicDataIngestionDbContext CreatePublicDataContext()
    {
        var options = new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
            .UseMySql(
                "Server=localhost;Database=ssalddel_model_boundary_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        return new PublicDataIngestionDbContext(options);
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
