using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Ssalddel.Infrastructure.Persistence.TraditionalMarkets;

public sealed class TraditionalMarketDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<TraditionalMarketDbContext>
{
    public TraditionalMarketDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var webProjectDirectory = Directory.Exists(Path.Combine(currentDirectory, "Ssalddel"))
            ? Path.Combine(currentDirectory, "Ssalddel") : currentDirectory;
        var configuration = new ConfigurationBuilder().SetBasePath(webProjectDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables().Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required for design-time DbContext creation.");
        // Fixed version avoids probing a live database just to construct options.
        var options = new DbContextOptionsBuilder<TraditionalMarketDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)), mysql =>
            {
                mysql.MigrationsAssembly("Ssalddel.Infrastructure");
                mysql.MigrationsHistoryTable("__EFMigrationsHistory_TraditionalMarkets");
            }).Options;
        return new TraditionalMarketDbContext(options);
    }
}
