using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Ssalddel.BatchMonitor.Windows;

public partial class FoodManagementPanel : UserControl
{
    private const int ClockScale = 12;
    private readonly Stopwatch clock = new();
    private Func<int, FoodManagementRound?> createPractice = null!;
    private Func<FoodCampaignStage, FoodManagementRound?> createStage = null!;
    private FoodCampaignProgress progress = null!;
    private DeliveryStore[] stores = [];
    private FoodManagementRound? activeRound;
    private IReadOnlyDictionary<string, string> driverLabels = new Dictionary<string, string>();
    private ExpansionRow[] expansionRows = [];
    private FoodManagementPreview? preview;
    private string? previousResult;
    private bool resultSaved;
    private (long Revision, string OrderId)? rendered;

    public FoodManagementPanel() => InitializeComponent();

    internal DateTime? Now => activeRound?.Now;
    internal string MapArea => (activeRound?.CampaignStage ?? (activeRound is null ? SelectedStage?.Stage : null)) is { } stage
        ? DistrictNames(stage) : "중랑구 사가정";
    internal bool IsPlaying => activeRound is not null;
    internal bool CanExpand => expansionRows.Length > 0;
    private StageRow? SelectedStage => StagePicker.SelectedItem as StageRow;
    private FoodManagementRound Round => activeRound ?? throw new InvalidOperationException("No food delivery round is active.");
    private FoodManagementSession CurrentOrder => Round.Orders[Math.Max(0, OrderPicker.SelectedIndex)];

    internal void Start(DeliverySimulation sample, FoodCampaignProgress campaignProgress, int? initialPracticeCount)
    {
        stores = sample.Sample.Stores;
        createPractice = count => FoodManagementRound.Create(sample.RoadNetwork, count);
        createStage = stage => FoodManagementRound.CreateCampaign(sample.RoadNetwork, sample.Sample, stage);
        progress = campaignProgress;
        ShowStageSelection(progress.UnlockedStages.FirstOrDefault(stage => stage.Id == progress.LastStartedStageId));
        if (initialPracticeCount is int count)
        {
            BeginPractice(count);
        }
    }

    internal void SetVisibleClock(bool visible)
    {
        if (visible && activeRound is { IsSettled: false } round &&
            (round.CampaignStage is not null || round.BatchCount > 1 || CurrentOrder.Stage != FoodManagementStage.Waiting))
        {
            clock.Start();
        }
        else
        {
            clock.Stop();
        }
    }

    internal void Advance()
    {
        if (activeRound is not { } round) return;
        round.Tick(clock.Elapsed * ClockScale);
        if (round.IsSettled)
        {
            clock.Stop();
        }
        RenderRound();
    }

    internal IEnumerable<FoodManagementEvent> Events() => activeRound is not { } round ? [] :
        round.Orders.SelectMany(order => order.Events.Select(item => round.BatchCount == 1 ? item :
            item with { Text = $"{order.OrderId} · {item.Text}" })).OrderBy(item => item.At);

    internal DeliveryMapSnapshot MapSnapshot()
    {
        if (activeRound is not { } round)
        {
            var stage = SelectedStage?.Stage;
            var restaurants = stores.Where(store => stage?.Districts.Any(district => district.District == store.District) == true)
                .ToArray();
            return new([], restaurants, [], [], ViewScope: "food-management", MapFitKey: $"preview:{stage?.Id}");
        }

        var waiting = round.Orders.Where(order => order.Stage == FoodManagementStage.Waiting).ToArray();
        var districts = waiting.Select(order => order.District).ToHashSet();
        var snapshot = round.MapSnapshot();
        var availableDrivers = snapshot.Couriers.Where(driver => driver.PickupStoreId is null && districts.Contains(driver.District))
            .Select(driver => driver.Id).ToArray();
        return snapshot with
        {
            ViewScope = "food-management",
            MapFitKey = round.CampaignStage?.Id ?? "practice",
            Management = new(waiting.Select(order => order.PickupStoreId).ToArray(),
                (Candidates.SelectedItem as CandidateRow)?.DriverId, waiting.Length > 0, availableDrivers,
                expansionRows.Select(row => row.Pin).OfType<FoodManagementExpansionPin>().ToArray()),
        };
    }

