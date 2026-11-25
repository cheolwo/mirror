using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantFulfillmentSettingsSessionTests
{
    [Fact]
    public async Task 인증Client가먼저등록돼도_운영API_Factory로설정을조회한다()
    {
        var observed = new List<Uri>();
        using var services = RegisteredRestaurantClients(observed);
        var factory = services.GetRequiredService<IHttpClientFactory>();
        using var bare = services.GetRequiredService<HttpClient>();
        Assert.Null(bare.BaseAddress);
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        await session.ApplyAsync(Fixture.Snapshot("owner-a", Fixture.Token("{\"ssalddel:restaurant_id\":101}")));
        using var authenticationHttp = factory.CreateClient("RestaurantAuthentication");
        var auth = new RestaurantAuthService(authenticationHttp, session);
        var encryption = new SsalddelIsmsPClientEncryptionService(new NoJs());
        using var settings = new 음식점배송지원설정Session(auth, factory, encryption, () => "http://controlled.invalid:5378/");

        var model = settings.Open();
        await model.LoadAsync();

        Assert.Null(model.Error);
        Assert.True(model.Loaded);
        Assert.Equal(101, model.Value!.RestaurantId);
        Assert.Equal(4, model.Value.Revision);
        Assert.Equal(new Uri("http://controlled.invalid:5378/api/v1/food-orders/restaurant/fulfillment-settings"), Assert.Single(observed));
        Assert.Null(bare.BaseAddress);
    }

    [Fact]
    public async Task 기본HttpClient에주소가없으면_기존생성자의상대경로는실패한다()
    {
        var observed = new List<Uri>();
        using var services = RegisteredRestaurantClients(observed);
        var factory = services.GetRequiredService<IHttpClientFactory>();
        using var bare = services.GetRequiredService<HttpClient>();
        Assert.Null(bare.BaseAddress);
        await Assert.ThrowsAsync<InvalidOperationException>(() => bare.GetAsync("api/v1/food-orders/restaurant/fulfillment-settings"));
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        await session.ApplyAsync(Fixture.Snapshot("owner-a", Fixture.Token("{\"ssalddel:restaurant_id\":101}")));
        using var authenticationHttp = factory.CreateClient("RestaurantAuthentication");
        var auth = new RestaurantAuthService(authenticationHttp, session);
        var encryption = new SsalddelIsmsPClientEncryptionService(new NoJs());
        using var settings = new 음식점배송지원설정Session(auth, bare, encryption, () => "http://controlled.invalid:5378/");

        var model = settings.Open();
        await model.LoadAsync();

        Assert.Null(model.Value);
        Assert.Equal("배송 지원 설정을 확인하지 못했습니다. 다시 불러와 주세요.", model.Error);
        Assert.Empty(observed);
    }

    [Fact]
    public async Task ReopeningPendingSettingsKeepsOriginalRequestAndDoesNotSendAutomatically()
    {
        using var f = await Fixture.Create();
        var model = await f.Edit();
        f.LoseNextResponse = true;
        await model.SaveAsync();
        Assert.True(model.HasPending);
        var first = Assert.Single(f.Requests).Request;
        Assert.Same(model, f.Settings.Open());
        await model.LoadAsync();
        Assert.Equal(1, f.Reads);
        Assert.Single(f.Requests);
        await model.SaveAsync();
        Assert.Equal(2, f.Requests.Count);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(f.Requests[1].Request));
        Assert.Equal(1, f.Applied);
        Assert.False(model.HasPending);
    }

    [Fact]
    public async Task NormalTokenRefreshKeepsPendingButNextRequestUsesTheNewSameRestaurantToken()
    {
        using var f = await Fixture.Create();
        var model = await f.Pending();
        await f.Identity("owner-a", 41, "refreshed");
        Assert.Same(model, f.Settings.Open());
        await model.SaveAsync();
        Assert.Equal(f.Requests[0].Request.ClientRequestId, f.Requests[1].Request.ClientRequestId);
        Assert.Equal(41, f.Requests[1].Restaurant);
        Assert.Equal("refreshed", f.Requests[1].Nonce);
        Assert.False(model.HasPending);
    }

    [Theory]
    [InlineData("owner-b", 41)]
    [InlineData("owner-a", 42)]
    public async Task DifferentOwnerOrRestaurantCannotReuseOldPending(string owner, long restaurant)
    {
        using var f = await Fixture.Create();
        var old = await f.Pending();
        await f.Identity(owner, restaurant, "new-scope");
        Assert.Null(f.Settings.Current);
        var current = f.Settings.Open();
        Assert.NotSame(old, current);
        await old.SaveAsync();
        Assert.Single(f.Requests);
        Assert.Same(current, f.Settings.Current);
    }

    [Fact]
    public async Task NewLoginForSameUserDoesNotReuseTheOldLoginPending()
    {
        using var f = await Fixture.Create();
        var old = await f.Pending();
        Assert.True((await f.Auth.LoginAsync("owner-a", "synthetic-password")).IsSuccess);
        Assert.NotSame(old, f.Settings.Open());
        await old.SaveAsync();
        Assert.Single(f.Requests);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"ssalddel:restaurant_id\":0}")]
    [InlineData("{\"ssalddel:restaurant_id\":-1}")]
    [InlineData("{\"ssalddel:restaurant_id\":\"not-a-number\"}")]
    [InlineData("{\"ssalddel:restaurant_id\":41,\"ssalddel:restaurant_id\":42}")]
    [InlineData("[]")]
    public async Task MissingOrAmbiguousRestaurantClaimFailsClosed(string payload)
    {
        using var f = await Fixture.Create();
        var old = await f.Pending();
        await f.Session.ApplyAsync(Fixture.Snapshot("owner-a", Fixture.Token(payload)));
        Assert.Null(f.Settings.Current);
        Assert.Throws<UnauthorizedAccessException>(() => f.Settings.Open());
        await old.SaveAsync();
        Assert.Single(f.Requests);
    }

    [Fact]
    public async Task EndpointChangeAndDisposalPreventOldRequests()
    {
        using var f = await Fixture.Create();
        var old = await f.Pending();
        f.Endpoint = "http://another-controlled.invalid/";
        Assert.Null(f.Settings.Current);
        await old.SaveAsync();
        Assert.Single(f.Requests);
        var next = f.Settings.Open();
        f.Settings.Dispose();
        await next.LoadAsync();
        Assert.Null(next.Value);
        Assert.Throws<ObjectDisposedException>(() => f.Settings.Open());
    }

    [Fact]
    public async Task RestaurantChangeDuringApiPreparationStopsBeforeTheRealProtectedClientSends()
    {
        using var f = await Fixture.Create(interceptFactory: true);
        var model = await f.Edit();
        f.BeforeApiCreated = () => f.Identity("owner-a", 42, "switched").GetAwaiter().GetResult();
        await model.SaveAsync();
        Assert.Empty(f.Requests);
        Assert.Null(f.Settings.Current);
    }

    [Fact]
    public async Task InFlightRequestKeepsCapturedRestaurantTokenAndCannotApplyLateResponseToNewScope()
    {
        using var f = await Fixture.Create();
        var model = await f.Edit();
        f.OnPut = async request =>
        {
            await f.Identity("owner-a", 42, "switched");
            return Fixture.Json(new RestaurantFulfillmentSupportResponse { RestaurantId = 41, Revision = 5 });
        };
        await model.SaveAsync();
        Assert.Equal(41, Assert.Single(f.Requests).Restaurant);
        Assert.Equal(4, model.Value!.Revision);
        Assert.Null(f.Settings.Current);
    }

    [Fact]
    public async Task OldUnauthorizedResponseCannotClearRefreshedCredentialsOrTrapPendingRecovery()
    {
        using var f = await Fixture.Create();
        var model = await f.Edit();
        f.OnPut = async _ =>
        {
            await f.Identity("owner-a", 41, "new-token");
            return new(HttpStatusCode.Unauthorized);
        };
        await model.SaveAsync();
        Assert.True(f.Session.IsAuthenticated);
        Assert.Same(model, f.Settings.Current);
        Assert.True(model.HasPending);
        Assert.NotNull(model.Value);
        f.OnPut = null;
        await model.SaveAsync();
        Assert.False(model.HasPending);
        Assert.Equal(f.Requests[0].Request.ClientRequestId, f.Requests[1].Request.ClientRequestId);
    }

    [Fact]
    public async Task CurrentUnauthorizedResponseEndsOnlyTheRejectedSessionWithoutAutomaticResend()
    {
        using var f = await Fixture.Create();
        var model = await f.Edit();
        f.OnPut = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        await model.SaveAsync();
        Assert.False(f.Session.IsAuthenticated);
        Assert.Null(f.Settings.Current);
        Assert.Single(f.Requests);
    }

    [Fact]
    public async Task WrongRestaurantResponseDoesNotReplaceConfirmedValueOrLosePending()
    {
        using var f = await Fixture.Create();
        var model = await f.Edit();
        f.OnPut = _ => Task.FromResult(Fixture.Json(new RestaurantFulfillmentSupportResponse { RestaurantId = 42, Revision = 5 }));
        await model.SaveAsync();
        Assert.True(model.HasPending);
        Assert.Equal(41, model.Value!.RestaurantId);
        Assert.Equal(4, model.Value.Revision);
        Assert.Same(model, f.Settings.Current);
    }

    [Fact]
    public async Task LogoutAndConcurrentSaveDoNotCreateASecondRequest()
    {
        using var f = await Fixture.Create();
        var model = await f.Edit();
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.OnPut = _ => response.Task;
        var sending = model.SaveAsync();
        Assert.True(model.IsSending);
        Assert.Same(model, f.Settings.Open());
        await model.SaveAsync();
        Assert.Single(f.Requests);
        await f.Auth.LogoutAsync();
        Assert.Null(f.Settings.Current);
        response.SetResult(Fixture.Json(new RestaurantFulfillmentSupportResponse { RestaurantId = 41, Revision = 5 }));
        await sending;
        Assert.Null(f.Settings.Current);
        Assert.Single(f.Requests);
    }

    private static ServiceProvider RegisteredRestaurantClients(List<Uri> observed)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("RestaurantAuthentication", client => client.BaseAddress = new("http://authentication.invalid/"))
            .ConfigurePrimaryHttpMessageHandler(() => new Handler((_, _) => throw new InvalidOperationException("Unexpected authentication HTTP")));
        services.AddSsalddelOperationalApiHttpClient(new Uri("http://controlled.invalid:5378/"));
        services.AddHttpClient(SsalddelHttpClientNames.OperationalApi)
            .ConfigurePrimaryHttpMessageHandler(() => new Handler((request, _) =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.True(request.RequestUri!.IsAbsoluteUri);
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.False(string.IsNullOrWhiteSpace(request.Headers.Authorization?.Parameter));
                observed.Add(request.RequestUri);
                return Task.FromResult(Fixture.Json(new RestaurantFulfillmentSupportResponse { RestaurantId = 101, Revision = 4 }));
            }));
        services.AddHttpClient(string.Empty)
            .ConfigurePrimaryHttpMessageHandler(() => new Handler((_, _) => throw new InvalidOperationException("Unexpected unnamed HTTP")));
        return services.BuildServiceProvider();
    }

    private sealed class Fixture : IDisposable
    {
        public ClientAuthSession Session { get; } = new(new MemoryStore(), new ClientSessionGuard());
        public HttpClient Http { get; }
        public RestaurantAuthService Auth { get; }
        public 음식점배송지원설정Session Settings { get; }
        public string Endpoint { get; set; } = "http://controlled.invalid/";
        public int Reads, Applied;
        public bool LoseNextResponse;
        public Action? BeforeApiCreated;
        public Func<RestaurantFulfillmentSupportRequest, Task<HttpResponseMessage>>? OnPut;
        public List<(RestaurantFulfillmentSupportRequest Request, long Restaurant, string Nonce)> Requests { get; } = [];
        private readonly Dictionary<Guid, RestaurantFulfillmentSupportResponse> receipts = [];
        private Fixture(bool interceptFactory)
        {
            Http = new(new Handler(Send)) { BaseAddress = new(Endpoint) };
            Auth = new(Http, Session);
            var encryption = new SsalddelIsmsPClientEncryptionService(new NoJs());
            Settings = interceptFactory
                ? new(Auth, provider => { BeforeApiCreated?.Invoke(); return new SsalddelJsonApiClient(new(Http, encryption, provider)); }, () => Endpoint)
                : new(Auth, Http, encryption, () => Endpoint);
        }
        public static async Task<Fixture> Create(bool interceptFactory = false)
        { var f = new Fixture(interceptFactory); await f.Identity("owner-a", 41, "original"); return f; }
        public Task Identity(string owner, long restaurant, string nonce)
            => Session.ApplyAsync(Snapshot(owner, Token(JsonSerializer.Serialize(new Dictionary<string, object>
                { ["ssalddel:restaurant_id"] = restaurant, ["nonce"] = nonce }))));
        public async Task<FoodMerchantFulfillmentSettingsViewModel> Edit()
        {
            var model = Settings.Open(); await model.LoadAsync();
            model.SelectPolicy("FixedContribution"); model.FixedContributionKrw = 2500; model.Enabled = true; model.Agreed = true;
            return model;
        }
        public async Task<FoodMerchantFulfillmentSettingsViewModel> Pending()
        { var model = await Edit(); LoseNextResponse = true; await model.SaveAsync(); return model; }
        private async Task<HttpResponseMessage> Send(HttpRequestMessage message, CancellationToken ct)
        {
            if (message.RequestUri!.AbsolutePath.EndsWith("/login"))
            {
                var snapshot = Snapshot("owner-a", Token("{\"ssalddel:restaurant_id\":41,\"nonce\":\"new-login\"}"));
                return Json(new 토큰응답 { AccessToken = snapshot.AccessToken, AccessTokenExpiresAtUtc = snapshot.AccessTokenExpiresAtUtc,
                    RefreshToken = snapshot.RefreshToken, RefreshTokenExpiresAtUtc = snapshot.RefreshTokenExpiresAtUtc,
                    UserId = snapshot.UserId, UserName = snapshot.UserName, Roles = snapshot.Roles.ToArray() });
            }
            Assert.EndsWith("/fulfillment-settings", message.RequestUri.AbsolutePath);
            var token = message.Headers.Authorization!.Parameter!;
            var encoded = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            using var payload = JsonDocument.Parse(Convert.FromBase64String(encoded.PadRight(encoded.Length + (4 - encoded.Length % 4) % 4, '=')));
            var restaurant = payload.RootElement.GetProperty("ssalddel:restaurant_id").GetInt64();
            if (message.Method == HttpMethod.Get)
            { Reads++; return Json(new RestaurantFulfillmentSupportResponse { RestaurantId = restaurant, Revision = 4 }); }
            Assert.Equal(HttpMethod.Put, message.Method);
            var request = (await message.Content!.ReadFromJsonAsync<RestaurantFulfillmentSupportRequest>(ct))!;
            var nonce = payload.RootElement.TryGetProperty("nonce", out var value) ? value.GetString()! : string.Empty;
            Requests.Add((request, restaurant, nonce));
            if (OnPut is not null) return await OnPut(request);
            if (!receipts.TryGetValue(request.ClientRequestId, out var result))
            {
                Applied++;
                result = new() { RestaurantId = restaurant, Revision = request.ExpectedRevision + 1,
                    FixedContributionKrw = request.FixedContributionKrw, Enabled = request.Enabled, CostSharingPolicy = request.CostSharingPolicy };
                receipts.Add(request.ClientRequestId, result);
            }
            if (LoseNextResponse) { LoseNextResponse = false; throw new HttpRequestException("synthetic response loss"); }
            return Json(result);
        }
        public static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        public static string Token(string payload) => "e30." + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".synthetic";
        public static ClientAuthTokenSnapshot Snapshot(string owner, string token)
            => new(token, DateTime.UtcNow.AddHours(1), "synthetic-refresh", DateTime.UtcNow.AddDays(1), owner, "합성 음식점", ["음식점"]);
        public void Dispose() { Settings.Dispose(); Http.Dispose(); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }
    private sealed class MemoryStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken ct = default) => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken ct = default) { value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken ct = default) { value = null; return Task.CompletedTask; }
    }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Unexpected JS call");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
