using System.Net;
using System.Text;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation 음식주문 Local·Remote 포트의 계약 호환을 검증한다.",
    Boundary = "자동 시험이며 실제 Hosted 연결이나 Unity 화면 증거가 아니다.")]
public sealed class SimulationFoodOrderRuntimeAdapterTests
{
    [Fact]
    public async Task RemoteAdapter는_동일한_음식주문포트를_Hosted경로로_전송한다()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://simulation.invalid/"),
        };
        var runtime = new RemoteSimulationFoodOrderRuntime(client);
        const string sessionId = "simulation-session:food-runtime";

        await runtime.ConfirmRestaurantResponseAsync(sessionId, new Simulation음식점응답Request());
        await runtime.PreviewFoodDeliveryAsync(sessionId, new Simulation음식배달PreviewRequest());
        await runtime.ConfirmFoodDeliveryAsync(sessionId, new Simulation음식배달ConfirmRequest());
        await runtime.PreviewFoodDeliveryReceiptAsync(sessionId, new Simulation음식배달수령PreviewRequest());
        await runtime.ConfirmFoodDeliveryReceiptAsync(sessionId, new Simulation음식배달수령ConfirmRequest());

        Assert.Equal(
            [
                "POST api/simulation/v1/sessions/simulation-session%3Afood-runtime/restaurant-responses/confirm",
                "POST api/simulation/v1/sessions/simulation-session%3Afood-runtime/food-delivery-previews",
                "POST api/simulation/v1/sessions/simulation-session%3Afood-runtime/food-deliveries/confirm",
                "POST api/simulation/v1/sessions/simulation-session%3Afood-runtime/food-delivery-receipt-previews",
                "POST api/simulation/v1/sessions/simulation-session%3Afood-runtime/food-delivery-receipts/confirm",
            ],
            handler.Requests);
        Assert.All(handler.ContentTypes, contentType => Assert.Equal("application/json", contentType));
    }

    [Fact]
    public void LocalRuntime과_RemoteAdapter는_하나의_음식주문Port를_구현한다()
    {
        Assert.True(typeof(ISimulationFoodOrderRuntime).IsAssignableFrom(typeof(LocalSimulationRuntime)));
        Assert.True(typeof(ISimulationFoodOrderRuntime).IsAssignableFrom(typeof(RemoteSimulationFoodOrderRuntime)));

        var methods = typeof(ISimulationFoodOrderRuntime).GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                "ConfirmFoodDeliveryAsync",
                "ConfirmFoodDeliveryReceiptAsync",
                "ConfirmRestaurantResponseAsync",
                "PreviewFoodDeliveryAsync",
                "PreviewFoodDeliveryReceiptAsync",
            ],
            methods);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public List<string?> ContentTypes { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.Method.Method + " " + request.RequestUri!.PathAndQuery.TrimStart('/'));
            ContentTypes.Add(request.Content?.Headers.ContentType?.MediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
