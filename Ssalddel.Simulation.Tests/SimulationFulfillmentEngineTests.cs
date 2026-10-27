using System.Globalization;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "합성 출고 배분·예약·피킹의 수량 보존과 결정성을 검증한다.",
    Boundary = "운영 재고·실제 스캐너·배송 완료·Unity Play Mode 증거가 아니다.")]
public sealed class SimulationFulfillmentEngineTests
{
    [Fact]
    public void Configure는한번만허용하고입력과참조를공유하지않는다()
    {
        var initial = State();
        var command = Command(SimulationFulfillmentActionCodes.Configure);
        command.InitialState = initial;
        var configured = SimulationFulfillmentEngine.Apply(null, command);
        Assert.Equal(SimulationFulfillmentEngine.BuildStateKey(initial), SimulationFulfillmentEngine.BuildStateKey(configured));
        initial.Stocks[0].OnHandQuantity = 999;
        initial.Workers[0].CapacityUnitCode = "kg";
        Assert.Equal(6, configured.Stocks[0].OnHandQuantity);
        Assert.Equal("piece", configured.Workers[0].CapacityUnitCode);
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(configured, command));
        Assert.Null(SimulationFulfillmentEngine.Clone(null));
    }

    [Fact]
    public void Configure는진행중원장과예약수량을초기설정으로받지않는다()
    {
        var reserved = Reserve(State(), Order());
        var command = Command(SimulationFulfillmentActionCodes.Configure);
        command.InitialState = reserved;
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(null, command));
        var invalid = State();
        invalid.Stocks[0].ReservedQuantity = 1;
        command.InitialState = invalid;
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(null, command));
    }

    [Theory]
    [InlineData("center")]
    [InlineData("outbound")]
    [InlineData("location")]
    [InlineData("stock")]
    [InlineData("worker")]
    [InlineData("skill")]
    [InlineData("capacity")]
    [InlineData("unit")]
    public void 가까운견적도센터재고담당자의불가조건을넘지못한다(string restriction)
    {
        var state = State();
        switch (restriction)
        {
            case "center": state.Centers[0].Enabled = false; break;
            case "outbound": state.Centers[0].OutboundEnabled = false; break;
            case "location": state.Locations[0].Enabled = false; break;
            case "stock": state.Stocks[0].Enabled = false; break;
            case "worker": state.Workers[0].Available = false; break;
            case "skill": state.Workers[0].CanPick = false; break;
            case "capacity": state.Workers[0].MaximumActiveQuantity = 0.1m; break;
            case "unit": state.Workers[0].CapacityUnitCode = "kg"; break;
        }
        var order = Order();
        order.AllowedCenterStableIds = new[] { "center:a" };
        var before = SimulationFulfillmentEngine.BuildStateKey(state);
        var preview = SimulationFulfillmentEngine.Preview(state, order);
        Assert.False(preview.IsFeasible);
        Assert.Empty(preview.Allocations);
        Assert.Contains(preview.CandidateCenters, x => x.CenterStableId == "center:a" && !x.CanFulfillWholeOrder);
        Assert.Equal(before, SimulationFulfillmentEngine.BuildStateKey(state));
    }

    [Fact]
    public void 센터선택은명시추정값과사용자허용범위를사용한다()
    {
        var state = State();
        var order = Order();
        order.CenterEstimates[0].EstimatedDistanceKm = 9;
        order.CenterEstimates[1].EstimatedDistanceKm = 1;
        var preview = SimulationFulfillmentEngine.Preview(state, order);
        Assert.Equal("center:b", Assert.Single(preview.SelectedCenterStableIds));
        Assert.All(preview.Allocations, x => Assert.Equal("worker:b", x.WorkerStableId));
        order.AllowedCenterStableIds = new[] { "center:a" };
        preview = SimulationFulfillmentEngine.Preview(state, order);
        Assert.Equal("center:a", Assert.Single(preview.SelectedCenterStableIds));
        Assert.Contains("CenterNotAllowed", preview.CandidateCenters.Single(x => x.CenterStableId == "center:b").ReasonCodes);
        order.CenterEstimates = Array.Empty<SimulationFulfillmentCenterEstimate>();
        Assert.All(SimulationFulfillmentEngine.Preview(state, order).CandidateCenters, x =>
        {
            Assert.Null(x.EstimatedDistanceKm);
            Assert.Null(x.EstimatedCost);
            Assert.Null(x.PreferenceScore);
        });
    }

    [Fact]
    public void 분할이필요하면미허용시차단하고허용시모든센터를원자예약한다()
    {
        var state = State();
        var order = Order(quantity: 8);
        var before = SimulationFulfillmentEngine.BuildStateKey(state);
        var denied = SimulationFulfillmentEngine.Preview(state, order);
        Assert.False(denied.IsFeasible);
        Assert.Contains("SplitCentersNotAllowed", denied.ReasonCodes);
        Assert.Empty(denied.Allocations);
        Assert.Throws<SimulationContractException>(() => Reserve(state, order));
        Assert.Equal(before, SimulationFulfillmentEngine.BuildStateKey(state));
        order.AllowSplitCenters = true;
        var reserved = Reserve(state, order);
        Assert.Equal(new[] { "center:a", "center:b" }, Assert.Single(reserved.Orders).SelectedCenterStableIds);
        Assert.Equal(10, reserved.Stocks.Sum(x => x.OnHandQuantity));
        Assert.Equal(8, reserved.Stocks.Sum(x => x.ReservedQuantity));
        Assert.Equal(0, state.Stocks.Sum(x => x.ReservedQuantity));
    }

    [Fact]
    public void 중복상품라인은공유재고를두번배정하지않는다()
    {
        var state = State();
        var order = Order(quantity: 4);
        order.AllowedCenterStableIds = new[] { "center:a" };
        var second = Line("line:2", 4);
        order.Lines = new[] { order.Lines[0], second };
        Assert.False(SimulationFulfillmentEngine.Preview(state, order).IsFeasible);
        second.Quantity = 2;
        var reserved = Reserve(state, order);
        Assert.Equal(6, reserved.Stocks.Single(x => x.StockStableId == "stock:a").ReservedQuantity);
        Assert.Equal(2, Assert.Single(reserved.Orders).Allocations.Select(x => x.AllocationStableId).Distinct().Count());
        Assert.All(order.Lines, line => Assert.Equal(line.Quantity,
            Assert.Single(reserved.Orders).Allocations.Where(x => x.LineStableId == line.LineStableId).Sum(x => x.Quantity)));
    }

    [Fact]
    public void 센터내부담당용량이막히면앞선상품배정을되돌려다른센터로재계획한다()
    {
        var state = State();
        state.Stocks[0].OnHandQuantity = 1;
        state.Stocks[1].OnHandQuantity = 1;
        state.Stocks = state.Stocks.Concat(new[] { new SimulationFulfillmentStock
        {
            StockStableId = "stock:a:second", CenterStableId = "center:a", LocationStableId = "location:a",
            ProductStableId = "product:beans", ProductBarcode = "SKU-BEANS", UnitCode = "piece", LotStableId = "lot:beans", OnHandQuantity = 1,
        } }).ToArray();
        foreach (var worker in state.Workers) worker.MaximumActiveQuantity = 1;
        var order = Order(quantity: 1);
        order.AllowSplitCenters = true;
        order.Lines = new[] { Line("line:1", 1), new SimulationFulfillmentOrderLine
        { LineStableId = "line:2", ProductStableId = "product:beans", ProductBarcode = "SKU-BEANS", UnitCode = "piece", Quantity = 1 } };
        var preview = SimulationFulfillmentEngine.Preview(state, order);
        Assert.True(preview.IsFeasible);
        Assert.Equal("center:b", Assert.Single(preview.Allocations, x => x.LineStableId == "line:1").CenterStableId);
        Assert.Equal("center:a", Assert.Single(preview.Allocations, x => x.LineStableId == "line:2").CenterStableId);
        Assert.Equal(2, Reserve(state, order).Stocks.Sum(x => x.ReservedQuantity));
    }

    [Fact]
    public void 소수수량은절삭없이예약하고부분피킹한다()
    {
        var initial = State();
        var order = Order(quantity: 0.625m);
        var reserved = Reserve(initial, order);
        var allocation = Assert.Single(Assert.Single(reserved.Orders).Allocations);
        Assert.Equal(0.625m, allocation.Quantity);
        Assert.Equal(6, reserved.Stocks[0].OnHandQuantity);
        Assert.Equal(0.625m, reserved.Stocks[0].ReservedQuantity);
        var before = SimulationFulfillmentEngine.BuildStateKey(reserved);
        var picked = SimulationFulfillmentEngine.Apply(reserved, Pick(reserved, 0.125m));
        Assert.Equal(5.875m, picked.Stocks[0].OnHandQuantity);
        Assert.Equal(0.5m, picked.Stocks[0].ReservedQuantity);
        Assert.Equal(0.125m, Assert.Single(Assert.Single(picked.Orders).Allocations).PickedQuantity);
        Assert.Equal(SimulationFulfillmentStateCodes.Picking, Assert.Single(picked.Orders).StateCode);
        Assert.Equal(before, SimulationFulfillmentEngine.BuildStateKey(reserved));
    }

    [Theory]
    [InlineData("location")]
    [InlineData("product")]
    [InlineData("allocation")]
    [InlineData("excess")]
    [InlineData("zero")]
    [InlineData("negative")]
    public void 위치상품수량대조실패는원본재고와진행상태를그대로보존한다(string mismatch)
    {
        var reserved = Reserve(State(), Order());
        var before = SimulationFulfillmentEngine.BuildStateKey(reserved);
        var command = Pick(reserved, 1);
        switch (mismatch)
        {
            case "location": command.LocationBarcode = "OTHER-LOCATION"; break;
            case "product": command.ProductBarcode = "OTHER-PRODUCT"; break;
            case "allocation": command.AllocationStableId = "allocation:missing"; break;
            case "excess": command.Quantity = 4; break;
            case "zero": command.Quantity = 0; break;
            case "negative": command.Quantity = -1; break;
        }
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(reserved, command));
        Assert.Equal(before, SimulationFulfillmentEngine.BuildStateKey(reserved));
    }

    [Theory]
    [InlineData("center")]
    [InlineData("outbound")]
    [InlineData("location")]
    [InlineData("stock")]
    [InlineData("worker")]
    public void 예약후자원이중지되면피킹을실행하지않는다(string resource)
    {
        var reserved = Reserve(State(), Order());
        switch (resource)
        {
            case "center": reserved.Centers[0].Enabled = false; break;
            case "outbound": reserved.Centers[0].OutboundEnabled = false; break;
            case "location": reserved.Locations[0].Enabled = false; break;
            case "stock": reserved.Stocks[0].Enabled = false; break;
            case "worker": reserved.Workers[0].Available = false; break;
        }
        var before = SimulationFulfillmentEngine.BuildStateKey(reserved);
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(reserved, Pick(reserved, 1)));
        Assert.Equal(before, SimulationFulfillmentEngine.BuildStateKey(reserved));
    }

    [Fact]
    public void 전량피킹후포장준비는재고를다시차감하거나배송완료로바꾸지않는다()
    {
        var reserved = Reserve(State(), Order());
        var ready = Command(SimulationFulfillmentActionCodes.PackReady, "order:1");
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(reserved, ready));
        var picked = SimulationFulfillmentEngine.Apply(reserved, Pick(reserved, 3));
        var packed = SimulationFulfillmentEngine.Apply(picked, ready);
        Assert.Equal(SimulationFulfillmentStateCodes.PackReady, Assert.Single(packed.Orders).StateCode);
        Assert.Equal(3, packed.Stocks[0].OnHandQuantity);
        Assert.Equal(0, packed.Stocks[0].ReservedQuantity);
        Assert.Equal(picked.Stocks.Sum(x => x.OnHandQuantity), packed.Stocks.Sum(x => x.OnHandQuantity));
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(packed, ready));
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(packed, Pick(packed, 1)));
    }

    [Fact]
    public void 취소는미피킹예약만해제하고이미피킹한주문은복구없이취소하지않는다()
    {
        var reserved = Reserve(State(), Order());
        var cancel = Command(SimulationFulfillmentActionCodes.Cancel, "order:1");
        var cancelled = SimulationFulfillmentEngine.Apply(reserved, cancel);
        Assert.Equal(SimulationFulfillmentStateCodes.Cancelled, Assert.Single(cancelled.Orders).StateCode);
        Assert.Equal(10, cancelled.Stocks.Sum(x => x.OnHandQuantity));
        Assert.Equal(0, cancelled.Stocks.Sum(x => x.ReservedQuantity));
        Assert.False(SimulationFulfillmentEngine.Preview(cancelled, Order()).IsFeasible);
        var picked = SimulationFulfillmentEngine.Apply(reserved, Pick(reserved, 1));
        var before = SimulationFulfillmentEngine.BuildStateKey(picked);
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(picked, cancel));
        Assert.Equal(before, SimulationFulfillmentEngine.BuildStateKey(picked));
    }

    [Fact]
    public void 담당자수량과주문수용량은독립조건이며포장준비때점유가끝난다()
    {
        var state = State();
        state.Workers[0].MaximumActiveQuantity = 3;
        state.Workers[0].MaximumActiveOrders = 2;
        var first = Order(quantity: 2);
        first.AllowedCenterStableIds = new[] { "center:a" };
        var reserved = Reserve(state, first);
        var second = Order("order:2", 2);
        second.AllowedCenterStableIds = new[] { "center:a" };
        Assert.False(SimulationFulfillmentEngine.Preview(reserved, second).IsFeasible);
        second.Lines[0].Quantity = 1;
        var two = Reserve(reserved, second);
        var third = Order("order:3", 0.1m);
        third.AllowedCenterStableIds = new[] { "center:a" };
        two.Workers[0].MaximumActiveQuantity = 20;
        Assert.False(SimulationFulfillmentEngine.Preview(two, third).IsFeasible);
        var picked = SimulationFulfillmentEngine.Apply(reserved, Pick(reserved, 2));
        second.Lines[0].Quantity = 2;
        Assert.False(SimulationFulfillmentEngine.Preview(picked, second).IsFeasible);
        var packed = SimulationFulfillmentEngine.Apply(picked, Command(SimulationFulfillmentActionCodes.PackReady, "order:1"));
        Assert.True(SimulationFulfillmentEngine.Preview(packed, second).IsFeasible);
    }

    [Fact]
    public void 다른단위의라인은서로다른담당용량으로계산한다()
    {
        var state = State();
        state.Stocks[0].OnHandQuantity = 1;
        state.Stocks = state.Stocks.Concat(new[] { new SimulationFulfillmentStock
        {
            StockStableId = "stock:kg", CenterStableId = "center:a", LocationStableId = "location:a",
            ProductStableId = "product:flour", ProductBarcode = "SKU-FLOUR", UnitCode = "kg", LotStableId = "lot:kg", OnHandQuantity = 2,
        } }).ToArray();
        state.Workers[0].MaximumActiveQuantity = 1;
        var order = Order(quantity: 1);
        order.AllowedCenterStableIds = new[] { "center:a" };
        order.Lines = order.Lines.Concat(new[] { new SimulationFulfillmentOrderLine
        { LineStableId = "line:kg", ProductStableId = "product:flour", ProductBarcode = "SKU-FLOUR", UnitCode = "kg", Quantity = 2 } }).ToArray();
        Assert.False(SimulationFulfillmentEngine.Preview(state, order).IsFeasible);
        state.Workers = state.Workers.Concat(new[] { new SimulationFulfillmentWorker
        { WorkerStableId = "worker:kg", CenterStableId = "center:a", CapacityUnitCode = "kg", MaximumActiveQuantity = 2 } }).ToArray();
        var reserved = Reserve(state, order);
        Assert.Equal("worker:kg", Assert.Single(Assert.Single(reserved.Orders).Allocations, x => x.UnitCode == "kg").WorkerStableId);
        Assert.Equal("worker:a", Assert.Single(Assert.Single(reserved.Orders).Allocations, x => x.UnitCode == "piece").WorkerStableId);
    }

    [Fact]
    public void 사본은중첩주문견적후보배분배열까지독립적이다()
    {
        var order = Order();
        var reserved = Reserve(State(), order);
        var baseline = SimulationFulfillmentEngine.BuildStateKey(reserved);
        order.Lines[0].Quantity = 500;
        order.CenterEstimates[0].EstimatedDistanceKm = 999;
        var copy = SimulationFulfillmentEngine.Clone(reserved)!;
        copy.Orders[0].Order.Lines[0].Quantity = 9;
        copy.Orders[0].Order.CenterEstimates[0].EstimatedCost = 999;
        copy.Orders[0].CandidateCenters[0].ReasonCodes = new[] { "changed" };
        copy.Orders[0].Allocations[0].PickedQuantity = 1;
        copy.Orders[0].SelectedCenterStableIds[0] = "changed";
        Assert.Equal(baseline, SimulationFulfillmentEngine.BuildStateKey(reserved));
        var configure = Command(SimulationFulfillmentActionCodes.Configure);
        configure.InitialState = State();
        var clone = SimulationFulfillmentEngine.CloneCommand(configure);
        clone.InitialState!.Stocks[0].OnHandQuantity = 999;
        Assert.Equal(6, configure.InitialState.Stocks[0].OnHandQuantity);
        var reserveCommand = Command(SimulationFulfillmentActionCodes.Reserve);
        reserveCommand.Order = Order();
        clone = SimulationFulfillmentEngine.CloneCommand(reserveCommand);
        clone.Order!.CenterEstimates[0].EstimatedCost = 999;
        Assert.Null(reserveCommand.Order.CenterEstimates[0].EstimatedCost);
    }

    [Fact]
    public void 상태키와명령키는배열순서문화권재시도revision과무관하고업무값을보존한다()
    {
        var reserved = Reserve(State(), Order());
        var key = SimulationFulfillmentEngine.BuildStateKey(reserved);
        var reversed = SimulationFulfillmentEngine.Clone(reserved)!;
        reversed.Centers = reversed.Centers.Reverse().ToArray();
        reversed.Stocks = reversed.Stocks.Reverse().ToArray();
        reversed.Locations = reversed.Locations.Reverse().ToArray();
        reversed.Workers = reversed.Workers.Reverse().ToArray();
        reversed.Orders[0].CandidateCenters = reversed.Orders[0].CandidateCenters.Reverse().ToArray();
        reversed.Orders[0].Order.CenterEstimates = reversed.Orders[0].Order.CenterEstimates.Reverse().ToArray();
        Assert.Equal(key, SimulationFulfillmentEngine.BuildStateKey(reversed));
        var command = Command(SimulationFulfillmentActionCodes.Reserve);
        command.Order = Order(quantity: 0.125m);
        var commandKey = SimulationFulfillmentEngine.BuildCommandKey(command);
        command.ExpectedRevision = 999;
        command.CommandId = "command:retry";
        Assert.Equal(commandKey, SimulationFulfillmentEngine.BuildCommandKey(command));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(commandKey, SimulationFulfillmentEngine.BuildCommandKey(command));
            Assert.Equal(key, SimulationFulfillmentEngine.BuildStateKey(reserved));
        }
        finally { CultureInfo.CurrentCulture = culture; }
        command.Order.Lines[0].Quantity = 0.126m;
        Assert.NotEqual(commandKey, SimulationFulfillmentEngine.BuildCommandKey(command));
        reversed.Workers[0].MaximumActiveQuantity += 1;
        Assert.NotEqual(key, SimulationFulfillmentEngine.BuildStateKey(reversed));
    }

    [Fact]
    public void 상태키는문자열경계충돌을막고긴복합위치키를임의거부하지않는다()
    {
        var left = State();
        left.ScenarioStableId = "ab"; left.ConfigurationRevision = "c";
        var right = State();
        right.ScenarioStableId = "a"; right.ConfigurationRevision = "bc";
        Assert.NotEqual(SimulationFulfillmentEngine.BuildStateKey(left), SimulationFulfillmentEngine.BuildStateKey(right));
        left.Locations[0].LocationBarcode = new string('x', 190);
        left.Centers[0].CenterStableId = new string('a', 190);
        left.Locations[0].CenterStableId = left.Centers[0].CenterStableId;
        left.Stocks[0].CenterStableId = left.Centers[0].CenterStableId;
        left.Workers[0].CenterStableId = left.Centers[0].CenterStableId;
        SimulationFulfillmentEngine.ValidateState(left);
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("overreserved")]
    [InlineData("unownedreservation")]
    [InlineData("locationref")]
    [InlineData("duplicate")]
    [InlineData("nullarray")]
    [InlineData("nullitem")]
    [InlineData("workerunit")]
    public void 잘못된상태는조용히보정하지않는다(string invalid)
    {
        var state = State();
        switch (invalid)
        {
            case "negative": state.Stocks[0].OnHandQuantity = -1; break;
            case "overreserved": state.Stocks[0].ReservedQuantity = 7; break;
            case "unownedreservation": state.Stocks[0].ReservedQuantity = 1; break;
            case "locationref": state.Stocks[0].LocationStableId = "missing"; break;
            case "duplicate": state.Stocks[1].StockStableId = state.Stocks[0].StockStableId; break;
            case "nullarray": state.Stocks = null!; break;
            case "nullitem": state.Stocks[0] = null!; break;
            case "workerunit": state.Workers[0].CapacityUnitCode = string.Empty; break;
        }
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.ValidateState(state));
    }

    [Fact]
    public void 저장된배분의위치단위허용센터와예약원장을다시검증한다()
    {
        var reserved = Reserve(State(), Order());
        var wrong = SimulationFulfillmentEngine.Clone(reserved)!;
        wrong.Orders[0].Allocations[0].LocationBarcode = "changed";
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.ValidateState(wrong));
        wrong = SimulationFulfillmentEngine.Clone(reserved)!;
        wrong.Orders[0].Order.AllowedCenterStableIds = new[] { "center:b" };
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.ValidateState(wrong));
        wrong = SimulationFulfillmentEngine.Clone(reserved)!;
        wrong.Workers[0].CapacityUnitCode = "kg";
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.ValidateState(wrong));
        wrong = SimulationFulfillmentEngine.Clone(reserved)!;
        wrong.Stocks[0].ReservedQuantity = 2;
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.ValidateState(wrong));
    }

    [Fact]
    public void 알수없는명령과null주문배열은상태를변경하지않고거부한다()
    {
        var state = State();
        var key = SimulationFulfillmentEngine.BuildStateKey(state);
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(state, Command("Unknown")));
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(state, null!));
        var command = Command(SimulationFulfillmentActionCodes.Reserve);
        command.Order = Order(); command.Order.Lines = null!;
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.BuildCommandKey(command));
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.CloneCommand(command));
        Assert.Throws<SimulationContractException>(() => SimulationFulfillmentEngine.Apply(state, command));
        Assert.Equal(key, SimulationFulfillmentEngine.BuildStateKey(state));
    }

    private static SimulationFulfillmentStateSnapshot State() => new()
    {
        ScenarioStableId = "scenario:fulfillment:test", ConfigurationRevision = "fixture:v1",
        Centers = new[]
        {
            new SimulationFulfillmentCenter { CenterStableId = "center:a", DisplayName = "합성 A센터" },
            new SimulationFulfillmentCenter { CenterStableId = "center:b", DisplayName = "합성 B센터" },
        },
        Locations = new[]
        {
            new SimulationFulfillmentLocation { LocationStableId = "location:a", CenterStableId = "center:a", LocationBarcode = "LOC-A" },
            new SimulationFulfillmentLocation { LocationStableId = "location:b", CenterStableId = "center:b", LocationBarcode = "LOC-B" },
        },
        Stocks = new[]
        {
            new SimulationFulfillmentStock { StockStableId = "stock:a", CenterStableId = "center:a", LocationStableId = "location:a", ProductStableId = "product:rice", ProductBarcode = "SKU-RICE", UnitCode = "piece", LotStableId = "lot:a", OnHandQuantity = 6 },
            new SimulationFulfillmentStock { StockStableId = "stock:b", CenterStableId = "center:b", LocationStableId = "location:b", ProductStableId = "product:rice", ProductBarcode = "SKU-RICE", UnitCode = "piece", LotStableId = "lot:b", OnHandQuantity = 4 },
        },
        Workers = new[]
        {
            new SimulationFulfillmentWorker { WorkerStableId = "worker:a", DisplayName = "합성 담당자 A", CenterStableId = "center:a", CapacityUnitCode = "piece", MaximumActiveQuantity = 20, MaximumActiveOrders = 2 },
            new SimulationFulfillmentWorker { WorkerStableId = "worker:b", DisplayName = "합성 담당자 B", CenterStableId = "center:b", CapacityUnitCode = "piece", MaximumActiveQuantity = 20, MaximumActiveOrders = 2 },
        },
    };

    private static SimulationFulfillmentOrderRequest Order(string id = "order:1", decimal quantity = 3) => new()
    {
        OrderStableId = id, Lines = new[] { Line("line:1", quantity) },
        CenterEstimates = new[]
        {
            new SimulationFulfillmentCenterEstimate { CenterStableId = "center:a", EstimatedDistanceKm = 1 },
            new SimulationFulfillmentCenterEstimate { CenterStableId = "center:b", EstimatedDistanceKm = 5 },
        },
    };

    private static SimulationFulfillmentOrderLine Line(string id, decimal quantity) => new()
    { LineStableId = id, ProductStableId = "product:rice", ProductBarcode = "SKU-RICE", UnitCode = "piece", Quantity = quantity };

    private static SimulationFulfillmentCommand Command(string action, string orderId = "") => new()
    { CommandId = "command:test:" + action, ActionCode = action, OrderStableId = orderId };

    private static SimulationFulfillmentStateSnapshot Reserve(SimulationFulfillmentStateSnapshot state, SimulationFulfillmentOrderRequest order)
    {
        var command = Command(SimulationFulfillmentActionCodes.Reserve); command.Order = order;
        return SimulationFulfillmentEngine.Apply(state, command);
    }

    private static SimulationFulfillmentCommand Pick(SimulationFulfillmentStateSnapshot state, decimal quantity)
    {
        var allocation = Assert.Single(state.Orders[0].Allocations);
        return new SimulationFulfillmentCommand
        {
            CommandId = "command:pick", ActionCode = SimulationFulfillmentActionCodes.Pick, OrderStableId = state.Orders[0].OrderStableId,
            AllocationStableId = allocation.AllocationStableId, LocationBarcode = allocation.LocationBarcode,
            ProductBarcode = allocation.ProductBarcode, Quantity = quantity,
        };
    }
}
