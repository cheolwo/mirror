using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ssalddel.BatchMonitor.Windows;

public partial class FoodManagementPanel : UserControl
{
    private const int ClockScale = 12;
    private readonly Stopwatch clock = new();
    // 기사 무념·경 시간(r124~r126)과 생산·소비 흐름(r134). Claude 작업본에서 추가한 상태다.
    private RiderMindBook mindBook = RiderMindBook.InMemory();
    private DeliveryRoadNetwork roads = null!;
    private Dictionary<string, (double Remaining, bool Broken)> roundMindStart = new(StringComparer.Ordinal);
    private Dictionary<string, double> deliveryKilometers = new(StringComparer.Ordinal);
    private RiderLive[]? settledLives;
    private bool mindCommitted;
    private string? selectedRiderId;
    private string? renderedMindKey;
    private string? renderedFlowKey;
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
        roads = sample.RoadNetwork;
        mindBook = RiderMindBook.Load();
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
            item with { Text = $"{order.OrderId} · {item.Text}" }))
            .Concat(MindEvents(round))
            .OrderBy(item => item.At);

    private IEnumerable<FoodManagementEvent> MindEvents(FoodManagementRound round) =>
        roundMindStart.Keys.Select(id => RoundLive(round, id))
            .Where(live => live.BrokeThisRound && live.BrokenAt is { } at && at <= round.Now)
            .Select(live => new FoodManagementEvent(live.BrokenAt!.Value,
                $"{Label(live.DriverId)} 무념 상태가 깨졌습니다 · 개인 타로 역방향", "rider_munyeom_broken"));

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
        var availableDrivers = snapshot.Couriers.Where(driver => driver.PickupStoreId is null && districts.Contains(driver.District)
                && !LiveFor(driver.Id).Broken)
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
            selectedRiderId = id;
            RenderMind();
            if (LiveFor(id).Broken)
            {
                Feedback.Text = $"{Label(id)} · 무념 상태가 깨졌습니다. 회차가 끝난 뒤 휴식·점검으로 회복하세요.";
                return;
            }

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
        var order = CurrentOrder;
        var current = order.Preview();
        preview = current;
        var rows = current.Candidates.Select(candidate =>
        {
            var recommended = candidate.DriverId == current.RecommendedDriverId;
            var mind = mindBook.Get(candidate.DriverId);
            var live = LiveFor(candidate.DriverId);
            var title = $"{candidate.Label} · {mind.GradeName}" + (recommended ? " · 추천" : "");
            var reason = recommended && candidate.Reason == "적정 픽업창"
                ? "추천 이유 · 적정 픽업창에서 음식 준비 시각과의 차이를 우선 비교"
                : candidate.Reason;
            var detail = candidate.Eligible && candidate.ArrivalAt is { } arrival
                ? $"{candidate.PickupKilometers:0.00}km · {arrival:HH:mm:ss} 도착 · {ReadyDifference(candidate.ReadyDifference)}\n{reason}"
                : reason;
            var eligible = candidate.Eligible;
            if (live.Broken)
            {
                eligible = false;
                detail = "무념 상태가 깨졌습니다 · 휴식과 점검 후 배차할 수 있습니다.";
            }
            else if (candidate.Eligible && candidate.ArrivalAt is { } arrivalAt)
            {
                // r125: 다음 주문 예상 수행시간에 여유 1분을 더해 남은 경 시간과 비교하고, 부족하면 다른 기사나 휴식을 권한다.
                var task = RiderMindRules.EstimateTaskMinutes(order.Now, arrivalAt, order.ReadyAt, DeliveryKilometers(order));
                var enough = RiderMindRules.HasEnough(live.RemainingMinutes, task);
                detail += $"\n경 {Minutes(live.RemainingMinutes)} 남음 · 예상 업무 {Minutes(task)} + 여유 1분 → " +
                    (enough ? "충분" : "부족 · 무념이 깨질 수 있어 다른 기사나 휴식을 권합니다");
            }
            return new CandidateRow(candidate.DriverId, title, detail, eligible);
        }).ToArray();
        Candidates.ItemsSource = rows;
        Candidates.SelectedItem = rows.FirstOrDefault(row => row.DriverId == selectedDriverId && row.Eligible);
        CandidatePanel.Visibility = Visibility.Visible;
    }

    private void SelectCandidate(object sender, SelectionChangedEventArgs e)
    {
        AssignButton.IsEnabled = activeRound is not null && Candidates.SelectedItem is CandidateRow { Eligible: true } &&
            CurrentOrder.Stage == FoodManagementStage.Waiting;
        if (Candidates.SelectedItem is CandidateRow row)
        {
            selectedRiderId = row.DriverId;
            RenderMind();
        }
    }

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
        RenderMind();
    }

    private void ChangeStage(object sender, SelectionChangedEventArgs e)
    {
        StartStageButton.IsEnabled = SelectedStage is { Unlocked: true };
        StartFeedback.Text = "";
        RenderMind();
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
        // 회차 시작 시점의 남은 경 시간을 고정해 두고, 업무 시간은 주문 사건(출발→전달)에서 계산한다.
        roundMindStart = driverLabels.Keys.ToDictionary(id => id,
            id => (Remaining: mindBook.Get(id).RemainingMinutes, Broken: mindBook.Get(id).Broken), StringComparer.Ordinal);
        deliveryKilometers = new(StringComparer.Ordinal);
        settledLives = null;
        mindCommitted = false;
        selectedRiderId = null;
        RiderFeedback.Text = "";
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
        RenderMind();
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
        CommitMind();
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

        SettlementText.Text += "\n\n" + MindSettlementText();
    }

    // r133: 경 유지 구간에 연결된 가상 순소득 ÷ 경 유지 시간. 비용 범위 미정이라 순소득은 가상 기사 보수로 둔다.
    private string MindSettlementText()
    {
        var lives = settledLives ?? roundMindStart.Keys.Select(id => RoundLive(Round, id)).ToArray();
        var worked = lives.Where(live => live.Orders.Count > 0).ToArray();
        if (worked.Length == 0) return "무념·경 시간 결산 · 이번 회차 업무 기록 없음";
        var lines = worked.Select(live =>
        {
            var perHour = live.IncomePerMaintainedHour is { } value ? $"{value:N0}원" : "산정 불가";
            var broken = live.BrokeThisRound ? " · 무념 깨짐" : "";
            return $"{Label(live.DriverId)} · 업무 {Minutes(live.UsedMinutes)} · 경 유지 {Minutes(live.MaintainedMinutes)}" +
                $" · 연결 소득 {live.LinkedIncome:N0}원/보수 {live.Pay:N0}원 · 경 시간당 {perHour}{broken}";
        });
        var maintained = worked.Sum(live => live.MaintainedMinutes);
        var linked = worked.Sum(live => live.LinkedIncome);
        var total = maintained > 0 ? $"{linked / (maintained / 60):N0}원" : "산정 불가";
        return "무념·경 시간 결산\n" + string.Join(Environment.NewLine, lines) +
            $"\n합계 경 유지 {Minutes(maintained)} · 연결 소득 {linked:N0}원 · 경 시간당 순소득 {total}" +
            "\n가상 값 · 비용 미반영이라 순소득은 가상 기사 보수입니다.";
    }

    private void CommitMind()
    {
        if (mindCommitted || activeRound is not { IsSettled: true } round) return;
        settledLives = roundMindStart.Keys.Select(id => RoundLive(round, id)).ToArray();
        mindBook.Commit(settledLives);
        mindCommitted = true;
        renderedMindKey = null;
        RenderMind();
    }

    private RiderLive RoundLive(FoodManagementRound round, string driverId)
    {
        (double Remaining, bool Broken) start = roundMindStart.TryGetValue(driverId, out var value) ? value
            : (Remaining: mindBook.Get(driverId).RemainingMinutes, Broken: mindBook.Get(driverId).Broken);
        return RiderMindEvaluator.Evaluate(driverId, start.Remaining, start.Broken, WorksFor(round, driverId), round.Now);
    }

    // 진행 중인 회차에서는 회차 시작값과 실제 업무 시간으로, 그 밖에는 저장된 기록으로 기사 상태를 읽는다.
    private RiderLive LiveFor(string driverId) =>
        activeRound is { IsSettled: false } round && roundMindStart.ContainsKey(driverId)
            ? RoundLive(round, driverId)
            : RiderMindEvaluator.Idle(mindBook.Get(driverId));

    private static IEnumerable<RiderWork> WorksFor(FoodManagementRound round, string driverId) =>
        round.Orders.Where(order => order.AssignedDriverId == driverId).Select(order =>
        {
            var departed = order.Events.FirstOrDefault(item => item.Code == "driver_departed");
            if (departed is null) return null;
            var delivered = order.Events.FirstOrDefault(item => item.Code == "food_delivered");
            var settled = order.Settlement is not null;
            return new RiderWork(order.OrderId, departed.At, delivered?.At, settled ? order.Settlement!.DriverPay : 0, settled);
        }).OfType<RiderWork>();

    private double DeliveryKilometers(FoodManagementSession order)
    {
        if (deliveryKilometers.TryGetValue(order.OrderId, out var cached)) return cached;
        var store = stores.FirstOrDefault(item => item.Id == order.PickupStoreId);
        var kilometers = 0.0;
        if (store is not null && roads.FindPath(roads.Closest(new(store.Latitude, store.Longitude)), order.DropoffNode) is { } path)
        {
            kilometers = path.Kilometers;
        }

        deliveryKilometers[order.OrderId] = kilometers;
        return kilometers;
    }

    // 지금 화면의 기사와 주문: 진행 중인 회차, 또는 스테이지 선택 화면에서 고른 스테이지를 미리 만든 회차.
    private (IReadOnlyList<DeliveryMapCourier> Riders, IReadOnlyList<FoodManagementSession> Orders, bool Preview) MindContext()
    {
        if (activeRound is { } round)
        {
            return (round.MapSnapshot().Couriers, round.Orders, false);
        }

        if (SelectedStage?.Stage is { } stage && createStage(stage) is { } previewRound)
        {
            return (previewRound.MapSnapshot().Couriers, previewRound.Orders, true);
        }

        return (Array.Empty<DeliveryMapCourier>(), Array.Empty<FoodManagementSession>(), true);
    }

    private void RenderMind()
    {
        if (roads is null || createStage is null) return;
        var (riders, orders, isPreview) = MindContext();
        var playing = activeRound is { IsSettled: false };

        var cards = riders.Select(rider =>
        {
            var mind = mindBook.Get(rider.Id);
            var live = LiveFor(rider.Id);
            var assigned = activeRound?.Orders.Any(order => order.AssignedDriverId == rider.Id) == true;
            var stateText = live.Broken ? "무념 깨짐" : live.Working ? "무념 유지 · 운행" : assigned && playing ? "무념 유지 · 배정" : "무념 유지";
            var percent = mind.CapacityMinutes > 0 ? Math.Clamp(live.RemainingMinutes / mind.CapacityMinutes * 100, 0, 100) : 0;
            var lowForTypical = !live.Broken && !RiderMindRules.CanTakeTypicalOrder(live);
            var mindText = $"경 {Minutes(live.RemainingMinutes)} 남음 · 유지 가능 {Minutes(mind.CapacityMinutes)}" +
                (lowForTypical ? " · 다음 주문엔 부족" : "");
            var shiftMaintained = mind.ShiftMaintainedMinutes + (playing ? live.MaintainedMinutes : 0);
            var shiftDeliveries = mind.ShiftDeliveries + (playing ? live.SettledDeliveries : 0);
            var recordText = $"이번 근무 무념 유지 {Minutes(shiftMaintained)} · 정산 배달 {shiftDeliveries}건 · 유효 근무 {mind.ValidShifts}회";
            var gradeText = mind.NextGradeName is { } next && mind.NextGradeMinutes is { } required
                ? $"다음 등급 {next}{(RiderGrades.IsProvisional(mind.Grade + 1) ? "(임시 기준)" : "")} · {Minutes(required)} 이상 유효 근무 " +
                    $"{mind.QualifyingShifts}/{RiderGrades.ShiftsForPromotion}회"
                : "최고 등급 대종사";
            var tarotText = live.Broken
                ? $"개인 타로 {mind.Tarot} · 역방향 · 무념 상태가 깨졌습니다"
                : $"개인 타로 {mind.Tarot} · 정방향 · 무념 유지";
            var selected = rider.Id == selectedRiderId;
            return new RiderCard(rider.Id, $"{rider.Label} · {mind.GradeName}", Paint(RiderGrades.Color(mind.Grade)),
                stateText, Paint(live.Broken ? "#FBE3DD" : live.Working ? "#DCEFFB" : "#D8F0E6"),
                Paint(live.Broken ? "#9B2C1F" : live.Working ? "#1D5A85" : "#146657"),
                mindText, percent, Paint(live.Broken ? "#C0392B" : lowForTypical ? "#E67E22" : "#2E9E5B"),
                recordText, gradeText, tarotText, Paint(live.Broken ? "#9B2C1F" : "#213742"),
                Paint(live.Broken ? "#FFF6F3" : "#FFFFFF"), Paint(selected ? "#2F6FD1" : "#D6E0E3"),
                new Thickness(selected ? 2 : 1), !playing && (live.Broken || mind.RemainingMinutes < mind.CapacityMinutes ||
                    mind.ShiftDeliveries > 0 || mind.ShiftWorkMinutes > 0), selected);
        }).OrderByDescending(card => card.Selected).ToArray();

        var key = string.Join("|", cards.Select(card => card.Key));
        if (key != renderedMindKey)
        {
            renderedMindKey = key;
            RiderCards.ItemsSource = cards;
        }

        RiderHint.Text = isPreview
            ? "스테이지를 시작하기 전에 기사 상태를 보세요. 경 시간이 부족한 기사는 휴식·점검으로 회복합니다."
            : playing
                ? "업무 중(출발→전달)에는 경 시간이 줄어듭니다. 남은 경 시간을 넘기면 무념이 깨지고 개인 타로가 역방향이 됩니다."
                : "회차가 끝났습니다. 휴식·점검으로 근무를 마감하면 유효 기록과 성장이 반영됩니다.";

        RenderFlow(riders, orders);
    }

    private void RenderFlow(IReadOnlyList<DeliveryMapCourier> riders, IReadOnlyList<FoodManagementSession> orders)
    {
        var districts = orders.Select(order => order.District ?? "").Distinct().ToArray();
        var inputs = districts.Select(district =>
        {
            var districtOrders = orders.Where(order => (order.District ?? "") == district).ToArray();
            var districtRiders = riders.Where(rider => (rider.District ?? "") == district).ToArray();
            var ready = districtRiders.Count(rider =>
                districtOrders.Any(order => order.AssignedDriverId == rider.Id) ||
                RiderMindRules.CanTakeTypicalOrder(LiveFor(rider.Id)));
            var kitchens = stores.Count(store => store.District == district);
            return new DistrictFlowInput(district, districtOrders.Length, kitchens, ready);
        }).ToArray();
        var summary = ProductionFlow.Summarize(inputs);
        var max = Math.Max(1, Math.Max(summary.Orders, Math.Max(summary.Prepare, summary.Deliver)));
        const double barWidth = 170;
        string Fill(bool bottleneck) => bottleneck ? "#E67E22" : "#4FA58F";
        var bars = new[]
        {
            new FlowBar("주문", summary.Orders.ToString(), summary.Orders * barWidth / max, Paint("#7D8C93")),
            new FlowBar("준비 가능", summary.Prepare.ToString(), summary.Prepare * barWidth / max,
                Paint(Fill(summary.Bottleneck == FlowBottleneck.Preparation))),
            new FlowBar("배송 여력", summary.Deliver.ToString(), summary.Deliver * barWidth / max,
                Paint(Fill(summary.Bottleneck == FlowBottleneck.Delivery))),
            new FlowBar("완료 상한", summary.Limit.ToString(), summary.Limit * barWidth / max, Paint("#2F6FD1")),
        };
        var rows = summary.Districts.Select(item => new FlowRow(item.District, item.Orders, item.Prepare, item.Deliver,
            item.Limit, Paint(item.Bottleneck is FlowBottleneck.Delivery or FlowBottleneck.Preparation ? "#B4561A" : "#213742"))).ToArray();
        var key = summary.Focus + string.Join("|", rows.Select(row => $"{row.District}{row.Orders}{row.Prepare}{row.Deliver}{row.Limit}"));
        if (key == renderedFlowKey) return;
        renderedFlowKey = key;
        FlowFocus.Text = summary.Focus;
        FlowFocus.Foreground = Paint(summary.Bottleneck is FlowBottleneck.Delivery or FlowBottleneck.Preparation ? "#B4561A" : "#146657");
        FlowBars.ItemsSource = bars;
        FlowRows.ItemsSource = rows;
        FlowSection.Visibility = orders.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RestRider(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RiderCard card }) return;
        if (activeRound is { IsSettled: false })
        {
            RiderFeedback.Text = "회차가 끝난 뒤 휴식·점검할 수 있습니다.";
            return;
        }

        var result = mindBook.Rest(card.DriverId);
        selectedRiderId = card.DriverId;
        RiderFeedback.Text = $"{card.Title.Split(" · ")[0]} · {result.Message}" +
            (mindBook.Warning is { } warning ? $"\n{warning}" : "");
        renderedMindKey = null;
        renderedFlowKey = null;
        RenderMind();
    }

    private static readonly Dictionary<string, SolidColorBrush> BrushCache = new(StringComparer.Ordinal);

    private static SolidColorBrush Paint(string color)
    {
        if (!BrushCache.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            BrushCache[color] = brush;
        }

        return brush;
    }

    private static string Minutes(double minutes) => RiderMindBook.FormatMinutes(minutes);

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
    private sealed record RiderCard(string DriverId, string Title, Brush GradeColor, string StateText,
        Brush StateBackground, Brush StateForeground, string MindText, double Percent, Brush BarColor,
        string RecordText, string GradeText, string TarotText, Brush TarotForeground, Brush CardBackground,
        Brush CardBorder, Thickness CardBorderThickness, bool CanRest, bool Selected)
    {
        public string Key => string.Join("/", DriverId, Title, StateText, MindText, Math.Round(Percent).ToString(),
            RecordText, GradeText, TarotText, CanRest.ToString(), Selected.ToString());
    }
    private sealed record FlowBar(string Label, string Value, double BarWidth, Brush Fill);
    private sealed record FlowRow(string District, int Orders, int Prepare, int Deliver, int Limit, Brush Ink);
}
