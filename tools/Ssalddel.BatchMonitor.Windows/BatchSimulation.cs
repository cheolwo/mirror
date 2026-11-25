using System;
using System.Collections.Generic;

namespace Ssalddel.BatchMonitor.Windows;

public enum BatchMonitorPhase
{
    Waiting,
    Grouped,
    InTransit,
    Completed
}

public sealed record BatchMonitorOrder(long Number, TimeSpan CreatedAt, long? BatchNumber, BatchMonitorPhase Phase);

public sealed record BatchMonitorBatch(
    long Number, TimeSpan CreatedAt, IReadOnlyList<long> OrderNumbers, BatchMonitorPhase Phase);

public sealed record BatchMonitorEvent(TimeSpan At, string Message);

public sealed class BatchSimulation
{
    private const long OrderIntervalTicks = 4 * TimeSpan.TicksPerSecond;
    private const int OrdersPerBatch = 3;
    private const int EventLimit = 16;
    private const int RebuildWindowSteps = 12;
    internal static readonly TimeSpan TransitAge = TimeSpan.FromSeconds(4);
    internal static readonly TimeSpan CompletionAge = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ExpiryAge = TimeSpan.FromSeconds(16);

    private readonly List<BatchMonitorOrder> orders = [];
    private readonly List<BatchMonitorBatch> batches = [];
    private readonly List<BatchMonitorEvent> events = [];
    private long nextStep;

    public BatchSimulation()
    {
        Orders = orders.AsReadOnly();
        Batches = batches.AsReadOnly();
        Events = events.AsReadOnly();
        Tick(TimeSpan.Zero);
    }

    public IReadOnlyList<BatchMonitorOrder> Orders { get; }
    public IReadOnlyList<BatchMonitorBatch> Batches { get; }
    public IReadOnlyList<BatchMonitorEvent> Events { get; }
    public TimeSpan Elapsed { get; private set; }

    public void Tick(TimeSpan elapsed)
    {
        if (elapsed < Elapsed)
        {
            return;
        }

        var targetStep = elapsed.Ticks / OrderIntervalTicks;
        if (targetStep - nextStep > RebuildWindowSteps)
        {
            // Rebuild from a batch boundary far enough back to cover every active item
            // and retained event. Identifiers derive from the schedule, including skipped steps.
            orders.Clear();
            batches.Clear();
            events.Clear();
            var firstStep = targetStep - RebuildWindowSteps;
            nextStep = firstStep - firstStep % OrdersPerBatch;
        }

        while (nextStep <= targetStep)
        {
            AdvanceStep(nextStep);
            nextStep++;
        }

        Elapsed = elapsed;
    }

    private void AdvanceStep(long step)
    {
        var at = TimeSpan.FromTicks(step * OrderIntervalTicks);
        AdvanceBatches(at);

        if (step > 0 && step % OrdersPerBatch == 0)
        {
            GroupWaitingOrders(step / OrdersPerBatch, at);
        }

        var orderNumber = step + 1;
        orders.Insert(0, new(orderNumber, at, null, BatchMonitorPhase.Waiting));
        Record(at, $"주문 {orderNumber}번 접수");
    }

    private void GroupWaitingOrders(long number, TimeSpan at)
    {
        var orderNumbers = new List<long>(OrdersPerBatch);
        for (var index = orders.Count - 1; index >= 0 && orderNumbers.Count < OrdersPerBatch; index--)
        {
            var order = orders[index];
            if (order.Phase == BatchMonitorPhase.Waiting)
            {
                orders[index] = order with { BatchNumber = number, Phase = BatchMonitorPhase.Grouped };
                orderNumbers.Add(order.Number);
            }
        }

        if (orderNumbers.Count == 0)
        {
            return;
        }

        batches.Insert(0, new(number, at, orderNumbers.AsReadOnly(), BatchMonitorPhase.Grouped));
        Record(at, $"묶음 {number}번 구성 · 주문 {orderNumbers.Count}건");
    }

    private void AdvanceBatches(TimeSpan at)
    {
        for (var index = batches.Count - 1; index >= 0; index--)
        {
            var batch = batches[index];
            var age = at - batch.CreatedAt;
            if (age >= ExpiryAge)
            {
                batches.RemoveAt(index);
                orders.RemoveAll(order => order.BatchNumber == batch.Number);
                Record(at, $"묶음 {batch.Number}번과 완료 주문 {batch.OrderNumbers.Count}건 정리");
                continue;
            }

            var phase = age >= CompletionAge ? BatchMonitorPhase.Completed
                : age >= TransitAge ? BatchMonitorPhase.InTransit
                : BatchMonitorPhase.Grouped;
            if (phase == batch.Phase)
            {
                continue;
            }

            batches[index] = batch with { Phase = phase };
            for (var orderIndex = 0; orderIndex < orders.Count; orderIndex++)
            {
                var order = orders[orderIndex];
                if (order.BatchNumber == batch.Number)
                {
                    orders[orderIndex] = order with { Phase = phase };
                }
            }

            var action = phase == BatchMonitorPhase.Completed ? "완료" : "이동 시작";
            Record(at, $"묶음 {batch.Number}번 {action}");
        }
    }

    private void Record(TimeSpan at, string message)
    {
        events.Insert(0, new(at, message));
        if (events.Count > EventLimit)
        {
            events.RemoveAt(EventLimit);
        }
    }
}
