using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.WebApp;

public sealed class DriverWebCurrentTransportReadContractTests
{
    [Fact]
    public async Task 현재_운송_없음을_명시한_404는_빈_결과로_읽는다()
    {
        using var client = CreateClient(HttpStatusCode.NotFound,
            """{"status":404,"errorCode":"CargoTransportNotFound"}""");
        var service = CreateService(client);

        Assert.Null(await service.현재운송조회Async());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"status":404,"errorCode":"OtherResourceNotFound"}""")]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"status":503,"errorCode":"CargoTransportNotFound"}""")]
    [InlineData(HttpStatusCode.Unauthorized, "")]
    [InlineData(HttpStatusCode.Forbidden, "")]
    public async Task 다른_오류는_빈_결과로_읽지_않고_HTTP_상태를_보존한다(
        HttpStatusCode statusCode,
        string body)
    {
        using var client = CreateClient(statusCode, body);
        var service = CreateService(client);

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => service.현재운송조회Async());

        Assert.Equal(statusCode, failure.StatusCode);
    }

    [Fact]
    public async Task 성공_응답에_운송_본문이_없으면_조회_오류로_처리한다()
    {
        using var client = CreateClient(HttpStatusCode.OK, "null");
        var service = CreateService(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.현재운송조회Async());
    }

    [Fact]
    public async Task 정상_응답은_운송_정보를_반환한다()
    {
        using var client = new HttpClient(new CurrentTransportHandler(
            HttpStatusCode.OK,
            JsonContent.Create(new 기사운송요약응답 { Id = 42, 운송번호 = "CURRENT-42" })))
        {
            BaseAddress = new Uri("https://driver.test/")
        };
        var service = CreateService(client);

        var result = await service.현재운송조회Async();

        Assert.NotNull(result);
        Assert.Equal(42, result.Id);
        Assert.Equal("CURRENT-42", result.운송번호);
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string body)
        => new(new CurrentTransportHandler(
            statusCode,
            new StringContent(body, Encoding.UTF8, "application/json")))
        {
            BaseAddress = new Uri("https://driver.test/")
        };

    private static 기사운송증빙Service CreateService(HttpClient client)
    {
        // The test project also compiles a linked auth class. Use the auth type from the service assembly.
        var authType = typeof(기사운송증빙Service).Assembly.GetType(
            "Ssalddel.WebApp.Services.WebAuthSessionService", throwOnError: true)!;
        var auth = Activator.CreateInstance(authType, client, new SessionStorage(), new ClientSessionGuard());
        return (기사운송증빙Service)Activator.CreateInstance(
            typeof(기사운송증빙Service), client, auth, new TransportRequestLedgerObserver())!;
    }

    private sealed class CurrentTransportHandler(HttpStatusCode statusCode, HttpContent content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/v1/driver/transports/current", request.RequestUri?.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("synthetic-access", request.Headers.Authorization?.Parameter);

            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
        }
    }

    private sealed class SessionStorage : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args)
            => InvokeAsync<T>(identifier, CancellationToken.None, args);

        public ValueTask<T> InvokeAsync<T>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("localStorage.getItem", identifier);
            var snapshot = new ClientAuthTokenSnapshot(
                "synthetic-access", DateTime.UtcNow.AddHours(1),
                "synthetic-refresh", DateTime.UtcNow.AddDays(1),
                "driver-42", "합성 기사", ["기사"]);
            return ValueTask.FromResult((T)(object)JsonSerializer.Serialize(snapshot));
        }
    }
}
