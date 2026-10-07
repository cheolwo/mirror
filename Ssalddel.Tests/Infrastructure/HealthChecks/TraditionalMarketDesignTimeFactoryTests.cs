using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MySqlConnector;
using Ssalddel.Infrastructure.Persistence.TraditionalMarkets;

namespace Ssalddel.Tests.Infrastructure.HealthChecks;

[CollectionDefinition("TraditionalMarketFactoryEnvironment", DisableParallelization = true)]
public sealed class TraditionalMarketFactoryEnvironmentCollection { }

[Collection("TraditionalMarketFactoryEnvironment")]
public sealed class TraditionalMarketDesignTimeFactoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FactoryUsesTheRuntimeDatabaseHistoryWithoutBootingOrConnectingTheWebHost(bool configured)
    {
        const string variable = "ConnectionStrings__DefaultConnection";
        const string connection = "Server=127.0.0.1;Port=1;Database=traditional_factory_fixture;User=fixture;Password=fixture";
        var previousConnection = Environment.GetEnvironmentVariable(variable);
        var previousDirectory = Directory.GetCurrentDirectory();
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "traditional-factory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            Directory.SetCurrentDirectory(temporaryDirectory);
            Environment.SetEnvironmentVariable(variable, configured ? connection : null);
            var factory = new TraditionalMarketDbContextDesignTimeFactory();
            if (!configured)
            {
                Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
                return;
            }
            using var context = factory.CreateDbContext([]);
            var expectedConnection = new MySqlConnectionStringBuilder(connection);
            var actualConnection = new MySqlConnectionStringBuilder(context.Database.GetConnectionString());
            Assert.Equal(expectedConnection.Server, actualConnection.Server);
            Assert.Equal(expectedConnection.Port, actualConnection.Port);
            Assert.Equal(expectedConnection.Database, actualConnection.Database);
            Assert.Equal(expectedConnection.UserID, actualConnection.UserID);
            Assert.Equal(expectedConnection.Password, actualConnection.Password);
            // Pomelo enables user variables and disables affected-row counting.
            // Every caller-supplied option must survive, and only these two known
            // provider additions may appear; textual key order is irrelevant.
            foreach (string key in expectedConnection.Keys)
                Assert.Equal(expectedConnection[key], actualConnection[key]);
            Assert.True(actualConnection.AllowUserVariables);
            Assert.False(actualConnection.UseAffectedRows);
            expectedConnection.AllowUserVariables = true;
            expectedConnection.UseAffectedRows = false;
            Assert.Equal(expectedConnection.Keys.Count, actualConnection.Keys.Count);
            foreach (string key in expectedConnection.Keys)
                Assert.Equal(expectedConnection[key], actualConnection[key]);
            var relational = Assert.Single(context.GetService<IDbContextOptions>().Extensions.OfType<RelationalOptionsExtension>());
            Assert.Equal("Ssalddel.Infrastructure", relational.MigrationsAssembly);
            Assert.Equal("__EFMigrationsHistory_TraditionalMarkets", relational.MigrationsHistoryTableName);
            Assert.NotEmpty(context.Database.GetMigrations());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previousConnection);
            Directory.SetCurrentDirectory(previousDirectory);
            Directory.Delete(temporaryDirectory, recursive: false);
        }
    }
}
