using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;
using SsalddelAdminApp.Services;

namespace Ssalddel.Tests.Clients
{
    public sealed class AdminFoodOperationsClientTests
    {
        [Fact]
        public async Task 음식목록조회는_음식경로와관리자토큰만사용한다()
        {
            using var storage = SecureStorage.UseForTest();
            using var fixture = await CreateAsync(_ => Json(HttpStatusCode.OK, new AdminFoodOrderListDto { Page = 2 }));
            var result = await fixture.Service.ListAsync("식당 & 주문", 2);
            var call = Assert.Single(fixture.Handler.Calls);
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal("/api/v1/admin/food-orders/operations", call.Uri.AbsolutePath);
            Assert.Contains("query=" + Uri.EscapeDataString("식당 & 주문"), call.Uri.Query);
            Assert.Contains("page=2", call.Uri.Query);
            Assert.Equal("fixture-access", call.Bearer);
            Assert.Equal(2, result.Page);
        }

        [Fact]
        public async Task 없는주문과_음식기능비활성은_서로다른결과다()
        {
            using var storage = SecureStorage.UseForTest();
            using var fixture = await CreateAsync(_ => Json(HttpStatusCode.NotFound, new { errorCode = "FeatureDisabled", detail = "disabled" }));
            await Assert.ThrowsAsync<AdminFoodWorkflowUnavailableException>(() => fixture.Service.조회Async("FOOD-ONE"));
            Assert.Single(fixture.Handler.Calls);
        }

        [Fact]
        public async Task 실제주문미존재만_null로반환한다()
        {
            using var storage = SecureStorage.UseForTest();
            using var fixture = await CreateAsync(_ => Json(HttpStatusCode.NotFound, new { title = "Not Found" }));
            Assert.Null(await fixture.Service.조회Async("FOOD-ONE"));
            Assert.Single(fixture.Handler.Calls);
        }

        [Fact]
        public async Task 검토충돌은_revision과요청ID를보존하고_호출자에게409를전달한다()
        {
            using var storage = SecureStorage.UseForTest();
            using var fixture = await CreateAsync(_ => Json(HttpStatusCode.Conflict, new { detail = "conflict", errorCode = "RevisionConflict" }));
            var body = ReviewRequest();
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Service.중단검토Async("attempt-one", body));
            Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
            var call = Assert.Single(fixture.Handler.Calls);
            Assert.Equal(HttpMethod.Put, call.Method);
            Assert.Equal("/api/v1/admin/food-orders/delivery-attempts/attempt-one/interruption-review", call.Uri.AbsolutePath);
            AssertPayload(body, call.Body);
        }

        [Fact]
        public async Task 검토401갱신후_동일한원요청을한번만재전송한다()
        {
            using var storage = SecureStorage.UseForTest();
            var putCount = 0;
            using var fixture = await CreateAsync(call =>
            {
                if (call.Uri.AbsolutePath == "/api/v1/auth/refresh") return Json(HttpStatusCode.OK, Token("refreshed-access"));
                if (++putCount == 1) return Json(HttpStatusCode.Unauthorized, new { title = "expired" });
                return Json(HttpStatusCode.OK, new 음식배달시도운영응답 { 시도StableId = "attempt-one", Revision = 8 });
            });
            var body = ReviewRequest();
            var result = await fixture.Service.중단검토Async("attempt-one", body);
            var puts = fixture.Handler.Calls.Where(x => x.Method == HttpMethod.Put).ToArray();
            Assert.Equal(2, puts.Length);
            Assert.All(puts, call => AssertPayload(body, call.Body));
            Assert.Equal("fixture-access", puts[0].Bearer);
            Assert.Equal("refreshed-access", puts[1].Bearer);
            Assert.Single(fixture.Handler.Calls.Where(x => x.Uri.AbsolutePath == "/api/v1/auth/refresh"));
            Assert.Equal(8, result.Revision);
        }

