using System.Globalization;
using System.Text.Json;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RestaurantDeskApp.Services;

/// <summary>한 수신함 페이지의 같은 계정·음식점에서만 설정 입력과 미확정 요청을 보존한다.</summary>
public sealed class 음식점배송지원설정Session : IDisposable
{
    private readonly RestaurantAuthService auth;
    private readonly Func<ISsalddelAccessTokenProvider, ISsalddelJsonApiClient> createApi;
    private readonly Func<string> endpoint;
    private 설정문맥? context;
    private FoodMerchantFulfillmentSettingsViewModel? model;
    private bool disposed;

    // 인증용 Client가 먼저 등록돼도 주소 없는 기본 HttpClient를 사용하지 않는다.
    public 음식점배송지원설정Session(RestaurantAuthService auth, IHttpClientFactory httpFactory,
        SsalddelIsmsPClientEncryptionService encryption, Func<string> endpoint)
        : this(auth, httpFactory.CreateClient(SsalddelHttpClientNames.OperationalApi), encryption, endpoint)
    {
    }

    public 음식점배송지원설정Session(RestaurantAuthService auth, HttpClient http,
        SsalddelIsmsPClientEncryptionService encryption, Func<string> endpoint)
        : this(auth, provider => new SsalddelJsonApiClient(new SsalddelProtectedApiClient(http, encryption, provider)), endpoint)
    {
    }

    internal 음식점배송지원설정Session(RestaurantAuthService auth,
        Func<ISsalddelAccessTokenProvider, ISsalddelJsonApiClient> createApi, Func<string> endpoint)
    {
        this.auth = auth;
        this.createApi = createApi;
        this.endpoint = endpoint;
        auth.SessionEnding += SessionEnding;
    }

    public event Action? Invalidated;

    public FoodMerchantFulfillmentSettingsViewModel? Current
    {
        get
        {
            if (model is not null && context != CaptureContext()) Reset(notify: false);
            return model;
        }
    }

    public FoodMerchantFulfillmentSettingsViewModel Open()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var next = CaptureContext();
        if (next is null)
        {
            Reset(notify: false);
            throw new UnauthorizedAccessException("음식점 계정을 확인하고 다시 로그인해 주세요.");
        }
        if (context == next && model is not null) return model;
        Reset(notify: false);
        context = next;
        return model = new(ct => LoadAsync(next, ct), (request, ct) => SaveAsync(next, request, ct));
    }

    private Task<RestaurantFulfillmentSupportResponse?> LoadAsync(설정문맥 owner, CancellationToken ct)
        => SendAsync(owner, api => api.GetAsync<RestaurantFulfillmentSupportResponse>(
            "api/v1/food-orders/restaurant/fulfillment-settings", "배송 지원 설정", cancellationToken: ct), ct);

    private Task<RestaurantFulfillmentSupportResponse?> SaveAsync(
        설정문맥 owner, RestaurantFulfillmentSupportRequest request, CancellationToken ct)
        => SendAsync(owner, api => api.SendAsync<RestaurantFulfillmentSupportRequest, RestaurantFulfillmentSupportResponse>(
            HttpMethod.Put, "api/v1/food-orders/restaurant/fulfillment-settings", request, "배송 지원 설정 변경", cancellationToken: ct), ct);

    private async Task<RestaurantFulfillmentSupportResponse?> SendAsync(설정문맥 owner,
        Func<ISsalddelJsonApiClient, Task<RestaurantFulfillmentSupportResponse?>> send, CancellationToken ct)
    {
        var request = await CreateRequestApiAsync(owner, ct);
        try
        {
            var response = await send(request.Api);
            EnsureCurrent(owner);
            ValidateRestaurant(owner, response);
            return response;
        }
        catch (SsalddelApiException ex) when (ex.StatusCode == 401)
        {
            await auth.InvalidateRejectedSessionForRequestAsync(owner.Owner, request.Token, owner.Generation, ct);
            throw;
        }
    }

    private async Task<설정요청Api> CreateRequestApiAsync(설정문맥 owner, CancellationToken ct)
    {
        EnsureCurrent(owner);
        var result = await auth.EnsureAccessTokenForRequestAsync(owner.Owner, owner.Generation, false, ct);
        EnsureCurrent(owner);
        if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorMessage);
        var credentials = await auth.CaptureRequestCredentialsAsync(owner.Owner, owner.Generation, ct);
        EnsureCurrent(owner);
        if (ReadRestaurantId(credentials.AccessToken) != owner.RestaurantId)
            throw new OperationCanceledException("음식점 인증이 변경되어 이전 설정 요청을 전송하지 않습니다.");
        return new(createApi(new 설정요청TokenProvider(this, owner, credentials.AccessToken)), credentials.AccessToken);
    }

    private void EnsureCurrent(설정문맥 owner)
    {
        try
        {
            auth.ThrowIfActorChanged(owner.Owner, owner.Generation);
            if (disposed || owner != CaptureContext())
                throw new OperationCanceledException("계정 또는 음식점이 변경되어 이전 설정 요청을 적용하지 않습니다.");
        }
        catch (OperationCanceledException)
        {
            if (context == owner) Reset();
            throw;
        }
    }

    private static void ValidateRestaurant(설정문맥 owner, RestaurantFulfillmentSupportResponse? response)
    {
        if (response is not null && response.RestaurantId != owner.RestaurantId)
            throw new InvalidOperationException("현재 음식점의 설정 응답을 확인하지 못했습니다.");
    }

    private 설정문맥? CaptureContext()
    {
        if (disposed || string.IsNullOrWhiteSpace(auth.Session.UserId)) return null;
        var restaurantId = ReadRestaurantId(auth.Session.AccessToken);
        return restaurantId is { } id
            ? new(auth.Session.UserId, auth.SessionGeneration, id, endpoint()) : null;
    }

    // 권한은 서버가 검증한다. 여기서는 토큰 갱신을 허용하면서 다른 음식점의 요청 재사용만 막는다.
    private static long? ReadRestaurantId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var claims = document.RootElement.EnumerateObject()
                .Where(x => x.Name == 음식점접근ClaimTypes.음식점Id).ToArray();
            if (claims.Length != 1) return null;
            var value = claims[0].Value;
            var text = value.ValueKind == JsonValueKind.String ? value.GetString()
                : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
        }
        catch (FormatException) { return null; }
        catch (JsonException) { return null; }
    }

    private void SessionEnding(bool _) => Reset();
    private void Reset(bool notify = true)
    {
        var hadModel = model is not null;
        context = null;
        model = null;
        if (hadModel && notify) Invalidated?.Invoke();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        auth.SessionEnding -= SessionEnding;
        context = null;
        model = null;
        Invalidated = null;
    }

    private sealed record 설정문맥(string Owner, long Generation, long RestaurantId, string Endpoint);
    private sealed record 설정요청Api(ISsalddelJsonApiClient Api, string Token);

    // 공용 API가 나중에 새 계정/음식점 토큰을 다시 읽지 않도록 요청마다 캡처한 토큰만 제공한다.
    private sealed class 설정요청TokenProvider(음식점배송지원설정Session session, 설정문맥 owner, string token)
        : ISsalddelAccessTokenProvider
    {
        public string AccessToken => token;
        public string? AuthenticationOwnerId => !session.disposed && session.CaptureContext() == owner ? owner.Owner : null;
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.EnsureCurrent(owner);
            return Task.FromResult<string?>(token);
        }
    }
}
