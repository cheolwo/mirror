using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Food;

public partial class OrdererRestaurantWorkspace
{
    private bool _initialized;
    private bool _stopped;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    [Parameter]
    public long? RestaurantId { get; set; }

    [Parameter]
    public EventCallback<long?> RestaurantSelected { get; set; }

    [Parameter]
    public EventCallback<string> OrderSubmitted { get; set; }

    [Parameter]
    public EventCallback<bool> AuthenticationModeChanged { get; set; }

    private 음식배달페이지접근ViewModel Access => ViewModel.접근;
    private 주문자앱인증ViewModel Authentication => ViewModel.인증;
    private 음식점탐색기준ViewModel Criteria => ViewModel.기준;
    private 음식점공개목록ViewModel List => ViewModel.목록;
    private 음식점공개상세ViewModel Detail => ViewModel.상세;
    private 음식주문작성ViewModel Writer => ViewModel.작성;
    private bool BusinessActive => !_stopped && !ViewModel.인증화면표시;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ViewModel.PropertyChanged += OnDisplayModeChanged;
    }

    private void OnDisplayModeChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_stopped && args.PropertyName == nameof(음식점탐색PageViewModel.인증화면표시))
        {
            _ = InvokeAsync(async () =>
            {
                if (!_stopped)
                {
                    await AuthenticationModeChanged.InvokeAsync(ViewModel.인증화면표시);
                }
            });
        }
    }

    private Task OpenAuthenticationAsync()
    {
        if (!_stopped)
        {
            ViewModel.인증화면진입();
        }

        return Task.CompletedTask;
    }

    private Task ReturnToBusinessAsync()
    {
        if (!_stopped)
        {
            ViewModel.탐색화면복귀();
        }

        return Task.CompletedTask;
    }

    protected override async Task OnInitializedAsync()
    {
        await InitializeAsync();
        _initialized = !_stopped;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!BusinessActive || !_initialized || !Access.사용가능)
        {
            return;
        }

        if (RestaurantId is long restaurantId)
        {
            if (Detail.요청RestaurantId != restaurantId)
            {
                Writer.음식점설정(null);
                await Detail.조회Async(restaurantId, _lifetimeCancellation.Token);
                if (!_stopped)
                {
                    Writer.음식점설정(Detail.상세);
                }
            }
        }
        else if (Detail.요청RestaurantId.HasValue)
        {
            Detail.선택해제();
            Writer.음식점설정(null);
        }
    }

    private async Task InitializeAsync()
    {
        if (_stopped)
        {
            return;
        }

        var token = _lifetimeCancellation.Token;
        if (!await Access.확인Async(token) || _stopped || !Access.사용가능)
        {
            return;
        }

        var authenticationTask = Authentication.초기화됨
            ? Task.FromResult(true)
            : Authentication.복원Async(token);
        var criteriaTask = Criteria.초기화됨 ? Task.FromResult(true) : Criteria.준비Async(token);
        var detailTask = RestaurantId is long restaurantId
            ? Detail.조회Async(restaurantId, token)
            : Task.FromResult(true);
        await Task.WhenAll(authenticationTask, criteriaTask, detailTask);
        if (!_stopped)
        {
            Writer.음식점설정(Detail.상세);
        }
    }

    private Task PrepareCriteriaAsync()
        => !BusinessActive ? Task.CompletedTask : Criteria.준비Async(_lifetimeCancellation.Token);

    private Task SearchAsync()
        => !BusinessActive ? Task.CompletedTask : List.조회Async(Criteria.선택배달권키, Criteria.반경Km, _lifetimeCancellation.Token);

    private Task ReloadListAsync()
        => !BusinessActive ? Task.CompletedTask : List.새로고침Async(_lifetimeCancellation.Token);

    private Task ChangePageAsync(int page)
        => !BusinessActive ? Task.CompletedTask : List.페이지조회Async(page, _lifetimeCancellation.Token);

    private void SetRadius(double radiusKm)
    {
        if (BusinessActive)
        {
            Criteria.빠른반경설정(radiusKm);
        }
    }

    private async Task SelectRestaurantAsync(long restaurantId, bool updateAddress = true)
    {
        if (!BusinessActive)
        {
            return;
        }

        if (Detail.요청RestaurantId != restaurantId)
        {
            Writer.음식점설정(null);
        }

        if (updateAddress && RestaurantSelected.HasDelegate)
        {
            await RestaurantSelected.InvokeAsync(restaurantId);
        }

        if (!BusinessActive)
        {
            return;
        }

        await Detail.조회Async(restaurantId, _lifetimeCancellation.Token);
        if (!_stopped)
        {
            Writer.음식점설정(Detail.상세);
        }
    }

    private Task SelectRestaurantFromListAsync(long restaurantId)
        => SelectRestaurantAsync(restaurantId);

    private Task RetryRestaurantAsync(long restaurantId)
        => SelectRestaurantAsync(restaurantId, updateAddress: false);

    private Task LoginAsync(공통로그인요청 request)
        => _stopped || !ViewModel.인증화면표시 ? Task.CompletedTask : ViewModel.로그인Async(
            request.UserNameOrEmail,
            request.Password,
            _lifetimeCancellation.Token);

    private async Task SubmitOrderAsync()
    {
        if (_stopped)
        {
            return;
        }

        var page = ViewModel;
        if (!await page.주문등록Async(_lifetimeCancellation.Token) || _stopped)
        {
            return;
        }

        var orderNo = page.작성.등록응답?.주문번호;
        if (!string.IsNullOrWhiteSpace(orderNo) && OrderSubmitted.HasDelegate)
        {
            await OrderSubmitted.InvokeAsync(orderNo);
        }
    }

    private async Task ClearSelectionAsync()
    {
        if (!BusinessActive)
        {
            return;
        }

        Detail.선택해제();
        Writer.음식점설정(null);
        if (RestaurantSelected.HasDelegate)
        {
            await RestaurantSelected.InvokeAsync(null);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_stopped)
        {
            _stopped = true;
            ViewModel.PropertyChanged -= OnDisplayModeChanged;
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        base.Dispose(disposing);
    }
}