        [Fact]
        public async Task 미인증은_음식업무HTTP를보내지않는다()
        {
            using var storage = SecureStorage.UseForTest();
            using var handler = new Handler(_ => throw new InvalidOperationException("업무 HTTP 금지"));
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/") };
            var session = new AdminAuthSession(new ClientSessionGuard());
            var service = new AdminFoodOperationsService(new AdminAuthenticatedApiClient(http, session, new AdminAuthService(http, session)));
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.ListAsync("", 1));
            Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
            Assert.Empty(handler.Calls);
        }

        [Theory]
        [InlineData("\"gateway\"")]
        [InlineData("<html>unavailable</html>")]
        public async Task 비정상오류본문은_프로토콜예외나빈목록성공으로바꾸지않는다(string content)
        {
            using var storage = SecureStorage.UseForTest();
            using var fixture = await CreateAsync(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
            { Content = new StringContent(content, Encoding.UTF8, "application/json") });
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Service.ListAsync("", 1));
            Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
            Assert.Single(fixture.Handler.Calls);
        }

        private static 음식배달중단검토요청 ReviewRequest() => new()
        {
            클라이언트요청Id = Guid.NewGuid(), 예상Revision = 7,
            판정Code = 음식배달중단검토판정Code.보호, 판정사유 = "확인한 근거", 악용확정여부 = false
        };

        private static void AssertPayload(음식배달중단검토요청 expected, string? json)
        {
            var actual = JsonSerializer.Deserialize<음식배달중단검토요청>(json!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(expected.클라이언트요청Id, actual.클라이언트요청Id);
            Assert.Equal(expected.예상Revision, actual.예상Revision);
            Assert.Equal(expected.판정Code, actual.판정Code);
            Assert.Equal(expected.판정사유, actual.판정사유);
            Assert.Equal(expected.악용확정여부, actual.악용확정여부);
        }

        private static 토큰응답 Token(string access = "fixture-access") => new()
        {
            AccessToken = access, RefreshToken = "fixture-refresh", UserId = "fixture-admin", UserName = "검증 운영자",
            Roles = ["서버관리자"], AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1), RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        };

        private static HttpResponseMessage Json<T>(HttpStatusCode status, T value) => new(status) { Content = JsonContent.Create(value) };

        private static async Task<Fixture> CreateAsync(Func<Call, HttpResponseMessage> response)
        {
            var handler = new Handler(response);
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/") };
            var session = new AdminAuthSession(new ClientSessionGuard());
            await session.ApplyAsync(Token());
            return new(http, handler, new AdminFoodOperationsService(new AdminAuthenticatedApiClient(http, session, new AdminAuthService(http, session))));
        }

        private sealed record Fixture(HttpClient Http, Handler Handler, AdminFoodOperationsService Service) : IDisposable
        { public void Dispose() => Http.Dispose(); }

        private sealed record Call(HttpMethod Method, Uri Uri, string? Body, string? Bearer);
        private sealed class Handler(Func<Call, HttpResponseMessage> response) : HttpMessageHandler
        {
            public List<Call> Calls { get; } = [];
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                var call = new Call(request.Method, request.RequestUri!, request.Content is null ? null : await request.Content.ReadAsStringAsync(token), request.Headers.Authorization?.Parameter);
                Calls.Add(call);
                return response(call);
            }
        }
    }
}

// 관리자 장치 저장소 경계만 격리합니다. 실제 APK/SecureStorage 검증을 의미하지 않습니다.
// 다른 역할의 네임스페이스별 저장소 어댑터와 공유하지 않습니다.
namespace SsalddelAdminApp.Services
{
    internal static class SecureStorage
    {
        private static readonly AsyncLocal<MemoryStorage?> Current = new();
        public static MemoryStorage Default => Current.Value ?? throw new InvalidOperationException("시험 저장소 범위가 필요합니다.");
        internal static IDisposable UseForTest()
        {
            var previous = Current.Value; Current.Value = new();
            return new Scope(previous);
        }
        private sealed class Scope(MemoryStorage? previous) : IDisposable
        { public void Dispose() => Current.Value = previous; }
        public sealed class MemoryStorage
        {
            private readonly Dictionary<string, string> values = [];
            public Task<string?> GetAsync(string key) => Task.FromResult(values.GetValueOrDefault(key));
            public Task SetAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
            public bool Remove(string key) => values.Remove(key);
        }
    }
}
