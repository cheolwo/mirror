using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using CargoAiClientProbeR114;
using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace CargoOrderCloseoutR70;

/// <summary>선택 의뢰의 실제 공통 VM 수락 POST 한 번만 엽니다. 다른 업무 변경은 기존 읽기 transport가 차단합니다.</summary>
internal sealed class AcceptanceApi(ProbeInput input, ReadOnlyProbeApi reads, string repository) : IRoleWorkspaceApi, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
    { BaseAddress = new(input.BaseAddress), Timeout = TimeSpan.FromSeconds(30) };
    private bool attempted;
    internal int PostAttempts { get; private set; }
    internal string? JournalState { get; private set; }
    internal int? HttpStatus { get; private set; }
    internal bool? ResponseMatchesRequest { get; private set; }

    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        => reads.GetAsync<T>(roleKey, path, cancellationToken);

    public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
    {
        var expectedPath = "api/v1/driver/dispatch-actions/" + Uri.EscapeDataString(input.RequestId) + "/accept";
        if (input.Mode != "accept" || roleKey != RoleWorkspaceCatalog.CargoDriver || path != expectedPath
            || body is not 기사화물배차수락요청 || typeof(T) != typeof(기사배차처리응답) || attempted)
            throw new InvalidOperationException("CloseoutMutationOutsideSelectedAcceptance");
        attempted = true;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), Json);
        var payloadSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var commandPath = Path.Combine(input.OutputRoot, "accept-command.json");
        var guardPath = Path.Combine(repository, "artifacts", "local", "cargo-order-closeout-r70", ".private", "accept-intents", input.RequestId + ".json");
        var record = new AcceptanceJournal
        {
            State = "Pending", RequestId = input.RequestId, DriverId = input.DriverId, Method = "POST", Path = expectedPath,
            PayloadSha256 = payloadSha, Payload = JsonSerializer.Deserialize<JsonElement>(bytes), StartedAtUtc = DateTimeOffset.UtcNow
        };
        Directory.CreateDirectory(Path.GetDirectoryName(guardPath)!);
        // 요청 ID 단위의 배타 기록도 남겨 다른 출력 폴더로 같은 수락을 다시 보내는 것을 막습니다.
        WriteNew(guardPath, record);
        WriteNew(commandPath, record);
        JournalState = record.State;
        try
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent(bytes) };
            request.Content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
            PostAttempts++;
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            HttpStatus = (int)response.StatusCode;
            var responseBytes = await ReadBoundedAsync(response.Content, cancellationToken);
            var responseSha = Convert.ToHexString(SHA256.HashData(responseBytes)).ToLowerInvariant();
            기사배차처리응답? typed = null;
            try { typed = JsonSerializer.Deserialize<기사배차처리응답>(responseBytes, Json); }
            catch (JsonException) { }
            ResponseMatchesRequest = typed?.RequestId == input.RequestId;
            record = record with
            {
                State = HttpStatus == 200 ? "Confirmed" : "Rejected", HttpStatus = HttpStatus,
                HttpResponseConfirmed = HttpStatus == 200, ResponseMatchesRequest = ResponseMatchesRequest,
                ResponseSha256 = responseSha, Response = typed is null ? null : new(typed.RequestId, typed.Message), EndedAtUtc = DateTimeOffset.UtcNow
            };
            WriteUpdate(commandPath, record); WriteUpdate(guardPath, record); JournalState = record.State;
            if (HttpStatus != 200) throw new RoleWorkspaceAccessException(HttpStatus.Value, "수락 처리 결과를 확인하지 못했습니다.");
            if (typed is null) throw new InvalidOperationException("CloseoutAcceptanceResponseInvalid");
            return (T)(object)typed;
        }
        catch
        {
            if (record.State == "Pending")
            {
                record = record with { State = "Unknown", HttpStatus = HttpStatus, EndedAtUtc = DateTimeOffset.UtcNow };
                WriteUpdate(commandPath, record); WriteUpdate(guardPath, record); JournalState = record.State;
            }
            throw;
        }
    }
    public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        => reads.PutAsync<T>(roleKey, path, body, cancellationToken);
    public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
        => reads.UploadAsync<T>(roleKey, path, body, cancellationToken);
    public void Dispose() => http.Dispose();

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength is > 65536) throw new InvalidOperationException("CloseoutResponseTooLarge");
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var bytes = new MemoryStream(); var buffer = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, ct); if (count == 0) break;
            if (bytes.Length + count > 65536) throw new InvalidOperationException("CloseoutResponseTooLarge");
            bytes.Write(buffer, 0, count);
        }
        return bytes.ToArray();
    }
    private static void WriteNew(string path, AcceptanceJournal record)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        JsonSerializer.Serialize(file, record, Json); file.Flush(flushToDisk: true);
    }
    private static void WriteUpdate(string path, AcceptanceJournal record)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        WriteNew(temp, record); File.Move(temp, path, overwrite: true);
    }
    private sealed record AcceptanceResponse(string RequestId, string Message);
    private sealed record AcceptanceJournal
    {
        public string State { get; init; } = "Pending";
        public string RequestId { get; init; } = "";
        public string DriverId { get; init; } = "";
        public string Method { get; init; } = "POST";
        public string Path { get; init; } = "";
        public string PayloadSha256 { get; init; } = "";
        public JsonElement Payload { get; init; }
        public DateTimeOffset StartedAtUtc { get; init; }
        public DateTimeOffset? EndedAtUtc { get; init; }
        public int? HttpStatus { get; init; }
        public bool HttpResponseConfirmed { get; init; }
        public bool? ResponseMatchesRequest { get; init; }
        public string? ResponseSha256 { get; init; }
        public AcceptanceResponse? Response { get; init; }
    }
}
