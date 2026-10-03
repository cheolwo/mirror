using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySqlConnector;

internal sealed class CargoJourneySettings
{
    internal const string DatabaseName = "ssalddel_cargo_journey";
    internal const int HostPort = 5322;
    internal const string BaseUrl = "http://127.0.0.1:5322";
    internal const string MongoConnection = "mongodb://127.0.0.1:27019";
    internal required string RepositoryRoot { get; init; }
    internal required string ArtifactRoot { get; init; }
    internal required string AccountPassword { get; init; }
    internal required string AccessKey { get; init; }
    internal required string RunStableId { get; init; }
    internal required IReadOnlyDictionary<string, string?> Configuration { get; init; }

    internal static CargoJourneySettings Load()
    {
        var root = FindRepositoryRoot();
        var artifactRoot = Path.Combine(root, "artifacts", "local", "cargo-warehouse-journey-r1");
        Directory.CreateDirectory(artifactRoot);
        Directory.CreateDirectory(Path.Combine(artifactRoot, "host-content"));
        var accountPassword = RequireSecret("CARGO_JOURNEY_ACCOUNT_PASSWORD", 16);
        var accessKey = RequireSecret("CARGO_JOURNEY_ACCESS_KEY", 32);
        var sql = new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1", Port = 13308,
            Database = DatabaseName, UserID = "cargo_journey",
            Password = RequireSecret("CARGO_JOURNEY_MYSQL_PASSWORD", 16),
            AllowUserVariables = true, CharacterSet = "utf8mb4"
        };
        var aesKey = RequireSecret("CARGO_JOURNEY_AES_KEY", 32);
        if (Convert.FromBase64String(aesKey).Length != 32)
            throw new InvalidOperationException("CargoJourneyAesKeyInvalid");
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = sql.ConnectionString,
            ["MongoDb:ConnectionString"] = MongoConnection,
            ["MongoDb:Database"] = DatabaseName,
            ["TransientState:Provider"] = "Memory",
            ["SsalddelExecution:Mode"] = "Simulation",
            ["SsalddelExecution:DevelopmentReadOnly"] = "false",
            ["DatabaseInitialization:RunAtStartup"] = "false",
            ["FoodObserverVerification:Enabled"] = "false",
            ["ObservableOperationsVerification:Enabled"] = "false",
            ["MobileFieldTest:Enabled"] = "false",
            ["VersionFeatureFlags:CommunityTrustWorkflow"] = "true",
            ["VersionFeatureFlags:DomesticTransportWorkflow"] = "true",
            ["VersionFeatureFlags:WarehouseFulfillmentWorkflow"] = "true",
            ["VersionFeatureFlags:OperationalWorldObservationWorkflow"] = "false",
            ["VersionFeatureFlags:FoodDeliveryWorkflow"] = "false",
            ["AgriculturalFisheriesBatch:Enabled"] = "false",
            ["CommunityPostEmailNotifications:Enabled"] = "false",
            ["CommunityPostTranslation:Enabled"] = "false",
            ["Jwt:Issuer"] = "CargoJourney.Local",
            ["Jwt:Audience"] = "CargoJourney.Local",
            ["Jwt:SecretKey"] = RequireSecret("CARGO_JOURNEY_JWT_SECRET", 32),
            ["IsmsPProtectedData:Aes256GcmKeyBase64"] = aesKey,
            ["IsmsPProtectedData:HashSalt"] = RequireSecret("CARGO_JOURNEY_HASH_SALT", 32),
            ["IsmsPProtectedData:FailWhenKeyMissing"] = "true",
            ["PersonalDataProtection:RequireCertificate"] = "false",
            ["PersonalDataProtection:ApplicationName"] = "Ssalddel.CargoJourneyVerification",
            ["PersonalDataProtection:KeyRingPath"] = Path.Combine(artifactRoot, "host-content", "key-ring"),
            ["Logging:LogLevel:Default"] = "Critical",
            ["Serilog:MinimumLevel:Default"] = "Fatal",
            ["SsalddelLogging:FilePath"] = Path.Combine(artifactRoot, "host.log")
        };
        var runStableId = "cargo-journey:" + Guid.NewGuid().ToString("N");
        var previousReady = Path.Combine(artifactRoot, "ready.json");
        if (File.Exists(previousReady))
        {
            using var previous = System.Text.Json.JsonDocument.Parse(File.ReadAllText(previousReady));
            if (previous.RootElement.TryGetProperty("runStableId", out var id) && id.GetString() is { Length: > 0 } previousId)
                runStableId = previousId;
        }
        return new CargoJourneySettings
        {
            RepositoryRoot = root, ArtifactRoot = artifactRoot,
            AccountPassword = accountPassword, AccessKey = accessKey,
            RunStableId = runStableId, Configuration = configuration
        };
    }

    internal void ApplyProcessOverrides()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", BaseUrl);
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_SSALDDEL", Path.Combine(ArtifactRoot, "host-content"));
        foreach (var item in Configuration)
            Environment.SetEnvironmentVariable(item.Key.Replace(":", "__"), item.Value);
    }

    internal void ValidateEffectiveConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment()
            || configuration["SsalddelExecution:Mode"] != "Simulation"
            || configuration.GetValue<bool>("DatabaseInitialization:RunAtStartup")
            || configuration["MongoDb:ConnectionString"] != MongoConnection
            || configuration["MongoDb:Database"] != DatabaseName
            || !configuration.GetValue<bool>("VersionFeatureFlags:DomesticTransportWorkflow")
            || !configuration.GetValue<bool>("VersionFeatureFlags:WarehouseFulfillmentWorkflow"))
            throw new InvalidOperationException("CargoJourneyHostBoundaryMismatch");
        ValidateSqlConnection(configuration.GetConnectionString("DefaultConnection") ?? "");
    }

    internal void ValidateSqlConnection(string connection)
    {
        var sql = new MySqlConnectionStringBuilder(connection);
        if (sql.Server != "127.0.0.1" || sql.Port != 13308 || sql.Database != DatabaseName || sql.UserID != "cargo_journey")
            throw new InvalidOperationException("CargoJourneyDatabaseBoundaryMismatch");
    }

    private static string RequireSecret(string name, int minimumLength)
        => Environment.GetEnvironmentVariable(name) is { } value && value.Length >= minimumLength
            ? value : throw new InvalidOperationException($"CargoJourneyEnvironmentMissing:{name}");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel", "Ssalddel.csproj"))) return directory.FullName;
        throw new InvalidOperationException("CargoJourneyRepositoryRootMissing");
    }
}
