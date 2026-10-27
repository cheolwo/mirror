using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using 살뜰.Data;

if (args.Length == 1 && args[0] == "--self-check") return SelectorSelfCheck.Run();
if (args is ["--primary-profile-self-check"]) return PrimaryAIProfileSelfCheck.Run();
if (args is not ["--serve"] and not ["--serve", "--cargo-primary-ai"])
{
    Console.Error.WriteLine("Usage: CargoAiDeviceVerificationR114 --serve [--cargo-primary-ai] (private process environment required)");
    return 2;
}

try
{
    var settings = CargoJourneySettings.Load(args is ["--serve", "--cargo-primary-ai"]);
    await settings.PrepareDatabaseAsync();
    settings.ApplyProcessOverrides();
    await using var factory = new CargoJourneyFactory(settings).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.AddUsbR67SyntheticAdapters();
        if (settings.CargoPrimaryAI)
            services.Replace(ServiceDescriptor.Scoped<살뜰.Services.Dispatch.Recommendation.I배차추천경로Service,
                Ssalddel.DispatchTraining.DispatchTrainingFixtureRoutes>());
        services.RemoveAll<IHostedService>();
    }));
    factory.UseKestrel(options => options.Listen(IPAddress.Loopback, CargoJourneySettings.HostPort));
    factory.StartServer();
    settings.ValidateEffectiveConfiguration(factory.Services.GetRequiredService<IConfiguration>(), factory.Services.GetRequiredService<IHostEnvironment>());
    IReadOnlyList<string> nullableSchemaAdditions;
    using (var schemaScope = factory.Services.CreateScope())
        nullableSchemaAdditions = await R114NullableSchema.PrepareAsync(settings, schemaScope.ServiceProvider.GetRequiredService<SsalddelContext>());
    var accounts = await R114Seed.PrepareAsync(factory.Services, settings);
    var ttl = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<살뜰.Services.Dispatch.Queue.배차큐정책Options>>().Value.추천유지시간초;
    if (ttl != CargoJourneySettings.VerificationRecommendationTtlSeconds) throw new InvalidOperationException("R114RecommendationTtlMismatch");
    var health = await factory.Services.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>().CheckHealthAsync();
    using (var scope = factory.Services.CreateScope())
        settings.ValidateSqlConnection(scope.ServiceProvider.GetRequiredService<SsalddelContext>().Database.GetDbConnection().ConnectionString);
    var ownSources = new[]
    {
        "eng/CargoAiDeviceVerificationR114/CargoAiDeviceVerificationR114.csproj",
        "eng/CargoAiDeviceVerificationR114/CargoJourneySettings.cs", "eng/CargoAiDeviceVerificationR114/R114Seed.cs",
        "eng/CargoAiDeviceVerificationR114/Program.cs", "eng/CargoAiDeviceVerificationR114/start.ps1",
        "eng/CargoAiDeviceVerificationR114/CargoAiSelector.cs", "eng/CargoAiDeviceVerificationR114/CargoJourneyControlStartupFilter.cs",
        "eng/CargoAiDeviceVerificationR114/CargoJourneyFactory.cs", "eng/CargoAiDeviceVerificationR114/CargoJourneyControlStartupFilter.PrimaryAI.cs",
        "eng/CargoAiDeviceVerificationR114/R114NullableSchema.cs",
        "eng/Ssalddel.DispatchTraining/DispatchTrainingFixtureAdapters.cs", "eng/Ssalddel.CargoJourneyVerification/CargoJourneySeed.cs",
        "Ssalddel/Services/Dispatch/AI/OllamaCargoDispatchCandidateAI.cs", "Ssalddel/Services/Dispatch/AI/CargoDispatchCandidateSelection.cs",
        "artifacts/local/physical-phone-r67/host/UsbSyntheticAdapters.cs"
    }.Select(path => new { path, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(settings.RepositoryRoot, path)))) }).ToArray();
    var ready = new
    {
        schemaVersion = "cargo-ai-device-r114.ready.v1", status = health.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "Ready" : "PreparedWithHealthWarnings", preparedAtUtc = DateTimeOffset.UtcNow,
        processId = Environment.ProcessId, runStableId = settings.RunStableId, baseUrl = CargoJourneySettings.BaseUrl,
        database = CargoJourneySettings.DatabaseName, mysqlPort = 13310, mongoPort = 27021,
        environment = "Development", executionMode = "Simulation", synthetic = true,
        serverListening = true, migrationsApplied = false, latestModelEnsureCreated = true,
        nullableSchemaAdditions,
        sourceFiles = ownSources, hostAssemblySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(R114Seed).Assembly.Location))),
        actualModelSourceSha256 = ownSources.Single(x => x.path.EndsWith(settings.CargoPrimaryAI ? "/OllamaCargoDispatchCandidateAI.cs" : "/CargoAiSelector.cs", StringComparison.Ordinal)).sha256,
        modelInferencePerformed = false, weightsTrained = false, operationalDispatchEnabled = false,
        cargoPrimaryAI = settings.CargoPrimaryAI,
        modelSelectionPath = settings.CargoPrimaryAI ? "ProductCargoCandidatePolicy" : "LegacyR114Selector",
        syntheticCompleteRouteFixture = settings.CargoPrimaryAI,
        automaticWorkersEnabled = false, hostedServicesRemoved = true, factoryHttpExternalRequestsBlocked = true,
        modelHttpBoundary = settings.CargoPrimaryAI ? "loopback_12681_post_api_chat_only" : "loopback_only_explicit_selector", photoStorageProvider = "Local", cloudPushEnabled = false,
        automaticPayoutEnabled = false, transportPrepaymentEnabled = false, foodAutomaticDispatchEnabled = false,
        recommendationTtlSeconds = ttl, recommendationTtlScope = "ArtifactHostOnly_NativeScreenshotAndCameraVerification",
        productDefaultRecommendationTtlSeconds = new 살뜰.Services.Dispatch.Queue.배차큐정책Options().추천유지시간초,
        productionDefaultTtlAcceptanceVerified = false, actualRouteVerified = false, actualPaymentVerified = false,
        driverStateSeeded = false, deviceUiProof = false, physicalDeviceVerified = false, actualCameraCaptureVerified = false, journeyCompleted = false,
        healthStatus = health.Status.ToString(), healthChecks = health.Entries.Select(x => new
        {
            name = x.Key, status = x.Value.Status.ToString(), description = x.Value.Description,
            pendingMigrations = x.Value.Data.Where(d => d.Key.EndsWith("PendingMigrations", StringComparison.Ordinal) && d.Value is int)
                .ToDictionary(d => d.Key, d => (int)d.Value)
        }),
        accounts = accounts.Select(a => new { a.UserId, a.UserName, a.Roles }),
        controlledOfferEndpoint = "/verification/r114/ai-recommend",
        limitations = new[]
        {
            "Local Simulation host; existing r67 DB and previous evidence are preserved.",
            "Driver shift, receiving intent and position must be submitted through current APIs; no seeded GPS or active shift.",
            "AI inference, product recommendation, device acceptance, photo upload, transport completion and financial results are separate evidence.",
            "Synthetic straight-line route estimates are not actual GPS, road routing or operational fare evidence."
        }
    };
    var readyBytes = JsonSerializer.SerializeToUtf8Bytes(ready, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllBytesAsync(Path.Combine(settings.ArtifactRoot, "ready.json"), readyBytes);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = health.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "Ready" : "PreparedWithHealthWarnings",
        healthStatus = health.Status.ToString(), baseUrl = CargoJourneySettings.BaseUrl, database = CargoJourneySettings.DatabaseName,
        runStableId = settings.RunStableId, readyFile = Path.Combine(settings.ArtifactRoot, "ready.json"),
        accountCount = accounts.Count, synthetic = true, operationalDispatchEnabled = false, automaticPayoutEnabled = false
    }));
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();
    try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    return 0;
}
catch (Exception error)
{
    // 연결 문자열·자격 증명·원시 응답을 콘솔에 출력하지 않는다.
    var safeCode = error is InvalidOperationException && System.Text.RegularExpressions.Regex.IsMatch(error.Message, "^R114[A-Za-z0-9:._-]+$") ? error.Message : null;
    Exception? cause = error;
    for (var depth = 0; depth < 8 && cause is not MySqlConnector.MySqlException && cause?.InnerException is not null; depth++) cause = cause.InnerException;
    var sql = cause as MySqlConnector.MySqlException;
    var missing = sql is null ? null : System.Text.RegularExpressions.Regex.Match(sql.Message, "Unknown column '([A-Za-z0-9_\\.가-힣]+)'", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "Failed", failureType = error.GetType().Name, errorCode = safeCode,
        mysqlNumber = sql?.Number, sqlState = sql?.SqlState, missingColumn = missing is { Success: true } ? missing.Groups[1].Value : null,
        detailsRetained = false }));
    return 1;
}
