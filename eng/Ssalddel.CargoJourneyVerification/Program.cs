using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using 살뜰.Data;

if (args.Length != 1 || args[0] != "--serve")
{
    Console.Error.WriteLine("Usage: Ssalddel.CargoJourneyVerification --serve (dedicated environment variables required)");
    return 2;
}

CargoJourneySettings? settings = null;
try
{
    settings = CargoJourneySettings.Load();
    settings.ApplyProcessOverrides();
    await using var factory = new CargoJourneyFactory(settings);
    factory.StartServer();
    var environment = factory.Services.GetRequiredService<IHostEnvironment>();
    var configuration = factory.Services.GetRequiredService<IConfiguration>();
    settings.ValidateEffectiveConfiguration(configuration, environment);
    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
        await CargoJourneySeed.PrepareAsync(scope.ServiceProvider, settings);
    }

    var readyPath = Path.Combine(settings.ArtifactRoot, "ready.json");
    var ready = new
    {
        schemaVersion = "cargo-journey-verification.ready.r1",
        status = "Prepared",
        baseUrl = CargoJourneySettings.BaseUrl,
        database = CargoJourneySettings.DatabaseName,
        environment = "Development",
        executionMode = "Simulation",
        runStableId = settings.RunStableId,
        warehouseId = CargoJourneySeed.WarehouseId,
        inboundRequestId = CargoJourneySeed.InboundRequestId,
        inboundItemId = CargoJourneySeed.InboundItemId,
        outboundPlanId = CargoJourneySeed.OutboundPlanId,
        expectedRequestId = CargoJourneySeed.ExpectedRequestId,
        quantity = CargoJourneySeed.Quantity,
        vehicleType = "1톤 카고",
        accounts = new
        {
            shipper = CargoJourneySeed.ShipperId,
            driver = CargoJourneySeed.DriverId,
            warehouse = CargoJourneySeed.WarehouseUserId,
            admin = CargoJourneySeed.AdminId
        },
        synthetic = true,
        serverListening = true,
        journeyCompleted = false,
        deviceUiProof = false,
        productionDispatcherProof = false
    };
    var json = JsonSerializer.Serialize(ready, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(readyPath, json);
    Console.WriteLine(json);

    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();
    try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    return 0;
}
catch (Exception exception)
{
    // Connection strings, credentials and token payloads never appear in console diagnostics.
    if (settings is not null)
    {
        var detail = exception.ToString();
        foreach (var name in new[] { "CARGO_JOURNEY_ACCOUNT_PASSWORD", "CARGO_JOURNEY_MYSQL_PASSWORD", "CARGO_JOURNEY_MYSQL_ROOT_PASSWORD", "CARGO_JOURNEY_JWT_SECRET", "CARGO_JOURNEY_AES_KEY", "CARGO_JOURNEY_HASH_SALT", "CARGO_JOURNEY_ACCESS_KEY" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } secret) detail = detail.Replace(secret, "[omitted]", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, "preparation-failure.txt"), detail);
    }
    Console.Error.WriteLine($"CargoJourneyPreparationFailed:{exception.GetType().Name}");
    return 1;
}
