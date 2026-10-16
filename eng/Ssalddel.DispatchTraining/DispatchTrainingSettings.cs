using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySqlConnector;

namespace Ssalddel.DispatchTraining;

internal sealed class DispatchTrainingSettings
{
    internal const int HostPort = 5324;
    internal const string BaseUrl = "http://127.0.0.1:5324";
    internal const string DatabaseName = "ssalddel_dispatch_training";
    internal const string MongoConnection = "mongodb://127.0.0.1:27023";
    internal required string RepositoryRoot { get; init; }
    internal required string ArtifactRoot { get; init; }
    internal required string AccountPassword { get; init; }
    internal required IReadOnlyDictionary<string, string?> Configuration { get; init; }

    internal static DispatchTrainingSettings Load()
    {
        var root = FindRoot();
        var artifacts = Path.Combine(root, "artifacts", "local", "dispatch-training-r58");
        Directory.CreateDirectory(Path.Combine(artifacts, "host-content"));
        var sql = new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1", Port = 13312, Database = DatabaseName, UserID = "dispatch_training",
            Password = Secret("MYSQL_PASSWORD"), AllowUserVariables = true, CharacterSet = "utf8mb4"
        };
        var config = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = sql.ConnectionString,
            ["MongoDb:ConnectionString"] = MongoConnection, ["MongoDb:Database"] = DatabaseName,
            ["TransientState:Provider"] = "Memory", ["SsalddelExecution:Mode"] = "Simulation",
            ["SsalddelExecution:DevelopmentReadOnly"] = "false", ["ObjectStorage:Provider"] = "Local",
            ["ObjectStorage:Local:RootPath"] = Path.Combine(artifacts, "photos"),
            ["DatabaseInitialization:RunAtStartup"] = "false", ["PersonalDataProtection:RequireCertificate"] = "false",
            ["PersonalDataProtection:KeyRingPath"] = Path.Combine(artifacts, "host-content", "key-ring"),
            ["Jwt:Issuer"] = "DispatchTraining.Local", ["Jwt:Audience"] = "DispatchTraining.Local",
            ["Jwt:SecretKey"] = Secret("JWT_SECRET"),
            ["IsmsPProtectedData:Aes256GcmKeyBase64"] = Secret("AES_KEY"),
            ["IsmsPProtectedData:HashSalt"] = Secret("HASH_SALT"), ["IsmsPProtectedData:FailWhenKeyMissing"] = "true",
            ["SharedDriverWork:Enabled"] = "true", ["CargoCompletionRecovery:Enabled"] = "false",
            ["VersionFeatureFlags:CommunityTrustWorkflow"] = "true",
            ["VersionFeatureFlags:DomesticTransportWorkflow"] = "true",
            ["VersionFeatureFlags:OperationalDispatchCore"] = "true",
            ["VersionFeatureFlags:WarehouseFulfillmentWorkflow"] = "false",
            ["VersionFeatureFlags:FoodDeliveryWorkflow"] = "false",
            ["VersionFeatureFlags:OperationalWorldObservationWorkflow"] = "false",
            ["FoodDispatchOllama:Enabled"] = "false", ["MobileFieldTest:Enabled"] = "false",
            ["ObservableOperationsVerification:Enabled"] = "false", ["FoodObserverVerification:Enabled"] = "false",
            ["AgriculturalFisheriesBatch:Enabled"] = "false", ["CommunityPostEmailNotifications:Enabled"] = "false",
            ["Logging:LogLevel:Default"] = "Critical", ["Serilog:MinimumLevel:Default"] = "Fatal",
            ["SsalddelLogging:FilePath"] = Path.Combine(artifacts, "host.log")
        };
        return new() { RepositoryRoot = root, ArtifactRoot = artifacts, AccountPassword = Secret("ACCOUNT_PASSWORD"), Configuration = config };
    }

    internal void Apply()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", BaseUrl);
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_SSALDDEL", Path.Combine(ArtifactRoot, "host-content"));
        foreach (var (key, value) in Configuration) Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);
    }

    internal void Validate(IConfiguration config, IHostEnvironment environment)
    {
        var sql = new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection") ?? "");
        if (!environment.IsDevelopment() || config["SsalddelExecution:Mode"] != "Simulation"
            || sql.Server != "127.0.0.1" || sql.Port != 13312 || sql.Database != DatabaseName || sql.UserID != "dispatch_training"
            || config["MongoDb:ConnectionString"] != MongoConnection || config["MongoDb:Database"] != DatabaseName
            || config["ObjectStorage:Provider"] != "Local" || config.GetValue<bool>("DatabaseInitialization:RunAtStartup"))
            throw new InvalidOperationException("DispatchTrainingBoundaryMismatch");
    }
    private static string Secret(string suffix) => Environment.GetEnvironmentVariable("DISPATCH_TRAINING_" + suffix) is { Length: >= 16 } value
        ? value : throw new InvalidOperationException("DispatchTrainingEnvironmentMissing:" + suffix);
    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(Environment.CurrentDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Ssalddel", "Ssalddel.csproj"))) return d.FullName;
        throw new InvalidOperationException("DispatchTrainingRepositoryRootMissing");
    }
}
