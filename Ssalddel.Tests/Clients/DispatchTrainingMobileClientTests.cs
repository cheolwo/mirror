using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Admin.DispatchTraining;
using Ssalddel.Contracts.Common;
using SsalddelAdminApp.Services;
using SecureStorage = SsalddelAdminApp.Services.SecureStorage;

namespace Ssalddel.Tests.Clients;

public sealed class DispatchTrainingMobileClientTests
{
    [Fact]
    public async Task capability는_인증없이조회하고_404를미지원으로반환한다()
    {
        using var storage = SecureStorage.UseForTest();
        using var fixture = await Fixture.Create(call => new(HttpStatusCode.NotFound));
        var capability = await fixture.Client.CapabilityAsync(default);
        Assert.False(capability.Enabled); var call = Assert.Single(fixture.Handler.Calls);
        Assert.Equal("/verification/dispatch-training/capability", call.Path); Assert.Null(call.Bearer);
    }
    [Fact]
    public async Task 정본조회는_기존관리자Bearer와_전용경로를쓴다()
    {
        using var storage = SecureStorage.UseForTest();
        using var fixture = await Fixture.Create(_ => Json(new DispatchTrainingSessionDto()));
        await fixture.Client.CurrentAsync(default); var call = Assert.Single(fixture.Handler.Calls);
        Assert.Equal("/verification/dispatch-training/current", call.Path); Assert.Equal("admin-training-access", call.Bearer);
    }
    [Fact]
    public async Task 지원명령은_원요청번호와수량을보내며_네트워크실패를재전송하지않는다()
    {
        using var storage = SecureStorage.UseForTest();
        using var fixture = await Fixture.Create(_ => throw new HttpRequestException("offline"));
        var request = new DispatchTrainingSupportDriversRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 7, RunId = "run-1", Count = 1 };
        await Assert.ThrowsAsync<AdminApiException>(() => fixture.Client.CommandAsync("support-drivers", request, default));
        var call = Assert.Single(fixture.Handler.Calls); using var body = JsonDocument.Parse(call.Body!);
        Assert.Equal("/verification/dispatch-training/support-drivers", call.Path);
        Assert.Equal(request.ClientRequestId, body.RootElement.GetProperty("clientRequestId").GetGuid());
        Assert.Equal(1, body.RootElement.GetProperty("count").GetInt32()); Assert.Equal(7, body.RootElement.GetProperty("expectedRevision").GetInt64());
    }
    [Fact]
    public async Task 알려지지않은명령은_HTTP를보내지않는다()
    {
        using var storage = SecureStorage.UseForTest(); using var fixture = await Fixture.Create(_ => Json(new DispatchTrainingCommandResultDto()));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Client.CommandAsync("delete-all", new(), default)); Assert.Empty(fixture.Handler.Calls);
    }
    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed record Call(string Path, string? Bearer, string? Body);
    private sealed class Handler(Func<Call, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var call = new Call(request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Parameter,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)); Calls.Add(call); return response(call);
        }
    }
    private sealed record Fixture(HttpClient Http, Handler Handler, DispatchTrainingClient Client) : IDisposable
    {
        public static async Task<Fixture> Create(Func<Call, HttpResponseMessage> response)
        {
            var handler = new Handler(response); var http = new HttpClient(handler) { BaseAddress = new("https://fixture.invalid/") };
            var session = new AdminAuthSession(new ClientSessionGuard());
            await session.ApplyAsync(new 토큰응답 { AccessToken = "admin-training-access", RefreshToken = "refresh", UserId = "admin", UserName = "관리자",
                Roles = ["서버관리자"], AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1), RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1) });
            return new(http, handler, new(http, new(http, session, new(http, session))));
        }
        public void Dispose() => Http.Dispose();
    }
}
