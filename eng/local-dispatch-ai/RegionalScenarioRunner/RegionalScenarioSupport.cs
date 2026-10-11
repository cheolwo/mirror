using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;

internal static class EvidenceFiles
{
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static void Save(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
    }
}

// Preview uses declared geometric distances, never a paid navigation API or real GPS.
internal sealed class RegionalGeometricRoutes : I배차추천경로Service
{
    public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
    public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? target) => Task.FromResult<배차경로예상결과?>(null);
    public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => Task.FromResult<배차경로예상결과?>(null);
    public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
    public decimal? CalculateDistanceKm(배차경로좌표 a, 배차경로좌표 b)
        => Math.Abs(a.Latitude - b.Latitude) * 111m + Math.Abs(a.Longitude - b.Longitude) * 88m;
}

// The runner explicitly supplies policy output; accidental operational snapshot reads fail.
internal sealed class NoOperationalDemand : I음식배달수요참여Service
{
    public Task<음식배달수요참여결과?> 평가Async(운송원장 queue, CancellationToken ct = default)
        => throw new InvalidOperationException("Regional scenario must use its declared synthetic demand snapshot.");
}

internal sealed class RegionalModelTransport(bool actual, string root, string runId, string caseId, string output) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public bool Actual => actual;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Calls != 0 || request.Method != HttpMethod.Post || request.RequestUri != new Uri("http://127.0.0.1:12681/api/chat"))
            throw new InvalidOperationException("Only one fixed-loopback model call is allowed per case.");
        var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
        using var json = JsonDocument.Parse(bytes);
        if (json.RootElement.GetProperty("model").GetString() != "qwen3:1.7b" || json.RootElement.GetProperty("stream").GetBoolean())
            throw new InvalidOperationException("Unexpected model configuration.");
        var key = json.RootElement.GetProperty("format").GetProperty("required")[0].GetString();
        if (key is not ("candidateToken" or "planToken")) throw new InvalidOperationException("Unknown bounded selection contract.");
        if (actual) Reserve();
        Calls++;
        File.WriteAllBytes(Path.Combine(output, "model-request.json"), bytes);
        HttpResponseMessage response;
        if (actual)
        {
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
            using var forward = new HttpRequestMessage(HttpMethod.Post, request.RequestUri) { Content = new ByteArrayContent(bytes) };
            forward.Content.Headers.ContentType = new("application/json");
            response = await client.SendAsync(forward, HttpCompletionOption.ResponseHeadersRead, ct);
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[65537]; var length = 0;
            while (length < buffer.Length)
            {
                var read = await body.ReadAsync(buffer.AsMemory(length), ct);
                if (read == 0) break;
                length += read;
            }
            response.Content.Dispose(); response.Content = new ByteArrayContent(buffer.AsSpan(0, length).ToArray());
        }
        else
        {
            var selection = JsonSerializer.Serialize(new Dictionary<string, string> { [key!] = key == "planToken" ? "P1" : "C1" });
            response = new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { model = "qwen3:1.7b", done = true,
                message = new { role = "assistant", content = selection } }), Encoding.UTF8, "application/json") };
        }
        var responseBytes = await response.Content.ReadAsByteArrayAsync(ct);
        File.WriteAllBytes(Path.Combine(output, "model-response.json"), responseBytes);
        EvidenceFiles.Save(Path.Combine(output, "model-exchange.json"), new { actual, requestSha256 = EvidenceFiles.Hash(bytes),
            responseSha256 = EvidenceFiles.Hash(responseBytes), status = (int)response.StatusCode });
        return response;
    }

    private void Reserve()
    {
        var attempts = Path.Combine(root, "attempts"); Directory.CreateDirectory(attempts);
        var lockPath = Path.Combine(attempts, "reservation.lock");
        using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        try
        {
            for (var index = 1; index <= 6; index++)
            {
                var path = Path.Combine(attempts, $"attempt-{index:000}.json");
                try
                {
                    EvidenceFiles.Save(path, new { runId, caseId, reservedAtUtc = DateTime.UtcNow, cumulativeMaximum = 6,
                        includesFailedAndInterrupted = true, priorBudgetsReused = false });
                    return;
                }
                catch (IOException) when (File.Exists(path)) { }
            }
            throw new InvalidOperationException("r50 cumulative model budget exhausted.");
        }
        finally { lease.Dispose(); }
    }
}
