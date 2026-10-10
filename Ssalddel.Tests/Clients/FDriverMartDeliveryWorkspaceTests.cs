using System.Net;
using System.Text.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Mart;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverMartDeliveryWorkspaceTests
{
    [Fact]
    public async Task DisabledAndEmptyAreDistinct_AndNoCommandIsSent()
    {
        var (model, api, _, _) = Create();
        api.List = _ => Task.FromResult(new 마트기사업무목록Dto { Enabled = false, Notice = "검증 기능 꺼짐" });
        await model.ActivateAsync();
        Assert.False(model.IsEnabled); Assert.False(model.IsEmpty); Assert.Equal("검증 기능 꺼짐", model.Notice);
        await model.AcceptCommand.ExecuteAsync(null); Assert.Empty(api.Posts);
        api.List = _ => Task.FromResult(new 마트기사업무목록Dto { Enabled = true });
        await model.RefreshCommand.ExecuteAsync(null);
        Assert.True(model.IsEmpty); Assert.False(model.HasSelection);
    }

    [Fact]
    public async Task AcceptPickupDeliver_RequeriesCanonicalState_AndDoesNotClaimPayment()
    {
        var (model, api, _, store) = Create();
        await model.ActivateAsync();
        Assert.True(model.CanAccept); Assert.False(model.CanDeliver);
        Assert.DoesNotContain("전달 상세 주소", model.DropoffText);
        await model.AcceptCommand.ExecuteAsync(null);
        Assert.True(model.SelectedItem!.Value.CanPickup); Assert.Equal(1, api.Gets);
        Assert.Contains("전달 상세 주소", model.DropoffText);
        model.PickupCode = "1234";
        await model.PickupCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, model.PickupCode); Assert.True(model.CanDeliver);
        await model.DeliverCommand.ExecuteAsync(null);
        Assert.Equal("Delivered", model.SelectedItem!.Value.StateCode);
        Assert.Equal([마트배송행동.Accept, 마트배송행동.Pickup, 마트배송행동.Deliver], api.Posts.Select(x => x.Action).ToArray());
        Assert.Equal(3, api.Gets); Assert.Null(store.Value); Assert.False(model.HasPendingRequest);
        Assert.Contains("정산·지급은 별도", model.Status);
    }

    [Theory]
    [InlineData(true, "1234")]
    [InlineData(false, "MART-QR-opaque-current")]
    public async Task CodeSubmissionIsThePickupAction_AndRawCodeIsNotPersisted(bool byNumber, string code)
    {
        var (model, api, _, store) = Create(Item("Accepted"));
        await model.ActivateAsync(); model.ByNumber = byNumber; model.PickupCode = code;
        await model.PickupCommand.ExecuteAsync(null);
        var sent = Assert.Single(api.Posts);
        Assert.Equal(마트배송행동.Pickup, sent.Action); Assert.Equal(code, sent.Code); Assert.Equal(byNumber, sent.ByNumber);
        var saved = Assert.Single(store.Saved);
        Assert.DoesNotContain("\"Code\":", saved);
        Assert.DoesNotContain("전달 상세 주소", saved);
        Assert.NotEqual(code, JsonDocument.Parse(saved).RootElement.GetProperty("CodeHash").GetString());
        Assert.Equal(string.Empty, model.PickupCode); Assert.True(model.CanDeliver);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("１２３４")]
    [InlineData("12A4")]
    public async Task FourDigitsAreAsciiAndExactLength(string code)
    {
        var (model, api, _, _) = Create(Item("Accepted"));
        await model.ActivateAsync(); model.PickupCode = code;
        Assert.False(model.CanPickup); await model.PickupCommand.ExecuteAsync(null); Assert.Empty(api.Posts);
    }

    [Fact]
    public async Task StorageFailureBlocksPost_AndDoesNotPretendSuccess()
    {
        var (model, api, _, store) = Create(); store.FailSave = true;
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null);
        Assert.Empty(api.Posts); Assert.False(model.HasPendingRequest); Assert.Contains("전송하지 않았습니다", model.Status);
    }

    [Fact]
    public async Task UnknownPostRetainsIdentity_RequeriesOnly_AndExplicitRetryUsesSameIdentity()
    {
        var (model, api, _, store) = Create(); api.FailPost = true;
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null);
        var first = Assert.Single(api.Posts);
        Assert.True(model.HasPendingRequest); Assert.NotNull(store.Value); Assert.False(model.CanRetry);
        api.Result = (_, _) => throw new FDriverApiException("아직 모름", HttpStatusCode.ServiceUnavailable);
        await model.CheckResultCommand.ExecuteAsync(null);
        Assert.Single(api.Posts); Assert.False(model.CanRetry);
        api.Result = (_, _) => throw new FDriverApiException("접수 없음", HttpStatusCode.NotFound);
        await model.CheckResultCommand.ExecuteAsync(null);
        Assert.True(model.CanRetry); Assert.Single(api.Posts);
        api.FailPost = false;
        await model.RetryCommand.ExecuteAsync(null);
        Assert.Equal(2, api.Posts.Count); Assert.Equal(first.Id, api.Posts[1].Id);
        Assert.False(model.HasPendingRequest);
    }

    [Fact]
    public async Task RestoredUnknownPickupRequiresSameCodeHash_AndDoesNotPostDuringRestore()
    {
        var (first, api, auth, store) = Create(Item("Accepted")); api.FailPost = true;
        await first.ActivateAsync(); first.ByNumber = false; first.PickupCode = "MART-QR-current-secret";
        await first.PickupCommand.ExecuteAsync(null); var request = store.Value!;
        first.Deactivate();
        var restarted = new 살뜰마트배송PageViewModel(api, auth, store);
        await restarted.ActivateAsync();
        Assert.Single(api.Posts); Assert.True(restarted.HasPendingRequest); Assert.False(restarted.CanRetry);
        Assert.Equal(1, api.ResultGets); Assert.Equal(request.RequestId, api.LastResultId);
        restarted.PickupCode = "MART-QR-other-secret"; Assert.False(restarted.CanRetry);
        restarted.PickupCode = "MART-QR-current-secret"; Assert.True(restarted.CanRetry);
        api.FailPost = false; await restarted.RetryCommand.ExecuteAsync(null);
        Assert.Equal(request.RequestId, api.Posts[1].Id); Assert.True(restarted.CanDeliver);
        Assert.Null(store.Value); Assert.Equal(string.Empty, restarted.PickupCode);
    }

    [Fact]
    public async Task RestoredSuccessfulReceiptReadsCurrentState_WithoutAnyNewPost()
    {
        var (model, api, auth, store) = Create(); api.FailPost = true;
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null); model.Deactivate();
        api.Current = Item("Accepted"); api.Result = (_, _) => Task.FromResult(Item("Accepted"));
        var restarted = new 살뜰마트배송PageViewModel(api, auth, store);
        await restarted.ActivateAsync();
        Assert.Single(api.Posts); Assert.False(restarted.HasPendingRequest); Assert.Null(store.Value);
        Assert.True(restarted.SelectedItem!.Value.CanPickup);
    }

    [Fact]
    public async Task CanonicalReadFailureKeepsReceiptAndBlocksRetry()
    {
        var (model, api, _, store) = Create();
        api.Get = (_, _) => throw new FDriverApiException("조회 실패", HttpStatusCode.ServiceUnavailable);
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null);
        Assert.True(model.HasPendingRequest); Assert.NotNull(store.Value); Assert.False(model.CanRetry);
        await model.CheckResultCommand.ExecuteAsync(null); Assert.Single(api.Posts); Assert.True(model.HasPendingRequest);
    }

    [Fact]
    public async Task QueueOrRevisionChangeDoesNotPermitSameRequestRetry()
    {
        var (model, api, _, _) = Create(); api.FailPost = true;
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null);
        api.Current.RecommendationRound++;
        await model.CheckResultCommand.ExecuteAsync(null);
        Assert.False(model.CanRetry); await model.RetryCommand.ExecuteAsync(null); Assert.Single(api.Posts);
    }

    [Fact]
    public async Task RepeatedClickWhilePostPendingSendsOnce()
    {
        var (model, api, _, _) = Create();
        var gate = new TaskCompletionSource<마트기사업무항목Dto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Command = (_, _, _) => gate.Task;
        await model.ActivateAsync();
        var first = model.AcceptCommand.ExecuteAsync(null);
        await model.AcceptCommand.ExecuteAsync(null);
        Assert.Single(api.Posts); Assert.True(model.IsBusy);
        api.Current = Item("Accepted"); gate.SetResult(api.Current); await first;
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task LogoutDuringGetDiscardsLatePrivateDataAndClearsOwnerPending()
    {
        var (model, api, auth, store) = Create();
        var gate = new TaskCompletionSource<마트기사업무목록Dto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.List = _ => gate.Task;
        var load = model.ActivateAsync(); await auth.ClearAsync();
        gate.SetResult(new() { Enabled = true, Items = [Item()] }); await load;
        Assert.Empty(model.Items); Assert.False(model.HasSelection); Assert.False(model.IsEnabled); Assert.Empty(api.Posts);
        Assert.Null(store.Value);
    }

    [Fact]
    public async Task AccountChangeClearsPendingAndDoesNotRestoreAnotherOwnersRequest()
    {
        var (model, api, auth, store) = Create(); api.FailPost = true;
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null); Assert.NotNull(store.Value);
        await auth.ApplyAsync(new ClientAuthTokenSnapshot("other-token", DateTime.UtcNow.AddHours(1), "other-refresh",
            DateTime.UtcNow.AddDays(1), "other-driver", "다른 기사", ["Driver"]));
        Assert.False(model.HasPendingRequest); Assert.Empty(model.Items); Assert.Null(store.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyStructuredConfirmedRejectionClearsPendingAfterCanonicalRead(bool confirmed)
    {
        var (model, api, _, store) = Create(Item("Accepted"));
        var error = new FDriverApiException("입력 거절", HttpStatusCode.BadRequest);
        error.Data["errorCode"] = "MartPickupCodeMismatch";
        if (confirmed) error.Data["commandRejected"] = true;
        api.Command = (_, _, _) => throw error;
        await model.ActivateAsync(); model.PickupCode = "1234"; await model.PickupCommand.ExecuteAsync(null);
        Assert.Equal(!confirmed, model.HasPendingRequest); Assert.Equal(confirmed, store.Value is null);
        Assert.Equal(confirmed ? 1 : 0, api.Gets);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AccessFailureStopsWorkWithoutDiscardingUnknownRequest(HttpStatusCode status)
    {
        var (model, api, _, store) = Create(); api.Command = (_, _, _) => throw new FDriverApiException("권한 변경", status);
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null);
        Assert.False(model.IsEnabled); Assert.False(model.HasSelection); Assert.True(model.HasPendingRequest); Assert.NotNull(store.Value);
    }

    [Fact]
    public async Task WrongOfferResultIsNotAppliedOrCleared()
    {
        var (model, api, _, store) = Create(); api.Command = (_, _, _) => Task.FromResult(Item("Accepted", "other-offer"));
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null);
        Assert.True(model.HasPendingRequest); Assert.NotNull(store.Value); Assert.Equal("mart-offer", model.SelectedItem!.Value.OfferId);
    }

    [Fact]
    public async Task RejectReadsOwnReceiptAndList_NotTheNextDriversWork()
    {
        var (model, api, _, store) = Create();
        api.Result = (_, _) => Task.FromResult(Item("Rejected"));
        api.Get = (_, _) => throw new FDriverApiException("다음 기사 권한", HttpStatusCode.NotFound);
        await model.ActivateAsync(); await model.RejectCommand.ExecuteAsync(null);
        Assert.Equal(마트배송행동.Reject, Assert.Single(api.Posts).Action);
        Assert.Equal(0, api.Gets); Assert.Equal(1, api.ResultGets); Assert.Null(store.Value);
        Assert.Equal("Rejected", model.SelectedItem!.Value.StateCode);
        Assert.DoesNotContain("전달 상세 주소", model.DropoffText);
    }

    [Fact]
    public async Task ConfirmedRejectedProposalMayClearAfter404AndSuccessfulOwnedListWithoutIt()
    {
        var (model, api, _, store) = Create();
        var error = new FDriverApiException("추천 변경", HttpStatusCode.Conflict);
        error.Data["errorCode"] = "MartDriverRecommendationChanged"; error.Data["commandRejected"] = true;
        api.Command = (_, _, _) =>
        {
            api.List = _ => Task.FromResult(new 마트기사업무목록Dto { Enabled = true });
            throw error;
        };
        api.Get = (_, _) => throw new FDriverApiException("본인 제안 없음", HttpStatusCode.NotFound);
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null);
        Assert.Null(store.Value); Assert.False(model.HasPendingRequest); Assert.Empty(model.Items);
        Assert.Contains("제안이 변경", model.Status); Assert.Single(api.Posts);
    }

    [Fact]
    public async Task UnknownPostWithMissingReceiptAndMissingOfferIsNotClearedOrRetried()
    {
        var (model, api, _, store) = Create(); api.FailPost = true;
        await model.ActivateAsync(); await model.AcceptCommand.ExecuteAsync(null);
        api.Get = (_, _) => throw new FDriverApiException("본인 제안 없음", HttpStatusCode.NotFound);
        await model.CheckResultCommand.ExecuteAsync(null);
        Assert.NotNull(store.Value); Assert.True(model.HasPendingRequest); Assert.False(model.CanRetry); Assert.Single(api.Posts);
    }

    [Fact]
    public async Task ConfirmedRejectionFactSurvivesRestart_WhenFollowupGetInitiallyFailed()
    {
        var (model, api, auth, store) = Create(Item("Accepted"));
        var error = new FDriverApiException("코드 거절", HttpStatusCode.BadRequest);
        error.Data["errorCode"] = "MartPickupCodeMismatch"; error.Data["commandRejected"] = true;
        api.Command = (_, _, _) => throw error;
        api.Get = (_, _) => throw new FDriverApiException("조회 불명확", HttpStatusCode.ServiceUnavailable);
        await model.ActivateAsync(); model.PickupCode = "1234"; await model.PickupCommand.ExecuteAsync(null);
        Assert.True(store.Value!.ServerRejected); model.Deactivate(); api.Get = null;
        var restarted = new 살뜰마트배송PageViewModel(api, auth, store);
        await restarted.ActivateAsync();
        Assert.Null(store.Value); Assert.False(restarted.HasPendingRequest); Assert.Single(api.Posts);
        Assert.Equal(0, api.ResultGets);
    }

    [Fact]
    public async Task LostBadPinResponseRestoresStoredRejection_ThenAllowsNewInputWithoutRepeatingPost()
    {
        var (model, api, auth, store) = Create(Item("Accepted")); api.FailPost = true;
        await model.ActivateAsync(); model.PickupCode = "9999"; await model.PickupCommand.ExecuteAsync(null); model.Deactivate();
        var error = new FDriverApiException("저장된 번호 거절", HttpStatusCode.Conflict);
        error.Data["errorCode"] = "MartPickupCodeMismatch"; error.Data["commandRejected"] = true;
        api.Result = (_, _) => throw error;
        var restarted = new 살뜰마트배송PageViewModel(api, auth, store);
        await restarted.ActivateAsync();
        Assert.Null(store.Value); Assert.False(restarted.HasPendingRequest); Assert.Single(api.Posts);
        restarted.PickupCode = "1234"; Assert.True(restarted.CanPickup);
    }

    private static (살뜰마트배송PageViewModel Model, MartApi Api, FDriverTestSession Auth, MartPendingStore Store) Create(마트기사업무항목Dto? item = null)
    {
        var api = new MartApi { Current = item ?? Item() }; var auth = new FDriverTestSession(); var store = new MartPendingStore();
        return (new(api, auth, store), api, auth, store);
    }
    internal static 마트기사업무항목Dto Item(string state = "Recommended", string offerId = "mart-offer") => new()
    {
        OfferId = offerId, OrderReference = "mart-order", StateCode = state,
        Revision = state == "Recommended" ? 0 : state == "Accepted" ? 1 : state == "PickedUp" ? 2 : 3,
        AttemptStableId = state == "Recommended" ? "" : "mart-attempt", RecommendationRound = 2,
        QueueUpdatedAtUtc = new(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc), LabelRevision = 7, DriverPayout = 4000,
        Pickup = new FoodDeliveryDriverStopDto { Label = "살뜰마트 1", Address = "픽업 주소" },
        Dropoff = new FoodDeliveryDriverStopDto { Label = "전달 장소 1", Address = "전달 상세 주소" },
        CanAccept = state == "Recommended", CanReject = state == "Recommended", CanPickup = state == "Accepted", CanDeliver = state == "PickedUp"
    };

    private sealed class MartPendingStore : I살뜰마트배송PendingStore
    {
        public 살뜰마트배송미확인요청? Value { get; set; }
        public bool FailSave { get; set; }
        public List<string> Saved { get; } = [];
        public Task<살뜰마트배송미확인요청?> LoadAsync(CancellationToken token = default) => Task.FromResult(Value);
        public Task SaveAsync(살뜰마트배송미확인요청 request, CancellationToken token = default)
        {
            if (FailSave) throw new IOException("저장 거절");
            Value = request; Saved.Add(JsonSerializer.Serialize(request)); return Task.CompletedTask;
        }
        public Task ClearAsync(string ownerId, CancellationToken token = default)
        { if (Value?.OwnerId == ownerId) Value = null; return Task.CompletedTask; }
    }

    private sealed class MartApi : I살뜰마트배송ApiClient
    {
        public 마트기사업무항목Dto Current { get; set; } = Item();
        public Func<CancellationToken, Task<마트기사업무목록Dto>>? List { get; set; }
        public Func<string, CancellationToken, Task<마트기사업무항목Dto>>? Get { get; set; }
        public Func<Guid, CancellationToken, Task<마트기사업무항목Dto>>? Result { get; set; }
        public Func<마트배송행동, Guid, CancellationToken, Task<마트기사업무항목Dto>>? Command { get; set; }
        public List<(마트배송행동 Action, Guid Id, string? Code, bool ByNumber)> Posts { get; } = [];
        public bool FailPost { get; set; }
        public int Gets { get; private set; }
        public int ResultGets { get; private set; }
        public Guid LastResultId { get; private set; }
        public Task<마트기사업무목록Dto> ListAsync(CancellationToken token = default)
            => List?.Invoke(token) ?? Task.FromResult(new 마트기사업무목록Dto { Enabled = true, Items = [Current] });
        public Task<마트기사업무항목Dto> GetAsync(string id, CancellationToken token = default)
        { Gets++; return Get?.Invoke(id, token) ?? Task.FromResult(Current); }
        public Task<마트기사업무항목Dto> ResultAsync(Guid id, CancellationToken token = default)
        {
            ResultGets++; LastResultId = id;
            return Result?.Invoke(id, token) ?? throw new FDriverApiException("접수 없음", HttpStatusCode.NotFound);
        }
        public Task<마트기사업무항목Dto> AcceptAsync(string id, 마트기사수락요청 request, CancellationToken token = default)
            => Send(마트배송행동.Accept, request.ClientRequestId, null, false, token);
        public Task<마트기사업무항목Dto> RejectAsync(string id, 마트기사거절요청 request, CancellationToken token = default)
            => Send(마트배송행동.Reject, request.ClientRequestId, null, false, token);
        public Task<마트기사업무항목Dto> PickupAsync(string id, 마트기사픽업요청 request, CancellationToken token = default)
            => Send(마트배송행동.Pickup, request.ClientRequestId, request.Code, request.ByNumber, token);
        public Task<마트기사업무항목Dto> DeliverAsync(string id, 마트기사전달요청 request, CancellationToken token = default)
            => Send(마트배송행동.Deliver, request.ClientRequestId, null, false, token);
        private Task<마트기사업무항목Dto> Send(마트배송행동 action, Guid id, string? code, bool byNumber, CancellationToken token)
        {
            Posts.Add((action, id, code, byNumber));
            if (Command is not null) return Command(action, id, token);
            if (FailPost) throw new FDriverApiException("응답 유실", null);
            Current = Item(action switch { 마트배송행동.Accept => "Accepted", 마트배송행동.Reject => "Rejected",
                마트배송행동.Pickup => "PickedUp", _ => "Delivered" });
            return Task.FromResult(Current);
        }
    }
}
