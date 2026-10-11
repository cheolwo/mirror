using System.Net;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Tests.Services.Dispatch.AI;

namespace Ssalddel.Tests.Clients;

/// <summary>
/// Isolated server ledgers and real loopback HTTP feed the production mobile client,
/// notification receiver and PageModel. Device presentation is a recording boundary.
/// </summary>
public sealed class FDriverServerDispatchScenarioTests
{
    [Theory]
    [InlineData("same-region", 1)]
    [InlineData("adjacent-region", 1)]
    [InlineData("demand-capacity", 0)]
    public async Task CanonicalWorkflow_ReachesSeparateCustomerReceipt(string scenarioId, int modelCalls)
    {
        var handler = new FDriverWorkflowModelHandler();
        using var modelHttp = new HttpClient(handler);
        var evidence = await RunScenarioAsync(scenarioId, modelHttp);
        Assert.Equal(modelCalls, evidence.ModelRequests);
        Assert.Equal(modelCalls, handler.Calls);
        Assert.True(evidence.NativePublicationRequest);
        Assert.True(evidence.InappBanner);
        Assert.True(evidence.Accepted && evidence.Arrival && evidence.Picked && evidence.Delivered && evidence.CustomerReceived);
        Assert.False(evidence.RealFcmDelivered || evidence.AndroidBannerRendered || evidence.DeviceUiRendered);
        if (modelCalls == 0)
        {
            Assert.Equal(1, evidence.EligibleCount);
            Assert.False(evidence.Applied);
            Assert.Contains("SingleCandidate", evidence.Reason);
            Assert.Equal(evidence.BaselineDriverId, evidence.SelectedDriverId);
        }
        else
        {
            Assert.True(evidence.EligibleCount >= 2);
            Assert.True(evidence.Applied);
        }
    }

