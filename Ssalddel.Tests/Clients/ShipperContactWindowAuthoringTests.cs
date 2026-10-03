using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Tests.UiCommon;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperContactWindowAuthoringTests
{
    [Fact]
    public async Task 등록은_입력한인계정보를전달하고_서버가반환한동일의뢰상세로간다()
    {
        var state = ShipperRequestContactWindowTests.CompleteState();
        state.상차상세주소 = "상차장 2";
        state.상차연락처이름 = "상차 담당";
        state.상차연락처전화번호 = "010-1111-2222";
        state.하차연락처이름 = "수령 담당";
        state.하차연락처전화번호 = "010-3333-4444";
        var operations = new Operations();
        var navigation = new Navigation();
        var model = new ShipperRequestAuthoringPageViewModel(state, operations, navigation);
        await model.InitializeAsync();
        await model.SubmitAsync();
        Assert.Equal(1, operations.AddCalls);
        Assert.Equal("상차장 2", operations.Submitted?.픽업정보?.주소.상세주소);
        Assert.Equal(state.상차연락처전화번호, operations.Submitted?.픽업정보?.연락처.전화번호);
        Assert.Equal(state.하차연락처전화번호, operations.Submitted?.하차정보?.연락처.전화번호);
        Assert.Equal(new DateTime(2030, 10, 3, 0, 0, 0, DateTimeKind.Utc), operations.Submitted?.픽업정보?.시간창?.시작일시);
        Assert.Null(operations.Submitted?.하차정보?.시간창);
        Assert.Equal(ShipperRoutes.RequestDetailFor("ledger-created"), navigation.LastPath);
    }

    [Fact]
    public async Task 등록실패는_인계입력을보존하고_같은값으로다시등록할수있다()
    {
        var state = ShipperRequestContactWindowTests.CompleteState();
        state.상차연락처전화번호 = "010-1111-2222";
        var operations = new Operations { FailAdd = true };
        var navigation = new Navigation();
        var model = new ShipperRequestAuthoringPageViewModel(state, operations, navigation);
        await model.InitializeAsync();
        await model.SubmitAsync();
        Assert.False(model.IsBusy);
        Assert.Contains("등록 실패", model.StatusMessage);
        Assert.Null(navigation.LastPath);
        Assert.Equal("010-1111-2222", state.상차연락처전화번호);
        Assert.Equal(new DateTime(2030, 10, 3, 9, 0, 0), state.상차시간창시작일시);
        operations.FailAdd = false;
        await model.SubmitAsync();
        Assert.Equal(2, operations.AddCalls);
        Assert.Equal("010-1111-2222", operations.Submitted?.픽업정보?.연락처.전화번호);
    }

    [Fact]
    public async Task 잘못된시간창은_등록서비스를호출하기전에보류한다()
    {
        var state = ShipperRequestContactWindowTests.CompleteState();
        state.상차시간창종료일시 = state.상차시간창시작일시;
        var operations = new Operations();
        var model = new ShipperRequestAuthoringPageViewModel(state, operations, new Navigation());
        await model.InitializeAsync();
        await model.SubmitAsync();
        Assert.Equal(0, operations.AddCalls);
        Assert.Contains("필수 항목", model.StatusMessage);
    }

    [Fact]
    public async Task 등록중재호출은_두번째원장생성을요청하지않는다()
    {
        var operations = new Operations { PendingAdd = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation());
        await model.InitializeAsync();
        var first = model.SubmitAsync();
        Assert.True(model.IsBusy);
        await model.SubmitAsync();
        Assert.Equal(1, operations.AddCalls);
        operations.PendingAdd.SetResult(new() { 의뢰Id = "ledger-created" });
        await first;
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task 수정초안은_원장개인정보를복사하고_실패에도선택의뢰를바꾸지않는다()
    {
        var operations = new Operations { FailUpdate = true };
        var ledger = new 화주운송의뢰상태ViewModel();
        var original = new ShipperRequestItem
        {
            의뢰Id = "original", 화물종류 = "화물", 픽업지 = "상차지", 하차지 = "하차지",
            픽업정보 = ShipperRequestHandoffMapper.CreatePickup(ShipperRequestContactWindowTests.CompleteState().ToDraft()),
            하차정보 = new() { 연락처 = new() { 전화번호 = "010-1111-2222" } }
        };
        ledger.저장적용(original);
        var model = new 화주운송의뢰수정ViewModel(operations, ledger);
        Assert.True(model.선택항목적용());
        model.초안.하차정보!.연락처.전화번호 = "010-3333-4444";
        model.초안.픽업정보!.시간창!.종료일시 = model.초안.픽업정보.시간창.종료일시.AddHours(1);
        Assert.False(await model.실행Async());
        Assert.Same(original, ledger.선택된의뢰);
        Assert.Equal("010-1111-2222", original.하차정보?.연락처.전화번호);
        Assert.Equal("010-3333-4444", model.초안.하차정보.연락처.전화번호);
        Assert.NotEqual(original.픽업정보?.시간창?.종료일시, model.초안.픽업정보.시간창.종료일시);
    }

    private sealed class Navigation : NavigationManager
    {
        public string? LastPath { get; private set; }
        public Navigation() => Initialize("https://test.invalid/", "https://test.invalid/shipper/request/review");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastPath = uri;
        protected override void NavigateToCore(string uri, NavigationOptions options) => LastPath = uri;
    }

    private sealed class Operations : IShipperOperationsService
    {
        public int AddCalls { get; private set; }
        public bool FailAdd { get; set; }
        public bool FailUpdate { get; set; }
        public TaskCompletionSource<ShipperRequestItem>? PendingAdd { get; set; }
        public ShipperRequestItem? Submitted { get; private set; }
        public Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            Submitted = request;
            return FailAdd ? Task.FromException<ShipperRequestItem>(new IOException("원장 연결 실패"))
                : PendingAdd?.Task ?? Task.FromResult(new ShipperRequestItem { 의뢰Id = "ledger-created" });
        }
        public Task<ShipperRequestItem> UpdateRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default)
            => FailUpdate ? Task.FromException<ShipperRequestItem>(new IOException("수정 연결 실패")) : Task.FromResult(request);
        public Task<IReadOnlyList<string>> GetVehicleTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(["1톤 카고"]);
        public Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShipperRequestItem?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<창고요약응답>> GetWarehousesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<입고요청항목응답>> GetInboundsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<재고항목응답>> GetInventoryAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<decimal> EstimateFareAsync(string vehicleType, decimal distanceKm, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteRequestAsync(string requestId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