    internal void HandleMapInput(string kind, string id)
    {
        if (activeRound is not { } round) return;
        if (kind == "managementExpansion")
        {
            ChooseExpansion(id);
            return;
        }
        if (kind == "managementDriver")
        {
            var waiting = round.Orders.Where(order => order.Stage == FoodManagementStage.Waiting)
                .OrderByDescending(order => order == CurrentOrder)
                .FirstOrDefault(order => order.Preview().Candidates.Any(candidate => candidate.DriverId == id && candidate.Eligible));
            if (waiting is null)
            {
                Feedback.Text = "이 기사가 지금 맡을 수 있는 대기 주문이 없습니다.";
                return;
            }

            SelectOrder(waiting);
            ShowCandidates(id);
            if (Candidates.SelectedItem is CandidateRow { Eligible: true } driver)
            {
                Feedback.Text = $"{driver.Title} 선택 · 새 주문이 뜬 매장 핀을 누르세요.";
            }
            else
            {
                Feedback.Text = preview?.Candidates.FirstOrDefault(candidate => candidate.DriverId == id)?.Reason
                    ?? "현재 배차할 수 있는 기사가 아닙니다.";
            }
            return;
        }

        var store = round.Orders.FirstOrDefault(order =>
            order.PickupStoreId == id && order.Stage == FoodManagementStage.Waiting);
        if (store is null) return;

        var driverId = (Candidates.SelectedItem as CandidateRow)?.DriverId;
        SelectOrder(store);
        ShowCandidates(driverId);
        if (Candidates.SelectedItem is CandidateRow)
        {
            AssignSelected();
        }
        else
        {
            Feedback.Text = driverId is null ? "먼저 기사 마커 또는 목록에서 기사를 고르세요."
                : "선택한 기사는 이 주문에 배정할 수 없습니다. 후보를 다시 확인하세요.";
        }
    }

    private void SelectOrder(FoodManagementSession order) =>
        OrderPicker.SelectedItem = OrderPicker.Items.OfType<OrderRow>().Single(row => row.OrderId == order.OrderId);

    internal void FocusEntry()
    {
        if (activeRound is null)
        {
            StartStageButton.Focus();
        }
        else if (OpenOrderButton.IsEnabled)
        {
            OpenOrderButton.Focus();
        }
        else if (RestartButton.IsVisible)
        {
            RestartButton.Focus();
        }
    }

    private void OpenOrder(object sender, RoutedEventArgs e)
    {
        ShowCandidates();
        Candidates.Focus();
    }

    private void ShowCandidates(string? selectedDriverId = null)
    {
        var current = CurrentOrder.Preview();
        preview = current;
        var rows = current.Candidates.Select(candidate =>
        {
            var recommended = candidate.DriverId == current.RecommendedDriverId;
            var title = recommended ? $"{candidate.Label} · 추천" : candidate.Label;
            var reason = recommended && candidate.Reason == "적정 픽업창"
                ? "추천 이유 · 적정 픽업창에서 음식 준비 시각과의 차이를 우선 비교"
                : candidate.Reason;
            var detail = candidate.Eligible && candidate.ArrivalAt is { } arrival
                ? $"{candidate.PickupKilometers:0.00}km · {arrival:HH:mm:ss} 도착 · {ReadyDifference(candidate.ReadyDifference)}\n{reason}"
                : reason;
            return new CandidateRow(candidate.DriverId, title, detail, candidate.Eligible);
        }).ToArray();
        Candidates.ItemsSource = rows;
        Candidates.SelectedItem = rows.FirstOrDefault(row => row.DriverId == selectedDriverId && row.Eligible);
        CandidatePanel.Visibility = Visibility.Visible;
    }

