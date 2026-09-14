using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;

namespace Ssalddel.Tests.Services.DeliveryZones;

public sealed class 배달운영권역LocalMySqlIntegrationTests
{
    [Fact]
    [Trait("Category", "LocalMySqlIntegration")]
    public async Task 공식_30개_후보를_읽고_Draft를_저장한_뒤_새_Context에서_재조회한다()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "SSALDDEL_DELIVERY_TERRITORY_MYSQL_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var publicDataConnectionString = Environment.GetEnvironmentVariable(
            "SSALDDEL_DELIVERY_TERRITORY_PUBLIC_DATA_MYSQL_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(publicDataConnectionString))
            publicDataConnectionString = connectionString;

        var publicOptions = new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
            .UseMySql(publicDataConnectionString, new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        await using var publicData = new PublicDataIngestionDbContext(publicOptions);
        var officialSource = new Official행정동배달운영권역Source(publicData);
        var candidates = await officialSource.조회Async(
            배달운영권역SourceScopes.NortheastSeoulRiderR1);
        Assert.Equal(30, candidates.Count);

        var mainConnectionBuilder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true,
            UseAffectedRows = false
        };
        await using var connection = new MySqlConnection(mainConnectionBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var mainOptions = new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        var suffix = Guid.NewGuid().ToString("N");
        var stableId = $"delivery-territory:kr:validation:{suffix}";

        await using (var write = new SsalddelContext(mainOptions, new DummyEncryption()))
        {
            write.Database.UseTransaction(transaction);
            var service = new 배달운영권역관리Service(
                write,
                officialSource,
                new EmptyDioramaStore());
            var result = await service.Draft생성Async(
                new 배달운영권역Draft생성Request
                {
                    DeliveryTerritoryStableId = stableId,
                    DisplayName = "로컬 MySQL 검증 권역",
                    AdministrativeAreaStableIds =
                    [
                        "region:kr:hjd:1126057000",
                        "region:kr:hjd:1126057500"
                    ],
                    ClientRequestId = $"local-mysql-{suffix}"
                },
                "user:local-mysql-validation");
            Assert.Equal(1, result.Revision);
        }

        await using (var readback = new SsalddelContext(mainOptions, new DummyEncryption()))
        {
            readback.Database.UseTransaction(transaction);
            var actual = await readback.배달운영권역
                .AsNoTracking()
                .Include(x => x.행정동Memberships)
                .SingleAsync(x => x.권역고유식별자 == stableId);
            Assert.Equal(2, actual.행정동Memberships.Count);
            Assert.All(actual.행정동Memberships, item =>
                Assert.Equal(배달운영권역행정동상태Codes.Included, item.상태Code));
            Assert.Equal(1, await readback.배달운영권역CommandReceipts
                .CountAsync(x => x.배달운영권역고유식별자 == stableId));
            Assert.Equal(1, await readback.배달운영권역변경Outbox
                .CountAsync(x => x.AggregateStableId == stableId));
            Assert.Equal(
                "user:local-mysql-validation",
                await readback.배달운영권역CommandReceipts
                    .Where(x => x.배달운영권역고유식별자 == stableId)
                    .Select(x => x.ActorUserStableId)
                    .SingleAsync());
        }

        await transaction.RollbackAsync();
    }

    private sealed class EmptyDioramaStore : I행정동디오라마ProjectionStore
    {
        public Task PublishAsync(
            행정동디오라마ProjectionBuildResult projection,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AdministrativeDongDioramaManifest?> FindManifestAsync(
            string administrativeAreaStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDioramaManifest?>(null);

        public Task<AdministrativeDongDioramaTile?> FindTileAsync(
            string administrativeAreaStableId,
            string tileStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDioramaTile?>(null);

        public Task<AdministrativeDongDisplayOverlayResponse?> FindDisplayOverlaysAsync(
            string administrativeAreaStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDisplayOverlayResponse?>(null);
    }

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
