using System.Text.Json;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodOrderSubmissionRecoveryTests
{
    [Fact]
    public async Task ResponseLossThenNewViewModelReadsExistingOrderWithoutPosting()
    {
        var store = new PersistentStore(); var api = new Api { LoseResponse = true };
        var first = New(store, api); await first.계정설정Async("owner");
        var request = Request();
        await Assert.ThrowsAsync<HttpRequestException>(() => first.등록Async(request, default));
        Assert.NotNull(store.Json);
        api.OrderNo = "FOOD-ACCEPTED";
        var reopened = New(store, api); await reopened.계정설정Async("owner");
        Assert.Equal("FOOD-ACCEPTED", reopened.접수주문번호);
        Assert.Equal(1, api.Posts); Assert.Equal(request.클라이언트요청Id, api.LastReadId);
        Assert.Null(store.Json); Assert.Null(reopened.Pending);
    }

    [Fact]
    public async Task SaveFailurePreventsSubmission()
    {
        var store = new PersistentStore { SaveFails = true }; var api = new Api();
        var vm = New(store, api); await vm.계정설정Async("owner");
        await Assert.ThrowsAsync<IOException>(() => vm.등록Async(Request(), default));
        Assert.Equal(0, api.Posts);
    }

    [Fact]
    public async Task MissingOrderRequiresExplicitResubmissionAndUsesOriginalRequest()
    {
        var request = Request(); var store = new PersistentStore(); await store.SaveAsync(new("owner", request, DateTime.UtcNow));
        var api = new Api(); var vm = New(store, api); await vm.계정설정Async("owner");
        Assert.True(vm.미접수확인됨); Assert.True(vm.입력잠금); Assert.Equal(0, api.Posts);
        Assert.True(await vm.동일주문재제출Async());
        Assert.Equal(request.클라이언트요청Id, api.LastPosted!.클라이언트요청Id);
        Assert.Equal("수령인", api.LastPosted.수령인정보.수령인명);
        Assert.Equal("메뉴", Assert.Single(api.LastPosted.상품목록).상품명);
        Assert.Equal("FOOD-NEW", vm.접수주문번호); Assert.Null(store.Json);
    }

    [Fact]
    public async Task LookupFailureKeepsOriginalRequestAndLocksResubmission()
    {
        var store = new PersistentStore(); await store.SaveAsync(new("owner", Request(), DateTime.UtcNow));
        var api = new Api { ReadFails = true }; var vm = New(store, api); await vm.계정설정Async("owner");
        Assert.True(vm.오류발생); Assert.False(vm.미접수확인됨); Assert.True(vm.입력잠금);
        Assert.False(await vm.동일주문재제출Async()); Assert.NotNull(store.Json); Assert.Equal(0, api.Posts);
    }

    [Fact]
    public async Task OtherAccountDoesNotReadOrDisplayPreviousRecipients()
    {
        var store = new PersistentStore(); await store.SaveAsync(new("previous", Request(), DateTime.UtcNow));
        var api = new Api(); var vm = New(store, api); await vm.계정설정Async("different");
        Assert.Null(vm.Pending); Assert.Null(store.Json); Assert.Equal(0, api.Reads); Assert.True(vm.초기확인완료);
    }

    [Fact]
    public async Task AnonymousSessionDoesNotReadStoredRequest()
    {
        var store = new PersistentStore(); await store.SaveAsync(new("owner", Request(), DateTime.UtcNow));
        var vm = New(store, new Api()); await vm.계정설정Async(null);
        Assert.Null(vm.Pending); Assert.Equal(0, store.Loads); Assert.NotNull(store.Json);
    }

    [Theory]
    [InlineData(400)] [InlineData(403)] [InlineData(422)]
    public async Task DefiniteRejectionReleasesDraftForCorrection(int status)
    {
        var store = new PersistentStore(); var api = new Api { Rejection = status };
        var vm = New(store, api); await vm.계정설정Async("owner");
        await Assert.ThrowsAsync<SsalddelApiException>(() => vm.등록Async(Request(), default));
        Assert.Null(vm.Pending); Assert.Null(store.Json); Assert.False(vm.입력잠금);
    }

    [Fact]
    public async Task EmptySuccessfulLookupIsNotProofOfAcceptance()
    {
        var store = new PersistentStore(); await store.SaveAsync(new("owner", Request(), DateTime.UtcNow));
        var vm = New(store, new Api { OrderNo = "" }); await vm.계정설정Async("owner");
        Assert.True(vm.오류발생); Assert.Null(vm.접수주문번호); Assert.NotNull(vm.Pending);
    }

    [Fact]
    public async Task UncertainOriginalRequestCannotBeReplacedWithNewRequestId()
    {
        var store = new PersistentStore(); var api = new Api { LoseResponse = true };
        var vm = New(store, api); await vm.계정설정Async("owner");
        await Assert.ThrowsAsync<HttpRequestException>(() => vm.등록Async(Request(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.등록Async(Request(), default));
        Assert.Equal(1, api.Posts);
    }

    [Fact]
    public async Task LateLookupCannotReplaceNewAccountsRecoveryState()
    {
        var store = new PersistentStore(); await store.SaveAsync(new("owner", Request(), DateTime.UtcNow));
        var api = new Api { ReadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = New(store, api); var old = vm.계정설정Async("owner");
        await api.ReadStarted.Task;
        await vm.계정설정Async("different");
        Assert.True(vm.초기확인완료); Assert.Null(vm.Pending);
        api.ReadCompletion.SetResult(new() { 주문번호 = "OLD" }); await old;
        Assert.Null(vm.접수주문번호); Assert.Null(vm.Pending); Assert.False(vm.오류발생);
    }

    [Fact]
    public async Task PageExitKeepsPersistentRequestButRejectsLateLookup()
    {
        var store = new PersistentStore(); await store.SaveAsync(new("owner", Request(), DateTime.UtcNow));
        var api = new Api { ReadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = New(store, api); var old = vm.계정설정Async("owner");
        await api.ReadStarted.Task; vm.Dispose();
        api.ReadCompletion.SetResult(new() { 주문번호 = "OLD" }); await old;
        Assert.Null(vm.Pending); Assert.Null(vm.접수주문번호); Assert.NotNull(store.Json);
    }

    private static FoodOrderSubmissionRecoveryViewModel New(PersistentStore store, Api api) => new(store, api, api);
    private static 음식주문등록요청 Request() => new()
    {
        클라이언트요청Id = Guid.NewGuid(), 음식점Id = 7,
        수령인정보 = new() { 수령인명 = "수령인", 연락처 = "연락처", 주소 = "전달 장소" },
        상품목록 = [new() { 메뉴Id = 8, 상품명 = "메뉴", 수량 = 2, 단가 = 3000 }]
    };
    private sealed class PersistentStore : IFoodOrderPendingSubmissionStore
    {
        public string? Json; public bool SaveFails; public int Loads;
        public Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default)
        { Loads++; return Task.FromResult(Json is null ? null : JsonSerializer.Deserialize<FoodOrderPendingSubmission>(Json)); }
        public Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default)
        { if (SaveFails) throw new IOException(); Json = JsonSerializer.Serialize(snapshot); return Task.CompletedTask; }
        public async Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default)
        { if ((await LoadAsync(cancellationToken))?.Request.클라이언트요청Id == requestId) Json = null; }
    }
    private sealed class Api : I주문자음식주문접수결과Service, I주문자음식주문쓰기Service
    {
        public bool LoseResponse; public bool ReadFails; public int? Rejection;
        public string? OrderNo; public int Posts; public int Reads; public Guid LastReadId;
        public 음식주문등록요청? LastPosted;
        public TaskCompletionSource<음식주문접수결과응답?>? ReadCompletion;
        public TaskCompletionSource ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<음식주문접수결과응답?> 접수결과Async(Guid requestId, CancellationToken cancellationToken = default)
        {
            Reads++; LastReadId = requestId;
            if (ReadCompletion is not null) { ReadStarted.TrySetResult(); return ReadCompletion.Task; }
            if (ReadFails) throw new HttpRequestException();
            return Task.FromResult(OrderNo is null ? null : new 음식주문접수결과응답 { 주문번호 = OrderNo });
        }
        public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken cancellationToken = default)
        {
            Posts++; LastPosted = request;
            if (Rejection is int status) throw new SsalddelApiException("거절", status, "접수", "", null);
            if (LoseResponse) throw new HttpRequestException();
            return Task.FromResult(new 음식주문응답 { 주문번호 = "FOOD-NEW" });
        }
    }
}
