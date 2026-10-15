using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.정산;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class NeighborhoodSettlementWorkflowTests
{
    [Fact]
    public async Task 생략한새옵션은구형요청의직렬화와멱등지문을변경하지않는다()
    {
        await using var f = await Fixture.Create(); var request = f.Terms();
        var payload = JsonSerializer.Serialize(new { Kind = "Create", AgreementId = "", Request = request }, Fixture.Json);
        var oldPayload = JsonNode.Parse(payload)!.AsObject();
        var oldRequest = oldPayload["request"]!.AsObject();
        oldRequest.Remove("workflowKind"); oldRequest.Remove("paymentReleaseCondition");
        Assert.DoesNotContain("\"workflowKind\"", payload); Assert.DoesNotContain("\"paymentReleaseCondition\"", payload);
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(oldPayload.ToJsonString(Fixture.Json)))).ToLowerInvariant();
        await f.Use().CreateAsync("owner", request, default);
        var receipt = await f.Db.Set<NeighborhoodSettlementCommandReceiptRecord>().SingleAsync();
        Assert.Equal(expected, receipt.InputDigest);
    }

    [Theory]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, NeighborhoodSettlementWorkflowCodes.InboundStorage, NeighborhoodSettlementWorkflowCodes.WorkCompleted)]
    [InlineData(NeighborhoodSettlementCodes.DistributedOrder, NeighborhoodSettlementWorkflowCodes.OutboundDelivery, NeighborhoodSettlementWorkflowCodes.CustomerReceived)]
    public async Task 신규약정은원장별업무와지급조건을동결하지만입력과재전송내용은바꾸지않는다(string sourceKind, string workflow, string release)
    {
        await using var f = await Fixture.Create(sourceKind);
        var input = f.Terms(); var before = JsonSerializer.Serialize(input);
        var receipt = await f.Use().CreateAsync("owner", input, default);
        Assert.Equal(before, JsonSerializer.Serialize(input));
        Assert.Null(input.WorkflowKind); Assert.Null(input.PaymentReleaseCondition);
        var summary = await f.Use().GetAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(workflow, summary.Agreement.Terms.WorkflowKind);
        Assert.Equal(release, summary.Agreement.Terms.PaymentReleaseCondition);
        var version = await f.Db.Set<NeighborhoodSettlementAgreementVersionRecord>().SingleAsync();
        Assert.Contains(workflow, version.TermsJson); Assert.Contains(release, version.TermsJson);
        var replay = await f.Use().CreateAsync("owner", input, default);
        Assert.True(replay.Replayed); Assert.Equal(receipt.AgreementId, replay.AgreementId);
        Assert.Single(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, NeighborhoodSettlementWorkflowCodes.OutboundDelivery)]
    [InlineData(NeighborhoodSettlementCodes.DistributedOrder, NeighborhoodSettlementWorkflowCodes.InboundStorage)]
    public async Task 원장과다른업무경계를클라이언트가선택할수없다(string sourceKind, string workflow)
    {
        await using var f = await Fixture.Create(sourceKind); var terms = f.Terms(); terms.WorkflowKind = workflow;
        await Error("NeighborhoodSettlementWorkflowSourceMismatch", () => f.Use().CreateAsync("owner", terms, default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, NeighborhoodSettlementWorkflowCodes.CustomerReceived)]
    [InlineData(NeighborhoodSettlementCodes.DistributedOrder, NeighborhoodSettlementWorkflowCodes.WorkCompleted)]
    public async Task 출고의고객수령조건을완화하거나입고를미래판매에묶는조건을거부한다(string sourceKind, string release)
    {
        await using var f = await Fixture.Create(sourceKind); var terms = f.Terms(); terms.PaymentReleaseCondition = release;
        await Error("NeighborhoodSettlementPaymentReleaseInvalid", () => f.Use().CreateAsync("owner", terms, default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, NeighborhoodSettlementCodes.Picking)]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, NeighborhoodSettlementCodes.Delivery)]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, NeighborhoodSettlementCodes.ProductSale)]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, NeighborhoodSettlementCodes.SalesAgency)]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, NeighborhoodSettlementCodes.OwnerRemittance)]
    [InlineData(NeighborhoodSettlementCodes.DistributedOrder, NeighborhoodSettlementCodes.Inbound)]
    [InlineData(NeighborhoodSettlementCodes.DistributedOrder, NeighborhoodSettlementCodes.Storage)]
    public async Task 신규약정에는다른업무흐름의보수항목을섞지못한다(string sourceKind, string lineKind)
    {
        await using var f = await Fixture.Create(sourceKind); var terms = f.Terms(); terms.Lines[0].Kind = lineKind;
        await Error("NeighborhoodSettlementWorkflowLineUnsupported", () => f.Use().CreateAsync("owner", terms, default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 약정조건변경으로원본이나업무흐름을다른것으로바꾸지못한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var change = f.Terms(); change.ExpectedRevision = receipt.Revision; change.SourceId = "another-order";
        await Error("NeighborhoodSettlementSourceChangeForbidden", () => f.Use().TermsAsync(receipt.AgreementId, "owner", change, default));
        change = f.Terms(); change.ExpectedRevision = receipt.Revision; change.WorkflowKind = NeighborhoodSettlementWorkflowCodes.InboundStorage;
        await Error("NeighborhoodSettlementWorkflowSourceMismatch", () => f.Use().TermsAsync(receipt.AgreementId, "owner", change, default));
        Assert.Equal(1, (await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Agreement.TermsVersion);
    }

    [Fact]
    public async Task 입고는적재완료근거로지급할수있고미래주문의고객수령을기다리지않는다()
    {
        await using var f = await Fixture.Create(NeighborhoodSettlementCodes.DistributedLot);
        f.Resolver.Snapshot = f.Source(ready: true, outcome: null);
        var receipt = await f.Activate(); receipt = await f.AccrueAndConfirm(receipt);
        var read = await f.Use().GetAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodSettlementWorkflowCodes.InboundStorage, read.Workflow!.Kind);
        Assert.True(read.Workflow.PaymentReleaseReady); Assert.Null(f.Resolver.Snapshot.RewardOutcome);
        var paid = await f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, read.Entries.Single().EntryId), default);
        var reported = await f.Use().GetAsync(receipt.AgreementId, "worker", default);
        Assert.Equal(100m, reported.Totals.ReportedPaymentAmountKrw);
        Assert.Equal(0m, reported.Totals.RecipientConfirmedAmountKrw);
        Assert.False(Assert.Single(reported.Payments).IsActualBankVerified);
        Assert.NotNull(paid.PaymentId);
    }

    [Fact]
    public async Task 출고작업보수는수령전에기록하지만지급신고는고객수령까지대기한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        receipt = await f.AccrueAndConfirm(receipt);
        var waiting = await f.Use().GetAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(100m, waiting.Totals.KnownConfirmedAmountKrw);
        Assert.False(waiting.Workflow!.PaymentReleaseReady); Assert.Empty(waiting.Payments);
        var entryId = Assert.Single(waiting.Entries).EntryId;
        Assert.False(waiting.Entries.Single().PaymentReleaseReady);
        Assert.Equal("DeliveryReceiptRequired", waiting.Entries.Single().PaymentWaitingReasonCode);
        await Error("NeighborhoodSettlementDeliveryReceiptRequired", () => f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, entryId), default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());

        f.Resolver.Snapshot = f.Source(ready: true, outcome: new(1, ["delivered", "customer-received"]));
        var ready = await f.Use().GetAsync(receipt.AgreementId, "worker", default);
        Assert.True(ready.Workflow!.PaymentReleaseReady);
        Assert.True(Assert.Single(ready.Entries).PaymentReleaseReady);
        Assert.Null(ready.Entries.Single().PaymentWaitingReasonCode);
        await f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, entryId), default);
        var reported = await f.Use().GetAsync(receipt.AgreementId, "worker", default);
        Assert.Equal(100m, reported.Totals.ReportedPaymentAmountKrw);
        Assert.Equal(0m, reported.Totals.RecipientConfirmedAmountKrw);
    }

    [Theory]
    [InlineData("MissingOutcome")]
    [InlineData("ZeroQuantity")]
    [InlineData("MissingEvidence")]
    [InlineData("WorkflowNotReady")]
    [InlineData("BlankEvidence")]
    [InlineData("FutureObservation")]
    [InlineData("UnspecifiedObservation")]
    [InlineData("WrongWorkflowKind")]
    public async Task 배송완료표시만있거나수령수량과근거가불충분하면출고지급을허용하지않는다(string invalid)
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); receipt = await f.AccrueAndConfirm(receipt);
        var outcome = invalid switch
        {
            "MissingOutcome" => null,
            "ZeroQuantity" => new NeighborhoodSettlementRewardOutcome(0, ["customer-received"]),
            "MissingEvidence" => new NeighborhoodSettlementRewardOutcome(1, []),
            "BlankEvidence" => new NeighborhoodSettlementRewardOutcome(1, [" "]),
            _ => new NeighborhoodSettlementRewardOutcome(1, ["delivered", "customer-received"])
        };
        f.Resolver.Snapshot = f.Source(ready: invalid != "WorkflowNotReady", outcome: outcome,
            observedAt: invalid == "FutureObservation" ? f.Clock.Now.AddMinutes(1)
                : invalid == "UnspecifiedObservation" ? DateTime.SpecifyKind(f.Clock.Now, DateTimeKind.Unspecified) : null,
            workflowKind: invalid == "WrongWorkflowKind" ? NeighborhoodSettlementWorkflowCodes.InboundStorage : null);
        var entryId = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries).EntryId;
        await Error("NeighborhoodSettlementDeliveryReceiptRequired", () => f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, entryId), default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 출고증액정정도원래작업의고객수령관문을따른다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); receipt = await f.AccrueAndConfirm(receipt);
        var entryId = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries).EntryId;
        receipt = await f.Use().AdjustmentAsync(receipt.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
            OriginalEntryId = entryId, AmountKrw = 20, Reason = "추가 작업량 합의" }, default);
        var adjustment = (await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries.Single(x => x.OriginalEntryId == entryId);
        receipt = await f.Confirm(receipt, adjustment.EntryId, "owner"); receipt = await f.Confirm(receipt, adjustment.EntryId, "worker");
        await Error("NeighborhoodSettlementDeliveryReceiptRequired", () => f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, adjustment.EntryId, 20), default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 이미수령한대금의감액환급은나중의배송예외로막지않는다()
    {
        await using var f = await Fixture.Create();
        f.Resolver.Snapshot = f.Source(ready: true, outcome: new(1, ["delivered", "customer-received"]));
        var receipt = await f.Activate(); receipt = await f.AccrueAndConfirm(receipt);
        var original = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries);
        receipt = await f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, original.EntryId), default);
        receipt = await f.Use().ConfirmReceiptAsync(receipt.AgreementId, "worker", new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
            PaymentId = receipt.PaymentId!, ConfirmedAmountKrw = 100, Reason = "입금 확인 진술" }, default);
        receipt = await f.Use().AdjustmentAsync(receipt.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
            OriginalEntryId = original.EntryId, AmountKrw = -20, Reason = "합의 환급" }, default);
        var refund = (await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries.Single(x => x.OriginalEntryId == original.EntryId);
        receipt = await f.Confirm(receipt, refund.EntryId, "owner"); receipt = await f.Confirm(receipt, refund.EntryId, "worker");
        f.Resolver.Snapshot = f.Source(ready: false, outcome: null);
        await f.Use().ReportPaymentAsync(receipt.AgreementId, "worker", f.Payment(receipt, refund.EntryId, 20), default);
        var reported = await f.Use().GetAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(2, reported.Payments.Count);
        Assert.Equal(80m, reported.Totals.ReportedPaymentAmountKrw);
    }

    [Fact]
    public async Task 정본가격판본이바뀌어도이미발생한채무는원래금액과당사자로지급할수있다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); receipt = await f.AccrueAndConfirm(receipt);
        var original = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries);
        f.Resolver.Snapshot = f.Source(ready: true, outcome: new(1, ["delivered", "customer-received"]), digest: "later-price-and-party-basis");
        await f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, original.EntryId), default);
        var read = await f.Use().GetAsync(receipt.AgreementId, "worker", default);
        Assert.Equal(100m, Assert.Single(read.Entries).AmountKrw);
        Assert.Equal("owner", Assert.Single(read.Payments).PayerUserId);
    }

    [Fact]
    public async Task 과거혼합약정은조회로바뀌지않으며명시적조건변경때만새경계를동결한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.ActivateLegacy();
        var before = await f.Db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking().SingleAsync();
        var read = await f.Use().GetAsync(receipt.AgreementId, "owner", default);
        Assert.Null(read.Agreement.Terms.WorkflowKind); Assert.Null(read.Agreement.Terms.PaymentReleaseCondition);
        Assert.Equal(NeighborhoodSettlementWorkflowCodes.LegacyMixed, read.Workflow!.Kind);
        Assert.Equal(before.TermsJson, (await f.Db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking().SingleAsync()).TermsJson);
        var change = f.Terms(); change.ExpectedRevision = receipt.Revision;
        await f.Use().TermsAsync(receipt.AgreementId, "owner", change, default);
        var versions = await f.Db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking().OrderBy(x => x.TermsVersion).ToArrayAsync();
        var old = JsonSerializer.Deserialize<NeighborhoodSettlementAgreementRequest>(versions[0].TermsJson, Fixture.Json)!;
        var current = JsonSerializer.Deserialize<NeighborhoodSettlementAgreementRequest>(versions[1].TermsJson, Fixture.Json)!;
        Assert.Null(old.WorkflowKind); Assert.Null(old.PaymentReleaseCondition);
        Assert.Equal(NeighborhoodSettlementWorkflowCodes.OutboundDelivery, current.WorkflowKind);
        Assert.Equal(NeighborhoodSettlementWorkflowCodes.CustomerReceived, current.PaymentReleaseCondition);
    }

    [Fact]
    public async Task 새판본의배송완료조건을과거발생항목에소급적용하지않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.ActivateLegacy(); receipt = await f.AccrueAndConfirm(receipt);
        var original = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries);
        var change = f.Terms(); change.ExpectedRevision = receipt.Revision;
        receipt = await f.Use().TermsAsync(receipt.AgreementId, "owner", change, default);
        await f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, original.EntryId), default);
        var read = await f.Use().GetAsync(receipt.AgreementId, "worker", default);
        Assert.Equal(1, Assert.Single(read.Entries).TermsVersion);
        Assert.Equal(2, read.Agreement.TermsVersion); Assert.False(read.Workflow!.PaymentReleaseReady);
        Assert.Equal(100m, Assert.Single(read.Payments).AmountKrw);
    }

    [Fact]
    public async Task 새판본에서만든증액도원본항목의과거지급조건을따른다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.ActivateLegacy(); receipt = await f.AccrueAndConfirm(receipt);
        var original = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries);
        var change = f.Terms(); change.ExpectedRevision = receipt.Revision;
        receipt = await f.Use().TermsAsync(receipt.AgreementId, "owner", change, default);
        receipt = await f.Use().AdjustmentAsync(receipt.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
            OriginalEntryId = original.EntryId, AmountKrw = 20, Reason = "과거 작업의 추가 보수" }, default);
        var adjustment = (await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries.Single(x => x.OriginalEntryId == original.EntryId);
        receipt = await f.Confirm(receipt, adjustment.EntryId, "owner"); receipt = await f.Confirm(receipt, adjustment.EntryId, "worker");
        await f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, adjustment.EntryId, 20), default);
        var payment = Assert.Single(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
        Assert.Equal(20m, JsonSerializer.Deserialize<NeighborhoodSettlementPaymentDto>(payment.DetailsJson, Fixture.Json)!.AmountKrw);
    }

    [Theory]
    [InlineData(NeighborhoodSettlementCodes.DistributedLot, false)]
    [InlineData(NeighborhoodSettlementCodes.DistributedOrder, true)]
    public async Task 입고완료보수와과거채권은현재원본조회장애가있어도확정근거로지급신고한다(string sourceKind, bool legacy)
    {
        await using var f = await Fixture.Create(sourceKind);
        var receipt = legacy ? await f.ActivateLegacy() : await f.Activate(); receipt = await f.AccrueAndConfirm(receipt);
        var entryId = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries).EntryId;
        f.Resolver.Unavailable = true;
        await f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, entryId), default);
        Assert.Single(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 고객수령을확인할수없는조회장애는출고지급을임의로완료처리하지않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); receipt = await f.AccrueAndConfirm(receipt);
        var entryId = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries).EntryId;
        f.Resolver.Unavailable = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", f.Payment(receipt, entryId), default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task 신규보관기간은전용재고근거를요구하고구형정본만과거완료근거를재사용한다(bool legacySource, bool explicitEvidence)
    {
        await using var f = await Fixture.Create(NeighborhoodSettlementCodes.DistributedLot);
        f.Resolver.Snapshot = f.Source(legacySource: legacySource, storageEvidence: explicitEvidence ? ["inventory-receipt"] : []);
        var terms = f.Terms(); terms.Lines = [new() { LineKey = "storage", Kind = NeighborhoodSettlementCodes.Storage,
            Billing = NeighborhoodSettlementCodes.StoragePeriod, AmountKrw = 100, Quantity = 1,
            PeriodStartUtc = f.Clock.Now.AddHours(-1), PeriodEndUtc = f.Clock.Now.AddMinutes(-1),
            PayerUserId = "owner", PayeeUserId = "worker", BeneficiaryRole = "SpaceOwner" }];
        var receipt = await f.Activate(terms);
        var request = new NeighborhoodSettlementAccrualRequest { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
            OriginKind = NeighborhoodSettlementCodes.DistributedLot, OriginId = "lot-1", Lines = [new() { LineKey = "storage", Quantity = 1,
                PeriodStartUtc = terms.Lines[0].PeriodStartUtc, PeriodEndUtc = terms.Lines[0].PeriodEndUtc }] };
        if (!legacySource && !explicitEvidence)
        {
            await Error("NeighborhoodSettlementStoragePeriodNotCompleted", () => f.Use().AccrueAsync(receipt.AgreementId, "owner", request, default));
            Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
        }
        else
        {
            await f.Use().AccrueAsync(receipt.AgreementId, "owner", request, default);
            var entry = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries);
            Assert.Equal(explicitEvidence ? "inventory-receipt" : "work-photo", Assert.Single(entry.WorkEvidenceReferences));
        }
    }

    [Fact]
    public async Task 일반운송은기존실제작업근거로완료를표시하고입출고분리때문에대기로오인하지않는다()
    {
        await using var f = await Fixture.Create(NeighborhoodSettlementCodes.CargoRequest);
        f.Resolver.Snapshot = f.Source(legacySource: true);
        var receipt = await f.Activate();
        var read = await f.Use().GetAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodSettlementWorkflowCodes.Other, read.Workflow!.Kind);
        Assert.Equal("Completed", read.Workflow.StateCode); Assert.True(read.Workflow.CompletionConfirmed);
        Assert.True(read.Workflow.PaymentReleaseReady); Assert.Null(read.Workflow.WaitingReasonCode);
    }

    [Fact]
    public async Task 과거입고원장에남은혼합피킹항목은추가발생을만들지못한다()
    {
        await using var f = await Fixture.Create(NeighborhoodSettlementCodes.DistributedLot);
        f.Resolver.Snapshot = f.Source(workKind: NeighborhoodSettlementCodes.Picking);
        var receipt = await f.ActivateLegacy(NeighborhoodSettlementCodes.Picking);
        await Error("NeighborhoodSettlementWorkflowLineUnsupported", () => f.Accrue(receipt));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    private static async Task Error(string code, Func<Task> call)
        => Assert.Equal(code, (await Assert.ThrowsAsync<NeighborhoodSettlementException>(call)).Code);

    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; } = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Resolver : INeighborhoodSettlementSourceResolver
    {
        public NeighborhoodSettlementSourceSnapshot Snapshot { get; set; } = new();
        public bool Unavailable { get; set; }
        public Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string kind, string id, CancellationToken ct)
            => Unavailable ? Task.FromException<NeighborhoodSettlementSourceSnapshot>(new InvalidOperationException("Source unavailable")) : Task.FromResult(Snapshot);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Fixture(SqliteConnection connection, SsalddelContext db, string sourceKind) : IAsyncDisposable
    {
        public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
        public SsalddelContext Db { get; } = db;
        public Resolver Resolver { get; } = new();
        public Clock Clock { get; } = new();
        public bool Inbound => sourceKind == NeighborhoodSettlementCodes.DistributedLot;
        public bool Cargo => sourceKind == NeighborhoodSettlementCodes.CargoRequest;
        public string ActivityKind => Cargo ? NeighborhoodSettlementCodes.TransportOnly : NeighborhoodSettlementCodes.StoredGoods;
        public NeighborhoodSettlementUseCase Use() => new(Db, Resolver,
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            Options.Create(new NeighborhoodSettlementOptions { Enabled = true, Profile = NeighborhoodSettlementOptions.RequiredProfile }), Clock);
        public NeighborhoodSettlementSourceSnapshot Source(bool ready = false, NeighborhoodSettlementRewardOutcome? outcome = null,
            string digest = "basis-v1", string? workKind = null, DateTime? observedAt = null, string? workflowKind = null,
            IReadOnlyList<string>? storageEvidence = null, bool legacySource = false) => new()
        {
            SourceKind = sourceKind, SourceId = Inbound ? "lot-1" : "order-1", SourceVersion = "1", SourceDigest = digest,
            AllowedActivityKinds = [ActivityKind],
            Participants = [new("owner", ["Owner"]), new("worker", ["Receiver", "Picker", "SpaceOwner", "Driver"]), new("buyer", ["Buyer"])],
            ActualCompletedUnits = 1, ObservedAtUtc = observedAt ?? Clock.Now, CompletionEvidenceReferences = ["work-photo"],
            StorageStartedAtUtc = Clock.Now.AddHours(-2), StorageIntervals = [new(Clock.Now.AddHours(-2), null)],
            StorageEvidenceReferences = storageEvidence ?? [],
            CompletedWork = [new(workKind ?? (Cargo ? NeighborhoodSettlementCodes.Delivery : Inbound ? NeighborhoodSettlementCodes.Inbound : NeighborhoodSettlementCodes.Picking),
                "InventoryHistory", "work-1", 1, Clock.Now.AddMinutes(-1), ["work-photo"], "worker", "owner", OriginRevision: 1,
                RelatedOrderId: Inbound ? null : "order-1")],
            RewardOutcome = outcome,
            Workflow = legacySource ? null : new() { Kind = workflowKind ?? (Inbound ? NeighborhoodSettlementWorkflowCodes.InboundStorage : NeighborhoodSettlementWorkflowCodes.OutboundDelivery),
                StateCode = ready ? "Completed" : "InProgress", CompletionConfirmed = ready, PaymentReleaseReady = ready,
                WaitingReasonCode = ready ? null : "CustomerReceiptRequired", InventoryLotIds = ["lot-1"] }
        };
        public NeighborhoodSettlementAgreementRequest Terms() => new()
        {
            RequestId = Guid.NewGuid(), SourceKind = sourceKind, SourceId = Inbound ? "lot-1" : "order-1",
            ActivityKind = ActivityKind,
            Lines = [new() { LineKey = "work", Kind = Cargo ? NeighborhoodSettlementCodes.Delivery : Inbound ? NeighborhoodSettlementCodes.Inbound : NeighborhoodSettlementCodes.Picking,
                Billing = NeighborhoodSettlementCodes.PerUnit, UnitPriceKrw = 100, Quantity = 1,
                PayerUserId = "owner", PayeeUserId = "worker", BeneficiaryRole = Cargo ? "Driver" : Inbound ? "Receiver" : "Picker" }]
        };
        public async Task<NeighborhoodSettlementReceiptDto> Activate(NeighborhoodSettlementAgreementRequest? terms = null)
        {
            var receipt = await Use().CreateAsync("owner", terms ?? Terms(), default);
            foreach (var actor in new[] { "owner", "worker" })
                receipt = await Use().ConsentAsync(receipt.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
                    TermsVersion = 1, Granted = true }, default);
            return receipt;
        }
        public Task<NeighborhoodSettlementReceiptDto> Accrue(NeighborhoodSettlementReceiptDto receipt)
            => Use().AccrueAsync(receipt.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
                OriginKind = "InventoryHistory", OriginId = "work-1", Lines = [new() { LineKey = "work", Quantity = 1 }] }, default);
        public async Task<NeighborhoodSettlementReceiptDto> AccrueAndConfirm(NeighborhoodSettlementReceiptDto receipt)
        {
            receipt = await Accrue(receipt); var entryId = Assert.Single(receipt.EntryIds);
            receipt = await Confirm(receipt, entryId, "owner"); return await Confirm(receipt, entryId, "worker");
        }
        public Task<NeighborhoodSettlementReceiptDto> Confirm(NeighborhoodSettlementReceiptDto receipt, string entryId, string actor)
            => Use().ConfirmEntryAsync(receipt.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, EntryId = entryId }, default);
        public NeighborhoodSettlementPaymentReportRequest Payment(NeighborhoodSettlementReceiptDto receipt, string entryId, decimal amount = 100)
            => new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, AmountKrw = amount, EvidenceReference = "당사자 지급 진술",
                Allocations = [new() { EntryId = entryId, AmountKrw = amount }] };
        public async Task<NeighborhoodSettlementReceiptDto> ActivateLegacy(string? lineKind = null)
        {
            // 과거 판본을 격리 DB의 초기 상태로 넣습니다. 이미 저장된 불변 TermsJson은 수정하지 않습니다.
            var terms = Terms();
            if (lineKind is not null) { terms.Lines[0].Kind = lineKind; terms.Lines[0].BeneficiaryRole = "Picker"; }
            var agreementId = "legacy-nsa-" + Guid.NewGuid().ToString("N");
            var consents = new[] { "owner", "worker" }.Select(actor => new NeighborhoodSettlementConsentDto
                { UserId = actor, TermsVersion = 1, Granted = true, RecordedAtUtc = Clock.Now }).ToArray();
            var termsJson = JsonSerializer.Serialize(terms, Json);
            var sourceJson = JsonSerializer.Serialize(Resolver.Snapshot, Json);
            var consentsJson = JsonSerializer.Serialize(consents, Json);
            Db.Add(new NeighborhoodSettlementAgreementRecord
            {
                AgreementId = agreementId, SourceKind = terms.SourceKind, SourceId = terms.SourceId,
                CreatorUserId = "owner", ActivityKind = terms.ActivityKind, TermsVersion = 1, Revision = 3,
                TermsJson = termsJson, SourceSnapshotJson = sourceJson, ConsentsJson = consentsJson,
                StatusCode = NeighborhoodSettlementCodes.Active, CreatedAtUtc = Clock.Now, UpdatedAtUtc = Clock.Now
            });
            Db.Add(new NeighborhoodSettlementAgreementVersionRecord
            {
                AgreementId = agreementId, TermsVersion = 1, TermsJson = termsJson,
                SourceSnapshotJson = sourceJson, ConsentsJson = consentsJson, CreatedAtUtc = Clock.Now
            });
            await Db.SaveChangesAsync(); Db.ChangeTracker.Clear();
            return new() { RequestId = terms.RequestId, AgreementId = agreementId, Kind = "LegacyFixture",
                Revision = 3, StatusCode = NeighborhoodSettlementCodes.Active, RecordedAtUtc = Clock.Now };
        }
        public static async Task<Fixture> Create(string sourceKind = NeighborhoodSettlementCodes.DistributedOrder)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); var fixture = new Fixture(connection, db, sourceKind);
            fixture.Resolver.Snapshot = fixture.Source(); return fixture;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
