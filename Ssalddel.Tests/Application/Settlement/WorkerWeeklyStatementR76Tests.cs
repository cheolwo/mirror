using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.도메인.정산;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Tests.Application.Settlement;

public sealed partial class NeighborhoodSettlementUseCaseTests
{
    [Theory]
    [InlineData("buyer", true)]
    [InlineData("seller", true)]
    [InlineData("picker", false)]
    [InlineData("owner", false)]
    public async Task R76_마트수령해결진입은현재원천의실제주문자와판매자에게만표시한다(string actor, bool allowed)
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(sourceKind: C.MartOrder);
        var agreement = await f.Activate();
        var summary = await f.Use().GetAsync(agreement.AgreementId, actor, default);
        Assert.Equal(allowed, summary.CanResolveMartReceipt);
    }

    [Fact]
    public async Task R76_본인완료주간만조회하고_화물보수null과_모의지급을_실입금으로숨기지않는다()
    {
        await using var f = await Fixture.Create();
        var memory = f.Db;
        // 전문 원장 조회의 합성 행만 주입한다. 제품의 관계형 저장 관문은 그대로 검사한다.
        await memory.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF");
        var start = new DateTime(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc); // 10/7 Wed 00 KST
        memory.AddRange(
            Food("mine", "worker", start, 4000), Food("before", "worker", start.AddTicks(-1), 9999),
            Food("next", "worker", start.AddDays(7), 9999), Food("other", "other", start, 9999));
        memory.AddRange(
            new 화주운송의뢰 { Id = 1, 의뢰Id = "unknown", 화주Id = "shipper" },
            new 운송원장 { Id = 1, 의뢰Id = "unknown", 운송번호 = "unknown", 확정기사Id = "worker", 기사_운송자 = "old-driver",
                도착 = start.AddDays(1), 상태 = 상태값.배차상태.인수완료 },
            new 운송원장 { Id = 2, 의뢰Id = "another", 운송번호 = "another", 확정기사Id = "other", 기사_운송자 = "worker",
                도착 = start.AddDays(1), 상태 = 상태값.배차상태.인수완료 });
        await memory.SaveChangesAsync(); var count = memory.ChangeTracker.Entries().Count();
        var read = await f.Use(memory).WeeklyStatementAsync("worker", new DateOnly(2026, 10, 9), default);
        Assert.Equal(new DateOnly(2026, 10, 7), read.CompletionWeekStartKst);
        Assert.Equal(start, read.PeriodStartUtc); Assert.Equal(start.AddDays(7), read.PeriodEndUtc);
        Assert.Equal(2, read.Lines.Count); Assert.Equal(1, read.UnknownAmountCount);
        Assert.Null(read.AccruedAmountKrw); Assert.Equal(4000m, read.KnownAccruedAmountKrw);
        Assert.Null(Assert.Single(read.Lines, x => x.SourceKind == C.CargoRequest).AccruedAmountKrw);
        Assert.Equal(음식주문기사지급상태Code.모의성공, Assert.Single(read.Lines, x => x.SourceKind == C.FoodOrder).PaymentStatusCode);
        Assert.False(read.IsActualBankVerified); Assert.Null(read.ActualBankVerifiedAmountKrw);
        Assert.Equal(0m, read.RecipientConfirmedAmountKrw); Assert.All(read.Lines, x => Assert.False(x.IsActualBankVerified));
        Assert.Equal(count, memory.ChangeTracker.Entries().Count()); Assert.Empty(await memory.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task R76_피킹은늦은보수기록일이아닌_완료주간에한번만포함한다()
    {
        await using var f = await Fixture.Create();
        var completed = new DateTime(2026, 10, 6, 14, 59, 59, DateTimeKind.Utc);
        f.Resolver.Snapshot = f.Source(workTime: completed);
        var r = await f.Activate(); r = await f.Accrue(r, 2);
        var entry = (await f.Use().GetAsync(r.AgreementId, "picker", default)).Entries.Single();
        r = await f.Confirm(r, entry.EntryId, "owner"); r = await f.Confirm(r, entry.EntryId, "picker");
        var previous = await f.Use().WeeklyStatementAsync("picker", new DateOnly(2026, 10, 6), default);
        var current = await f.Use().WeeklyStatementAsync("picker", new DateOnly(2026, 10, 7), default);
        Assert.Equal(200m, previous.AccruedAmountKrw); Assert.Equal(completed, Assert.Single(previous.Lines).CompletedAtUtc);
        Assert.Equal(200m, previous.KnownPaymentConditionReadyAmountKrw); Assert.Empty(current.Lines);
        Assert.Empty((await f.Use().WeeklyStatementAsync("buyer", new DateOnly(2026, 10, 6), default)).Lines);
        Assert.Single(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task R76_기존배송참고와입고원가배부는_작업자보수에중복합산하지않는다()
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(delivery: true);
        var r = await f.Activate(f.DeliveryTerms()); r = await f.Accrue(r, 1, lineKey: "delivery");
        var before = await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync();
        var read = await f.Use().WeeklyStatementAsync("driver", new DateOnly(2026, 10, 8), default);
        Assert.Empty(read.Lines); Assert.Equal(1, read.ExcludedDeliveryReferenceCount);
        Assert.Equal(0m, read.AccruedAmountKrw); Assert.Equal(before, await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(false, "neighborhood-settlement-r38", SsalddelExecutionMode.Simulation)]
    [InlineData(true, "wrong", SsalddelExecutionMode.Simulation)]
    [InlineData(true, "neighborhood-settlement-r38", SsalddelExecutionMode.Operational)]
    public async Task R76_주간명세도_기존Simulation경계를유지한다(bool enabled, string profile, SsalddelExecutionMode mode)
    {
        await using var f = await Fixture.Create();
        await Error("NeighborhoodSettlementDisabled", () => f.Use(enabled, profile, mode).WeeklyStatementAsync("picker", null, default));
    }

    [Fact]
    public async Task R76_지급자에게만미신고상한을보이고_수령미확인이어도예약한신고를중복지급하지않는다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate(); r = await f.Accrue(r, 2);
        var entry = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single();
        r = await f.Confirm(r, entry.EntryId, "owner"); r = await f.Confirm(r, entry.EntryId, "picker");
        var payer = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single();
        Assert.Equal(200m, payer.ReportablePaymentAmountKrw); Assert.Contains("report-payment", payer.AllowedActions);
        var recipient = (await f.Use().GetAsync(r.AgreementId, "picker", default)).Entries.Single();
        Assert.Null(recipient.ReportablePaymentAmountKrw); Assert.DoesNotContain("report-payment", recipient.AllowedActions);
        var report = f.Report(r, entry.EntryId, 120); r = await f.Use().ReportPaymentAsync(r.AgreementId, "owner", report, default);
        var replay = await f.Use().ReportPaymentAsync(r.AgreementId, "owner", report, default); Assert.True(replay.Replayed);
        payer = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single();
        Assert.Equal(80m, payer.ReportablePaymentAmountKrw);
        await Error("NeighborhoodSettlementPaymentExceedsUnpaidAmount", () => f.Use().ReportPaymentAsync(r.AgreementId, "owner", f.Report(r, entry.EntryId, 81), default));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    private static 음식주문기사정산 Food(string id, string actor, DateTime completed, decimal? amount) => new()
    {
        정산StableId = id, 주문번호 = "order-" + id, 기사Id = actor, 전달완료시각Utc = completed, 세전대금 = amount,
        배달시도Id = id switch { "mine" => 1, "before" => 2, "next" => 3, "other" => 4, _ => throw new ArgumentOutOfRangeException(nameof(id)) },
        정산상태Code = 음식주문기사정산상태Code.모의검증준비, 지급상태Code = 음식주문기사지급상태Code.모의성공
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task R76_판매대행료완료일은_판매근거를쓰며_시각없으면기록일로추정하지않는다(bool known)
    {
        await using var f = await Fixture.Create();
        f.Resolver.Snapshot = f.Source(saleWork: true, workTime: known ? f.Clock.Now.AddMinutes(-1) : null);
        var terms = f.Terms(); terms.Lines = [new() { LineKey = "agency", Kind = C.SalesAgency, Billing = C.PerUnit,
            UnitPriceKrw = 20, Quantity = 5, PayerUserId = "owner", PayeeUserId = "seller", BeneficiaryRole = "Seller" }];
        var r = await f.Activate(terms); await f.Accrue(r, 2, lineKey: "agency");
        var read = await f.Use().WeeklyStatementAsync("seller", new DateOnly(2026, 10, 8), default);
        if (known) { Assert.Equal(40m, read.AccruedAmountKrw); Assert.Single(read.Lines); Assert.Equal(0, read.UnassignedCompletionTimeCount); }
        else { Assert.Empty(read.Lines); Assert.Equal(1, read.UnassignedCompletionTimeCount); }
    }

    [Fact]
    public async Task R76_입고단위보관보수는_실제입고완료와공간제공자를연결하며_원가배부를더하지않는다()
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(sourceKind: C.DistributedLot);
        var terms = f.Terms(); terms.Lines = [new() { LineKey = "storage", Kind = C.Storage, Billing = C.PerUnit,
            UnitPriceKrw = 30, Quantity = 5, PayerUserId = "owner", PayeeUserId = "space", BeneficiaryRole = "SpaceOwner" }];
        var r = await f.Activate(terms); await f.Accrue(r, 2, lineKey: "storage");
        var read = await f.Use().WeeklyStatementAsync("space", new DateOnly(2026, 10, 8), default);
        Assert.Equal(60m, read.AccruedAmountKrw); Assert.Equal(C.Storage, Assert.Single(read.Lines).ActivityKind);
        Assert.Equal(0, read.UnassignedCompletionTimeCount);
        f.Resolver.Failure = new InvalidOperationException("specialist unavailable");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Use().WeeklyStatementAsync("space", null, default));
    }
}
