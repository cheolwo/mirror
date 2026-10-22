using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySqlConnector;

// 연결한 CargoJourneyFactory/Seed의 타입 호환만 유지한다. r67 설정과 DB는 수정하지 않는다.
internal sealed class CargoJourneySettings
{
    internal const string DatabaseName = "mirror_operations_r20r2_20261022";
    internal const int HostPort = 5368;
    internal const int VerificationRecommendationTtlSeconds = 300;
    internal const string BaseUrl = "http://127.0.0.1:5368";
    internal const string MongoConnection = "mongodb://127.0.0.1:27021";
    internal required string RepositoryRoot { get; init; }
    internal required string ArtifactRoot { get; init; }
    internal required string AccountPassword { get; init; }
    internal required string AccessKey { get; init; }
    internal required string RunStableId { get; init; }
    internal bool CargoPrimaryAI { get; init; }
    internal required IReadOnlyDictionary<string, string?> Configuration { get; init; }

    internal static CargoJourneySettings Load(bool cargoPrimaryAI = false)
    {
        var root = FindRepositoryRoot();
        var artifactRoot = Path.Combine(root, "artifacts", "local", "cargo-operations-r20-r2", "host");
        Directory.CreateDirectory(Path.Combine(artifactRoot, "host-content"));
        Directory.CreateDirectory(Path.Combine(artifactRoot, ".private"));
        var password = RequireSecret("USB_R74_ACCOUNT_PASSWORD", 16);
        var sql = new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1", Port = 13310, Database = DatabaseName, UserID = "usb_r74",
            Password = RequireSecret("USB_R74_SQL_PASSWORD", 16),
            AllowUserVariables = true, CharacterSet = "utf8mb4", ConnectionTimeout = 10, DefaultCommandTimeout = 30
        };
        _ = RequireSecret("USB_R74_SQL_ROOT_PASSWORD", 16);
        var aesKey = RequireSecret("USB_R74_AES_KEY", 32);
        if (Convert.FromBase64String(aesKey).Length != 32) throw new InvalidOperationException("R114AesKeyInvalid");
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
            ["IdentitySeed:DevelopmentAccounts:Enabled"] = "false",
            ["IdentitySeed:DevelopmentAccounts:AllowInSimulation"] = "false",
            ["VersionFeatureFlags:CommunityTrustWorkflow"] = "true",
            ["VersionFeatureFlags:DomesticTransportWorkflow"] = "true",
            ["VersionFeatureFlags:WarehouseFulfillmentWorkflow"] = "true",
            ["VersionFeatureFlags:FoodDeliveryWorkflow"] = "false",
            ["VersionFeatureFlags:PlatformOperationsControl"] = "true",
            ["VersionFeatureFlags:OperationalWorldObservationWorkflow"] = "false",
            ["VersionFeatureFlags:SsalddelMartWorkflow"] = "false",
            ["SharedDriverWork:Enabled"] = "true",
            ["SharedDriverWork:OperationalMaxConcurrentJobs"] = "4",
            ["NeighborhoodMemberDelivery:Enabled"] = "false",
            ["NeighborhoodMemberDelivery:OperationalEnabled"] = "false",
            ["DispatchQueue:추천유지시간초"] = VerificationRecommendationTtlSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["StraightLineDriverPricing:Enabled"] = "true",
            ["TransportPrepayment:Enabled"] = "true",
            ["TransportPrepayment:EnableDepositWebhook"] = "false",
            ["DriverPayoutBatch:Enabled"] = "false",
            ["DriverPayoutBatch:AutomaticPreparationEnabled"] = "false",
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
            ["Jwt:Issuer"] = "CargoOperationsR20.Local",
            ["Jwt:Audience"] = "CargoOperationsR20.Local",
            ["Jwt:SecretKey"] = RequireSecret("USB_R74_JWT_SECRET", 32),
            ["IsmsPProtectedData:Aes256GcmKeyBase64"] = aesKey,
            ["IsmsPProtectedData:HashSalt"] = RequireSecret("USB_R74_HASH_SALT", 32),
            ["IsmsPProtectedData:FailWhenKeyMissing"] = "true",
            ["PersonalDataProtection:RequireCertificate"] = "false",
            ["PersonalDataProtection:ApplicationName"] = "Ssalddel.CargoOperationsCloseout.R20",
            ["PersonalDataProtection:KeyRingPath"] = Path.Combine(artifactRoot, "host-content", "key-ring"),
            ["Logging:LogLevel:Default"] = "Critical",
            ["Serilog:MinimumLevel:Default"] = "Fatal",
            ["SsalddelLogging:FilePath"] = Path.Combine(artifactRoot, ".private", "host.log")
        };
        if (cargoPrimaryAI)
        {
            configuration["CargoDispatchOllama:Enabled"] = "true";
            configuration["CargoDispatchOllama:BaseUrl"] = "http://127.0.0.1:12681/";
            configuration["CargoDispatchOllama:Model"] = "qwen3:1.7b";
            configuration["CargoDispatchOllama:TimeoutSeconds"] = "5";
            configuration["VersionFeatureFlags:OperationalDispatchCore"] = "true";
        }
        return new CargoJourneySettings
        {
            RepositoryRoot = root, ArtifactRoot = artifactRoot, AccountPassword = password,
            AccessKey = RequireSecret("USB_R74_ACCESS_KEY", 32),
            RunStableId = ReadOrCreateRunMarker(artifactRoot), Configuration = configuration, CargoPrimaryAI = cargoPrimaryAI
        };
    }

    internal async Task PrepareDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1", Port = 13310, UserID = "root",
            Password = RequireSecret("USB_R74_SQL_ROOT_PASSWORD", 16),
            Pooling = false, ConnectionTimeout = 10, DefaultCommandTimeout = 30
        };
        try
        {
            await using var sql = new MySqlConnection(connection.ConnectionString);
            await sql.OpenAsync(cancellationToken);
            // 고정한 새 DB의 생성/권한만 허용한다. 기존 schema·table·migration·volume은 변경하지 않는다.
            await using (var create = new MySqlCommand("CREATE DATABASE IF NOT EXISTS `mirror_operations_r20r2_20261022` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci", sql))
                await create.ExecuteNonQueryAsync(cancellationToken);
            await using (var grant = new MySqlCommand("GRANT ALL PRIVILEGES ON `mirror_operations_r20r2_20261022`.* TO 'usb_r74'@'%'", sql))
                await grant.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException) { throw new InvalidOperationException("R114DedicatedDatabasePreparationFailed"); }
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
        if (!environment.IsDevelopment()) throw new InvalidOperationException("R114EnvironmentMismatch");
        foreach (var setting in Configuration)
            if (!string.Equals(configuration[setting.Key], setting.Value, StringComparison.Ordinal))
                throw new InvalidOperationException("R114ConfigurationMismatch:" + setting.Key);
        ValidateSqlConnection(configuration.GetConnectionString("DefaultConnection") ?? "");
    }

    internal void ValidateSqlConnection(string connection)
    {
        var sql = new MySqlConnectionStringBuilder(connection);
        if (sql.Server != "127.0.0.1" || sql.Port != 13310 || sql.Database != DatabaseName || sql.UserID != "usb_r74")
            throw new InvalidOperationException("R114DatabaseBoundaryMismatch");
    }

    private static string ReadOrCreateRunMarker(string artifactRoot)
    {
        var path = Path.Combine(artifactRoot, ".private", "host-state.json");
        var proposed = new HostMarker("cargo-operations-r20.host-state.v1", DatabaseName, "cargo-operations-r20:" + Guid.NewGuid().ToString("N"));
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(stream, proposed); stream.Flush(true);
        }
        catch (IOException) when (File.Exists(path)) { }
        var marker = JsonSerializer.Deserialize<HostMarker>(File.ReadAllBytes(path)) ?? throw new InvalidOperationException("R114RunMarkerMissing");
        if (marker.Schema != proposed.Schema || marker.Database != DatabaseName || !marker.RunStableId.StartsWith("cargo-operations-r20:", StringComparison.Ordinal)
            || !Guid.TryParseExact(marker.RunStableId["cargo-operations-r20:".Length..], "N", out _))
            throw new InvalidOperationException("R114RunMarkerMismatch");
        return marker.RunStableId;
    }

    private static string RequireSecret(string name, int minimumLength)
        => Environment.GetEnvironmentVariable(name) is { } value && value.Length >= minimumLength
            ? value : throw new InvalidOperationException("R114EnvironmentMissing:" + name);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel", "Ssalddel.csproj"))) return directory.FullName;
        throw new InvalidOperationException("R114RepositoryRootMissing");
    }
    private sealed record HostMarker(string Schema, string Database, string RunStableId);
}
