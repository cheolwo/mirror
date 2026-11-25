using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Inventory;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.기사;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class 출고예정회차조회Tests
{
    private static readonly DateTime Start = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 선택구간과이전미완료를별도로집계하고원장을수정하지않는다()
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        var carry = Plan(warehouse, Start.AddDays(-2), 7);
        carry.UpdatedAt = Start.AddHours(1);
        var current = Plan(warehouse, Start.AddHours(1), 10);
        var done = Plan(warehouse, Start.AddDays(-3), 9, 출고상태.출고완료);
        done.출고처리일시 = Start.AddDays(-2);
        var cancelled = Plan(warehouse, Start.AddDays(-1), 11, 출고상태.취소);
        db.출고예정.AddRange(carry, current, done, cancelled);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await Use(db).목록Async(Request(warehouse), default);

        Assert.True(result.IsSuccess);
        var period = Assert.IsType<OutboundPeriodResponse>(result.Value.Period);
        Assert.Equal(10, period.Current.ReceivedQuantity);
        Assert.Equal(7, period.Carryover.ReceivedQuantity);
        Assert.Equal(2, period.TotalCount);
        Assert.Equal(carry.Id, period.Items[0].OutboundPlanId);
        Assert.True(period.Items[0].IsCarryover);
        Assert.Equal(carry.CreatedAt, period.Items[0].CreatedAtUtc);
        Assert.False(period.Workforce.AttendanceConfirmed);
        Assert.Equal(1, period.Workforce.RegisteredWorkerCount);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(0, await db.SaveChangesAsync());
    }

    [Fact]
    public async Task 접수경계와한국날짜를자정넘는조회에서도구분한다()
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        var start = Start.AddHours(14);
        var end = start.AddHours(4);
        db.출고예정.AddRange(Plan(warehouse, start, 2), Plan(warehouse, end.AddTicks(-1), 3),
            Plan(warehouse, end, 13), Plan(warehouse, Start.AddHours(-9).AddTicks(-1), 5));
        await db.SaveChangesAsync();
        var request = Request(warehouse); request.PeriodStartUtc = start; request.PeriodEndUtc = end;

        var result = await Use(db).목록Async(request, default);

        Assert.True(result.IsSuccess);
        var period = result.Value.Period!;
        Assert.Equal(5, period.Current.ReceivedQuantity);
        Assert.Equal(2, period.Today.ReceivedQuantity);
        Assert.Equal(7, period.PreviousDays.Count);
        Assert.Equal("2026-10-31", period.PreviousDays[^1].DateKst);
        Assert.Equal(5, period.PreviousDays[^1].Totals.ReceivedQuantity);
    }

    [Fact]
    public async Task 집계는목록검색과50건페이지의영향없이long수량을반환한다()
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        db.출고예정.AddRange(Enumerable.Range(0, 75).Select(index => Plan(warehouse, Start.AddMinutes(index), 2_000_000_000)));
        await db.SaveChangesAsync();
        var request = Request(warehouse); request.Page = 1; request.PageSize = 50; request.Search = "없는검색";

        var result = await Use(db).목록Async(request, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        var period = result.Value.Period!;
        Assert.Equal(75, period.Current.PlanCount);
        Assert.Equal(150_000_000_000L, period.Current.ReceivedQuantity);
        Assert.Equal(75, period.TotalCount);
        Assert.Equal(25, period.Items.Count);
        Assert.False(period.HasNextPage);
    }

    [Theory]
    [InlineData("start-only")]
    [InlineData("end-only")]
    [InlineData("local")]
    [InlineData("unspecified")]
    [InlineData("zero")]
    [InlineData("reverse")]
    [InlineData("too-long")]
    [InlineData("no-warehouse")]
    public async Task 잘못된구간은400이며기존목록호출은기간없이유지된다(string mode)
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        var request = Request(warehouse);
        switch (mode)
        {
            case "start-only": request.PeriodEndUtc = null; break;
            case "end-only": request.PeriodStartUtc = null; break;
            case "local": request.PeriodStartUtc = DateTime.SpecifyKind(Start, DateTimeKind.Local); break;
            case "unspecified": request.PeriodEndUtc = DateTime.SpecifyKind(Start.AddHours(1), DateTimeKind.Unspecified); break;
            case "zero": request.PeriodEndUtc = Start; break;
            case "reverse": request.PeriodEndUtc = Start.AddTicks(-1); break;
            case "too-long": request.PeriodEndUtc = Start.AddDays(1).AddTicks(1); break;
            case "no-warehouse": request.WarehouseId = null; break;
        }
        var invalid = await Use(db).목록Async(request, default);
        Assert.True(invalid.IsFailed);
        Assert.Equal(400, invalid.Errors.Single().Metadata["StatusCode"]);
        var original = await Use(db).목록Async(new(), default);
        Assert.True(original.IsSuccess);
        Assert.Null(original.Value.Period);
    }

    [Fact]
    public async Task 다른창고는404로숨기고미인증은집계전에401로차단한다()
    {
        await using var db = Context();
        var hiddenWarehouse = await Warehouse(db, "other");
        db.출고예정.Add(Plan(hiddenWarehouse, Start, 999));
        await db.SaveChangesAsync();

        var hidden = await Use(db).목록Async(Request(hiddenWarehouse), default);
        var absent = await Use(db).목록Async(Request(long.MaxValue), default);
        var anonymous = await Use(db, null).목록Async(Request(hiddenWarehouse), default);

        Assert.Equal(404, hidden.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(404, absent.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(401, anonymous.Errors.Single().Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 일부작업완료와취소작업을구분하며담당자는표시명만반환한다()
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        var plan = Plan(warehouse, Start, 10);
        db.출고예정.Add(plan); await db.SaveChangesAsync();
        db.피킹포장작업.AddRange(Task(plan, "pick-1", 4, 피킹포장작업유형.피킹, 피킹포장작업상태.완료),
            Task(plan, "pick-2", 6, 피킹포장작업유형.피킹, 피킹포장작업상태.대기),
            Task(plan, "pack-1", 4, 피킹포장작업유형.포장, 피킹포장작업상태.완료),
            Task(plan, "cancelled", 100, 피킹포장작업유형.피킹, 피킹포장작업상태.취소));
        await db.SaveChangesAsync();

        var result = await Use(db).목록Async(Request(warehouse), default);

        var period = result.Value.Period!;
        var item = Assert.Single(period.Items);
        Assert.Equal(4, period.Current.PickedQuantity);
        Assert.Equal(4, period.Current.PackedQuantity);
        Assert.Equal(0, period.Current.HandedOffQuantity);
        Assert.False(item.NeedsReview);
        Assert.Equal("피킹 담당자", item.NextActor);
        Assert.Equal(new[] { "가상 작업자" }, item.WorkerDisplayNames);
        Assert.Equal(1, period.Workforce.OpenTaskCount);
        Assert.Equal(1, period.Workforce.AssignedWorkerCount);
        Assert.False(period.Workforce.AttendanceConfirmed);
    }

    [Theory]
    [InlineData("overcount")]
    [InlineData("wrong-warehouse")]
    [InlineData("negative")]
    [InlineData("missing-time")]
    [InlineData("future-time")]
    [InlineData("before-task")]
    [InlineData("before-plan")]
    public async Task 잘못된연결완료수량을확정실적으로합산하지않는다(string mode)
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        var plan = Plan(warehouse, Start, 10); db.출고예정.Add(plan); await db.SaveChangesAsync();
        var task = Task(plan, "invalid", mode == "overcount" ? 11 : 4, 피킹포장작업유형.피킹, 피킹포장작업상태.완료);
        if (mode == "wrong-warehouse") task.창고Id = warehouse + 10;
        if (mode == "negative") task.수량 = -1;
        if (mode == "missing-time") task.완료일시Utc = null;
        if (mode == "future-time") task.완료일시Utc = DateTime.UtcNow.AddDays(1);
        if (mode == "before-task") task.CreatedAt = Start.AddHours(1);
        if (mode == "before-plan") plan.CreatedAt = Start.AddHours(1);
        db.피킹포장작업.Add(task); await db.SaveChangesAsync();

        var result = await Use(db).목록Async(Request(warehouse), default);

        Assert.Equal(0, result.Value.Period!.Current.PickedQuantity);
        var item = Assert.Single(result.Value.Period.Items);
        Assert.True(item.NeedsReview);
        Assert.NotEmpty(item.ReviewReason);
    }

    [Fact]
    public async Task 작업없는출고와잘못된접수시각은확인필요로표시한다()
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        db.출고예정.AddRange(Plan(warehouse, Start, 3), Plan(warehouse, DateTime.MinValue, 4));
        await db.SaveChangesAsync();

        var result = await Use(db).목록Async(Request(warehouse), default);

        var period = result.Value.Period!;
        Assert.Equal(2, period.Items.Count);
        Assert.All(period.Items, item => Assert.True(item.NeedsReview));
        Assert.Contains(period.Items, item => item.WaitingReason == "작업 연결 미확인");
        Assert.Contains(period.Items, item => item.ReviewReason.Contains("접수 시각", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 미래접수와확인할수없는인계시각을완료로표시하지않는다()
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        var future = DateTime.UtcNow.AddDays(1);
        var plan = Plan(warehouse, future, 6, 출고상태.출고완료);
        plan.출고처리일시 = future.AddMinutes(1);
        db.출고예정.Add(plan); await db.SaveChangesAsync();
        var request = Request(warehouse); request.PeriodStartUtc = future; request.PeriodEndUtc = future.AddHours(1);

        var result = await Use(db).목록Async(request, default);

        var item = Assert.Single(result.Value.Period!.Items);
        Assert.True(item.NeedsReview);
        Assert.Equal(0, item.HandedOffQuantity);
        Assert.NotEqual("출고 인계 완료", item.WaitingReason);
    }

    [Fact]
    public async Task 실제인계완료량은피킹포장과독립적으로집계하고취소접수량은보존한다()
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        var completed = Plan(warehouse, Start, 6, 출고상태.출고완료);
        completed.출고처리일시 = Start.AddHours(1);
        var cancelled = Plan(warehouse, Start, 4, 출고상태.취소);
        db.출고예정.AddRange(completed, cancelled); await db.SaveChangesAsync();
        db.피킹포장작업.AddRange(Task(completed, "done-pick", 6, 피킹포장작업유형.피킹, 피킹포장작업상태.완료),
            Task(completed, "done-pack", 6, 피킹포장작업유형.포장, 피킹포장작업상태.완료),
            Task(cancelled, "cancel-pick", 4, 피킹포장작업유형.피킹, 피킹포장작업상태.완료));
        await db.SaveChangesAsync();

        var result = await Use(db).목록Async(Request(warehouse), default);

        var period = result.Value.Period!;
        Assert.Equal(10, period.Current.ReceivedQuantity);
        Assert.Equal(6, period.Current.PickedQuantity);
        Assert.Equal(6, period.Current.PackedQuantity);
        Assert.Equal(6, period.Current.HandedOffQuantity);
        Assert.Equal("출고 인계 완료", period.Items.Single(item => item.OutboundPlanId == completed.Id).WaitingReason);
        Assert.True(period.Items.Single(item => item.OutboundPlanId == completed.Id).CanOpenReview);
        Assert.False(period.Items.Single(item => item.OutboundPlanId == cancelled.Id).CanOpenReview);
        Assert.Equal(0, period.Workforce.OpenTaskCount);
    }

    [Fact]
    public async Task 미확인담당자의내부ID와기존응답의null기간을노출하지않는다()
    {
        await using var db = Context();
        var warehouse = await Warehouse(db);
        var plan = Plan(warehouse, Start, 6); db.출고예정.Add(plan); await db.SaveChangesAsync();
        var task = Task(plan, "private-display", 6, 피킹포장작업유형.피킹, 피킹포장작업상태.대기);
        task.작업자표시명 = " " + task.작업자UserId + " ";
        db.피킹포장작업.Add(task); await db.SaveChangesAsync();

        var result = await Use(db).목록Async(Request(warehouse), default);

        Assert.Empty(Assert.Single(result.Value.Period!.Items).WorkerDisplayNames);
        Assert.DoesNotContain("Period", System.Text.Json.JsonSerializer.Serialize(new 출고예정검토목록페이지응답()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 관계형DB가페이지전체집계와한국날짜분류를실행한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        await db.Database.EnsureCreatedAsync();
        var warehouse = await Warehouse(db);
        var plan = Plan(warehouse, Start, 10);
        db.출고예정.AddRange(plan, Plan(warehouse, Start.AddDays(-1), 4)); await db.SaveChangesAsync();
        db.피킹포장작업.Add(Task(plan, "relational", 5, 피킹포장작업유형.피킹, 피킹포장작업상태.완료));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        var result = await Use(db).목록Async(Request(warehouse), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.Period!.Current.ReceivedQuantity);
        Assert.Equal(5, result.Value.Period.Current.PickedQuantity);
        Assert.Equal(4, result.Value.Period.PreviousDays[^1].Totals.ReceivedQuantity);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
    private static 출고예정검토UseCase Use(SsalddelContext db, string? userId = "worker")
        => new(db, new User(userId));
    private static 출고예정검토목록조회요청 Request(long warehouse) => new()
        { WarehouseId = warehouse, PeriodStartUtc = Start, PeriodEndUtc = Start.AddHours(8), PageSize = 12 };
    private static async Task<long> Warehouse(SsalddelContext db, string user = "worker")
    {
        var warehouse = new 창고 { 창고명 = "가상 창고", 주소 = "가상 주소", 소유자UserId = user, IsActive = true };
        db.창고.Add(warehouse); await db.SaveChangesAsync();
        db.창고사용자.Add(new 창고사용자 { 창고Id = warehouse.Id, UserId = user, 역할명 = "출고" });
        await db.SaveChangesAsync(); return warehouse.Id;
    }
    private static 출고예정 Plan(long warehouse, DateTime created, int quantity, string status = 출고상태.준비중)
        => new() { 출고창고Id = warehouse, 상품명 = "가상 상품", SKU = "SAMPLE", 주문참조번호 = "SAMPLE-ORDER", 수량 = quantity,
            상태 = status, CreatedAt = created, UpdatedAt = created };
    private static 피킹포장작업 Task(출고예정 plan, string key, int quantity, string type, string status)
        => new() { 작업Key = key, 출고예정Id = plan.Id, 창고Id = plan.출고창고Id, 작업유형 = type, 상태 = status,
            수량 = quantity, 작업자UserId = "private-user-id", 작업자표시명 = "가상 작업자", SKU = plan.SKU, 상품명 = plan.상품명,
            CreatedAt = Start, UpdatedAt = Start.AddMinutes(1), 완료일시Utc = status == 피킹포장작업상태.완료 ? Start.AddMinutes(1) : null };
    private sealed class User(string? id) : ICurrentUserAccessor
    {
        public string? UserId => id;
        public string? Role => 역할명.창고관리자;
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
