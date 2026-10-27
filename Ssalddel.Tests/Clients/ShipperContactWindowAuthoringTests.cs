using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Privacy;
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
        state.화물수량 = 5;
        state.화물설명 = "봉인된 상자를 기울이지 마세요.";
        state.화물중량Kg = 12.5m;
        state.화물길이Mm = 450; state.화물폭Mm = 350; state.화물높이Mm = 250; state.팔레트개수 = 2;
        state.화물부피Cbm = .48m;
        state.온도조건 = "냉장";
        state.화물적재형태 = "컨테이너 화물(LCL/FCL)";
        state.서비스레벨 = "지정 시간";
        state.요청사항 = "상차 전에 연락해 주세요.";
        state.대기료 = 0m;
        state.수작업비 = 1500m;
        state.할증 = null;
        state.알선소Id = "broker-reference";
        state.절차메모 = "수작업 범위 사전 확인";
        var operations = new Operations();
        var navigation = new Navigation();
        var model = new ShipperRequestAuthoringPageViewModel(state, operations, navigation);
        await model.InitializeAsync();
        await ShipperConsentFixture.ConfirmAsync(model);
        await model.SubmitAsync();
        Assert.Equal(1, operations.AddCalls);
        Assert.Equal("상차장 2", operations.Submitted?.픽업정보?.주소.상세주소);
        Assert.Equal(state.상차연락처전화번호, operations.Submitted?.픽업정보?.연락처.전화번호);
        Assert.Equal(state.하차연락처전화번호, operations.Submitted?.하차정보?.연락처.전화번호);
        Assert.Equal(new DateTime(2030, 10, 3, 0, 0, 0, DateTimeKind.Utc), operations.Submitted?.픽업정보?.시간창?.시작일시);
        Assert.Null(operations.Submitted?.하차정보?.시간창);
        Assert.Equal(5, operations.Submitted?.화물원본?.수량);
        Assert.Equal(state.화물설명, operations.Submitted?.화물원본?.설명);
        Assert.Equal(12.5m, operations.Submitted?.화물원본?.중량Kg);
        Assert.Equal(450, operations.Submitted?.화물원본?.길이Mm);
        Assert.Equal(350, operations.Submitted?.화물원본?.폭Mm);
        Assert.Equal(250, operations.Submitted?.화물원본?.높이Mm);
        Assert.Equal(2, operations.Submitted?.화물원본?.팔레트개수);
        Assert.Equal(.48m, operations.Submitted?.화물원본?.부피Cbm);
        Assert.Equal("냉장", operations.Submitted?.화물원본?.온도조건);
        Assert.Equal(state.화물적재형태, operations.Submitted?.화물적재형태);
        Assert.Equal("지정 시간", operations.Submitted?.서비스레벨);
        Assert.Equal(state.요청사항, operations.Submitted?.요청사항);
        Assert.Equal(0m, operations.Submitted?.대기료);
        Assert.Equal(1500m, operations.Submitted?.수작업비);
        Assert.Null(operations.Submitted?.할증);
        Assert.Equal(state.알선소Id, operations.Submitted?.알선소Id);
        Assert.Equal(state.절차메모, operations.Submitted?.절차메모);
        Assert.Equal(ShipperRoutes.RequestDetailFor("ledger-created"), navigation.LastPath);
    }

    [Fact]
    public async Task 중량만입력한접수는_수량과부피와온도를만들지않는다()
    {
        var state = ShipperRequestContactWindowTests.CompleteState();
        state.화물수량 = null;
        state.화물중량Kg = 20m;
        state.화물부피Cbm = null;
        state.온도조건 = null;
        var operations = new Operations();
        using var model = new ShipperRequestAuthoringPageViewModel(state, operations, new Navigation());
        await model.InitializeAsync();
        await ShipperConsentFixture.ConfirmAsync(model);
        await model.SubmitAsync();
        Assert.Equal(1, operations.AddCalls);
        Assert.Equal(20m, operations.Submitted?.화물원본?.중량Kg);
        Assert.Null(operations.Submitted?.화물원본?.수량);
        Assert.Null(operations.Submitted?.화물원본?.부피Cbm);
        Assert.Null(operations.Submitted?.화물원본?.온도조건);
    }

    [Theory]
    [InlineData("인수증")]
    [InlineData("현금성 정산")]
    [InlineData("현금")]
    [InlineData("별도정산")]
    public async Task 정산시점과수납주체가불명확한선택은_새접수나보호기록을만들지않는다(string payment)
    {
        var state = ShipperRequestContactWindowTests.CompleteState();
        state.결제수단 = payment;
        var operations = new Operations();
        using var model = new ShipperRequestAuthoringPageViewModel(state, operations, new Navigation());
        await model.InitializeAsync();
        await ShipperConsentFixture.ConfirmAsync(model);
        await model.SubmitAsync();
        Assert.Equal(0, operations.AddCalls);
        Assert.False(model.HasPendingRequest);
        Assert.Contains("정산 시점과 수납 주체", model.StatusMessage);
        Assert.Equal(payment, state.결제수단);
        state.결제수단 = "계좌이체";
        await model.SubmitAsync();
        Assert.Equal(1, operations.AddCalls);
        Assert.Equal("계좌이체", operations.Submitted?.결제수단);
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
        await ShipperConsentFixture.ConfirmAsync(model);
        await model.SubmitAsync();
        Assert.False(model.IsBusy);
        Assert.Contains("이전 접수 결과", model.StatusMessage);
        Assert.Null(navigation.LastPath);
        Assert.Equal("010-1111-2222", state.상차연락처전화번호);
        Assert.Equal(new DateTime(2030, 10, 3, 9, 0, 0), state.상차시간창시작일시);
        operations.FailAdd = false;
        await model.SubmitAsync();
        Assert.Equal(1, operations.AddCalls); // 불명확한 응답에는 GET만 수행합니다.
        await model.RetryPendingAsync();
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
        await ShipperConsentFixture.ConfirmAsync(model);
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
        await ShipperConsentFixture.ConfirmAsync(model);
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
        public Task<신청개인정보동의증적Response?> RecordPrivacyConsentAsync(신청개인정보동의기록Request request, CancellationToken cancellationToken = default)
            => Task.FromResult<신청개인정보동의증적Response?>(ShipperConsentFixture.Response(request));
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
        public Task<ShipperRequestItem?> GetRequestByClientRequestIdAsync(string requestId, CancellationToken cancellationToken = default) => Task.FromResult<ShipperRequestItem?>(null);
        public Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<창고요약응답>> GetWarehousesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<입고요청항목응답>> GetInboundsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<재고항목응답>> GetInventoryAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<decimal> EstimateFareAsync(string vehicleType, decimal distanceKm, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteRequestAsync(string requestId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
