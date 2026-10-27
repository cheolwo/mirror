using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Application.CommandProcessing;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;

if (args is not ["--serve"] and not ["--inspect-schema"]) return 2;
CargoJourneySettings? settings = null;
try
{
    settings = CargoJourneySettings.Load();
    settings.ApplyProcessOverrides();
    await using var factory = new CargoJourneyFactory(settings).WithWebHostBuilder(builder =>
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, UsbFoodControlStartupFilter>();
            services.AddUsbR67SyntheticAdapters();
            services.AddR108ExplicitPorts();
            services.AddSingleton<AcceptanceNotificationFault>();
            services.AddDbContext<SsalddelContext>((sp,options) => options.AddInterceptors(sp.GetRequiredService<AcceptanceNotificationFault>()));
            services.AddSingleton<HarnessActor>();
            services.RemoveAll<ICurrentUserAccessor>();
            services.AddSingleton<ICurrentUserAccessor>(sp => sp.GetRequiredService<HarnessActor>());
            services.AddTransient<IStartupFilter, MemberFixtureControl>();
            services.AddTransient<IStartupFilter, RuntimeProofControl>();
        }));
    factory.UseKestrel(options => options.Listen(IPAddress.Loopback, CargoJourneySettings.HostPort));
    factory.StartServer();
    settings.ValidateEffectiveConfiguration(factory.Services.GetRequiredService<IConfiguration>(), factory.Services.GetRequiredService<IHostEnvironment>());
    if (args[0] == "--inspect-schema")
    {
        await R108SchemaReadiness.InspectAsync(factory.Services, settings);
        Console.WriteLine("R108SchemaInspected; read-only; no seed or migration executed");
        return 0;
    }
    await R108SchemaReadiness.PrepareAsync(factory.Services, settings);
    var marker = Path.Combine(settings.ArtifactRoot, "initialized.json");
    if (!File.Exists(marker))
    {
        var accounts = await UsbDeviceSeed.PrepareAsync(factory.Services, settings);
        await File.WriteAllTextAsync(marker, JsonSerializer.Serialize(new
        {
            settings.RunStableId, database = CargoJourneySettings.DatabaseName,
            schemaBasis = R108SchemaReadiness.Mode == "MigrationFirst"
                ? "FreshDedicatedDatabaseActualFourContextMigrations_NotOperationalVerification"
                : "FreshDatabaseEnsureCreated_CurrentEfModel_NotOperationalMigration",
            syntheticAccounts = accounts.Count, createdAtUtc = DateTime.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    var health = await R108SchemaReadiness.CaptureReadinessAsync(factory.Services, settings, "readiness-diagnostic.json");
    var ready = new
    {
        schemaVersion = "integrated-workflow-r108.ready.r1", status = "Listening",
        settings.RunStableId, database = CargoJourneySettings.DatabaseName, baseUrl = CargoJourneySettings.BaseUrl,
        environment = "Development", executionMode = "Simulation", synthetic = true,
        hostedServicesRemoved = true, externalHttpBlocked = true,
        cloudPushEnabled = false, automaticPayoutEnabled = false, transportPrepaymentEnabled = false,
        mysqlPort = 13310, mongoPort = 27021, recommendationTtlSeconds = CargoJourneySettings.VerificationRecommendationTtlSeconds,
        schemaMode = R108SchemaReadiness.Mode,
        currentModelFreshDatabaseOnly = R108SchemaReadiness.Mode == "CurrentModel",
        isolatedMigrationChainApplied = R108SchemaReadiness.Mode == "MigrationFirst",
        healthStatus = health.Status, healthChecks = health.Entries,
        operationalMigrationVerified = false,
        preservedRestart = File.Exists(Path.Combine(settings.ArtifactRoot, "ready.json")),
        foodRouteAndPricingSynthetic = true, localSyntheticPhotosOnly = true,
        memberProviderRouteGuardEmulated = true, actualRouteVerified = false,
        pushTransport = "ExplicitLocalFake_NoGoogleOrDeviceDelivery",
        atUtc = DateTime.UtcNow
    };
    await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, "ready.json"),
        JsonSerializer.Serialize(ready, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("R108Listening:5378; Simulation; hosted services/external effects disabled");
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();
    try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); } catch (OperationCanceledException) { }
    return 0;
}
catch (Exception exception)
{
    var safe = new
    {
        status = "Failed", type = exception.GetType().FullName,
        methods = new System.Diagnostics.StackTrace(exception, false).GetFrames()
            .Select(x => x.GetMethod()).Where(x => x is not null)
            .Take(10).Select(x => x!.DeclaringType?.FullName + "." + x.Name).ToArray()
    };
    if (settings is not null)
        await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, "host-failure.json"), JsonSerializer.Serialize(safe));
    Console.Error.WriteLine(JsonSerializer.Serialize(safe));
    return 1;
}