    // The runner passes its budgeted model transport here. Mock and actual runs share
    // every other server/mobile step; this method never starts a model process.
    public static async Task<FoodDispatchWorkflowEvidence> RunScenarioAsync(string scenarioId, HttpClient modelHttp,
        string? databasePath = null, FoodDispatchRegionalSeed? regionalSeed = null)
    {
        await using var mobile = await FDriverWorkflowMobileSession.CreateAsync(scenarioId, modelHttp, databasePath, regionalSeed);
        var fixture = mobile.Fixture;
        var model = mobile.Model;
        var steps = new List<FoodDispatchWorkflowStep>();
        var initial = await fixture.ReadSnapshotAsync();
        Assert.Equal(1, initial.OrderCount);
        Assert.Equal(1, initial.QueueCount);
        Assert.Equal(0, initial.AttemptCount);
        steps.Add(Step("order-confirmed-and-recommendation-saved", initial, model));

        var push = Assert.Single(fixture.PushMessages);
        Assert.True(push.DataOnly && push.HighPriority);
        Assert.Equal(5, push.Data.Count);
        Assert.True(await mobile.Receiver.ReceiveAsync(push.Data));
        Assert.True(mobile.Notifications.PublishRequests > 0);
        Assert.Empty(mobile.Transport.Requests); // A push hint cannot accept or fetch work itself.
        var nativePublicationRequest = mobile.Notifications.PublishRequests > 0;

        await model.InitializeAsync();
        Assert.True(model.IsAuthenticated);
        Assert.False(model.HasWorkspaceWarning);
        Assert.True(model.HasNewRecommendations);
        var inappBanner = model.HasNewRecommendations;
        Assert.Equal(fixture.OfferId, Assert.Single(model.RecommendedTicketItems).TicketId);
        Assert.Empty(model.ActiveDeliveryItems);
        steps.Add(Step("push-hint-and-canonical-inapp-banner", await fixture.ReadSnapshotAsync(), model));

        var readsBeforeOpening = CurrentReads(mobile);
        await model.OpenNewRecommendationsCommand.ExecuteAsync(null);
        Assert.True(CurrentReads(mobile) > readsBeforeOpening);
        Assert.False(model.HasNewRecommendations);
        Assert.Equal(fixture.OfferId, model.SelectedTicket?.TicketId);
        Assert.Equal(0, (await fixture.ReadSnapshotAsync()).AttemptCount);
        Assert.True(model.CanAcceptSelectedTicket);
        await model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        var accepted = await fixture.ReadSnapshotAsync();
        Assert.Equal(1, accepted.AttemptCount);
        Assert.Equal(fixture.DriverId, accepted.ConfirmedDriverId);
        Assert.Equal(fixture.OfferId, Assert.Single(model.ActiveDeliveryItems).OfferId);
        Assert.Equal(FDriverFoodStage.PickupTravel, model.CurrentDeliveryStage);
        Assert.True(model.HasPickupStage);
        Assert.False(model.HasDeliveryStage || model.HasNewRecommendations);
        Assert.True(model.CanRecordArrival);
        Assert.False(model.CanConfirmPickup || model.CanCompleteDelivery);
        steps.Add(Step("mobile-accepted-pickup-card", accepted, model));

        await fixture.SetDriverLocationAsync(atPickup: true);
        await model.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        var arrived = await fixture.ReadSnapshotAsync();
        Assert.True(arrived.Arrived);
        Assert.Equal(FDriverFoodStage.PickupWaiting, model.CurrentDeliveryStage);
        Assert.False(model.CanRecordArrival || model.CanConfirmPickup);
        steps.Add(Step("mobile-arrival-awaiting-preparation", arrived, model));

        await fixture.MarkPreparationReadyAsync();
        await FDriverLifecycleTestSupport.RefreshBackground(model);
        Assert.True(model.CanConfirmPickup);
        await model.ConfirmPickupCommand.ExecuteAsync(null);
        var picked = await fixture.ReadSnapshotAsync();
        Assert.True(picked.PickedUp);
        Assert.Equal(FDriverFoodStage.Delivery, model.CurrentDeliveryStage);
        Assert.True(model.HasDeliveryStage && model.CanCompleteDelivery);
        Assert.False(model.HasPickupStage || model.CanConfirmPickup);
        steps.Add(Step("mobile-picked-delivery-card", picked, model));

        await fixture.SetDriverLocationAsync(atPickup: false);
        await model.CompleteDeliveryCommand.ExecuteAsync(null);
        var delivered = await fixture.ReadSnapshotAsync();
        Assert.True(delivered.Delivered);
        Assert.False(delivered.CustomerReceived); // Driver delivery does not certify receipt.
        Assert.Empty(model.ActiveDeliveryItems);
        Assert.False(model.CanCompleteDelivery);
        Assert.Equal(FDriverFoodStage.Waiting, model.CurrentDeliveryStage);
        steps.Add(Step("mobile-delivered-customer-receipt-pending", delivered, model));

        await fixture.ConfirmReceiptAsync();
        var received = await fixture.ReadSnapshotAsync();
        Assert.True(received.Delivered && received.CustomerReceived);
        steps.Add(Step("customer-receipt-confirmed-separately", received, model));
        var recommendation = fixture.RecommendationEvidence
            ?? throw new InvalidOperationException("합성 배차 추천 근거가 생성되지 않았습니다.");
        Assert.True(recommendation.ModelRequests <= 1);
        Assert.All(mobile.Transport.Requests, request => Assert.Equal("server", request.Boundary));
        Assert.All(mobile.Transport.Requests, request => Assert.InRange(request.StatusCode, 200, 299));
        Assert.DoesNotContain(mobile.Transport.Requests, request => request.Path.Contains("/route", StringComparison.Ordinal)
            || request.Path.Contains("/auth/", StringComparison.Ordinal) || request.Path.Contains("/location", StringComparison.Ordinal));

        return new()
        {
            ScenarioId = scenarioId, OrderNo = fixture.OrderNo, OfferId = fixture.OfferId,
            FrozenFee = received.FrozenFee, ServerSteps = fixture.Steps.ToArray(),
            BaselineDriverId = recommendation.BaselineDriverId,
            SelectedDriverId = recommendation.SelectedDriverId, ModelRequests = recommendation.ModelRequests,
            Applied = recommendation.Applied, Reason = recommendation.Reason, EligibleCount = recommendation.EligibleCount,
            Fallback = !recommendation.Applied, InferenceSkipped = recommendation.ModelRequests == 0,
            InferenceSkipReason = recommendation.ModelRequests == 0 ? recommendation.Reason : null,
            DemandSnapshotCurrent = recommendation.Trace.수요자료최신,
            DemandDecision = fixture.DemandEvidence,
            Steps = steps.ToArray(), DispatchRequestOutboxCount = received.DispatchRequestOutboxCount,
            DispatchRequestOutboxStatuses = received.DispatchRequestOutboxStatuses,
            NotificationOutboxCount = received.NotificationOutboxCount,
            NotificationOutboxStatuses = received.NotificationOutboxStatuses,
            NativePublicationRequest = nativePublicationRequest, NativePublicationRequests = mobile.Notifications.PublishRequests,
            InappBanner = inappBanner, Accepted = accepted.AttemptCount == 1, Arrival = arrived.Arrived,
            Picked = picked.PickedUp, Delivered = delivered.Delivered, CustomerReceived = received.CustomerReceived,
            HttpRequests = mobile.Transport.Requests.Count, HttpTrace = mobile.Transport.Requests.ToArray(),
            SubstitutedPorts = ["Menu:pass-through-no-price-or-stock-revalidation", "Route:synthetic-fixed",
                "Pricing:production-policy-with-synthetic-route-weather-in-Simulation", "Settlement:no-op",
                "Mongo-and-food-ledger-synchronization:no-op", "Restaurant-notification:no-op",
                "Domain-events:recording-publisher-no-automatic-handlers-explicit-Outbox-drain",
                "Area-projection:synthetic-not-common-SQL-projection", "Database-encryption:pass-through",
                "FCM:recording-only", "Native-notification:recording-only", "Push-callback:actual-receiver-payload-injection",
                "GPS-and-map-lifetime:disabled-synthetic-server-location", "SecureStorage:in-memory-fixture",
                "Authentication:fixture-Bearer-not-production-JWT"]
        };
    }

