using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.RoleWorkspace;
using Ssalddel.Contracts.Common;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Clients;

public sealed class UnifiedRoleWorkspaceAccessTests
{
    [Fact]
    public async Task Credentials_are_scoped_and_role_choice_does_not_grant_access()
    {
        var requests = new List<(string Path, string? Token)>();
        using var http = new HttpClient(new Handler((r, _) =>
        {
            requests.Add((r.RequestUri!.AbsolutePath, r.Headers.Authorization?.Parameter));
            return Task.FromResult(r.RequestUri.AbsolutePath.EndsWith("login")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Token("restaurant-token")) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new Reply("ok")) });
        })) { BaseAddress = new Uri("https://local.test/") };
        using var access = new RoleWorkspaceAccess(http, new Primary(), new Stores());
        await access.SignInAsync("restaurant", "person", "password");
        Assert.False(access.GetIdentity("operator").IsAuthenticated);
        await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => access.GetAsync<Reply>("operator", "api/v1/admin/food-orders", default));
        await access.GetAsync<Reply>("restaurant", "api/v1/food-orders/restaurant/inbox", default);
        await access.GetAsync<Reply>("shipper", "api/v1/shipper/requests", default);
        Assert.Equal("restaurant-token", requests[1].Token);
        Assert.Equal("primary-token", requests[2].Token);
        Assert.Equal(3, requests.Count);
    }
    [Fact]
    public async Task Forbidden_preserves_identity_and_does_not_replay_command()
    {
        var count = 0;
        using var http = new HttpClient(new Handler((r, _) => { count++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)); })) { BaseAddress = new Uri("https://local.test/") };
        var primary = new Primary();
        using var access = new RoleWorkspaceAccess(http, primary, new Stores());
        var exception = await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => access.PostAsync<Reply>("cargo-driver", "api/v1/driver/transports/1/pickup", new { }, default));
        Assert.Equal(403, exception.StatusCode);
        Assert.True(access.GetIdentity("cargo-driver").IsAuthenticated);
        Assert.Equal(1, count);
    }
    [Fact]
    public async Task Unauthorized_clears_identity_without_replaying_command()
    {
        var count = 0;
        using var http = new HttpClient(new Handler((r, _) => { count++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); })) { BaseAddress = new Uri("https://local.test/") };
        using var access = new RoleWorkspaceAccess(http, new Primary(), new Stores());
        await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => access.PostAsync<Reply>("warehouse", "api/v1/warehouse-operations/inbounds/1/complete", new { }, default));
        Assert.False(access.GetIdentity("warehouse").IsAuthenticated);
        Assert.Equal(1, count);
    }
    [Theory]
    [InlineData("https://outside.test/api/v1/orders")]
    [InlineData("api/v1/../private")]
    [InlineData("//outside.test/private")]
    public async Task Unsafe_endpoint_is_rejected_before_credentials_are_sent(string path)
    {
        using var http = new HttpClient(new Handler((r, _) => throw new Xunit.Sdk.XunitException("No HTTP expected"))) { BaseAddress = new Uri("https://local.test/") };
        using var access = new RoleWorkspaceAccess(http, new Primary(), new Stores());
        await Assert.ThrowsAsync<ArgumentException>(() => access.GetAsync<Reply>("orderer", path, default));
    }
    [Fact]
    public async Task Response_from_previous_owner_is_discarded()
    {
        var primary = new Primary();
        using var http = new HttpClient(new Handler((r, _) => { primary.ChangeOwner(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new Reply("private")) }); })) { BaseAddress = new Uri("https://local.test/") };
        using var access = new RoleWorkspaceAccess(http, primary, new Stores());
        await Assert.ThrowsAsync<OperationCanceledException>(() => access.GetAsync<Reply>("shipper", "api/v1/shipper/requests", default));
    }
    [Fact]
    public async Task Submission_problem_preserves_menu_price_code_without_replaying()
    {
        var count = 0;
        using var http = new HttpClient(new Handler((r, _) =>
        {
            count++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            { Content = JsonContent.Create(new { errorCode = "MenuPriceChanged", detail = "private detail" }) });
        })) { BaseAddress = new Uri("https://local.test/") };
        using var access = new RoleWorkspaceAccess(http, new Primary(), new Stores());
        var error = await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => access.PostAsync<Reply>("orderer", "api/v1/food-orders", new { }, default));
        Assert.Equal("MenuPriceChanged", error.ErrorCode);
        Assert.Contains("MenuPriceChanged", error.ResponseBody);
        Assert.DoesNotContain("private detail", error.Message);
        Assert.Equal(1, count);
    }
    private sealed record Reply(string Value);
    private static 토큰응답 Token(string token) => new() { AccessToken = token, AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1), RefreshToken = "refresh", RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1), UserId = "owner", UserName = "person", Roles = ["음식점"] };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken); }
    private sealed class Stores : IRoleWorkspaceTokenStoreFactory
    {
        public IClientSecureTokenStore Create(string roleKey) => new Store();
        private sealed class Store : IClientSecureTokenStore
        {
            private ClientAuthTokenSnapshot? _value;
            public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken ct = default) => Task.FromResult(_value);
            public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken ct = default) { _value = value; return Task.CompletedTask; }
            public Task ClearAsync(CancellationToken ct = default) { _value = null; return Task.CompletedTask; }
        }
    }
    private sealed class Primary : IRoleWorkspacePrimaryAuth
    {
        public event Action? Changed;
        public RoleWorkspaceIdentity Identity { get; private set; } = new("owner", 1, true);
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SignInAsync(string name, string password, CancellationToken ct) => Task.CompletedTask;
        public Task SignOutAsync(CancellationToken ct) { Identity = new(null, Identity.Revision + 1, false); Changed?.Invoke(); return Task.CompletedTask; }
        public Task<string?> GetTokenAsync(CancellationToken ct) => Task.FromResult<string?>(Identity.IsAuthenticated ? "primary-token" : null);
        public void ChangeOwner() { Identity = new("other", Identity.Revision + 1, true); Changed?.Invoke(); }
    }
}
