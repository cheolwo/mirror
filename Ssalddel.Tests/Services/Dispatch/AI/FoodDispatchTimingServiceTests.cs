using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class FoodDispatchTimingServiceTests
{
    [Theory]
    [InlineData("Disabled")]
    [InlineData("Cargo")]
    [InlineData("Market")]
    [InlineData("ActiveOffer")]
    [InlineData("Retry")]
    [InlineData("Expired")]
    public async Task 적용대상이아니거나재배차기존제안상한이면DB와모델을읽지않는다(string scenario)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.Queue("current");
        var options = new FoodDispatchTimingOptions { Enabled = scenario != "Disabled" };
        if (scenario == "Cargo") queue.배차업무유형 = 상태값.배차업무유형.용달운송;
        if (scenario == "Market") queue.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트음식주문;
        if (scenario == "ActiveOffer") queue.현재추천대상기사Id = "private-driver";
        if (scenario == "Retry") queue.추천라운드 = 1;
        if (scenario == "Expired") queue.CreatedAt = f.Clock.Now.AddSeconds(-61).UtcDateTime;
        await f.Connection.CloseAsync();

        var result = await f.Service(options).EvaluateAsync(queue);

        Assert.NotEqual("Collect", result.Decision.ActionCode);
        Assert.NotNull(result.Input.InformationReadiness);
        Assert.Equal(scenario == "Expired" ? "Missing" : "NotApplicable", result.Input.InformationReadiness.SchedulingStateCode);
        Assert.Equal(0, f.Demand.Calls);
        Assert.Equal(0, f.AI.Calls);
        Assert.Equal(0, f.Order.Reads);
    }

    [Fact]
    public async Task 최근유입이있고현재가용기사가있으면최초접수부터30초까지만수집한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        var before = System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync());

        var result = await f.Service().EvaluateAsync(queue);

        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Equal(new DateTimeOffset(queue.CreatedAt, TimeSpan.Zero).AddSeconds(30), result.Decision.RecheckAtUtc);
        Assert.Equal(new DateTimeOffset(queue.CreatedAt, TimeSpan.Zero).AddSeconds(60), result.Decision.HardDeadlineUtc);
        Assert.Equal(1, result.Input.WaitingOrders);
        Assert.Equal(1, result.Input.CompatibleOrders);
        Assert.Equal(0.1m, result.Input.ExpectedNewOrders);
        Assert.Equal(1, result.Input.AvailableDrivers);
        Assert.Equal("Applied", result.AIStatusCode);
        Assert.Equal("Current", result.Input.InformationReadiness!.SchedulingStateCode);
        Assert.Equal("Missing", result.Input.InformationReadiness.CoordinationStateCode);
        Assert.All(result.Input.InformationReadiness.Evidence.Take(6), x => Assert.Equal("Current", x.StateCode));
        Assert.All(result.Input.InformationReadiness.Evidence.Skip(6), x => Assert.Equal("Missing", x.StateCode));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()));
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 현재주문만있으면미래유입을만들지않고즉시기존후보를처리한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(arrival: false);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(0m, result.Input.ExpectedNewOrders);
        Assert.Equal("NoExpectedNewOrders", result.Decision.ReasonCode);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 호환주문이쌓이면모델로더기다리지않고즉시진행한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.Db.운송원장.Add(f.Queue("compatible"));
        await f.PersistAsync();
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(2, result.Input.CompatibleOrders);
        Assert.True(result.Input.PromisingPlanAvailable);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("Stale")]
    [InlineData("Future")]
    [InlineData("NoDriver")]
    [InlineData("DuplicateDriver")]
    [InlineData("DuplicatePriority")]
    public async Task 수요나기사정보가미확인이면기다리는판단을모델에맡기지않는다(string scenario)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        if (scenario == "Missing") f.Demand.Value = null;
        if (scenario == "Stale") f.Demand.Value!.판정시각Utc = f.Clock.Now.AddMinutes(-6);
        if (scenario == "Future") f.Demand.Value!.판정시각Utc = f.Clock.Now.AddSeconds(1);
        if (scenario == "NoDriver") f.Demand.Value!.기사판정목록 = [];
        if (scenario == "DuplicateDriver") f.Demand.Value!.기사판정목록 = [Driver("same", 1), Driver("same", 2)];
        if (scenario == "DuplicatePriority") f.Demand.Value!.기사판정목록 = [Driver("first", 1), Driver("second", 1)];
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData("Database", "SnapshotReadFailed")]
    [InlineData("Truncated", "SnapshotTruncated")]
    [InlineData("NotObserved", "CurrentQueueNotObserved")]
    [InlineData("Region", "RegionUnknown")]
    public async Task 조회실패잘림현재주문누락권역미확인은즉시기존흐름으로복귀한다(string scenario, string status)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        var options = new FoodDispatchTimingOptions { Enabled = true };
        if (scenario == "Database") await f.Connection.CloseAsync();
        if (scenario == "Truncated") options.MaximumQueueRows = 1;
        if (scenario == "NotObserved") queue = f.Queue("not-persisted");
        if (scenario == "Region") queue.픽업_위도 = null;
        var result = await f.Service(options).EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(status, result.AIStatusCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 화물마트취소기존추천다른권역은수집대상과신규유입에넣지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(arrival: false);
        foreach (var kind in new[] { "cargo", "market", "cancelled", "offered", "outside" })
        {
            var other = f.Queue(kind);
            if (kind == "cargo") other.배차업무유형 = 상태값.배차업무유형.용달운송;
            if (kind == "market") other.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트음식주문;
            if (kind == "cancelled") other.상태 = 상태값.배차상태.취소;
            if (kind == "offered") { other.현재추천대상기사Id = "driver"; other.CreatedAt = f.Clock.Now.AddHours(-1).UtcDateTime; }
            if (kind == "outside") other.픽업_위도 = 36m;
            f.Db.운송원장.Add(other);
        }
        await f.PersistAsync();
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(1, result.Input.WaitingOrders);
        Assert.Equal(0m, result.Input.ExpectedNewOrders);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
    }

    [Theory]
    [InlineData("Confirmed")]
    [InlineData("Carrier")]
    public async Task 현재원장에확정기사가있으면대기상태오래된투영이어도수집하지않는다(string kind)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = f.Queue("reserved");
        if (kind == "Confirmed") queue.확정기사Id = "driver";
        else queue.기사_운송자 = "driver";
        await f.Connection.CloseAsync();
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("ExistingFlow", result.Decision.ActionCode);
        Assert.Equal(0, f.Demand.Calls);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 다른오래된주문과UpdatedAt이현재주문의고정수집기준을바꾸지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        queue.UpdatedAt = f.Clock.Now.AddDays(1).UtcDateTime;
        var other = f.Queue("old-distant");
        other.CreatedAt = f.Clock.Now.AddSeconds(-100).UtcDateTime;
        other.픽업_위도 = 37.52m;
        f.Db.운송원장.Add(other);
        await f.PersistAsync();
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Equal(new DateTimeOffset(queue.CreatedAt, TimeSpan.Zero).AddSeconds(30), result.Decision.RecheckAtUtc);
    }

    [Theory]
    [InlineData("Timeout")]
    [InlineData("NetworkFailure")]
    [InlineData("InvalidResponse")]
    public async Task 모델실패는기다림을추가하지않는다(string failure)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.AI.Selection = new("Collect", failure);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Null(result.Decision.RecheckAtUtc);
        Assert.Equal(failure, result.AIStatusCode);
    }

    [Fact]
    public async Task 모델응답중수집기한이끝나면Collect응답도서버가기각한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(ageSeconds: 20);
        f.AI.AfterCall = () => f.Clock.Now = f.Clock.Now.AddSeconds(11);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal("CollectionElapsed", result.Decision.ReasonCode);
        Assert.Equal("ServerRequiredAfterAI", result.AIStatusCode);
    }

    [Fact]
    public async Task 모델꺼짐은서버의짧은수집판정을사용한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.AI.Enabled = false;
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Equal("Disabled", result.AIStatusCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 첫계획에서추천으로전환된시도1회는재배차로오인하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        queue.계획배차시도횟수 = 1;
        var result = await f.Service().EvaluateAsync(queue);
        Assert.False(result.Input.IsRetry);
        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Equal(1, f.AI.Calls);
    }

    [Fact]
    public async Task 오래된수요판정시각을현재DB조회시각으로승격하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.Demand.Value!.판정시각Utc = f.Clock.Now.AddSeconds(-30);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(f.Demand.Value.판정시각Utc, result.Input.SnapshotAtUtc);
        Assert.Equal("SnapshotStale", result.Decision.ReasonCode);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 원주문조리예정완료임박은즉시처리하고미확인은최대수집상한만적용한다(bool readyKnown)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        queue.원본의뢰Id = "original-food-order";
        f.Db.운송원장.Attach(queue);
        f.Db.Entry(queue).Property(x => x.원본의뢰Id).IsModified = true;
        await f.PersistAsync();
        var order = new FoodOrderPort(readyKnown ? f.Clock.Now.AddSeconds(10).UtcDateTime : null);
        order.Value!.배차대기Id = queue.Id;
        var result = await f.Service(order: order).EvaluateAsync(queue);
        Assert.Equal("original-food-order", order.RequestedId);
        Assert.Equal(readyKnown ? "DispatchNow" : "Collect", result.Decision.ActionCode);
        Assert.Equal(readyKnown ? f.Clock.Now.AddSeconds(10) : null, result.Input.LatestDispatchAtUtc);
        Assert.Equal(readyKnown ? 0 : 1, f.AI.Calls);
    }

    [Fact]
    public async Task 잘못된정책설정은예외로주문처리를끊지않고기존배차로돌린다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Connection.CloseAsync();
        var result = await f.Service(new() { Enabled = true, Policy = new() { MaxWaitSeconds = 0 } }).EvaluateAsync(f.Queue("current"));
        Assert.Equal("InvalidConfiguration", result.AIStatusCode);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData("Missing", "OrderNotFound")]
    [InlineData("Unavailable", "OrderSourceUnavailable")]
    [InlineData("OrderNumber", "OrderLinkMismatch")]
    [InlineData("QueueLink", "OrderLinkMismatch")]
    [InlineData("Revision", "OrderLinkMismatch")]
    [InlineData("UnknownState", "OrderNotActive")]
    [InlineData("Cancelled", "OrderNotActive")]
    [InlineData("PickedUp", "OrderNotActive")]
    [InlineData("Delivered", "OrderNotActive")]
    public async Task 원주문누락연결불일치종결픽업이후는추가대기만금지한다(string scenario, string reason)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        if (scenario == "Missing") f.Order.Value = null;
        if (scenario == "OrderNumber") f.Order.Value!.주문번호 = "other-order";
        if (scenario == "QueueLink") f.Order.Value!.배차대기Id = queue.Id + 1;
        if (scenario == "Revision") f.Order.Value!.Revision = 0;
        if (scenario == "UnknownState") f.Order.Value!.상태 = "unrecognized";
        if (scenario == "Cancelled") f.Order.Value!.상태 = 음식주문상태코드.취소;
        if (scenario == "PickedUp") f.Order.Value!.상태 = 음식주문상태코드.픽업완료;
        if (scenario == "Delivered") f.Order.Value!.상태 = 음식주문상태코드.전달완료;
        var before = System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync());

        var result = await f.Service(withoutOrderStore: scenario == "Unavailable").EvaluateAsync(queue);

        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal("InformationMissing", result.Decision.ReasonCode);
        Assert.Equal("Missing", result.Input.InformationReadiness!.SchedulingStateCode);
        Assert.Equal(reason, Evidence(result, FoodDispatchInformationKeys.Order).ReasonCode);
        Assert.Equal(0, f.AI.Calls);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()));
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("Pickup")]
    [InlineData("Dropoff")]
    public async Task 픽업전달어느좌표든없으면위치근거가부족하다(string side)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        if (side == "Pickup") queue.픽업_위도 = null;
        else queue.하차_경도 = null;
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal("Missing", Evidence(result, FoodDispatchInformationKeys.Locations).StateCode);
        Assert.Equal("LocationsMissing", Evidence(result, FoodDispatchInformationKeys.Locations).ReasonCode);
        Assert.Equal(0, f.Demand.Calls);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData("Demand", "Revision")]
    [InlineData("Demand", "State")]
    [InlineData("Demand", "QueueLink")]
    [InlineData("Demand", "Estimate")]
    [InlineData("AI", "Revision")]
    [InlineData("AI", "State")]
    [InlineData("AI", "QueueLink")]
    [InlineData("AI", "Estimate")]
    public async Task 자료조회나모델대기중원주문판본상태연결조리가바뀌면수집을취소한다(string stage, string change)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        var originalTime = f.Clock.Now;
        Action mutate = () =>
        {
            if (change == "Revision") f.Order.Value!.Revision++;
            if (change == "State") f.Order.Value!.상태 = 음식주문상태코드.취소;
            if (change == "QueueLink") f.Order.Value!.배차대기Id = queue.Id + 1;
            if (change == "Estimate") f.Order.Value!.조리예상완료시각Utc = f.Clock.Now.AddMinutes(5).UtcDateTime;
        };
        if (stage == "Demand") f.Demand.AfterCall = mutate;
        else f.AI.AfterCall = mutate;

        var result = await f.Service().EvaluateAsync(queue);

        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal("InformationStale", result.Decision.ReasonCode);
        Assert.Equal("Stale", result.Input.InformationReadiness!.SchedulingStateCode);
        var order = Evidence(result, FoodDispatchInformationKeys.Order);
        Assert.Equal("OrderChanged", order.ReasonCode);
        Assert.Equal(1L, order.SourceRevision);
        Assert.Equal(originalTime, order.ObservedAtUtc);
        Assert.Equal("PreparationChanged", Evidence(result, FoodDispatchInformationKeys.Preparation).ReasonCode);
        Assert.Equal(stage == "AI" ? 1 : 0, f.AI.Calls);
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 재조리이전준비완료와예정시각을현재차수상한으로쓰지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        var oldReady = f.Clock.Now.AddMinutes(-1).UtcDateTime;
        var recooking = f.Clock.Now.AddSeconds(-10).UtcDateTime;
        f.Order.Value!.상태 = 음식주문상태코드.조리중;
        f.Order.Value.CurrentPreparationRound = 2;
        f.Order.Value.CurrentCookingStartedAtUtc = recooking;
        f.Order.Value.RecookingRequestedAtUtc = recooking;
        f.Order.Value.픽업준비시각Utc = oldReady;
        f.Order.Value.조리예상완료시각Utc = oldReady;
        f.Order.Value.상태이력 =
        [
            new() { 다음상태 = 음식주문상태코드.픽업대기, 사유 = "음식점 픽업 준비 완료", 전이시각Utc = oldReady },
            new() { 다음상태 = 음식주문상태코드.조리중, 사유 = 음식주문현재조리Policy.재조리요청이력사유, 전이시각Utc = recooking }
        ];
        f.Order.Value.주문번호 = "current";
        var before = System.Text.Json.JsonSerializer.Serialize(f.Order.Value);

        var result = await f.Service().EvaluateAsync(queue);

        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Null(result.Input.LatestDispatchAtUtc);
        Assert.Equal("Current", Evidence(result, FoodDispatchInformationKeys.Preparation).StateCode);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(f.Order.Value));
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("NoMinutes")]
    [InlineData("PreviousRoundFutureEstimate")]
    [InlineData("CurrentRoundEstimate")]
    public async Task 재조리차수의예정시각은미래값이어도확정상한으로재사용하지않는다(string scenario)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        var recooking = f.Clock.Now.AddSeconds(-5).UtcDateTime;
        f.Order.Value!.상태 = 음식주문상태코드.조리중;
        f.Order.Value.CurrentPreparationRound = 2;
        f.Order.Value.CurrentCookingStartedAtUtc = recooking;
        f.Order.Value.RecookingRequestedAtUtc = recooking;
        f.Order.Value.조리예상분 = scenario == "NoMinutes" ? null : 20;
        f.Order.Value.조리예상완료시각Utc = scenario == "CurrentRoundEstimate"
            ? recooking.AddMinutes(20) : recooking.AddMinutes(10);
        f.Order.Value.상태이력 = [new() { 다음상태 = 음식주문상태코드.조리중,
            사유 = 음식주문현재조리Policy.재조리요청이력사유, 전이시각Utc = recooking }];
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("Collect", result.Decision.ActionCode);
        Assert.Null(result.Input.LatestDispatchAtUtc);
        Assert.Equal("Current", Evidence(result, FoodDispatchInformationKeys.Preparation).StateCode);
    }

    [Theory]
    [InlineData("Round")]
    [InlineData("CurrentCooking")]
    [InlineData("CurrentReady")]
    [InlineData("Recooking")]
    [InlineData("CookingWithoutStart")]
    [InlineData("ReadyWithoutTime")]
    public async Task 현재차수선언과조리이력이모순되면과거준비정보를재사용하지않는다(string scenario)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        if (scenario == "Round") f.Order.Value!.CurrentPreparationRound = 2;
        if (scenario == "CurrentCooking") f.Order.Value!.CurrentCookingStartedAtUtc = f.Clock.Now.AddMinutes(-1).UtcDateTime;
        if (scenario == "CurrentReady") f.Order.Value!.CurrentPickupReadyAtUtc = f.Clock.Now.AddMinutes(-1).UtcDateTime;
        if (scenario == "Recooking") f.Order.Value!.RecookingRequestedAtUtc = f.Clock.Now.AddMinutes(-1).UtcDateTime;
        if (scenario == "CookingWithoutStart") f.Order.Value!.상태 = 음식주문상태코드.조리중;
        if (scenario == "ReadyWithoutTime") f.Order.Value!.상태 = 음식주문상태코드.픽업대기;
        f.Order.Value!.조리예상완료시각Utc = f.Clock.Now.AddMinutes(5).UtcDateTime;
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Null(result.Input.LatestDispatchAtUtc);
        Assert.Equal("Missing", Evidence(result, FoodDispatchInformationKeys.Preparation).StateCode);
        Assert.Equal("InvalidPreparation", Evidence(result, FoodDispatchInformationKeys.Preparation).ReasonCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("Unspecified")]
    public async Task 조리근거시각은동일UTC축으로정규화한다(string kind)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        var actual = f.Clock.Now.AddSeconds(10);
        f.Order.Value!.조리예상완료시각Utc = kind == "Local"
            ? actual.LocalDateTime : DateTime.SpecifyKind(actual.UtcDateTime, DateTimeKind.Unspecified);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(actual, result.Input.LatestDispatchAtUtc);
        Assert.Equal("DeadlineApproaching", result.Decision.ReasonCode);
        Assert.Equal("Current", Evidence(result, FoodDispatchInformationKeys.Preparation).StateCode);
    }

    [Fact]
    public async Task 확인된수요처리여력기사수신규유입0은누락이아닌현재근거다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(arrival: false);
        f.Demand.Value!.예상업무량건수 = 0m;
        f.Demand.Value.기존기사가용처리건수 = 0m;
        f.Demand.Value.기사판정목록 = [];
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("Current", result.Input.InformationReadiness!.SchedulingStateCode);
        Assert.Equal("Current", Evidence(result, FoodDispatchInformationKeys.Demand).StateCode);
        Assert.Equal("Current", Evidence(result, FoodDispatchInformationKeys.DriverCapacity).StateCode);
        Assert.Equal(0, result.Input.AvailableDrivers);
        Assert.Equal(0m, result.Input.ExpectedNewOrders);
        Assert.Equal("NoAvailableDrivers", result.Decision.ReasonCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Theory]
    [InlineData(-1, "Stale", "ObservationFuture")]
    [InlineData(15, "Current", "InformationObserved")]
    [InlineData(16, "Stale", "ObservationStale")]
    [InlineData(360, "Stale", "ObservationStale")]
    public async Task 수요근거원래관측시각의미래와유효경계를보존한다(int age, string state, string reason)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync();
        f.Demand.Value!.판정시각Utc = f.Clock.Now.AddSeconds(-age);
        var result = await f.Service().EvaluateAsync(queue);
        foreach (var key in new[] { FoodDispatchInformationKeys.Demand, FoodDispatchInformationKeys.DriverCapacity })
        {
            Assert.Equal(state, Evidence(result, key).StateCode);
            Assert.Equal(reason, Evidence(result, key).ReasonCode);
            Assert.Equal(f.Demand.Value.판정시각Utc, Evidence(result, key).ObservedAtUtc);
        }
        Assert.Equal(state == "Current" ? "Collect" : "DispatchNow", result.Decision.ActionCode);
        Assert.Equal(state == "Current" ? 1 : 0, f.AI.Calls);
    }

    [Fact]
    public async Task 모델대기중준비정보유효기간이끝나면다시관측한것처럼갱신하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(ageSeconds: 1);
        var observedAt = f.Clock.Now;
        f.AI.AfterCall = () => f.Clock.Now = f.Clock.Now.AddSeconds(16);
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal("SnapshotStale", result.Decision.ReasonCode);
        Assert.Equal("Stale", result.Input.InformationReadiness!.SchedulingStateCode);
        Assert.Equal("ObservationStale", Evidence(result, FoodDispatchInformationKeys.Order).ReasonCode);
        Assert.Equal(observedAt, Evidence(result, FoodDispatchInformationKeys.Order).ObservedAtUtc);
        Assert.Equal("ServerRequiredAfterAI", result.AIStatusCode);
        Assert.Equal(3, f.Order.Reads);
    }

    [Fact]
    public async Task 모델꺼짐이어도원주문재조회시간경과를최종시각에포함한다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = await f.SeedAsync(ageSeconds: 1);
        var observedAt = f.Clock.Now;
        f.AI.Enabled = false;
        f.Order.BeforeRead = read => { if (read == 2) f.Clock.Now = f.Clock.Now.AddSeconds(16); };
        var result = await f.Service().EvaluateAsync(queue);
        Assert.Equal(observedAt.AddSeconds(16), result.Input.NowUtc);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal("Stale", result.Input.InformationReadiness!.SchedulingStateCode);
        Assert.Equal(0, f.AI.Calls);
    }

    [Fact]
    public async Task 정책설정이null이어도준비상태를반환하고즉시복귀한다()
    {
        await using var f = await Fixture.CreateAsync();
        var result = await f.Service(new() { Enabled = true, Policy = null! }).EvaluateAsync(f.Queue("current"));
        Assert.Equal("InvalidConfiguration", result.AIStatusCode);
        Assert.Equal("DispatchNow", result.Decision.ActionCode);
        Assert.Equal("Missing", result.Input.InformationReadiness!.SchedulingStateCode);
        Assert.Equal(0, f.Order.Reads);
    }

    private static FoodDispatchInformationEvidence Evidence(FoodDispatchTimingEvaluation result, string key)
        => Assert.Single(result.Input.InformationReadiness!.Evidence, x => x.Key == key);

    [Fact]
    public async Task 호출자취소는복구판정으로숨기지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service().EvaluateAsync(f.Queue("current"), cancellation.Token));
        Assert.Equal(0, f.Demand.Calls);
        Assert.Equal(0, f.AI.Calls);
    }

    private static 음식배달수요기사판정 Driver(string id, int priority)
        => new() { 기사Id = id, 우선순위 = priority, 신규제안허용 = true };

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;
        public SsalddelContext Db { get; } = db;
        public MutableClock Clock { get; } = new();
        public DemandPort Demand { get; } = new();
        public AIPort AI { get; } = new();
        public FoodOrderPort Order { get; } = new();
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new EncryptionPort());
            await db.Database.EnsureCreatedAsync();
            var f = new Fixture(connection, db);
            f.Demand.Value = new() { 정책판본 = 음식배달수요참여Policy.현재정책판본,
                판정시각Utc = f.Clock.Now, 데이터상태Code = 음식배달수요자료상태Code.최신,
                예상업무량건수 = 1m, 기존기사가용처리건수 = 4m, 기사판정목록 = [Driver("private-driver", 1)] };
            return f;
        }
        public 운송원장 Queue(string id, int ageSeconds = 5) => new()
        {
            의뢰Id = id,
            원본의뢰Id = id, 운송번호 = $"transport-{id}", 배차업무유형 = 상태값.배차업무유형.음식배달,
            상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천대기,
            픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.501m, 하차_경도 = 127.001m,
            CreatedAt = Clock.Now.AddSeconds(-ageSeconds).UtcDateTime, UpdatedAt = Clock.Now.UtcDateTime
        };
        public async Task<운송원장> SeedAsync(bool arrival = true, int ageSeconds = 5)
        {
            var queue = Queue("current", ageSeconds);
            Db.운송원장.Add(queue);
            if (arrival) { var previous = Queue("completed-arrival", 50); previous.상태 = "완료"; Db.운송원장.Add(previous); }
            await PersistAsync();
            Order.Value!.배차대기Id = queue.Id;
            return queue;
        }
        public Task PersistAsync() => PersistCoreAsync();
        private async Task PersistCoreAsync() { await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); }
        public FoodDispatchTimingService Service(FoodDispatchTimingOptions? options = null, ISsalddelFoodOrderStore? order = null,
            bool withoutOrderStore = false)
            => new(Db, Demand, AI, Options.Create(options ?? new() { Enabled = true }), new(), Clock, NullLogger<FoodDispatchTimingService>.Instance,
                withoutOrderStore ? null : order ?? Order);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class DemandPort : I음식배달수요참여Service
    {
        public 음식배달수요참여결과? Value { get; set; }
        public int Calls { get; private set; }
        public Action? AfterCall { get; set; }
        public Task<음식배달수요참여결과?> 평가Async(운송원장 queue, CancellationToken cancellationToken = default)
        { Calls++; AfterCall?.Invoke(); return Task.FromResult(Value); }
    }
    private sealed class AIPort : IFoodDispatchTimingAI
    {
        public bool Enabled { get; set; } = true;
        public int Calls { get; private set; }
        public Action? AfterCall { get; set; }
        public FoodDispatchTimingAISelection Selection { get; set; } = new("Collect", "Applied");
        public Task<FoodDispatchTimingAISelection> SelectAsync(FoodDispatchTimingInput input, FoodDispatchTimingDecision baseline,
            CancellationToken cancellationToken = default)
        { Calls++; AfterCall?.Invoke(); return Task.FromResult(Selection); }
    }
    private sealed class EncryptionPort : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class FoodOrderPort(DateTime? readyAt = null) : ISsalddelFoodOrderStore
    {
        public string? RequestedId { get; private set; }
        public int Reads { get; private set; }
        public Action<int>? BeforeRead { get; set; }
        public 음식주문응답? Value { get; set; } = new()
        {
            상태 = 음식주문상태코드.주문확인, 배차상태 = 음식주문배차상태코드.배차대기,
            Revision = 1, 조리예상완료시각Utc = readyAt,
            최근변경시각Utc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        public 음식주문응답? GetOrder(string orderNo)
        {
            RequestedId = orderNo;
            Reads++;
            BeforeRead?.Invoke(Reads);
            if (Value is not null && string.IsNullOrEmpty(Value.주문번호)) Value.주문번호 = orderNo;
            return Value;
        }
        public 음식주문목록응답 GetOrders() => throw new NotSupportedException();
        public 음식주문응답 AddOrder(음식주문등록요청 request) => throw new NotSupportedException();
        public 음식주문응답? 음식점수락(string orderNo, 음식점주문수락요청 request) => throw new NotSupportedException();
        public 음식주문응답? 배차대기반영(string orderNo, long dispatchWaitId, DateTime dispatchRequestedAtUtc) => throw new NotSupportedException();
    }
}
