using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Mart;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Tests.Application.Mart;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;
using 살뜰.도메인.마트;
using 살뜰.도메인.정산;
using 살뜰.도메인.창고;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using W = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementWorkflowCodes;

namespace Ssalddel.Tests.Application.Settlement;

/// <summary>실제 창고·기사·고객 command와 격리 SQL 정산을 연결합니다. HTTP·휴대폰·은행 이체 시험은 아닙니다.</summary>
public sealed class MartReceiptSettlementTests
{
    [Fact]
    public async Task 사전합의한실제작업보수는고객수령후명시신고하고대금수령진술과은행확인을구별한다()
    {
        await using var f = await MartReceiptTestFixture.CreateAsync();
        var resolver = new NeighborhoodSettlementSourceResolver(f.Db, f.Clock);
        var before = await resolver.ResolveAsync(C.MartOrder, "701", default);
        Assert.Null(before.CustomerReceipt); Assert.Null(before.RewardOutcome);
        Assert.DoesNotContain("customerReceipt", JsonSerializer.Serialize(before, Json), StringComparison.Ordinal);
        var economics = await Activate(f, 701);
        await f.CompleteWarehouseAsync(); await f.DeliverAsync(701);
        await AccrueAndConfirm(f, economics);
        var waiting = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
        var picking = Assert.Single(waiting.Entries, entry => entry.Kind == C.Picking);
        Assert.False(waiting.Workflow!.PaymentReleaseReady); Assert.Empty(waiting.Payments);
        await Error("NeighborhoodSettlementDeliveryReceiptRequired", () => Report(economics, picking.EntryId, picking.AmountKrw!.Value));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());

