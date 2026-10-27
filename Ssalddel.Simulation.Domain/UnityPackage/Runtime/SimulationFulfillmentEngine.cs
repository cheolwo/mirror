using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    /// <summary>
    /// 합성 센터·위치 재고·담당자의 순수 출고 계산입니다. 입력을 변경하지 않습니다.
    /// 세션 revision·명령 재시도·저장·실제 스캐너/운영 원장은 호출자가 소유합니다.
    /// </summary>
    public static class SimulationFulfillmentEngine
    {
        public static SimulationFulfillmentPlanSnapshot Preview(
            SimulationFulfillmentStateSnapshot state, SimulationFulfillmentOrderRequest order)
        {
            ValidateState(state);
            ValidateOrder(order, state);
            var request = CloneOrder(order);
            var result = new SimulationFulfillmentPlanSnapshot
            { OrderStableId = request.OrderStableId, Order = request };
            if (state.Orders.Any(x => x.OrderStableId == order.OrderStableId))
                return Reject(result, "OrderAlreadyExists");

            var wholePlans = new Dictionary<string, SimulationFulfillmentAllocation[]>(StringComparer.Ordinal);
            var candidates = new List<SimulationFulfillmentCenterCandidate>();
            foreach (var center in state.Centers.OrderBy(x => x.CenterStableId, StringComparer.Ordinal))
            {
                var reasons = new List<string>();
                if (!center.Enabled) reasons.Add("CenterDisabled");
                if (!center.OutboundEnabled) reasons.Add("CenterOutboundDisabled");
                if (request.AllowedCenterStableIds.Length > 0 && !request.AllowedCenterStableIds.Contains(center.CenterStableId))
                    reasons.Add("CenterNotAllowed");
                var estimate = request.CenterEstimates.SingleOrDefault(x => x.CenterStableId == center.CenterStableId);
                var candidate = new SimulationFulfillmentCenterCandidate
                {
                    CenterStableId = center.CenterStableId, DisplayName = center.DisplayName,
                    EstimatedDistanceKm = estimate?.EstimatedDistanceKm, EstimatedCost = estimate?.EstimatedCost,
                    // 누락값을 실제 거리/운임으로 만들지 않습니다. 같은 가중 합의 명시 추정 비교입니다.
                    PreferenceScore = estimate is null || (!estimate.EstimatedDistanceKm.HasValue && !estimate.EstimatedCost.HasValue)
                        ? (decimal?)null : -(estimate.EstimatedDistanceKm.GetValueOrDefault() / 1000m
                            + estimate.EstimatedCost.GetValueOrDefault() / 1000000m),
                };
                if (reasons.Count == 0)
                {
                    var stocks = UsableStocks(state).Where(x => x.CenterStableId == center.CenterStableId
                        && x.OnHandQuantity > x.ReservedQuantity && request.Lines.Any(line => Matches(x, line))).ToArray();
                    if (stocks.Length == 0) reasons.Add("MatchingStockUnavailable");
                    if (!state.Workers.Any(worker => worker.CenterStableId == center.CenterStableId
                        && stocks.Any(stock => stock.UnitCode == worker.CapacityUnitCode) && WorkerRemaining(state, worker) > 0)) reasons.Add("WorkerUnavailableOrCapacityExceeded");
                }
                candidate.IsEligible = reasons.Count == 0;
                if (candidate.IsEligible)
                {
                    var full = Allocate(state, request, new[] { center.CenterStableId }, out var allocations);
                    candidate.CanFulfillWholeOrder = full;
                    if (full) wholePlans.Add(center.CenterStableId, allocations);
                    else reasons.Add("WholeOrderCapacityInsufficient");
                }
                candidate.ReasonCodes = reasons.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                candidates.Add(candidate);
            }
            result.CandidateCenters = candidates.ToArray();
            var ranked = candidates.Where(x => x.IsEligible)
                .OrderByDescending(x => x.PreferenceScore.HasValue)
                .ThenByDescending(x => x.PreferenceScore.GetValueOrDefault())
                .ThenBy(x => x.CenterStableId, StringComparer.Ordinal).ToArray();

            SimulationFulfillmentAllocation[] selected;
            if (wholePlans.Count > 0)
            {
                // 기존 공통 규칙은 int 수량 계약입니다. 1은 원수량이 아니라 이미 완수 가능성이
                // 검증된 센터·라인 묶음의 선택 토큰입니다. decimal 물량은 아래 Allocate가 그대로 보존합니다.
                var choices = ranked.Where(x => wholePlans.ContainsKey(x.CenterStableId))
                    .Select((x, index) => new WholeCenterChoice(x.CenterStableId, index + 1L, ranked.Length - index)).ToArray();
                var lineCandidates = request.Lines.Select((line, lineIndex) =>
                    (IReadOnlyList<WholeCenterChoice>)choices.Select(x => new WholeCenterChoice(
                        x.Center, x.Warehouse, x.Score, checked(x.Warehouse * request.Lines.Length + lineIndex))).ToArray()).ToArray();
                var common = Ssalddel.WorkflowRules.창고출고배분Policy.판정(
                    request.Lines.Select(_ => 1).ToArray(), lineCandidates,
                    x => x.Stock, x => x.Warehouse, _ => 1, x => x.Score, _ => false);
                selected = wholePlans[common[0].Candidate.Center];
            }
            else
            {
                if (!Allocate(state, request, ranked.Select(x => x.CenterStableId).ToArray(), out selected))
                    return Reject(result, ranked.Length == 0 ? "NoEligibleCenter" : "InsufficientEligibleStockOrWorkerCapacity");
                if (!request.AllowSplitCenters && selected.Select(x => x.CenterStableId).Distinct().Count() > 1)
                    return Reject(result, "SplitCentersNotAllowed");
            }
            result.IsFeasible = true;
            result.Allocations = selected;
            result.SelectedCenterStableIds = selected.Select(x => x.CenterStableId).Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
            return result;
        }

        public static SimulationFulfillmentStateSnapshot Apply(
            SimulationFulfillmentStateSnapshot? state, SimulationFulfillmentCommand command)
        {
            ValidateCommand(command);
            if (command.ActionCode == SimulationFulfillmentActionCodes.Configure)
            {
                Require(state is null, "AlreadyConfigured");
                ValidateState(command.InitialState!);
                Require(command.InitialState!.Orders.Length == 0 && command.InitialState.Stocks.All(x => x.ReservedQuantity == 0), "InitialReservationsNotEmpty");
                return Clone(command.InitialState)!;
            }
            Require(state is not null, "NotConfigured");
            ValidateState(state!);
            var next = Clone(state)!;
            if (command.ActionCode == SimulationFulfillmentActionCodes.Reserve)
            {
                var plan = Preview(next, command.Order!);
                Require(plan.IsFeasible, "ReservationRejected:" + string.Join(",", plan.ReasonCodes));
                // 모든 위치·담당자 배분을 확인한 사본에서만 예약합니다. 일부 실패를 원본에 쓰지 않습니다.
                foreach (var group in plan.Allocations.GroupBy(x => x.StockStableId))
                {
                    var stock = next.Stocks.Single(x => x.StockStableId == group.Key);
                    var quantity = group.Sum(x => x.Quantity);
                    Require(stock.OnHandQuantity - stock.ReservedQuantity >= quantity, "ReservationConflict");
                    stock.ReservedQuantity += quantity;
                }
                plan.StateCode = SimulationFulfillmentStateCodes.Reserved;
                next.Orders = next.Orders.Concat(new[] { plan }).ToArray();
            }
            else
            {
                var plan = next.Orders.SingleOrDefault(x => x.OrderStableId == command.OrderStableId);
                Require(plan is not null, "OrderNotFound");
                Require(IsActive(plan!), "OrderNotActive");
                if (command.ActionCode == SimulationFulfillmentActionCodes.Pick)
                {
                    var allocation = plan!.Allocations.SingleOrDefault(x => x.AllocationStableId == command.AllocationStableId);
                    Require(allocation is not null, "AllocationNotFound");
                    var stock = next.Stocks.Single(x => x.StockStableId == allocation!.StockStableId);
                    var location = next.Locations.Single(x => x.LocationStableId == allocation!.LocationStableId);
                    var worker = next.Workers.Single(x => x.WorkerStableId == allocation!.WorkerStableId);
                    var center = next.Centers.Single(x => x.CenterStableId == stock.CenterStableId);
                    Require(center.Enabled && center.OutboundEnabled && stock.Enabled && location.Enabled && worker.Available && worker.CanPick, "PickingResourceUnavailable");
                    Require(command.LocationBarcode == allocation!.LocationBarcode, "LocationBarcodeMismatch");
                    Require(command.ProductBarcode == allocation.ProductBarcode, "ProductBarcodeMismatch");
                    Require(command.Quantity <= allocation.Quantity - allocation.PickedQuantity, "PickQuantityExceeded");
                    Require(stock.OnHandQuantity >= command.Quantity && stock.ReservedQuantity >= command.Quantity, "PickInventoryConflict");
                    stock.OnHandQuantity -= command.Quantity;
                    stock.ReservedQuantity -= command.Quantity;
                    allocation.PickedQuantity += command.Quantity;
                    plan.StateCode = SimulationFulfillmentStateCodes.Picking;
                }
                else if (command.ActionCode == SimulationFulfillmentActionCodes.PackReady)
                {
                    Require(plan!.Allocations.All(x => x.PickedQuantity == x.Quantity), "PickingIncomplete");
                    plan.StateCode = SimulationFulfillmentStateCodes.PackReady;
                }
                else
                {
                    Require(plan!.Allocations.All(x => x.PickedQuantity == 0), "PickedOrderCancellationRequiresRecovery");
                    foreach (var group in plan.Allocations.GroupBy(x => x.StockStableId))
                    {
                        var stock = next.Stocks.Single(x => x.StockStableId == group.Key);
                        var quantity = group.Sum(x => x.Quantity);
                        Require(stock.ReservedQuantity >= quantity, "CancellationReservationConflict");
                        stock.ReservedQuantity -= quantity;
                    }
                    plan.StateCode = SimulationFulfillmentStateCodes.Cancelled;
                }
            }
            ValidateState(next);
            return next;
        }

        private static bool Allocate(SimulationFulfillmentStateSnapshot state, SimulationFulfillmentOrderRequest order,
            string[] centerOrder, out SimulationFulfillmentAllocation[] allocations)
        {
            // 선호 순서는 탐색 순서일 뿐 hard feasibility를 대체하지 않습니다. 잔여망으로 앞선
            // SKU의 배정을 되돌려 다른 센터로 옮길 수 있어 공유 재고·담당자 수용량을 함께 지킵니다.
            var rank = centerOrder.Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index, StringComparer.Ordinal);
            var lines = order.Lines.OrderBy(x => x.LineStableId, StringComparer.Ordinal).ToArray();
            var stocks = UsableStocks(state).Where(x => rank.ContainsKey(x.CenterStableId) && x.OnHandQuantity > x.ReservedQuantity)
                .OrderBy(x => rank[x.CenterStableId]).ThenBy(x => x.LocationStableId, StringComparer.Ordinal)
                .ThenBy(x => x.LotStableId, StringComparer.Ordinal).ThenBy(x => x.StockStableId, StringComparer.Ordinal).ToArray();
            var workers = state.Workers.Where(x => rank.ContainsKey(x.CenterStableId) && WorkerRemaining(state, x) > 0)
                .OrderBy(x => rank[x.CenterStableId]).ThenByDescending(x => WorkerRemaining(state, x))
                .ThenBy(x => x.WorkerStableId, StringComparer.Ordinal).ToArray();
            var source = 0;
            var lineStart = 1;
            var stockStart = lineStart + lines.Length;
            var workerStart = stockStart + stocks.Length * 2;
            var sink = workerStart + workers.Length;
            var graph = new DecimalFlow(sink + 1);
            var lineSupply = new FlowEdge[lines.Length];
            var lineStock = new FlowEdge?[lines.Length, stocks.Length];
            var stockWorker = new FlowEdge?[stocks.Length, workers.Length];
            for (var line = 0; line < lines.Length; line++)
            {
                lineSupply[line] = graph.Add(source, lineStart + line, lines[line].Quantity);
                for (var stock = 0; stock < stocks.Length; stock++)
                    if (Matches(stocks[stock], lines[line]))
                        lineStock[line, stock] = graph.Add(lineStart + line, stockStart + stock * 2, lines[line].Quantity);
            }
            for (var stock = 0; stock < stocks.Length; stock++)
            {
                var available = stocks[stock].OnHandQuantity - stocks[stock].ReservedQuantity;
                graph.Add(stockStart + stock * 2, stockStart + stock * 2 + 1, available);
                for (var worker = 0; worker < workers.Length; worker++)
                    if (workers[worker].CenterStableId == stocks[stock].CenterStableId && workers[worker].CapacityUnitCode == stocks[stock].UnitCode)
                        stockWorker[stock, worker] = graph.Add(stockStart + stock * 2 + 1, workerStart + worker, available);
            }
            for (var worker = 0; worker < workers.Length; worker++)
                graph.Add(workerStart + worker, sink, WorkerRemaining(state, workers[worker]));
            graph.Solve(source, sink);
            if (lineSupply.Any(x => x.Remaining != 0))
            { allocations = Array.Empty<SimulationFulfillmentAllocation>(); return false; }
            var result = new List<SimulationFulfillmentAllocation>();
            for (var stock = 0; stock < stocks.Length; stock++)
            {
                var workerQuantities = Enumerable.Range(0, workers.Length)
                    .Select(worker => stockWorker[stock, worker]?.Flow ?? 0).ToArray();
                for (var line = 0; line < lines.Length; line++)
                {
                    var remaining = lineStock[line, stock]?.Flow ?? 0;
                    for (var worker = 0; worker < workers.Length && remaining > 0; worker++)
                    {
                        var quantity = Math.Min(remaining, workerQuantities[worker]);
                        if (quantity <= 0) continue;
                        var item = stocks[stock];
                        var location = state.Locations.Single(x => x.LocationStableId == item.LocationStableId);
                        result.Add(new SimulationFulfillmentAllocation
                        {
                            AllocationStableId = AllocationId(order.OrderStableId, lines[line].LineStableId, item.StockStableId, workers[worker].WorkerStableId),
                            LineStableId = lines[line].LineStableId, CenterStableId = item.CenterStableId, StockStableId = item.StockStableId,
                            LocationStableId = item.LocationStableId, LocationBarcode = location.LocationBarcode,
                            ProductStableId = item.ProductStableId, ProductBarcode = item.ProductBarcode, UnitCode = item.UnitCode,
                            LotStableId = item.LotStableId, WorkerStableId = workers[worker].WorkerStableId, Quantity = quantity,
                        });
                        remaining -= quantity;
                        workerQuantities[worker] -= quantity;
                    }
                    Require(remaining == 0, "AllocationFlowMismatch");
                }
                Require(workerQuantities.All(x => x == 0), "WorkerFlowMismatch");
            }
            allocations = result.OrderBy(x => x.AllocationStableId, StringComparer.Ordinal).ToArray();
            return true;
        }

        private sealed class FlowEdge
        {
            public FlowEdge(int to, decimal capacity) { To = to; Initial = capacity; Remaining = capacity; }
            public int To { get; }
            public decimal Initial { get; }
            public decimal Remaining { get; set; }
            public decimal Flow => Initial - Remaining;
            public FlowEdge Reverse { get; set; } = null!;
        }
        private sealed class DecimalFlow
        {
            private readonly List<FlowEdge>[] edges;
            public DecimalFlow(int count) => edges = Enumerable.Range(0, count).Select(_ => new List<FlowEdge>()).ToArray();
            public FlowEdge Add(int from, int to, decimal capacity)
            {
                var forward = new FlowEdge(to, capacity);
                var reverse = new FlowEdge(from, 0);
                forward.Reverse = reverse; reverse.Reverse = forward;
                edges[from].Add(forward); edges[to].Add(reverse);
                return forward;
            }
            public void Solve(int source, int sink)
            {
                while (true)
                {
                    var previous = new FlowEdge?[edges.Length];
                    var visited = new bool[edges.Length];
                    var queue = new Queue<int>(); queue.Enqueue(source); visited[source] = true;
                    while (queue.Count > 0 && !visited[sink])
                    {
                        var current = queue.Dequeue();
                        foreach (var edge in edges[current])
                        {
                            if (visited[edge.To] || edge.Remaining <= 0) continue;
                            visited[edge.To] = true; previous[edge.To] = edge; queue.Enqueue(edge.To);
                        }
                    }
                    if (!visited[sink]) return;
                    var quantity = decimal.MaxValue;
                    for (var node = sink; node != source; node = previous[node]!.Reverse.To)
                        quantity = Math.Min(quantity, previous[node]!.Remaining);
                    for (var node = sink; node != source; node = previous[node]!.Reverse.To)
                    { previous[node]!.Remaining -= quantity; previous[node]!.Reverse.Remaining += quantity; }
                }
            }
        }

        private static IEnumerable<SimulationFulfillmentStock> UsableStocks(SimulationFulfillmentStateSnapshot state)
            => state.Stocks.Where(x => x.Enabled && state.Locations.Any(location => location.LocationStableId == x.LocationStableId && location.Enabled));
        private static bool Matches(SimulationFulfillmentStock stock, SimulationFulfillmentOrderLine line)
            => stock.ProductStableId == line.ProductStableId && stock.ProductBarcode == line.ProductBarcode && stock.UnitCode == line.UnitCode;
        private static bool IsActive(SimulationFulfillmentPlanSnapshot order)
            => order.StateCode == SimulationFulfillmentStateCodes.Reserved || order.StateCode == SimulationFulfillmentStateCodes.Picking;
        private static decimal WorkerRemaining(SimulationFulfillmentStateSnapshot state, SimulationFulfillmentWorker worker)
        {
            if (!worker.Available || !worker.CanPick) return 0;
            var active = state.Orders.Where(IsActive).Where(x => x.Allocations.Any(a => a.WorkerStableId == worker.WorkerStableId)).ToArray();
            if (active.Length >= worker.MaximumActiveOrders) return 0;
            return Math.Max(0, worker.MaximumActiveQuantity - active.SelectMany(x => x.Allocations)
                .Where(x => x.WorkerStableId == worker.WorkerStableId).Sum(x => x.Quantity));
        }

        public static void ValidateState(SimulationFulfillmentStateSnapshot state)
        {
            if (state is null) throw new SimulationContractException("SimulationFulfillmentStateMissing");
            Id(state.ScenarioStableId); Id(state.ConfigurationRevision);
            Items(state.Centers); Items(state.Locations); Items(state.Stocks); Items(state.Workers); Items(state.Orders);
            Unique(state.Centers.Select(x => x.CenterStableId)); Unique(state.Locations.Select(x => x.LocationStableId));
            Unique(state.Stocks.Select(x => x.StockStableId)); Unique(state.Workers.Select(x => x.WorkerStableId));
            Unique(state.Orders.Select(x => x.OrderStableId));
            foreach (var center in state.Centers) Text(center.DisplayName);
            var locationKeys = state.Locations.Select(x => Pair(x.CenterStableId, x.LocationBarcode)).ToArray();
            Require(locationKeys.Distinct(StringComparer.Ordinal).Count() == locationKeys.Length, "DuplicateLocationBarcode");
            foreach (var location in state.Locations)
            { Id(location.CenterStableId); Id(location.LocationBarcode); Require(state.Centers.Any(x => x.CenterStableId == location.CenterStableId), "LocationCenterMissing"); }
            foreach (var stock in state.Stocks)
            {
                Id(stock.CenterStableId); Id(stock.LocationStableId); Id(stock.ProductStableId); Id(stock.ProductBarcode); Id(stock.UnitCode); Id(stock.LotStableId);
                Require(state.Locations.Any(x => x.LocationStableId == stock.LocationStableId && x.CenterStableId == stock.CenterStableId), "StockLocationMismatch");
                Require(stock.OnHandQuantity >= 0 && stock.ReservedQuantity >= 0 && stock.ReservedQuantity <= stock.OnHandQuantity, "InvalidStockQuantities");
            }
            foreach (var barcode in state.Stocks.GroupBy(x => x.ProductBarcode, StringComparer.Ordinal))
                Require(barcode.Select(x => Pair(x.ProductStableId, x.UnitCode)).Distinct().Count() == 1, "AmbiguousProductBarcode");
            foreach (var worker in state.Workers)
            {
                Id(worker.CenterStableId); Id(worker.CapacityUnitCode); Text(worker.DisplayName);
                Require(state.Centers.Any(x => x.CenterStableId == worker.CenterStableId), "WorkerCenterMissing");
                Require(worker.MaximumActiveQuantity > 0 && worker.MaximumActiveOrders > 0, "InvalidWorkerCapacity");
            }
            var allAllocations = new List<SimulationFulfillmentAllocation>();
            foreach (var plan in state.Orders)
            {
                ValidateOrder(plan.Order, state);
                Items(plan.Allocations); Items(plan.CandidateCenters); Strings(plan.SelectedCenterStableIds); Strings(plan.ReasonCodes);
                Require(plan.OrderStableId == plan.Order.OrderStableId && plan.IsFeasible && plan.Allocations.Length > 0 && plan.ReasonCodes.Length == 0, "InvalidStoredPlan");
                Require(IsActive(plan) || plan.StateCode == SimulationFulfillmentStateCodes.PackReady || plan.StateCode == SimulationFulfillmentStateCodes.Cancelled, "InvalidOrderState");
                Unique(plan.CandidateCenters.Select(x => x.CenterStableId));
                foreach (var candidate in plan.CandidateCenters)
                { Require(state.Centers.Any(x => x.CenterStableId == candidate.CenterStableId), "CandidateCenterMissing"); Text(candidate.DisplayName); Strings(candidate.ReasonCodes); Require(Nonnegative(candidate.EstimatedDistanceKm) && Nonnegative(candidate.EstimatedCost), "InvalidEstimate"); }
                var centers = plan.Allocations.Select(x => x.CenterStableId).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
                Require(centers.SequenceEqual(plan.SelectedCenterStableIds.OrderBy(x => x, StringComparer.Ordinal)), "SelectedCentersMismatch");
                Require(plan.Order.AllowSplitCenters || centers.Length == 1, "StoredSplitCentersNotAllowed");
                Require(plan.Order.AllowedCenterStableIds.Length == 0 || centers.All(x => plan.Order.AllowedCenterStableIds.Contains(x)), "StoredCenterNotAllowed");
                foreach (var allocation in plan.Allocations)
                {
                    Id(allocation.AllocationStableId); Id(allocation.StockStableId); Id(allocation.WorkerStableId);
                    var line = plan.Order.Lines.SingleOrDefault(x => x.LineStableId == allocation.LineStableId);
                    var stock = state.Stocks.SingleOrDefault(x => x.StockStableId == allocation.StockStableId);
                    var worker = state.Workers.SingleOrDefault(x => x.WorkerStableId == allocation.WorkerStableId);
                    Require(line is not null && stock is not null && worker is not null, "AllocationReferenceMissing");
                    Require(Matches(stock!, line!) && allocation.ProductStableId == stock!.ProductStableId
                        && allocation.ProductBarcode == stock.ProductBarcode && allocation.UnitCode == stock.UnitCode
                        && allocation.LotStableId == stock.LotStableId && allocation.CenterStableId == stock.CenterStableId
                        && allocation.LocationStableId == stock.LocationStableId && worker!.CenterStableId == stock.CenterStableId
                        && worker.CapacityUnitCode == allocation.UnitCode
                        && allocation.LocationBarcode == state.Locations.Single(x => x.LocationStableId == stock.LocationStableId).LocationBarcode, "AllocationReferenceMismatch");
                    Require(allocation.AllocationStableId == AllocationId(plan.OrderStableId, allocation.LineStableId, allocation.StockStableId, allocation.WorkerStableId), "AllocationIdMismatch");
                    Require(allocation.Quantity > 0 && allocation.PickedQuantity >= 0 && allocation.PickedQuantity <= allocation.Quantity, "InvalidAllocationQuantities");
                    if (plan.StateCode == SimulationFulfillmentStateCodes.Reserved || plan.StateCode == SimulationFulfillmentStateCodes.Cancelled)
                        Require(allocation.PickedQuantity == 0, "InvalidUnpickedOrderState");
                    if (plan.StateCode == SimulationFulfillmentStateCodes.PackReady)
                        Require(allocation.PickedQuantity == allocation.Quantity, "InvalidPackReadyState");
                    allAllocations.Add(allocation);
                }
                foreach (var line in plan.Order.Lines)
                    Require(plan.Allocations.Where(x => x.LineStableId == line.LineStableId).Sum(x => x.Quantity) == line.Quantity, "OrderAllocationQuantityMismatch");
            }
            Unique(allAllocations.Select(x => x.AllocationStableId));
            foreach (var stock in state.Stocks)
            {
                var reserved = state.Orders.Where(IsActive).SelectMany(x => x.Allocations).Where(x => x.StockStableId == stock.StockStableId)
                    .Sum(x => x.Quantity - x.PickedQuantity);
                Require(stock.ReservedQuantity == reserved, "ReservationLedgerMismatch");
            }
            foreach (var worker in state.Workers)
            {
                var active = state.Orders.Where(IsActive).Where(x => x.Allocations.Any(a => a.WorkerStableId == worker.WorkerStableId)).ToArray();
                Require(active.Length <= worker.MaximumActiveOrders && active.SelectMany(x => x.Allocations)
                    .Where(x => x.WorkerStableId == worker.WorkerStableId).Sum(x => x.Quantity) <= worker.MaximumActiveQuantity, "WorkerCapacityExceeded");
            }
        }

        private static void ValidateOrder(SimulationFulfillmentOrderRequest order, SimulationFulfillmentStateSnapshot state)
        {
            ValidateOrderShape(order);
            foreach (var id in order.AllowedCenterStableIds) Require(state.Centers.Any(x => x.CenterStableId == id), "AllowedCenterMissing");
            foreach (var estimate in order.CenterEstimates)
                Require(state.Centers.Any(x => x.CenterStableId == estimate.CenterStableId), "EstimateCenterMissing");
        }

        private static void ValidateOrderShape(SimulationFulfillmentOrderRequest order)
        {
            if (order is null) throw new SimulationContractException("SimulationFulfillmentOrderMissing");
            Id(order.OrderStableId);
            Items(order.Lines); Items(order.CenterEstimates); Strings(order.AllowedCenterStableIds);
            Require(order.Lines.Length > 0, "OrderLinesMissing");
            Unique(order.Lines.Select(x => x.LineStableId)); Unique(order.CenterEstimates.Select(x => x.CenterStableId));
            Unique(order.AllowedCenterStableIds);
            foreach (var line in order.Lines)
            { Id(line.ProductStableId); Id(line.ProductBarcode); Id(line.UnitCode); Require(line.Quantity > 0, "InvalidOrderQuantity"); }
            foreach (var estimate in order.CenterEstimates)
                Require(Nonnegative(estimate.EstimatedDistanceKm) && Nonnegative(estimate.EstimatedCost), "InvalidEstimate");
        }

        private static void ValidateCommand(SimulationFulfillmentCommand command)
        {
            if (command is null) throw new SimulationContractException("SimulationFulfillmentCommandMissing");
            Id(command.CommandId); Require(command.ExpectedRevision >= 0, "InvalidExpectedRevision");
            if (command.ActionCode == SimulationFulfillmentActionCodes.Configure)
                Require(command.InitialState is not null && command.Order is null && Empty(command.OrderStableId)
                    && Empty(command.AllocationStableId) && Empty(command.LocationBarcode) && Empty(command.ProductBarcode) && command.Quantity == 0, "InvalidConfigureCommand");
            else if (command.ActionCode == SimulationFulfillmentActionCodes.Reserve)
                Require(command.InitialState is null && command.Order is not null && (Empty(command.OrderStableId) || command.OrderStableId == command.Order.OrderStableId)
                    && Empty(command.AllocationStableId) && Empty(command.LocationBarcode) && Empty(command.ProductBarcode) && command.Quantity == 0, "InvalidReserveCommand");
            else if (command.ActionCode == SimulationFulfillmentActionCodes.Pick)
            { Require(command.InitialState is null && command.Order is null && command.Quantity > 0, "InvalidPickCommand"); Id(command.OrderStableId); Id(command.AllocationStableId); Id(command.LocationBarcode); Id(command.ProductBarcode); }
            else if (command.ActionCode == SimulationFulfillmentActionCodes.PackReady || command.ActionCode == SimulationFulfillmentActionCodes.Cancel)
            { Require(command.InitialState is null && command.Order is null && Empty(command.AllocationStableId) && Empty(command.LocationBarcode) && Empty(command.ProductBarcode) && command.Quantity == 0, "InvalidOrderCommand"); Id(command.OrderStableId); }
            else throw new SimulationContractException("SimulationFulfillmentUnknownAction");
        }

        public static SimulationFulfillmentStateSnapshot? Clone(SimulationFulfillmentStateSnapshot? state)
        {
            if (state is null) return null;
            ValidateState(state);
            return new SimulationFulfillmentStateSnapshot
            {
                ScenarioStableId = state.ScenarioStableId, ConfigurationRevision = state.ConfigurationRevision,
                Centers = state.Centers.Select(x => new SimulationFulfillmentCenter { CenterStableId = x.CenterStableId, DisplayName = x.DisplayName, Enabled = x.Enabled, OutboundEnabled = x.OutboundEnabled }).ToArray(),
                Locations = state.Locations.Select(x => new SimulationFulfillmentLocation { LocationStableId = x.LocationStableId, CenterStableId = x.CenterStableId, LocationBarcode = x.LocationBarcode, Enabled = x.Enabled }).ToArray(),
                Stocks = state.Stocks.Select(x => new SimulationFulfillmentStock { StockStableId = x.StockStableId, CenterStableId = x.CenterStableId, LocationStableId = x.LocationStableId, ProductStableId = x.ProductStableId, ProductBarcode = x.ProductBarcode, UnitCode = x.UnitCode, LotStableId = x.LotStableId, OnHandQuantity = x.OnHandQuantity, ReservedQuantity = x.ReservedQuantity, Enabled = x.Enabled }).ToArray(),
                Workers = state.Workers.Select(x => new SimulationFulfillmentWorker { WorkerStableId = x.WorkerStableId, DisplayName = x.DisplayName, CenterStableId = x.CenterStableId, Available = x.Available, CanPick = x.CanPick, CapacityUnitCode = x.CapacityUnitCode, MaximumActiveQuantity = x.MaximumActiveQuantity, MaximumActiveOrders = x.MaximumActiveOrders }).ToArray(),
                Orders = state.Orders.Select(ClonePlan).ToArray(),
            };
        }

        public static SimulationFulfillmentOrderRequest CloneOrder(SimulationFulfillmentOrderRequest order)
        {
            ValidateOrderShape(order);
            return new SimulationFulfillmentOrderRequest
            {
                OrderStableId = order.OrderStableId, AllowSplitCenters = order.AllowSplitCenters,
                AllowedCenterStableIds = order.AllowedCenterStableIds.ToArray(),
                Lines = order.Lines.Select(x => new SimulationFulfillmentOrderLine { LineStableId = x.LineStableId, ProductStableId = x.ProductStableId, ProductBarcode = x.ProductBarcode, UnitCode = x.UnitCode, Quantity = x.Quantity }).ToArray(),
                CenterEstimates = order.CenterEstimates.Select(x => new SimulationFulfillmentCenterEstimate { CenterStableId = x.CenterStableId, EstimatedDistanceKm = x.EstimatedDistanceKm, EstimatedCost = x.EstimatedCost }).ToArray(),
            };
        }

        public static SimulationFulfillmentCommand CloneCommand(SimulationFulfillmentCommand command)
        {
            ValidateCommand(command);
            return new SimulationFulfillmentCommand { CommandId = command.CommandId, ExpectedRevision = command.ExpectedRevision, ActionCode = command.ActionCode,
                InitialState = Clone(command.InitialState), Order = command.Order is null ? null : CloneOrder(command.Order), OrderStableId = command.OrderStableId,
                AllocationStableId = command.AllocationStableId, LocationBarcode = command.LocationBarcode, ProductBarcode = command.ProductBarcode, Quantity = command.Quantity };
        }

        private static SimulationFulfillmentPlanSnapshot ClonePlan(SimulationFulfillmentPlanSnapshot plan)
        {
            if (plan is null) throw new SimulationContractException("SimulationFulfillmentPlanMissing");
            Items(plan.CandidateCenters); Items(plan.Allocations); Strings(plan.ReasonCodes); Strings(plan.SelectedCenterStableIds);
            return new SimulationFulfillmentPlanSnapshot
            {
                OrderStableId = plan.OrderStableId, Order = CloneOrder(plan.Order), StateCode = plan.StateCode, IsFeasible = plan.IsFeasible,
                ReasonCodes = plan.ReasonCodes.ToArray(), SelectedCenterStableIds = plan.SelectedCenterStableIds.ToArray(),
                CandidateCenters = plan.CandidateCenters.Select(x => new SimulationFulfillmentCenterCandidate { CenterStableId = x.CenterStableId, DisplayName = x.DisplayName, IsEligible = x.IsEligible, CanFulfillWholeOrder = x.CanFulfillWholeOrder, ReasonCodes = x.ReasonCodes?.ToArray() ?? throw new SimulationContractException("SimulationFulfillmentCandidateReasonsMissing"), EstimatedDistanceKm = x.EstimatedDistanceKm, EstimatedCost = x.EstimatedCost, PreferenceScore = x.PreferenceScore }).ToArray(),
                Allocations = plan.Allocations.Select(x => new SimulationFulfillmentAllocation { AllocationStableId = x.AllocationStableId, LineStableId = x.LineStableId, CenterStableId = x.CenterStableId, StockStableId = x.StockStableId, LocationStableId = x.LocationStableId, LocationBarcode = x.LocationBarcode, ProductStableId = x.ProductStableId, ProductBarcode = x.ProductBarcode, UnitCode = x.UnitCode, LotStableId = x.LotStableId, WorkerStableId = x.WorkerStableId, Quantity = x.Quantity, PickedQuantity = x.PickedQuantity }).ToArray(),
            };
        }

        public static string BuildStateKey(SimulationFulfillmentStateSnapshot? state)
        {
            var key = new Canonical(); key.Add("simulation-fulfillment-state.v1");
            if (state is null) { key.Add((string?)null); return key.ToString(); }
            ValidateState(state); key.Add(state.ScenarioStableId); key.Add(state.ConfigurationRevision);
            key.List(state.Centers, x => x.CenterStableId, x => { key.Add(x.CenterStableId); key.Add(x.DisplayName); key.Add(x.Enabled); key.Add(x.OutboundEnabled); });
            key.List(state.Locations, x => x.LocationStableId, x => { key.Add(x.LocationStableId); key.Add(x.CenterStableId); key.Add(x.LocationBarcode); key.Add(x.Enabled); });
            key.List(state.Stocks, x => x.StockStableId, x => { key.Add(x.StockStableId); key.Add(x.CenterStableId); key.Add(x.LocationStableId); key.Add(x.ProductStableId); key.Add(x.ProductBarcode); key.Add(x.UnitCode); key.Add(x.LotStableId); key.Add(x.OnHandQuantity); key.Add(x.ReservedQuantity); key.Add(x.Enabled); });
            key.List(state.Workers, x => x.WorkerStableId, x => { key.Add(x.WorkerStableId); key.Add(x.DisplayName); key.Add(x.CenterStableId); key.Add(x.Available); key.Add(x.CanPick); key.Add(x.CapacityUnitCode); key.Add(x.MaximumActiveQuantity); key.Add(x.MaximumActiveOrders); });
            key.List(state.Orders, x => x.OrderStableId, x => WritePlan(key, x));
            return key.ToString();
        }

        public static string BuildCommandKey(SimulationFulfillmentCommand command)
        {
            ValidateCommand(command);
            var key = new Canonical(); key.Add("simulation-fulfillment-command.v1"); key.Add(command.ActionCode);
            // 명령 ID와 기대 세션 revision은 상위의 재시도/동시성 계약입니다.
            key.Add(BuildStateKey(command.InitialState));
            if (command.Order is null) key.Add((string?)null); else { ValidateOrderShape(command.Order); WriteOrder(key, command.Order); }
            key.Add(command.OrderStableId); key.Add(command.AllocationStableId); key.Add(command.LocationBarcode); key.Add(command.ProductBarcode); key.Add(command.Quantity);
            return key.ToString();
        }

        private static void WriteOrder(Canonical key, SimulationFulfillmentOrderRequest order)
        {
            key.Add(order.OrderStableId); key.Add(order.AllowSplitCenters);
            key.List(order.AllowedCenterStableIds, x => x, key.Add);
            key.List(order.Lines, x => x.LineStableId, x => { key.Add(x.LineStableId); key.Add(x.ProductStableId); key.Add(x.ProductBarcode); key.Add(x.UnitCode); key.Add(x.Quantity); });
            key.List(order.CenterEstimates, x => x.CenterStableId, x => { key.Add(x.CenterStableId); key.Add(x.EstimatedDistanceKm); key.Add(x.EstimatedCost); });
        }
        private static void WritePlan(Canonical key, SimulationFulfillmentPlanSnapshot plan)
        {
            key.Add(plan.OrderStableId); WriteOrder(key, plan.Order); key.Add(plan.StateCode); key.Add(plan.IsFeasible);
            key.List(plan.ReasonCodes, x => x, key.Add); key.List(plan.SelectedCenterStableIds, x => x, key.Add);
            key.List(plan.CandidateCenters, x => x.CenterStableId, x => { key.Add(x.CenterStableId); key.Add(x.DisplayName); key.Add(x.IsEligible); key.Add(x.CanFulfillWholeOrder); key.List(x.ReasonCodes, y => y, key.Add); key.Add(x.EstimatedDistanceKm); key.Add(x.EstimatedCost); key.Add(x.PreferenceScore); });
            key.List(plan.Allocations, x => x.AllocationStableId, x => { key.Add(x.AllocationStableId); key.Add(x.LineStableId); key.Add(x.CenterStableId); key.Add(x.StockStableId); key.Add(x.LocationStableId); key.Add(x.LocationBarcode); key.Add(x.ProductStableId); key.Add(x.ProductBarcode); key.Add(x.UnitCode); key.Add(x.LotStableId); key.Add(x.WorkerStableId); key.Add(x.Quantity); key.Add(x.PickedQuantity); });
        }
        private static SimulationFulfillmentPlanSnapshot Reject(SimulationFulfillmentPlanSnapshot plan, string reason)
        { plan.StateCode = SimulationFulfillmentStateCodes.Rejected; plan.IsFeasible = false; plan.ReasonCodes = new[] { reason }; plan.Allocations = Array.Empty<SimulationFulfillmentAllocation>(); return plan; }
        private static string AllocationId(string order, string line, string stock, string worker)
        {
            var key = new Canonical(); key.Add(order); key.Add(line); key.Add(stock); key.Add(worker);
            using (var sha = SHA256.Create())
                return "fulfillment-allocation:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(key.ToString())).Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private static string Pair(string first, string second) { var key = new Canonical(); key.Add(first); key.Add(second); return key.ToString(); }
        private static void Items<T>(T[] items) where T : class => Require(items is not null && items.All(x => x is not null), "ArrayMissingOrNullItem");
        private static void Strings(string[] items)
        {
            if (items is null) throw new SimulationContractException("SimulationFulfillmentArrayMissing");
            foreach (var item in items) Id(item);
        }
        private static void Unique(IEnumerable<string> ids) { var values = ids.ToArray(); foreach (var value in values) Id(value); Require(values.Distinct(StringComparer.Ordinal).Count() == values.Length, "DuplicateStableId"); }
        private static bool Nonnegative(decimal? value) => !value.HasValue || value.Value >= 0;
        private static bool Empty(string text) => text == string.Empty;
        private static void Id(string text) => Require(!string.IsNullOrWhiteSpace(text) && text.Length <= 200 && text == text.Trim() && !text.Any(char.IsControl), "InvalidIdentifier");
        private static void Text(string text) => Require(text is not null && text.Length <= 1000 && !text.Any(char.IsControl), "InvalidDisplayText");
        private static void Require(bool condition, string reason) { if (!condition) throw new SimulationContractException("SimulationFulfillment" + reason); }

        private sealed class WholeCenterChoice
        {
            public WholeCenterChoice(string center, long warehouse, decimal score, long stock = 0) { Center = center; Warehouse = warehouse; Score = score; Stock = stock; }
            public string Center { get; } public long Warehouse { get; } public decimal Score { get; } public long Stock { get; }
        }
        private sealed class Canonical
        {
            private readonly StringBuilder value = new StringBuilder();
            public void Add(string? text) { if (text is null) value.Append("N;"); else value.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text).Append(';'); }
            public void Add(decimal number) => Add(number.ToString("G29", CultureInfo.InvariantCulture));
            public void Add(decimal? number) { if (number.HasValue) Add(number.Value); else Add((string?)null); }
            public void Add(bool flag) => Add(flag ? "true" : "false");
            public void Add(int number) => Add(number.ToString(CultureInfo.InvariantCulture));
            public void List<T>(IEnumerable<T> items, Func<T, string> sortKey, Action<T> write)
            { var array = items.OrderBy(sortKey, StringComparer.Ordinal).ToArray(); Add(array.Length); foreach (var item in array) write(item); }
            public override string ToString() => value.ToString();
        }
    }
}
