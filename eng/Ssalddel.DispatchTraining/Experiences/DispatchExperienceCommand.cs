using System.Text.Json;

namespace Ssalddel.DispatchTraining.Experiences;

/// <summary>로컬 훈련 원문 접수·조회만 수행한다. 서버 설정이나 업무 API를 시작하지 않는다.</summary>
internal static class DispatchExperienceCommand
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var repo = Environment.CurrentDirectory;
            if (!File.Exists(Path.Combine(repo, "eng/Ssalddel.DispatchTraining/Ssalddel.DispatchTraining.csproj")))
                throw new DispatchExperienceException("RepositoryRootRequired");
            var options = Parse(args);
            var root = PrivatePath(repo, options.GetValueOrDefault("--experience-root") ?? "artifacts/local/dispatch-experiences");
            using var cancel = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancel.Cancel(); };
            Console.CancelKeyPress += handler;
            try
            {
                if (args[0] == "--experience-selftest")
                {
                    var report = await DispatchExperienceSelfChecks.RunAsync(
                        PrivatePath(repo, "artifacts/local/dispatch-experience-setup/selfchecks"), cancel.Token);
                    var representatives = DispatchExperienceReviewSelectorSelfChecks.Run();
                    var combined = report with { Checks = report.Checks.Concat(representatives.Checks.Select(
                        x => new DispatchExperienceSelfCheckResult("representative:" + x.Name, x.Passed, x.Failure))).ToArray() };
                    Console.WriteLine(JsonSerializer.Serialize(combined, Json));
                    return combined.Passed ? 0 : 1;
                }
                var corpus = new DispatchExperienceCorpus(root);
                var imported = 0; var reused = 0; var failed = 0;
                if (args[0] == "--experience-collect")
                {
                    var source = PrivatePath(repo, options.GetValueOrDefault("--source-root") ?? "artifacts/local/dispatch-training-r58");
                    SeparateRoots(source, root);
                    foreach (var path in SessionPaths(source))
                    {
                        try
                        {
                            var result = await corpus.ImportAsync(path, cancel.Token);
                            if (result.Reused) reused++; else imported++;
                        }
                        catch (Exception e) when (e is IOException or JsonException or ArgumentException)
                        {
                            failed++;
                            Console.Error.WriteLine(JsonSerializer.Serialize(new
                            {
                                source = Path.GetRelativePath(source, path), errorCode = ErrorCode(e)
                            }, Json));
                        }
                    }
                }
                else if (args[0] == "--experience-import")
                {
                    var source = PrivatePath(repo, options["--session"]);
                    SeparateRoots(source, root);
                    var result = await corpus.ImportAsync(source, cancel.Token);
                    if (result.Reused) reused++; else imported++;
                }
                else if (args[0] == "--experience-review")
                {
                    var path = PrivatePath(repo, options["--command"]);
                    if (new FileInfo(path).Length > 16_384) throw new DispatchExperienceException("ReviewCommandTooLarge");
                    var command = JsonSerializer.Deserialize<DispatchExperienceReviewCommand>(await File.ReadAllBytesAsync(path, cancel.Token), Json)
                        ?? throw new DispatchExperienceException("ReviewCommandRequired");
                    var result = await corpus.ReviewAsync(command, cancel.Token);
                    Console.WriteLine(JsonSerializer.Serialize(result, Json));
                }
                var summary = await corpus.ReadAndPublishAsync(
                    (value, token) => DispatchExperienceReport.WriteAsync(root, value, token), cancel.Token);
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    imported, reused, failed, summary.SnapshotCount, summary.RunCount,
                    summary.TotalSlotCount, summary.ScheduledSlotCount, summary.SkippedSlotCount,
                    orderExperiences = summary.Orders.Count,
                    submittedOrders = summary.Orders.Count(x => x.SubmissionStatusCode == "Confirmed"),
                    observedCompletedOrders = summary.Orders.Count(x => x.ObservedStateCode == "Completed"),
                    currentHumanReviews = summary.Orders.Count(x => x.ReviewCurrent),
                    summary.Synthetic, summary.OperationalDispatchEnabled, summary.TrainingEligible, summary.WeightsTrained,
                    report = Path.GetRelativePath(repo, Path.Combine(root, "current-summary.md")),
                    representatives = Path.GetRelativePath(repo, Path.Combine(root, "current-representatives.md"))
                }, Json));
                return failed == 0 ? 0 : 1;
            }
            finally { Console.CancelKeyPress -= handler; }
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("ExperienceCommandCancelled"); return 1; }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ErrorCode(e)); return 1;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        if (args.Length == 0) throw new DispatchExperienceException("ExperienceCommandRequired");
        string[] allowed = args[0] switch
        {
            "--experience-collect" => ["--source-root", "--experience-root"],
            "--experience-import" => ["--session", "--experience-root"],
            "--experience-status" => ["--experience-root"],
            "--experience-review" => ["--command", "--experience-root", "--human-reviewed"],
            "--experience-selftest" => [],
            _ => throw new DispatchExperienceException("UnknownExperienceCommand")
        };
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            var key = args[i];
            if (!allowed.Contains(key) || options.ContainsKey(key)) throw new DispatchExperienceException("InvalidExperienceOption");
            if (key == "--human-reviewed") { options.Add(key, "true"); continue; }
            if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal)) throw new DispatchExperienceException("ExperienceOptionValueRequired");
            options.Add(key, args[i]);
        }
        if (args[0] == "--experience-import" && !options.ContainsKey("--session")
            || args[0] == "--experience-review" && (!options.ContainsKey("--command") || !options.ContainsKey("--human-reviewed")))
            throw new DispatchExperienceException("ExplicitExperienceInputRequired");
        return options;
    }

    private static IEnumerable<string> SessionPaths(string root)
    {
        if (!Directory.Exists(root)) yield break;
        var current = Path.Combine(root, "session.json");
        if (File.Exists(current)) yield return current;
        foreach (var category in new[] { "completed-runs", "archive" })
        {
            var parent = Path.Combine(root, category);
            CheckLinks(parent);
            if (!Directory.Exists(parent)) continue;
            foreach (var folder in Directory.GetDirectories(parent).Order(StringComparer.Ordinal))
            {
                CheckLinks(folder);
                if (category == "archive")
                {
                    var path = Path.Combine(folder, "session.json");
                    if (File.Exists(path)) yield return path;
                }
                else
                    foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal)) yield return path;
            }
        }
    }

    private static string PrivatePath(string repo, string value)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value, repo));
        var prefix = Path.GetFullPath(Path.Combine(repo, "artifacts/local")) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new DispatchExperienceException("ExperiencePrivatePathRequired");
        CheckLinks(path);
        return path;
    }

    internal static void CheckLinks(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("ExperiencePathLinkRejected");
    }

    private static void SeparateRoots(string source, string output)
    {
        source = Path.TrimEndingDirectorySeparator(source);
        output = Path.TrimEndingDirectorySeparator(output);
        static bool Within(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (Within(source, output) || Within(output, source)) throw new DispatchExperienceException("ExperienceSourceOutputOverlap");
    }

    private static string ErrorCode(Exception e) => e is DispatchExperienceException known ? known.Code : e switch
    {
        JsonException => "ExperienceJsonInvalid",
        UnauthorizedAccessException => "ExperienceAccessDenied",
        ArgumentException => "ExperienceArgumentsInvalid",
        _ => "ExperienceIoFailure"
    };

}