        var request = await f.ConfirmationRequestAsync(701);
        var confirmed = await f.CustomerUseCase().확인Async(701, request, default);
        Assert.True(confirmed.IsSuccess);
        var after = await resolver.ResolveAsync(C.MartOrder, "701", default);
        var receipt = Assert.IsType<NeighborhoodSettlementCustomerReceiptDto>(after.CustomerReceipt);
        Assert.Equal(2, receipt.ConfirmedQuantity); Assert.Equal(4, after.ActualCompletedUnits);
        Assert.Contains(receipt.ReceiptId, receipt.EvidenceReferences);
        Assert.Equal(before.SourceVersion, after.SourceVersion); Assert.Equal(before.SourceDigest, after.SourceDigest);
        Assert.Equal(JsonSerializer.Serialize(before.Participants, Json), JsonSerializer.Serialize(after.Participants, Json));
        Assert.Null(after.RewardOutcome); Assert.Null(after.HistoricalSaleTotalKrw);
        Assert.DoesNotContain(after.CompletedWork, work => work.Kind is C.ProductSale or C.Delivery or C.ActivityReward);
        var ready = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
        Assert.True(ready.Workflow!.PaymentReleaseReady); Assert.All(ready.Entries, entry => Assert.True(entry.PaymentReleaseReady));
        Assert.Equal(C.Active, ready.Agreement.StatusCode); Assert.Equal(1, ready.Agreement.TermsVersion);
        Assert.Empty(ready.Payments);
        var report = await Report(economics, picking.EntryId, picking.AmountKrw.Value);
        var reported = await economics.Use.GetAsync(economics.Id, economics.Picker, default);
        var payment = Assert.Single(reported.Payments);
        Assert.Equal(C.Reported, payment.StatusCode); Assert.Equal(0, payment.RecipientConfirmedAmountKrw);
        Assert.False(payment.IsActualBankVerified); Assert.False(reported.Totals.IsActualBankVerified);
        await economics.Use.ConfirmReceiptAsync(economics.Id, economics.Picker, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = report.Revision, PaymentId = payment.PaymentId, ConfirmedAmountKrw = 100,
            Reason = "격리 시험의 부분 대금 수령 진술" }, default);
        var received = await economics.Use.GetAsync(economics.Id, economics.Picker, default);
        Assert.Equal(100, received.Totals.RecipientConfirmedAmountKrw);
        Assert.Equal(C.PartiallyConfirmed, Assert.Single(received.Payments).StatusCode);
        Assert.False(received.Totals.IsActualBankVerified);

        // r67 원주문 조회도 별도 상태를 만들지 않고 기존 본인 정산의 현재 지급 관문을 읽습니다.
        var order = await f.Db.마트주문.SingleAsync(row => row.Id == 701);
        order.커뮤니티원장Id = "ssalddel-mart:" + order.주문참조번호;
        order.커뮤니티원장템플릿Key = CommunityLedgerTemplateKeys.SsalddelMart; await f.Db.SaveChangesAsync();
        var ledger = new 커뮤니티원장Dto { 원장Id = order.커뮤니티원장Id, 커뮤니티Id = "platform", Revision = 1,
            원장템플릿Key = CommunityLedgerTemplateKeys.SsalddelMart, 생성자UserId = economics.Payer,
            외부참조 = new Dictionary<string, string> { ["원천유형"] = "SsalddelMartOrder", ["원천Id"] = order.주문참조번호 } };
        var query = new 주문별원가보수조회Service(f.Db, new Ledgers(ledger), new Disclosures(), resolver,
            economics.Use, Execution(), Options.Create(new NeighborhoodSettlementOptions { Enabled = true }), f.Clock);
        var read = await query.GetAsync(ledger.원장Id, economics.Picker, default);
        Assert.True(Assert.Single(Assert.Single(read.Sources).Settlements).Workflow!.PaymentReleaseReady);
        Assert.Null(read.PlatformProfitAmountKrw); Assert.False(read.IsActualBankVerified);
    }

    [Theory]
    [InlineData("Partial")]
    [InlineData("Missing")]
    [InlineData("Damaged")]
    public async Task 수령뒤문제보류는기존보수와신고를보존하고신규양수신고만막으며음수환급은분리한다(string reason)
    {
        await using var f = await MartReceiptTestFixture.CreateAsync(); var economics = await Activate(f, 701);
        await f.CompleteWarehouseAsync(); await f.DeliverAsync(701); await AccrueAndConfirm(f, economics);
        Assert.True((await f.CustomerUseCase().확인Async(701, await f.ConfirmationRequestAsync(701), default)).IsSuccess);
        var ready = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
        var picking = Assert.Single(ready.Entries, entry => entry.Kind == C.Picking);
        var report = await Report(economics, picking.EntryId, picking.AmountKrw!.Value);
        await economics.Use.ConfirmReceiptAsync(economics.Id, economics.Picker, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = report.Revision, PaymentId = report.PaymentId!, ConfirmedAmountKrw = picking.AmountKrw.Value,
            Reason = "격리 시험의 대금 수령 진술" }, default);
        var prior = await new NeighborhoodSettlementSourceResolver(f.Db, f.Clock).ResolveAsync(C.MartOrder, "701", default);
        var request = await f.ConfirmationRequestAsync(701);
        var held = await f.CustomerUseCase().문제신고Async(701, new()
        { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = request.ExpectedAttemptStableId,
            ExpectedRevision = request.ExpectedRevision, ReasonCode = reason }, default);
        Assert.True(held.IsSuccess);
        var source = await new NeighborhoodSettlementSourceResolver(f.Db, f.Clock).ResolveAsync(C.MartOrder, "701", default);
        Assert.False(source.Workflow!.PaymentReleaseReady); Assert.Null(source.CustomerReceipt); Assert.Null(source.RewardOutcome);
        Assert.Equal(prior.SourceVersion, source.SourceVersion); Assert.Equal(prior.SourceDigest, source.SourceDigest);
        var physical = await 마트수령근거Reader.ReadAsync(f.Db, 701, f.Clock.GetUtcNow().UtcDateTime);
        Assert.NotNull(physical.ConfirmedProof); Assert.NotNull(physical.Hold); Assert.False(physical.PaymentReleaseReady);
        var retained = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
        Assert.All(retained.Entries, entry => Assert.Equal(C.Confirmed, entry.StatusCode));
        Assert.Equal(200, retained.Totals.ReportedPaymentAmountKrw); Assert.Equal(200, retained.Totals.RecipientConfirmedAmountKrw);
        var packing = Assert.Single(retained.Entries, entry => entry.Kind == C.Packing);
        await Error("NeighborhoodSettlementDeliveryReceiptRequired", () => Report(economics, packing.EntryId, packing.AmountKrw!.Value));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());

        // 실제 받은 금액의 감액 환급은 고객 수령 관문을 새로 요구하지 않는 기존 경로를 유지합니다.
        var adjustment = await economics.Use.AdjustmentAsync(economics.Id, economics.Payer, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = retained.Agreement.Revision, OriginalEntryId = picking.EntryId,
            AmountKrw = -100, Reason = "격리 시험의 상호 확인된 감액" }, default);
        var refundId = Assert.Single(adjustment.EntryIds);
        await Confirm(economics, refundId, economics.Payer); await Confirm(economics, refundId, economics.Picker);
        var current = await economics.Use.GetAsync(economics.Id, economics.Picker, default);
        await economics.Use.ReportPaymentAsync(economics.Id, economics.Picker, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = current.Agreement.Revision, AmountKrw = 100,
            EvidenceReference = "simulation:r68-refund-report", Allocations = [new() { EntryId = refundId, AmountKrw = 100 }] }, default);
        var refund = await economics.Use.GetAsync(economics.Id, economics.Picker, default);
        Assert.Equal(100, refund.Totals.ReportedPaymentAmountKrw); Assert.Equal(200, refund.Totals.RecipientConfirmedAmountKrw);
        Assert.False(refund.Workflow!.PaymentReleaseReady); Assert.False(refund.Totals.IsActualBankVerified);
    }

    [Fact]
    public async Task 별도주문의수령은다른주문의작업보수지급을열지않는다()
    {
        await using var f = await MartReceiptTestFixture.CreateAsync();
        var first = await Activate(f, 701); var second = await Activate(f, 702);
        await f.CompleteWarehouseAsync(); await f.CompleteAllDeliveriesAsync();
        await AccrueAndConfirm(f, first); await AccrueAndConfirm(f, second);
        Assert.True((await f.CustomerUseCase().확인Async(701, await f.ConfirmationRequestAsync(701), default)).IsSuccess);
        Assert.True((await first.Use.GetAsync(first.Id, first.Payer, default)).Workflow!.PaymentReleaseReady);
        var waiting = await second.Use.GetAsync(second.Id, second.Payer, default);
        Assert.False(waiting.Workflow!.PaymentReleaseReady);
        var picking = Assert.Single(waiting.Entries, entry => entry.Kind == C.Picking);
        await Error("NeighborhoodSettlementDeliveryReceiptRequired", () => Report(second, picking.EntryId, picking.AmountKrw!.Value));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 수령뒤출고가취소되면이전에확정한수령으로새지급을열지않는다()
    {
        await using var f = await MartReceiptTestFixture.CreateAsync(); var economics = await Activate(f, 701);
        await f.CompleteWarehouseAsync(); await f.DeliverAsync(701); await AccrueAndConfirm(f, economics);
        Assert.True((await f.CustomerUseCase().확인Async(701, await f.ConfirmationRequestAsync(701), default)).IsSuccess);
        var orderReference = await f.Db.마트주문.Where(row => row.Id == 701).Select(row => row.주문참조번호).SingleAsync();
        var plan = await f.Db.출고예정.SingleAsync(row => row.주문참조번호 == orderReference);
        plan.상태 = 출고상태.취소; await f.Db.SaveChangesAsync();
        var source = await new NeighborhoodSettlementSourceResolver(f.Db, f.Clock).ResolveAsync(C.MartOrder, "701", default);
        Assert.False(source.Workflow!.PaymentReleaseReady); Assert.Null(source.CustomerReceipt);
        var read = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
        Assert.Equal(300, read.Totals.KnownConfirmedAmountKrw);
        var picking = Assert.Single(read.Entries, entry => entry.Kind == C.Picking);
        await Error("NeighborhoodSettlementDeliveryReceiptRequired", () => Report(economics, picking.EntryId, picking.AmountKrw!.Value));
    }

    [Fact]
    public async Task 수령전용근거의식별값판본수량시각증거관문을다시검사하고성과금결과로대체하지않는다()
    {
        await using var f = await MartReceiptTestFixture.CreateAsync();
        var actual = new NeighborhoodSettlementSourceResolver(f.Db, f.Clock);
        var proxy = new Resolver(await actual.ResolveAsync(C.MartOrder, "701", default));
        var economics = await Activate(f, 701, proxy);
        await f.CompleteWarehouseAsync(); proxy.Snapshot = await actual.ResolveAsync(C.MartOrder, "701", default);
        await AccrueAndConfirm(f, economics, proxy);
        var baseline = proxy.Snapshot; var now = f.Clock.GetUtcNow().UtcDateTime;
        var receiptId = "mart-customer-receipt:701:" + Guid.NewGuid().ToString("N");
        var read = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
        var entry = Assert.Single(read.Entries, item => item.Kind == C.Picking);
        foreach (var invalid in new[] { "Valid", "Missing", "WrongSource", "WrongId", "ZeroRevision", "ZeroQuantity", "FutureReceipt",
            "UnspecifiedReceipt", "FutureObservation", "UnspecifiedObservation", "NoEvidence", "BlankEvidence", "WrongEvidence", "WorkflowFalse", "WrongWorkflow" })
        {
            proxy.Snapshot = new()
            {
                SourceKind = baseline.SourceKind, SourceId = baseline.SourceId, SourceVersion = baseline.SourceVersion,
                SourceDigest = baseline.SourceDigest, Participants = baseline.Participants, AllowedActivityKinds = baseline.AllowedActivityKinds,
                CompletedWork = baseline.CompletedWork, ActualCompletedUnits = baseline.ActualCompletedUnits,
                ObservedAtUtc = invalid == "FutureObservation" ? now.AddSeconds(1)
                    : invalid == "UnspecifiedObservation" ? DateTime.SpecifyKind(now, DateTimeKind.Unspecified) : now,
                // 성과금 결과를 둬도 전용 고객 수령 근거를 대신하지 않습니다.
                RewardOutcome = new(2, ["not-a-mart-receipt"]),
                Workflow = new() { Kind = invalid == "WrongWorkflow" ? W.InboundStorage : W.OutboundDelivery,
                    CompletionConfirmed = true, PaymentReleaseReady = invalid != "WorkflowFalse" },
                CustomerReceipt = invalid == "Missing" ? null : new()
                {
                    SourceId = invalid == "WrongSource" ? "702" : "701", ReceiptId = invalid == "WrongId" ? "unbound-receipt" : receiptId,
                    Revision = invalid == "ZeroRevision" ? 0 : 4, ConfirmedQuantity = invalid == "ZeroQuantity" ? 0 : 2,
                    ConfirmedAtUtc = invalid == "FutureReceipt" ? now.AddSeconds(1)
                        : invalid == "UnspecifiedReceipt" ? DateTime.SpecifyKind(now, DateTimeKind.Unspecified) : now,
                    EvidenceReferences = invalid == "NoEvidence" ? [] : invalid == "BlankEvidence" ? [receiptId, ""]
                        : invalid == "WrongEvidence" ? ["not-this-receipt"] : [receiptId]
                }
            };
            var current = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
            if (invalid == "Valid")
            {
                Assert.True(current.Workflow!.PaymentReleaseReady);
                Assert.All(current.Entries, item => Assert.True(item.PaymentReleaseReady));
                continue;
            }
            Assert.False(current.Workflow!.PaymentReleaseReady);
            Assert.All(current.Entries, item => Assert.False(item.PaymentReleaseReady));
            await Error("NeighborhoodSettlementDeliveryReceiptRequired", () => Report(economics, entry.EntryId, entry.AmountKrw!.Value));
        }
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Economics(string Id, string Payer, string Picker, string Packer, long OrderId, NeighborhoodSettlementUseCase Use);
    private static SsalddelExecutionModePolicy Execution() => new(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
    private static async Task<Economics> Activate(MartReceiptTestFixture f, long orderId, INeighborhoodSettlementSourceResolver? resolver = null)
    {
        resolver ??= new NeighborhoodSettlementSourceResolver(f.Db, f.Clock);
        var source = await resolver.ResolveAsync(C.MartOrder, orderId.ToString(CultureInfo.InvariantCulture), default);
        var payer = source.Participants.Single(party => party.Roles.Contains(NeighborhoodSettlementRoles.Owner)).UserId;
        var picker = source.Participants.Single(party => party.Roles.Contains(NeighborhoodSettlementRoles.Picker)).UserId;
        var packer = source.Participants.Single(party => party.Roles.Contains(NeighborhoodSettlementRoles.Packer)).UserId;
        var quantity = await f.Db.Set<마트주문상품>().Where(line => line.마트주문Id == orderId).SumAsync(line => line.수량);
        var use = new NeighborhoodSettlementUseCase(f.Db, resolver, Execution(), Options.Create(new NeighborhoodSettlementOptions { Enabled = true }), f.Clock);
        var receipt = await use.CreateAsync(payer, new()
        {
            RequestId = Guid.NewGuid(), SourceKind = C.MartOrder, SourceId = source.SourceId, ActivityKind = C.StoredGoods,
            Lines = [new() { LineKey = "pick", Kind = C.Picking, Billing = C.PerUnit, UnitPriceKrw = 100, Quantity = quantity,
                PayerUserId = payer, PayeeUserId = picker, BeneficiaryRole = NeighborhoodSettlementRoles.Picker },
                new() { LineKey = "pack", Kind = C.Packing, Billing = C.PerUnit, UnitPriceKrw = 50, Quantity = quantity,
                    PayerUserId = payer, PayeeUserId = packer, BeneficiaryRole = NeighborhoodSettlementRoles.Packer }]
        }, default);
        foreach (var actor in new[] { payer, picker, packer }.Distinct(StringComparer.Ordinal))
            receipt = await use.ConsentAsync(receipt.AgreementId, actor, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, TermsVersion = 1, Granted = true }, default);
        return new(receipt.AgreementId, payer, picker, packer, orderId, use);
    }
    private static async Task AccrueAndConfirm(MartReceiptTestFixture f, Economics economics, INeighborhoodSettlementSourceResolver? resolver = null)
    {
        resolver ??= new NeighborhoodSettlementSourceResolver(f.Db, f.Clock);
        var source = await resolver.ResolveAsync(C.MartOrder, economics.OrderId.ToString(CultureInfo.InvariantCulture), default);
        foreach (var work in source.CompletedWork.Where(work => work.Kind is C.Picking or C.Packing))
        {
            var summary = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
            var receipt = await economics.Use.AccrueAsync(economics.Id, economics.Payer, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = summary.Agreement.Revision, OriginKind = work.OriginKind, OriginId = work.OriginId,
                Lines = [new() { LineKey = work.Kind == C.Picking ? "pick" : "pack", Quantity = work.Quantity }] }, default);
            var entryId = Assert.Single(receipt.EntryIds);
            await Confirm(economics, entryId, economics.Payer); await Confirm(economics, entryId, work.ActorUserId!);
        }
    }
    private static async Task Confirm(Economics economics, string entryId, string actor)
    {
        var current = await economics.Use.GetAsync(economics.Id, actor, default);
        await economics.Use.ConfirmEntryAsync(economics.Id, actor, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = current.Agreement.Revision, EntryId = entryId }, default);
    }
    private static async Task<NeighborhoodSettlementReceiptDto> Report(Economics economics, string entryId, decimal amount)
    {
        var current = await economics.Use.GetAsync(economics.Id, economics.Payer, default);
        return await economics.Use.ReportPaymentAsync(economics.Id, economics.Payer, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = current.Agreement.Revision, AmountKrw = amount,
            EvidenceReference = "simulation:r68-work-payment-report", Allocations = [new() { EntryId = entryId, AmountKrw = amount }] }, default);
    }
    private static async Task Error(string code, Func<Task<NeighborhoodSettlementReceiptDto>> operation)
        => Assert.Equal(code, (await Assert.ThrowsAsync<NeighborhoodSettlementException>(operation)).Code);
    private sealed class Resolver(NeighborhoodSettlementSourceSnapshot source) : INeighborhoodSettlementSourceResolver
    {
        public NeighborhoodSettlementSourceSnapshot Snapshot { get; set; } = source;
        public Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string sourceKind, string sourceId, CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);
    }
    private sealed class Ledgers(커뮤니티원장Dto root) : I커뮤니티원장저장소
    {
        public Task<커뮤니티원장Dto?> 원장조회Async(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(id == root.원장Id ? root : null);
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Disclosures : I주문원장공개요청저장소
    {
        public Task<IReadOnlySet<string>> 승인된대상원장Ids조회Async(string rootId, string actor, IEnumerable<string> ids, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<원장공개요청기록> 요청생성Async(원장공개요청기록 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<원장공개요청기록?> 요청조회Async(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<원장공개요청기록>> 받은요청목록Async(string actor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<원장공개요청기록?> 요청결정Async(string id, string actor, bool approved, string? memo, DateTimeOffset at, DateTimeOffset expires, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