    [Theory]
    [InlineData("unknown", HttpStatusCode.OK)]
    [InlineData("C2", HttpStatusCode.ServiceUnavailable)]
    public async Task InvalidOrFailedModel_FallsBackAndStillCompletesCanonicalWorkflow(string token, HttpStatusCode status)
    {
        var handler = new FDriverWorkflowModelHandler(token, status);
        using var modelHttp = new HttpClient(handler);
        var result = await RunScenarioAsync("same-region", modelHttp);
        Assert.Equal(1, handler.Calls);
        Assert.False(result.Applied);
        Assert.Equal(result.BaselineDriverId, result.SelectedDriverId);
        Assert.True(result.CustomerReceived);
    }

    [Theory]
    [InlineData("appKey", "CargoYongdalDriverApp")]
    [InlineData("userId", "other-fixture-driver")]
    [InlineData("type", "DriverDispatchRecommendation")]
    [InlineData("offerId", "")]
    [InlineData("expiresAtUtc", "2000-01-01T00:00:00Z")]
    public async Task AlteredActualServerHint_IsRejectedWithoutApiOrStateMutation(string key, string value)
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler());
        await using var mobile = await FDriverWorkflowMobileSession.CreateAsync("same-region", modelHttp);
        var data = new Dictionary<string, string>(Assert.Single(mobile.Fixture.PushMessages).Data) { [key] = value };
        Assert.False(await mobile.Receiver.ReceiveAsync(data));
        Assert.Equal(0, mobile.Notifications.PublishRequests);
        Assert.Empty(mobile.Transport.Requests);
        Assert.Equal(0, (await mobile.Fixture.ReadSnapshotAsync()).AttemptCount);
    }

    [Fact]
    public async Task DuplicateServerHintAndOutboxPoll_DoNotAcceptOrDuplicateCanonicalOffer()
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler());
        await using var mobile = await FDriverWorkflowMobileSession.CreateAsync("same-region", modelHttp);
        await mobile.Fixture.DispatchPendingNotificationsAsync();
        var data = Assert.Single(mobile.Fixture.PushMessages).Data;
        Assert.True(await mobile.Receiver.ReceiveAsync(data));
        Assert.True(await mobile.Receiver.ReceiveAsync(data));
        await mobile.Model.InitializeAsync();
        await FDriverLifecycleTestSupport.RefreshBackground(mobile.Model);
        Assert.Single(mobile.Model.RecommendedTicketItems);
        Assert.True(mobile.Model.HasNewRecommendations);
        Assert.Equal(0, (await mobile.Fixture.ReadSnapshotAsync()).AttemptCount);
        Assert.DoesNotContain(mobile.Transport.Requests, request => request.Method == "POST");
    }

    [Fact]
    public async Task NativePublicationDeclined_CanonicalInappBannerStillAllowsExplicitAcceptance()
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler());
        await using var mobile = await FDriverWorkflowMobileSession.CreateAsync("same-region", modelHttp);
        mobile.Notifications.PublicationAllowed = false; // Device port denial, not an Android permission operation.
        Assert.False(await mobile.Receiver.ReceiveAsync(Assert.Single(mobile.Fixture.PushMessages).Data));
        Assert.True(mobile.Notifications.PublishRequests > 0);
        Assert.Empty(mobile.Transport.Requests);
        Assert.Equal(0, (await mobile.Fixture.ReadSnapshotAsync()).AttemptCount);
        await mobile.Model.InitializeAsync();
        Assert.True(mobile.Model.HasNewRecommendations);
        Assert.Equal(mobile.Fixture.OfferId, mobile.Model.SelectedTicket?.TicketId);
        Assert.True(mobile.Model.CanAcceptSelectedTicket);
        await mobile.Model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        Assert.Equal(1, (await mobile.Fixture.ReadSnapshotAsync()).AttemptCount);
        Assert.Equal(mobile.Fixture.OfferId, mobile.Model.ActiveDelivery?.OfferId);
        Assert.False(mobile.Model.HasNewRecommendations);
    }

    [Fact]
    public async Task FailedCanonicalRead_PreservesRecommendationButBlocksStaleAcceptanceUntilRecovery()
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler());
        await using var mobile = await FDriverWorkflowMobileSession.CreateAsync("same-region", modelHttp);
        Assert.Equal(0, mobile.Notifications.PublishRequests); // No receiver/push injection preceded this canonical query.
        await mobile.Model.InitializeAsync();
        Assert.True(mobile.Model.HasNewRecommendations);
        Assert.True(mobile.Model.CanAcceptSelectedTicket);
        mobile.Transport.FailNextCurrentRead = true;
        await FDriverLifecycleTestSupport.RefreshBackground(mobile.Model);
        Assert.True(mobile.Model.HasWorkspaceWarning);
        Assert.Equal(mobile.Fixture.OfferId, mobile.Model.SelectedTicket?.TicketId);
        Assert.False(mobile.Model.CanAcceptSelectedTicket);
        var count = mobile.Transport.Requests.Count;
        await mobile.Model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        Assert.Equal(count, mobile.Transport.Requests.Count);
        Assert.Equal(0, (await mobile.Fixture.ReadSnapshotAsync()).AttemptCount);
        await FDriverLifecycleTestSupport.RefreshBackground(mobile.Model);
        Assert.False(mobile.Model.HasWorkspaceWarning);
        Assert.True(mobile.Model.CanAcceptSelectedTicket);
    }

    [Fact]
    public async Task LostAcceptResponse_RequeriesPersistedAttemptWithoutSendingSecondAccept()
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler());
        await using var mobile = await FDriverWorkflowMobileSession.CreateAsync("same-region", modelHttp);
        await mobile.Model.InitializeAsync();
        mobile.Transport.LoseNextAcceptResponse = true;
        await mobile.Model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        Assert.Equal(1, (await mobile.Fixture.ReadSnapshotAsync()).AttemptCount);
        Assert.Equal(mobile.Fixture.OfferId, mobile.Model.ActiveDelivery?.OfferId);
        Assert.False(mobile.Model.HasWorkspaceWarning);
        await mobile.Model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        Assert.Single(mobile.Transport.Requests, request => request.Method == "POST" && request.Path.EndsWith("/accept"));
    }

    [Fact]
    public async Task BeforeArrivalOrPreparation_MobileAndServerBothRejectPrematurePickupAndDelivery()
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler());
        await using var mobile = await FDriverWorkflowMobileSession.CreateAsync("same-region", modelHttp);
        await mobile.Model.InitializeAsync();
        await mobile.Model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        var count = mobile.Transport.Requests.Count;
        await mobile.Model.ConfirmPickupCommand.ExecuteAsync(null);
        await mobile.Model.CompleteDeliveryCommand.ExecuteAsync(null);
        Assert.Equal(count, mobile.Transport.Requests.Count);
        await Assert.ThrowsAsync<FDriverApiException>(() => mobile.Api.ConfirmPickupAsync(mobile.Fixture.OfferId));
        await Assert.ThrowsAsync<FDriverApiException>(() => mobile.Api.CompleteAsync(mobile.Fixture.OfferId));
        var snapshot = await mobile.Fixture.ReadSnapshotAsync();
        Assert.False(snapshot.Arrived || snapshot.PickedUp || snapshot.Delivered || snapshot.CustomerReceived);
        Assert.Equal(1, snapshot.AttemptCount);
    }

    [Fact]
    public async Task DifferentAuthenticatedDriver_CannotAcceptSelectedDriversOffer()
    {
        using var modelHttp = new HttpClient(new FDriverWorkflowModelHandler());
        await using var mobile = await FDriverWorkflowMobileSession.CreateAsync("same-region", modelHttp);
        await mobile.Session.ApplyAsync(FDriverWorkflowMobileSession.Token("other-fixture-driver", "fixture-driver:other-fixture-driver"));
        await Assert.ThrowsAsync<FDriverApiException>(() => mobile.Api.AcceptAsync(mobile.Fixture.OfferId));
        Assert.Equal(0, (await mobile.Fixture.ReadSnapshotAsync()).AttemptCount);
    }

    private static int CurrentReads(FDriverWorkflowMobileSession mobile)
        => mobile.Transport.Requests.Count(request => request.Method == "GET" && request.Path.EndsWith("/workspace/current"));

    private static FoodDispatchWorkflowStep Step(string name, FoodDispatchWorkflowSnapshot snapshot, MainPageModel model)
        => new(name, snapshot.OrderStatus, snapshot.QueueStatus, snapshot.QueueStage.ToString(), snapshot.AttemptCount,
            snapshot.Arrived, snapshot.PickedUp, snapshot.Delivered, snapshot.CustomerReceived,
            model.CurrentDeliveryStage.ToString(), model.HasPickupStage, model.HasDeliveryStage,
            model.CanAcceptSelectedTicket, model.CanRecordArrival, model.CanConfirmPickup, model.CanCompleteDelivery);
}

