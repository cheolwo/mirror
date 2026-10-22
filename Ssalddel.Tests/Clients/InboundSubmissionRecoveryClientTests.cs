using System.Net;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 인증·JSON·역할 adapter를 통한 모의 HTTP 조회입니다. 운영 HTTP 증거와 구분합니다.</summary>
public sealed class InboundSubmissionRecoveryClientTests
{
    private static readonly Guid RequestId = Guid.Parse("0cb9db56-c46b-4f28-a97d-fc5d381dfadd");

    [Fact]
    public async Task 원요청조회는_인증된정확한GuidGET으로정본과요청번호를읽는다()
    {
        await using var fixture = new Fixture(HttpStatusCode.OK);
        var result = await fixture.Client.GetInboundByClientRequestAsync(RequestId);
        Assert.Equal(77, result?.Id);
        Assert.Equal(RequestId, result?.ClientRequestId);
        AssertReadOnly(fixture.Handler);
    }

    [Fact]
    public async Task 조회404는_결과미확인null이며자동POST를보내지않는다()
    {
        await using var fixture = new Fixture(HttpStatusCode.NotFound);
        Assert.Null(await fixture.Client.GetInboundByClientRequestAsync(RequestId));
        AssertReadOnly(fixture.Handler);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task 조회실패는_미확인null대신HTTP상태를보존한다(HttpStatusCode status)
    {
        await using var fixture = new Fixture(status);
        var error = await Assert.ThrowsAsync<SsalddelApiException>(() => fixture.Client.GetInboundByClientRequestAsync(RequestId));
        Assert.Equal((int)status, error.StatusCode);
        AssertReadOnly(fixture.Handler);
    }

    [Fact]
    public async Task 빈원요청번호는_HTTP로전송하지않는다()
    {
        await using var fixture = new Fixture(HttpStatusCode.OK);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Client.GetInboundByClientRequestAsync(Guid.Empty));
        Assert.Empty(fixture.Handler.Requests);
    }

    private static void AssertReadOnly(Handler handler)
    {
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"/api/v1/warehouse-operations/inbounds/by-client-request/{RequestId:D}", request.Path);
        Assert.Equal("Bearer", request.Scheme);
        Assert.Equal("synthetic-inbound-lookup-token", request.Token);
        Assert.False(request.HasBody);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Handler Handler { get; }
        public WebShipperWarehouseWorkspaceService Client { get; }
        private readonly HttpClient http;
        private readonly SsalddelIsmsPClientEncryptionService encryption;
        public Fixture(HttpStatusCode status)
        {
            Handler = new(status);
            http = new(Handler) { BaseAddress = new("https://test.invalid/") };
            encryption = new(new NoJs());
            Client = new(new SsalddelJsonApiClient(new SsalddelProtectedApiClient(http, encryption, new TokenProvider())));
        }
        public async ValueTask DisposeAsync() { http.Dispose(); await encryption.DisposeAsync(); }
    }
    private sealed record RequestCapture(HttpMethod Method, string? Path, string? Scheme, string? Token, bool HasBody);
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<RequestCapture> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.Method, request.RequestUri?.PathAndQuery,
                request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter, request.Content is not null));
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = status == HttpStatusCode.OK
                    ? JsonContent.Create(new 입고요청항목응답 { Id = 77, ClientRequestId = RequestId })
                    : JsonContent.Create(new { message = "synthetic lookup response" })
            });
        }
    }
    private sealed class TokenProvider : ISsalddelAccessTokenProvider
    { public string? AccessToken => "synthetic-inbound-lookup-token"; }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("조회는 JS 암호화나 쓰기를 호출하지 않습니다.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
            => throw new InvalidOperationException("조회는 JS 암호화나 쓰기를 호출하지 않습니다.");
    }
}
