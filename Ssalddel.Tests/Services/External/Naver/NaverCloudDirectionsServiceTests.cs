using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using 살뜰.Services.External.Naver;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.External.Naver;

public sealed class NaverCloudDirectionsServiceTests
{
    [Fact]
    public async Task RequestedOptionAndMeterUnitsArePreserved()
    {
        var handler = new Handler("""
            {"code":0,"route":{"traoptimal":[{"summary":{"distance":9000}}],
            "traavoidcaronly":[{"summary":{"distance":2345,"duration":120000}}]}}
            """);
        var route = await Client(handler).GetDrivingRouteAsync(37.5m, 127m, 37.51m, 127.01m, "traavoidcaronly");
        Assert.Equal(2.345m, route!.DistanceKm);
        Assert.Equal("traavoidcaronly", route.RouteType);
        Assert.Equal(TimeSpan.FromMinutes(2), route.Duration);
        var query = Uri.UnescapeDataString(handler.Uri!.Query);
        Assert.Contains("start=127,37.5", query);
        Assert.Contains("goal=127.01,37.51", query);
        Assert.Contains("option=traavoidcaronly", query);
    }

    [Theory]
    [InlineData("{\"code\":3,\"route\":{\"trafast\":[{\"summary\":{\"distance\":1000}}]}}")]
    [InlineData("{\"code\":0,\"route\":{\"trafast\":[{\"summary\":{\"duration\":1000}}]}}")]
    [InlineData("{\"code\":0,\"route\":{\"trafast\":[{\"summary\":{\"distance\":-1}}]}}")]
    [InlineData("{\"code\":0,\"route\":{\"traoptimal\":[{\"summary\":{\"distance\":1000}}]}}")]
    public async Task FailedOrUnrequestedRouteCannotSupplyDistance(string payload)
        => Assert.Null(await Client(new Handler(payload)).GetDrivingRouteAsync(37.5m, 127m, 37.51m, 127.01m));

    [Fact]
    public async Task MissingKeysDoNotCallProvider()
    {
        var handler = new Handler("{}");
        var client = new NaverCloudDirectionsService(new HttpClient(handler), Options.Create(new NaverCloudDirectionsOptions()));
        Assert.Null(await client.GetDrivingRouteAsync(37.5m, 127m, 37.51m, 127.01m));
        Assert.Null(handler.Uri);
    }

    [Fact]
    public async Task CancellationReachesHttpRequest()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(new Handler("{}")).GetDrivingRouteAsync(
            37.5m, 127m, 37.51m, 127.01m, cancellationToken: cancellation.Token));
    }

    private static NaverCloudDirectionsService Client(Handler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://maps.apigw.ntruss.com") },
        Options.Create(new NaverCloudDirectionsOptions { ApiKeyId = "test-id", ApiKey = "test-key" }));

    private sealed class Handler(string payload) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
        }
    }
}
