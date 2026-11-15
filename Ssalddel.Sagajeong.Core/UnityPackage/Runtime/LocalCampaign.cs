using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Ssalddel.BatchMonitor.Windows;

internal sealed class LocalCampaign
{
    private const long DayMs = 86_400_000;
    private const long ClosingOffsetMs = 23_400_000;
    private const long EmpressSecondOrderDelayMs = 120_000;
    private const int PolicyDay = 3;
    private const string Open = "open";
    private const string Closed = "closed";
    private const string PolicyGate = "policy";
    private static readonly DateTime StartTime = new(2026, 1, 1, 11, 30, 0);

    private readonly DeliveryRoadNetwork roads;
    private readonly DeliverySample sample;
    private readonly bool manualPickup;

    internal LocalCampaign(DeliveryRoadNetwork roads, DeliverySample sample, bool manualPickup = false)
    {
        this.roads = roads;
        this.sample = sample;
        this.manualPickup = manualPickup;
        Round = CreateRound();
    }

    internal FoodManagementRound Round { get; private set; }
    internal long ElapsedMs { get; private set; }
    internal string Now => StartTime.AddMilliseconds(ElapsedMs).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    internal string? Policy { get; private set; }
    internal bool AcceptingAssignments => Phase == Open;

    private long roundStartedAtMs;
    private int Day { get; set; } = 1;
    private string Phase { get; set; } = Open;
    private LocalCampaignSummary? LatestSummary { get; set; }
    private long OpenMs => (Day - 1) * DayMs;
    private long CloseMs => Math.Max(OpenMs + ClosingOffsetMs, roundStartedAtMs);
    private long NextOpenMs => OpenMs + DayMs;
    private bool HasActiveDelivery => Round.Orders.Any(order =>
        order.AssignedDriverId is not null && order.Settlement is null);

    internal bool IsSettled => PlannedOrders().All(order => order.Settlement is not null);

    internal bool AcceptsTime(long requestedMs) =>
        requestedMs >= ElapsedMs && requestedMs <= ElapsedMs + PolicyDay * DayMs &&
        requestedMs < (DateTime.MaxValue - StartTime).TotalMilliseconds - DayMs;

    internal void Advance(long requestedMs)
    {
        if (Phase == PolicyGate)
            return;

        var policyDeadline = (PolicyDay - 1) * DayMs + ClosingOffsetMs;
        var targetMs = Policy is null ? Math.Min(requestedMs, policyDeadline) : requestedMs;
        while (true)
        {
            if (Phase == Open)
            {
                Tick(Math.Min(targetMs, CloseMs));
                if (ElapsedMs < CloseMs)
                    return;

                Phase = Day == PolicyDay && Policy is null ? PolicyGate : Closed;
                LatestSummary = Summarize();
                if (Phase == PolicyGate || ElapsedMs == targetMs)
                    return;
            }

            var nextBoundaryMs = Math.Max(ElapsedMs, NextOpenMs);
            Tick(Math.Min(targetMs, nextBoundaryMs));
            if (ElapsedMs >= NextOpenMs && HasActiveDelivery && ElapsedMs < targetMs)
                Tick(targetMs);
            LatestSummary = Summarize();

            if (ElapsedMs < NextOpenMs || HasActiveDelivery)
                return;

            Day++;
            Phase = Open;
            roundStartedAtMs = ElapsedMs;
            Round = CreateRound();
            if (ElapsedMs == targetMs)
                return;
        }
    }

    internal bool TryChoosePolicy(string? policy)
    {
        if (Phase != PolicyGate || policy is not ("chariot" or "temperance" or "empress"))
            return false;

        Policy = policy;
        Phase = Closed;
        return true;
    }

    internal LocalCampaignState Snapshot()
    {
        var nextBoundaryMs = Phase switch
        {
            Open => CloseMs,
            Closed => NextOpenMs,
            _ => ElapsedMs
        };
        var canFastForward = nextBoundaryMs > ElapsedMs && !HasActiveDelivery &&
            (Phase == Closed || IsSettled);
        return new(Day, Phase, Policy, nextBoundaryMs, canFastForward, LatestSummary);
    }

    internal string OrderId(FoodManagementSession order) =>
        FormattableString.Invariant($"day-{Day}:{order.OrderId}");

    internal IEnumerable<FoodManagementSession> VisibleOrders() =>
        PlannedOrders().Where(order => Policy != "empress" || Day <= PolicyDay ||
            ElapsedMs - roundStartedAtMs >= EmpressSecondOrderDelayMs || order.PickupStoreId == "jungnang-a");

    internal LocalCampaignSettlement? Settlement()
    {
        if (!IsSettled)
            return null;

        var orders = PlannedOrders().ToArray();
        var bonus = FastDispatchBonus(orders);
        var revenue = orders.Sum(order => order.Settlement!.PlatformRevenue);
        var driverPay = orders.Sum(order => order.Settlement!.DriverPay);
        var onTimeCount = orders.Length - Round.LateOrders.Count;
        return new(onTimeCount == orders.Length, onTimeCount, revenue, driverPay, revenue - driverPay,
            bonus, revenue - driverPay + bonus);
    }

    private FoodManagementRound CreateRound()
    {
        var stage = FoodCampaignStage.All[Day > PolicyDay && Policy == "chariot" ? 1 : 0];
        var round = FoodManagementRound.CreateCampaign(roads, sample, stage)
            ?? throw new InvalidOperationException("사가정 음식 배달 표본을 열 수 없습니다.");
        if (manualPickup) round.EnableManualPickup();
        return round;
    }

    private IEnumerable<FoodManagementSession> PlannedOrders() =>
        Day > PolicyDay && Policy == "temperance"
            ? Round.Orders.Where(order => order.PickupStoreId == "jungnang-a")
            : Round.Orders;

    private void Tick(long atMs)
    {
        ElapsedMs = atMs;
        Round.Tick(TimeSpan.FromMilliseconds(ElapsedMs - roundStartedAtMs));
    }

    private LocalCampaignSummary Summarize()
    {
        var orders = PlannedOrders().ToArray();
        var completed = orders.Where(order => order.Settlement is not null).ToArray();
        var waiting = orders.Count(order => order.AssignedDriverId is null);
        return new(Day, completed.Length, waiting, orders.Length - completed.Length - waiting,
            completed.Sum(order => order.Settlement!.PlatformBalance) + FastDispatchBonus(completed), orders.Length);
    }

    private int FastDispatchBonus(IEnumerable<FoodManagementSession> completed) =>
        FoodManagementRound.FastDispatchBonusPerOrder *
        completed.Count(order => !Round.LateOrders.Contains(order));
}

internal sealed record LocalCampaignState(int Day, string Phase, string? Policy, long NextBoundaryMs,
    bool CanFastForward, LocalCampaignSummary? LatestSummary);

internal sealed record LocalCampaignSummary(int Day, int Completed, int Waiting, int Delivering, int Balance,
    int TotalOrders);

internal sealed record LocalCampaignSettlement(bool Cleared, int OnTimeCount, int Revenue, int DriverPay,
    int BaseBalance, int Bonus, int Balance);
