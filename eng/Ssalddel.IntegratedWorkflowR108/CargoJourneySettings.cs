using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySqlConnector;

// The name is the compatibility contract of the linked CargoJourney factory/seed.
// All endpoints and data are owned by this separate USB r67 artifact host.
internal sealed class CargoJourneySettings
{
    internal static readonly string DatabaseName = ReadDatabase();
    internal const string CurrentModelDatabasePrefix = "mirror_workflow_r108_";
    internal const string MigrationFirstDatabasePrefix = "r108m_";
    internal static string DatabaseGuid => DatabaseName[(DatabaseName.StartsWith(MigrationFirstDatabasePrefix, StringComparison.Ordinal)
        ? MigrationFirstDatabasePrefix.Length : CurrentModelDatabasePrefix.Length)..];
    internal const int HostPort = 5378;
    internal const int VerificationRecommendationTtlSeconds = 60;
    internal const string BaseUrl = "http://127.0.0.1:5378";
    internal const string MongoConnection = "mongodb://127.0.0.1:27021";
    internal required string RepositoryRoot { get; init; }
    internal required string ArtifactRoot { get; init; }
    internal required string AccountPassword { get; init; }
    internal required string AccessKey { get; init; }
    internal required string RunStableId { get; init; }
    internal required IReadOnlyDictionary<string, string?> Configuration { get; init; }

    internal static readonly string[] SecretEnvironmentNames =
    [
        "USB_R74_SQL_PASSWORD", "USB_R74_SQL_ROOT_PASSWORD", "USB_R74_ACCOUNT_PASSWORD",
        "USB_R74_JWT_SECRET", "USB_R74_AES_KEY", "USB_R74_HASH_SALT", "USB_R74_ACCESS_KEY"
    ];

