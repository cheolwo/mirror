using System.Net;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Mart;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;

namespace Ssalddel.Tests.Clients;

public sealed class WorkflowCompletionFoodTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FoodResult_RequiresMatchingReceiptAndFreshCanonicalRead_AndDismissDoesNotPost(bool final)
    {
        var api = FoodApi(final); var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        api.DeliveryAction = (_, _, _) =>
        {
            api.Workspace = _ => Task.FromResult(final ? Workspace() : Workspace(Delivery(true, 8)));
            return Task.FromResult(Receipt(final));
        };
        await Execute(model, final);
        var notice = Assert.IsType<WorkflowCompletionNotice>(model.Completion.Current);
        Assert.Equal(final, notice.IsFinal); Assert.True(model.Completion.IsDialog);
        Assert.Equal(final ? "전달을 완료했습니다" : "음식을 픽업했습니다", notice.Title);
        if (final) Assert.Contains("정산·지급", notice.NextStep);
        model.Completion.DismissCommand.Execute(null);
        Assert.Null(model.Completion.Current); Assert.Single(model.Completion.History);
        Assert.Single(api.DeliveryActionCalls);
        await FDriverLifecycleTestSupport.RefreshBackground(model);
        Assert.Null(model.Completion.Current); Assert.Single(api.DeliveryActionCalls);
        await model.StopMonitoringAsync();
    }

    [Theory]
    [InlineData("offer")]
    [InlineData("attempt")]
    [InlineData("revision")]
    [InlineData("equal-revision")]
    [InlineData("status")]
    [InlineData("missing")]
    public async Task FoodMismatchedOrIncompleteReceipt_DoesNotAnnounceCompletion(string mismatch)
    {
        var api = FoodApi(true); var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        var result = Receipt(true);
        switch (mismatch)
        {
            case "offer": result.OfferId = "other"; break;
            case "attempt": result.DeliveryAttemptId = "other"; break;
            case "revision": result.AttemptRevision = 6; break;
            case "equal-revision": result.AttemptRevision = 7; break;
            case "status": result.Status = DriverWorkOfferStatus.Accepted; break;
            case "missing": result.AttemptRevision = null; break;
        }
        api.Workspace = _ => Task.FromResult(Workspace());
        api.DeliveryAction = (_, _, _) => Task.FromResult(result);
        await model.CompleteDeliveryCommand.ExecuteAsync(null);
        Assert.Null(model.Completion.Current); Assert.Empty(model.Completion.History);
        Assert.Single(api.DeliveryActionCalls);
        await model.StopMonitoringAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostResponseOrFailedRead_DoesNotAnnounceOrRepeatPost(bool failRead)
    {
        var api = FoodApi(true); var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        api.Workspace = _ => failRead ? throw new FDriverApiException("offline", HttpStatusCode.ServiceUnavailable)
            : Task.FromResult(Workspace());
        api.DeliveryAction = (_, _, _) => failRead ? Task.FromResult(Receipt(true))
            : throw new FDriverApiException("lost response", null);
        await model.CompleteDeliveryCommand.ExecuteAsync(null);
        Assert.Null(model.Completion.Current); Assert.Empty(model.Completion.History);
        Assert.Single(api.DeliveryActionCalls);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task PickupReadWithOldState_DoesNotShowSuccess()
    {
        var api = FoodApi(false); var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        api.DeliveryAction = (_, _, _) => Task.FromResult(Receipt(false));
        await model.ConfirmPickupCommand.ExecuteAsync(null);
        Assert.Null(model.Completion.Current); Assert.Empty(model.Completion.History);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task AccountChangeDuringFoodCommand_CannotDisplayOldAccountCompletion()
    {
        var auth = new FDriverTestSession(); var api = FoodApi(true);
        var model = FDriverLifecycleTestSupport.Model(auth, api); await model.InitializeAsync();
        api.DeliveryAction = async (_, _, _) => { await auth.ClearAsync(); return Receipt(true); };
        await model.CompleteDeliveryCommand.ExecuteAsync(null);
        Assert.Null(model.Completion.Current); Assert.Empty(model.Completion.History);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public void NativeModes_AreCurrentScreenOnly_KeepHistoryAndDismissWithoutReplaying()
    {
        var state = new FDriverCompletionFeedbackState();
        var notice = new WorkflowCompletionNotice("one", "픽업 완료", "저장 확인", "다음 단계", false,
            WorkflowCompletionVisuals.FoodIconPath, "#2563EB");
        Assert.Equal(0, state.ModeIndex); Assert.False(state.HasNotice);
        state.ModeIndex = 1; Assert.True(state.Record(notice)); Assert.True(state.IsBrief); Assert.False(state.IsDialog);
        state.ModeIndex = 2; Assert.False(state.HasNotice); Assert.Single(state.History);
        Assert.True(state.Record(notice with { Key = "two" })); Assert.False(state.HasNotice);
        Assert.True(state.Record(notice with { Key = "three", IsFinal = true })); Assert.True(state.IsDialog);
        state.Dismiss(); Assert.False(state.Record(notice with { Key = "three", IsFinal = true }));
        state.Clear(); Assert.Empty(state.History); Assert.Equal(2, state.ModeIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MartReceiptAndCanonicalCompletion_ShowResult_WithoutAnotherCommand(bool final)
    {
        var api = new MartApi { Item = MartItem(final ? "PickedUp" : "Accepted", final ? 2 : 1) };
        var model = new 살뜰마트배송PageViewModel(api, new FDriverTestSession(), new MartStore());
        await model.ActivateAsync(); model.PickupCode = "1234";
        if (final) await model.DeliverCommand.ExecuteAsync(null); else await model.PickupCommand.ExecuteAsync(null);
        var notice = Assert.IsType<WorkflowCompletionNotice>(model.Completion.Current); Assert.Equal(final, notice.IsFinal);
        model.Completion.DismissCommand.Execute(null); Assert.Equal(1, api.Posts); Assert.False(model.HasPendingRequest);
        model.Deactivate();
    }

    [Fact]
    public async Task MartCanonicalOldState_KeepsPendingRequestWithoutFalseSuccessOrReplay()
    {
        var api = new MartApi { Item = MartItem("Accepted", 1), OldCanonical = true };
        var model = new 살뜰마트배송PageViewModel(api, new FDriverTestSession(), new MartStore());
        await model.ActivateAsync(); model.PickupCode = "1234"; await model.PickupCommand.ExecuteAsync(null);
        Assert.Null(model.Completion.Current); Assert.True(model.HasPendingRequest); Assert.False(model.CanRetry);
        Assert.Equal(1, api.Posts); model.Deactivate();
    }

    [Fact]
    public async Task MartDifferentCanonicalAttempt_DoesNotAnnounceAnotherAttemptCompletion()
    {
        var api = new MartApi { Item = MartItem("Accepted", 1), DifferentCanonicalAttempt = true };
        var model = new 살뜰마트배송PageViewModel(api, new FDriverTestSession(), new MartStore());
        await model.ActivateAsync(); model.PickupCode = "1234"; await model.PickupCommand.ExecuteAsync(null);
        Assert.Null(model.Completion.Current); Assert.Empty(model.Completion.History);
        Assert.True(model.HasPendingRequest); Assert.False(model.CanRetry); Assert.Equal(1, api.Posts);
        model.Deactivate();
    }

    private static Task Execute(MainPageModel model, bool final) => final
        ? model.CompleteDeliveryCommand.ExecuteAsync(null) : model.ConfirmPickupCommand.ExecuteAsync(null);
    private static FDriverTestWorkspaceApi FoodApi(bool final) => new() { Workspace = _ => Task.FromResult(Workspace(Delivery(final, 7))) };
    private static FoodDeliveryDriverActionResponse Receipt(bool final) => new()
    {
        OfferId = "offer", DeliveryAttemptId = "attempt", AttemptRevision = 8,
        Status = final ? DriverWorkOfferStatus.Completed : DriverWorkOfferStatus.MovingToDropoff, Message = "처리했습니다."
    };
    private static FoodDeliveryDriverActiveDeliveryDto Delivery(bool final, long revision) => new()
    {
        OfferId = "offer", DeliveryAttemptId = "attempt", AttemptRevision = revision,
        WorkStatus = final ? DriverWorkOfferStatus.MovingToDropoff : DriverWorkOfferStatus.MovingToPickup,
        RestaurantArrivedAtUtc = DateTime.UtcNow.AddMinutes(-1),
        AvailableActions = [new() { ActionId = final ? 음식배달가능행동Ids.기사전달완료 : 음식배달가능행동Ids.기사픽업확인 }]
    };
    private static FoodDeliveryDriverWorkspaceDto Workspace(params FoodDeliveryDriverActiveDeliveryDto[] active) => new()
    { DriverId = "test-driver", ActiveDeliveries = active, MaxActiveDeliveries = 3, UpdatedAtUtc = DateTime.UtcNow };
    private static 마트기사업무항목Dto MartItem(string state, long revision) => new()
    {
        OfferId = "mart-offer", AttemptStableId = "mart-attempt", StateCode = state, Revision = revision,
        CanPickup = state == "Accepted", CanDeliver = state == "PickedUp", LabelRevision = 7
    };
    private sealed class MartStore : I살뜰마트배송PendingStore
    {
        private 살뜰마트배송미확인요청? item;
        public Task<살뜰마트배송미확인요청?> LoadAsync(CancellationToken token = default) => Task.FromResult(item);
        public Task SaveAsync(살뜰마트배송미확인요청 request, CancellationToken token = default) { item = request; return Task.CompletedTask; }
        public Task ClearAsync(string owner, CancellationToken token = default) { item = null; return Task.CompletedTask; }
    }
    private sealed class MartApi : I살뜰마트배송ApiClient
    {
        public 마트기사업무항목Dto Item { get; set; } = MartItem("Accepted", 1);
        public bool OldCanonical { get; set; }
        public bool DifferentCanonicalAttempt { get; set; }
        public int Posts { get; private set; }
        public Task<마트기사업무목록Dto> ListAsync(CancellationToken token = default) => Task.FromResult(new 마트기사업무목록Dto { Enabled = true, Items = [Item] });
        public Task<마트기사업무항목Dto> GetAsync(string offerId, CancellationToken token = default) => Task.FromResult(Item);
        public Task<마트기사업무항목Dto> ResultAsync(Guid requestId, CancellationToken token = default) => throw new FDriverApiException("not found", HttpStatusCode.NotFound);
        public Task<마트기사업무항목Dto> AcceptAsync(string id, 마트기사수락요청 request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<마트기사업무항목Dto> RejectAsync(string id, 마트기사거절요청 request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<마트기사업무항목Dto> PickupAsync(string id, 마트기사픽업요청 request, CancellationToken token = default) => Post("PickedUp", 2);
        public Task<마트기사업무항목Dto> DeliverAsync(string id, 마트기사전달요청 request, CancellationToken token = default) => Post("Delivered", 3);
        private Task<마트기사업무항목Dto> Post(string state, long revision)
        {
            Posts++; var response = MartItem(state, revision);
            if (!OldCanonical)
            {
                Item = DifferentCanonicalAttempt ? MartItem(state, revision) : response;
                if (DifferentCanonicalAttempt) Item.AttemptStableId = "another-attempt";
            }
            return Task.FromResult(response);
        }
    }
}
