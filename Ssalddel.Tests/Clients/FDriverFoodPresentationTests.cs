using System.Globalization;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverFoodPresentationTests
{
    [Theory]
    [InlineData(true, true, true, true, false, false)]
    [InlineData(false, true, true, false, false, true)]
    [InlineData(false, true, false, false, true, false)]
    [InlineData(false, false, true, false, false, true)]
    [InlineData(false, false, false, false, false, false)]
    public void ServerActions_ArrivalPrecedesPickupWhenBothAreAllowed(bool complete, bool pickup, bool arrival,
        bool expectedComplete, bool expectedPickup, bool expectedArrival)
    {
        var model = Model();
        model.ActiveDelivery = Delivery(2500, Actions(complete, pickup, arrival));
        Assert.Equal(expectedComplete, model.IsCompletionPrimaryAction);
        Assert.Equal(expectedPickup, model.IsPickupPrimaryAction);
        Assert.Equal(expectedArrival, model.IsArrivalPrimaryAction);
        Assert.False(model.IsArrivalSecondaryAction);
        Assert.False(model.IsRecommendationPrimaryAction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Busy_DisablesTheStageActionWithoutChangingItsIdentity(bool arrived)
    {
        var model = Model();
        model.IsAuthenticated = true;
        model.ActiveDelivery = Delivery(2500, Actions(false, true, !arrived)) with
        {
            RestaurantArrivedAtUtc = arrived ? DateTime.UtcNow : null
        };
        Assert.Equal(arrived, model.CanConfirmPickup);
        Assert.Equal(!arrived, model.CanRecordArrival);
        model.IsBusy = true;
        Assert.Equal(arrived, model.IsPickupPrimaryAction);
        Assert.Equal(!arrived, model.IsArrivalPrimaryAction);
        Assert.False(model.IsArrivalSecondaryAction);
        Assert.False(model.CanConfirmPickup);
        Assert.False(model.CanRecordArrival);
    }

    [Fact]
    public async Task PickupCommand_BeforeArrivalDoesNotCallApi_AfterCanonicalArrivalItDoes()
    {
        var source = DeliveryData(2500, Actions(false, true, true));
        var inner = WorkspaceApi(() => source);
        var api = new PickupCountingApi(inner);
        var model = Model(api);
        try
        {
            await model.InitializeAsync();
            Assert.Equal(FDriverFoodStage.PickupTravel, model.CurrentDeliveryStage);
            Assert.True(model.CanRecordArrival);
            Assert.False(model.CanConfirmPickup);
            await model.ConfirmPickupCommand.ExecuteAsync(null);
            Assert.Equal(0, api.PickupCalls);

            source.RestaurantArrivedAtUtc = DateTime.UtcNow;
            source.AttemptRevision++;
            source.AvailableActions = Actions(false, true, false);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(FDriverFoodStage.PickupWaiting, model.CurrentDeliveryStage);
            Assert.True(model.CanConfirmPickup);
            await model.ConfirmPickupCommand.ExecuteAsync(null);
            Assert.Equal(1, api.PickupCalls);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public void RecordedArrivalWithoutPickupPermission_KeepsPickupCardAndDisablesItsAction()
    {
        var model = Model();
        model.IsAuthenticated = true;
        model.ActiveDelivery = Delivery(2500, []) with { RestaurantArrivedAtUtc = DateTime.UtcNow };
        Assert.Equal(FDriverFoodStage.PickupWaiting, model.CurrentDeliveryStage);
        Assert.True(model.HasPickupStage);
        Assert.True(model.IsPickupPrimaryAction);
        Assert.False(model.CanConfirmPickup);
        Assert.False(model.CanRecordArrival);
        Assert.False(model.HasDeliveryStage);
    }

    [Fact]
    public async Task RecookingWaitsForCurrentRoundReadyAndPermissionBeforeExplicitPickup()
    {
        var recookingAt = DateTime.UtcNow.AddMinutes(-10);
        var source = DeliveryData(4200, []);
        source.DeliveryAttemptId = "replacement-attempt";
        source.AttemptRevision = 2;
        source.RestaurantArrivedAtUtc = recookingAt.AddMinutes(2);
        source.CurrentPreparationRound = 2;
        source.RecookingRequestedAtUtc = recookingAt;
        source.DisplayedPreparationReadyAtUtc = recookingAt.AddMinutes(1);
        var api = new PickupCountingApi(WorkspaceApi(() => source));
        var model = Model(api);
        try
        {
            await model.InitializeAsync();
            Assert.Equal(2, model.ActiveDelivery!.CurrentPreparationRound);
            Assert.Equal(recookingAt, model.ActiveDelivery.RecookingRequestedAtUtc);
            Assert.True(model.ActiveDelivery.IsRecooking);
            Assert.Equal(FDriverFoodStage.PickupWaiting, model.CurrentDeliveryStage);
            Assert.Contains("재조리 음식 · 준비 중", model.PickupPreparationTimeText);
            Assert.Contains("준비 예정", model.PickupPreparationTimeText);
            Assert.DoesNotContain("준비 완료", model.PickupPreparationTimeText);
            Assert.Contains("이번 음식의 준비 완료를 기다려", model.NextActionGuide);
            Assert.False(model.CanConfirmPickup);
            await model.ConfirmPickupCommand.ExecuteAsync(null);
            Assert.Equal(0, api.PickupCalls);

            source.CurrentPickupReadyAtUtc = recookingAt.AddMinutes(8);
            source.AvailableActions = Actions(false, true, false);
            await FDriverLifecycleTestSupport.RefreshBackground(model);

            Assert.Equal(source.CurrentPickupReadyAtUtc, model.ActiveDelivery!.CurrentPickupReadyAtUtc);
            Assert.Contains("재조리 음식 · 준비 완료", model.PickupPreparationTimeText);
            Assert.Contains("음식을 받은 뒤 픽업 완료", model.NextActionGuide);
            Assert.True(model.CanConfirmPickup);
            Assert.Equal(0, api.PickupCalls);
            await model.ConfirmPickupCommand.ExecuteAsync(null);
            Assert.Equal(1, api.PickupCalls);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public void CurrentReadyTimestampDoesNotGrantPickupWithoutServerAction()
    {
        var model = Model();
        model.IsAuthenticated = true;
        var source = DeliveryData(4200, []);
        source.RestaurantArrivedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        source.CurrentPreparationRound = 2;
        source.RecookingRequestedAtUtc = DateTime.UtcNow.AddMinutes(-10);
        source.CurrentPickupReadyAtUtc = DateTime.UtcNow.AddMinutes(-1);
        model.ActiveDelivery = ActiveDeliveryPreview.From(source);

        Assert.True(model.HasPickupPreparationTime);
        Assert.Contains("재조리 음식 · 준비 완료", model.PickupPreparationTimeText);
        Assert.False(model.CanConfirmPickup);
        Assert.True(model.IsPickupPrimaryAction);
    }

    [Fact]
    public void RecookingMetadataDoesNotKeepPreparationInformationAfterPickup()
    {
        var model = Model();
        var source = DeliveryData(4200, Actions(true, false, false));
        source.WorkStatus = DriverWorkOfferStatus.MovingToDropoff;
        source.CurrentPreparationRound = 2;
        source.RecookingRequestedAtUtc = DateTime.UtcNow.AddMinutes(-10);
        source.CurrentPickupReadyAtUtc = DateTime.UtcNow.AddMinutes(-2);
        model.ActiveDelivery = ActiveDeliveryPreview.From(source);

        Assert.Equal(FDriverFoodStage.Delivery, model.CurrentDeliveryStage);
        Assert.True(model.HasDeliveryStage);
        Assert.False(model.HasPickupPreparationTime);
        Assert.Equal(string.Empty, model.PickupPreparationTimeText);
    }

    [Theory]
    [InlineData(DriverWorkOfferStatus.PickupConfirmed)]
    [InlineData(DriverWorkOfferStatus.MovingToDropoff)]
    public void PickupConfirmedAndDeliveryStates_ShowRecipientWithoutInventingCompletionPermission(string status)
    {
        var source = DeliveryData(4300, []);
        source.WorkStatus = status;
        source.Recipient = new() { DisplayName = "지정 수령자", DeliveryInstructions = "문 앞 전달", OrdererIsRecipient = false };
        var model = Model();
        model.IsAuthenticated = true;
        model.ActiveDelivery = ActiveDeliveryPreview.From(source);
        Assert.Equal(FDriverFoodStage.Delivery, model.CurrentDeliveryStage);
        Assert.True(model.HasDeliveryStage);
        Assert.True(model.HasDeliveryRecipient);
        Assert.False(model.HasPickupStage);
        Assert.False(model.IsCurrentTargetPickup);
        Assert.True(model.IsCompletionPrimaryAction);
        Assert.False(model.CanCompleteDelivery);
        Assert.Contains(source.Dropoff.Address, model.CurrentDeliveryTargetText);
        Assert.Equal("지정 수령자", model.ActiveDelivery.RecipientRelationshipText);
    }

    [Fact]
    public async Task SameDeliveryRefresh_PreservesCollapse_StageChangeReopensWithCanonicalFee()
    {
        var source = DeliveryData(2500, Actions(false, true, true));
        var model = FDriverLifecycleTestSupport.Model(new(), WorkspaceApi(() => source));
        try
        {
            await model.InitializeAsync();
            Assert.True(model.IsCurrentDeliveryExpanded);
            var initialKey = model.CurrentDeliveryPresentationKey;
            var contextChanges = 0;
            model.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainPageModel.CurrentDeliveryPresentationKey)) contextChanges++;
            };
            model.ToggleCurrentDeliveryCardCommand.Execute(null);
            Assert.True(model.IsCurrentDeliverySummaryVisible);
            Assert.False(model.IsCurrentDeliveryContentVisible);

            source.AttemptRevision++;
            source.DriverPayout = 3300;
            source.DisplayedPreparationReadyAtUtc = DateTime.UtcNow.AddMinutes(5);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.False(model.IsCurrentDeliveryExpanded);
            Assert.True(model.IsCurrentDeliverySummaryVisible);
            Assert.Equal(FDriverFoodStage.PickupTravel, model.CurrentDeliveryStage);
            Assert.Equal(3300m, model.ActiveDelivery!.DriverPayout);
            Assert.Equal(source.AttemptRevision, model.ActiveDelivery.AttemptRevision);
            Assert.Equal(initialKey, model.CurrentDeliveryPresentationKey);
            Assert.Equal(0, contextChanges);

            source.RestaurantArrivedAtUtc = DateTime.UtcNow;
            source.AvailableActions = Actions(false, true, false);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.True(model.IsCurrentDeliveryExpanded);
            Assert.Equal(FDriverFoodStage.PickupWaiting, model.CurrentDeliveryStage);
            Assert.NotEqual(initialKey, model.CurrentDeliveryPresentationKey);
            Assert.Equal(1, contextChanges);
            model.ToggleCurrentDeliveryCardCommand.Execute(null);

            source.WorkStatus = DriverWorkOfferStatus.MovingToDropoff;
            source.AvailableActions = Actions(true, false, false);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.True(model.IsCurrentDeliveryExpanded);
            Assert.Equal(FDriverFoodStage.Delivery, model.CurrentDeliveryStage);
            Assert.Equal(3300m, model.ActiveDelivery!.DriverPayout);
            Assert.Equal(source.WorkStatus, model.ActiveDelivery.WorkStatus);
            Assert.Same(source.AvailableActions, model.ActiveDelivery.AvailableActions);
            Assert.Equal(2, contextChanges);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NextOfferOrAttempt_ReopensCardEvenWhenTheStageIsUnchanged(bool nextOffer)
    {
        var source = DeliveryData(2500, Actions(false, true, true));
        var model = FDriverLifecycleTestSupport.Model(new(), WorkspaceApi(() => source));
        try
        {
            await model.InitializeAsync();
            model.ToggleCurrentDeliveryCardCommand.Execute(null);
            if (nextOffer) source.OfferId = "next-offer";
            else source.DeliveryAttemptId = "next-attempt";
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.True(model.IsCurrentDeliveryExpanded);
            Assert.True(model.IsCurrentDeliveryContentVisible);
            Assert.False(model.IsCurrentDeliverySummaryVisible);
            Assert.Equal(FDriverFoodStage.PickupTravel, model.CurrentDeliveryStage);
            Assert.Equal(source.OfferId, model.ActiveDelivery!.OfferId);
            Assert.Equal(source.DeliveryAttemptId, model.ActiveDelivery.DeliveryAttemptId);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public void CurrentCardToggle_DoesNotMutateServerDeliveryOrItsFeeAndPermissions()
    {
        var model = Model();
        var delivery = Delivery(4700, Actions(false, true, true));
        model.ActiveDeliveryItems.Add(delivery);
        model.ActiveDelivery = delivery;
        model.ToggleCurrentDeliveryCardCommand.Execute(null);
        Assert.False(model.IsFoodCardExpanded);
        Assert.Same(delivery, model.ActiveDelivery);
        Assert.Same(delivery, Assert.Single(model.ActiveDeliveryItems));
        Assert.Equal(4700m, model.ActiveDelivery.DriverPayout);
        Assert.Equal(DriverWorkOfferStatus.MovingToPickup, model.ActiveDelivery.WorkStatus);
        Assert.True(model.ActiveDelivery.Can(음식배달가능행동Ids.기사가게도착));
        Assert.True(model.ActiveDelivery.Can(음식배달가능행동Ids.기사픽업확인));
        model.ToggleCurrentDeliveryCardCommand.Execute(null);
        Assert.True(model.IsCurrentDeliveryContentVisible);
        Assert.Same(delivery, model.ActiveDelivery);
    }

    [Fact]
    public void ArrivalWithoutAttempt_CannotBecomeAnAction()
    {
        var model = Model();
        model.ActiveDelivery = Delivery(2500, Actions(false, false, true)) with { DeliveryAttemptId = "" };
        Assert.False(model.IsArrivalPrimaryAction);
        Assert.False(model.IsArrivalSecondaryAction);
        Assert.False(model.HasDeliveryPrimaryAction);
    }

    [Fact]
    public void CurrentCard_PreservesFullTargetAndFeeOfSelectedActiveDelivery()
    {
        var model = Model();
        var selected = Delivery(2500, Actions(false, true, false));
        model.ActiveDeliveryItems.Add(selected);
        model.ActiveDeliveryItems.Add(Delivery(9900, []));
        model.ActiveDelivery = selected;
        Assert.False(model.IsDeliveryDetailsExpanded);
        Assert.True(model.IsFoodCardExpanded);
        Assert.True(model.IsCurrentTargetPickup);
        Assert.Contains(selected.Pickup.Address, model.CurrentDeliveryTargetText);
        Assert.Equal($"배달료 {2500m.ToString("N0", CultureInfo.CurrentCulture)}원", model.CurrentDeliveryFeeText);
        Assert.DoesNotContain(12400m.ToString("N0", CultureInfo.CurrentCulture), model.CurrentDeliveryFeeText);
        model.ActiveDelivery = selected with
        {
            WorkStatus = DriverWorkOfferStatus.MovingToDropoff,
            AvailableActions = Actions(true, false, false)
        };
        Assert.Equal("전달할 곳", model.CurrentDeliveryTargetLabel);
        Assert.False(model.IsCurrentTargetPickup);
        Assert.Contains(selected.Dropoff.Address, model.CurrentDeliveryTargetText);
    }

    [Theory]
    [InlineData(FDriverFoodStage.PickupTravel)]
    [InlineData(FDriverFoodStage.PickupWaiting)]
    [InlineData(FDriverFoodStage.Delivery)]
    public void ActiveDeliveryWithoutRecommendations_DetailsCommandOpensAndClosesExistingWork(FDriverFoodStage stage)
    {
        var model = Model(); model.IsAuthenticated = true;
        var source = DeliveryData(4200, stage == FDriverFoodStage.Delivery ? Actions(true, false, false)
            : stage == FDriverFoodStage.PickupWaiting ? Actions(false, true, false) : Actions(false, false, true));
        source.OrderNo = "existing-order-for-support";
        if (stage == FDriverFoodStage.PickupWaiting) source.RestaurantArrivedAtUtc = DateTime.UtcNow;
        if (stage == FDriverFoodStage.Delivery) source.WorkStatus = DriverWorkOfferStatus.MovingToDropoff;
        var delivery = ActiveDeliveryPreview.From(source); model.ActiveDelivery = delivery;
        var visibility = new List<bool>();
        model.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(MainPageModel.IsDeliveryDetailsVisible)) visibility.Add(model.IsDeliveryDetailsVisible);
        };

        Assert.False(model.HasRecommendationContent); Assert.True(model.HasDeliveryDetails);
        Assert.False(model.IsDeliveryDetailsExpanded); Assert.Equal(stage, model.CurrentDeliveryStage);
        model.ToggleDeliveryDetailsCommand.Execute(null);
        Assert.True(model.IsDeliveryDetailsExpanded); Assert.True(model.IsDeliveryDetailsVisible);
        Assert.True(model.ActiveDelivery!.HasSupportSource); Assert.Equal(source.OrderNo, model.ActiveDelivery.OrderNo);
        Assert.Same(delivery, model.ActiveDelivery); Assert.Equal(stage, model.CurrentDeliveryStage);
        model.ToggleDeliveryDetailsCommand.Execute(null);
        Assert.False(model.IsDeliveryDetailsExpanded); Assert.False(model.IsDeliveryDetailsVisible);
        Assert.Same(delivery, model.ActiveDelivery); Assert.Equal(stage, model.CurrentDeliveryStage);
        Assert.Equal(new[] { true, false }, visibility);
    }

    [Fact]
    public void RecommendationSelection_RemainsExplicitAcceptAndPresentationTogglesPreserveSelection()
    {
        var model = Model();
        var ticket = DeliveryTicketPreview.From(new FoodDeliveryDriverOfferDto
        {
            OfferId = "recommendation", DriverPayout = 4300,
            Pickup = new() { Label = "추천 음식점", Address = "중랑구 추천로 5 1층" }
        });
        model.RecommendedTicketItems.Add(ticket);
        model.SelectedTicket = ticket;
        Assert.True(model.IsRecommendationPrimaryAction);
        Assert.False(model.CanAcceptSelectedTicket);
        Assert.Equal(FDriverFoodStage.Waiting, model.CurrentDeliveryStage);
        Assert.False(model.IsCurrentDeliveryContentVisible);
        Assert.False(model.IsCurrentDeliverySummaryVisible);
        Assert.False(model.HasPickupStage);
        Assert.False(model.HasDeliveryStage);
        model.ToggleCurrentDeliveryCardCommand.Execute(null);
        Assert.False(model.IsCurrentDeliveryExpanded);
        model.ToggleDeliveryDetailsCommand.Execute(null);
        Assert.True(model.IsDeliveryDetailsVisible);
        model.ToggleWorkControlsCommand.Execute(null);
        Assert.True(model.IsFoodCardExpanded);
        Assert.Same(ticket, model.SelectedTicket);
        Assert.Null(model.ActiveDelivery);
        Assert.Contains(ticket.Pickup.Address, model.CurrentDeliveryTargetText);
        model.ToggleDeliveryDetailsCommand.Execute(null);
        Assert.False(model.IsDeliveryDetailsVisible);
        Assert.True(model.IsFoodCardExpanded);
        model.ToggleWorkControlsCommand.Execute(null);
        Assert.False(model.IsFoodCardExpanded);
    }

    [Theory]
    [InlineData(480, 1200, 72, false, 192)]
    [InlineData(480, 1200, 72, true, 336)]
    [InlineData(600, 100, 48, false, 188)]
    [InlineData(0, 100, 48, false, 0)]
    public void MeasuredCard_ReservesMapSpaceEvenWhenTextContentGrows(double usable, double body,
        double footer, bool expanded, double expected)
        => Assert.Equal(expected, FDriverFoodPresentationLayout.CardHeight(usable, body, footer, expanded), 6);

    [Theory]
    [InlineData(true, 775)]
    [InlineData(false, 485.6)]
    public void MeasuredCard_ReservesBothMapOverlaysBeyondPercentageMinimum(bool expanded, double expected)
    {
        // The 320 DIP/font-scale 2.0 capture had 365px of map, while its
        // legend and recenter occupied 210+45 and 163+21px respectively.
        const double usable = 1214;
        const double overlaysWithMargins = 210 + 45 + 163 + 21;
        var height = FDriverFoodPresentationLayout.CardHeight(usable, 2000, 163, expanded, overlaysWithMargins);
        Assert.Equal(expected, height, 6);
        Assert.True(usable - height >= overlaysWithMargins);
        Assert.True(height <= usable * (expanded ? 0.70 : 0.40));
    }

    [Fact]
    public void MeasuredCard_SufficientMapSpaceKeepsExistingExpandedHeight()
        => Assert.Equal(336, FDriverFoodPresentationLayout.CardHeight(480, 1200, 72, true, 100), 6);

    private static MainPageModel Model() => FDriverLifecycleTestSupport.Model(new(), new());

    private static MainPageModel Model(IFoodDeliveryDriverApiService api)
    {
        var session = new FDriverTestSession();
        var http = new HttpClient(new FDriverTestHttpHandler((_, _) => Task.FromResult(FDriverTestHttpHandler.TokenResponse())))
        { BaseAddress = new Uri("http://localhost/") };
        return new(new(), session, new(http, session), api, new FDriverTestLocationService(), new());
    }

    private static FDriverTestWorkspaceApi WorkspaceApi(Func<FoodDeliveryDriverActiveDeliveryDto> delivery)
        => new()
        {
            Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto
            {
                DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3,
                ActiveDeliveries = [delivery()]
            })
        };

    private static ActiveDeliveryPreview Delivery(decimal fee, IReadOnlyList<업무가능행동Dto> actions)
        => ActiveDeliveryPreview.From(DeliveryData(fee, actions));

    private static FoodDeliveryDriverActiveDeliveryDto DeliveryData(decimal fee, IReadOnlyList<업무가능행동Dto> actions)
        => new()
        {
            OfferId = "active", DeliveryAttemptId = "attempt", WorkStatus = DriverWorkOfferStatus.MovingToPickup,
            DriverPayout = fee, AvailableActions = actions,
            Pickup = new() { Label = "음식점", Address = "서울특별시 중랑구 용마산로 332-22 1층 안쪽 출입구" },
            Dropoff = new() { Label = "고객 주소", Address = "서울특별시 중랑구 면목로 44길 123 아주 긴 동 이름 1203호" }
        };

    private sealed class PickupCountingApi(FDriverTestWorkspaceApi inner) : IFoodDeliveryDriverApiService
    {
        public int PickupCalls { get; private set; }
        public Task<FoodDeliveryDriverActionResponse> ConfirmPickupAsync(string offerId, CancellationToken cancellationToken = default)
        {
            PickupCalls++;
            return inner.ConfirmPickupAsync(offerId, cancellationToken);
        }
        public Task<FoodDeliveryDriverWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
            => inner.GetWorkspaceAsync(cancellationToken);
        public Task<FoodDeliveryDailySettlementDto> GetDailySettlementAsync(DateOnly date, CancellationToken cancellationToken = default)
            => inner.GetDailySettlementAsync(date, cancellationToken);
        public Task<FoodDeliveryCompletedDeliveryDetailDto> GetCompletedDeliveryDetailAsync(string settlementId, CancellationToken cancellationToken = default)
            => inner.GetCompletedDeliveryDetailAsync(settlementId, cancellationToken);
        public Task<기사운행상태응답?> GetWorkStatusAsync(CancellationToken cancellationToken = default)
            => inner.GetWorkStatusAsync(cancellationToken);
        public Task<운영배차수신상태Dto> GetDispatchAvailabilityAsync(CancellationToken cancellationToken = default)
            => inner.GetDispatchAvailabilityAsync(cancellationToken);
        public Task<운영배차수신상태Dto> ChangeDispatchIntentAsync(운영배차수신의사변경요청 request, CancellationToken cancellationToken = default)
            => inner.ChangeDispatchIntentAsync(request, cancellationToken);
        public Task StartWorkAsync(string startLocation, CancellationToken cancellationToken = default)
            => inner.StartWorkAsync(startLocation, cancellationToken);
        public Task StopWorkAsync(CancellationToken cancellationToken = default)
            => inner.StopWorkAsync(cancellationToken);
        public Task<기사위치갱신응답?> UpdateLocationAsync(기사위치갱신요청 request, CancellationToken cancellationToken = default)
            => inner.UpdateLocationAsync(request, cancellationToken);
        public Task<FoodDeliveryDriverActionResponse> AcceptAsync(string offerId, CancellationToken cancellationToken = default)
            => inner.AcceptAsync(offerId, cancellationToken);
        public Task<FoodDeliveryDriverActionResponse> RejectAsync(string offerId, CancellationToken cancellationToken = default)
            => inner.RejectAsync(offerId, cancellationToken);
        public Task<FoodDeliveryDriverActionResponse> AcceptBundleAsync(IReadOnlyList<string> offerIds, CancellationToken cancellationToken = default)
            => inner.AcceptBundleAsync(offerIds, cancellationToken);
        public Task<FoodDeliveryDriverActionResponse> RecordRestaurantArrivalAsync(string offerId, 음식배달가게도착요청 request, CancellationToken cancellationToken = default)
            => inner.RecordRestaurantArrivalAsync(offerId, request, cancellationToken);
        public Task<FoodDeliveryDriverActionResponse> InterruptAsync(string offerId, 음식배달중단요청 request, CancellationToken cancellationToken = default)
            => inner.InterruptAsync(offerId, request, cancellationToken);
        public Task<FoodDeliveryDriverActionResponse> CompleteAsync(string offerId, CancellationToken cancellationToken = default)
            => inner.CompleteAsync(offerId, cancellationToken);
        public Task<FoodDeliveryDriverRouteResponseDto> GetRouteAsync(FoodDeliveryDriverRouteRequestDto request, CancellationToken cancellationToken = default)
            => inner.GetRouteAsync(request, cancellationToken);
    }

    private static IReadOnlyList<업무가능행동Dto> Actions(bool complete, bool pickup, bool arrival)
    {
        var actions = new List<업무가능행동Dto>();
        if (complete) actions.Add(new() { ActionId = 음식배달가능행동Ids.기사전달완료 });
        if (pickup) actions.Add(new() { ActionId = 음식배달가능행동Ids.기사픽업확인 });
        if (arrival) actions.Add(new() { ActionId = 음식배달가능행동Ids.기사가게도착 });
        return actions;
    }
}
