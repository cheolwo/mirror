using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Settlement;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Tests.Application.Warehouse;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Services.Audit;
using 살뜰.Services.Options;
using 살뜰.도메인.마트;
using 살뜰.도메인.정산;
using 살뜰.도메인.창고;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class MartActivitySettlementTests
{
    [Fact]
    public async Task 실제시작완료와보호근거를datetime6정밀도로함께동결하고재조회한다()
    {
        var raw = new DateTime(2026, 10, 13, 1, 2, 3, DateTimeKind.Unspecified).AddTicks(17);
        var normalized = WarehouseActivityCompletionEvidence.NormalizeUtc(raw);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind); Assert.Equal(0, normalized.Ticks % 10);
        Assert.Equal(raw.Ticks - 7, normalized.Ticks);
        await using var f = await Fixture.Create(); await f.Complete();
        var tasks = await f.Db.피킹포장작업.Where(task => task.Id >= 901 && task.Id <= 904).ToArrayAsync();
        var originalProofs = tasks.ToDictionary(task => task.Id, task => task.완료근거Json);
        foreach (var task in tasks)
        {
            var proof = WarehouseActivityCompletionEvidence.Read(task.완료근거Json)!;
            Assert.Equal(0, task.시작일시Utc!.Value.Ticks % 10); Assert.Equal(0, task.완료일시Utc!.Value.Ticks % 10);
            Assert.Equal(task.완료일시Utc.Value, proof.CompletedAtUtc); Assert.Equal(0, proof.CompletedAtUtc.Ticks % 10);
            Assert.Equal(DateTimeKind.Utc, proof.CompletedAtUtc.Kind);
            if (task.작업유형 == 피킹포장작업유형.피킹) Assert.Equal(task.시작일시Utc, proof.StartedAtUtc);
            else Assert.Null(proof.StartedAtUtc);
            // MySQL datetime(6)의 소수 여섯 자리 저장·Kind 미보존을 격리 DB에서 모사합니다.
            task.시작일시Utc = new DateTime(task.시작일시Utc.Value.Ticks / 10 * 10, DateTimeKind.Unspecified);
            task.완료일시Utc = new DateTime(task.완료일시Utc.Value.Ticks / 10 * 10, DateTimeKind.Unspecified);
        }
        foreach (var history in await f.Db.재고이력.Where(history => history.이력유형 == "포장").ToArrayAsync())
        {
            Assert.Equal(0, history.처리일시.Ticks % 10);
            history.처리일시 = new DateTime(history.처리일시.Ticks / 10 * 10, DateTimeKind.Unspecified);
        }
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        Assert.Equal(2, (await f.Resolver.ResolveAsync(C.MartOrder, "701", default)).CompletedWork.Count);
        Assert.Equal(2, (await f.Resolver.ResolveAsync(C.MartOrder, "702", default)).CompletedWork.Count);
        foreach (var task in await f.Db.피킹포장작업.Where(task => task.Id >= 901 && task.Id <= 904).ToArrayAsync())
            Assert.Equal(originalProofs[task.Id], task.완료근거Json);
    }

    [Fact]
    public async Task 과거백나노초근거가SQL절삭시각과다르면자동보정하여승격하지않는다()
    {
        await using var f = await Fixture.Create(); await f.Complete();
        var task = await f.Db.피킹포장작업.SingleAsync(task => task.Id == 901);
        var proof = WarehouseActivityCompletionEvidence.Read(task.완료근거Json)!;
        task.완료근거Json = JsonSerializer.Serialize(proof with { CompletedAtUtc = proof.CompletedAtUtc.AddTicks(7) });
        var original = task.완료근거Json; await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var source = await f.Resolver.ResolveAsync(C.MartOrder, "701", default);
        Assert.DoesNotContain(source.CompletedWork, work => work.OriginId == "901");
        Assert.Equal("MartWorkEvidenceRequired", source.Workflow!.WaitingReasonCode);
        Assert.Equal(original, (await f.Db.피킹포장작업.AsNoTracking().SingleAsync(task => task.Id == 901)).완료근거Json);
    }

    [Fact]
    public async Task 한재고두판매주문을실제피킹포장한처리량만합의단가로누적하고완료와지급을합치지않는다()
    {
        await using var f = await Fixture.Create();
        var before = await f.Resolver.ResolveAsync(C.MartOrder, "701", default);
        var first = await f.Activate("701"); var second = await f.Activate("702");
        await f.Complete();
        var after = await f.Resolver.ResolveAsync(C.MartOrder, "701", default);
        Assert.Equal(before.SourceDigest, after.SourceDigest);
        Assert.Equal(new[] { C.Picking, C.Packing }, after.CompletedWork.Select(work => work.Kind));
        Assert.All(after.CompletedWork, work => Assert.Equal(2, work.Quantity));
        Assert.Equal("picker", after.CompletedWork[0].ActorUserId);
        Assert.Equal("packer", after.CompletedWork[1].ActorUserId);
        Assert.Contains(after.CompletedWork[1].EvidenceReferences, reference => reference.StartsWith("inventory-history:", StringComparison.Ordinal));
        Assert.DoesNotContain(after.CompletionEvidenceReferences, reference => reference.StartsWith("photo:", StringComparison.Ordinal));
        Assert.Null(after.HistoricalSaleTotalKrw); Assert.Null(after.RewardOutcome);
        Assert.False(after.Workflow!.PaymentReleaseReady);
        first = await f.Accrue(first, "pick", "901", 2);
        first = await f.Accrue(first, "pack", "903", 2);
        second = await f.Accrue(second, "pick", "902", 3);
        second = await f.Accrue(second, "pack", "904", 3);
        var entries = await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync();
        Assert.Equal(4, entries.Length);
        var read = await f.Use.GetAsync(first.AgreementId, "goods-owner-a", default);
        Assert.Equal(300m, read.Totals.KnownAccruedAmountKrw);
        Assert.All(read.Entries, entry => Assert.Equal(C.Accrued, entry.StatusCode));
        Assert.Empty(read.Payments); Assert.False(read.Totals.IsActualBankVerified);
        await Error("NeighborhoodSettlementCompletedQuantityExceeded", () => f.Accrue(first, "pick", "901", 1));
        Assert.Equal(4, await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
        var packingTask = await f.Db.피킹포장작업.SingleAsync(task => task.Id == 903);
        var original = packingTask.완료근거Json;
        var packingProof = WarehouseActivityCompletionEvidence.Read(original)!;
        Assert.Equal(packingTask.완료일시Utc, packingTask.시작일시Utc);
        Assert.Null(packingProof.StartedAtUtc);
        Assert.Equal(after.CompletedWork[0].AgreementDeadlineAtUtc, packingProof.AgreementDeadlineAtUtc);
        await f.Pack();
        Assert.Equal(original, (await f.Db.피킹포장작업.SingleAsync(task => task.Id == 903)).완료근거Json);
        Assert.Single(await f.Db.재고이력.Where(history => history.이력유형 == "포장").ToArrayAsync());
    }

    [Fact]
    public async Task 완료후재투영은실제작업자수량과동결근거를덮어쓰지않는다()
    {
        await using var f = await Fixture.Create(); await f.Complete();
        var before = await f.Resolver.ResolveAsync(C.MartOrder, "701", default);
        var original = (await f.Db.피킹포장작업.SingleAsync(task => task.Id == 901)).완료근거Json;
        await new 피킹포장작업투영Service(f.Db).투영Async("CHANGED-ORDER", new()
        {
            피킹작업목록 = [new() { TaskKey = "PICK-701", WorkerUserId = "different-worker", Quantity = 88 }],
            포장작업목록 = [new() { TaskKey = "PACK-701", PackerUserId = "different-packer", Quantity = 88 }]
        }, cancellationToken: default);
        var after = await f.Resolver.ResolveAsync(C.MartOrder, "701", default);
        Assert.Equal(before.SourceDigest, after.SourceDigest); Assert.Equal(2, after.CompletedWork.Count);
        Assert.Equal(original, (await f.Db.피킹포장작업.SingleAsync(task => task.Id == 901)).완료근거Json);
    }

    [Fact]
    public async Task 허용된대리완료의실제처리자를배정자로바꾸지않고보수귀속보류를알려준다()
    {
        await using var f = await Fixture.Create(); await f.Complete("packer");
        var proof = WarehouseActivityCompletionEvidence.Read((await f.Db.피킹포장작업.SingleAsync(task => task.Id == 901)).완료근거Json)!;
        Assert.Equal("packer", proof.ActorUserId);
        var source = await f.Resolver.ResolveAsync(C.MartOrder, "701", default);
        Assert.DoesNotContain(source.CompletedWork, work => work.Kind == C.Picking);
        Assert.Equal("MartActualActorAssignmentMismatch", source.Workflow!.WaitingReasonCode);
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 다른요청의작업상태변경은500대신409로재조회를안내한다()
    {
        await using var f = await Fixture.Create();
        await using var independent = f.Independent();
        var use = new 피킹작업UseCase(independent, new Current("picker"), new Log(), new Publisher());
        Assert.True((await use.시작Async("PICK-701", Fixture.Context("picker"), default)).IsSuccess);
        var stale = new 피킹작업UseCase(f.Db, new Current("picker"), new Log(), new Publisher());
        var result = await stale.시작Async("PICK-701", Fixture.Context("picker"), default);
        Assert.True(result.IsFailed); Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(피킹포장작업상태.진행중, (await f.Db.피킹포장작업.SingleAsync(task => task.Id == 901)).상태);
    }

    [Fact]
    public async Task 단가미정은영원으로채우지않고외부인과실제작업자가아닌수취인의청구를막는다()
    {
        await using var f = await Fixture.Create();
        var terms = f.Terms("701"); terms.Lines[0].UnitPriceKrw = null;
        var receipt = await f.Activate("701", terms); await f.Complete();
        await Error("NeighborhoodSettlementAmountUnresolved", () => f.Accrue(receipt, "pick", "901", 2));
        await Error("NeighborhoodSettlementNotFound", () => f.Use.GetAsync(receipt.AgreementId, "outsider", default));
        var task = await f.Db.피킹포장작업.SingleAsync(task => task.Id == 903);
        var proof = WarehouseActivityCompletionEvidence.Read(task.완료근거Json)!;
        task.완료근거Json = JsonSerializer.Serialize(proof with { ActorUserId = "different-packer" });
        await f.Db.SaveChangesAsync();
        var source = await f.Resolver.ResolveAsync(C.MartOrder, "701", default);
        Assert.DoesNotContain(source.CompletedWork, work => work.Kind == C.Packing);
        Assert.Equal("MartActualActorAssignmentMismatch", source.Workflow!.WaitingReasonCode);
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 이미완료한작업에새고가판본을합의하거나철회후재동의해도소급청구하지못한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate("701"); await f.Complete();
        var change = f.Terms("701"); change.ExpectedRevision = receipt.Revision;
        change.Lines[0].UnitPriceKrw = 999;
        receipt = await f.Use.TermsAsync(receipt.AgreementId, "goods-owner-a", change, default);
        receipt = await f.Consent(receipt, 2);
        await Error("NeighborhoodSettlementWorkPriorConsentRequired", () => f.Accrue(receipt, "pick", "901", 2));
        await Error("NeighborhoodSettlementWorkPriorConsentRequired", () => f.Accrue(receipt, "pack", "903", 2));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 다른수취인기록이있어도같은실작업수량은전역에서한번만소비한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate("701"); await f.Complete();
        var existing = new NeighborhoodSettlementEntryDto { EntryId = "historical-other-payee", AgreementId = receipt.AgreementId,
            TermsVersion = 1, LineKey = "different-line", Kind = C.Picking, Billing = C.PerUnit, Quantity = 2,
            PayerUserId = "goods-owner-a", PayeeUserId = "historical-other-actor", AmountKrw = 200,
            OriginKind = "WarehouseTask", OriginId = "901", StatusCode = C.Accrued };
        f.Db.Add(new NeighborhoodSettlementEntryRecord { EntryId = existing.EntryId, AgreementId = receipt.AgreementId, TermsVersion = 1,
            LineKey = existing.LineKey, Kind = existing.Kind, OriginKind = existing.OriginKind, OriginId = existing.OriginId,
            AccrualKey = "historical-other-key", DetailsJson = JsonSerializer.Serialize(existing, new JsonSerializerOptions(JsonSerializerDefaults.Web)), StatusCode = C.Accrued });
        await f.Db.SaveChangesAsync();
        await Error("NeighborhoodSettlementCompletedQuantityExceeded", () => f.Accrue(receipt, "pick", "901", 1));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData("missing-proof", "MartCompletionEvidenceRequired")]
    [InlineData("missing-deadline", "MartWorkAgreementDeadlineRequired")]
    [InlineData("wrong-packing-history", "MartPackingEvidenceRequired")]
    [InlineData("future-start", "MartWorkEvidenceRequired")]
    public async Task 과거완료추정과시각수량불일치를작업보수근거로승격하지않는다(string defect, string reason)
    {
        await using var f = await Fixture.Create(); await f.Complete();
        var task = await f.Db.피킹포장작업.SingleAsync(task => task.Id == (defect == "future-start" ? 901 : 903));
        var proof = WarehouseActivityCompletionEvidence.Read(task.완료근거Json)!;
        if (defect == "missing-proof") task.완료근거Json = null;
        if (defect == "missing-deadline") task.완료근거Json = JsonSerializer.Serialize(proof with { AgreementDeadlineAtUtc = null });
        if (defect == "wrong-packing-history") (await f.Db.재고이력.SingleAsync(history => history.이력유형 == "포장")).변경후수량--;
        if (defect == "future-start") task.완료근거Json = JsonSerializer.Serialize(proof with { StartedAtUtc = f.Clock.Now.AddMinutes(5), AgreementDeadlineAtUtc = f.Clock.Now.AddMinutes(5) });
        await f.Db.SaveChangesAsync();
        var source = await f.Resolver.ResolveAsync(C.MartOrder, "701", default);
        Assert.DoesNotContain(source.CompletedWork, work => work.OriginId == task.Id.ToString());
        Assert.Equal(reason, source.Workflow!.WaitingReasonCode);
    }

    private static async Task Error(string code, Func<Task> call)
        => Assert.Equal(code, (await Assert.ThrowsAsync<NeighborhoodSettlementException>(call)).Code);

    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = DateTime.UtcNow.AddHours(-1);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Current(string user) : ICurrentUserAccessor
    { public string? UserId => user; public string? Role => "창고관리자"; }
    private sealed class Log : I사용자행위로그Service
    { public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class Publisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
    private sealed class Fixture(DistributedTestFixture data) : IAsyncDisposable
    {
        public 살뜰.Data.SsalddelContext Db => data.Db;
        public Clock Clock { get; } = new();
        public NeighborhoodSettlementSourceResolver Resolver => new(Db, Clock);
        public NeighborhoodSettlementUseCase Use => new(Db, Resolver,
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            Options.Create(new NeighborhoodSettlementOptions { Enabled = true }), Clock);
        public NeighborhoodSettlementAgreementRequest Terms(string id) => new()
        {
            RequestId = Guid.NewGuid(), SourceKind = C.MartOrder, SourceId = id, ActivityKind = C.StoredGoods,
            Lines = [new() { LineKey = "pick", Kind = C.Picking, Billing = C.PerUnit, UnitPriceKrw = 100, Quantity = id == "701" ? 2 : 3,
                PayerUserId = "goods-owner-a", PayeeUserId = "picker", BeneficiaryRole = "Picker" },
                new() { LineKey = "pack", Kind = C.Packing, Billing = C.PerUnit, UnitPriceKrw = 50, Quantity = id == "701" ? 2 : 3,
                PayerUserId = "goods-owner-a", PayeeUserId = "packer", BeneficiaryRole = "Packer" }]
        };
        public async Task<NeighborhoodSettlementReceiptDto> Activate(string id, NeighborhoodSettlementAgreementRequest? terms = null)
            => await Consent(await Use.CreateAsync("goods-owner-a", terms ?? Terms(id), default), 1);
        public async Task<NeighborhoodSettlementReceiptDto> Consent(NeighborhoodSettlementReceiptDto receipt, int version)
        {
            foreach (var actor in new[] { "goods-owner-a", "picker", "packer" })
                receipt = await Use.ConsentAsync(receipt.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
                    TermsVersion = version, Granted = true }, default);
            return receipt;
        }
        public Task<NeighborhoodSettlementReceiptDto> Accrue(NeighborhoodSettlementReceiptDto receipt, string line, string task, decimal quantity)
            => Use.AccrueAsync(receipt.AgreementId, "goods-owner-a", new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
                OriginKind = "WarehouseTask", OriginId = task, Lines = [new() { LineKey = line, Quantity = quantity }] }, default);
        public 살뜰.Data.SsalddelContext Independent() => data.IndependentContext();
        public static 창고작업요청Context Context(string user) => new("mart-test", user, "", "창고관리자", "/fixture", "fixture", "", "");
        public async Task Complete(string actor = "picker")
        {
            var picking = new 피킹작업UseCase(Db, new Current(actor), new Log(), data.Publisher);
            foreach (var key in new[] { "PICK-701", "PICK-702" })
            {
                Assert.True((await picking.시작Async(key, Context(actor), default)).IsSuccess);
                Assert.True((await picking.완료Async(key, new() { RackCode = "RACK-1", ProductConfirmed = true, QuantityConfirmed = true }, Context(actor), default)).IsSuccess);
            }
            await Pack(); Clock.Now = DateTime.UtcNow.AddSeconds(1);
        }
        public async Task Pack()
        {
            var result = await new 포장작업UseCase(Db, new Current("packer"), new Log(), data.Publisher).완료Async(
                1, new() { PackagingQuantity = 5, PackagingType = 포장유형코드.일반포장, InventoryConfirmed = true, PackageLabelConfirmed = true }, Context("packer"), default);
            Assert.True(result.IsSuccess, string.Join(",", result.Errors.Select(error => error.Message)));
        }
        public static async Task<Fixture> Create()
        {
            var data = await DistributedTestFixture.Create(); var f = new Fixture(data);
            var warehouse = await f.Db.창고.SingleAsync(row => row.Id == 11); warehouse.소유자UserId = "packer";
            f.Db.창고사용자.Add(new() { 창고Id = 11, UserId = "picker" });
            var item = await f.Db.입고상품.SingleAsync(row => row.Id == 1); item.가용수량 = 5; item.입고수량 = 5; item.상태 = "적재완료";
            foreach (var id in new[] { 701, 702 })
            {
                var quantity = id == 701 ? 2 : 3; var planId = id - 500; var reference = $"MART-{id}";
                f.Db.마트주문.Add(new 마트주문 { Id = id, 주문참조번호 = reference, 주문자UserId = $"buyer-{id}", 판매자UserId = "seller-a",
                    상품목록 = [new() { 출고예정Id = planId, SKU = item.SKU, 상품명 = item.상품명, 수량 = quantity }] });
                f.Db.출고예정.Add(new() { Id = planId, 주문참조번호 = reference, 주문자UserId = $"buyer-{id}", 판매자UserId = "seller-a",
                    입고상품Id = item.Id, 출고창고Id = 11, SKU = item.SKU, 상품명 = item.상품명, 수량 = quantity });
                foreach (var kind in new[] { 피킹포장작업유형.피킹, 피킹포장작업유형.포장 })
                    f.Db.피킹포장작업.Add(new() { Id = id + (kind == 피킹포장작업유형.피킹 ? 200 : 202),
                        작업Key = $"{(kind == 피킹포장작업유형.피킹 ? "PICK" : "PACK")}-{id}", 작업유형 = kind,
                        주문참조번호 = reference, 출고예정Id = planId, 입고상품Id = item.Id, 창고Id = 11,
                        작업자UserId = kind == 피킹포장작업유형.피킹 ? "picker" : "packer", 라인Key = $"line-{id}",
                        SKU = item.SKU, 상품명 = item.상품명, 수량 = quantity, 적재대코드 = "RACK-1" });
            }
            await f.Db.SaveChangesAsync(); return f;
        }
        public ValueTask DisposeAsync() => data.DisposeAsync();
    }
}
