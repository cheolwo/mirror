using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Settlement;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Application.Admin.Settlement;

public sealed partial class 배송료정산배치R73Tests
{
    private sealed class Clock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task 자동명세는기존기능과자동준비옵션이모두켜져야저장한다(bool enabled, bool automatic)
    {
        await using var f = await Fixture.Create(); await f.Seed(1);
        var service = (I배송료주간자동준비Service)f.Use(enabled: enabled, automatic: automatic, clock: new Clock(Start.AddDays(8)));
        Assert.False((await service.준비Async()).Enabled);
        Assert.Empty(await f.Db.기사운송대금정산배치.ToArrayAsync());
        Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 자동명세는한국수요일경계에서닫힌완료주만묶고늦은승인일로옮기지않는다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(2);
        var second = await f.Db.기사운송대금지급요청.SingleAsync(x => x.Id == ids[1]);
        (await f.Db.운송원장.SingleAsync(x => x.Id == second.운송Id)).도착 = Start.AddDays(7);
        (await f.Db.기사운송대금지급요청.SingleAsync(x => x.Id == ids[0])).승인일시Utc = Start.AddDays(8);
        await f.Db.SaveChangesAsync();
        var service = (I배송료주간자동준비Service)f.Use(automatic: true, clock: new Clock(Start.AddDays(9)));
        var result = await service.준비Async(); Assert.Equal(1, result.PreparedBatches);
        var batch = await f.Db.기사운송대금정산배치.Include(x => x.행목록).SingleAsync();
        Assert.Equal(Start, batch.기간시작Utc); Assert.Equal(ids[0], Assert.Single(batch.행목록).지급요청Id);
        Assert.Equal("system:weekly-statement-preparation", batch.생성관리자Id);
        Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 자동명세는신고건만보류하고수납과계좌검사를공통경로로실행한다()
    {
        await using var f = await Fixture.Create(); await f.Seed(2);
        f.Db.비정상운송사건.Add(new 비정상운송사건
        {
            사건StableId = "automatic-incident", 운송의뢰Id = "request-1", 사건유형Code = 비정상운송사건유형Codes.화물훼손,
            상태Code = 비정상운송사건상태Codes.운영검토대기, 정산보류적용여부 = true
        });
        await f.Db.SaveChangesAsync();
        var service = (I배송료주간자동준비Service)f.Use(automatic: true, clock: new Clock(Start.AddDays(8)));
        var result = await service.준비Async(); Assert.Equal(1, result.PreparedBatches); Assert.Equal(1, result.HeldLines);
        var batch = await f.Db.기사운송대금정산배치.SingleAsync();
        Assert.Equal(42000m, batch.준비합계금액); Assert.Equal(1, batch.보류건수);
        Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 자동명세는재시작과처리상한이후다음채무를이어읽고중복편입하지않는다()
    {
        await using var f = await Fixture.Create(); await f.Seed(3);
        for (var i = 0; i < 4; i++)
        {
            await using var db = f.OpenDb();
            var service = (I배송료주간자동준비Service)f.Use(db, automatic: true, take: 1, clock: new Clock(Start.AddDays(8)));
            Assert.Equal(i < 3 ? 1 : 0, (await service.준비Async()).PreparedBatches);
        }
        Assert.Equal(3, await f.Db.기사운송대금정산배치행.CountAsync());
        Assert.Equal(3, await f.Db.기사운송대금지급요청.CountAsync());
        Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 내부자동명세포트가있어도일반기사의관리자API준비권한은열리지않는다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(1);
        var useCase = f.Use(role: "기사", automatic: true, clock: new Clock(Start.AddDays(8)));
        AssertStatus(await useCase.준비Async(Request(ids)), 403);
        Assert.Empty(await f.Db.기사운송대금정산배치.ToArrayAsync());
    }

    [Theory]
    [InlineData("incident")]
    [InlineData("refund")]
    [InlineData("account")]
    public async Task 이미준비된자동명세도_새신고환불계좌변경을보류하고_해소뒤현재조건을다시검사한다(string mutation)
    {
        await using var f = await Fixture.Create(); await f.Seed(1);
        var service = (I배송료주간자동준비Service)f.Use(automatic: true, clock: new Clock(Start.AddDays(8)));
        await service.준비Async();
        var source = await f.Db.화주운송의뢰.SingleAsync();
        var account = await f.Db.Set<기사정산계좌>().SingleAsync();
        비정상운송사건? incident = null;
        if (mutation == "incident")
        {
            incident = new() { 사건StableId = "after-prepared", 운송의뢰Id = "request-0", 사건유형Code = 비정상운송사건유형Codes.화물훼손,
                상태Code = 비정상운송사건상태Codes.운영검토대기, 정산보류적용여부 = true };
            f.Db.비정상운송사건.Add(incident);
        }
        if (mutation == "refund") source.결제상태 = 상태값.결제상태.환불됨;
        if (mutation == "account") account.확인상태 = 기사정산계좌확인상태.미확인;
        await f.Db.SaveChangesAsync();

        var held = await service.준비Async();
        Assert.Equal(1, held.RevalidatedBatches); Assert.Equal(1, held.HeldLines); Assert.Equal(0, held.PreparedBatches);
        var batch = await f.Db.기사운송대금정산배치.AsNoTracking().SingleAsync();
        Assert.Equal("Held", batch.상태코드); Assert.Equal(0m, batch.준비합계금액);
        if (incident is not null) { incident.정산보류적용여부 = false; incident.상태Code = "Closed"; }
        source.결제상태 = 상태값.결제상태.결제완료; account.확인상태 = 기사정산계좌확인상태.확인완료;
        await f.Db.SaveChangesAsync();
        var ready = await service.준비Async();
        Assert.Equal(1, ready.RevalidatedBatches); Assert.Equal(0, ready.HeldLines);
        Assert.Equal("Prepared", (await f.Db.기사운송대금정산배치.AsNoTracking().SingleAsync()).상태코드);
        Assert.Single(await f.Db.기사운송대금지급요청.ToArrayAsync());
        Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42000.5)]
    public async Task 상한앞의잘못된채무를검토로남기고_재시작뒤다음정상채무를진행한다(double invalid)
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(2);
        (await f.Db.기사운송대금지급요청.SingleAsync(x => x.Id == ids[0])).지급예정금액 = (decimal)invalid;
        await f.Db.SaveChangesAsync();
        for (var i = 0; i < 2; i++)
        {
            await using var context = f.OpenDb();
            var result = await ((I배송료주간자동준비Service)f.Use(context, automatic: true, take: 1, clock: new Clock(Start.AddDays(8)))).준비Async();
            Assert.Equal(i == 0 ? 1 : 0, result.ReviewRequiredLines);
            Assert.Equal(i == 0 ? 0 : 1, result.PreparedBatches);
        }
        var rejected = await f.Db.기사운송대금지급요청.AsNoTracking().SingleAsync(x => x.Id == ids[0]);
        Assert.Equal((decimal)invalid, rejected.지급예정금액);
        Assert.Equal(기사지급요청상태코드.승인됨, rejected.상태코드);
        Assert.Equal("ApprovedDebtAmountInvalid", rejected.자동명세검토코드);
        Assert.NotNull(rejected.자동명세검토시각Utc);
        Assert.Equal(ids[1], (await f.Db.기사운송대금정산배치행.AsNoTracking().SingleAsync()).지급요청Id);
        Assert.Equal(2, await f.Db.기사운송대금지급요청.CountAsync()); Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 늦은승인은_기존완료주의별도명세로추가하며_이전명세나채무를중복생성하지않는다()
    {
        await using var f = await Fixture.Create(); var ids = await f.Seed(2);
        var late = await f.Db.기사운송대금지급요청.SingleAsync(x => x.Id == ids[1]); late.상태코드 = "AwaitingApproval";
        await f.Db.SaveChangesAsync();
        var service = (I배송료주간자동준비Service)f.Use(automatic: true, clock: new Clock(Start.AddDays(8)));
        Assert.Equal(1, (await service.준비Async()).PreparedBatches);
        late.상태코드 = 기사지급요청상태코드.승인됨; late.승인일시Utc = Start.AddDays(8);
        await f.Db.SaveChangesAsync();
        Assert.Equal(1, (await service.준비Async()).PreparedBatches);
        Assert.Equal(0, (await service.준비Async()).PreparedBatches);
        var batches = await f.Db.기사운송대금정산배치.AsNoTracking().ToArrayAsync(); Assert.Equal(2, batches.Length);
        Assert.All(batches, batch => Assert.Equal(Start, batch.기간시작Utc));
        Assert.Equal(2, await f.Db.기사운송대금정산배치행.CountAsync()); Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 같은시각과상한에서도_기존자동명세를순환재검증한다()
    {
        await using var f = await Fixture.Create(); await f.Seed(3);
        for (var i = 0; i < 8; i++)
        {
            await using var context = f.OpenDb();
            await ((I배송료주간자동준비Service)f.Use(context, automatic: true, take: 1, clock: new Clock(Start.AddDays(8)))).준비Async();
        }
        var batches = await f.Db.기사운송대금정산배치.AsNoTracking().ToArrayAsync();
        Assert.Equal(3, batches.Length); Assert.All(batches, batch => Assert.True(batch.Revision > 1));
        Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
    }

    [Fact]
    public async Task 자동준비동시실행도_한채무한행과기존관리자재검증판본을유지한다()
    {
        await using var f = await Fixture.Create(); await f.Seed(1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Attempt()
        {
            await using var context = f.OpenDb(); await start.Task;
            await ((I배송료주간자동준비Service)f.Use(context, automatic: true, clock: new Clock(Start.AddDays(8)))).준비Async();
        }
        var a = Task.Run(Attempt); var b = Task.Run(Attempt); start.SetResult(); await Task.WhenAll(a, b);
        var batch = await f.Db.기사운송대금정산배치.AsNoTracking().SingleAsync();
        Assert.Single(await f.Db.기사운송대금정산배치행.ToArrayAsync()); Assert.Empty(await f.Db.기사지급Outbox.ToArrayAsync());
        await using var adminDb = f.OpenDb();
        var next = await f.Use(adminDb).재검증Async(batch.Id, new() { ExpectedRevision = batch.Revision }); Assert.True(next.IsSuccess);
        AssertStatus(await f.Use(adminDb).재검증Async(batch.Id, new() { ExpectedRevision = batch.Revision }), 409);
    }
}
