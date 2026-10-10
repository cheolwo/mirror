using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FDriverApp.Services;
using Ssalddel.Contracts.Mart;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverMartDeliveryApiClientTests
{
    [Fact]
    public async Task CommandsUseDedicatedRoutesAndTypedRevisionRequest_WithoutFoodFallback()
    {
        var bodies = new List<JsonElement>(); var auth = new FDriverTestSession();
        var handler = new FDriverTestHttpHandler(async (request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-access", request.Headers.Authorization?.Parameter);
            if (request.Content is not null)
                bodies.Add(JsonDocument.Parse(await request.Content.ReadAsStringAsync()).RootElement.Clone());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(FDriverMartDeliveryWorkspaceTests.Item()) };
        });
        var api = Client(handler, auth); var id = Guid.NewGuid();
        await api.AcceptAsync("offer-1", new() { ClientRequestId = id, ExpectedRecommendationRound = 4, ExpectedQueueUpdatedAtUtc = DateTime.UnixEpoch });
        await api.RejectAsync("offer-1", new() { ClientRequestId = id, ExpectedRecommendationRound = 4 });
        await api.PickupAsync("offer-1", new() { ClientRequestId = id, ExpectedRevision = 7, ExpectedLabelRevision = 2, Code = "1234", ByNumber = true });
        await api.DeliverAsync("offer-1", new() { ClientRequestId = id, ExpectedRevision = 8 });
        Assert.Equal(["/api/v1/driver/mart-deliveries/offer-1/accept", "/api/v1/driver/mart-deliveries/offer-1/reject",
            "/api/v1/driver/mart-deliveries/offer-1/pickup", "/api/v1/driver/mart-deliveries/offer-1/deliver"], handler.Paths);
        Assert.All(bodies, body => Assert.Equal(id, body.GetProperty("clientRequestId").GetGuid()));
        Assert.Equal(7, bodies[2].GetProperty("expectedRevision").GetInt64());
        Assert.Equal(2, bodies[2].GetProperty("expectedLabelRevision").GetInt64());
        Assert.True(bodies[2].GetProperty("byNumber").GetBoolean());
    }

    [Fact]
    public async Task LostPostResponseDoesNotAutomaticallyRetry()
    {
        var handler = new FDriverTestHttpHandler((_, _) => throw new HttpRequestException("transport-secret"));
        var api = Client(handler, new FDriverTestSession());
        var error = await Assert.ThrowsAsync<FDriverApiException>(() => api.PickupAsync("offer", new() { ClientRequestId = Guid.NewGuid(), Code = "1234" }));
        Assert.Single(handler.Paths); Assert.DoesNotContain("transport-secret", error.Message);
    }

    [Fact]
    public async Task Post401DoesNotRefreshAndRepeatTheCommand()
    {
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        await Assert.ThrowsAsync<FDriverApiException>(() => Client(handler, new FDriverTestSession()).AcceptAsync("offer", new() { ClientRequestId = Guid.NewGuid() }));
        Assert.Equal(["/api/v1/driver/mart-deliveries/offer/accept"], handler.Paths);
    }

    [Fact]
    public async Task Read401CanRefreshThenRequery_ButDoesNotExecuteWork()
    {
        var first = true;
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/v1/auth/refresh") return Task.FromResult(FDriverTestHttpHandler.TokenResponse());
            Assert.Equal(HttpMethod.Get, request.Method);
            if (first) { first = false; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new 마트기사업무목록Dto { Enabled = false }) });
        });
        Assert.False((await Client(handler, new FDriverTestSession()).ListAsync()).Enabled);
        Assert.Equal(["/api/v1/driver/mart-deliveries", "/api/v1/auth/refresh", "/api/v1/driver/mart-deliveries"], handler.Paths);
    }

    [Fact]
    public async Task AccountChangeDuringHttpDiscardsTheOldResponse()
    {
        var auth = new FDriverTestSession();
        var handler = new FDriverTestHttpHandler(async (_, _) =>
        {
            await auth.ClearAsync();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 마트기사업무목록Dto { Enabled = true }) };
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler, auth).ListAsync());
    }

    [Fact]
    public async Task StructuredFeatureDisabled404MapsToNoticeOnlyForListGet()
    {
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        { Content = JsonContent.Create(new { errorCode = "FeatureDisabled", detail = "server-detail-not-for-display" }) }));
        var api = Client(handler, new FDriverTestSession());
        var list = await api.ListAsync();
        Assert.False(list.Enabled); Assert.Empty(list.Items);
        Assert.Contains("비활성화", list.Notice); Assert.DoesNotContain("server-detail", list.Notice);
        await Assert.ThrowsAsync<FDriverApiException>(() => api.GetAsync("offer"));
        await Assert.ThrowsAsync<FDriverApiException>(() => api.ResultAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<FDriverApiException>(() => api.AcceptAsync("offer", new() { ClientRequestId = Guid.NewGuid() }));
        Assert.Equal(4, handler.Paths.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "MartDriverRequestNotFound")]
    [InlineData(HttpStatusCode.NotFound, "featuredisabled")]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "FeatureDisabled")]
    [InlineData(HttpStatusCode.Unauthorized, "FeatureDisabled")]
    [InlineData(HttpStatusCode.Forbidden, "FeatureDisabled")]
    public async Task ListErrorsOtherThanExactStructuredFeatureDisabled404RemainFailures(HttpStatusCode status, string code)
    {
        var handler = new FDriverTestHttpHandler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath == "/api/v1/auth/refresh" ? FDriverTestHttpHandler.TokenResponse()
                : new HttpResponseMessage(status) { Content = JsonContent.Create(new { errorCode = code }) }));
        var error = await Assert.ThrowsAsync<FDriverApiException>(() => Client(handler, new FDriverTestSession()).ListAsync());
        Assert.Equal(status, error.StatusCode);
    }

    [Theory]
    [InlineData(true, "MartPickupCodeMismatch", HttpStatusCode.BadRequest, true)]
    [InlineData(false, "MartPickupCodeMismatch", HttpStatusCode.BadRequest, false)]
    [InlineData(true, "", HttpStatusCode.BadRequest, false)]
    [InlineData(true, "MartBackendUnavailable", HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(true, "MartAccessChanged", HttpStatusCode.Forbidden, false)]
    public async Task StructuredRejectionIsNotInferredFromHttpStatusAlone(bool rejected, string code, HttpStatusCode status, bool expected)
    {
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        { Content = JsonContent.Create(new { errorCode = code, commandRejected = rejected, detail = "QR-raw-secret" }) }));
        var error = await Assert.ThrowsAsync<FDriverApiException>(() => Client(handler, new FDriverTestSession()).DeliverAsync("offer", new() { ClientRequestId = Guid.NewGuid() }));
        Assert.Equal(expected, 살뜰마트배송ApiClient.IsConfirmedCommandRejection(error));
        Assert.DoesNotContain("QR-raw-secret", error.Message);
        Assert.DoesNotContain("QR-raw-secret", string.Join(",", error.Data.Values.Cast<object>()));
    }

    [Fact]
    public async Task ReceiptReadIsGetWithStableGuid_And404RemainsUnknown()
    {
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Null(request.Content);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            { Content = JsonContent.Create(new { errorCode = "MartDriverRequestNotFound" }) });
        });
        var id = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<FDriverApiException>(() => Client(handler, new FDriverTestSession()).ResultAsync(id));
        Assert.Equal($"/api/v1/driver/mart-deliveries/requests/{id:D}", Assert.Single(handler.Paths));
        Assert.False(살뜰마트배송ApiClient.IsConfirmedCommandRejection(error));
    }

    private static 살뜰마트배송ApiClient Client(FDriverTestHttpHandler handler, FDriverTestSession auth)
    {
        var http = new HttpClient(handler) { BaseAddress = new("https://localhost.test/") };
        return new(http, auth, new FDriverAuthApiService(http, auth));
    }
}
