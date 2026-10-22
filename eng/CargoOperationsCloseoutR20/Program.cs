using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using 살뜰.Data;

if (args is not ["--serve"]) return 2;
try
{
    // r114의 실제 제품 호스트·합성 fixture만 재사용하고 DB와 실행 증거는 새 영역에 둔다.
    var settings = CargoJourneySettings.Load(cargoPrimaryAI: true);
    var sourceBinding = CargoSourceBinding.Bind(settings);
    await settings.PrepareDatabaseAsync();
    settings.ApplyProcessOverrides();
    await using var factory = new CargoJourneyFactory(settings).WithWebHostBuilder(builder =>
        builder.ConfigureServices(services =>
        {
            services.AddUsbR67SyntheticAdapters();
            services.Replace(ServiceDescriptor.Scoped<살뜰.Services.Dispatch.Recommendation.I배차추천경로Service,
                Ssalddel.DispatchTraining.DispatchTrainingFixtureRoutes>());
            services.RemoveAll<IHostedService>();
            services.AddTransient<IStartupFilter, CargoDisputeObservationStartupFilter>();
            services.AddTransient<IStartupFilter, CargoPaymentFollowupStartupFilter>();
            services.Insert(0, ServiceDescriptor.Singleton<IStartupFilter>(new CargoSourceBindingStartupFilter(sourceBinding)));
        }));
    factory.UseKestrel(options => options.Listen(IPAddress.Loopback, CargoJourneySettings.HostPort));
    factory.StartServer();
    settings.ValidateEffectiveConfiguration(factory.Services.GetRequiredService<IConfiguration>(),
        factory.Services.GetRequiredService<IHostEnvironment>());
    var accounts = await R114Seed.PrepareAsync(factory.Services, settings);
    var health = await factory.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync();
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
    settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
    sourceBinding.AssertCurrent();
    var ready = new
    {
        schemaVersion = "cargo-operations-closeout-r20.r1", status = "ListeningWithFreshModel",
        preparedAtUtc = DateTimeOffset.UtcNow, processId = Environment.ProcessId,
        runStableId = settings.RunStableId, baseUrl = CargoJourneySettings.BaseUrl,
        database = CargoJourneySettings.DatabaseName, executionMode = "Simulation", synthetic = true,
        sourceBinding = new { schemaVersion = CargoSourceBinding.SchemaVersion,
            manifestSha256 = sourceBinding.Manifest.ManifestSha256, sourceCount = sourceBinding.Manifest.Sources.Length },
        serverListening = true, currentModelFreshDatabaseOnly = true,
        migrationsApplied = false, operationalMigrationVerified = false,
        healthStatus = health.Status.ToString(),
        healthChecks = health.Entries.Select(x => new { name = x.Key, status = x.Value.Status.ToString(),
            description = x.Value.Description, pendingMigrations = x.Value.Data.Where(d => d.Key.EndsWith("PendingMigrations", StringComparison.Ordinal) && d.Value is int)
                .ToDictionary(d => d.Key, d => (int)d.Value) }),
        accounts = accounts.Select(a => new { a.UserId, a.UserName, a.Roles }),
        transportPrepaymentEnabled = true, paymentProvider = "FakePG_SimulationOnly",
        sourceDatabaseMigrationHistoryFabricated = false,
        workersEnabled = false, externalHttpBlockedExceptLoopbackModel = true,
        automaticPayoutEnabled = false, cloudPushEnabled = false, cloudDeployment = false,
        evidenceBoundToCompletedJourney = false,
        limitations = new[] {
            "Full product HTTP on a new isolated database; not the old read-only preview host.",
            "EnsureCreated is a fresh-model test fixture, not an operational migration or a healthy readiness substitute.",
            "Route estimates, accounts and camera attachment fixtures are synthetic; no real goods or money.",
            "Existing r114/r19 hosts, databases, device evidence and source media are preserved."
        }
    };
    await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, "ready.json"),
        JsonSerializer.Serialize(ready, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(new { status = ready.status, baseUrl = ready.baseUrl,
        database = ready.database, healthStatus = ready.healthStatus, runStableId = ready.runStableId,
        prepayment = "SimulationOnly", payout = "Disabled" }));
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();
    try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    return 0;
}
catch (Exception exception)
{
    // 자격값·SQL 원문·응답 본문은 출력하지 않는다.
    Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "Failed", type = exception.GetType().Name,
        code = exception is InvalidOperationException && System.Text.RegularExpressions.Regex.IsMatch(exception.Message, "^(R114|R20)[A-Za-z0-9:._-]+$") ? exception.Message : null }));
    return 1;
}
