using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using 살뜰.Services.Options;

namespace 살뜰.Services.External.Toss;

public interface ITossPaymentsService
{
    Task<TossConfirmResult> ConfirmAsync(TossConfirmApiRequest request);
    Task<TossConfirmResult> ConfirmAsync(TossConfirmApiRequest request, CancellationToken ct) => ConfirmAsync(request);
    Task<TossConfirmResult> GetAsync(string paymentKey, CancellationToken ct = default)
        => Task.FromResult(new TossConfirmResult(false, "{\"code\":\"PaymentLookupUnavailable\"}", null, IsUncertain: true));
}

public sealed class TossPaymentsService(HttpClient httpClient, IOptions<TossPaymentsOptions> options) : ITossPaymentsService
{
    public Task<TossConfirmResult> ConfirmAsync(TossConfirmApiRequest request) => ConfirmAsync(request, default);
    public Task<TossConfirmResult> ConfirmAsync(TossConfirmApiRequest request, CancellationToken ct)
        => SendAsync(HttpMethod.Post, "/v1/payments/confirm", request, ct);
    public Task<TossConfirmResult> GetAsync(string paymentKey, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(paymentKey) || paymentKey.Length > 200
            ? Task.FromResult(new TossConfirmResult(false, "{\"code\":\"InvalidPaymentLookup\"}", null))
            : SendAsync(HttpMethod.Get, "/v1/payments/" + Uri.EscapeDataString(paymentKey), null, ct);
    private async Task<TossConfirmResult> SendAsync(HttpMethod method, string path, TossConfirmApiRequest? input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(options.Value.SecretKey)) return new(false, "{\"code\":\"PaymentProviderNotConfigured\"}", null);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(options.Value.SecretKey + ":")));
        if (input is not null) request.Content = JsonContent.Create(new { paymentKey = input.PaymentKey, orderId = input.OrderId, amount = input.Amount });
        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            var sanitized = TossPaymentResponsePolicy.Sanitize(raw, out var secretHash);
            if (!response.IsSuccessStatusCode)
                return new(false, sanitized, null, IsUncertain: (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout);
            if (!TossPaymentResponsePolicy.TryRead(sanitized, out var fact)) return new(false, "{\"code\":\"ProviderResponseInvalid\"}", null, IsUncertain: true);
            return new(true, sanitized, fact!.Method, fact.Status, fact.PaymentKey, fact.OrderId, fact.TotalAmount, fact.Currency,
                DepositCallbackVerificationHash: secretHash);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return new(false, "{\"code\":\"PaymentOutcomeUncertain\"}", null, IsUncertain: true); }
        catch (HttpRequestException)
        { return new(false, "{\"code\":\"PaymentOutcomeUncertain\"}", null, IsUncertain: true); }
    }
}

public sealed record TossConfirmApiRequest(string PaymentKey, string OrderId, int Amount);
public sealed record TossConfirmResult(bool IsSuccess, string ResponseJson, string? PaymentMethod,
    string? ProviderStatus = null, string? PaymentKey = null, string? OrderId = null, int? Amount = null,
    string? Currency = null, bool IsUncertain = false, string? DepositCallbackVerificationHash = null);
public sealed record TossPaymentFact(string Status, string PaymentKey, string OrderId, int TotalAmount, string Currency, string? Method, int? BalanceAmount);

/// <summary>HTTP 성공을 수납으로 해석하지 않습니다. PG 정본의 식별값·금액·통화를 별도로 대조합니다.</summary>
public static class TossPaymentResponsePolicy
{
    public static bool TryRead(string json, out TossPaymentFact? fact)
    {
        fact = null;
        try
        {
            using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !String(root, "status", out var status)
                || status is not ("READY" or "IN_PROGRESS" or "WAITING_FOR_DEPOSIT" or "DONE" or "CANCELED" or "PARTIAL_CANCELED" or "ABORTED" or "EXPIRED")
                || !String(root, "paymentKey", out var key) || key.Length > 200 || !String(root, "orderId", out var order) || order.Length > 128
                || !root.TryGetProperty("totalAmount", out var amount) || amount.ValueKind != JsonValueKind.Number || !amount.TryGetInt32(out var total) || total <= 0
                || !String(root, "currency", out var currency) || currency != "KRW") return false;
            int? balance = null;
            if (root.TryGetProperty("balanceAmount", out var b))
            { if (b.ValueKind != JsonValueKind.Number || !b.TryGetInt32(out var value) || value < 0 || value > total) return false; balance = value; }
            if (status == "DONE" && balance is { } remaining && remaining != total) return false;
            if (status == "DONE" && root.TryGetProperty("cancels", out var cancels) && cancels.ValueKind == JsonValueKind.Array && cancels.GetArrayLength() != 0) return false;
            fact = new(status, key, order, total, currency, root.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null, balance);
            return true;
        }
        catch (JsonException) { return false; }
    }
    public static string Sanitize(string raw, out string? secretHash)
    {
        secretHash = null;
        try
        {
            var root = JsonNode.Parse(raw);
            if (root is not JsonObject obj) return "{\"code\":\"ProviderResponseInvalid\"}";
            if (obj["secret"] is JsonValue value && value.TryGetValue<string>(out var secret) && !string.IsNullOrWhiteSpace(secret)) secretHash = HashSecret(secret);
            RemoveSecrets(root); return root.ToJsonString();
        }
        catch (JsonException) { return "{\"code\":\"ProviderResponseInvalid\"}"; }
    }
    public static string HashSecret(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
    private static void RemoveSecrets(JsonNode? node)
    {
        if (node is JsonObject obj)
        { foreach (var key in obj.Select(x => x.Key).ToArray()) if (string.Equals(key, "secret", StringComparison.OrdinalIgnoreCase)) obj.Remove(key); else RemoveSecrets(obj[key]); }
        else if (node is JsonArray array) foreach (var item in array) RemoveSecrets(item);
    }
    private static bool String(JsonElement root, string name, out string value)
    { value = string.Empty; if (!root.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String) return false; value = p.GetString() ?? ""; return !string.IsNullOrWhiteSpace(value); }
}
