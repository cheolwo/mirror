using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace HongdalApp.Services;

public interface I꾸미기보유권LocalStore
{
    Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken cancellationToken = default);
}

public sealed class Maui꾸미기보유권LocalStore : I꾸미기보유권LocalStore
{
    private const string StorageKeyPrefix = "hongdal.community-decoration-entitlements.v1.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<노드스티커보유권동기화Response?> LoadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storageKey = BuildStorageKey(userId);
        string? json;
        try
        {
            json = await SecureStorage.Default.GetAsync(storageKey);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SecureStorage.Default.Remove(storageKey);
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<노드스티커보유권동기화Response>(json, JsonOptions);
        }
        catch (JsonException)
        {
            SecureStorage.Default.Remove(storageKey);
            return null;
        }
    }

    public async Task SaveAsync(
        노드스티커보유권동기화Response snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await SecureStorage.Default.SetAsync(
                BuildStorageKey(snapshot.사용자UserId),
                JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SecureStorage.Default.Remove(BuildStorageKey(snapshot.사용자UserId));
        }
    }

    private static string BuildStorageKey(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId.Trim()));
        return $"{StorageKeyPrefix}{Convert.ToHexString(hash)}";
    }
}

public sealed record 꾸미기구매처리결과(
    bool 성공,
    string 안내문구,
    노드스티커FakePg결제승인Response? 결제승인 = null);

public sealed class 꾸미기보유권동기화Service
{
    private const string EntitlementsPath = "api/v1/community/node-sticker-store/entitlements/me";
    private const string ConfirmPath = "api/v1/community/node-sticker-store/fake-pg/confirm";

    private readonly HttpClient httpClient;
    private readonly IAuthSession authSession;
    private readonly I꾸미기보유권LocalStore localStore;
    private readonly PlatformCommunityDecorationStateService decorationState;

    public 꾸미기보유권동기화Service(
        HttpClient httpClient,
        IAuthSession authSession,
        I꾸미기보유권LocalStore localStore,
        PlatformCommunityDecorationStateService decorationState)
    {
        this.httpClient = httpClient;
        this.authSession = authSession;
        this.localStore = localStore;
        this.decorationState = decorationState;
    }

    public bool 서버구매지원상품인가(CommunityDecorationProduct product)
        => product.Assets.Any(asset => asset.NodeSticker is not null);

    public async Task<bool> RestoreAndSynchronizeAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthenticatedUser(out var userId, out _))
        {
            decorationState.ClearAccountOwnedPacks();
            return false;
        }

        var cached = await localStore.LoadAsync(userId, cancellationToken);
        if (cached is not null && string.Equals(cached.사용자UserId, userId, StringComparison.Ordinal))
        {
            Apply(cached);
        }

        return await SynchronizeAsync(cancellationToken);
    }

    public async Task<bool> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthenticatedUser(out var userId, out var accessToken))
        {
            decorationState.ClearAccountOwnedPacks();
            return false;
        }

        try
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, EntitlementsPath, accessToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var snapshot = await response.Content.ReadFromJsonAsync<노드스티커보유권동기화Response>(cancellationToken);
            if (snapshot is null || !string.Equals(snapshot.사용자UserId, userId, StringComparison.Ordinal))
            {
                return false;
            }

            await localStore.SaveAsync(snapshot, cancellationToken);
            Apply(snapshot);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task<꾸미기구매처리결과> ConfirmFakePurchaseAsync(
        CommunityDecorationProduct product,
        string paymentMethod,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!서버구매지원상품인가(product))
        {
            return new(false, "아직 서버 구매와 연결되지 않은 꾸미기 상품입니다.");
        }

        if (!TryGetAuthenticatedUser(out var userId, out var accessToken))
        {
            return new(false, "구매한 꾸미기를 여러 기기에서 사용하려면 먼저 로그인해 주세요.");
        }

        var payload = new 노드스티커FakePg결제승인Request
        {
            상품Key = product.Key,
            팩Key = product.PackKey,
            Amount = decimal.ToInt32(decimal.Round(product.PriceAmount, 0, MidpointRounding.AwayFromZero)),
            결제수단 = string.IsNullOrWhiteSpace(paymentMethod) ? "FakePG" : paymentMethod.Trim(),
            IdempotencyKey = $"decoration-{userId}-{product.PackKey}"
        };

        노드스티커FakePg결제승인Response? approval;
        try
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Post, ConfirmPath, accessToken);
            request.Content = JsonContent.Create(payload);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new(false, string.IsNullOrWhiteSpace(body)
                    ? $"서버 구매 승인에 실패했습니다. HTTP {(int)response.StatusCode}"
                    : body);
            }

            approval = await response.Content.ReadFromJsonAsync<노드스티커FakePg결제승인Response>(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new(false, "서버에 연결할 수 없어 구매를 승인하지 못했습니다.");
        }
        catch (JsonException)
        {
            return new(false, "서버 구매 승인 응답을 읽을 수 없습니다.");
        }

        if (approval is null)
        {
            return new(false, "서버 구매 승인 응답이 비어 있습니다.");
        }

        await MergeApprovedEntitlementAsync(userId, approval.보유권, cancellationToken);
        await SynchronizeAsync(cancellationToken);
        return new(true, "구매한 꾸미기를 이 기기에 저장하고 계정 보유권과 동기화했습니다.", approval);
    }

    public void ClearVisibleEntitlements()
        => decorationState.ClearAccountOwnedPacks();

    private async Task MergeApprovedEntitlementAsync(
        string userId,
        노드스티커보유권Response entitlement,
        CancellationToken cancellationToken)
    {
        var cached = await localStore.LoadAsync(userId, cancellationToken);
        var merged = (cached?.보유권목록 ?? [])
            .Where(item => !string.Equals(item.팩Key, entitlement.팩Key, StringComparison.OrdinalIgnoreCase))
            .Append(entitlement)
            .OrderBy(item => item.팩Key, StringComparer.Ordinal)
            .ToArray();
        var snapshot = new 노드스티커보유권동기화Response
        {
            사용자UserId = userId,
            서버기준시각Utc = DateTime.UtcNow,
            보유권목록 = merged
        };

        await localStore.SaveAsync(snapshot, cancellationToken);
        Apply(snapshot);
    }

    private void Apply(노드스티커보유권동기화Response snapshot)
        => decorationState.SynchronizeServerOwnedPacks(snapshot.보유권목록.Select(item => item.팩Key));

    private bool TryGetAuthenticatedUser(out string userId, out string accessToken)
    {
        userId = authSession.UserId?.Trim() ?? string.Empty;
        accessToken = authSession.AccessToken?.Trim() ?? string.Empty;
        return userId.Length > 0 && accessToken.Length > 0;
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
