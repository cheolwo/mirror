using System.Net;
using System.Net.Http.Json;
using DriverApp.Services;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;

namespace Ssalddel.Tests.Clients;

public sealed class DriverWorkPreferencesApiR71Tests
{
    [Fact]
    public async Task 화물기사adapter는_기존인증client의같은GET_PUT경로와판본을사용한다()
    {
        var api = new CargoApi(); var session = new CargoSession();
        var client = new DriverApp.Services.DriverWorkParticipationClient(api, session);
        var changes = 0; client.SessionChanged += () => changes++;
        await client.GetAsync(); Assert.Equal(IDriverWorkParticipationClient.Endpoint, api.Path); Assert.Equal("GET", api.Method);
        var request = new DriverWorkParticipationUpdate { ExpectedRevision = 4, Settings = DriverWorkPreferencesR71Tests.Response().Settings };
        await client.UpdateAsync(request); Assert.Equal("PUT", api.Method); Assert.Same(request, api.Payload);
        session.Change("driver-b"); Assert.Equal(1, changes); Assert.Equal("driver-b", client.Identity.DriverId); Assert.Equal(2, client.Identity.SessionRevision);
    }

    [Fact]
    public async Task 화물설정응답없음은_샘플로대체하지않는다()
    {
        var api = new CargoApi { Response = null }; var client = new DriverApp.Services.DriverWorkParticipationClient(api, new CargoSession());
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => client.UpdateAsync(new()));
    }

    [Fact]
    public async Task 음식기사adapter는_기존인증HTTP와동일공통설정경로를사용한다()
    {
        var session = new FDriverTestSession(); DriverWorkParticipationUpdate? sent = null; var methods = new List<HttpMethod>();
        var handler = new FDriverTestHttpHandler(async (request, token) =>
        {
            Assert.Equal("/" + IDriverWorkParticipationClient.Endpoint, request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme); Assert.Equal("test-access", request.Headers.Authorization.Parameter);
            methods.Add(request.Method);
            if (request.Method == HttpMethod.Put) sent = await request.Content!.ReadFromJsonAsync<DriverWorkParticipationUpdate>(token);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(DriverWorkPreferencesR71Tests.Response() with { DriverId = "test-driver" }) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        var client = new FDriverApp.Services.DriverWorkParticipationClient(new FoodDeliveryDriverApiService(http, session, new(http, session)), session);
        await client.GetAsync(); await client.UpdateAsync(new() { ExpectedRevision = 4, Settings = DriverWorkPreferencesR71Tests.Response().Settings });
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Put }, methods); Assert.Equal(4, sent!.ExpectedRevision);
        var changes = 0; client.SessionChanged += () => changes++; await session.ClearAsync();
        Assert.Equal(1, changes); Assert.False(client.Identity.IsAuthenticated);
    }

    [Fact]
    public async Task 음식설정503은_자동재전송이나샘플성공으로숨기지않는다()
    {
        var sends = 0; var session = new FDriverTestSession();
        var handler = new FDriverTestHttpHandler((_, _) => { sends++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); });
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        var client = new FDriverApp.Services.DriverWorkParticipationClient(new FoodDeliveryDriverApiService(http, session, new(http, session)), session);
        await Assert.ThrowsAsync<FDriverApiException>(() => client.UpdateAsync(new())); Assert.Equal(1, sends);
    }

    private sealed class CargoSession : IAuthSession
    {
        public string? AccessToken => "token";
        public string? RefreshToken => "refresh";
        public DateTime AccessTokenExpiresAtUtc => DateTime.UtcNow.AddHours(1);
        public DateTime RefreshTokenExpiresAtUtc => DateTime.UtcNow.AddHours(2);
        public string? UserId { get; private set; } = "driver-a";
        public string? UserName => UserId;
        public IReadOnlyList<string> Roles => ["Driver"];
        public bool IsAuthenticated => UserId is not null;
        public long Version => SessionRevision;
        public long SessionRevision { get; private set; } = 1;
        public event Action? Changed;
        public void Change(string? id) { UserId = id; SessionRevision++; Changed?.Invoke(); }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) { Change(null); return Task.CompletedTask; }
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion, CancellationToken cancellationToken = default, bool startsNewSession = false) => throw new NotSupportedException();
        public Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CargoApi : IDriverApiClient
    {
        public string? Path { get; private set; }
        public string? Method { get; private set; }
        public object? Payload { get; private set; }
        public DriverWorkParticipationResponse? Response { get; set; } = DriverWorkPreferencesR71Tests.Response();
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, CancellationToken cancellationToken = default)
        { Path = path; Method = "GET"; return Task.FromResult((TResponse?)(object?)Response); }
        public Task<TResponse?> PutAsync<TRequest, TResponse>(string path, TRequest payload, string operationName, CancellationToken cancellationToken = default)
        { Path = path; Method = "PUT"; Payload = payload; return Task.FromResult((TResponse?)(object?)Response); }
        public Task<TResponse?> PostAsync<TResponse>(string path, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest payload, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync(string path, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string path, TRequest payload, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PutAsync<TRequest>(string path, TRequest payload, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string path, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
