using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

/// <summary>실제 typed/protected/json client와 모의 HTTP handler의 계약 시험. 서버·기기·송금 실행이 아닙니다.</summary>
public sealed class 활동정산ClientR76Tests
{
    [Fact]
    public async Task 본인주간명세는_기간만보호GET으로보내고_타인ID나쓰기를추가하지않는다()
    {
        await using var f = new Fixture();
        await f.Client.WeeklyAsync(new DateOnly(2026, 10, 7), default);
        var sent = Assert.Single(f.Handler.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method); Assert.Equal("/api/v1/neighborhood-settlements/weekly?completionDateKst=2026-10-07", sent.Path);
        Assert.Equal("Bearer synthetic-worker-token", sent.Authorization); Assert.Null(sent.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task 주간HTTP실패는_빈명세로바뀌지않는다(HttpStatusCode status)
    {
        await using var f = new Fixture(status);
        var ex = await Assert.ThrowsAsync<SsalddelApiException>(() => f.Client.WeeklyAsync(null, default));
        Assert.Equal((int)status, ex.StatusCode); Assert.Single(f.Handler.Requests);
    }

    [Fact]
    public async Task 지급신고는_같은요청과판본과배분을POST하며_실패시자동재전송하지않는다()
    {
        await using var f = new Fixture(HttpStatusCode.ServiceUnavailable);
        var request = new NeighborhoodSettlementPaymentReportRequest { RequestId = Guid.NewGuid(), ExpectedRevision = 8,
            AmountKrw = 300, EvidenceReference = "당사자 지급 진술", Allocations = [new() { EntryId = "entry-1", AmountKrw = 300 }] };
        await Assert.ThrowsAsync<SsalddelApiException>(() => f.Client.ReportPaymentAsync("agreement-1", request, default));
        var sent = Assert.Single(f.Handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method); Assert.Equal("/api/v1/neighborhood-settlements/agreement-1/payment-reports", sent.Path);
        Assert.Equal("Bearer synthetic-worker-token", sent.Authorization);
        var body = JsonSerializer.Deserialize<NeighborhoodSettlementPaymentReportRequest>(sent.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(request.RequestId, body.RequestId); Assert.Equal(8, body.ExpectedRevision);
        Assert.Equal(300m, body.AmountKrw); Assert.Equal("entry-1", Assert.Single(body.Allocations).EntryId);
    }

    private sealed class Fixture(HttpStatusCode status = HttpStatusCode.OK) : IAsyncDisposable
    {
        public Handler Handler { get; } = new(status);
        private readonly SsalddelIsmsPClientEncryptionService encryption = new(new NoJs());
        private HttpClient? http;
        public 활동정산Client Client => new(new SsalddelJsonApiClient(new SsalddelProtectedApiClient(
            http ??= new(Handler) { BaseAddress = new("https://test.invalid/") }, encryption, new Token())));
        public async ValueTask DisposeAsync() { http?.Dispose(); await encryption.DisposeAsync(); }
    }
    private sealed record Capture(HttpMethod Method, string? Path, string? Authorization, string? Body);
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<Capture> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new(request.Method, request.RequestUri?.PathAndQuery, request.Headers.Authorization?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return new(status) { Content = status == HttpStatusCode.OK ? JsonContent.Create(new WorkerWeeklyStatementDto { WorkerUserId = "worker" })
                : JsonContent.Create(new { errorCode = "SyntheticUnavailable", message = "일시 오류" }) };
        }
    }
    private sealed class Token : ISsalddelAccessTokenProvider { public string? AccessToken => "synthetic-worker-token"; }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => throw new InvalidOperationException("This contract has no raw protected personal fields.");
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
}
