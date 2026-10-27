using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ssalddel.DispatchTraining.Experiences;

namespace Ssalddel.DispatchTraining;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("--experience-", StringComparison.Ordinal))
            return await DispatchExperienceCommand.RunAsync(args);
        if (args is ["--selftest"])
        {
            var report = await DispatchTrainingSelfChecks.RunAsync(Path.Combine(Environment.CurrentDirectory, "artifacts/local/dispatch-training-r58/selfchecks"));
            report = report with { Checks = report.Checks.Concat(await DispatchTrainingFixtureSelfChecks.RunAsync()).ToArray() };
            report = report with { Checks = report.Checks.Concat(await DispatchTrainingShadowSelfChecks.RunAsync()).ToArray() };
            report = report with { Checks = report.Checks.Concat(DispatchTrainingCargoAIControlSelfChecks.Run()).ToArray() };
            Console.WriteLine(JsonSerializer.Serialize(report)); return report.Passed ? 0 : 1;
        }
        if (args is ["--archive-session"])
        {
            var folder = Path.Combine(Environment.CurrentDirectory, "artifacts/local/dispatch-training-r58");
            var result = await DispatchTrainingArchive.ArchiveAsync(Path.Combine(folder, "session.json"),
                [Path.Combine(folder, "actors.json"), Path.Combine(folder, "ready.json")]);
            Console.WriteLine(JsonSerializer.Serialize(result)); return 0;
        }
        if (args is not ["--serve"] and not ["--serve-cargo-ai"]) { Console.WriteLine("Use --serve or --serve-cargo-ai with dedicated environment, --selftest, --archive-session, or --experience-collect / --experience-status / --experience-import / --experience-review / --experience-selftest."); return 2; }
        try
        {
            var cargoCandidateAI = args is ["--serve-cargo-ai"];
            var settings = DispatchTrainingSettings.Load(cargoCandidateAI); settings.Apply();
            using var lease = DispatchTrainingArchive.AcquireHostLease(Path.Combine(settings.ArtifactRoot, "session.json"));
            await using var factory = new DispatchTrainingFactory(settings);
            factory.StartServer();
            settings.Validate(factory.Services.GetRequiredService<IConfiguration>(), factory.Services.GetRequiredService<IHostEnvironment>());
            using (var scope = factory.Services.CreateScope()) await DispatchTrainingSeed.PrepareAsync(scope.ServiceProvider, settings);
            await factory.Services.GetRequiredService<DispatchTrainingCoordinator>().InitializeAsync();
            factory.Services.GetRequiredService<DispatchTrainingReadiness>().Ready = true;
            var ready = new { schemaVersion = "dispatch-training.ready.r58", baseUrl = DispatchTrainingSettings.BaseUrl,
                database = DispatchTrainingSettings.DatabaseName, synthetic = true, executionMode = "Simulation",
                adminAccount = DispatchTrainingSeed.AdminId, durationSeconds = 1800, intervalSeconds = 120,
                maximumOrders = 15, cargoCandidateAI, shadowComparisonIsActualSelection = false, phoneVerified = false, productionEffects = false };
            await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, "ready.json"), JsonSerializer.Serialize(ready, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(ready));
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
            try { await Task.Delay(Timeout.Infinite, stop.Token); } catch (OperationCanceledException) { }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("DispatchTrainingHostFailed:" + e.GetType().Name); return 1; }
    }
}
