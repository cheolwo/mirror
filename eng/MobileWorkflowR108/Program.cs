using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace MobileWorkflowR108;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2) { Console.WriteLine("Usage: MobileWorkflowR108 <private-input.json> <new-artifacts-output>"); return 2; }
        var root = FindRoot();
        var output = Path.GetFullPath(args[1]);
        if (!output.StartsWith(Path.Combine(root, "artifacts", "local") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output)) throw new InvalidOperationException("A fresh artifacts/local output is required.");
        var inputPath = Path.GetFullPath(args[0]);
        if (!inputPath.Contains(Path.DirectorySeparatorChar + ".private" + Path.DirectorySeparatorChar)) throw new InvalidOperationException("Private input path required.");
        var input = JsonSerializer.Deserialize<Input>(await File.ReadAllTextAsync(inputPath), Json) ?? throw new InvalidDataException();
        var origin = new Uri(input.BaseAddress);
        if (origin.Scheme != "http" || origin.Host != "127.0.0.1" || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.UserInfo))
            throw new InvalidOperationException("Credential-free loopback origin required.");
        Directory.CreateDirectory(output);
        var checks = new List<object>(); var captures = new List<object>();
        var privateRoot = Path.Combine(output, ".private"); Directory.CreateDirectory(privateRoot);
        var protector = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(privateRoot, "keys")))
            .CreateProtector("MobileWorkflowR108.Pending");
        var pendingFile = Path.Combine(privateRoot, "pending.protected");
        var actor = new Actor(input.Owner, input.AccessToken, input.PickupLatitude, input.PickupLongitude, input.SamplePhotoPath);
        var fault = new ResponseLossHandler(origin) { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false } };
        using var http = new HttpClient(fault) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(30) };
        var js = new ProductEncryptionJs(root, input.NodeExecutable);
        await using var encryption = new SsalddelIsmsPClientEncryptionService(js);
        var api = new SsalddelJsonApiClient(new SsalddelProtectedApiClient(http, encryption, actor));
        var client = new NeighborhoodMemberDeliveryClient(api);
        NeighborhoodMemberDeliverySession Session() => new(new ProtectedFilePendingStore(pendingFile, protector));
        NeighborhoodMemberOffersViewModel Offers() => new(client, actor, Session(), actor);
        NeighborhoodMemberWorkViewModel Work() => new(client, actor, Session(), actor);
        void Check(bool value, string name) { if (!value) throw new CheckFailure(name); checks.Add(new { name, passed = true }); }
        void SavedPending(NeighborhoodMemberPageViewModel vm, string kind)
        {
            Check(vm.Pending?.Kind == kind, kind + ": uncertain command retained");
            Check(File.Exists(pendingFile), kind + ": protected pending persisted");
            var encrypted = File.ReadAllText(pendingFile);
            Check(!encrypted.Contains("ContentBase64", StringComparison.OrdinalIgnoreCase) && !encrypted.Contains(input.AccessToken, StringComparison.Ordinal), kind + ": pending file excludes photo and token plaintext");
            var clear = protector.Unprotect(encrypted);
            Check(!clear.Contains("ContentBase64", StringComparison.OrdinalIgnoreCase), kind + ": decrypted recovery metadata excludes photo bytes");
        }
        string? requestId = null; var status = "Failed"; string? failureCode = null;
        var resumeExisting = !string.IsNullOrWhiteSpace(input.ResumeRequestId);
        var expectedSuccessfulLosses = resumeExisting ? 4 : 5;
        object? previousAttempt = null;
        try
        {
            if (resumeExisting)
            {
                requestId = input.ResumeRequestId;
                var previousPath = Path.GetFullPath(input.PreviousResultPath ?? throw new CheckFailure("resume previous evidence required"));
                Check(previousPath.StartsWith(Path.Combine(root, "artifacts", "local") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                    "resume origin stays in local verification evidence");
                var previousBytes = await File.ReadAllBytesAsync(previousPath);
                using var previous = JsonDocument.Parse(previousBytes);
                Check(previous.RootElement.GetProperty("requestId").GetString() == requestId
                    && previous.RootElement.GetProperty("http").EnumerateArray().Any(call => call.GetProperty("path").GetString()!.EndsWith("/accept", StringComparison.Ordinal)
                        && call.GetProperty("status").GetInt32() == 200 && call.GetProperty("successfulResponseDiscarded").GetBoolean()),
                    "same request retains its earlier accepted-response-loss evidence");
                previousAttempt = new { file = Path.GetRelativePath(root, previousPath), sha256 = Convert.ToHexString(SHA256.HashData(previousBytes)).ToLowerInvariant(),
                    acceptanceRepeated = false, priorAttemptRewritten = false };
                using var existing = Work(); await existing.LoadAsync(requestId);
                Check(existing.Loaded && existing.Work is { StateCode: "Assigned", PickupEvidenceId: null, DeliveryEvidenceId: null }
                    && existing.Pending is null && !existing.CanComplete, "same assigned work remains uncompleted after rejected invalid-photo fixture");
            }
            else
            {
                using (var offers = Offers())
                {
                    await offers.LoadAsync(input.OfferId); Check(offers.Loaded && offers.CanAccept, "current offer loaded through actual product HTTP client");
                    fault.Arm("/accept");
                    var accepted = await offers.AcceptAsync();
                    Check(fault.Losses == 1, "acceptance success response reached and discarded at client boundary");
                    Check(accepted is null, "lost acceptance response is not shown as confirmed");
                    SavedPending(offers, "accept");
                }
                var acceptWrites = fault.WriteCount;
                using var restored = Offers();
                await restored.LoadAsync(input.OfferId); requestId = restored.AcceptedRequestId;
                Check(requestId is not null && restored.Pending is null, "new VM/session recovers accepted work by canonical GET");
                Check(fault.WriteCount == acceptWrites, "accept recovery sends no new business mutation");
            }
            var writes = fault.WriteCount;
            foreach (var stage in new[] { "Pickup", "Delivery" })
            {
                using (var work = Work())
                {
                    await work.LoadAsync(requestId);
                    Check(work.Loaded && work.Stage == stage && !work.CanComplete, stage + ": completion blocked before evidence");
                    await work.CaptureAsync(); Check(work.Photo is not null && work.CanComplete, stage + ": synthetic camera adapter attaches photo");
                    fault.Arm("/evidence"); await work.CompleteAsync(); SavedPending(work, "evidence");
                }
                writes = fault.WriteCount;
                using (var recovered = Work())
                {
                    await recovered.LoadAsync(requestId);
                    Check(recovered.Loaded && recovered.Pending is null && recovered.CanComplete && recovered.Photo is null,
                        stage + ": new VM/session reconciles photo receipt without recapture");
                    Check(fault.WriteCount == writes, stage + ": photo recovery uses GET only");
                    fault.Arm(stage == "Pickup" ? "/pickup" : "/deliver");
                    await recovered.CompleteAsync(); SavedPending(recovered, stage == "Pickup" ? "pickup" : "deliver");
                }
                writes = fault.WriteCount;
                using var final = Work(); await final.LoadAsync(requestId);
                Check(final.Loaded && final.Pending is null && final.Work?.StateCode == (stage == "Pickup" ? "PickedUp" : "Delivered"),
                    stage + ": completion response loss reconciled from canonical state");
                Check(fault.WriteCount == writes, stage + ": completion recovery sends no new mutation");
                captures.Add(new { stage, state = final.Work?.StateCode, final.Work?.Revision, pending = final.Pending is not null,
                    final.Work?.FareKrw, receiptConfirmed = final.Work?.ReceiptConfirmedAtUtc is not null });
            }
            using (var done = Work())
            {
                await done.LoadAsync(requestId);
                Check(done.Work is { Pickup: null, Dropoff: null, RequestNotes: null }, "completed work hides private place and request details");
                Check(done.Work?.ReceiptConfirmedAtUtc is null, "delivery remains separate from recipient confirmation");
            }
            Check(fault.Losses == expectedSuccessfulLosses, "listed actual committed success responses discarded only at client boundary");
            Check(fault.WriteCount == expectedSuccessfulLosses, "only expected evidence/completion writes and any new acceptance; no duplicate POST");
            Check(!File.Exists(pendingFile), "all recovered pending operations cleared");
            var missingReceipt = await client.EvidenceReceiptAsync("r108-missing-work", Guid.NewGuid(), CancellationToken.None);
            Check(missingReceipt is null, "actual API empty404 remains an absent photo receipt; no work-existence inference");
            status = "PassedWithinListedScope";
        }
        catch (CheckFailure ex) { failureCode = ex.Message; }
        catch (Exception ex) { failureCode = ex.GetType().Name; }
        var result = new { schemaVersion = "mobile-http-vm-recovery.r108", status, failureCode, checkedAtUtc = DateTime.UtcNow,
            productClient = nameof(NeighborhoodMemberDeliveryClient), productJsonClient = nameof(SsalddelJsonApiClient), actualHttp = true,
            transportProtectionDeterminedByProductContract = true,
            encryptedProductTransportModuleExecutedInNode = js.EncryptionCalls > 0, encryptionCalls = js.EncryptionCalls,
            productTransportModuleSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(root, "Ssalddel.Ui.Common", "wwwroot", "Areas", "App", "js", "ssalddel-isms-p-transport.js")))).ToLowerInvariant(),
            consumingUiAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(NeighborhoodMemberWorkViewModel).Assembly.Location))).ToLowerInvariant(),
            syntheticGps = true, syntheticCamera = true,
            persistentPendingAdapter = "Local DataProtection file; not MAUI SecureStorage", vmAndSessionRecreated = true,
            processRestarted = false, resumeExisting, previousAttempt, checks, captures, http = fault.Calls, losses = fault.Losses, writes = fault.WriteCount,
            requestId, physicalDeviceVerified = false, apkInstalled = false, googleFcmSent = false, actualPayment = false,
            fullOrderOrReceiptCompleted = false, credentialsRecorded = false };
        await File.WriteAllTextAsync(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(result, Json));
        Console.WriteLine(JsonSerializer.Serialize(new { status, failureCode, checks = checks.Count, losses = fault.Losses, writes = fault.WriteCount }, Json));
        return status == "PassedWithinListedScope" ? 0 : 1;
    }
    private static string FindRoot() { var d = new DirectoryInfo(Environment.CurrentDirectory); while (d is not null) { if (File.Exists(Path.Combine(d.FullName, "Ssalddel.v3.5.slnx"))) return d.FullName; d = d.Parent; } throw new InvalidOperationException("Repository root required."); }
    private sealed class CheckFailure(string code) : Exception(code);
    private sealed record Input(string BaseAddress, string Owner, string AccessToken, string OfferId, decimal PickupLatitude, decimal PickupLongitude, string SamplePhotoPath, string NodeExecutable,
        string? ResumeRequestId = null, string? PreviousResultPath = null);
}

