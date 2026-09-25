using Microsoft.AspNetCore.Components;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Food;

public partial class OrdererFoodOrderWorkspace : IAsyncDisposable
{
    private bool _initialized;
    private readonly CancellationTokenSource _locationRefreshCancellation = new();
    private Task? _locationRefreshTask;

    [Parameter]
    public string? OrderNo { get; set; }

    [Parameter]
    public EventCallback<string?> OrderSelected { get; set; }

    private 음식배달페이지접근ViewModel Access => ViewModel.접근;
    private 주문자앱인증ViewModel Authentication => ViewModel.인증;
    private 주문자음식주문목록ViewModel List => ViewModel.목록;
    private 주문자음식주문상세ViewModel Detail => ViewModel.상세;

    protected override async Task OnInitializedAsync()
    {
        await InitializeAsync();
        _initialized = true;
        _locationRefreshTask = RefreshDriverLocationLoopAsync(_locationRefreshCancellation.Token);
    }

    protected override Task OnParametersSetAsync()
        => !_initialized
            ? Task.CompletedTask
            : ViewModel.경로선택반영Async(OrderNo);

    private Task InitializeAsync() => ViewModel.초기화Async(OrderNo);

    private Task LoginAsync(공통로그인요청 request)
        => ViewModel.로그인Async(request, OrderNo);

    private async Task LogoutAsync()
    {
        if (await ViewModel.로그아웃Async() && OrderSelected.HasDelegate)
        {
            await OrderSelected.InvokeAsync(null);
        }
    }

    private async Task SelectOrderAsync(string orderNo)
    {
        await ViewModel.주문선택Async(orderNo);
        if (OrderSelected.HasDelegate)
        {
            await OrderSelected.InvokeAsync(orderNo);
        }
    }

    private Task RetryOrderAsync(string orderNo)
        => ViewModel.주문선택Async(orderNo);

    private Task RefreshOrderAsync(string orderNo)
        => ViewModel.주문진행새로고침Async();

    private Task ConfirmReceiptAsync()
        => ViewModel.주문수령확인Async();

    private async Task ClearSelectionAsync()
    {
        ViewModel.주문선택해제();
        if (OrderSelected.HasDelegate)
        {
            await OrderSelected.InvokeAsync(null);
        }
    }

    private async Task RefreshDriverLocationLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var locationState = Detail.상세?.기사위치.상태;
                if (!Authentication.로그인됨
                    || string.IsNullOrWhiteSpace(Detail.요청OrderNo)
                    || locationState is not (음식배달위치추적상태코드.추적중 or 음식배달위치추적상태코드.갱신지연))
                {
                    continue;
                }

                await InvokeAsync(() => ViewModel.주문진행새로고침Async(cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _locationRefreshCancellation.CancelAsync();
        if (_locationRefreshTask is not null)
        {
            try
            {
                await _locationRefreshTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _locationRefreshCancellation.Dispose();
    }
}
