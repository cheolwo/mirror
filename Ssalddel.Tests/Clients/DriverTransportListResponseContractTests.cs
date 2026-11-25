using System.Net;
using System.Text;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 기사 HTTP 클라이언트·목록 서비스·내역 VM의 응답 경계. 서버/기기 실행 증거는 아닙니다.</summary>
public sealed class DriverTransportListResponseContractTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound, null)]
    [InlineData(HttpStatusCode.NoContent, null)]
    [InlineData(HttpStatusCode.OK, "null")]
    [InlineData(HttpStatusCode.OK, "[null]")]
    public async Task 목록의_누락본문과_null원소를_정상빈내역으로_바꾸지않는다(
        HttpStatusCode status, string? body)
    {
        using var fixture = await Fixture.CreateAsync(status, body);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => fixture.Service.목록조회Async());

        Assert.True(fixture.Auth.IsAuthenticated);
        Assert.Equal("/api/v1/driver/transports", Assert.Single(fixture.Handler.Paths));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, null)]
    [InlineData(HttpStatusCode.NoContent, null)]
    [InlineData(HttpStatusCode.OK, "null")]
    [InlineData(HttpStatusCode.OK, "[null]")]
    [InlineData(HttpStatusCode.OK, "{\"detail\":\"RAW_PRIVATE_ADDRESS\"}")]
    [InlineData(HttpStatusCode.OK, "\"RAW_PRIVATE_ADDRESS\"")]
    public async Task 잘못된목록응답은_내역실패로표시하고_명시재조회로회복한다(
        HttpStatusCode status, string? body)
    {
        using var fixture = await Fixture.CreateAsync(status, body);
        using var model = new 기사운송내역PageViewModel(fixture.Service, fixture.Auth);

        await model.InitializeAsync();

        Assert.False(model.불러오는중);
        Assert.False(model.로그인필요);
        Assert.True(fixture.Auth.IsAuthenticated);
        Assert.Empty(model.운송목록);
        Assert.False(string.IsNullOrWhiteSpace(model.오류메시지));
        Assert.DoesNotContain("RAW_PRIVATE", model.오류메시지);
        Assert.Single(fixture.Handler.Paths);

        fixture.Handler.Status = HttpStatusCode.OK;
        fixture.Handler.Body = ValidListJson;
        await model.RefreshAsync();

        Assert.Equal(42, Assert.Single(model.운송목록).Id);
        Assert.Null(model.오류메시지);
        Assert.False(model.불러오는중);
        Assert.False(model.로그인필요);
        Assert.Equal(2, fixture.Handler.Paths.Count);
    }

    [Fact]
    public async Task 정상빈배열은_성공한빈내역으로_보존한다()
    {
        using var fixture = await Fixture.CreateAsync(HttpStatusCode.OK, "[]");
        using var model = new 기사운송내역PageViewModel(fixture.Service, fixture.Auth);

        await model.InitializeAsync();

        Assert.Empty(model.운송목록);
        Assert.Null(model.오류메시지);
        Assert.False(model.불러오는중);
        Assert.False(model.로그인필요);
        Assert.Single(fixture.Handler.Paths);
    }

    [Fact]
    public async Task 정상목록은_서버의_운송식별자와_내용을_그대로전달한다()
    {
        using var fixture = await Fixture.CreateAsync(HttpStatusCode.OK, ValidListJson);

        var row = Assert.Single(await fixture.Service.목록조회Async());

        Assert.Equal(42, row.Id);
        Assert.Equal("synthetic-transport-42", row.운송번호);
        Assert.Equal("인수완료", row.상태);
        Assert.Equal("synthetic-driver-list", row.기사_운송자);
        Assert.Equal(6500m, row.운임);
        Assert.Single(fixture.Handler.Paths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 현재운송과_개별상세의_404는_기존_없음의미를_유지한다(bool detail)
    {
        using var fixture = await Fixture.CreateAsync(HttpStatusCode.NotFound, null);

        if (detail)
            Assert.Null(await fixture.Service.상세조회Async(42));
        else
            Assert.Null(await fixture.Service.현재조회Async());

        Assert.Equal(detail ? "/api/v1/driver/transports/42" : "/api/v1/driver/transports/current",
            Assert.Single(fixture.Handler.Paths));
        Assert.True(fixture.Auth.IsAuthenticated);
    }

    [Fact]
    public async Task 목록의_503은_기존_HTTP실패상태를_유지한다()
    {
        using var fixture = await Fixture.CreateAsync(HttpStatusCode.ServiceUnavailable,
            "{\"detail\":\"RAW_PRIVATE_ADDRESS\"}");

        var error = await Assert.ThrowsAsync<DriverApiException>(() => fixture.Service.목록조회Async());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.DoesNotContain("RAW_PRIVATE", error.Message);
        Assert.True(fixture.Auth.IsAuthenticated);
        Assert.Single(fixture.Handler.Paths);
    }

    private const string ValidListJson = """
        [{"id":42,"운송번호":"synthetic-transport-42","상태":"인수완료",
          "기사_운송자":"synthetic-driver-list","출발지":"합성 상차지","도착지":"합성 하차지","운임":6500}]
        """;

    private sealed class Fixture(AuthSession auth, ResponseHandler handler, HttpClient http) : IDisposable
    {
        public AuthSession Auth { get; } = auth;
        public ResponseHandler Handler { get; } = handler;
        public DriverTransportApiService Service { get; } = new(new DriverApiClient(
            http, auth, new AuthApiService(http, auth), new DriverOperatingProfileService()));

        public static async Task<Fixture> CreateAsync(HttpStatusCode status, string? body)
        {
            var auth = new AuthSession(new TokenStore(), new ClientSessionGuard());
            await auth.RestoreAsync();
            var handler = new ResponseHandler { Status = status, Body = body };
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://cargo-list.example.test/") };
            return new(auth, handler, http);
        }

        public void Dispose() => http.Dispose();
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; }
        public string? Body { get; set; }
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-only-driver-list-access", request.Headers.Authorization?.Parameter);
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = Body is null ? null : new StringContent(Body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? _snapshot = new("test-only-driver-list-access",
            DateTime.UtcNow.AddHours(1), "test-only-driver-list-refresh", DateTime.UtcNow.AddDays(1),
            "synthetic-driver-list", "합성 기사", ["Driver"]);

        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot);

        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _snapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _snapshot = null;
            return Task.CompletedTask;
        }
    }
}
