using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

public sealed class FoodOrderInterruptionReviewClientTests
{
    [Fact]
    public async Task 검토는기존PUT계약의시도Id판본요청Id근거를그대로전달한다()
    {
        음식배달중단검토요청? submitted = null;
        var id = Guid.NewGuid();
        using var handler = new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/api/v1/admin/food-orders/delivery-attempts/attempt%20one/interruption-review", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("synthetic-token", request.Headers.Authorization.Parameter);
            submitted = await request.Content!.ReadFromJsonAsync<음식배달중단검토요청>();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 음식배달시도운영응답 { 시도StableId = "attempt one", Revision = 12 }) };
        });
        var result = await Create(handler).중단검토Async(" attempt one ", new()
        {
            클라이언트요청Id = id, 예상Revision = 11, 판정Code = 음식배달중단검토판정Code.보호,
            악용확정여부 = false, 판정사유 = "사고 확인 근거"
        });
        Assert.Equal(id, submitted!.클라이언트요청Id);
        Assert.Equal(11, submitted.예상Revision);
        Assert.Equal(음식배달중단검토판정Code.보호, submitted.판정Code);
        Assert.Equal("사고 확인 근거", submitted.판정사유);
        Assert.False(submitted.악용확정여부);
        Assert.Equal(12, result.Revision);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(409)]
    [InlineData(503)]
    public async Task HTTP실패코드를숨기거나성공기본값으로채우지않는다(int status)
    {
        using var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).중단검토Async("attempt", new()));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
    }

    [Fact]
    public async Task 정상응답의null도검토완료로간주하지않는다()
    {
        using var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json") }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).중단검토Async("attempt", new()));
    }

    private static FoodOrderOperationsTraceAdminService Create(HttpMessageHandler handler)
    {
        var session = new 관리자인증세션Service(new ConfigurationBuilder().Build(), new Environment());
        session.로그인적용(new() { AccessToken = "synthetic-token", UserId = "synthetic-admin", Roles = ["서버관리자"] });
        return new(new HttpClient(handler) { BaseAddress = new("https://example.invalid/") }, session);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => respond(request);
    }
    private sealed class Environment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
