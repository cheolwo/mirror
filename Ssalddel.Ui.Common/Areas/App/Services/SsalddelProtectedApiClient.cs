using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Net;
using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public sealed class SsalddelProtectedApiClient
{
    private const string PublicKeyPath = "api/v1/security/isms-p/transport/public-key";
    private static readonly HttpRequestOptionsKey<string> OwnerKey = new("Ssalddel.AuthenticationOwner");

    private readonly HttpClient httpClient;
    private readonly SsalddelIsmsPClientEncryptionService encryptionService;
    private readonly ISsalddelAccessTokenProvider accessTokenProvider;
    private IsmsPClientEncryptionPublicKeyResponse? cachedPublicKey;

    public SsalddelProtectedApiClient(
        HttpClient httpClient,
        SsalddelIsmsPClientEncryptionService encryptionService,
        ISsalddelAccessTokenProvider accessTokenProvider)
    {
        this.httpClient = httpClient;
        this.encryptionService = encryptionService;
        this.accessTokenProvider = accessTokenProvider;
    }

    public async Task<HttpResponseMessage> GetAsync(
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestUri);
        return await SendAsync(HttpMethod.Get, requestUri, cancellationToken);
    }

    public async Task<HttpResponseMessage> DeleteAsync(
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestUri);
        using var message = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        await ApplyAuthorizationAsync(message, cancellationToken);
        return await httpClient.SendAsync(message, cancellationToken);
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken = default)
        => SendAsync(method, requestUri, headers: null, cancellationToken: cancellationToken);

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string requestUri,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestUri);
        using var message = new HttpRequestMessage(method, requestUri);
        await ApplyAuthorizationAsync(message, cancellationToken);
        ApplyHeaders(message, headers);
        var response = await httpClient.SendAsync(message, cancellationToken);
        if (method != HttpMethod.Get || response.StatusCode != HttpStatusCode.Unauthorized)
            return response;
        message.Options.TryGetValue(OwnerKey, out var owner);
        if (owner is not null && !string.Equals(owner, accessTokenProvider.AuthenticationOwnerId, StringComparison.Ordinal)) return response;
        string? refreshed;
        try { refreshed = await accessTokenProvider.RefreshAccessTokenAsync(message.Headers.Authorization?.Parameter, owner, cancellationToken); }
        catch { response.Dispose(); throw; }
        if (string.IsNullOrWhiteSpace(refreshed)
            || (owner is not null && !string.Equals(owner, accessTokenProvider.AuthenticationOwnerId, StringComparison.Ordinal))) return response;
        response.Dispose();
        using var retry = new HttpRequestMessage(method, requestUri);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.Trim());
        ApplyHeaders(retry, headers);
        return await httpClient.SendAsync(retry, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsProtectedJsonAsync<TRequest>(
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken = default)
        => await SendAsProtectedJsonAsync(HttpMethod.Post, requestUri, request, cancellationToken);

    public async Task<TResponse?> PostAsProtectedJsonAsync<TRequest, TResponse>(
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await PostAsProtectedJsonAsync(requestUri, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    public async Task<HttpResponseMessage> PutAsProtectedJsonAsync<TRequest>(
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken = default)
        => await SendAsProtectedJsonAsync(HttpMethod.Put, requestUri, request, cancellationToken);

    public async Task<HttpResponseMessage> PostAsync(
        string requestUri,
        HttpContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestUri);
        ArgumentNullException.ThrowIfNull(content);

        using var message = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        await ApplyAuthorizationAsync(message, cancellationToken);
        return await httpClient.SendAsync(message, cancellationToken);
    }

    public async Task<TResponse?> PutAsProtectedJsonAsync<TRequest, TResponse>(
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await PutAsProtectedJsonAsync(requestUri, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    public Task<HttpResponseMessage> SendAsProtectedJsonAsync<TRequest>(
        HttpMethod method,
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken = default)
        => SendAsProtectedJsonAsync(
            method,
            requestUri,
            request,
            headers: null,
            cancellationToken: cancellationToken);

    public async Task<HttpResponseMessage> SendAsProtectedJsonAsync<TRequest>(
        HttpMethod method,
        string requestUri,
        TRequest request,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestUri);

        using var message = new HttpRequestMessage(method, requestUri);
        await ApplyAuthorizationAsync(message, cancellationToken);
        ApplyHeaders(message, headers);
        message.Content = await CreateProtectedJsonContentAsync(requestUri, request, cancellationToken);
        EnsureRequestOwner(message);
        return await httpClient.SendAsync(message, cancellationToken);
    }

    private void EnsureRequestOwner(HttpRequestMessage message)
    {
        if (message.Options.TryGetValue(OwnerKey, out var owner)
            && !string.Equals(owner, accessTokenProvider.AuthenticationOwnerId, StringComparison.Ordinal))
            throw new HttpRequestException("계정이 변경되어 이전 요청을 중단했습니다.");
    }

    private async Task ApplyAuthorizationAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var originalOwner = accessTokenProvider.AuthenticationOwnerId;
        var token = (await accessTokenProvider.GetAccessTokenAsync(cancellationToken))?.Trim();
        var currentOwner = accessTokenProvider.AuthenticationOwnerId;
        if (originalOwner is not null && !string.Equals(originalOwner, currentOwner, StringComparison.Ordinal))
            throw new HttpRequestException("계정이 변경되어 이전 요청을 중단했습니다.");
        if (currentOwner is not null) message.Options.Set(OwnerKey, currentOwner);
        if (!string.IsNullOrWhiteSpace(token))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static void ApplyHeaders(
        HttpRequestMessage message,
        IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var (name, value) in headers)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (string.Equals(name, "Authorization", StringComparison.OrdinalIgnoreCase)
                || !message.Headers.TryAddWithoutValidation(name, value.Trim()))
            {
                throw new InvalidOperationException($"요청 헤더 '{name}'을(를) 적용할 수 없습니다.");
            }
        }
    }

    public async Task<JsonContent> CreateProtectedJsonContentAsync<TRequest>(
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!SsalddelIsmsPClientEncryptionService.RequiresEncryptedTransport<TRequest>())
        {
            return JsonContent.Create(request);
        }

        var publicKey = await GetPublicKeyAsync(cancellationToken);
        var envelope = await encryptionService.EncryptJsonAsync(
            publicKey,
            request,
            associatedData: requestUri);

        return JsonContent.Create(envelope);
    }

    private async Task<IsmsPClientEncryptionPublicKeyResponse> GetPublicKeyAsync(
        CancellationToken cancellationToken)
    {
        if (cachedPublicKey is not null &&
            cachedPublicKey.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return cachedPublicKey;
        }

        cachedPublicKey = await httpClient.GetFromJsonAsync<IsmsPClientEncryptionPublicKeyResponse>(
            PublicKeyPath,
            cancellationToken)
            ?? throw new InvalidOperationException("ISMS-P transport public key response was empty.");

        return cachedPublicKey;
    }
}
