using System.Net;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

/// <summary>실제 protected/json/typed client를 통한 모의 HTTP 시험. 운영 서버 실행과 구별합니다.</summary>
public sealed class FoodOrderSubmissionRecoveryClientTests
{
    private static readonly Guid RequestId = Guid.Parse("c2d9ba10-bd5d-49d1-967a-fb806c103aaa");

    [Fact]
    public async Task 접수조회는_정확한GuidGET경로와현재인증헤더로읽기만수행한다()
    {
        await using var fixture = new Fixture(HttpStatusCode.OK);

        var result = await fixture.Client.접수결과Async(RequestId);

        Assert.Equal("FOOD-EXISTING", result!.주문번호);
        AssertReadOnlyRequest(fixture.Handler);
    }

    [Fact]
    public async Task 접수조회404는_미접수값으로반환하며POST하지않는다()
    {
        await using var fixture = new Fixture(HttpStatusCode.NotFound);

        Assert.Null(await fixture.Client.접수결과Async(RequestId));

        AssertReadOnlyRequest(fixture.Handler);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task 그외HTTP실패는_미접수null로오인하지않고상태코드있는예외로남긴다(HttpStatusCode status)
    {
        await using var fixture = new Fixture(status);

        var failure = await Assert.ThrowsAsync<SsalddelApiException>(() => fixture.Client.접수결과Async(RequestId));

        Assert.Equal((int)status, failure.StatusCode);
        Assert.Contains("접수 결과 조회", failure.OperationName);
        AssertReadOnlyRequest(fixture.Handler);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task 저장된제출복구는_실제HTTP조회결과만반영하고자동재제출하지않는다(HttpStatusCode status)
    {
        await using var fixture = new Fixture(status);
        var store = new PendingStore(new("owner", new()
        {
            클라이언트요청Id = RequestId, 음식점Id = 7,
            상품목록 = []
        }, DateTime.UtcNow));
        using var recovery = new FoodOrderSubmissionRecoveryViewModel(store, fixture.Client, fixture.Client);

        await recovery.계정설정Async("owner");

        AssertReadOnlyRequest(fixture.Handler);
        if (status == HttpStatusCode.OK)
        {
            Assert.Equal("FOOD-EXISTING", recovery.접수주문번호);
            Assert.Null(recovery.Pending);
            Assert.Null(store.Pending);
            Assert.False(recovery.입력잠금);
        }
        else
        {
            Assert.NotNull(recovery.Pending);
            Assert.NotNull(store.Pending);
            Assert.Null(recovery.접수주문번호);
            Assert.True(recovery.입력잠금);
            Assert.Equal(status == HttpStatusCode.NotFound, recovery.미접수확인됨);
            Assert.Equal(status != HttpStatusCode.NotFound, recovery.오류발생);
            if (status != HttpStatusCode.NotFound) Assert.Equal((int)status, recovery.오류!.Http상태코드);
        }
    }

    [Fact]
    public async Task 빈요청ID는_HTTP를보내지않는다()
    {
        await using var fixture = new Fixture(HttpStatusCode.OK);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Client.접수결과Async(Guid.Empty));

        Assert.Empty(fixture.Handler.Requests);
    }

    private static void AssertReadOnlyRequest(RecordingHandler handler)
    {
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"/api/v1/food-orders/client-requests/{RequestId:D}", request.Path);
        Assert.Equal("Bearer", request.Scheme);
        Assert.Equal("synthetic-read-only-token", request.Token);
        Assert.False(request.HasBody);
        Assert.DoesNotContain(handler.Requests, x => x.Method == HttpMethod.Post);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public RecordingHandler Handler { get; }
        public 주문자음식주문Client Client { get; }
        private readonly HttpClient http;
        private readonly SsalddelIsmsPClientEncryptionService encryption;
        public Fixture(HttpStatusCode status)
        {
            Handler = new(status);
            http = new(Handler) { BaseAddress = new("https://test.invalid/") };
            encryption = new(new NoJs());
            ISsalddelJsonApiClient json = new SsalddelJsonApiClient(
                new SsalddelProtectedApiClient(http, encryption, new TokenProvider()));
            Client = new(json);
        }
        public async ValueTask DisposeAsync()
        { http.Dispose(); await encryption.DisposeAsync(); }
    }

    private sealed record RequestCapture(HttpMethod Method, string? Path, string? Scheme, string? Token, bool HasBody);
    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<RequestCapture> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.Method, request.RequestUri?.PathAndQuery,
                request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter, request.Content is not null));
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = status == HttpStatusCode.OK
                    ? JsonContent.Create(new 음식주문접수결과응답 { 주문번호 = "FOOD-EXISTING" })
                    : JsonContent.Create(new { message = "synthetic lookup response" })
            });
        }
    }
    private sealed class TokenProvider : ISsalddelAccessTokenProvider
    { public string? AccessToken => "synthetic-read-only-token"; }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("접수 결과 GET은 JS 암호화나 쓰기를 호출하지 않습니다.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new InvalidOperationException("접수 결과 GET은 JS 암호화나 쓰기를 호출하지 않습니다.");
    }
    private sealed class PendingStore(FoodOrderPendingSubmission? value) : IFoodOrderPendingSubmissionStore
    {
        public FoodOrderPendingSubmission? Pending { get; private set; } = value;
        public Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Pending);
        public Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("접수 결과 조회는 저장된 요청을 재제출하지 않습니다.");
        public Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default)
        {
            if (Pending?.Request.클라이언트요청Id == requestId) Pending = null;
            return Task.CompletedTask;
        }
    }
}
