using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Extensions;
using 살뜰.Services.External.Naver;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class ReturnAreaRouteLoggingTests
{
    [Fact]
    public async Task PrivateRouteCoordinatesAreSentToProviderWithoutDefaultHttpUriLogs()
    {
        var logs = new Collector();
        var handler = new RouteHandler();
        var services = new ServiceCollection();
        services.AddLogging(x => x.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.Configure<NaverCloudDirectionsOptions>(x =>
        { x.BaseUrl = "https://maps.local.invalid/"; x.ApiKeyId = "test-id"; x.ApiKey = "test-key"; });
        services.AddSsalddelHttpClients();
        services.AddHttpClient<INaverCloudDirectionsService, NaverCloudDirectionsService>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient("ordinary-client", x => x.BaseAddress = new("https://public.local.invalid/"))
            .ConfigurePrimaryHttpMessageHandler(() => new RouteHandler());
        using var provider = services.BuildServiceProvider();
        var route = await provider.GetRequiredService<INaverCloudDirectionsService>()
            .GetDrivingRouteAsync(37.543219m, 127.123456m, 37.765432m, 127.234567m, "traoptimal");
        Assert.Equal(5m, route!.DistanceKm);
        Assert.Contains("127.123456", Uri.UnescapeDataString(handler.Uri!.Query));
        Assert.DoesNotContain(logs.Messages, x => x.Contains("37.543219") || x.Contains("127.123456")
            || x.Contains("37.765432") || x.Contains("127.234567"));
        await provider.GetRequiredService<IHttpClientFactory>().CreateClient("ordinary-client").GetAsync("health");
        Assert.Contains(logs.Messages, x => x.Contains("public.local.invalid"));
    }

    private sealed class RouteHandler : HttpMessageHandler
    {
        public Uri? Uri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("""{"code":0,"route":{"traoptimal":[{"summary":{"distance":5000,"duration":600000,"tollFare":0}}]}}""") });
        }
    }
    private sealed class Collector : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(Messages);
        public void Dispose() { }
        private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
}
