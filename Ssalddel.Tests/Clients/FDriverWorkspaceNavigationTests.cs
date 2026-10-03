using System.Net;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverWorkspaceNavigationTests
{
    [Fact]
    public void NewWorkspace_StartsInDelivery_WithNoPrivateScrollHistory()
    {
        var navigation = new FDriverWorkspaceNavigationState();

        Assert.Equal(FDriverWorkspaceSection.Delivery, navigation.Selected);
        Assert.True(navigation.IsDelivery);
        Assert.False(navigation.IsAuxiliary);
        Assert.False(navigation.TryReturnToDelivery());
        foreach (var section in Enum.GetValues<FDriverWorkspaceSection>())
            Assert.Equal(0d, navigation.GetScrollY(section));
    }

    [Theory]
    [InlineData(FDriverWorkspaceSection.Delivery)]
    [InlineData(FDriverWorkspaceSection.Settlement)]
    [InlineData(FDriverWorkspaceSection.Profile)]
    public void SectionSelection_ExposesOnlyItsOwnWorkspace(FDriverWorkspaceSection selected)
    {
        var navigation = new FDriverWorkspaceNavigationState();

        navigation.Select(selected);

        Assert.Equal(selected, navigation.Selected);
        Assert.Equal(selected == FDriverWorkspaceSection.Delivery, navigation.IsDelivery);
        Assert.Equal(selected == FDriverWorkspaceSection.Settlement, navigation.IsSettlement);
        Assert.Equal(selected == FDriverWorkspaceSection.Profile, navigation.IsProfile);
        Assert.Equal(selected != FDriverWorkspaceSection.Delivery, navigation.IsAuxiliary);
        Assert.False(string.IsNullOrWhiteSpace(navigation.Title));
    }

    [Fact]
    public void SectionChange_NotifiesTheBindingsThatHideAndLabelWorkspaces()
    {
        var navigation = new FDriverWorkspaceNavigationState();
        var notified = new List<string?>();
        navigation.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        navigation.Select(FDriverWorkspaceSection.Profile);

        Assert.Contains(nameof(navigation.Selected), notified);
        Assert.Contains(nameof(navigation.IsDelivery), notified);
        Assert.Contains(nameof(navigation.IsSettlement), notified);
        Assert.Contains(nameof(navigation.IsProfile), notified);
        Assert.Contains(nameof(navigation.IsAuxiliary), notified);
        Assert.Contains(nameof(navigation.Title), notified);
    }

    [Theory]
    [InlineData("settlement", FDriverWorkspaceSection.Settlement)]
    [InlineData("profile", FDriverWorkspaceSection.Profile)]
    [InlineData("dispatch", FDriverWorkspaceSection.Delivery)]
    [InlineData("bundle", FDriverWorkspaceSection.Delivery)]
    [InlineData("delivery", FDriverWorkspaceSection.Delivery)]
    [InlineData("workspace", FDriverWorkspaceSection.Delivery)]
    [InlineData("restaurant", FDriverWorkspaceSection.Delivery)]
    [InlineData("customer", FDriverWorkspaceSection.Delivery)]
    [InlineData("route", FDriverWorkspaceSection.Delivery)]
    public void ExistingEntryFocus_OpensTheOwningWorkspace(string focus, FDriverWorkspaceSection expected)
    {
        var navigation = new FDriverWorkspaceNavigationState();
        navigation.Select(FDriverWorkspaceSection.Profile);

        Assert.True(navigation.TryApplyFocus(focus));
        Assert.Equal(expected, navigation.Selected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unrecognized-entry")]
    public void MissingOrUnknownFocus_DoesNotDiscardTheCurrentWorkspace(string? focus)
    {
        var navigation = new FDriverWorkspaceNavigationState();
        navigation.Select(FDriverWorkspaceSection.Settlement);
        navigation.RememberScroll(FDriverWorkspaceSection.Settlement, 180.5d);

        Assert.False(navigation.TryApplyFocus(focus));
        Assert.Equal(FDriverWorkspaceSection.Settlement, navigation.Selected);
        Assert.Equal(180.5d, navigation.GetScrollY(FDriverWorkspaceSection.Settlement));
    }

    [Fact]
    public void ScrollHistory_IsIndependentAcrossWorkspaces_AndSurvivesReturningToDelivery()
    {
        var navigation = new FDriverWorkspaceNavigationState();
        navigation.RememberScroll(FDriverWorkspaceSection.Delivery, 120d);
        navigation.RememberScroll(FDriverWorkspaceSection.Settlement, 860.5d);
        navigation.RememberScroll(FDriverWorkspaceSection.Profile, 44d);
        navigation.Select(FDriverWorkspaceSection.Profile);

        Assert.True(navigation.TryReturnToDelivery());
        Assert.False(navigation.TryReturnToDelivery());
        Assert.True(navigation.IsDelivery);
        Assert.Equal(120d, navigation.GetScrollY(FDriverWorkspaceSection.Delivery));
        Assert.Equal(860.5d, navigation.GetScrollY(FDriverWorkspaceSection.Settlement));
        Assert.Equal(44d, navigation.GetScrollY(FDriverWorkspaceSection.Profile));

        navigation.Select(FDriverWorkspaceSection.Settlement);
        Assert.Equal(860.5d, navigation.GetScrollY(navigation.Selected));
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidScrollMeasurement_IsClampedWithoutChangingOtherWorkspaces(double measured)
    {
        var navigation = new FDriverWorkspaceNavigationState();
        navigation.RememberScroll(FDriverWorkspaceSection.Delivery, 90d);
        navigation.RememberScroll(FDriverWorkspaceSection.Settlement, 220d);

        navigation.RememberScroll(FDriverWorkspaceSection.Settlement, measured);

        Assert.Equal(0d, navigation.GetScrollY(FDriverWorkspaceSection.Settlement));
        Assert.Equal(90d, navigation.GetScrollY(FDriverWorkspaceSection.Delivery));
        Assert.Equal(FDriverWorkspaceSection.Delivery, navigation.Selected);
    }

    [Fact]
    public void Reset_RemovesAllPrivateScrollHistory_AndReturnsToDelivery()
    {
        var navigation = new FDriverWorkspaceNavigationState();
        foreach (var section in Enum.GetValues<FDriverWorkspaceSection>())
            navigation.RememberScroll(section, 140d);
        navigation.Select(FDriverWorkspaceSection.Profile);

        navigation.Reset();

        Assert.True(navigation.IsDelivery);
        Assert.False(navigation.IsAuxiliary);
        foreach (var section in Enum.GetValues<FDriverWorkspaceSection>())
            Assert.Equal(0d, navigation.GetScrollY(section));
    }

    [Fact]
    public async Task WorkspaceNavigation_PreservesSelectedDeliveryAndRoute_WithoutApiRequests()
    {
        var session = new FDriverTestSession();
        var workspaceCalls = 0;
        var routeCalls = 0;
        var api = new FDriverTestWorkspaceApi
        {
            Workspace = _ =>
            {
                workspaceCalls++;
                return Task.FromResult(FDriverTestWorkspaceApi.Data("selected-delivery", coordinates: true));
            },
            Route = _ =>
            {
                routeCalls++;
                return Task.FromResult(new FoodDeliveryDriverRouteResponseDto
                {
                    DistanceKm = 2m,
                    DurationMinutes = 8,
                    Points = [new() { Latitude = 37.5m, Longitude = 127m }, new() { Latitude = 37.6m, Longitude = 127.1m }]
                });
            }
        };
        var model = FDriverLifecycleTestSupport.Model(session, api);
        try
        {
            await model.InitializeAsync();
            await model.SelectTicketCommand.ExecuteAsync(Assert.Single(model.RecommendedTicketItems));
            var selectedTicket = model.SelectedTicket;
            var routeOverlays = model.SelectedRouteOverlays;
            var mapMarkers = model.MapMarkers;
            var routeText = model.RouteStatusText;
            var workStage = model.WorkStage;
            var payout = model.TodayExpectedPayout;
            var workspaceCallsBeforeNavigation = workspaceCalls;
            var routeCallsBeforeNavigation = routeCalls;
            var workStatusCallsBeforeNavigation = api.WorkStatusCalls;
            Assert.NotNull(selectedTicket);
            Assert.NotEmpty(routeOverlays);

            model.Navigation.Select(FDriverWorkspaceSection.Settlement);
            model.Navigation.RememberScroll(FDriverWorkspaceSection.Settlement, 480d);
            model.Navigation.Select(FDriverWorkspaceSection.Profile);
            Assert.True(model.Navigation.TryReturnToDelivery());

            Assert.Same(selectedTicket, model.SelectedTicket);
            Assert.Same(routeOverlays, model.SelectedRouteOverlays);
            Assert.Same(mapMarkers, model.MapMarkers);
            Assert.Equal(routeText, model.RouteStatusText);
            Assert.Equal(workStage, model.WorkStage);
            Assert.Equal(payout, model.TodayExpectedPayout);
            Assert.Equal(workspaceCallsBeforeNavigation, workspaceCalls);
            Assert.Equal(routeCallsBeforeNavigation, routeCalls);
            Assert.Equal(workStatusCallsBeforeNavigation, api.WorkStatusCalls);
            Assert.True(model.IsAuthenticated);
            Assert.Equal(0, session.ClearCount);
            Assert.Equal("selected-delivery", Assert.Single(model.RecommendedTicketItems).TicketId);
        }
        finally
        {
            await model.StopMonitoringAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticationLoss_ResetsWorkspaceAndPrivateScrollHistory(bool deviceRemovalFails)
    {
        var session = new FDriverTestSession
        {
            ClearFailure = deviceRemovalFails ? new InvalidOperationException("test storage removal failure") : null
        };
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        try
        {
            await model.InitializeAsync();
            foreach (var section in Enum.GetValues<FDriverWorkspaceSection>())
                model.Navigation.RememberScroll(section, 330d);
            model.Navigation.Select(FDriverWorkspaceSection.Profile);
            api.Workspace = _ => Task.FromException<FoodDeliveryDriverWorkspaceDto>(
                new FDriverApiException("test unauthorized", HttpStatusCode.Unauthorized));

            await FDriverLifecycleTestSupport.RefreshBackground(model);

            Assert.False(model.IsAuthenticated);
            Assert.Equal(1, session.ClearCount);
            Assert.True(model.Navigation.IsDelivery);
            Assert.Empty(model.RecommendedTicketItems);
            foreach (var section in Enum.GetValues<FDriverWorkspaceSection>())
                Assert.Equal(0d, model.Navigation.GetScrollY(section));
        }
        finally
        {
            await model.StopMonitoringAsync();
        }
    }

    [Fact]
    public async Task ExplicitLogout_DoesNotLeaveTheNextLoginInAnAuxiliaryWorkspace()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        try
        {
            await model.InitializeAsync();
            model.Navigation.Select(FDriverWorkspaceSection.Settlement);
            model.Navigation.RememberScroll(FDriverWorkspaceSection.Delivery, 300d);
            model.Navigation.RememberScroll(FDriverWorkspaceSection.Settlement, 700d);

            await model.LogoutCommand.ExecuteAsync(null);

            Assert.False(model.IsAuthenticated);
            Assert.True(model.Navigation.IsDelivery);
            Assert.Equal(0d, model.Navigation.GetScrollY(FDriverWorkspaceSection.Delivery));
            Assert.Equal(0d, model.Navigation.GetScrollY(FDriverWorkspaceSection.Settlement));

            model.LoginId = "test-driver";
            model.Password = "test-password";
            await model.LoginCommand.ExecuteAsync(null);

            Assert.True(model.IsAuthenticated);
            Assert.True(model.Navigation.IsDelivery);
            Assert.Equal("before", Assert.Single(model.RecommendedTicketItems).TicketId);
        }
        finally
        {
            await model.StopMonitoringAsync();
        }
    }
}
