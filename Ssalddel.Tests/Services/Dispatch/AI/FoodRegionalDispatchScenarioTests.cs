using System.Net;
using System.Text;
using System.Text.Json;
using Ssalddel.Tests.Clients;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class FoodRegionalDispatchScenarioTests
{
    [Theory]
    [InlineData(37.583, 127.086)]
    [InlineData(37.591, 127.061)]
    [InlineData(37.550, 127.083)]
    public async Task PublicStoreAnchor_WithSyntheticDestination_CompletesHttpMobileWorkflow(double latitude, double longitude)
    {
        var lat = (decimal)latitude; var lon = (decimal)longitude;
        using var http = new HttpClient(new Model());
        var evidence = await FDriverServerDispatchScenarioTests.RunScenarioAsync("same-region", http,
            regionalSeed: new("synthetic-test-record", "합성 시험 매장", "합성 공개 매장 도로", lat, lon,
                lat + 0.002m, lon + 0.002m, "합성 시험 메뉴", 9000m, true));
        Assert.True(evidence.CustomerReceived && evidence.Applied);
        Assert.False(evidence.AndroidBannerRendered || evidence.RealFcmDelivered);
    }

    [Fact]
    public async Task CustomerDestinationWithoutSyntheticDeclaration_IsRejectedBeforeModel()
    {
        using var http = new HttpClient(new Model());
        await Assert.ThrowsAsync<ArgumentException>(() => FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", http,
            regionalSeed: new("record", "매장", "주소", 37.58m, 127.08m, 37.59m, 127.09m, "메뉴", 9000m, false)));
    }

    private sealed class Model : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { model = "qwen3:1.7b", done = true,
                    message = new { role = "assistant", content = "{\"candidateToken\":\"C1\"}" } }), Encoding.UTF8, "application/json")
            });
    }
}
