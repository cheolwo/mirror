using System.Net;
using System.Reflection;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverFoodNotificationTests
{
    [Fact]
    public void RepeatedCanonicalOffer_HasOneAlertAndOneUnread()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a");
        var offer = Preview("one");
        state.Reconcile([offer], DateTime.UtcNow); state.Reconcile([offer], DateTime.UtcNow);
        Assert.Single(service.Published); Assert.Equal(1, state.UnreadCount);
        Assert.Equal("one", state.LatestOfferId);
    }

    [Fact]
    public void Restart_ReadReceiptSuppressesDuplicateAlert()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a"); state.Reconcile([Preview("one")], DateTime.UtcNow);
        var restarted = new FDriverFoodNotificationState(service);
        restarted.BindAccount("driver-a"); restarted.Reconcile([Preview("one")], DateTime.UtcNow);
        Assert.Single(service.Published); Assert.Equal(0, restarted.UnreadCount);
    }

    [Fact]
    public void ExpiredOrRemovedOffer_ClearsUnreadAndNativeNotification()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a"); state.Reconcile([Preview("one")], DateTime.UtcNow);
        var cancelBefore = service.CancelCount;
        state.Reconcile([], DateTime.UtcNow);
        Assert.Equal(0, state.UnreadCount); Assert.Null(state.LatestOfferId);
        Assert.Equal(cancelBefore + 1, service.CancelCount);
    }

    [Fact]
    public void NewAndExpiredTogether_CountsOnlyActionableFreshOffers()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a");
        state.Reconcile([Preview("old", -1), Preview("one"), Preview("two")], DateTime.UtcNow);
        Assert.Equal(2, state.UnreadCount); Assert.Equal("two", state.LatestOfferId);
        Assert.Equal("two", Assert.Single(service.Published).OfferId);
    }

    [Fact]
    public void DuplicateWorkspaceIds_DoNotRepeatCountOrAlert()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a"); state.Reconcile([Preview("one"), Preview("one")], DateTime.UtcNow);
        Assert.Equal(1, state.UnreadCount); Assert.Single(service.Published);
    }

    [Fact]
    public void MissingExpiryOrServerAction_DoesNotBecomeNativePermission()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        var noExpiry = Offer("unknown"); noExpiry.ExpiresAtUtc = null;
        var noAction = Offer("blocked"); noAction.AvailableActions = [];
        state.BindAccount("driver-a");
        state.Reconcile([DeliveryTicketPreview.From(noExpiry), DeliveryTicketPreview.From(noAction)], DateTime.UtcNow);
        Assert.Empty(service.Published); Assert.Equal(0, state.UnreadCount);
    }

    [Fact]
    public void PermissionDenied_KeepsInAppNoticeWithoutClaimingNativeDelivery()
    {
        var service = new Notifications { PublishAllowed = false }; var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a"); state.Reconcile([Preview("one")], DateTime.UtcNow);
        Assert.Equal(1, state.UnreadCount); Assert.Empty(service.Published);
    }

    [Fact]
    public void MarkRead_DismissesAlertAndDoesNotRealertSameOffer()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a"); state.Reconcile([Preview("one")], DateTime.UtcNow);
        state.MarkRead(); state.Reconcile([Preview("one")], DateTime.UtcNow);
        Assert.Equal(0, state.UnreadCount); Assert.Single(service.Published);
    }

    [Fact]
    public void AccountChange_DoesNotReuseSeenOrAnotherAccountTarget()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a"); state.Reconcile([Preview("one")], DateTime.UtcNow);
        service.Target = new("driver-a", "one"); state.BindAccount("driver-b");
        state.Reconcile([Preview("one")], DateTime.UtcNow);
        Assert.Null(service.Target); Assert.Equal("driver-b", service.Receipt!.UserId);
        Assert.Equal(2, service.Published.Count);
    }

    [Fact]
    public void ValidOpenTargetWithoutReceipt_IsRetainedUntilCanonicalRead()
    {
        var service = new Notifications { Target = new("driver-a", "one") };
        new FDriverFoodNotificationState(service).BindAccount("driver-a");
        Assert.Equal("one", service.Target!.OfferId);
    }

    [Fact]
    public void Logout_ClearRemovesAccountReceiptAndPendingOpen()
    {
        var service = new Notifications(); var state = new FDriverFoodNotificationState(service);
        state.BindAccount("driver-a"); state.Reconcile([Preview("one")], DateTime.UtcNow);
        service.Target = new("driver-a", "one"); state.Clear();
        Assert.Null(service.Target); Assert.Null(service.Receipt); Assert.Equal(0, state.UnreadCount);
    }

    [Fact]
    public async Task CanonicalExpiredOffers_AreRemovedFromNoticeListMapAndBundle()
    {
        var valid = Offer("fresh"); var expired = Offer("expired", -1);
        var api = Api([expired, valid]);
        var source = await api.Workspace(default);
        source.BundleCandidates = [new() { OfferIds = ["expired", "fresh"] }];
        var notifications = new Notifications(); var model = Model(api, notifications);
        await model.InitializeAsync();
        Assert.Equal("fresh", Assert.Single(model.RecommendedTicketItems).TicketId);
        Assert.Equal("fresh", Assert.Single(model.MapMarkers).RequestId);
        Assert.Empty(model.BundleCandidateItems); Assert.Equal(1, model.RecommendedTickets);
        Assert.Equal(1, model.HasNewRecommendations ? 1 : 0);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task CountdownExpiry_RemovesCachedNoticeAndSelectionWithoutServerMutation()
    {
        var offer = Offer("expiring"); var api = Api([offer]); var notifications = new Notifications();
        var model = Model(api, notifications); await model.InitializeAsync();
        Assert.True(model.HasNewRecommendations);
        var now = DateTime.UtcNow.AddMinutes(3);
        foreach (var item in model.RecommendedTicketItems) item.UpdateCountdown(now);
        typeof(MainPageModel).GetMethod("ReconcileFoodNotificationCountdowns", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(model, [now]);
        Assert.False(model.HasNewRecommendations); Assert.Empty(model.RecommendedTicketItems);
        Assert.Null(model.SelectedTicket); Assert.Empty(model.MapMarkers);
        Assert.Equal(0, api.StopWorkCalls);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task AcceptedOffer_ClearsOldRecommendationAlertAndKeepsCurrentDelivery()
    {
        var api = Api([Offer("one")]); var notifications = new Notifications(); var model = Model(api, notifications);
        await model.InitializeAsync();
        api.Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto
        {
            Recommendations = [Offer("one")], ActiveDeliveries = [new() { OfferId = "one", RestaurantName = "현재 배달" }]
        });
        await FDriverLifecycleTestSupport.RefreshBackground(model);
        Assert.False(model.HasNewRecommendations); Assert.Empty(model.RecommendedTicketItems);
        Assert.Equal("one", model.ActiveDelivery!.OfferId);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task PendingNotification_SelectsExactCanonicalOfferAfterRestart()
    {
        var notifications = new Notifications { Target = new("test-driver", "second") };
        var model = Model(Api([Offer("first"), Offer("second")]), notifications);
        await model.InitializeAsync();
        Assert.Equal("second", model.SelectedTicket!.TicketId); Assert.Null(notifications.Target);
        Assert.False(model.HasNewRecommendations); Assert.True(model.Navigation.IsDelivery);
        Assert.Equal("dispatch", model.TakeFoodNotificationFocus());
        Assert.Null(model.TakeFoodNotificationFocus());
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task ExpiredNotification_DoesNotSelectDifferentNewOffer()
    {
        var notifications = new Notifications { Target = new("test-driver", "expired") };
        var model = Model(Api([Offer("expired", -1), Offer("fresh")]), notifications);
        await model.InitializeAsync();
        Assert.Contains("해당 추천은 종료", model.StatusMessage);
        Assert.Null(notifications.Target); Assert.Equal("fresh", model.SelectedTicket!.TicketId);
        Assert.Empty(model.ActiveDeliveryItems);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task AnotherAccountNotification_IsDiscardedWithoutChangingSelectedWork()
    {
        var notifications = new Notifications { Target = new("someone-else", "second") };
        var model = Model(Api([Offer("first"), Offer("second")]), notifications);
        await model.InitializeAsync();
        Assert.Equal("first", model.SelectedTicket!.TicketId); Assert.Null(notifications.Target);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task FailedCanonicalRead_KeepsPointerForRetry_AndNoAlertFromCachedPayload()
    {
        var notifications = new Notifications { Target = new("test-driver", "second") };
        var api = Api([Offer("second")]);
        api.Workspace = _ => throw new FDriverApiException("일시 조회 실패", HttpStatusCode.ServiceUnavailable);
        var model = Model(api, notifications); await model.InitializeAsync();
        Assert.NotNull(notifications.Target); Assert.Empty(notifications.Published); Assert.Null(model.SelectedTicket);
        api.Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto { Recommendations = [Offer("second")] });
        await model.ResumeFoodNotificationWorkspaceAsync();
        Assert.Equal("second", model.SelectedTicket!.TicketId); Assert.Null(notifications.Target);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task OpenInAppNotice_RequeriesBeforeSelectingAndNeverAccepts()
    {
        var api = Api([Offer("first"), Offer("second")]); var notifications = new Notifications();
        var model = Model(api, notifications); await model.InitializeAsync();
        var reads = 0;
        api.Workspace = _ => { reads++; return Task.FromResult(new FoodDeliveryDriverWorkspaceDto { Recommendations = [Offer("second")] }); };
        await model.OpenNewRecommendationsCommand.ExecuteAsync(null);
        Assert.Equal(1, reads); Assert.Equal("second", model.SelectedTicket!.TicketId);
        Assert.Empty(model.ActiveDeliveryItems); Assert.False(model.HasNewRecommendations);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task ReturnToApp_RefreshesCanonicalWorkspaceImmediately()
    {
        var api = Api([Offer("before")]); var model = Model(api, new()); await model.InitializeAsync();
        api.Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto { Recommendations = [Offer("after")] });
        await model.ResumeFoodNotificationWorkspaceAsync();
        Assert.Equal("after", Assert.Single(model.RecommendedTicketItems).TicketId);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task DispatchOff_DoesNotPublishNewAlertsFromStaleRecommendation()
    {
        var api = Api([Offer("one")]); api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.Off });
        var notifications = new Notifications(); var model = Model(api, notifications); await model.InitializeAsync();
        Assert.False(model.HasNewRecommendations); Assert.Empty(notifications.Published);
        Assert.False(model.CanAcceptSelectedTicket);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task LateReturnReadAfterPageExit_CannotOpenNotificationTarget()
    {
        var api = Api([Offer("before")]); var notifications = new Notifications(); var model = Model(api, notifications);
        await model.InitializeAsync();
        notifications.Target = new("test-driver", "late");
        var late = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Workspace = _ => late.Task;
        var resume = model.ResumeFoodNotificationWorkspaceAsync(); await model.StopMonitoringAsync();
        late.SetResult(new() { Recommendations = [Offer("late")] }); await resume;
        Assert.Equal("before", model.SelectedTicket!.TicketId); Assert.NotNull(notifications.Target);
    }

    [Fact]
    public async Task FinalUnauthorized_RemovesPrivateNotificationReceiptAndOpenTarget()
    {
        var api = Api([Offer("one")]); var notifications = new Notifications(); var model = Model(api, notifications);
        await model.InitializeAsync(); notifications.Target = new("test-driver", "one");
        api.Workspace = _ => throw new FDriverApiException("인증 종료", HttpStatusCode.Unauthorized);
        await model.ResumeFoodNotificationWorkspaceAsync();
        Assert.False(model.IsAuthenticated); Assert.False(model.HasNewRecommendations);
        Assert.Null(notifications.Receipt); Assert.Null(notifications.Target);
        await model.StopMonitoringAsync();
    }

    private static FoodDeliveryDriverOfferDto Offer(string id, int expiresInMinutes = 2) => new()
    {
        OfferId = id, RestaurantName = "합성 음식점", DriverPayout = 2500,
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expiresInMinutes),
        AvailableActions = [new 업무가능행동Dto { ActionId = 음식배달가능행동Ids.기사제안수락 }]
    };
    private static DeliveryTicketPreview Preview(string id, int expiresInMinutes = 2) => DeliveryTicketPreview.From(Offer(id, expiresInMinutes));
    private static FDriverTestWorkspaceApi Api(IReadOnlyList<FoodDeliveryDriverOfferDto> offers)
    {
        var workspace = new FoodDeliveryDriverWorkspaceDto { DriverId = "test-driver", Recommendations = offers, MaxActiveDeliveries = 3 };
        return new()
        {
            Workspace = _ => Task.FromResult(workspace),
            Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On })
        };
    }
    private static MainPageModel Model(FDriverTestWorkspaceApi api, Notifications notifications)
    {
        var session = new FDriverTestSession();
        var http = new HttpClient(new FDriverTestHttpHandler((_, _) => Task.FromResult(FDriverTestHttpHandler.TokenResponse())))
            { BaseAddress = new Uri("http://localhost/") };
        return new(new(), session, new(http, session), api, new FDriverTestLocationService(), new(), notifications);
    }
    private sealed class Notifications : IFDriverFoodNotificationService
    {
        public FDriverFoodNotificationReceipt? Receipt { get; set; }
        public FDriverFoodNotificationTarget? Target { get; set; }
        public bool PublishAllowed { get; set; } = true;
        public int CancelCount { get; private set; }
        public List<FDriverFoodNotificationTarget> Published { get; } = [];
        public FDriverFoodNotificationReceipt? ReadReceipt() => Receipt;
        public void WriteReceipt(FDriverFoodNotificationReceipt receipt) => Receipt = new(receipt.UserId, receipt.SeenOfferIds.ToArray());
        public FDriverFoodNotificationTarget? ReadOpenTarget() => Target;
        public void SetOpenTarget(FDriverFoodNotificationTarget target) => Target = target;
        public void ClearOpenTarget() => Target = null;
        public bool Publish(string userId, string offerId, DateTime expiry)
        {
            if (!PublishAllowed) return false;
            Published.Add(new(userId, offerId)); return true;
        }
        public void Cancel() => CancelCount++;
        public void ClearAccount() { Cancel(); Receipt = null; Target = null; }
    }
}
