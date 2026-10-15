using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Tests.Services.Dispatch.AI;

namespace Ssalddel.Tests.Clients;

/// <summary>
/// 운영자 화면 없이 일반 기사 권한으로 수행한다. 실제 HTTP/SQL/제품 PageModel을
/// 연결하되 GPS, native 알림, FCM, 추론, Redis와 전체 프로세스 재시작은 대체 경계다.
/// </summary>
public sealed class FDriverSelfOperationPilotTests
{
    [Fact]
    public async Task 일반기사_운행시작후_알림복구와Host재생성을거쳐_두건을순서대로수행한다()
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler("C1"));
        await using var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", modelHttp);
        await fixture.PreparePilotDriverAsync("scenario-older");
        var firstId = fixture.OfferId;
        var notifications = new FDriverWorkflowNotifications();
        var traces = new List<FDriverWorkflowHttpHandler>();
        await using (var first = await PilotMobile.CreateAsync(fixture, notifications))
        {
            traces.Add(first.Transport);
            await first.Model.InitializeAsync();
            Assert.False(first.Model.IsOnDuty || first.Model.ReceivesNewDispatches);
            Assert.True(first.Model.CanToggleWork);
            await first.Model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.True(first.Model.IsOnDuty && first.Model.ReceivesNewDispatches);
            Assert.Single(first.Transport.Requests, x => x.Method == "POST" && x.Path.EndsWith("/work/start"));
            Assert.Equal(["기사"], first.Session.Roles);
            await fixture.PublishPilotPickupLocationAsync();
            await fixture.RecommendPilotOrderAsync();
            var recommendation = await fixture.ReadPilotSnapshotAsync();
            Assert.Equal(fixture.DriverId, recommendation.RecommendedDriverId);
            Assert.Equal(0, fixture.ModelRequests); // One eligible ordinary driver needs no inference.

            fixture.PushSucceeded = false;
            await fixture.DispatchPendingNotificationsAsync();
            Assert.Equal(0, notifications.PublishRequests);
            Assert.Single(fixture.PushMessages);
            fixture.PushSucceeded = true;
            await fixture.MakePilotNotificationRetryDueAsync();
            await fixture.DispatchPendingNotificationsAsync();
            Assert.Equal(2, fixture.PushMessages.Count);
            Assert.True(await first.Receiver.ReceiveAsync(fixture.PushMessages.Last().Data));
            Assert.True(notifications.PublishRequests > 0);
            await FDriverLifecycleTestSupport.RefreshBackground(first.Model);
            Assert.True(first.Model.HasNewRecommendations);
            Assert.Equal(firstId, first.Model.SelectedTicket?.TicketId);
            var repaired = await fixture.ReadPilotSnapshotAsync();
            Assert.Equal(recommendation.RecommendationRound, repaired.RecommendationRound);
            Assert.Equal(recommendation.FrozenFee, repaired.FrozenFee);
            Assert.Equal(1, repaired.NotificationOutboxCount);
            Assert.Equal(["Succeeded"], repaired.NotificationOutboxStatuses);
            await PickUpAsync(fixture, first.Model);
            Assert.Equal(FDriverFoodStage.Delivery, first.Model.CurrentDeliveryStage);
        }

        var beforeRestart = await fixture.ReadPilotSnapshotAsync();
        await fixture.RestartPilotHttpHostAsync();
        await using var resumed = await PilotMobile.CreateAsync(fixture, notifications);
        traces.Add(resumed.Transport);
        await resumed.Model.InitializeAsync();
        Assert.True(resumed.Model.IsOnDuty && resumed.Model.ReceivesNewDispatches);
        Assert.Equal(firstId, resumed.Model.ActiveDelivery?.OfferId);
        Assert.Equal(FDriverFoodStage.Delivery, resumed.Model.CurrentDeliveryStage);
        Assert.False(resumed.Model.CanAcceptSelectedTicket || resumed.Model.CanConfirmPickup);
        Assert.DoesNotContain(resumed.Transport.Requests, x => x.Method == "POST" && x.Path.EndsWith("/work/start"));
        var afterRestart = await fixture.ReadPilotSnapshotAsync();
        Assert.Equal(beforeRestart.AttemptRevision, afterRestart.AttemptRevision);
        Assert.Equal(beforeRestart.FrozenFee, afterRestart.FrozenFee);
        await DeliverAndReceiveAsync(fixture, resumed.Model);

        await fixture.CreateNextPilotOrderAsync();
        var secondId = fixture.OfferId;
        await fixture.PublishPilotPickupLocationAsync();
        await fixture.RecommendPilotOrderAsync();
        await fixture.DispatchPendingNotificationsAsync();
        var nextHint = fixture.PushMessages.Last().Data;
        Assert.Equal(secondId, nextHint["offerId"]);
        Assert.True(await resumed.Receiver.ReceiveAsync(nextHint));
        Assert.True(await resumed.Receiver.ReceiveAsync(nextHint));
        // The old message may still have a valid TTL, but it cannot authorize or
        // replace current work. Canonical query must select the new offer only.
        Assert.True(await resumed.Receiver.ReceiveAsync(fixture.PushMessages.First().Data));
        notifications.SetOpenTarget(new(fixture.DriverId, firstId));
        await FDriverLifecycleTestSupport.RefreshBackground(resumed.Model);
        await resumed.Model.OpenNewRecommendationsCommand.ExecuteAsync(null);
        Assert.Single(resumed.Model.RecommendedTicketItems);
        Assert.Equal(secondId, resumed.Model.SelectedTicket?.TicketId);
        Assert.Equal(0, (await fixture.ReadPilotSnapshotAsync()).AttemptCount);
        var expired = new Dictionary<string, string>(nextHint) { ["expiresAtUtc"] = "2000-01-01T00:00:00Z" };
        var publications = notifications.PublishRequests;
        Assert.False(await resumed.Receiver.ReceiveAsync(expired));
        Assert.Equal(publications, notifications.PublishRequests);
        await PickUpAsync(fixture, resumed.Model);
        await resumed.Model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        await DeliverAndReceiveAsync(fixture, resumed.Model);

        var firstFinal = await fixture.ReadPilotSnapshotAsync(firstId);
        var secondFinal = await fixture.ReadPilotSnapshotAsync();
        Assert.True(firstFinal.CustomerReceived && secondFinal.CustomerReceived);
        Assert.Equal(1, firstFinal.AttemptCount);
        Assert.Equal(1, secondFinal.AttemptCount);
        Assert.Equal(2, secondFinal.OrderCount);
        Assert.Equal(2, secondFinal.QueueCount);
        Assert.Equal(2, secondFinal.NotificationOutboxCount);
        Assert.All(secondFinal.NotificationOutboxStatuses, x => Assert.Equal("Succeeded", x));
        Assert.Equal(FDriverFoodStage.Waiting, resumed.Model.CurrentDeliveryStage);
        Assert.Empty(resumed.Model.ActiveDeliveryItems);
        var requests = traces.SelectMany(x => x.Requests).ToArray();
        Assert.Equal(2, requests.Count(x => x.Method == "POST" && x.Path.EndsWith("/accept")));
        Assert.DoesNotContain(requests, x => x.Path.Contains("/admin", StringComparison.OrdinalIgnoreCase));
        Assert.All(requests, x => Assert.Equal("server", x.Boundary));
        Assert.All(requests, x => Assert.InRange(x.StatusCode, 200, 299));
    }

    [Fact]
    public async Task 본인이운행을시작해도_기존선정기준의다른기사를제치거나그추천을수락하지못한다()
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler("C1"));
        await using var fixture = await FoodDispatchWorkflowScenarioFixture.CreateAsync("same-region", modelHttp);
        await fixture.PreparePilotDriverAsync("scenario-near", otherDriversReceive: true);
        await using var mobile = await PilotMobile.CreateAsync(fixture, new());
        await mobile.Model.InitializeAsync();
        await mobile.Model.ToggleWorkCommand.ExecuteAsync(null);
        await fixture.PublishPilotPickupLocationAsync();
        await fixture.RecommendPilotOrderAsync();
        var snapshot = await fixture.ReadPilotSnapshotAsync();
        Assert.Equal("scenario-older", snapshot.RecommendedDriverId);
        Assert.NotEqual(fixture.DriverId, snapshot.RecommendedDriverId);
        await FDriverLifecycleTestSupport.RefreshBackground(mobile.Model);
        Assert.Empty(mobile.Model.RecommendedTicketItems);
        Assert.False(mobile.Model.CanAcceptSelectedTicket);
        await Assert.ThrowsAsync<FDriverApiException>(() => mobile.Api.AcceptAsync(fixture.OfferId));
        Assert.Equal(0, (await fixture.ReadPilotSnapshotAsync()).AttemptCount);
        Assert.Equal(["기사"], mobile.Session.Roles);
        Assert.DoesNotContain(mobile.Transport.Requests, x => x.Path.Contains("/admin", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task PickUpAsync(FoodDispatchWorkflowScenarioFixture fixture, MainPageModel model)
    {
        Assert.True(model.CanAcceptSelectedTicket);
        await model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        Assert.Equal(FDriverFoodStage.PickupTravel, model.CurrentDeliveryStage);
        await fixture.SetDriverLocationAsync(atPickup: true);
        await model.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        Assert.Equal(FDriverFoodStage.PickupWaiting, model.CurrentDeliveryStage);
        Assert.False(model.CanConfirmPickup);
        await fixture.MarkPreparationReadyAsync();
        await FDriverLifecycleTestSupport.RefreshBackground(model);
        Assert.True(model.CanConfirmPickup);
        await model.ConfirmPickupCommand.ExecuteAsync(null);
        Assert.Equal(FDriverFoodStage.Delivery, model.CurrentDeliveryStage);
    }

    private static async Task DeliverAndReceiveAsync(FoodDispatchWorkflowScenarioFixture fixture, MainPageModel model)
    {
        await fixture.SetDriverLocationAsync(atPickup: false);
        await model.CompleteDeliveryCommand.ExecuteAsync(null);
        var delivered = await fixture.ReadPilotSnapshotAsync();
        Assert.True(delivered.Delivered);
        Assert.False(delivered.CustomerReceived);
        await fixture.ConfirmReceiptAsync();
        Assert.True((await fixture.ReadPilotSnapshotAsync()).CustomerReceived);
        Assert.Empty(model.ActiveDeliveryItems);
    }

    private sealed class PilotMobile : IAsyncDisposable
    {
        public FDriverTestSession Session { get; } = new();
        public FDriverWorkflowHttpHandler Transport { get; } = new();
        public HttpClient Http { get; }
        public FoodDeliveryDriverApiService Api { get; }
        public FDriverFoodPushReceiver Receiver { get; }
        public MainPageModel Model { get; }

        private PilotMobile(FoodDispatchWorkflowScenarioFixture fixture, FDriverWorkflowNotifications notifications)
        {
            Http = new(Transport) { BaseAddress = fixture.BaseAddress, Timeout = TimeSpan.FromSeconds(20) };
            var auth = new FDriverAuthApiService(Http, Session);
            Api = new(Http, Session, auth);
            Receiver = new(Session, new FDriverWorkflowPushStore(fixture.DriverId), notifications);
            Model = new(new(), Session, auth, Api, new FDriverTestLocationService(), new(), notifications);
            Model.SetWorkspacePageVisible(false);
            Model.PauseFoodMap();
        }

        public static async Task<PilotMobile> CreateAsync(FoodDispatchWorkflowScenarioFixture fixture,
            FDriverWorkflowNotifications notifications)
        {
            var mobile = new PilotMobile(fixture, notifications);
            await mobile.Session.ApplyAsync(FDriverWorkflowMobileSession.Token(fixture.DriverId, fixture.AccessToken));
            return mobile;
        }

        public async ValueTask DisposeAsync()
        {
            await Model.StopMonitoringAsync();
            Http.Dispose();
        }
    }
}