    private void SelectCandidate(object sender, SelectionChangedEventArgs e) =>
        AssignButton.IsEnabled = activeRound is not null && Candidates.SelectedItem is CandidateRow { Eligible: true } &&
            CurrentOrder.Stage == FoodManagementStage.Waiting;

    private void AssignDriver(object sender, RoutedEventArgs e) => AssignSelected();

    private void AssignSelected()
    {
        if (preview is null || Candidates.SelectedItem is not CandidateRow candidate)
        {
            return;
        }

        // Recheck against the current virtual clock and driver capacity before applying the command.
        Round.Tick(clock.Elapsed * ClockScale);
        var result = Round.Assign(Guid.NewGuid().ToString("N"), CurrentOrder.OrderId, candidate.DriverId, preview.Revision);
        Feedback.Text = result.Message;
        if (!result.Accepted)
        {
            ShowCandidates();
            return;
        }

        clock.Start();
        ClearCandidates();
        RenderRound();
    }

    private void ChangeOrder(object sender, SelectionChangedEventArgs e)
    {
        if (activeRound is null || OrderPicker.SelectedIndex < 0) return;
        ClearCandidates();
        Feedback.Text = "";
        RenderRound();
    }

    private void ShowStageSelection(FoodCampaignStage? target = null)
    {
        clock.Reset();
        activeRound = null;
        SetExpansionRows([]);
        ClearCandidates();
        previousResult = null;
        RoundPanel.Visibility = Visibility.Collapsed;
        StageSelection.Visibility = Visibility.Visible;
        var stages = FoodCampaignStage.All.Concat(progress.UnlockedStages).DistinctBy(stage => stage.Id)
            .OrderBy(stage => stage.Districts.Count);
        StagePicker.ItemsSource = stages.Select(stage =>
        {
            var unlocked = progress.IsUnlocked(stage);
            var status = progress.IsCleared(stage) ? "통과" : unlocked ? "열림" : "잠금";
            var distribution = stage.Districts.Select((district, position) =>
                $"{district.District} {(position == stage.Districts.Count - 1 ? 2 : 1)}건");
            return new StageRow(stage, $"{stage.Districts.Count}. {stage.Title} · {status}",
                $"{string.Join(" · ", distribution)}\n배차 제한 가상 {stage.DispatchLimit.TotalMinutes:0}분", unlocked);
        }).ToArray();
        StagePicker.SelectedItem = StagePicker.Items.OfType<StageRow>()
            .First(row => row.Stage.Id == (target ?? FoodCampaignStage.All[0]).Id);
        StagePicker.ScrollIntoView(StagePicker.SelectedItem);
        StartFeedback.Text = "";
        ProgressWarning.Text = progress.Warning;
    }

    private void ChangeStage(object sender, SelectionChangedEventArgs e)
    {
        StartStageButton.IsEnabled = SelectedStage is { Unlocked: true };
        StartFeedback.Text = "";
    }

    private void StartStage(object sender, RoutedEventArgs e)
    {
        if (SelectedStage is { Unlocked: true } row)
        {
            BeginStage(row.Stage);
        }
    }

    private void BeginStage(FoodCampaignStage stage)
    {
        if (!progress.IsUnlocked(stage)) return;
        if (createStage(stage) is { } round)
        {
            progress.RecordStageStart(stage);
            ProgressWarning.Text = progress.Warning;
            ResetRound(round);
            FocusEntry();
        }
        else
        {
            ShowStartError($"{stage.Title}의 가상 매장과 연결 경로를 열 수 없습니다.");
        }
    }

    private void StartPracticeOne(object sender, RoutedEventArgs e) => BeginPractice(1);
    private void StartPracticeTwo(object sender, RoutedEventArgs e) => BeginPractice(2);

    private void BeginPractice(int count)
    {
        if (createPractice(count) is { } round)
        {
            ResetRound(round);
            FocusEntry();
        }
        else
        {
            ShowStartError("사가정 자유 연습의 가상 연결 경로를 열 수 없습니다.");
        }
    }

