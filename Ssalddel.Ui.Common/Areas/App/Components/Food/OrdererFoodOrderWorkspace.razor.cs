using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Food;

public partial class OrdererFoodOrderWorkspace : IAsyncDisposable
{
    private bool _initialized;
    private bool? _reportedAuthenticationMode;
    private 주문자음식주문새로고침Controller? _refreshController;
    private 역할앱생명주기State? _lifecycle;

    [Parameter]
    public string? OrderNo { get; set; }

    [Parameter]
    public EventCallback<string?> OrderSelected { get; set; }

    [Parameter]
    public EventCallback<bool> AuthenticationModeChanged { get; set; }

    private 음식배달페이지접근ViewModel Access => ViewModel.접근;
    private 주문자앱인증ViewModel Authentication => ViewModel.인증;
    private 주문자음식주문목록ViewModel List => ViewModel.목록;
    private 주문자음식주문상세ViewModel Detail => ViewModel.상세;
    private 주문자음식주문새로고침Controller RefreshController
        => _refreshController ??= new(ViewModel, AppQueryEnabled);

    private bool AppQueryEnabled => _lifecycle is null
        || (_lifecycle.단계 == 역할앱생명주기단계.활성 && _lifecycle.연결가능);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _lifecycle = Services.GetService<역할앱생명주기State>();
        if (_lifecycle is not null) _lifecycle.Changed += HandleLifecycleChanged;
    }

    private void HandleLifecycleChanged()
    {
        // 신호 발생 시 상태를 캡처하여 연속된 pause/resume을 UI 큐에서 합쳐 버리지 않습니다.
        var enabled = AppQueryEnabled;
        _ = InvokeAsync(() => RefreshController.앱조회상태변경Async(enabled));
    }

    protected override async Task OnInitializedAsync()
    {
        await InitializeAsync();
        _initialized = true;
        RefreshController.시작(action => InvokeAsync(action));
    }

    protected override Task OnParametersSetAsync()
        => !_initialized
            ? Task.CompletedTask
            : RefreshController.작업실행Async(token => ViewModel.경로선택반영Async(OrderNo, token));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var mode = ViewModel.인증화면표시;
        if (!RefreshController.중지됨 && _reportedAuthenticationMode != mode)
        {
            _reportedAuthenticationMode = mode;
            await AuthenticationModeChanged.InvokeAsync(mode);
        }
    }

    private Task InitializeAsync()
        => RefreshController.작업실행Async(token => ViewModel.초기화Async(OrderNo, token));

    private Task LoginAsync(공통로그인요청 request)
        => RefreshController.작업실행Async(token => ViewModel.로그인Async(request, OrderNo, token));

    private Task SearchListAsync()
        => RefreshController.목록작업실행Async(token => ViewModel.목록검색Async(token));

    private Task ReloadListAsync()
        => RefreshController.목록작업실행Async(token => ViewModel.목록새로고침Async(token));

    private Task ResetListAsync()
        => RefreshController.목록작업실행Async(token => ViewModel.검색조건초기화Async(token));

    private Task ChangeListPageAsync(int page)
        => RefreshController.목록작업실행Async(token => ViewModel.페이지변경Async(page, token));

    private async Task LogoutAsync()
    {
        var loggedOut = false;
        await RefreshController.작업실행Async(async token => loggedOut = await ViewModel.로그아웃Async(token));
        if (loggedOut && !RefreshController.중지됨 && OrderSelected.HasDelegate)
        {
            await OrderSelected.InvokeAsync(null);
        }
    }

    private async Task SelectOrderAsync(string orderNo)
    {
        await RefreshController.작업실행Async(token => ViewModel.주문선택Async(orderNo, token));
        if (!RefreshController.중지됨 && ViewModel.개인주문조회가능 && OrderSelected.HasDelegate)
        {
            await OrderSelected.InvokeAsync(orderNo);
        }
    }

    private Task RetryOrderAsync(string orderNo)
        => RefreshController.작업실행Async(token => ViewModel.주문선택Async(orderNo, token));

    private Task RefreshOrderAsync(string orderNo)
        => RefreshController.작업실행Async(token => ViewModel.주문진행새로고침Async(token));

    private Task ConfirmReceiptAsync()
        => RefreshController.작업실행Async(token => ViewModel.주문수령확인Async(token));

    private Task CancelOrderAsync()
        => RefreshController.작업실행Async(token => ViewModel.주문취소Async(token));

    private async Task ClearSelectionAsync()
    {
        await RefreshController.작업실행Async(_ =>
        {
            ViewModel.주문선택해제();
            return Task.CompletedTask;
        });
        if (!RefreshController.중지됨 && OrderSelected.HasDelegate)
        {
            await OrderSelected.InvokeAsync(null);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_lifecycle is not null) _lifecycle.Changed -= HandleLifecycleChanged;
            _refreshController?.중지();
        }
        base.Dispose(disposing);
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (_refreshController is not null)
        {
            await _refreshController.DisposeAsync();
        }
    }
}
