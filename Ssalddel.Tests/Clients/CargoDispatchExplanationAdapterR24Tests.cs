using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Clients;

public sealed partial class CargoDispatchExplanationFeedbackR24Tests
{
    [Fact]
    public async Task 기사실제인증의정상토큰갱신은설명조회와의견제출을취소하지않는다()
    {
        var session = new DriverApp.Services.AuthSession(new AdapterTokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(AdapterSnapshot("owner-a", "expired") with { AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) });
        using var handler = new AdapterHandler(); using var http = new HttpClient(handler) { BaseAddress = new("https://fixture.test/") };
        using var client = new DriverApp.Services.DriverCargoDispatchExplanationClient(http, session, new(http, session));
        var notices = 0; client.SessionChanged += () => notices++;
        using var model = AdapterModel(client, http); await model.LoadAsync("request-a");
        Assert.NotNull(model.Explanation); Assert.Null(model.Error); Assert.Equal(0, notices);
        Assert.Equal("renewed-access", session.AccessToken); Assert.Equal(1, handler.Refreshes);
        model.CategoryCode = "helpful"; await model.SubmitAsync();
        Assert.Equal("received", model.Receipt?.Status); Assert.Equal(1, handler.FeedbackPosts);
        await session.ApplyAsync(AdapterSnapshot("owner-b", "token-b"));
        await session.ApplyAsync(AdapterSnapshot("owner-a", "token-a-again"));
        Assert.Equal(2, notices); Assert.Null(model.Explanation); Assert.Null(model.Receipt);
    }

    [Fact]
    public async Task 화주실제세션의동일판본갱신은초안을보존하고재로그인은초안을비운다()
    {
        var session = new SsalddelApp.Services.AuthSession(new AdapterTokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(AdapterSnapshot("owner-a", "original-access"));
        using var handler = new AdapterHandler(); using var http = new HttpClient(handler) { BaseAddress = new("https://fixture.test/") };
        // 유효한 토큰과 실제 세션 갱신 이벤트를 검사한다. 이 흐름은 로그인/푸시 등록 분기를 실행하지 않는다.
        using var client = new SsalddelApp.Services.ShipperCargoDispatchExplanationClient(http, session, new(http, session, null!, null!));
        var notices = 0; client.SessionChanged += () => notices++;
        using var model = AdapterModel(client, http); await model.LoadAsync("request-a");
        model.CategoryCode = "other"; model.Comment = "유지할 의견";
        Assert.True(await session.TryRefreshAsync(AdapterSnapshot("owner-a", "renewed-access"), session.SessionRevision, session.RefreshToken));
        Assert.Equal(0, notices); Assert.NotNull(model.Explanation); Assert.Equal("유지할 의견", model.Comment); Assert.True(model.CanSubmit);
        await session.ApplyAsync(AdapterSnapshot("owner-a", "relogin-access"));
        Assert.Equal(1, notices); Assert.Null(model.Explanation); Assert.Equal(string.Empty, model.Comment);
        await session.ApplyAsync(AdapterSnapshot("owner-b", "token-b"));
        await session.ApplyAsync(AdapterSnapshot("owner-a", "token-a-again"));
        Assert.Equal(3, notices); Assert.Null(model.Explanation);
    }

    [Fact]
    public async Task Web언어변경과동일스냅샷복원은초안을유지하고새인증은판본을바꾼다()
    {
        var js = new AdapterJs { Json = JsonSerializer.Serialize(AdapterSnapshot("owner-a", "token-a"), new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        using var handler = new AdapterHandler(); using var http = new HttpClient(handler) { BaseAddress = new("https://fixture.test/") };
        var session = new Ssalddel.WebApp.Services.WebAuthSessionService(http, js, new ClientSessionGuard());
        await session.RestoreAsync();
        using var client = new Ssalddel.WebApp.Services.WebCargoDispatchExplanationClient(http, session);
        var notices = 0; client.SessionChanged += () => notices++;
        using var model = AdapterModel(client, http); await model.LoadAsync("request-a");
        model.CategoryCode = "other"; model.Comment = "유지할 Web 의견"; var identity = client.Identity;
        await session.SetPreferredLanguageAsync("ko"); await session.RestoreAsync();
        Assert.Equal(identity, client.Identity); Assert.Equal(0, notices); Assert.NotNull(model.Explanation); Assert.Equal("유지할 Web 의견", model.Comment);
        await session.ClearAsync();
        js.Json = JsonSerializer.Serialize(AdapterSnapshot("owner-a", "relogin-token"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await session.RestoreAsync();
        Assert.Equal(2, notices); Assert.True(client.Identity.SessionRevision > identity.SessionRevision);
        Assert.Null(model.Explanation); Assert.Equal(string.Empty, model.Comment);
    }

    private static CargoDispatchExplanationFeedbackViewModel AdapterModel(ICargoDispatchExplanationClient client, HttpClient http)
        => new(client, new VolatileWorkflowRecoveryStore(), new WorkflowRecoveryContext(
            () => new RoleWorkspaceIdentity(client.Identity.UserId, client.Identity.SessionRevision, client.Identity.IsAuthenticated), () => http.BaseAddress));

    private static ClientAuthTokenSnapshot AdapterSnapshot(string owner, string token)
        => new(token, DateTime.UtcNow.AddHours(1), "refresh-" + token, DateTime.UtcNow.AddDays(1), owner, "합성 사용자", ["화주", "기사"]);

    private sealed class AdapterTokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); value = null; return Task.CompletedTask; }
    }

    private sealed class AdapterHandler : HttpMessageHandler
    {
        public int Refreshes, FeedbackPosts;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request.RequestUri!.AbsolutePath == "/api/v1/auth/refresh")
            {
                Refreshes++;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 토큰응답
                {
                    AccessToken = "renewed-access", AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                    RefreshToken = "renewed-refresh", RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1), UserId = "owner-a", Roles = ["기사"]
                }) };
            }
            if (request.RequestUri.AbsolutePath == "/api/v1/auth/preferences/language")
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 표시언어설정응답 { LanguageCode = "ko" }) };
            if (request.Method == HttpMethod.Post)
            {
                FeedbackPosts++;
                var body = await request.Content!.ReadFromJsonAsync<CargoDispatchFeedbackRequest>(cancellationToken: ct);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Receipt(body!)) };
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Recorded("request-a")) };
        }
    }

    private sealed class AdapterJs : IJSRuntime
    {
        public string? Json;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested(); object? result = null;
            if (identifier == "localStorage.getItem") result = Json;
            else if (identifier == "localStorage.setItem") Json = args![1]?.ToString();
            else if (identifier == "localStorage.removeItem") Json = null;
            else throw new NotSupportedException(identifier);
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }
}