    private void ShowStartError(string message)
    {
        if (activeRound is null)
        {
            StartFeedback.Text = message;
        }
        else
        {
            Feedback.Text = message;
        }
    }

    private void OpenStageSelection(object sender, RoutedEventArgs e)
    {
        ShowStageSelection(activeRound?.CampaignStage);
        FocusEntry();
    }

    private void SelectExpansion(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpansionRow row })
        {
            ChooseExpansion(row.Stage.Id);
        }
    }

    private void ChooseExpansion(string id)
    {
        var choice = expansionRows.FirstOrDefault(row => row.Stage.Id == id);
        if (choice is null || !progress.IsUnlocked(choice.Stage)) return;
        ShowStageSelection(choice.Stage);
        FocusEntry();
    }

    private void RestartRound(object sender, RoutedEventArgs e)
    {
        RememberResult();
        if (Round.CampaignStage is { } stage)
        {
            BeginStage(stage);
        }
        else
        {
            BeginPractice(Round.BatchCount);
        }
    }

    private void ChangeBatch(object sender, RoutedEventArgs e)
    {
        RememberResult();
        BeginPractice(Round.BatchCount == 1 ? 2 : 1);
    }

    private void RememberResult()
    {
        if (Round.IsSettled)
        {
            var roundLabel = Round.CampaignStage?.Title ?? $"{Round.BatchCount}건 배치";
            previousResult = $"직전 {roundLabel} · 차액 {Round.Orders.Sum(order => order.Settlement!.PlatformBalance):N0}원" +
                $" · 보너스 {Round.FastDispatchBonus:N0}원\n" + string.Join(" · ",
                    Round.Orders.Select(order => $"{Label(order.Settlement!.DriverId)} {Duration(order.Settlement.DeliveryTime)}"));
        }
    }

    private void ResetRound(FoodManagementRound round)
    {
        clock.Reset();
        rendered = null;
        resultSaved = false;
        SetExpansionRows([]);
        activeRound = round;
        driverLabels = round.MapSnapshot().Couriers.ToDictionary(driver => driver.Id, driver => driver.Label);
        Feedback.Text = "";
        ClearCandidates();
        RoundTitle.Text = round.CampaignStage?.Title ?? "사가정 · 음식 배차";
        OrderPicker.ItemsSource = round.Orders.Select(order =>
            new OrderRow(order.OrderId, $"{order.OrderId} · {order.PickupStoreName} 매장")).ToArray();
        OrderPicker.SelectedIndex = 0;
        OrderPicker.Visibility = round.BatchCount > 1 ? Visibility.Visible : Visibility.Collapsed;
        PreviousText.Visibility = previousResult is null ? Visibility.Collapsed : Visibility.Visible;
        PreviousText.Text = previousResult;
        StageSelection.Visibility = Visibility.Collapsed;
        RoundPanel.Visibility = Visibility.Visible;
        if (round.CampaignStage is not null || round.BatchCount > 1) clock.Start();
        RenderRound();
    }

    private void ClearCandidates()
    {
        preview = null;
        Candidates.ItemsSource = null;
        AssignButton.IsEnabled = false;
        CandidatePanel.Visibility = Visibility.Collapsed;
    }

    private void RenderRound()
    {
        var waiting = Round.Orders.Count(order => order.Stage == FoodManagementStage.Waiting);
        var summary = $"주문 {Round.BatchCount}건 · 배차 대기 {waiting}건";
        if (Round.CampaignStage is { } stage)
        {
            summary = $"{DistrictNames(stage)}\n{summary} · 제시간 배차 {Round.OnTimeAssignmentCount}건";
            if (waiting > 0)
            {
                summary += $"\n배차 마감까지 가상 {Duration(TimeSpan.FromSeconds(Math.Ceiling(Round.DispatchRemaining.TotalSeconds)))}";
                if (Round.DispatchRemaining == TimeSpan.Zero)
                {
                    summary += " · 남은 주문 배차 가능";
                }
            }
            else if (!Round.IsSettled)
            {
                summary += "\n배달과 정산을 진행 중입니다.";
            }
        }
        else if (Round.BatchCount > 1 && waiting > 0)
        {
            summary += Round.BonusRemaining > TimeSpan.Zero
                ? $"\n빠른 배차 보너스 · 남은 가상 시간 {Duration(Round.BonusRemaining)}"
                : "\n빠른 배차 보너스 시간 종료";
        }
        BatchStatus.Text = summary;

        var current = CurrentOrder;
        var key = (Round.Orders.Sum(order => order.Revision), current.OrderId);
        if (rendered == key) return;
        rendered = key;
        OrderDetail.Text = $"{((OrderRow)OrderPicker.Items[Math.Max(0, OrderPicker.SelectedIndex)]).Title}\n" +
            $"{current.Menu} 1건 · 준비 예정 {current.ReadyAt:HH:mm:ss}";
        OpenOrderButton.IsEnabled = current.Stage == FoodManagementStage.Waiting;
        BatchButton.Visibility = Round.CampaignStage is null ? Visibility.Visible : Visibility.Collapsed;
        BatchButton.Content = Round.BatchCount == 1 ? "2건 배치 시작" : "1건 배치로 돌아가기";
        BatchButton.IsEnabled = Round.BatchCount == 1 && waiting == 1 || Round.IsSettled;
        HashSet<string> lateOrders = Round.CampaignStage is null ? [] : Round.LateOrders.Select(order => order.OrderId).ToHashSet();
        RoundStatus.Text = string.Join(Environment.NewLine, Round.Orders.Select(order =>
            (Round.BatchCount == 1 ? "" : $"{order.OrderId} · ") + OrderStatus(order) +
            (lateOrders.Contains(order.OrderId) ? " · 늦게 지정" : "")));
        AllEvents.Text = string.Join(Environment.NewLine, Events().Select(item => $"{item.At:HH:mm:ss}   {item.Text}"));
        SettlementText.Visibility = Round.IsSettled ? Visibility.Visible : Visibility.Collapsed;
        RestartButton.Visibility = SettlementText.Visibility;
        CampaignMenuButton.Visibility = Round.IsSettled || (Round.CampaignStage is null && waiting == Round.BatchCount)
            ? Visibility.Visible : Visibility.Collapsed;
        if (Round.IsSettled)
        {
            SaveSettlement();
        }
        else
        {
            SaveProgressButton.Visibility = Visibility.Collapsed;
        }
    }

    private void RetrySaveProgress(object sender, RoutedEventArgs e) => SaveSettlement();

    private void SaveSettlement()
    {
        if (Round.CampaignStage is not null && !resultSaved)
        {
            resultSaved = progress.RecordResult(Round);
            ProgressWarning.Text = progress.Warning;
        }
        SaveProgressButton.Visibility = Round.CampaignStage is not null && !resultSaved ? Visibility.Visible : Visibility.Collapsed;
        if (Round.CampaignStage is { } stage && Round.CampaignCleared && resultSaved)
        {
            var options = FoodCampaignStage.NextOptions(stage).Where(progress.IsUnlocked).Select(CreateExpansionRow);
            SetExpansionRows(options.ToArray());
        }
        RenderSettlement();
    }

    private ExpansionRow CreateExpansionRow(FoodCampaignStage stage)
    {
        var district = stage.ExpansionDistrict;
        var store = stores.FirstOrDefault(item => item.District == district.District);
        var label = $"{district.District} {district.TerritoryName}";
        return new(stage, label, store is null ? null : new(stage.Id, label, store.Latitude, store.Longitude));
    }

    private void SetExpansionRows(ExpansionRow[] rows)
    {
        expansionRows = rows;
        ExpansionOptions.ItemsSource = rows;
        ExpansionPanel.Visibility = CanExpand ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderSettlement()
    {
        var settlements = Round.Orders.Select(order => order.Settlement!).ToArray();
        var revenue = settlements.Sum(item => item.PlatformRevenue);
        var balance = settlements.Sum(item => item.PlatformBalance);
        var timing = Round.Orders.Select(order =>
            $"{order.OrderId} 픽업 {order.Settlement!.PickupAt:HH:mm:ss} · 전달 {order.Settlement.DeliveredAt:HH:mm:ss} ({Duration(order.Settlement.DeliveryTime)})");
        var riders = driverLabels.Select(rider =>
            $"{rider.Value} 보수 {settlements.Where(item => item.DriverId == rider.Key).Sum(item => item.DriverPay):N0}원");
        SettlementText.Text = "가상 회차 결산\n" + string.Join(Environment.NewLine, timing) +
            $"\n플랫폼 수입 {revenue:N0}원 · 차액 {balance:N0}원\n" + string.Join(Environment.NewLine, riders);
        if (Round.BatchCount > 1)
        {
            SettlementText.Text += $"\n빠른 배차 보너스 {Round.FastDispatchBonus:N0}원\n차액과 보너스 합계 {balance + Round.FastDispatchBonus:N0}원";
        }
        if (Round.CampaignStage is not null)
        {
            var result = Round.CampaignCleared
                ? !resultSaved ? "통과 · 해금 기록 저장 대기" :
                    CanExpand ? "통과 · 다음 확장 지역을 고르세요." : "모든 스테이지 통과"
                : "제한 시간 초과 · 늦은 주문 " + string.Join(", ", Round.LateOrders.Select(order => order.OrderId));
            SettlementText.Text = $"{result}\n제시간 배차 {Round.OnTimeAssignmentCount}/{Round.BatchCount}건\n\n{SettlementText.Text}";
        }
    }

    private string OrderStatus(FoodManagementSession order)
    {
        var driver = order.AssignedDriverId is { } id ? $"{Label(id)} · " : "";
        return driver + (order.Stage switch
        {
            FoodManagementStage.Waiting => "조리 주문 · 기사 지정 대기",
            FoodManagementStage.Assigned => "기사 수락 대기",
            FoodManagementStage.Accepted => "기사 수락 · 출발 준비",
            FoodManagementStage.ToPickup => "매장으로 이동",
            FoodManagementStage.AtPickup => "매장 도착 · 픽업 대기",
            FoodManagementStage.ToDropoff => $"픽업 완료 · 전달 이동 · 예상 보수 {order.DriverPay:N0}원",
            FoodManagementStage.Delivered => $"전달 완료 · 주문자 수령 대기 · {order.AccruedDriverPay:N0}원 적립",
            FoodManagementStage.Received => "주문자 수령 확인 · 가상 정산 처리 중",
            FoodManagementStage.Settled => "주문자 수령 확인 · 가상 정산 완료",
            _ => throw new InvalidOperationException("Unknown food management stage."),
        });
    }

    private string Label(string driverId) => driverLabels.GetValueOrDefault(driverId, driverId);

    private static string DistrictNames(FoodCampaignStage stage) =>
        string.Join(" · ", stage.Districts.Select(district => district.District));

    private static string ReadyDifference(TimeSpan? difference) => difference switch
    {
        null => "준비 시각 비교 불가",
        { } gap when gap == TimeSpan.Zero => "준비 시각에 맞춰 도착",
        { } gap when gap < TimeSpan.Zero => $"준비 {Duration(gap.Duration())} 전",
        { } gap => $"준비 {Duration(gap)} 후",
    };

    private static string Duration(TimeSpan value) => $"{(int)value.TotalMinutes}분 {value.Seconds}초";

    private sealed record CandidateRow(string DriverId, string Title, string Detail, bool Eligible);
    private sealed record OrderRow(string OrderId, string Title);
    private sealed record StageRow(FoodCampaignStage Stage, string Title, string Detail, bool Unlocked)
    {
        public string AccessibleName => $"{Title}. {Detail}";
    }
    private sealed record ExpansionRow(FoodCampaignStage Stage, string Label, FoodManagementExpansionPin? Pin);
}
