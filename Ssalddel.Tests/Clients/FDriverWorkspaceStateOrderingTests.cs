using System.Net;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverWorkspaceStateOrderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulCanonicalRead_ClearsOnlyItsPreviousRefreshFailure(bool backgroundFailure)
    {
        var current = Workspace("current", complete: false);
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(current) };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        api.Workspace = _ => throw new FDriverApiException("Local verification request failed.", HttpStatusCode.ServiceUnavailable);
        if (backgroundFailure) await FDriverLifecycleTestSupport.RefreshBackground(model);
        else await model.RefreshCommand.ExecuteAsync(null);
        Assert.Contains("배달 상태를 불러오지 못했습니다", model.StatusMessage);
        Assert.DoesNotContain("Local verification", model.StatusMessage);
        Assert.DoesNotContain("Local verification", model.WorkspaceSyncText);

        api.Workspace = _ => Task.FromResult(current);
        await model.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("current", model.ActiveDelivery?.OfferId);
        Assert.Equal("현재 배달을 확인해 주세요.", model.StatusMessage);
        Assert.False(model.HasWorkspaceWarning);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task SuccessfulCanonicalRead_PreservesNewerCommandNotice()
    {
        var current = Workspace("current", complete: false);
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(current) };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        api.Workspace = _ => throw new FDriverApiException("offline", HttpStatusCode.ServiceUnavailable);
        await model.RefreshCommand.ExecuteAsync(null);
        model.OpenCurrentDeliveryCommand.Execute(null);
        var commandNotice = model.StatusMessage;

        api.Workspace = _ => Task.FromResult(current);
        await model.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(commandNotice, model.StatusMessage);
        Assert.Contains("현재 단계", model.StatusMessage);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task OlderSuccessfulRead_CannotClearNewerRefreshFailure()
    {
        var current = Workspace("current", complete: false);
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(current) };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        var old = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Workspace = _ => { started.TrySetResult(); return old.Task; };
        var poll = FDriverLifecycleTestSupport.RefreshBackground(model);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        api.Workspace = _ => throw new FDriverApiException("new read failed", HttpStatusCode.ServiceUnavailable);
        await model.RefreshCommand.ExecuteAsync(null);
        var currentNotice = model.StatusMessage;
        old.SetResult(current);
        await poll;
        Assert.Equal(currentNotice, model.StatusMessage);
        Assert.Contains("배달 상태를 불러오지 못했습니다", model.StatusMessage);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task OldOwnerSuccessfulRead_CannotReplaceNewOwnerNotice()
    {
        var current = Workspace("current", complete: false);
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(current) };
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        api.Workspace = _ => throw new FDriverApiException("offline", HttpStatusCode.ServiceUnavailable);
        await model.RefreshCommand.ExecuteAsync(null);
        var old = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Workspace = _ => { started.TrySetResult(); return old.Task; };
        var poll = FDriverLifecycleTestSupport.RefreshBackground(model);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.ApplyAsync(new Ssalddel.Client.Infrastructure.Security.ClientAuthTokenSnapshot(
            "other-access", DateTime.UtcNow.AddHours(1), "other-refresh", DateTime.UtcNow.AddDays(1),
            "other-driver", "다른 기사", ["Driver"]));
        var newOwnerNotice = model.StatusMessage;
        old.SetResult(current);
        await poll;
        Assert.Equal(newOwnerNotice, model.StatusMessage);
        Assert.Contains("기사 계정이 변경", model.StatusMessage);
        Assert.Empty(model.ActiveDeliveryItems);
        await model.StopMonitoringAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OlderBackgroundRead_AfterCompletionCannotRestoreCompletedDeliveryOrItsError(bool failOldRead)
    {
        var previous = Workspace("completed", complete: true);
        var next = Workspace("next", complete: false);
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(previous) };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        Assert.True(model.CanCompleteDelivery);

        var old = new TaskCompletionSource<FoodDeliveryDriverWorkspaceDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Workspace = _ => { started.TrySetResult(); return old.Task; };
        var poll = FDriverLifecycleTestSupport.RefreshBackground(model);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        api.Workspace = _ => Task.FromResult(next);
        await model.CompleteDeliveryCommand.ExecuteAsync(null);
        Assert.Equal("next", model.ActiveDelivery?.OfferId);
        var currentStatus = model.StatusMessage;
        var currentSync = model.WorkspaceSyncText;

        if (failOldRead) old.SetException(new FDriverApiException("old request failed", HttpStatusCode.ServiceUnavailable));
        else old.SetResult(previous);
        await poll;

        Assert.Equal("next", model.ActiveDelivery?.OfferId);
        Assert.DoesNotContain(model.ActiveDeliveryItems, delivery => delivery.OfferId == "completed");
        Assert.False(model.CanCompleteDelivery);
        Assert.Equal(currentStatus, model.StatusMessage);
        Assert.Equal(currentSync, model.WorkspaceSyncText);
        Assert.False(model.HasWorkspaceWarning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OlderAvailabilityRead_CannotReplaceNewerDispatchIntentOrNextDelivery(bool failOldRead)
    {
        var previous = Workspace("completed", complete: true);
        var api = new FDriverTestWorkspaceApi { Workspace = _ => Task.FromResult(previous) };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        var old = new TaskCompletionSource<운영배차수신상태Dto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Availability = _ => { started.TrySetResult(); return old.Task; };
        var poll = FDriverLifecycleTestSupport.RefreshBackground(model);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        api.Workspace = _ => Task.FromResult(Workspace("next", complete: false));
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.Off });
        await model.CompleteDeliveryCommand.ExecuteAsync(null);
        var currentNotice = model.DispatchIntentNotice;
        if (failOldRead) old.SetException(new FDriverApiException("old availability failed", HttpStatusCode.ServiceUnavailable));
        else old.SetResult(new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On });
        await poll;
        Assert.Equal("next", model.ActiveDelivery?.OfferId);
        Assert.True(model.DispatchIntentKnown);
        Assert.False(model.ReceivesNewDispatches);
        Assert.Equal(currentNotice, model.DispatchIntentNotice);
    }

    private static FoodDeliveryDriverWorkspaceDto Workspace(string id, bool complete) => new()
    {
        DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3,
        ActiveDeliveries = [new()
        {
            OfferId = id, RestaurantName = "동네 식당", DeliveryAttemptId = id + "-attempt",
            WorkStatus = complete ? DriverWorkOfferStatus.MovingToDropoff : DriverWorkOfferStatus.MovingToPickup,
            AvailableActions = complete ? [new() { ActionId = 음식배달가능행동Ids.기사전달완료 }] : [],
            Pickup = new() { Label = "음식점", Address = "음식점 주소" },
            Dropoff = new() { Label = "전달지", Address = "전달지 주소" }
        }]
    };
}
