using System.Net;
using System.Text;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common;
using SsalddelAdminApp.Services;
using SecureStorage = SsalddelAdminApp.Services.SecureStorage;

namespace Ssalddel.Tests.Clients;

public sealed class AdminApiProblemFallbackTests
{
    private const string Untrusted = "synthetic-private-gateway-detail";
    private const string Fallback = "관리자 요청을 처리하지 못했습니다.";
    private const string AccessToken = "test-only-admin-problem-access";
    private const string ListPath = "api/v1/admin/food-orders/operations";

    [Theory]
    [InlineData("<html>synthetic-private-gateway-detail</html>", "text/html", HttpStatusCode.BadGateway, null)]
    [InlineData("upstream failed: synthetic-private-gateway-detail", "text/plain", HttpStatusCode.ServiceUnavailable, null)]
    [InlineData("{\"debug\":\"synthetic-private-gateway-detail\"", "application/json", HttpStatusCode.InternalServerError, null)]
    [InlineData("{\"debug\":\"synthetic-private-gateway-detail\",\"errorCode\":\"FeatureDisabled\"}", "application/json", HttpStatusCode.NotFound, "FeatureDisabled")]
    public async Task 인정된메시지가없는오류본문은_원문대신_기존고정안내와_상태코드를전달한다(
        string body, string mediaType, HttpStatusCode status, string? errorCode)
    {
        using var storage = SecureStorage.UseForTest();
        using var fixture = await Fixture.CreateAsync(status, body, mediaType);

        var error = await Assert.ThrowsAsync<AdminApiException>(() => fixture.Api.GetAsync<AdminFoodOrderListDto>(ListPath));

        Assert.Equal(Fallback, error.Message);
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(errorCode, error.ErrorCode);
        AssertSafe(error.ToString());
        Assert.Single(fixture.Handler.Paths);
        Assert.True(fixture.Session.IsServerAdmin);
        Assert.Equal(AccessToken, fixture.Session.AccessToken);
    }

    [Theory]
    [InlineData("{\"detail\":\"상세 안내\",\"message\":\"메시지 안내\",\"title\":\"제목 안내\",\"errorCode\":\"RevisionConflict\"}", HttpStatusCode.Conflict, "상세 안내", "RevisionConflict")]
    [InlineData("{\"detail\":\" \",\"message\":\"메시지 안내\",\"title\":\"제목 안내\",\"errorCode\":\"ReviewRequired\"}", HttpStatusCode.UnprocessableEntity, "메시지 안내", "ReviewRequired")]
    [InlineData("{\"detail\":null,\"message\":7,\"title\":\"제목 안내\",\"errorCode\":\"FeatureDisabled\"}", HttpStatusCode.NotFound, "제목 안내", "FeatureDisabled")]
    public async Task 정형Problem의_detail_message_title_우선순위와_업무코드는_유지한다(
        string body, HttpStatusCode status, string expectedMessage, string expectedCode)
    {
        using var storage = SecureStorage.UseForTest();
        using var fixture = await Fixture.CreateAsync(status, body, "application/problem+json");

        var error = await Assert.ThrowsAsync<AdminApiException>(() => fixture.Api.GetAsync<AdminFoodOrderListDto>(ListPath));

        Assert.Equal(expectedMessage, error.Message);
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(expectedCode, error.ErrorCode);
        Assert.Single(fixture.Handler.Paths);
        Assert.True(fixture.Session.IsServerAdmin);
    }

    [Fact]
    public async Task 음식목록의_원문없는실패후_명시적재조회만으로_정상내용을받는다()
    {
        using var storage = SecureStorage.UseForTest();
        using var fixture = await Fixture.CreateAsync(HttpStatusCode.ServiceUnavailable,
            $"<html>{Untrusted}</html>", "text/html");
        var service = new AdminFoodOperationsService(fixture.Api);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.ListAsync("", 1));

        Assert.Equal(Fallback, error.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        AssertSafe(error.ToString());
        Assert.Single(fixture.Handler.Paths);

        fixture.Handler.Status = HttpStatusCode.OK;
        fixture.Handler.MediaType = "application/json";
        fixture.Handler.Body = """
            {"items":[{"orderNo":"SYNTHETIC-FOOD-12","restaurantName":"합성 매장","orderStatus":"접수"}],
             "totalCount":1,"page":1,"pageSize":20}
            """;

        var result = await service.ListAsync("", 1);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        var row = Assert.Single(result.Items);
        Assert.Equal("SYNTHETIC-FOOD-12", row.OrderNo);
        Assert.Equal("합성 매장", row.RestaurantName);
        Assert.Equal("접수", row.OrderStatus);
        Assert.Equal(new[] { "/" + ListPath, "/" + ListPath }, fixture.Handler.Paths);
        Assert.True(fixture.Session.IsServerAdmin);
        Assert.Equal(AccessToken, fixture.Session.AccessToken);
    }

    private static void AssertSafe(string message)
    {
        Assert.DoesNotContain(Untrusted, message);
        Assert.DoesNotContain("<html>", message);
        Assert.DoesNotContain(AccessToken, message);
    }

    private sealed class Fixture(HttpClient http, ResponseHandler handler, AdminAuthSession session) : IDisposable
    {
        internal ResponseHandler Handler { get; } = handler;
        internal AdminAuthSession Session { get; } = session;
        internal AdminAuthenticatedApiClient Api { get; } = new(http, session, new AdminAuthService(http, session));

        internal static async Task<Fixture> CreateAsync(HttpStatusCode status, string body, string mediaType)
        {
            // 기존 관리자 저장소 대역을 사용하며 장치 저장소나 실제 서버에는 접근하지 않습니다.
            var session = new AdminAuthSession(new ClientSessionGuard());
            await session.ApplyAsync(new 토큰응답
            {
                AccessToken = AccessToken,
                RefreshToken = "test-only-admin-problem-refresh",
                UserId = "synthetic-admin-problem-owner",
                UserName = "합성 운영자",
                Roles = ["서버관리자"],
                AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1)
            });
            var handler = new ResponseHandler { Status = status, Body = body, MediaType = mediaType };
            var http = new HttpClient(handler) { BaseAddress = new("https://admin-problem.example.test/") };
            return new(http, handler, session);
        }

        public void Dispose() => http.Dispose();
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        internal HttpStatusCode Status { get; set; }
        internal string Body { get; set; } = string.Empty;
        internal string MediaType { get; set; } = "application/json";
        internal List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(AccessToken, request.Headers.Authorization?.Parameter);
            Assert.Equal("/" + ListPath, request.RequestUri!.AbsolutePath);
            Paths.Add(request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, MediaType)
            });
        }
    }
}
