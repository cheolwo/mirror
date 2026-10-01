using System.Net;
using System.Text;
using System.Text.Json;
using Hongdal.Extensions;
using Hongdal.Services.External.Apify;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class ApifyActorGatewayTests
{
    [Fact]
    public void AddApifyAmazonProductResearch_기존Amazon설정을공통모듈설정으로승계한다()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApifyAmazon:Enabled"] = "true",
                ["ApifyAmazon:ApiToken"] = "legacy-token",
                ["ApifyAmazon:BaseUrl"] = "https://api.example.test/v2/",
                ["ApifyAmazon:ActorId"] = "custom~amazon-actor",
                ["ApifyAmazon:TimeoutSeconds"] = "90"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddApifyAmazonProductResearch(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApifyOptions>>().Value;
        Assert.True(options.Enabled);
        Assert.Equal("legacy-token", options.ApiToken);
        Assert.Equal("https://api.example.test/v2/", options.BaseUrl);
        Assert.Equal(90, options.TimeoutSeconds);
        Assert.Contains("custom~amazon-actor", options.AllowedActorIds);
        Assert.NotNull(provider.GetRequiredService<IApifyActorGateway>());
        Assert.NotNull(provider.GetRequiredService<IApifyAmazonProductClient>());
    }

    [Fact]
    public async Task RunSyncGetDatasetItemsAsync_허용되지않은Actor를실행하지않는다()
    {
        var handler = new CountingHandler();
        var sut = CreateGateway(handler, ["junglee~amazon-crawler"]);

        var action = () => sut.RunSyncGetDatasetItemsAsync(
            CreateRequest("another~actor"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Contains("허용되지 않은", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RunSyncGetDatasetItemsAsync_전역비용상한을넘는요청을실행하지않는다()
    {
        var handler = new CountingHandler();
        var sut = CreateGateway(handler, ["junglee~amazon-crawler"], maxTotalChargeUsd: 2m);
        var request = CreateRequest("junglee~amazon-crawler") with
        {
            MaxTotalChargeUsd = 2.01m
        };

        var action = () => sut.RunSyncGetDatasetItemsAsync(request, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Contains("전역 상한", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RunSyncGetDatasetItemsAsync_dataset배열을독립된결과로반환한다()
    {
        var handler = new CountingHandler(
            """
            [
              { "id": "first" },
              { "id": "second" }
            ]
            """);
        var sut = CreateGateway(handler, ["junglee~amazon-crawler"]);

        var result = await sut.RunSyncGetDatasetItemsAsync(
            CreateRequest("junglee~amazon-crawler") with { MaxItems = 2 },
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("first", result.Items[0].GetProperty("id").GetString());
        Assert.Equal(1, handler.RequestCount);
    }

    private static ApifyActorSyncRequest CreateRequest(string actorId)
        => new(
            actorId,
            JsonSerializer.SerializeToElement(new { startUrls = Array.Empty<object>() }),
            TimeoutSeconds: 120,
            MemoryMegabytes: 1024,
            MaxItems: 1,
            MaxTotalChargeUsd: 1m);

    private static ApifyActorGateway CreateGateway(
        HttpMessageHandler handler,
        string[] allowedActorIds,
        decimal maxTotalChargeUsd = 2m)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.apify.com/v2/")
        };
        return new ApifyActorGateway(httpClient, Options.Create(new ApifyOptions
        {
            Enabled = true,
            ApiToken = "test-token",
            AllowedActorIds = allowedActorIds,
            MaxTotalChargeUsd = maxTotalChargeUsd
        }));
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly string _json;

        public CountingHandler(string json = "[]")
        {
            _json = json;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }
}
