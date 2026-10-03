using System.Net;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverAuthenticationRecoveryTests
{
    [Theory]
    [InlineData("connection", null)]
    [InlineData("timeout", null)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    public async Task RefreshTransientFailure_PreservesSessionAndFailureStatus(string failure, HttpStatusCode? expectedStatus)
    {
        var session = new FDriverTestSession(expired: true);
        var handler = new FDriverTestHttpHandler((_, _) => failure switch
        {
            "connection" => Task.FromException<HttpResponseMessage>(new HttpRequestException("test connection failure")),
            "timeout" => Task.FromException<HttpResponseMessage>(new TaskCanceledException("test timeout")),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        });
        using var http = Client(handler);
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));

        var exception = await Assert.ThrowsAsync<FDriverApiException>(() => api.GetWorkspaceAsync());

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.Equal(0, session.ClearCount);
        Assert.Equal(ClientAuthSessionRestoreState.RefreshRequired, session.CurrentState);
        Assert.Equal(["/api/v1/auth/refresh"], handler.Paths);
    }

    [Fact]
    public async Task WorkspaceUnauthorized_TransientForcedRefresh_DoesNotEraseSessionOrRetryWorkspace()
    {
        var session = new FDriverTestSession();
        var handler = new FDriverTestHttpHandler((request, _) => request.RequestUri!.AbsolutePath.Contains("auth/refresh", StringComparison.Ordinal)
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("test connection failure"))
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = Client(handler);
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));

        var exception = await Assert.ThrowsAsync<FDriverApiException>(() => api.GetWorkspaceAsync());

        Assert.Null(exception.StatusCode);
        Assert.Equal(0, session.ClearCount);
        Assert.True(session.IsAuthenticated);
        Assert.Equal(["/api/v1/driver/food-deliveries/workspace", "/api/v1/auth/refresh"], handler.Paths);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RefreshCredentialRejection_ReportsUnauthorizedAndClearsSession(HttpStatusCode rejection)
    {
        var session = new FDriverTestSession(expired: true);
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(rejection)));
        using var http = Client(handler);
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));

        var exception = await Assert.ThrowsAsync<FDriverApiException>(() => api.GetWorkspaceAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(1, session.ClearCount);
        Assert.Equal(ClientAuthSessionRestoreState.Anonymous, session.CurrentState);
    }

    [Fact]
    public async Task RefreshRejection_WhenDeviceRemovalFailsAfterMemoryClear_StillReportsUnauthorized()
    {
        var session = new FDriverTestSession(expired: true) { ClearFailure = new InvalidOperationException("test storage failure") };
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = Client(handler);
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));

        var exception = await Assert.ThrowsAsync<FDriverApiException>(() => api.GetWorkspaceAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(ClientAuthSessionRestoreState.Anonymous, session.CurrentState);
    }

    [Fact]
    public async Task WorkspaceStillUnauthorized_AfterSuccessfulRefresh_RemainsAnAuthenticationFailure()
    {
        var session = new FDriverTestSession();
        var handler = new FDriverTestHttpHandler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.Contains("auth/refresh", StringComparison.Ordinal)
                ? FDriverTestHttpHandler.TokenResponse()
                : new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = Client(handler);
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));

        var exception = await Assert.ThrowsAsync<FDriverApiException>(() => api.GetWorkspaceAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(1, session.ApplyCount);
        Assert.Equal(["/api/v1/driver/food-deliveries/workspace", "/api/v1/auth/refresh", "/api/v1/driver/food-deliveries/workspace"], handler.Paths);
    }

    [Fact]
    public async Task CancelledRefresh_LateSuccessfulResponse_DoesNotApplyToken()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new FDriverTestSession(expired: true);
        var handler = new FDriverTestHttpHandler((_, _) => { entered.SetResult(); return response.Task; });
        using var http = Client(handler);
        using var cancellation = new CancellationTokenSource();
        var auth = new FDriverAuthApiService(http, session);
        var refresh = auth.EnsureAccessTokenResultAsync(cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cancellation.Cancel();
        response.SetResult(FDriverTestHttpHandler.TokenResponse());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(0, session.ApplyCount);
        Assert.Equal(0, session.ClearCount);
    }

    private static HttpClient Client(HttpMessageHandler handler)
        => new(handler) { BaseAddress = new Uri("http://localhost/") };
}
