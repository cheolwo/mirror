using System.Net;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverWorkspaceRefreshRecoveryTests
{
    [Theory]
    [InlineData(false, "pickup")]
    [InlineData(true, "pickup")]
    [InlineData(false, "complete")]
    [InlineData(true, "complete")]
    public async Task FailedLatestRead_PreservesWorkAndBlocksOldActionsUntilCanonicalRecovery(bool background, string action)
    {
        var workspace = Workspace(action);
        var api = Api(workspace);
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        var ticket = model.SelectedTicket!;
        var bundle = model.BundleCandidateItems.Single();
        Assert.True(model.CanAcceptSelectedTicket);
        Assert.True(action == "pickup" ? model.CanConfirmPickup : model.CanCompleteDelivery);
        var changes = new List<string?>();
        model.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        api.Workspace = _ => throw new FDriverApiException("offline", HttpStatusCode.ServiceUnavailable);
        if (background) await FDriverLifecycleTestSupport.RefreshBackground(model);
        else await model.RefreshCommand.ExecuteAsync(null);

        Assert.True(model.IsAuthenticated);
        Assert.Equal("active", model.ActiveDelivery?.OfferId);
        Assert.Equal(ticket.TicketId, model.SelectedTicket?.TicketId);
        Assert.True(model.HasWorkspaceWarning);
        Assert.False(model.CanConfirmPickup);
        Assert.False(model.CanCompleteDelivery);
        Assert.False(model.CanAcceptSelectedTicket);
        Assert.False(model.CanOpenInterruption);
        Assert.False(model.CanToggleDispatchIntent);
        Assert.False(model.AcceptTicketCommand.CanExecute(ticket));
        Assert.False(model.RejectTicketCommand.CanExecute(ticket));
        Assert.False(model.AcceptBundleCommand.CanExecute(bundle));
        Assert.Contains(nameof(MainPageModel.CanConfirmPickup), changes);
        Assert.Contains(nameof(MainPageModel.CanCompleteDelivery), changes);

        var notice = model.StatusMessage;
        // Execute directly as well: stale UI callbacks must not bypass the state guard.
        await model.AcceptTicketCommand.ExecuteAsync(ticket);
        await model.RejectTicketCommand.ExecuteAsync(ticket);
        await model.AcceptBundleCommand.ExecuteAsync(bundle);
        await model.AcceptSelectedTicketCommand.ExecuteAsync(null);
        await model.ConfirmPickupCommand.ExecuteAsync(null);
        await model.CompleteDeliveryCommand.ExecuteAsync(null);
        Assert.Empty(api.DeliveryActionCalls);
        Assert.Equal(notice, model.StatusMessage);

        api.Workspace = _ => Task.FromResult(workspace);
        await model.RefreshCommand.ExecuteAsync(null);
        Assert.False(model.HasWorkspaceWarning);
        Assert.True(model.CanAcceptSelectedTicket);
        Assert.True(model.AcceptTicketCommand.CanExecute(model.SelectedTicket));
        Assert.True(model.RejectTicketCommand.CanExecute(model.SelectedTicket));
        Assert.True(model.AcceptBundleCommand.CanExecute(model.BundleCandidateItems.Single()));
        Assert.True(action == "pickup" ? model.CanConfirmPickup : model.CanCompleteDelivery);
        await Execute(model, action, ticket, bundle);
        Assert.Equal((action, "active"), Assert.Single(api.DeliveryActionCalls));
        await model.StopMonitoringAsync();
    }

    [Theory]
    [InlineData("accept", false)]
    [InlineData("reject", false)]
    [InlineData("bundle", false)]
    [InlineData("pickup", false)]
    [InlineData("complete", false)]
    [InlineData("accept", true)]
    [InlineData("reject", true)]
    [InlineData("bundle", true)]
    [InlineData("pickup", true)]
    [InlineData("complete", true)]
    public async Task LostActionResponse_ReadsCanonicalWithoutRepeatingPostAndBlocksIfReadAlsoFails(string action, bool readFails)
    {
        var initial = Workspace(action);
        var current = new FoodDeliveryDriverWorkspaceDto
        {
            DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3,
            ActiveDeliveries = [new() { OfferId = "next", WorkStatus = DriverWorkOfferStatus.MovingToPickup }]
        };
        var api = Api(initial);
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        var ticket = model.SelectedTicket!;
        var bundle = model.BundleCandidateItems.Single();
        var reads = 0;
        api.Workspace = _ =>
        {
            reads++;
            return readFails ? throw new FDriverApiException("offline", HttpStatusCode.ServiceUnavailable)
                : Task.FromResult(current);
        };
        api.DeliveryAction = (_, _, _) => throw new FDriverApiException("lost response", null);

        await Execute(model, action, ticket, bundle);
        Assert.Single(api.DeliveryActionCalls);
        Assert.Equal(1, reads);
        Assert.True(model.IsAuthenticated);
        if (readFails)
        {
            Assert.Equal("active", model.ActiveDelivery?.OfferId);
            Assert.True(model.HasWorkspaceWarning);
            await Execute(model, action, ticket, bundle);
            Assert.Single(api.DeliveryActionCalls);
            Assert.Equal(1, reads);
            api.Workspace = _ => Task.FromResult(current);
            await model.RefreshCommand.ExecuteAsync(null);
        }
        Assert.Equal("next", model.ActiveDelivery?.OfferId);
        Assert.False(model.HasWorkspaceWarning);
        Assert.Single(api.DeliveryActionCalls);
        Assert.False(model.CanConfirmPickup);
        Assert.False(model.CanCompleteDelivery);
        await model.StopMonitoringAsync();
    }

    private static Task Execute(MainPageModel model, string action, DeliveryTicketPreview ticket, FoodDeliveryBundlePreview bundle)
        => action switch
        {
            "accept" => model.AcceptTicketCommand.ExecuteAsync(ticket),
            "reject" => model.RejectTicketCommand.ExecuteAsync(ticket),
            "bundle" => model.AcceptBundleCommand.ExecuteAsync(bundle),
            "pickup" => model.ConfirmPickupCommand.ExecuteAsync(null),
            "complete" => model.CompleteDeliveryCommand.ExecuteAsync(null),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

    private static FDriverTestWorkspaceApi Api(FoodDeliveryDriverWorkspaceDto workspace) => new()
    {
        Workspace = _ => Task.FromResult(workspace),
        Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On })
    };

    private static FoodDeliveryDriverWorkspaceDto Workspace(string action) => new()
    {
        DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3,
        ActiveDeliveries = [new()
        {
            OfferId = "active", DeliveryAttemptId = "attempt", AttemptRevision = 7,
            WorkStatus = action == "pickup" ? DriverWorkOfferStatus.MovingToPickup : DriverWorkOfferStatus.MovingToDropoff,
            AvailableActions = [new() { ActionId = action == "pickup" ? 음식배달가능행동Ids.기사픽업확인 : 음식배달가능행동Ids.기사전달완료 },
                new() { ActionId = 음식배달가능행동Ids.기사배달중단 }]
        }],
        Recommendations = [new()
        {
            OfferId = "ticket", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(3),
            AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사제안수락 }, new() { ActionId = 음식배달가능행동Ids.기사제안거절 }]
        }],
        BundleCandidates = [new() { BundleId = "bundle", OfferIds = ["ticket"] }]
    };
}