internal sealed class Actor(string owner, string token, decimal lat, decimal lng, string photoPath)
    : ISsalddel현재사용자Context, ISsalddelAccessTokenProvider, IRoleWorkspaceLocationProvider, I업무사진촬영Adapter
{
    public 현재사용자Snapshot 현재사용자 => new(owner, "r108 synthetic member", []);
    public string AuthenticationOwnerId => owner;
    public string AccessToken => token;
    public bool IsSupported => true;
    public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct) => Task.FromResult<RoleWorkspaceLocation?>(new((double)lat, (double)lng, 5, DateTimeOffset.UtcNow));
    public async Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct)
        => new(Path.GetFileName(photoPath), Path.GetExtension(photoPath).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg", await File.ReadAllBytesAsync(photoPath, ct));
}

internal sealed class ProtectedFilePendingStore(string file, IDataProtector protector) : INeighborhoodMemberPendingStore
{
    public async Task<NeighborhoodMemberPendingOperation?> LoadAsync(string owner, CancellationToken ct)
    {
        if (!File.Exists(file)) return null;
        var value = JsonSerializer.Deserialize<NeighborhoodMemberPendingOperation>(protector.Unprotect(await File.ReadAllTextAsync(file, ct)), Program.Json);
        return value?.Owner == owner ? value : null;
    }
    public Task SaveAsync(NeighborhoodMemberPendingOperation value, CancellationToken ct)
        => File.WriteAllTextAsync(file, protector.Protect(JsonSerializer.Serialize(value, Program.Json)), ct);
    public Task RemoveAsync(string owner, CancellationToken ct) { ct.ThrowIfCancellationRequested(); File.Delete(file); return Task.CompletedTask; }
}