public sealed record FoodDispatchWorkflowHttpRequest(string Method, string Path, int StatusCode, string Boundary);
public sealed record FoodDispatchWorkflowStep(string Name, string OrderStatus, string QueueStatus, string QueueStage,
    int AttemptCount, bool Arrived, bool Picked, bool Delivered, bool CustomerReceived,
    string MobileStage, bool PickupCard, bool DeliveryCard, bool CanAccept, bool CanArrive, bool CanPick, bool CanDeliver);

public sealed record FoodDispatchWorkflowEvidence
{
    public string ScenarioId { get; init; } = string.Empty;
    public string OrderNo { get; init; } = string.Empty;
    public string OfferId { get; init; } = string.Empty;
    public decimal? FrozenFee { get; init; }
    public IReadOnlyList<string> ServerSteps { get; init; } = [];
    public string BaselineDriverId { get; init; } = string.Empty;
    public string SelectedDriverId { get; init; } = string.Empty;
    public int ModelRequests { get; init; }
    public bool Applied { get; init; }
    public bool Fallback { get; init; }
    public bool InferenceSkipped { get; init; }
    public string? InferenceSkipReason { get; init; }
    public bool DemandSnapshotCurrent { get; init; }
    public 음식배달수요참여결과? DemandDecision { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int EligibleCount { get; init; }
    public IReadOnlyList<FoodDispatchWorkflowStep> Steps { get; init; } = [];
    public int DispatchRequestOutboxCount { get; init; }
    public IReadOnlyList<string> DispatchRequestOutboxStatuses { get; init; } = [];
    public int NotificationOutboxCount { get; init; }
    public IReadOnlyList<string> NotificationOutboxStatuses { get; init; } = [];
    public bool NativePublicationRequest { get; init; }
    public int NativePublicationRequests { get; init; }
    public bool InappBanner { get; init; }
    public bool Accepted { get; init; }
    public bool Arrival { get; init; }
    public bool Picked { get; init; }
    public bool Delivered { get; init; }
    public bool CustomerReceived { get; init; }
    public int HttpRequests { get; init; }
    public IReadOnlyList<FoodDispatchWorkflowHttpRequest> HttpTrace { get; init; } = [];
    public IReadOnlyList<string> SubstitutedPorts { get; init; } = [];
    public bool RealFcmDelivered { get; init; }
    public bool AndroidBannerRendered { get; init; }
    public bool DeviceUiRendered { get; init; }
}
