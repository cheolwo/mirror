using System.Net;
using System.Text.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverPendingDurableRecoveryTests
{
    [Fact]
    public async Task Arrival_RestartReadsCanonicalThenExplicitRetryUsesOriginalIdAndRevision()
    {
        var active = Active(); var api = Api(active); var store = new PendingStore();
        var requests = new List<음식배달가게도착요청>();
        api.Arrival = (_, request, _) => { requests.Add(request); throw new FDriverApiException("lost", null); };
        var first = Model(api, store); await first.InitializeAsync();
        await first.RecordRestaurantArrivalCommand.ExecuteAsync(null); await first.StopMonitoringAsync();
        active.AttemptRevision = 8;
        var restarted = Model(api, store); await restarted.InitializeAsync();
        Assert.Single(requests);
        await restarted.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        Assert.Equal(2, requests.Count);
        Assert.Equal(requests[0].클라이언트요청Id, requests[1].클라이언트요청Id);
        Assert.Equal(7, requests[1].예상시도Revision);
        await restarted.StopMonitoringAsync();
    }

    [Fact]
    public async Task AppliedArrival_RestartConfirmsWithoutPostingAgain()
    {
        var active = Active(); var api = Api(active); var store = new PendingStore(); var calls = 0;
        api.Arrival = (_, _, _) => { calls++; active.RestaurantArrivedAtUtc = DateTime.UtcNow; throw new FDriverApiException("lost", null); };
        var first = Model(api, store); await first.InitializeAsync();
        // The POST committed, but its follow-up GET was also lost.
        var read = api.Workspace;
        api.Arrival = (_, _, _) => { calls++; active.RestaurantArrivedAtUtc = DateTime.UtcNow; api.Workspace = _ => throw new FDriverApiException("offline", null); throw new FDriverApiException("lost", null); };
        await first.RecordRestaurantArrivalCommand.ExecuteAsync(null); await first.StopMonitoringAsync();
        api.Workspace = _ => throw new FDriverApiException("Local verification request failed.", HttpStatusCode.ServiceUnavailable);
        var restarted = Model(api, store); await restarted.InitializeAsync();
        Assert.Equal(1, calls);
        Assert.NotNull((await store.LoadAsync())!.Arrival);
        Assert.Contains("배달 상태를 불러오지 못했습니다", restarted.StatusMessage);
        api.Workspace = read;
        await restarted.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(1, calls);
        Assert.Null(await store.LoadAsync());
        Assert.Equal("현재 배달을 확인해 주세요.", restarted.StatusMessage);
        await restarted.StopMonitoringAsync();
    }

    [Fact]
    public async Task Interruption_RestartPreservesMemoReasonRevisionAndLocksInputUntilExplicitRetry()
    {
        var api = Api(Active()); var store = new PendingStore(); var requests = new List<음식배달중단요청>();
        api.Interruption = (_, request, _) => { requests.Add(request); throw new FDriverApiException("lost", null); };
        var first = Model(api, store); await first.InitializeAsync();
        first.OpenInterruptionCommand.Execute(null);
        first.ExceptionEditor.SelectedReason = first.ExceptionEditor.Reasons.First();
        first.ExceptionEditor.Memo = "안전한 곳에서 대기";
        await first.SubmitInterruptionCommand.ExecuteAsync(null); await first.StopMonitoringAsync();
        var restarted = Model(api, store); await restarted.InitializeAsync();
        Assert.Single(requests);
        Assert.True(restarted.ExceptionEditor.IsOpen);
        Assert.False(restarted.ExceptionEditor.IsInputUnlocked);
        Assert.Equal("안전한 곳에서 대기", restarted.ExceptionEditor.Memo);
        restarted.ExceptionEditor.Memo = "입력이 바뀌어도 원래 요청 유지";
        await restarted.SubmitInterruptionCommand.ExecuteAsync(null);
        Assert.Equal(requests[0].클라이언트요청Id, requests[1].클라이언트요청Id);
        Assert.Equal(requests[0].예상시도Revision, requests[1].예상시도Revision);
        Assert.Equal(requests[0].사유Code, requests[1].사유Code);
        Assert.Equal("안전한 곳에서 대기", requests[1].메모);
        await restarted.StopMonitoringAsync();
    }

    [Fact]
    public async Task RemovedAttempt_RestartDoesNotReplayInterruptionOrExposeOldInput()
    {
        var api = Api(Active()); var store = new PendingStore(); var calls = 0;
        api.Interruption = (_, _, _) => { calls++; throw new FDriverApiException("lost", null); };
        var first = Model(api, store); await first.InitializeAsync(); first.OpenInterruptionCommand.Execute(null);
        first.ExceptionEditor.SelectedReason = first.ExceptionEditor.Reasons.First(); first.ExceptionEditor.Memo = "private memo";
        await first.SubmitInterruptionCommand.ExecuteAsync(null); await first.StopMonitoringAsync();
        api.Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto { DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow });
        var restarted = Model(api, store); await restarted.InitializeAsync();
        Assert.Equal(1, calls); Assert.False(restarted.ExceptionEditor.HasPendingRequest);
        Assert.Empty(restarted.ExceptionEditor.Memo); Assert.Null(await store.LoadAsync());
        await restarted.StopMonitoringAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Intent_RestartNeverPosts_AndOnlyUnappliedIntentCanExplicitlyRetry(bool applied)
    {
        var api = Api(Active()); var store = new PendingStore(); var requests = new List<운영배차수신의사변경요청>();
        var intent = 운영배차수신의사Code.Off;
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = intent });
        api.ChangeIntent = (request, _) => { requests.Add(request); throw new FDriverApiException("lost", null); };
        var first = Model(api, store); await first.InitializeAsync(); await first.ToggleDispatchIntentCommand.ExecuteAsync(null); await first.StopMonitoringAsync();
        if (applied) intent = 운영배차수신의사Code.On;
        var restarted = Model(api, store); await restarted.InitializeAsync(); Assert.Single(requests);
        if (applied) Assert.Null(await store.LoadAsync());
        else
        {
            await restarted.ToggleDispatchIntentCommand.ExecuteAsync(null);
            Assert.Equal(2, requests.Count);
            Assert.Equal(requests[0].클라이언트요청Id, requests[1].클라이언트요청Id);
            Assert.Equal(운영배차수신의사Code.On, requests[1].수신의사Code);
        }
        await restarted.StopMonitoringAsync();
    }

    [Theory]
    [InlineData("arrival")]
    [InlineData("intent")]
    [InlineData("interruption")]
    public async Task SaveFailure_PreventsEveryPendingWrite_AndDisablesFurtherActions(string operation)
    {
        var api = Api(Active()); var store = new PendingStore(); var writes = 0;
        api.Arrival = (_, _, _) => { writes++; return Success(); };
        api.Interruption = (_, _, _) => { writes++; return Success(); };
        api.ChangeIntent = (_, _) => { writes++; return Task.FromResult(new 운영배차수신상태Dto()); };
        var model = Model(api, store); await model.InitializeAsync(); store.SaveFailure = new IOException("unavailable");
        if (operation == "arrival") await model.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        else if (operation == "intent") await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
        else
        {
            model.OpenInterruptionCommand.Execute(null); model.ExceptionEditor.SelectedReason = model.ExceptionEditor.Reasons.First();
            await model.SubmitInterruptionCommand.ExecuteAsync(null);
        }
        Assert.Equal(0, writes); Assert.Contains("안전하게 저장", model.StatusMessage);
        Assert.False(model.CanRecordArrival); Assert.False(model.CanSubmitInterruption); Assert.False(model.CanToggleDispatchIntent);
        var storageNotice = model.StatusMessage;
        store.SaveFailure = null;
        await model.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(0, writes);
        Assert.Equal(storageNotice, model.StatusMessage);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task LoadFailure_DoesNotClearUncertainStorageOrQueryProtectedWorkspace()
    {
        var api = Api(Active()); var reads = 0; var store = new PendingStore { LoadFailure = new JsonException("corrupt") };
        api.Workspace = _ => { reads++; return Task.FromResult(new FoodDeliveryDriverWorkspaceDto()); };
        var model = Model(api, store); await model.InitializeAsync();
        Assert.Equal(0, reads); Assert.Equal(0, store.Clears);
        Assert.Contains("안전하게 저장", model.StatusMessage); Assert.False(model.CanRecordArrival);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task FailedCanonicalGet_BlocksExplicitArrivalRetryAndKeepsOriginalSnapshot()
    {
        var api = Api(Active()); var store = new PendingStore(); var writes = 0;
        api.Arrival = (_, _, _) => { writes++; throw new FDriverApiException("lost", null); };
        var model = Model(api, store); await model.InitializeAsync(); await model.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        api.Workspace = _ => throw new FDriverApiException("offline", null);
        await model.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        Assert.Equal(1, writes); Assert.NotNull((await store.LoadAsync())!.Arrival);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task FailedAvailabilityGet_BlocksUnconfirmedIntentRetry()
    {
        var api = Api(Active()); var store = new PendingStore(); var writes = 0;
        api.ChangeIntent = (_, _) => { writes++; throw new FDriverApiException("lost", null); };
        var model = Model(api, store); await model.InitializeAsync(); await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
        api.Availability = _ => throw new FDriverApiException("offline", null);
        await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
        Assert.Equal(1, writes); Assert.NotNull((await store.LoadAsync())!.DispatchIntent);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task ChangedOwnerDuringDelayedSave_DoesNotPostOrRestoreOldRequest()
    {
        var api = Api(Active()); var store = new PendingStore(); var session = new FDriverTestSession(); var writes = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.BeforeSave = async () => { entered.TrySetResult(); await release.Task; };
        api.Arrival = (_, _, _) => { writes++; return Success(); };
        var model = Model(api, store, session); await model.InitializeAsync();
        var command = model.RecordRestaurantArrivalCommand.ExecuteAsync(null); await entered.Task;
        await session.ApplyAsync(new ClientAuthTokenSnapshot("other-access", DateTime.UtcNow.AddHours(1), "other-refresh",
            DateTime.UtcNow.AddDays(1), "other-driver", "other", ["Driver"]));
        release.SetResult(); await command;
        Assert.Equal(0, writes); Assert.Null(await store.LoadAsync()); Assert.Empty(model.ExceptionEditor.Memo);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task ExplicitLogout_ClearsPendingIntentAndDoesNotClaimSuccessOnClearFailure()
    {
        var api = new FDriverTestWorkspaceApi(); var store = new PendingStore();
        api.ChangeIntent = (_, _) => throw new FDriverApiException("lost", null);
        var model = Model(api, store); await model.InitializeAsync(); await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
        store.ClearFailure = new IOException("unavailable");
        await model.LogoutCommand.ExecuteAsync(null);
        Assert.False(model.IsAuthenticated); Assert.Contains("안전하게 저장", model.StatusMessage);
        Assert.NotNull((await store.LoadAsync())!.DispatchIntent);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task FinalUnauthorized_HidesPrivateInputButSameOwnerRestartCanRecoverPending()
    {
        var api = Api(Active()); var store = new PendingStore(); var ids = new List<Guid>();
        api.Interruption = (_, request, _) => { ids.Add(request.클라이언트요청Id); throw new FDriverApiException("login", HttpStatusCode.Unauthorized); };
        var first = Model(api, store); await first.InitializeAsync(); first.OpenInterruptionCommand.Execute(null);
        first.ExceptionEditor.SelectedReason = first.ExceptionEditor.Reasons.First(); first.ExceptionEditor.Memo = "original";
        await first.SubmitInterruptionCommand.ExecuteAsync(null);
        Assert.False(first.IsAuthenticated); Assert.Empty(first.ExceptionEditor.Memo);
        api.Interruption = (_, request, _) => { ids.Add(request.클라이언트요청Id); throw new FDriverApiException("lost", null); };
        var restarted = Model(api, store); await restarted.InitializeAsync();
        Assert.Equal("original", restarted.ExceptionEditor.Memo); Assert.Single(ids);
        await restarted.SubmitInterruptionCommand.ExecuteAsync(null); Assert.Equal(ids[0], ids[1]);
        await restarted.StopMonitoringAsync();
    }

    private static MainPageModel Model(FDriverTestWorkspaceApi api, PendingStore store, FDriverTestSession? session = null)
    {
        session ??= new();
        var http = new HttpClient(new FDriverTestHttpHandler((_, _) => Task.FromResult(FDriverTestHttpHandler.TokenResponse()))) { BaseAddress = new("http://localhost/") };
        return new(new(), session, new(http, session), api, new FDriverTestLocationService(), new(), pendingOperationStore: store);
    }
    private static Task<FoodDeliveryDriverActionResponse> Success() => Task.FromResult(new FoodDeliveryDriverActionResponse());
    private static FoodDeliveryDriverActiveDeliveryDto Active() => new()
    {
        OfferId = "offer-1", DeliveryAttemptId = "attempt-1", AttemptRevision = 7, RestaurantName = "음식점",
        WorkStatus = DriverWorkOfferStatus.MovingToPickup,
        AvailableActions = [new 업무가능행동Dto { ActionId = 음식배달가능행동Ids.기사가게도착, ExpectedRevision = 7 },
            new 업무가능행동Dto { ActionId = 음식배달가능행동Ids.기사배달중단, ExpectedRevision = 7 }]
    };
    private static FDriverTestWorkspaceApi Api(FoodDeliveryDriverActiveDeliveryDto active) => new()
    { Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto { DriverId = "test-driver", ActiveDeliveries = [active], UpdatedAtUtc = DateTime.UtcNow }) };

    private sealed class PendingStore : IFDriverPendingOperationStore
    {
        private string? json;
        public Exception? LoadFailure, SaveFailure, ClearFailure;
        public Func<Task>? BeforeSave;
        public int Clears;
        public Task<FDriverPendingOperationsSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); if (LoadFailure is not null) throw LoadFailure;
            return Task.FromResult(json is null ? null : JsonSerializer.Deserialize<FDriverPendingOperationsSnapshot>(json));
        }
        public async Task SaveAsync(FDriverPendingOperationsSnapshot snapshot, CancellationToken cancellationToken = default)
        { if (BeforeSave is not null) await BeforeSave(); if (SaveFailure is not null) throw SaveFailure; json = JsonSerializer.Serialize(snapshot); }
        public async Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
        { if (ClearFailure is not null) throw ClearFailure; if ((await LoadAsync(cancellationToken))?.OwnerId == ownerId) { json = null; Clears++; } }
    }
}
