using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Ssalddel.BatchMonitor.Windows;

public partial class MainWindow : Window
{
    private readonly BatchSimulation simulation = new();
    private readonly MixedObservation? observation = MixedObservation.Load(AppContext.BaseDirectory);
    private readonly Stopwatch stopwatch = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private BatchMonitorEvent? latestEvent;
    private ObservationEvent? latestDeliveryEvent;
    private ObservationEvent? latestFreightEvent;
    private string? latestFreightStatus;
    private NaverBatchMap? naverMap;
    private readonly string observationHeading;
    private readonly string observationDescription;
    private bool managementStarted;
    private bool mapReady;
    private string mapStatus = "네이버 지도 연결 준비 중";

    private string? DistrictNames => observation?.Delivery.DistrictStops is { Count: > 0 } stops
        ? string.Join(" · ", stops.Select(stop => stop.Name)) : null;

    public MainWindow()
    {
        InitializeComponent();
        if (observation is { } mixed)
        {
            Title = "Mirror · 음식·화물 지도 시뮬레이션 · Claude 작업본";
            PageHeading.Text = "길 구간 지도 · 음식·화물 시뮬레이션";
            PageDescription.Text = DistrictNames is { } districts
                ? $"{districts} · 가상 매장 {mixed.Delivery.StoreCount}곳 · 구간 화물 운송"
                : $"사가정 매장 {mixed.Delivery.StoreCount}곳 · 음식 주문 표본 {mixed.Delivery.SampleCount}건 · 화물과 배송지는 가상";
            ListHeading.Text = "음식·화물 현황";
            AssignmentHeading.Text = $"가상 음식 기사 {mixed.Delivery.Couriers.Count}명";
            OrderHeading.Text = "최근 가상 음식 주문";
            NoBatch.Visibility = Visibility.Collapsed;
            FreightStatus.Visibility = Visibility.Visible;
            OrderLegend.Text = "● 음식 배송지";
            RouteLegend.Text = "━ 음식 전달";
            WaitingLegend.Text = "┄ 매장으로 이동";
            CompletedLegend.Text = "■ 가상 화물 차량";
            CompletedLegend.Foreground = System.Windows.Media.Brushes.SaddleBrown;
            RouteMap.Visibility = Visibility.Collapsed;
            UnavailableMap.Visibility = Visibility.Visible;
            UnavailableMap.Text = "지도 연결을 기다리는 동안 음식과 화물은 왼쪽에서 계속 진행됩니다.";
        }
        else
        {
            PageDescription.Text = "기본 가상 배치 · 실제 매장·주문 표본은 연결되지 않았습니다.";
        }
        observationHeading = PageHeading.Text;
        observationDescription = PageDescription.Text;
        timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            stopwatch.Start();
            var launchOptions = Environment.GetCommandLineArgs();
            if (launchOptions.Contains("--management", StringComparer.Ordinal))
            {
                var practiceCount = launchOptions.Contains("--batch=2", StringComparer.Ordinal) ? 2 :
                    launchOptions.Contains("--batch=1", StringComparer.Ordinal) ? 1 : (int?)null;
                SetManagementMode(true, practiceCount);
            }
            Refresh();
            timer.Start();
            ConnectNaverMap();
        };
        Closed += (_, _) =>
        {
            timer.Stop();
            naverMap?.Dispose();
        };
    }

    private void Refresh()
    {
        if (ManagementPanel.IsVisible)
        {
            ManagementPanel.Advance();
            ClockText.Text = ManagementPanel.Now is { } now ? $"가상 {now:HH:mm:ss}" : "스테이지 대기";
            EventLog.Text = ManagementPanel.IsPlaying
                ? string.Join(Environment.NewLine, ManagementPanel.Events().TakeLast(3).Select(entry =>
                    $"{entry.At:HH:mm:ss}   {entry.Text}"))
                : "스테이지를 시작하면 이곳에 배차와 배달 사건이 표시됩니다.";
            UpdateManagementMapHeading();
            if (naverMap?.IsReady == true)
            {
                naverMap.Update(ManagementPanel.MapSnapshot());
            }
            return;
        }

        if (observation is { } mixed)
        {
            RefreshObservation(mixed);
            return;
        }

        simulation.Tick(stopwatch.Elapsed);
        ClockText.Text = $"경과 {FormatTime(simulation.Elapsed)}";
        if (naverMap?.IsReady == true)
        {
            naverMap.Update(simulation);
        }
        else
        {
            RouteMap.Update(simulation);
        }

        var currentEvent = simulation.Events.FirstOrDefault();
        if (latestEvent == currentEvent)
        {
            return;
        }

        latestEvent = currentEvent;
        Counters.Text = $"대기 {simulation.Orders.Count(order => order.Phase == BatchMonitorPhase.Waiting)}건 · " +
                        $"배치 {simulation.Batches.Count(batch => batch.Phase != BatchMonitorPhase.Completed)}개 · " +
                        $"완료 {simulation.Orders.Count(order => order.Phase == BatchMonitorPhase.Completed)}건";
        NoBatch.Visibility = simulation.Batches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BatchList.ItemsSource = simulation.Batches
            .Select(batch => $"B{batch.Number:00}  {batch.OrderNumbers.Count}건  ·  {Status(batch.Phase)}").ToArray();
        OrderList.ItemsSource = simulation.Orders.Select(order => new OrderRow(
            order.Number,
            order.BatchNumber is long batch ? $"주문 {order.Number:00} · B{batch:00}" : $"주문 {order.Number:00}",
            Status(order.Phase))).ToArray();
        EventLog.Text = string.Join(Environment.NewLine, simulation.Events.Take(3).Select(entry =>
            $"{FormatTime(entry.At)}   {entry.Message}"));
    }

    private void SwitchMode(object sender, RoutedEventArgs e) =>
        SetManagementMode(ManagementPanel.Visibility == Visibility.Collapsed);

    private void SetManagementMode(bool enter, int? initialPracticeCount = null)
    {
        if (enter && !managementStarted)
        {
            var sample = DeliverySimulation.Load(Path.Combine(AppContext.BaseDirectory, "Samples", "SeoulEast"));
            if (sample is null)
            {
                PageDescription.Text = "경영 체험에 필요한 서울 동북권 가상 도로 표본을 열 수 없습니다.";
                return;
            }
            ManagementPanel.Start(sample, FoodCampaignProgress.Load(), initialPracticeCount);
            managementStarted = true;
        }

        ManagementPanel.Visibility = enter ? Visibility.Visible : Visibility.Collapsed;
        ObservationPanel.Visibility = enter ? Visibility.Collapsed : Visibility.Visible;
        ManagementPanel.SetVisibleClock(enter);
        LeftColumn.Width = new GridLength(enter ? 372 : 282);
        ModeButton.Content = enter ? "기존 관찰" : "경영 체험";
        ModeIndicator.Text = enter ? "● 운영자 모드" : "● 자동 관찰 중";
        PageHeading.Text = enter ? "플랫폼 경영 체험" : observationHeading;
        PageDescription.Text = enter ? "가상 음식 주문을 배차하고 정산해 다음 지역을 여세요." : observationDescription;
        if (enter)
        {
            stopwatch.Stop();
            ManagementPanel.FocusEntry();
        }
        else
        {
            stopwatch.Start();
            latestEvent = null;
            latestDeliveryEvent = null;
        }
        ShowMapStatus(mapReady, mapStatus);
        Refresh();
    }

    private void RefreshObservation(MixedObservation mixed)
    {
        mixed.Tick(stopwatch.Elapsed);
        var food = mixed.Delivery;
        var freight = mixed.Freight;
        ClockText.Text = $"경과 {FormatTime(food.Elapsed)}";
        if (naverMap?.IsReady == true)
        {
            naverMap.Update(mixed.MapSnapshot());
        }

        var foodEvent = food.Events.FirstOrDefault();
        var freightEvent = freight?.Events.FirstOrDefault();
        var freightStatus = freight?.CurrentStatus;
        if (latestDeliveryEvent == foodEvent && latestFreightEvent == freightEvent && latestFreightStatus == freightStatus && foodEvent is not null)
        {
            return;
        }

        latestDeliveryEvent = foodEvent;
        latestFreightEvent = freightEvent;
        latestFreightStatus = freightStatus;
        var waiting = food.Orders.Count(order => order.Phase == DeliveryPhase.Waiting);
        var moving = food.Couriers.Count(courier => courier.Order is not null);
        var awaitingReceipt = food.Orders.Count(order => order.Phase == DeliveryPhase.Delivered);
        Counters.Text = $"음식 대기 {waiting} · 진행 {moving} · 수령 대기 {awaitingReceipt}\n음식 완료 누계 {food.CompletedCount}건";
        FreightStatus.Text = freight is null ? "화물 경로 표본 미확인"
            : $"화물 · {freightStatus ?? "다음 의뢰 준비"}\n" +
              (freight.CurrentRoute is { } route ? $"{route}\n" : "") +
              $"화물 인수 누계 {freight.CompletedCount}건";
        BatchList.ItemsSource = food.Couriers.Select(courier => courier.Order is { } order
            ? $"{courier.Label}   D-{order.Number:00} · {Status(order.Phase)}" +
              (courier.District is null ? "" : $"\n{order.Store.Name}")
            : $"{courier.Label}   배차 대기").ToArray();
        OrderList.ItemsSource = food.Orders.OrderByDescending(order => order.Number).Take(8).Select(order =>
            new OrderRow(order.Number, $"{order.Store.Name}\n{order.Menu}" +
                (order.ObservedTime is { Length: > 0 } hour ? $"\n기록 {hour}" : ""), Status(order.Phase))).ToArray();
        EventLog.Text = string.Join(Environment.NewLine, mixed.LatestEvents(3).Select(entry =>
            $"{FormatTime(entry.At)}   {entry.Message}"));
    }

    private async void ConnectNaverMap()
    {
        var clientId = NaverBatchMap.FindClientId();
        if (clientId is null)
        {
            ShowMapStatus(false, "네이버 지도 설정을 찾을 수 없습니다.");
            return;
        }

        naverMap = new(MapSurface, ShowMapStatus, (kind, id) =>
        {
            if (ManagementPanel.IsVisible)
            {
                ManagementPanel.HandleMapInput(kind, id);
                Refresh();
            }
        });
        await naverMap.ConnectAsync(clientId);
    }

    private void ShowMapStatus(bool ready, string description)
    {
        mapReady = ready;
        mapStatus = description;
        if (ManagementPanel.Visibility == Visibility.Visible)
        {
            UpdateManagementMapHeading();
            RouteMap.Visibility = Visibility.Collapsed;
            UnavailableMap.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
            UnavailableMap.Text = "왼쪽에서 기사 지정과 가상 결산을 계속 진행할 수 있습니다.";
            RouteCredit.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
            CompletedLegend.Visibility = Visibility.Collapsed;
            return;
        }

        var area = observation?.Delivery.DistrictStops is { Count: > 0 } stops
            ? $"서울 동북권 {stops.Count}개 구" : "사가정";
        MapHeading.Text = ready ? $"{area} · 네이버 지도" : "가상 관찰 구역";
        MapDescription.Text = ready && observation is not null
            ? DistrictNames is null
                ? "원은 음식 배송지 · 주황색 차량과 선은 화물 · 완료 8초 뒤 정리"
                : "원은 음식 배송지 · 구별 현재 픽업 매장 · 주황색 차량과 선은 화물"
            : !ready && observation is null ? $"{description} 가상 지도를 표시합니다." : description;
        RouteMap.Visibility = !ready && observation is null ? Visibility.Visible : Visibility.Collapsed;
        UnavailableMap.Visibility = !ready && observation is not null ? Visibility.Visible : Visibility.Collapsed;
        if (!ready && observation is not null)
        {
            UnavailableMap.Text = "음식과 화물의 진행은 왼쪽 목록에서 계속 확인할 수 있습니다.";
        }
        RouteCredit.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        CompletedLegend.Visibility = Visibility.Visible;
    }

    private void UpdateManagementMapHeading()
    {
        MapHeading.Text = mapReady ? $"{ManagementPanel.MapArea} · 네이버 지도" : ManagementPanel.MapArea;
        MapDescription.Text = !mapReady ? mapStatus :
            ManagementPanel.CanExpand ? "지도를 한 단계 축소해 다음 확장 방향을 고르세요." :
            ManagementPanel.IsPlaying ? "기사 마커를 고르고 새 주문이 뜬 매장 핀을 눌러 배차" :
            "선택한 스테이지의 가상 매장 · 시작하면 주문과 기사가 표시됩니다.";
    }

    private static string Status(DeliveryPhase phase) => phase switch
    {
        DeliveryPhase.Waiting => "배차 대기",
        DeliveryPhase.ToPickup => "매장 이동",
        DeliveryPhase.AtPickup => "픽업 대기",
        DeliveryPhase.ToDropoff => "전달 이동",
        DeliveryPhase.Delivered => "수령 대기",
        DeliveryPhase.Completed => "수령 확인",
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    private static string Status(BatchMonitorPhase phase) => phase switch
    {
        BatchMonitorPhase.Waiting => "접수 대기",
        BatchMonitorPhase.Grouped => "묶음 준비",
        BatchMonitorPhase.InTransit => "이동 중",
        BatchMonitorPhase.Completed => "완료",
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    private static string FormatTime(TimeSpan elapsed) => $"{(long)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

    private sealed record OrderRow(long Number, string Detail, string Status);
}
