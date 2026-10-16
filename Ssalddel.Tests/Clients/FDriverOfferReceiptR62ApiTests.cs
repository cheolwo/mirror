using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FDriverApp.Services;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverOfferReceiptR62ApiTests
{
    [Fact]
    public async Task 네이티브수신확인은기존인증과정확한추천차수를전송한다()
    {
        var session = new FDriverTestSession(); var calls = 0;
        using var http = new HttpClient(new FDriverTestHttpHandler(async (request, token) =>
        {
            calls++;
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/api/v1/driver/operational-dispatch/offers/offer-r62/receipt", request.RequestUri!.AbsolutePath);
            Assert.Equal("test-access", request.Headers.Authorization!.Parameter);
            var body = await request.Content!.ReadFromJsonAsync<운영배차제안표시확인요청>(token);
            Assert.Equal(7, body!.추천라운드);
            return new(HttpStatusCode.NoContent);
        })) { BaseAddress = new("http://localhost/") };
        await new FoodDeliveryDriverApiService(http, session, new(http, session)).AcknowledgeOfferPresentationAsync("offer-r62", 7);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task 공통수신확인은기존food_driver인증transport를재사용한다()
    {
        var transport = new RoleApi(); IRoleWorkspaceApi api = transport;
        await api.AcknowledgeOfferPresentationAsync("offer-r62", 3, default);
        Assert.Equal("food-driver", transport.Role);
        Assert.Equal("api/v1/driver/operational-dispatch/offers/offer-r62/receipt", transport.Path);
        Assert.Equal(3, Assert.IsType<운영배차제안표시확인요청>(transport.Body).추천라운드);
    }

    [Fact]
    public void 서버추천차수는표시투영과JSON에보존되고구형응답은0이다()
    {
        var offer = new DriverWorkOfferDto("offer", "food-driver", "food", "food", "title", "summary",
            new("pickup", "address", 0, 0), new("dropoff", "area", 0, 0), 4000, 1, "reason")
            { RecommendationRound = 8 };
        var projected = FoodDeliveryCurrentWorkProjector.ToOffer(offer);
        Assert.Equal(8, JsonSerializer.Deserialize<FoodDeliveryDriverOfferDto>(JsonSerializer.Serialize(projected))!.RecommendationRound);
        Assert.Equal(0, JsonSerializer.Deserialize<FoodDeliveryDriverOfferDto>("{}")!.RecommendationRound);
    }

    private sealed class RoleApi : IRoleWorkspaceApi
    {
        public string? Role { get; private set; }
        public string? Path { get; private set; }
        public object? Body { get; private set; }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        { Role = roleKey; Path = path; Body = body; return Task.FromResult<T?>(default); }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
