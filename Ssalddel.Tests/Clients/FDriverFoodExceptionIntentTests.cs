using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverFoodExceptionIntentTests
{
    [Fact]
    public void ActiveProjection_PreservesAttemptAndPreparationEvidence()
    {
        var source = Active();
        source.RestaurantArrivedAtUtc = DateTime.UtcNow.AddMinutes(-20);
        source.DisplayedPreparationReadyAtUtc = DateTime.UtcNow.AddMinutes(-12);
        source.PreparationDelayEligibleAtUtc = DateTime.UtcNow.AddMinutes(-2);
        source.IsPreparationDelayRedispatch = true;
        var display = ActiveDeliveryPreview.From(source);
        Assert.Equal(source.DeliveryAttemptId, display.DeliveryAttemptId);
        Assert.Equal(source.AttemptRevision, display.AttemptRevision);
        Assert.Equal(source.RestaurantArrivedAtUtc, display.RestaurantArrivedAtUtc);
        Assert.Equal(source.DisplayedPreparationReadyAtUtc, display.DisplayedPreparationReadyAtUtc);
        Assert.Equal(source.PreparationDelayEligibleAtUtc, display.PreparationDelayEligibleAtUtc);
        Assert.True(display.IsPreparationDelayRedispatch);
    }

    [Fact]
    public void InterruptionDraft_ReusesRequestIdRevisionAndOriginalPayloadAfterResponseLoss()
    {
        var state = new FDriverDeliveryExceptionState();
        state.Open(ActiveDeliveryPreview.From(Active()));
        state.SelectedReason = state.Reasons.Single(x => x.Code == 음식배달중단사유Code.사고);
        state.Memo = "안전한 곳에서 대기";
        var first = state.Prepare(DateTime.UtcNow)!;
        state.Memo = "변경해도 전송 초안은 고정";
        state.SelectedReason = state.Reasons.Last();
        first.사유Code = "caller mutation";
        var retry = state.Prepare(DateTime.UtcNow)!;
        Assert.Equal(first.클라이언트요청Id, retry.클라이언트요청Id);
        Assert.Equal(7, retry.예상시도Revision);
        Assert.Equal(음식배달중단사유Code.사고, retry.사유Code);
        Assert.Equal("안전한 곳에서 대기", retry.메모);
        Assert.False(state.IsInputUnlocked);
    }

    [Fact]
    public void PreparationDelay_CannotUseMissingArrivalOrFutureServerThreshold()
    {
        var state = new FDriverDeliveryExceptionState();
        var delivery = Active();
        delivery.PreparationDelayEligibleAtUtc = DateTime.UtcNow.AddMinutes(1);
        state.Open(ActiveDeliveryPreview.From(delivery));
        state.SelectedReason = state.Reasons.Single(x => x.Code == 음식배달중단사유Code.조리지연);
        Assert.Null(state.Prepare(DateTime.UtcNow));
        Assert.False(state.HasPendingRequest);
        delivery.RestaurantArrivedAtUtc = DateTime.UtcNow.AddMinutes(-2);
        state.Reconcile(ActiveDeliveryPreview.From(delivery));
        Assert.Null(state.Prepare(DateTime.UtcNow));
        delivery.PreparationDelayEligibleAtUtc = DateTime.UtcNow.AddMinutes(-1);
        state.Reconcile(ActiveDeliveryPreview.From(delivery));
        Assert.NotNull(state.Prepare(DateTime.UtcNow));
    }

    [Fact]
    public void CanonicalRevisionChange_UnlocksDraft_AndRemovedAttemptClearsPrivateInput()
    {
        var state = new FDriverDeliveryExceptionState();
        var delivery = Active();
        state.Open(ActiveDeliveryPreview.From(delivery));
        state.SelectedReason = state.Reasons.First();
        Assert.NotNull(state.Prepare(DateTime.UtcNow));
        delivery.AttemptRevision++;
        state.Reconcile(ActiveDeliveryPreview.From(delivery));
        Assert.True(state.IsInputUnlocked);
        Assert.Contains("상태가 변경", state.Notice);
        state.Reconcile(null);
        Assert.False(state.IsOpen);
        Assert.Null(state.Delivery);
        Assert.Empty(state.Memo);
    }

    [Fact]
    public async Task Arrival_ResponseLoss_RequeriesAndRetriesSameCommandId()
    {
        var active = Active();
        var api = Api(active);
        var requests = new List<음식배달가게도착요청>();
        api.Arrival = (_, request, _) =>
        {
            requests.Add(request);
            if (requests.Count == 1) throw new FDriverApiException("응답 유실", null);
            active.AttemptRevision++; active.RestaurantArrivedAtUtc = DateTime.UtcNow;
            active.AvailableActions = [Action(음식배달가능행동Ids.기사배달중단)];
            return Task.FromResult(new FoodDeliveryDriverActionResponse());
        };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        await model.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        Assert.True(model.CanRecordArrival);
        await model.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        Assert.Equal(2, requests.Count);
        Assert.Equal(requests[0].클라이언트요청Id, requests[1].클라이언트요청Id);
        Assert.Equal(7, requests[1].예상시도Revision);
        Assert.False(model.CanRecordArrival);
        Assert.NotNull(model.ActiveDelivery!.RestaurantArrivedAtUtc);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task Interruption_ConflictRequeriesCanonicalRevision_BeforeNewDraft()
    {
        var active = Active();
        var api = Api(active);
        api.Interruption = (_, _, _) =>
        {
            active.AttemptRevision = 8;
            throw new FDriverApiException("상태 충돌", HttpStatusCode.Conflict);
        };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        model.OpenInterruptionCommand.Execute(null);
        model.ExceptionEditor.SelectedReason = model.ExceptionEditor.Reasons.First();
        await model.SubmitInterruptionCommand.ExecuteAsync(null);
        Assert.Equal(8, model.ExceptionEditor.Delivery!.AttemptRevision);
        Assert.True(model.CanEditInterruption);
        Assert.Equal(0, api.StopWorkCalls);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task Interruption_ResponseLostAfterMutation_RemovesStaleEditorByCanonicalWorkspace()
    {
        var active = Active();
        var api = Api(active);
        api.Interruption = (_, _, _) =>
        {
            api.Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto());
            throw new FDriverApiException("응답 유실", null);
        };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        model.OpenInterruptionCommand.Execute(null);
        model.ExceptionEditor.SelectedReason = model.ExceptionEditor.Reasons.First();
        await model.SubmitInterruptionCommand.ExecuteAsync(null);
        Assert.False(model.ExceptionEditor.IsOpen);
        Assert.Null(model.ActiveDelivery);
        Assert.True(model.IsRegularWorkspaceVisible);
        Assert.Equal(0, api.StopWorkCalls);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task PendingInterruption_LocksInputAndFulfillment_AndPreservesSameRetry()
    {
        var api = Api(Active());
        var ids = new List<Guid>();
        api.Interruption = (_, request, _) => { ids.Add(request.클라이언트요청Id); throw new FDriverApiException("응답 유실", null); };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        model.OpenInterruptionCommand.Execute(null);
        model.ExceptionEditor.SelectedReason = model.ExceptionEditor.Reasons.First();
        await model.SubmitInterruptionCommand.ExecuteAsync(null);
        Assert.False(model.CanEditInterruption);
        Assert.False(model.CanConfirmPickup);
        Assert.False(model.IsRegularWorkspaceVisible);
        model.CloseInterruptionCommand.Execute(null);
        Assert.True(model.IsRegularWorkspaceVisible);
        Assert.False(model.CanConfirmPickup);
        Assert.False(model.CanRecordArrival);
        model.OpenInterruptionCommand.Execute(null);
        await model.SubmitInterruptionCommand.ExecuteAsync(null);
        Assert.Equal(ids[0], ids[1]);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task Arrival_LateSuccessAfterExit_CannotReplaceWorkspaceOrStartAnotherAction()
    {
        var api = Api(Active());
        var late = new TaskCompletionSource<FoodDeliveryDriverActionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken commandToken = default;
        api.Arrival = (_, _, token) => { commandToken = token; return late.Task; };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        var command = model.RecordRestaurantArrivalCommand.ExecuteAsync(null);
        await model.StopMonitoringAsync();
        Assert.True(commandToken.IsCancellationRequested);
        late.SetResult(new() { Message = "late" });
        await command;
        Assert.NotEqual("late", model.StatusMessage);
        Assert.Equal(7, model.ActiveDelivery!.AttemptRevision);
    }

    [Fact]
    public async Task FinalUnauthorized_ClearsExceptionDraftAndAuth()
    {
        var api = Api(Active());
        api.Interruption = (_, _, _) => throw new FDriverApiException("다시 로그인", HttpStatusCode.Unauthorized);
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        model.OpenInterruptionCommand.Execute(null);
        model.ExceptionEditor.SelectedReason = model.ExceptionEditor.Reasons.First();
        model.ExceptionEditor.Memo = "private draft";
        await model.SubmitInterruptionCommand.ExecuteAsync(null);
        Assert.False(model.IsAuthenticated);
        Assert.False(model.ExceptionEditor.IsOpen);
        Assert.Empty(model.ExceptionEditor.Memo);
        Assert.False(model.DispatchIntentKnown);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task DispatchOff_DoesNotStopWork_AndCurrentDeliveryAndLocationContinue()
    {
        var active = Active();
        var api = Api(active);
        var intent = 운영배차수신의사Code.On;
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = intent });
        api.ChangeIntent = (request, _) => { intent = request.수신의사Code; return Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = intent }); };
        var session = new FDriverTestSession();
        using var http = new HttpClient(new FDriverTestHttpHandler((_, _) => Task.FromResult(FDriverTestHttpHandler.TokenResponse()))) { BaseAddress = new("http://localhost/") };
        var model = new MainPageModel(new(), session, new(http, session), api, new Location(), new());
        await model.InitializeAsync();
        model.IsOnDuty = true;
        await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
        Assert.False(model.ReceivesNewDispatches);
        Assert.True(model.IsOnDuty);
        Assert.Equal(active.OfferId, model.ActiveDelivery!.OfferId);
        Assert.Equal(0, api.StopWorkCalls);
        await FDriverLifecycleTestSupport.RefreshBackground(model);
        Assert.True(api.LocationCalls > 0);
        Assert.True(model.CanRecordArrival);
        Assert.False(model.CanToggleWork);
        await model.ToggleWorkCommand.ExecuteAsync(null);
        Assert.Equal(0, api.StopWorkCalls);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task DispatchIntent_ResponseLoss_RetriesSameIdWithoutAutomaticallyEnablingOnStart()
    {
        var api = Api(Active());
        var requests = new List<운영배차수신의사변경요청>();
        api.ChangeIntent = (request, _) => { requests.Add(request); throw new FDriverApiException("응답 유실", null); };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        Assert.False(model.ReceivesNewDispatches);
        await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
        await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
        Assert.Equal(2, requests.Count);
        Assert.Equal(requests[0].클라이언트요청Id, requests[1].클라이언트요청Id);
        Assert.All(requests, x => Assert.Equal(운영배차수신의사Code.On, x.수신의사Code));
        Assert.False(model.ReceivesNewDispatches);
        Assert.Equal(0, api.StopWorkCalls);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task MissingAvailableAction_DisablesArrivalAndInterruption()
    {
        var active = Active(); active.AvailableActions = [];
        var model = FDriverLifecycleTestSupport.Model(new(), Api(active));
        await model.InitializeAsync();
        Assert.False(model.CanRecordArrival);
        Assert.False(model.CanOpenInterruption);
        model.OpenInterruptionCommand.Execute(null);
        Assert.False(model.ExceptionEditor.IsOpen);
        await model.StopMonitoringAsync();
    }

    [Theory]
    [InlineData("arrival", "POST", "/api/v1/driver/food-deliveries/offers/a%2Fb/restaurant-arrival")]
    [InlineData("interruption", "POST", "/api/v1/driver/food-deliveries/offers/a%2Fb/interruption")]
    [InlineData("intent", "PUT", "/api/v1/driver/operational-dispatch/availability/intent")]
    public async Task Api_UsesExistingContractRouteAndPreservesCommandId(string action, string method, string path)
    {
        var id = Guid.NewGuid(); string? sent = null; string? sentPath = null; string? sentMethod = null;
        var handler = new FDriverTestHttpHandler(async (request, _) =>
        {
            sentPath = request.RequestUri!.AbsolutePath; sentMethod = request.Method.Method;
            sent = await request.Content!.ReadAsStringAsync();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new FoodDeliveryDriverActionResponse()) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        var session = new FDriverTestSession(); var service = new FoodDeliveryDriverApiService(http, session, new(http, session));
        if (action == "arrival") await service.RecordRestaurantArrivalAsync("a/b", new() { 클라이언트요청Id = id, 예상시도Revision = 7 });
        if (action == "interruption") await service.InterruptAsync("a/b", new() { 클라이언트요청Id = id, 예상시도Revision = 7, 사유Code = 음식배달중단사유Code.사고 });
        if (action == "intent") await service.ChangeDispatchIntentAsync(new() { 클라이언트요청Id = id, 수신의사Code = 운영배차수신의사Code.Off });
        Assert.Equal(method, sentMethod); Assert.Equal(path, sentPath);
        using var json = JsonDocument.Parse(sent!);
        Assert.Equal(id, json.RootElement.GetProperty("클라이언트요청Id").GetGuid());
    }

    private static 업무가능행동Dto Action(string id) => new() { ActionId = id, ExpectedRevision = 7 };
    private static FoodDeliveryDriverActiveDeliveryDto Active() => new()
    {
        OfferId = "offer-1", DeliveryAttemptId = "attempt-1", AttemptRevision = 7,
        RestaurantName = "검증 음식점", WorkStatus = DriverWorkOfferStatus.MovingToPickup,
        AvailableActions = [Action(음식배달가능행동Ids.기사가게도착), Action(음식배달가능행동Ids.기사배달중단), Action(음식배달가능행동Ids.기사픽업확인)]
    };
    private static FDriverTestWorkspaceApi Api(FoodDeliveryDriverActiveDeliveryDto active) => new()
    { Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto { ActiveDeliveries = [active], UpdatedAtUtc = DateTime.UtcNow }) };
    private sealed class Location : IFDriverLocationService
    {
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<FDriverLocationSnapshot?>(new(37.5m, 127m, 10, DateTime.UtcNow));
    }
}