    internal static CargoJourneySettings Load()
    {
        var root = FindRepositoryRoot();
        var artifactRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("R108_ARTIFACT_ROOT")
            ?? throw new InvalidOperationException("R108ArtifactRootRequired"));
        var allowed = Path.GetFullPath(Path.Combine(root, "artifacts", "local", "integrated-workflow-r108")) + Path.DirectorySeparatorChar;
        if (!artifactRoot.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("R108ArtifactRootOutsideOwnedArea");
        if (!string.Equals(artifactRoot, Path.GetFullPath(Path.Combine(allowed, DatabaseGuid)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("R108ExactGuidArtifactRootRequired");
        Directory.CreateDirectory(Path.Combine(artifactRoot, "host-content"));
        Directory.CreateDirectory(Path.Combine(artifactRoot, ".private"));
        var password = RequireSecret("USB_R74_ACCOUNT_PASSWORD", 16);
        var sql = new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1", Port = 13310, Database = DatabaseName, UserID = "usb_r74",
            Password = RequireSecret("USB_R74_SQL_PASSWORD", 16),
            AllowUserVariables = true, CharacterSet = "utf8mb4"
        };
        _ = RequireSecret("USB_R74_SQL_ROOT_PASSWORD", 16);
        var aesKey = RequireSecret("USB_R74_AES_KEY", 32);
        if (Convert.FromBase64String(aesKey).Length != 32)
            throw new InvalidOperationException("UsbR67AesKeyInvalid");
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = sql.ConnectionString,
            ["ConnectionStrings:SharedPublicData"] = "",
            ["MongoDb:ConnectionString"] = MongoConnection,
            ["MongoDb:Database"] = DatabaseName,
            ["TransientState:Provider"] = "Memory",
            ["SsalddelExecution:Mode"] = "Simulation",
            // Development·loopback·전용 DB 호스트의 합성 기사 시험만 우회하며 실제 자격 승인으로 저장하지 않는다.
            ["TransportQualification:AllowSimulationBypass"] = "true",
            ["SsalddelExecution:DevelopmentReadOnly"] = "false",
            ["ObjectStorage:Provider"] = "Local",
            ["CargoCompletionRecovery:Enabled"] = "false",
            ["DatabaseInitialization:RunAtStartup"] = "false",
            ["DatabaseInitialization:FailOnError"] = "true",
            ["SimulationSharedPublicData:Enabled"] = "false",
            ["SimulationWorldDerivationDatabase:Enabled"] = "false",
            ["SimulationSessionDatabase:Enabled"] = "false",
            ["FoodObserverVerification:Enabled"] = "false",
            ["ObservableOperationsVerification:Enabled"] = "false",
            ["MobileFieldTest:Enabled"] = "false",
            ["MobileFieldTest:FoodDispatch:Enabled"] = "false",
            ["IdentitySeed:BootstrapAdmin:Enabled"] = "false",
            ["IdentitySeed:DevelopmentAccounts:Enabled"] = "true",
            ["IdentitySeed:DevelopmentAccounts:AllowInSimulation"] = "true",
            ["IdentitySeed:DevelopmentAccounts:AdminPassword"] = password,
            ["IdentitySeed:DevelopmentAccounts:OrdererPassword"] = password,
            ["IdentitySeed:DevelopmentAccounts:DriverPassword"] = password,
            ["IdentitySeed:DevelopmentAccounts:ShipperPassword"] = password,
            ["VersionFeatureFlags:CommunityTrustWorkflow"] = "true",
            ["VersionFeatureFlags:DomesticTransportWorkflow"] = "true",
            ["VersionFeatureFlags:WarehouseFulfillmentWorkflow"] = "true",
            ["VersionFeatureFlags:FoodDeliveryWorkflow"] = "true",
            ["VersionFeatureFlags:PlatformOperationsControl"] = "true",
            ["VersionFeatureFlags:OperationalWorldObservationWorkflow"] = "false",
            ["VersionFeatureFlags:SsalddelMartWorkflow"] = "false",
            ["NeighborhoodMemberDelivery:Enabled"] = "true",
            ["NeighborhoodMemberDelivery:OperationalEnabled"] = "false",
            ["DriverPayoutBatch:AutomaticPreparationEnabled"] = "false",
            ["SharedDriverWork:Enabled"] = "true",
            ["SharedDriverWork:OperationalMaxConcurrentJobs"] = "4",
            // Artifact host only: time for native screenshots/camera. Product default/expiry guards remain intact.
            ["DispatchQueue:추천유지시간초"] = VerificationRecommendationTtlSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["StraightLineDriverPricing:Enabled"] = "true",
            // No invented small-goods quote or qualifications: an unknown quote remains unknown.
            ["TransportPrepayment:Enabled"] = "false",
            ["TransportPrepayment:EnableDepositWebhook"] = "false",
            ["DriverPayoutBatch:Enabled"] = "false",
            ["AgriculturalFisheriesBatch:Enabled"] = "false",
            ["CommunityEditorialBatch:Enabled"] = "false",
            ["CommunityPostEmailNotifications:Enabled"] = "false",
            ["CommunityPostTranslation:Enabled"] = "false",
            ["CommunityLedgerProjection:Enabled"] = "false",
            ["SalesChannelOrderSync:Enabled"] = "false",
            ["WorkRelationshipSnapshots:Enabled"] = "false",
            ["OperationsAlerts:Enabled"] = "false",
            ["UnitedStatesAddress:CensusGeocoder:Enabled"] = "false",
            ["Typecast:Enabled"] = "false",
            ["YouTube:Enabled"] = "false",
            ["TossPayments:ClientKey"] = "",
            ["TossPayments:SecretKey"] = "",
            ["AzureBlobStorage:ServiceUri"] = "",
            ["GoogleCloudStorage:BucketName"] = "",
            ["GoogleCloudStorage:ServiceAccountJsonPath"] = "",
            ["PushNotifications:ProjectId"] = "",
            ["PushNotifications:ServiceAccountJsonPath"] = "",
            ["PushNotifications:ServerKey"] = "",
            ["Jwt:Issuer"] = "IntegratedR108.Local",
            ["Jwt:Audience"] = "IntegratedR108.Local",
            ["Jwt:SecretKey"] = RequireSecret("USB_R74_JWT_SECRET", 32),
            ["IsmsPProtectedData:Aes256GcmKeyBase64"] = aesKey,
            ["IsmsPProtectedData:HashSalt"] = RequireSecret("USB_R74_HASH_SALT", 32),
            ["IsmsPProtectedData:FailWhenKeyMissing"] = "true",
            ["PersonalDataProtection:RequireCertificate"] = "false",
            ["PersonalDataProtection:ApplicationName"] = "Ssalddel.IntegratedWorkflow.R108." + DatabaseName,
            ["PersonalDataProtection:KeyRingPath"] = Path.Combine(artifactRoot, "host-content", "key-ring"),
            ["Logging:LogLevel:Default"] = "Critical",
            ["Serilog:MinimumLevel:Default"] = "Fatal",
            ["SsalddelLogging:FilePath"] = Path.Combine(artifactRoot, ".private", "host.log")
        };
        var runStableId = "integrated-r108:" + DatabaseGuid;
        var readyPath = Path.Combine(artifactRoot, "ready.json");
        if (File.Exists(readyPath))
        {
            using var previous = JsonDocument.Parse(File.ReadAllText(readyPath));
            using var preparation = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifactRoot, "preparation.json")));
            ValidatePreservedReadyBinding(previous.RootElement, preparation.RootElement,
                DatabaseName, runStableId, artifactRoot, R108SchemaReadiness.Mode);
        }
        return new CargoJourneySettings
        {
            RepositoryRoot = root, ArtifactRoot = artifactRoot, AccountPassword = password,
            AccessKey = RequireSecret("USB_R74_ACCESS_KEY", 32),
            RunStableId = runStableId, Configuration = configuration
        };
    }

    internal static void ValidatePreservedReadyBinding(JsonElement ready, JsonElement preparation,
        string databaseName, string runStableId, string artifactRoot, string schemaMode)
    {
        // Program의 Pascal RunStableId와 구판 camel 표기를 모두 읽되 별칭 중복은 거절한다.
        // 재시작 전에 실제 준비 기록·정확 GUID DB와 대조하며 seed/원장을 바꾸지 않는다.
        if (SingleString(ready, "schemaVersion") != "integrated-workflow-r108.ready.r1"
            || SingleString(ready, "database") != databaseName
            || SingleString(ready, "runStableId") != runStableId
            || (SingleString(ready, "schemaMode", optional: true) ?? "CurrentModel") != schemaMode)
            throw new InvalidOperationException("R108PreservedRunStableIdMismatch");
        if (SingleString(preparation, "schemaVersion") != "integrated-workflow-r108.preparation.r1"
            || SingleString(preparation, "status") != "FreshDedicatedDatabasePrepared"
            || SingleString(preparation, "database") != databaseName
            || SingleString(preparation, "runStableId") != runStableId
            || (SingleString(preparation, "schemaMode", optional: true) ?? "CurrentModel") != schemaMode
            || !string.Equals(Path.GetFullPath(SingleString(preparation, "outputRoot")!), artifactRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("R108PreservedPreparationMismatch");
    }

    private static string? SingleString(JsonElement value, string name, bool optional = false)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("R108SingleRecordFieldRequired");
        var matches = value.EnumerateObject().Where(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (optional && matches.Length == 0) return null;
        if (matches.Length != 1 || matches[0].Value.ValueKind != JsonValueKind.String
            || matches[0].Value.GetString() is not { Length: > 0 } text)
            throw new InvalidOperationException("R108SingleRecordFieldRequired");
        return text;
    }

    internal void ApplyProcessOverrides()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", BaseUrl);
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_SSALDDEL", Path.Combine(ArtifactRoot, "host-content"));
        foreach (var setting in Configuration)
            Environment.SetEnvironmentVariable(setting.Key.Replace(":", "__"), setting.Value);
    }

    internal void ValidateEffectiveConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("UsbR67EnvironmentMismatch");
        foreach (var setting in Configuration)
            if (!string.Equals(configuration[setting.Key], setting.Value, StringComparison.Ordinal))
                throw new InvalidOperationException("UsbR67ConfigurationMismatch:" + setting.Key);
        ValidateSqlConnection(configuration.GetConnectionString("DefaultConnection") ?? "");
    }

    internal void ValidateSqlConnection(string connection)
    {
        var sql = new MySqlConnectionStringBuilder(connection);
        if (sql.Server != "127.0.0.1" || sql.Port != 13310 || sql.Database != DatabaseName || sql.UserID != "usb_r74")
            throw new InvalidOperationException("UsbR67DatabaseBoundaryMismatch");
    }

    private static string RequireSecret(string name, int minimumLength)
        => Environment.GetEnvironmentVariable(name) is { } value && value.Length >= minimumLength
            ? value : throw new InvalidOperationException("UsbR67EnvironmentMissing:" + name);

    private static string ReadDatabase()
    {
        var value = Environment.GetEnvironmentVariable("R108_DATABASE") ?? "";
        var mode = R108SchemaReadiness.Mode;
        var currentModelName = System.Text.RegularExpressions.Regex.IsMatch(value, "^mirror_workflow_r108_[a-f0-9]{32}$");
        var migrationFirstName = System.Text.RegularExpressions.Regex.IsMatch(value, "^r108m_[a-f0-9]{32}$");
        if (!currentModelName && !migrationFirstName)
            throw new InvalidOperationException("R108FreshGuidDatabaseRequired");
        if (migrationFirstName && mode != "MigrationFirst")
            throw new InvalidOperationException("R108ShortDatabaseRequiresMigrationFirst");
        if (currentModelName && mode == "MigrationFirst"
            && !Environment.GetCommandLineArgs().Contains("--inspect-schema", StringComparer.Ordinal))
            throw new InvalidOperationException("R108LegacyMigrationDatabaseIsReadOnly");
        return value;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel", "Ssalddel.csproj"))) return directory.FullName;
        throw new InvalidOperationException("UsbR67RepositoryRootMissing");
    }
}
