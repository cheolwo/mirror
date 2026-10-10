using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverVerticalRefactorTests
{
    [Fact]
    public async Task AutomaticCurrentWorkReads_ExcludeProfileSettlement_AndProfileEntryFetchesItOnce()
    {
        var data = FDriverTestWorkspaceApi.Data("current-a");
        data.Settlement.년도 = 2026; data.Settlement.월 = 10; data.Settlement.이용료 = 1200;
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(data) };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        try
        {
            await model.InitializeAsync();
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(2, api.CurrentWorkCalls); Assert.Equal(0, api.WorkspaceCalls);
            model.Navigation.Select(FDriverWorkspaceSection.Profile);
            await model.RefreshProfileSettlementAsync();
            Assert.Equal(1, api.WorkspaceCalls); Assert.Contains("1,200", model.SettlementText);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(3, api.CurrentWorkCalls); Assert.Equal(1, api.WorkspaceCalls);
            Assert.Equal("current-a", Assert.Single(model.RecommendedTicketItems).TicketId);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task MissingProfileSettlement_DoesNotReplaceCurrentWorkOrReportZero()
    {
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        try
        {
            await model.InitializeAsync();
            var current = Assert.Single(model.RecommendedTicketItems).TicketId;
            model.Navigation.Select(FDriverWorkspaceSection.Profile);
            await model.RefreshProfileSettlementAsync(); // Missing month/year is not an actual zero statement.
            Assert.Contains("확인하지 못했습니다", model.SettlementText);
            Assert.DoesNotContain("0원", model.SettlementText);
            Assert.Equal(current, Assert.Single(model.RecommendedTicketItems).TicketId);
            Assert.True(model.IsAuthenticated);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task CurrentApi_UsesDedicatedPath_AndStartTransmitsItsOwnRequestId()
    {
        기사운행시작요청? sent = null;
        var handler = new FDriverTestHttpHandler(async (request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                sent = await request.Content!.ReadFromJsonAsync<기사운행시작요청>();
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 기사운행시작응답 { Status = "운행중" }) };
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new FoodDeliveryDriverCurrentWorkDto { DriverId = "test-driver" }) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        var session = new FDriverTestSession(); var api = new FoodDeliveryDriverApiService(http, session, new(http, session));
        await api.GetCurrentWorkAsync(); var id = Guid.NewGuid(); await api.StartWorkAsync("현재 위치", id);
        Assert.Equal(new[] { "/api/v1/driver/food-deliveries/workspace/current", "/api/v1/driver/food-deliveries/work/start" }, handler.Paths);
        Assert.Equal(id, sent?.ClientRequestId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void MissingDistance_RemainsDistinctFromConfirmedZero_InNativeAndSharedCards(int? distance)
    {
        var dto = new FoodDeliveryDriverOfferDto { OfferId = "offer-a", DistanceKm = distance };
        var native = DeliveryTicketPreview.From(dto);
        var shared = FoodDriverWorkspacePresentation.Offer(dto, DateTime.UtcNow);
        if (distance is null)
        {
            Assert.Null(native.DistanceKm); Assert.Null(native.ToDriverWorkOffer(new()).DistanceKm);
            Assert.Equal("미확인", native.DistanceText);
            Assert.Equal("미확인", Assert.Single(shared.Summary!.Metrics!, item => item.Label == "제안 거리").Value);
        }
        else
        {
            Assert.Equal(0d, native.DistanceKm); Assert.Equal(0d, native.ToDriverWorkOffer(new()).DistanceKm);
            Assert.NotEqual("미확인", native.DistanceText);
            Assert.Equal("0 km", Assert.Single(shared.Summary!.Metrics!, item => item.Label == "제안 거리").Value);
        }
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void OfferExpiry_UsesSameInputClock_ForNativeAndSharedActions(int seconds, bool available)
    {
        var now = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);
        var dto = new FoodDeliveryDriverOfferDto { OfferId = "offer-a", ExpiresAtUtc = now.AddSeconds(seconds),
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사제안수락, ExpiresAtUtc = now.AddSeconds(seconds) }] };
        var native = DeliveryTicketPreview.From(dto, now);
        var shared = FoodDriverWorkspacePresentation.Offer(dto, now);
        Assert.Equal(available, native.CanAccept);
        Assert.Equal(available, Assert.Single(shared.Actions!, item => item.Key == 음식배달가능행동Ids.기사제안수락).Enabled);
    }

    [Fact]
    public void ActionExpiryAlsoBlocksNativeOffer_WhenOverallOfferHasNoExpiry()
    {
        var now = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);
        var dto = new FoodDeliveryDriverOfferDto { OfferId = "offer-a",
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사제안수락, ExpiresAtUtc = now }] };
        Assert.False(DeliveryTicketPreview.From(dto, now).CanAccept);
        Assert.False(Assert.Single(FoodDriverWorkspacePresentation.Offer(dto, now).Actions!).Enabled);
    }
}
