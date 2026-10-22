using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Notifications;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;

return await Verification.RunAsync(args);

internal static partial class Verification
{
    private const string AppKey = "ssalddel.shipper";
    private const string PackageId = "com.companyname.ssalddelapp";
    private const string InputSchema = "workflow-progress-device-input.r7";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { WriteIndented = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    internal static async Task<int> RunAsync(string[] args)
    {
        string? output = null;
        var ownsOutput = false;
        string stage = "Arguments";
        try
        {
            Require(args.Length == 3 && args[0] is "--validate" or "--send", "Arguments");
            var repository = RepositoryRoot();
            var runRoot = Path.Combine(repository, "artifacts", "local", "workflow-progress-device", "20261022-r1");
            var privateRoot = Path.Combine(runRoot, ".private");
            stage = "InputBoundary";
            var inputPath = Below(args[1], privateRoot);
            Require(File.Exists(inputPath) && new FileInfo(inputPath).Length is > 0 and <= 32768, "InputFileSize");
            NoReparse(inputPath);
            output = Below(args[2], runRoot);
            Require(!IsBelow(output, privateRoot), "OutputPrivateBoundary");
            Require(!Directory.Exists(output) && !File.Exists(output), "OutputAlreadyExists");
            NoReparse(Path.GetDirectoryName(output)!);
            var bytes = await File.ReadAllBytesAsync(inputPath);
            var input = JsonSerializer.Deserialize<DeviceInput>(bytes, Json) ?? throw new VerificationFailure("InputMissing");
            stage = "SampleValidation";
            var data = Validate(input, DateTimeOffset.UtcNow);
            stage = "CredentialBinding";
            var options = Credentials(repository, input.FirebaseConfigurationPath);
            Directory.CreateDirectory(output);
            ownsOutput = true;
            await SaveNew(Path.Combine(output, "validation.json"), new
            {
                schemaVersion = "workflow-progress-device-validation.r7", input.SampleId, input.StageCode,
                input.SourceKind, input.InstallationAppVersion, input.ActiveInstallationCount,
                installationSnapshotOnly = true, tokenHash = input.TokenSha256,
                inputSha256 = Hash(bytes), sdkAndServerProjectMatched = true, androidPackageMatched = true,
                productionPayloadParsed = true, credentialsRecorded = false, actualDeviceReceived = false,
                atUtc = DateTimeOffset.UtcNow
            });
            if (args[0] == "--validate")
            {
                Console.WriteLine("WorkflowProgressDeviceValidationPassed; no network request or notification sent.");
                return 0;
            }

            // The lock is shared across output directories. An uncertain provider response is never retried.
            // An intentional duplicate event uses a NEW sampleId and keeps the original eventId explicitly.
            stage = "AttemptReservation";
            var attemptRoot = Path.Combine(privateRoot, "send-attempts");
            NoReparse(privateRoot);
            Directory.CreateDirectory(attemptRoot);
            NoReparse(attemptRoot);
            await SaveNew(Path.Combine(attemptRoot, input.SampleId + ".json"), new
            {
                schemaVersion = "workflow-progress-device-attempt.r7", status = "AttemptStarted",
                input.SampleId, input.EventId, input.StageCode, inputSha256 = Hash(bytes),
                tokenHash = input.TokenSha256, input.InstallationId, atUtc = DateTimeOffset.UtcNow,
                ambiguousFailureMustNotRetry = true
            });
            stage = "ProviderRequest";
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var http = new HttpClient(new FcmOnlyHandler(options.ProjectId)) { Timeout = TimeSpan.FromSeconds(30) };
            var sender = new FirebaseFcmPushService(http, Options.Create(options), NullLogger<FirebaseFcmPushService>.Instance);
            var copy = WorkflowProgressNotification.Get(input.StageCode);
            var accepted = await sender.SendAsync(new(input.Token, copy.Title, copy.Body, data,
                HighPriority: true, DataOnly: true), cancellation.Token);
            await SaveNew(Path.Combine(output, "provider-result.json"), new
            {
                schemaVersion = "workflow-progress-device-provider-result.r7", input.SampleId, input.StageCode,
                providerAccepted = accepted, tokenHash = input.TokenSha256, input.InstallationAppVersion,
                productSenderUsed = true, dataOnly = true, actualDeviceReceived = false,
                soundHeard = (bool?)null, notificationTapped = false, businessStateChanged = false,
                productOutboxMarkedSent = false, atUtc = DateTimeOffset.UtcNow
            });
            Require(accepted, "ProviderRejected");
            Console.WriteLine("WorkflowProgressDeviceProviderAccepted; device receipt, sound and tap remain separate verification.");
            return 0;
        }
        catch (Exception error)
        {
            var code = error is VerificationFailure known ? known.Code
                : error is IOException ? "FileOrAttemptUnavailable"
                : error is OperationCanceledException ? "ProviderOutcomeUnknown"
                : "VerificationFailed";
            if (ownsOutput && output is not null && Directory.Exists(output))
            {
                try { await SaveNew(Path.Combine(output, "failure.json"), new
                    { schemaVersion = "workflow-progress-device-failure.r7", stage, code, atUtc = DateTimeOffset.UtcNow,
                        retryAllowed = false, providerOutcomeMayBeUnknown = stage == "ProviderRequest" }); }
                catch { /* Never overwrite prior evidence or reveal original values. */ }
            }
            Console.Error.WriteLine("WorkflowProgressDeviceFailed:" + code + "; stage=" + stage);
            return 1;
        }
    }

    private static Dictionary<string, string> Validate(DeviceInput input, DateTimeOffset now)
    {
        Require(input.SchemaVersion == InputSchema && input.SourceKind == "SyntheticDeviceSample", "SyntheticInputRequired");
        Require(SafeSample().IsMatch(input.SampleId), "SampleId");
        Require(input.AppKey == AppKey && input.PackageId == PackageId
            && input.InstallationAppKey == AppKey && input.InstallationPlatform == "Android", "InstallationAppMismatch");
        Require(input.InstallationActive && input.ActiveInstallationCount == 1, "ExactlyOneActiveInstallationRequired");
        Require(SafeOpaque(input.InstallationId) && SafeOpaque(input.InstallationAppVersion), "InstallationMetadata");
        Require(SafeOpaque(input.OwnerUserId) && input.InstallationOwnerUserId == input.OwnerUserId, "InstallationOwnerMismatch");
        Require(input.RegistrationVerifiedAtUtc >= now.AddMinutes(-10)
            && input.RegistrationVerifiedAtUtc <= now.AddMinutes(1), "RegistrationSnapshotStale");
        Require(input.Token.Length is >= 20 and <= 2000 && !input.Token.Any(char.IsWhiteSpace)
            && !input.Token.Any(char.IsControl) && SafeHex().IsMatch(input.TokenSha256)
            && Hash(Encoding.UTF8.GetBytes(input.Token)) == input.TokenSha256, "TokenBinding");
        Require(input.OccurredAtUtc >= now.AddMinutes(-10) && input.OccurredAtUtc <= now.AddMinutes(1), "SampleTime");
        Require(WorkflowProgressNotification.TryGet(input.StageCode, out var stage), "StageCode");
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = WorkflowProgressNotification.MessageType, ["eventId"] = input.EventId,
            ["workflowKey"] = stage.WorkflowKey, ["stageCode"] = stage.Code,
            ["sourceId"] = input.SourceId, ["userId"] = input.OwnerUserId,
            ["occurredAtUtc"] = input.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture), ["deepLink"] = input.DeepLink
        };
        Require(WorkflowProgressNotification.TryParse(data, out _), "ProductionPayloadRejected");
        return data;
    }

    private static PushNotificationsOptions Credentials(string repository, string sdkPath)
    {
        Require(Path.IsPathFullyQualified(sdkPath) && !IsBelow(sdkPath, repository), "ExternalSdkPathRequired");
        NoReparse(sdkPath);
        Require(File.Exists(sdkPath) && new FileInfo(sdkPath).Length <= 524288, "SdkConfigurationUnavailable");
        using var local = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "Ssalddel", "appsettings.Local.json")));
        var section = local.RootElement.GetProperty("PushNotifications");
        var credentialPath = Environment.ExpandEnvironmentVariables(section.GetProperty("ServiceAccountJsonPath").GetString() ?? "");
        if (!Path.IsPathFullyQualified(credentialPath)) credentialPath = Path.Combine(repository, "Ssalddel", credentialPath);
        credentialPath = Path.GetFullPath(credentialPath);
        Require(!IsBelow(credentialPath, repository) && File.Exists(credentialPath)
            && new FileInfo(credentialPath).Length <= 131072, "ExternalServerCredentialRequired");
        NoReparse(credentialPath);
        using var credentials = JsonDocument.Parse(File.ReadAllText(credentialPath));
        Require(credentials.RootElement.GetProperty("type").GetString() == "service_account", "ServiceAccountRequired");
        var project = credentials.RootElement.GetProperty("project_id").GetString();
        Require(project is not null && SafeProject().IsMatch(project), "ServerProject");
        using var sdk = JsonDocument.Parse(File.ReadAllText(sdkPath));
        Require(sdk.RootElement.GetProperty("project_info").GetProperty("project_id").GetString() == project
            && sdk.RootElement.GetProperty("client").EnumerateArray().Count(x => x.GetProperty("client_info")
                .GetProperty("android_client_info").GetProperty("package_name").GetString() == PackageId) == 1,
            "SdkProjectOrPackageMismatch");
        if (section.TryGetProperty("ProjectId", out var configured) && !string.IsNullOrWhiteSpace(configured.GetString()))
            Require(configured.GetString() == project, "ConfiguredServerProjectMismatch");
        return new() { ProjectId = project!, ServiceAccountJsonPath = credentialPath, ServerKey = string.Empty };
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel", "Ssalddel.csproj"))) return directory.FullName;
        throw new VerificationFailure("RepositoryWorkingDirectoryRequired");
    }
    private static bool SafeOpaque(string? value) => value is { Length: > 0 and <= 200 }
        && value == value.Trim() && !value.Any(char.IsControl);
    private static string Below(string path, string root)
    {
        var full = Path.GetFullPath(path);
        Require(IsBelow(full, root), "ArtifactPathBoundary");
        return full;
    }
    private static bool IsBelow(string path, string root) => Path.GetFullPath(path).StartsWith(
        Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase);
    private static void NoReparse(string path)
    {
        for (var current = new FileInfo(Path.GetFullPath(path)) as FileSystemInfo; current is not null;)
        {
            if (File.Exists(current.FullName) || Directory.Exists(current.FullName))
                Require((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) == 0, "ReparsePathRejected");
            var parent = Path.GetDirectoryName(current.FullName);
            if (parent is null || parent == current.FullName) break;
            current = new DirectoryInfo(parent);
        }
    }
    private static async Task SaveNew(string path, object value)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, value, Json);
        await stream.FlushAsync();
        stream.Flush(true);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool condition, string code) { if (!condition) throw new VerificationFailure(code); }
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)] private static partial Regex SafeSample();
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex SafeHex();
    [GeneratedRegex("^[a-z][a-z0-9-]{4,62}$", RegexOptions.CultureInvariant)] private static partial Regex SafeProject();
    private sealed class VerificationFailure(string code) : Exception { internal string Code => code; }
    private sealed class FcmOnlyHandler(string projectId) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Require(request.RequestUri is { Scheme: "https", Host: "fcm.googleapis.com", Port: 443 } uri
                && uri.AbsolutePath == $"/v1/projects/{projectId}/messages:send" && uri.Query.Length == 0
                && uri.UserInfo.Length == 0 && request.Method == HttpMethod.Post, "FcmHttpBoundary");
            return base.SendAsync(request, ct);
        }
    }
}

internal sealed record DeviceInput
{
    public string SchemaVersion { get; init; } = "";
    public string SampleId { get; init; } = "";
    public string OwnerUserId { get; init; } = "";
    public string AppKey { get; init; } = "";
    public string PackageId { get; init; } = "";
    public string InstallationId { get; init; } = "";
    public string InstallationOwnerUserId { get; init; } = "";
    public string InstallationAppKey { get; init; } = "";
    public string InstallationPlatform { get; init; } = "";
    public bool InstallationActive { get; init; }
    public int ActiveInstallationCount { get; init; }
    public string InstallationAppVersion { get; init; } = "";
    public DateTimeOffset RegistrationVerifiedAtUtc { get; init; }
    public string Token { get; init; } = "";
    public string TokenSha256 { get; init; } = "";
    public string FirebaseConfigurationPath { get; init; } = "";
    public string SourceKind { get; init; } = "";
    public string StageCode { get; init; } = "";
    public string EventId { get; init; } = "";
    public string SourceId { get; init; } = "";
    public string DeepLink { get; init; } = "";
    public DateTimeOffset OccurredAtUtc { get; init; }
}