internal sealed class ResponseLossHandler(Uri origin) : DelegatingHandler
{
    private string? armed;
    public int WriteCount { get; private set; }
    public int Losses { get; private set; }
    public List<object> Calls { get; } = [];
    public void Arm(string suffix) { if (armed is not null) throw new InvalidOperationException("Unconsumed response loss"); armed = suffix; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        if (uri.Scheme != origin.Scheme || uri.Host != origin.Host || uri.Port != origin.Port) throw new InvalidOperationException("Loopback target changed.");
        var response = await base.SendAsync(request, ct); await response.Content.LoadIntoBufferAsync(ct);
        var write = request.Method != HttpMethod.Get; if (write) WriteCount++;
        var discard = write && armed is not null && uri.AbsolutePath.EndsWith(armed, StringComparison.Ordinal) && response.IsSuccessStatusCode;
        string? errorCode = null;
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if ((body.RootElement.TryGetProperty("errorCode", out var field) || body.RootElement.TryGetProperty("code", out field)) && field.ValueKind == JsonValueKind.String)
                {
                    var code = field.GetString();
                    if (code is not null && System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9_.-]{1,120}$")) errorCode = code;
                }
            }
            catch (JsonException) { }
        }
        Calls.Add(new { method = request.Method.Method, path = uri.AbsolutePath, status = (int)response.StatusCode, errorCode, successfulResponseDiscarded = discard });
        if (discard) { armed = null; Losses++; response.Dispose(); throw new HttpRequestException("r108 simulated response loss after server success"); }
        return response;
    }
}

internal sealed class ProductEncryptionJs(string root, string node) : IJSRuntime, IJSObjectReference
{
    public int EncryptionCalls { get; private set; }
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
    {
        if (identifier == "import") return (TValue)(object)(IJSObjectReference)this;
        if (identifier != "encryptJson") throw new NotSupportedException(identifier);
        var start = new ProcessStartInfo(node) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root, "eng", "MobileWorkflowR108", "encrypt-product-module.mjs"));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Node start failed");
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { modulePath = Path.Combine(root, "Ssalddel.Ui.Common", "wwwroot", "Areas", "App", "js", "ssalddel-isms-p-transport.js"), args }, Program.Json));
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct); _ = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException("Product transport module execution failed");
        var value = JsonSerializer.Deserialize<TValue>(await stdout, Program.Json) ?? throw new InvalidDataException("Encryption result absent");
        EncryptionCalls++; return value;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
