using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Color = MudBlazor.Color;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp.Components.Pages;

public partial class OrderDetail : ComponentBase, IDisposable
{
    [Inject] public I음식점주문DeskService OrderDeskService { get; set; } = default!;
    [Inject] public I음식점주문SignalRClientService RestaurantRealtimeService { get; set; } = default!;
    [Inject] public ISsalddelDocumentOutputService DocumentOutputService { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public NavigationManager NavigationManager { get; set; } = default!;
    [Parameter]
    public string OrderNo { get; set; } = string.Empty;

    private 음식점주문DeskItem? order;
    private static readonly int[] QuickPreparationMinutes = [10, 15, 20, 30, 45];
    private string? loadedOrderNo;
    private int preparationMinutes = 20;
    private int? serverPreparationMinutes;
    private string rejectionReason = string.Empty;
    private bool isBusy;
    private bool isLoading;
    private bool isRefreshing;
    private bool refreshQueued;
    private bool readFailed;
    private bool workLocked;
    private DateTimeOffset? lastSuccessfulRefresh;
    private string? message;
    private Severity messageSeverity = Severity.Info;
    private CancellationTokenSource selectionCancellation = new();
    private long selectionGeneration;
    private string? selectionOrderNo;
    private bool disposed;
    private bool realtimeConnectionAttempted;
    private bool realtimeConnectionInFlight;
    private long realtimeConnectionGeneration;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private Task? periodicRefreshTask;

    protected override void OnInitialized()
    {
        RestaurantRealtimeService.주문수신 += HandleOrderReceivedAsync;
        RestaurantRealtimeService.주문상태변경 += HandleOrderChangedAsync;
        RestaurantRealtimeService.재연결후재조회요청 += HandleReconnectedAsync;
        RestaurantRealtimeService.상태변경 += HandleConnectionChangedAsync;
        periodicRefreshTask = RunPeriodicRefreshAsync(lifetimeCancellation.Token);
    }

    private Task HandleOrderReceivedAsync(음식점주문수신알림 notification)
        => RefreshNotifiedOrderAsync(notification.주문번호);

    private Task HandleOrderChangedAsync(음식점주문상태변경알림 notification)
        => RefreshNotifiedOrderAsync(notification.주문번호);

    private Task RefreshNotifiedOrderAsync(string orderNo)
        => InvokeAsync(async () =>
        {
            if (disposed || workLocked || !string.Equals(OrderNo, orderNo, StringComparison.Ordinal)) return;
            await RefreshCoreAsync(automatic: true);
            if (!disposed) StateHasChanged();
        });

    private Task HandleReconnectedAsync()
        => InvokeAsync(async () =>
        {
            if (disposed || workLocked) return;
            await RefreshCoreAsync(automatic: true);
            if (!disposed) StateHasChanged();
        });

    private Task HandleConnectionChangedAsync(음식점실시간연결상태변경 change)
        => InvokeAsync(() =>
        {
            if (disposed) return;
            if (change.상태 == 음식점실시간연결상태.연결끊김)
            {
                realtimeConnectionAttempted = false;
                return;
            }
            if (change.상태 != 음식점실시간연결상태.인증필요) return;
            selectionCancellation.Cancel();
            HandleRequestFailure(new UnauthorizedAccessException(change.안내), string.Empty);
            StateHasChanged();
        });

    private async Task ConnectRealtimeAsync()
    {
        // A hub callback may requery while this connection is starting; it must not await that same attempt.
        if (disposed || workLocked || realtimeConnectionAttempted || realtimeConnectionInFlight) return;
        var generation = ++realtimeConnectionGeneration;
        realtimeConnectionInFlight = true;
        realtimeConnectionAttempted = true;
        try
        {
            await RestaurantRealtimeService.연결Async(lifetimeCancellation.Token);
            if (!disposed && generation == realtimeConnectionGeneration && !workLocked)
                realtimeConnectionAttempted = RestaurantRealtimeService.연결상태 == 음식점실시간연결상태.연결됨;
        }
        catch (Exception) when (disposed || lifetimeCancellation.IsCancellationRequested || generation != realtimeConnectionGeneration) { }
        catch (UnauthorizedAccessException ex)
        {
            selectionCancellation.Cancel();
            HandleRequestFailure(ex, string.Empty);
        }
        catch (Exception)
        {
            // A disconnected hub must not suppress canonical periodic reads.
            realtimeConnectionAttempted = false;
        }
        finally
        {
            if (!disposed && generation == realtimeConnectionGeneration) realtimeConnectionInFlight = false;
        }
    }

    private async Task RunPeriodicRefreshAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    if (disposed || workLocked) return;
                    await RefreshCoreAsync(automatic: true);
                    if (!disposed) StateHasChanged();
                });
            }
        }
        catch (Exception) when (disposed || cancellationToken.IsCancellationRequested) { }
    }

    private Task DrainQueuedRefreshAsync()
        => refreshQueued && !disposed && !workLocked
            ? RefreshCoreAsync(automatic: true) : Task.CompletedTask;

    private bool IsCurrentSelection(string orderNo, long generation)
        => !disposed && generation == selectionGeneration
            && string.Equals(OrderNo, orderNo, StringComparison.Ordinal);

    protected override async Task OnParametersSetAsync()
    {
        if (disposed || string.Equals(selectionOrderNo, OrderNo, StringComparison.Ordinal)) return;
        selectionCancellation.Cancel();
        selectionCancellation.Dispose();
        selectionCancellation = new CancellationTokenSource();
        var cancellationToken = selectionCancellation.Token;
        var generation = ++selectionGeneration;
        var requestedOrderNo = OrderNo;
        if (!string.Equals(selectionOrderNo, OrderNo, StringComparison.Ordinal))
        {
            order = null;
            message = null;
            rejectionReason = string.Empty;
            lastSuccessfulRefresh = null;
            isBusy = false;
            isRefreshing = false;
            refreshQueued = false;
            readFailed = false;
            workLocked = false;
            loadedOrderNo = null;
            serverPreparationMinutes = null;
            preparationMinutes = 20;
        }
        selectionOrderNo = requestedOrderNo;
        isLoading = true;
        try
        {
            var loaded = await OrderDeskService.주문조회Async(requestedOrderNo, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentSelection(requestedOrderNo, generation)) return;
            ApplyServerOrder(loaded);
            readFailed = false;
            workLocked = false;
            lastSuccessfulRefresh = DateTimeOffset.UtcNow;
            message = null;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested || !IsCurrentSelection(requestedOrderNo, generation))
        {
        }
        catch (Exception ex)
        {
            HandleRequestFailure(ex, "주문을 불러오지 못했습니다. 새로고침으로 다시 확인해 주세요.");
        }
        finally
        {
            if (IsCurrentSelection(requestedOrderNo, generation))
            {
                isLoading = false;
                await DrainQueuedRefreshAsync();
            }
        }
        if (IsCurrentSelection(requestedOrderNo, generation)) await ConnectRealtimeAsync();
    }

    private bool WorkDisabled => isBusy || isLoading || readFailed || workLocked || disposed || isRefreshing;

    private void ApplyServerOrder(음식점주문DeskItem? loaded)
    {
        order = loaded;
        if (loaded is null) return;
        var latestMinutes = 음식점조리시간정책.Clamp(loaded.선택조리예상분
            ?? (loaded.추천조리예상분 > 0 ? loaded.추천조리예상분 : 20));
        if (!string.Equals(loadedOrderNo, loaded.주문번호, StringComparison.Ordinal)
            || preparationMinutes == serverPreparationMinutes)
        {
            preparationMinutes = latestMinutes;
        }
        serverPreparationMinutes = latestMinutes;
        loadedOrderNo = loaded.주문번호;
    }

    private string CurrentStepLabel => readFailed ? "최신 상태 확인이 필요해요"
        : order is null ? "주문 확인 중"
        : order.배차상태 == 음식주문배차상태코드.배차불가 ? "배차 확인이 필요해요"
        : order.수락가능 ? "주문 확인이 필요해요"
        : order.조리시작가능 ? "조리를 시작해 주세요"
        : order.픽업준비가능 ? "조리 중이에요"
        : OrderStatusLabel(order.상태);

    private string NextStepGuide => readFailed ? "상태를 다시 확인한 뒤 진행해 주세요."
        : order is null ? "주문을 불러오고 있어요."
        : order.배차상태 == 음식주문배차상태코드.배차불가 ? "기사 배정에 문제가 있어요. 상세 정보의 배차 상태와 최근 안내를 확인해 주세요."
        : order.수락가능 ? "메뉴와 조리 예상시간을 확인하고 주문을 접수해 주세요."
        : order.조리시작가능 ? "기사가 배정됐어요. 조리를 시작할 수 있습니다."
        : order.픽업준비가능 ? "조리가 끝나면 픽업 준비 완료를 눌러 주세요."
        : order.조리변경가능 && order.상세주문?.조리시작시각Utc.HasValue == true ? "조리는 시작됐어요. 기사 재배정을 기다리고 있습니다."
        : order.조리변경가능 ? "기사 배정을 기다리고 있어요. 조리는 배정 후 시작해 주세요."
        : order.상태 == 음식점주문Desk상태코드.픽업대기 ? "기사가 음식을 픽업할 때까지 기다려 주세요."
        : order.상태 == 음식점주문Desk상태코드.픽업완료 ? "기사가 음식을 전달하고 있어요."
        : order.상태 == 음식점주문Desk상태코드.전달완료 ? "음식 전달이 완료됐어요. 수령 확인을 기다리고 있습니다."
        : order.상태 == 음식점주문Desk상태코드.수령확인 ? "주문자가 음식을 받았어요. 이 주문은 완료됐습니다."
        : order.상태 == 음식점주문Desk상태코드.거절 ? "거절된 주문이에요."
        : order.상태 == 음식점주문Desk상태코드.취소 ? "취소된 주문이에요."
        : "현재는 추가 작업이 없습니다. 새로고침으로 상태를 확인할 수 있어요.";

    private static string OrderStatusLabel(string status) => status switch
    {
        음식점주문Desk상태코드.주문대기 => "주문 확인 필요",
        "주문확인" => "기사 배정 대기",
        음식점주문Desk상태코드.수락처리중 => "주문 확인 중",
        음식점주문Desk상태코드.기사배정 => "기사 배정 완료",
        음식점주문Desk상태코드.조리중 => "조리 중",
        음식점주문Desk상태코드.픽업대기 => "픽업 대기",
        음식점주문Desk상태코드.픽업완료 => "배달 중",
        음식점주문Desk상태코드.전달완료 => "수령 확인 대기",
        음식점주문Desk상태코드.수령확인 => "주문 완료",
        음식점주문Desk상태코드.거절 => "주문 거절",
        음식점주문Desk상태코드.취소 => "주문 취소",
        _ => status
    };

    private void SetPreparationMinutes(int minutes)
        => preparationMinutes = 음식점조리시간정책.Clamp(minutes);

    private Task RefreshAsync() => RefreshCoreAsync(automatic: false);

    private async Task RefreshCoreAsync(bool automatic)
    {
        if (disposed || (automatic && workLocked) || string.IsNullOrWhiteSpace(selectionOrderNo)) return;
        if (isBusy || isLoading || isRefreshing)
        {
            if (automatic) refreshQueued = true;
            return;
        }

        isRefreshing = true;
        var requestedOrderNo = OrderNo;
        var generation = selectionGeneration;
        var cancellationToken = selectionCancellation.Token;
        try
        {
            do
            {
                refreshQueued = false;
                var loaded = await OrderDeskService.주문조회Async(requestedOrderNo, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentSelection(requestedOrderNo, generation)) return;
                var recovering = readFailed;
                ApplyServerOrder(loaded);
                readFailed = false;
                workLocked = false;
                lastSuccessfulRefresh = DateTimeOffset.UtcNow;
                if (!automatic || recovering || order is null)
                {
                    messageSeverity = order is null ? Severity.Warning : Severity.Info;
                    message = order is null ? "주문을 찾지 못했습니다. 주문 목록에서 다시 확인해 주세요." : null;
                }
            } while (refreshQueued && !workLocked);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested || !IsCurrentSelection(requestedOrderNo, generation))
        {
        }
        catch (Exception ex)
        {
            HandleRequestFailure(ex, "최신 상태를 확인하지 못했습니다. 마지막 확인 내용을 표시하고 있어요. 새로고침으로 다시 확인해 주세요.");
        }
        finally
        {
            if (IsCurrentSelection(requestedOrderNo, generation)) isRefreshing = false;
        }
        if (!readFailed && IsCurrentSelection(requestedOrderNo, generation)) await ConnectRealtimeAsync();
    }

    private async Task AcceptAndPrintAsync()
    {
        if (order is null || WorkDisabled)
        {
            return;
        }

        isBusy = true;
        message = null;
        var requestedOrderNo = OrderNo;
        var generation = selectionGeneration;
        var cancellationToken = selectionCancellation.Token;
        try
        {
            preparationMinutes = 음식점조리시간정책.Clamp(preparationMinutes);
            var result = await OrderDeskService.주문수락후전표준비Async(
                requestedOrderNo,
                preparationMinutes,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentSelection(requestedOrderNo, generation)) return;
            if (!result.성공 || result.전표Draft is null)
            {
                readFailed = true;
                messageSeverity = Severity.Error;
                message = $"{result.메시지} 새로고침으로 최신 상태를 확인해 주세요.";
                return;
            }

            var output = DocumentOutputService.CreateOutboundExpectedItems(result.전표Draft);
            try
            {
                await JS.InvokeVoidAsync("ssalddelDocumentOutput.printHtml", cancellationToken, output.Title, output.Html);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentSelection(requestedOrderNo, generation)) return;
                await OrderDeskService.전표출력완료Async(requestedOrderNo, cancellationToken);
            }
            catch (JSException)
            {
                var loaded = await OrderDeskService.주문조회Async(requestedOrderNo, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentSelection(requestedOrderNo, generation)) return;
                ApplyServerOrder(loaded);
                messageSeverity = Severity.Warning;
                message = $"{OrderNo} 주문을 확인해 배차를 요청했습니다. 기사 배정 후 조리를 시작해 주세요. 이 기기에서는 전표 출력 창을 열지 못했습니다.";
                return;
            }

            var latest = await OrderDeskService.주문조회Async(requestedOrderNo, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentSelection(requestedOrderNo, generation)) return;
            ApplyServerOrder(latest);
            messageSeverity = Severity.Success;
            message = $"{OrderNo} 주문을 확인해 배차를 요청하고 전표 출력 요청을 보냈습니다.";
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested || !IsCurrentSelection(requestedOrderNo, generation))
        {
        }
        catch (Exception ex)
        {
            HandleRequestFailure(ex, "주문 확인 결과를 확인하지 못했습니다. 중복 처리하지 않도록 새로고침 후 현재 상태를 확인해 주세요.");
        }
        finally
        {
            if (IsCurrentSelection(requestedOrderNo, generation))
            {
                isBusy = false;
                await DrainQueuedRefreshAsync();
            }
        }
    }

    private async Task RejectAsync()
    {
        if (order is null || WorkDisabled || string.IsNullOrWhiteSpace(rejectionReason))
        {
            return;
        }

        await RunProgressAsync(
            token => OrderDeskService.주문거절Async(order.주문번호, rejectionReason, token),
            "주문을 거절했습니다.");
    }

    private async Task StartCookingAsync()
    {
        if (order is null || WorkDisabled || !order.조리시작가능) return;
        await RunProgressAsync(
            token => OrderDeskService.조리시작Async(order.주문번호, preparationMinutes, token),
            "조리를 시작했습니다.");
    }

    private async Task UpdateCookingTimeAsync()
    {
        if (order is null || WorkDisabled)
        {
            return;
        }

        preparationMinutes = 음식점조리시간정책.Clamp(preparationMinutes);
        await RunProgressAsync(
            token => OrderDeskService.조리시간변경Async(order.주문번호, preparationMinutes, token),
            $"조리 예상 시간을 {preparationMinutes}분으로 변경했습니다.");
    }

    private async Task MarkPickupReadyAsync()
    {
        if (order is null || WorkDisabled)
        {
            return;
        }

        await RunProgressAsync(
            token => OrderDeskService.픽업준비완료Async(order.주문번호, token),
            "픽업 준비가 완료됐습니다.");
    }

    private async Task RunProgressAsync(
        Func<CancellationToken, Task<음식점주문DeskItem?>> action,
        string successMessage)
    {
        isBusy = true;
        message = null;
        var requestedOrderNo = OrderNo;
        var generation = selectionGeneration;
        var cancellationToken = selectionCancellation.Token;
        try
        {
            var updated = await action(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentSelection(requestedOrderNo, generation)) return;
            if (updated is null)
            {
                readFailed = true;
                messageSeverity = Severity.Warning;
                message = "서버에서 이 주문을 찾지 못했습니다. 주문 목록이나 새로고침으로 다시 확인해 주세요.";
                return;
            }

            ApplyServerOrder(updated);
            messageSeverity = Severity.Success;
            message = successMessage;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested || !IsCurrentSelection(requestedOrderNo, generation))
        {
        }
        catch (Exception ex)
        {
            HandleRequestFailure(ex, "주문 진행 결과를 확인하지 못했습니다. 중복 처리하지 않도록 새로고침 후 현재 상태를 확인해 주세요.");
        }
        finally
        {
            if (IsCurrentSelection(requestedOrderNo, generation))
            {
                isBusy = false;
                await DrainQueuedRefreshAsync();
            }
        }
    }

    private void HandleRequestFailure(Exception exception, string retryMessage)
    {
        readFailed = true;
        messageSeverity = Severity.Error;
        if (exception is UnauthorizedAccessException
            || exception is SsalddelApiException { StatusCode: 401 }
            || exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
        {
            workLocked = true;
            order = null;
            message = "로그인이 만료되었습니다. 다시 로그인해 주세요.";
            NavigationManager.NavigateTo("/login", replace: true);
        }
        else if (exception is SsalddelApiException { StatusCode: 403 }
            || exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden })
        {
            workLocked = true;
            order = null;
            message = "이 계정은 현재 음식점 주문을 처리할 수 없습니다. 음식점 운영 권한을 확인해 주세요.";
        }
        else
        {
            message = retryMessage;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        realtimeConnectionGeneration++;
        realtimeConnectionInFlight = false;
        RestaurantRealtimeService.주문수신 -= HandleOrderReceivedAsync;
        RestaurantRealtimeService.주문상태변경 -= HandleOrderChangedAsync;
        RestaurantRealtimeService.재연결후재조회요청 -= HandleReconnectedAsync;
        RestaurantRealtimeService.상태변경 -= HandleConnectionChangedAsync;
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        selectionGeneration++;
        selectionCancellation.Cancel();
        selectionCancellation.Dispose();
    }

    private static Color StatusColor(string status) => status switch
    {
        음식점주문Desk상태코드.주문대기 => Color.Error,
        음식점주문Desk상태코드.수락처리중 => Color.Info,
        음식점주문Desk상태코드.조리중 => Color.Primary,
        음식점주문Desk상태코드.픽업대기 => Color.Success,
        음식점주문Desk상태코드.거절 or 음식점주문Desk상태코드.취소 => Color.Error,
        음식점주문Desk상태코드.기사배정 or 음식점주문Desk상태코드.픽업완료 => Color.Info,
        음식점주문Desk상태코드.전달완료 or 음식점주문Desk상태코드.수령확인 => Color.Success,
        음식점주문Desk상태코드.전표출력됨 => Color.Success,
        음식점주문Desk상태코드.상세조회실패 => Color.Warning,
        _ => Color.Default
    };

    private static Severity StatusAlertSeverity(string status) => status switch
    {
        음식점주문Desk상태코드.거절 or 음식점주문Desk상태코드.취소 => Severity.Error,
        음식점주문Desk상태코드.전달완료 or 음식점주문Desk상태코드.수령확인 => Severity.Success,
        _ => Severity.Info
    };

    private static string DispatchStatusLabel(string? status) => status switch
    {
        음식주문배차상태코드.배차대기 => "기사 제안 대기",
        음식주문배차상태코드.추천중 => "기사 추천 중",
        음식주문배차상태코드.기사배정 => "기사 배정 완료",
        음식주문배차상태코드.배달중 => "배달 중",
        음식주문배차상태코드.배달완료 => "배달 완료",
        음식주문배차상태코드.배차불가 => "배차 확인 필요",
        _ => "배차 전"
    };

    private static Color DispatchStatusColor(string? status) => status switch
    {
        음식주문배차상태코드.배달완료 => Color.Success,
        음식주문배차상태코드.기사배정 or 음식주문배차상태코드.배달중 => Color.Primary,
        음식주문배차상태코드.배차불가 => Color.Error,
        _ => Color.Warning
    };

    private static string FormatOptionalDate(DateTime? value)
        => value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "확인 중";
}
