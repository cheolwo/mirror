using System.Text.Json;
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

public sealed partial class NeighborhoodSettlementUseCaseTests
{
    [Theory]
    [InlineData(false, "neighborhood-settlement-r38", SsalddelExecutionMode.Simulation)]
    [InlineData(true, "wrong-profile", SsalddelExecutionMode.Simulation)]
    [InlineData(true, "neighborhood-settlement-r38", SsalddelExecutionMode.Operational)]
    public async Task 기본비활성과정확한SimulationProfile이아니면기록을만들지않는다(bool enabled, string profile, SsalddelExecutionMode mode)
    {
        await using var f = await Fixture.Create();
        var u = f.Use(enabled, profile, mode);
        await Error("NeighborhoodSettlementDisabled", () => u.CreateAsync("owner", f.Terms(), default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 상대의금액을볼수없고다른계정은원장과요청영수증을조회하지못한다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate();
        await Error("NeighborhoodSettlementNotFound", () => f.Use().GetAsync(r.AgreementId, "outsider", default));
        Assert.Empty(await f.Use().MySummaryAsync("outsider", default));
        await Error("NeighborhoodSettlementReceiptNotFound", () => f.Use().ReceiptAsync("buyer", r.RequestId, default));
        var buyer = await f.Use().GetAsync(r.AgreementId, "buyer", default);
        Assert.Empty(buyer.Agreement.Terms.Lines); Assert.Empty(buyer.Entries); Assert.Empty(buyer.Payments);
    }

    [Fact]
    public async Task 같은요청은최초응답을재생하고금액변경은충돌로차단한다()
    {
        await using var f = await Fixture.Create(); var request = f.Terms();
        var first = await f.Use().CreateAsync("owner", request, default);
        await using var independent = f.Context(); var repeat = await f.Use(independent).CreateAsync("owner", request, default);
        Assert.Equal(first.AgreementId, repeat.AgreementId); Assert.True(repeat.Replayed);
        request.Lines[0].UnitPriceKrw = 101;
        await Error("NeighborhoodSettlementRequestContentConflict", () => f.Use().CreateAsync("owner", request, default));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 조건변경은전원동의를초기화하고이전판본과발생액은보존한다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate();
        r = await f.Accrue(r, 2); var before = await f.Use().GetAsync(r.AgreementId, "owner", default);
        var change = f.Terms(); change.ExpectedRevision = r.Revision; change.Lines[0].UnitPriceKrw = 200;
        r = await f.Use().TermsAsync(r.AgreementId, "owner", change, default);
        var after = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal(2, after.Agreement.TermsVersion); Assert.Empty(after.Agreement.Consents);
        Assert.Equal(200m, after.Entries.Single().AmountKrw); Assert.Equal(before.Entries.Single().AmountKrw, after.Entries.Single().AmountKrw);
        await Error("NeighborhoodSettlementLatestConsentRequired", () => f.Accrue(r, 1));
        var versions = await f.Db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking().OrderBy(x => x.TermsVersion).ToArrayAsync();
        Assert.Equal(2, versions.Length); Assert.Contains("100", versions[0].TermsJson); Assert.Contains("200", versions[1].TermsJson);
    }

    [Fact]
    public async Task 당사자나동결가격근거가바뀌면새동의없이추가발생을만들지않는다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate();
        f.Resolver.Snapshot = f.Source(digest: "changed-source");
        await Error("NeighborhoodSettlementSourceChangedReconfirmationRequired", () => f.Accrue(r, 1));
        var read = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal("ReconfirmationRequired", read.Agreement.StatusCode);
        Assert.Equal(new[] { "change-terms" }, read.Agreement.AllowedActions);
        Assert.Empty(read.Entries);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(6)]
    public async Task 수량은양수여야하며실제완료량과합의상한을넘지못한다(int quantity)
    {
        await using var f = await Fixture.Create(); var r = await f.Activate();
        var code = quantity <= 0 ? "NeighborhoodSettlementQuantityInvalid" : "NeighborhoodSettlementCompletedQuantityExceeded";
        await Error(code, () => f.Accrue(r, quantity));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 실제피킹한사람이아니거나사진만있고완료근거가없으면보수를발생하지않는다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate();
        f.Resolver.Snapshot = f.Source(workActor: null);
        await Error("NeighborhoodSettlementCompletedWorkRequired", () => f.Accrue(r, 1));
        f.Resolver.Snapshot = f.Source(workActor: "receiver");
        await Error("NeighborhoodSettlementCompletedWorkRequired", () => f.Accrue(r, 1));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 합의를다시만들거나payer와LineKey를바꿔도동일원본처리량을복제할수없다()
    {
        await using var f = await Fixture.Create(); var first = await f.Activate(); first = await f.Accrue(first, 4);
        var terms = f.Terms(); terms.Lines[0].LineKey = "another-label"; terms.Lines[0].PayerUserId = "buyer";
        terms.ExpectedRevision = first.Revision;
        var second = await f.Use().TermsAsync(first.AgreementId, "owner", terms, default);
        second = await f.Reconsent(second, terms);
        await Error("NeighborhoodSettlementCompletedQuantityExceeded", () => f.Accrue(second, 2, "buyer", "another-label"));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 지급신고는payer만수령확인은payee만하며부분금액과대기신고한도를구분한다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate(); r = await f.Accrue(r, 2);
        var entry = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single();
        r = await f.Confirm(r, entry.EntryId, "owner"); r = await f.Confirm(r, entry.EntryId, "picker");
        var request = f.Report(r, entry.EntryId, 120);
        await Error("NeighborhoodSettlementPayerRequired", () => f.Use().ReportPaymentAsync(r.AgreementId, "picker", request, default));
        r = await f.Use().ReportPaymentAsync(r.AgreementId, "owner", request, default);
        var pending = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal(120m, pending.Totals.ReportedPaymentAmountKrw); Assert.Equal(0m, pending.Totals.RecipientConfirmedAmountKrw);
        Assert.False(pending.Payments.Single().IsActualBankVerified);
        await Error("NeighborhoodSettlementPaymentExceedsUnpaidAmount", () => f.Use().ReportPaymentAsync(r.AgreementId, "owner", f.Report(r, entry.EntryId, 100), default));
        var receipt = new NeighborhoodSettlementPaymentReceiptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            PaymentId = r.PaymentId!, ConfirmedAmountKrw = 70, Reason = "일부 수령 확인" };
        await Error("NeighborhoodSettlementRecipientRequired", () => f.Use().ConfirmReceiptAsync(r.AgreementId, "owner", receipt, default));
        r = await f.Use().ConfirmReceiptAsync(r.AgreementId, "picker", receipt, default);
        var partial = await f.Use().GetAsync(r.AgreementId, "picker", default);
        Assert.Equal(70m, partial.Totals.RecipientConfirmedAmountKrw); Assert.Equal(130m, partial.Totals.OutstandingAmountKrw);
        Assert.Equal(NeighborhoodSettlementCodes.PartiallyConfirmed, partial.Payments.Single().StatusCode);
        receipt.RequestId = Guid.NewGuid(); receipt.ExpectedRevision = r.Revision; receipt.ConfirmedAmountKrw = 120;
        await f.Use().ConfirmReceiptAsync(r.AgreementId, "picker", receipt, default);
        Assert.Equal(120m, (await f.Use().GetAsync(r.AgreementId, "owner", default)).Totals.RecipientConfirmedAmountKrw);
    }

    [Fact]
    public async Task 분쟁과감액정정은원본을덮어쓰지않고양측확인전추가지급을막는다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate(); r = await f.Accrue(r, 2);
        var original = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single();
        r = await f.Confirm(r, original.EntryId, "owner"); r = await f.Confirm(r, original.EntryId, "picker");
        r = await f.Use().AdjustmentAsync(r.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginalEntryId = original.EntryId, AmountKrw = -50, Reason = "합의 감액" }, default);
        var adjustment = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single(x => x.OriginalEntryId is not null);
        await Error("NeighborhoodSettlementAdjustmentConfirmationRequired", () => f.Confirm(r, original.EntryId, "picker"));
        r = await f.Confirm(r, adjustment.EntryId, "owner"); r = await f.Confirm(r, adjustment.EntryId, "picker");
        r = await f.Confirm(r, original.EntryId, "owner"); r = await f.Confirm(r, original.EntryId, "picker");
        await Error("NeighborhoodSettlementPaymentExceedsUnpaidAmount", () => f.Use().ReportPaymentAsync(r.AgreementId, "owner", f.Report(r, original.EntryId, 160), default));
        var read = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal(200m, read.Entries.Single(x => x.EntryId == original.EntryId).AmountKrw);
        Assert.Equal(150m, read.Totals.KnownConfirmedAmountKrw);
    }

    [Fact]
    public async Task 보관기간은실제보관시작이후경과한기간만허용하고합의간중복을막는다()
    {
        await using var f = await Fixture.Create();
        f.Resolver.Snapshot = f.Source(sourceKind: NeighborhoodSettlementCodes.DistributedLot);
        var terms = f.Terms();
        terms.Lines = [new() { LineKey = "period", Kind = NeighborhoodSettlementCodes.Storage, Billing = NeighborhoodSettlementCodes.StoragePeriod,
            AmountKrw = 500, Quantity = 5, PayerUserId = "owner", PayeeUserId = "space", BeneficiaryRole = "SpaceOwner",
            PeriodStartUtc = f.Clock.Now.AddDays(-1), PeriodEndUtc = f.Clock.Now.AddHours(-1) }];
        var r = await f.Activate(terms);
        var accrual = new NeighborhoodSettlementAccrualRequest { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginKind = f.Resolver.Snapshot.SourceKind, OriginId = f.Resolver.Snapshot.SourceId,
            Lines = [new() { LineKey = "period", Quantity = 5, PeriodStartUtc = terms.Lines[0].PeriodStartUtc, PeriodEndUtc = terms.Lines[0].PeriodEndUtc }] };
        await Error("NeighborhoodSettlementStoragePeriodNotCompleted", () => f.Use().AccrueAsync(r.AgreementId, "owner", accrual, default));
        f.Resolver.Snapshot = f.Source(storageStart: f.Clock.Now.AddDays(-2), sourceKind: NeighborhoodSettlementCodes.DistributedLot);
        await f.Use().AccrueAsync(r.AgreementId, "owner", accrual, default);
        terms.RequestId = Guid.NewGuid(); terms.ExpectedRevision = r.Revision + 1;
        var current = await f.Use().GetAsync(r.AgreementId, "owner", default); terms.ExpectedRevision = current.Agreement.Revision;
        var second = await f.Use().TermsAsync(r.AgreementId, "owner", terms, default);
        second = await f.Reconsent(second, terms);
        accrual.RequestId = Guid.NewGuid(); accrual.ExpectedRevision = second.Revision;
        await Error("NeighborhoodSettlementStoragePeriodOverlap", () => f.Use().AccrueAsync(second.AgreementId, "owner", accrual, default));
    }

    [Fact]
    public async Task 판매금액미확인은무료가아니며입고보관료와출고판매대행료를분리한다()
    {
        await using var f = await Fixture.Create();
        f.Resolver.Snapshot = f.Source(saleTotal: null, saleWork: true, workTime: null);
        var terms = f.Terms(); terms.GoodsOwnerUserId = "owner";
        terms.Lines = [new() { LineKey = "sale", Kind = NeighborhoodSettlementCodes.ProductSale, Billing = NeighborhoodSettlementCodes.Fixed,
                AmountKrw = 1000, PayerUserId = "buyer", PayeeUserId = "owner", BeneficiaryRole = "Owner" },
            new() { LineKey = "agency", Kind = NeighborhoodSettlementCodes.SalesAgency, Billing = NeighborhoodSettlementCodes.PerUnit,
                UnitPriceKrw = 20, Quantity = 5, PayerUserId = "owner", PayeeUserId = "seller", BeneficiaryRole = "Seller" }];
        var r = await f.Activate(terms); var read = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal(1, read.Totals.UnresolvedLineCount);
        await Error("NeighborhoodSettlementAmountUnresolved", () => f.Accrue(r, 1, "owner", "sale"));
        r = await f.Use().AccrueAsync(r.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginKind = "ProofReceipt", OriginId = "pick-1", Lines = [new() { LineKey = "agency", Quantity = 2 }] }, default);
        read = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal(40m, read.Totals.KnownAccruedAmountKrw); Assert.Single(read.Entries);
        Assert.All(read.Entries, x => Assert.NotEqual(NeighborhoodSettlementCodes.ProductSale, x.Kind));

        await using var inbound = await Fixture.Create();
        inbound.Resolver.Snapshot = inbound.Source(sourceKind: NeighborhoodSettlementCodes.DistributedLot, saleTotal: null);
        var storageTerms = inbound.Terms();
        storageTerms.Lines = [new() { LineKey = "storage", Kind = NeighborhoodSettlementCodes.Storage, Billing = NeighborhoodSettlementCodes.PerUnit,
            UnitPriceKrw = 30, Quantity = 5, PayerUserId = "owner", PayeeUserId = "space", BeneficiaryRole = "SpaceOwner" }];
        var storage = await inbound.Activate(storageTerms);
        storage = await inbound.Use().AccrueAsync(storage.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = storage.Revision,
            OriginKind = "ProofReceipt", OriginId = "pick-1", Lines = [new() { LineKey = "storage", Quantity = 2 }] }, default);
        var storageRead = await inbound.Use().GetAsync(storage.AgreementId, "owner", default);
        Assert.Equal(60m, storageRead.Totals.KnownAccruedAmountKrw);
        Assert.Equal(NeighborhoodSettlementCodes.Storage, Assert.Single(storageRead.Entries).Kind);
        Assert.NotEqual(read.Agreement.Terms.WorkflowKind, storageRead.Agreement.Terms.WorkflowKind);
    }

    [Fact]
    public async Task 기존배송원장을참고할뿐새지급을만들지않고관련Source의반복참고도막는다()
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(delivery: true);
        var terms = f.Terms(); terms.Lines = [new() { LineKey = "delivery", Kind = NeighborhoodSettlementCodes.Delivery,
            AmountKrw = 4000, PayerUserId = "buyer", PayeeUserId = "driver", BeneficiaryRole = "Driver" }];
        var r = await f.Activate(terms, "buyer"); r = await f.Accrue(r, 1, "buyer", "delivery");
        var read = await f.Use().GetAsync(r.AgreementId, "driver", default); var reference = read.Entries.Single();
        Assert.True(reference.IncludedInDelivery); Assert.Empty(reference.AllowedActions); Assert.Equal(0m, read.Totals.KnownAccruedAmountKrw);
        await Error("NeighborhoodSettlementEntryNotConfirmable", () => f.Confirm(r, reference.EntryId, "driver"));
        f.Resolver.Snapshot = f.Source(delivery: true, sourceId: "lot-2", originId: "pick-2");
        terms.SourceId = "lot-2";
        var second = await f.Activate(terms, "buyer");
        await Error("NeighborhoodSettlementOriginalDeliveryAlreadyReferenced", () => f.Accrue(second, 1, "buyer", "delivery", "pick-2"));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 판매자수금은동일총액의소유자송금채무를만들고경제합계에두번더하지않는다()
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(saleTotal: 1000, saleWork: true);
        var terms = f.Terms(); terms.GoodsPaymentRoute = NeighborhoodSettlementCodes.SellerCollect; terms.GoodsOwnerUserId = "owner"; terms.SellerUserId = "seller";
        terms.Lines = [new() { LineKey = "sale", Kind = NeighborhoodSettlementCodes.ProductSale, AmountKrw = 1000,
                PayerUserId = "buyer", PayeeUserId = "seller", BeneficiaryRole = "Seller" },
            new() { LineKey = "remit", Kind = NeighborhoodSettlementCodes.OwnerRemittance, AmountKrw = 1000,
                PayerUserId = "seller", PayeeUserId = "owner", BeneficiaryRole = "Owner", OriginalLineKey = "sale" }];
        var r = await f.Activate(terms, "seller"); r = await f.Accrue(r, 1, "seller", "sale");
        var seller = await f.Use().GetAsync(r.AgreementId, "seller", default);
        Assert.Equal(2, seller.Entries.Count); Assert.Equal(1000m, seller.Totals.KnownAccruedAmountKrw); Assert.Equal(1, seller.Totals.TransitEntryCount);
        Assert.Equal(1000m, seller.Entries.Single(x => x.IsTransit).AmountKrw);
        terms.RequestId = Guid.NewGuid(); terms.Lines[1].AmountKrw = 900;
        await Error("NeighborhoodSettlementOwnerRemittanceGrossMismatch", () => f.Use().CreateAsync("seller", terms, default));
    }

    [Fact]
    public async Task 내부작업은내부원가로남지만외부수금신고가되지않는다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms(); terms.Lines[0].PayerUserId = "picker";
        f.Resolver.Snapshot = f.Source(internalPicker: true);
        var r = await f.Activate(terms, "picker"); r = await f.Accrue(r, 2, "picker");
        var read = await f.Use().GetAsync(r.AgreementId, "picker", default); var entry = read.Entries.Single();
        Assert.True(entry.IsInternal); Assert.Equal(0m, read.Totals.KnownAccruedAmountKrw); Assert.Equal(1, read.Totals.InternalEntryCount);
        r = await f.Confirm(r, entry.EntryId, "picker");
        await Error("NeighborhoodSettlementEntryNotPayable", () => f.Use().ReportPaymentAsync(r.AgreementId, "picker", f.Report(r, entry.EntryId, 200), default));
    }

    [Fact]
    public async Task 음식화물활동종류를섞거나검증되지않은역할에숫자금액을지정할수없다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms(); terms.ActivityKind = NeighborhoodSettlementCodes.PreparedFood;
        await Error("NeighborhoodSettlementActivityUnsupported", () => f.Use().CreateAsync("owner", terms, default));
        terms = f.Terms(); terms.Lines[0].PayeeUserId = "buyer";
        await Error("NeighborhoodSettlementLinePartyMismatch", () => f.Use().CreateAsync("owner", terms, default));
        terms = f.Terms(); terms.Lines[0].PayeeUserId = "";
        await Error("NeighborhoodSettlementUnmappedAmountForbidden", () => f.Use().CreateAsync("owner", terms, default));
    }

    [Fact]
    public async Task 이미받은대금의감액은반대방향환급신고와수령확인후순수령액에반영한다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate(); r = await f.Accrue(r, 1);
        var entry = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single();
        r = await f.Confirm(r, entry.EntryId, "owner"); r = await f.Confirm(r, entry.EntryId, "picker");
        r = await f.Use().ReportPaymentAsync(r.AgreementId, "owner", f.Report(r, entry.EntryId, 100), default);
        r = await f.Use().ConfirmReceiptAsync(r.AgreementId, "picker", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            PaymentId = r.PaymentId!, ConfirmedAmountKrw = 100, Reason = "원금 수령" }, default);
        r = await f.Use().AdjustmentAsync(r.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginalEntryId = entry.EntryId, AmountKrw = -50, Reason = "반품 합의" }, default);
        var refundEntry = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single(x => x.AmountKrw < 0);
        r = await f.Confirm(r, refundEntry.EntryId, "owner"); r = await f.Confirm(r, refundEntry.EntryId, "picker");
        await Error("NeighborhoodSettlementPayerRequired", () => f.Use().ReportPaymentAsync(r.AgreementId, "owner", f.Report(r, refundEntry.EntryId, 50), default));
        r = await f.Use().ReportPaymentAsync(r.AgreementId, "picker", f.Report(r, refundEntry.EntryId, 50), default);
        var pending = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal(100m, pending.Totals.RecipientConfirmedAmountKrw);
        Assert.Equal("picker", pending.Payments.Single(x => x.PaymentId == r.PaymentId).PayerUserId);
        Assert.Equal("owner", pending.Payments.Single(x => x.PaymentId == r.PaymentId).PayeeUserId);
        r = await f.Use().ConfirmReceiptAsync(r.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            PaymentId = r.PaymentId!, ConfirmedAmountKrw = 50, Reason = "환급 수령" }, default);
        var final = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal(50m, final.Totals.KnownConfirmedAmountKrw); Assert.Equal(50m, final.Totals.RecipientConfirmedAmountKrw);
        Assert.Equal(0m, final.Totals.OutstandingAmountKrw); Assert.False(final.Totals.IsActualBankVerified);
        await Error("NeighborhoodSettlementRefundExceedsReceivedAmount", () => f.Use().ReportPaymentAsync(r.AgreementId, "picker", f.Report(r, refundEntry.EntryId, 50), default));
        await Error("NeighborhoodSettlementPaidAdjustmentRequiresCorrection", () => f.Use().DisputeAsync(r.AgreementId, "owner",
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision, EntryId = refundEntry.EntryId, Reason = "환급 후 조건 변경" }, default));
        Assert.DoesNotContain("dispute", (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single(x => x.EntryId == refundEntry.EntryId).AllowedActions);
        r = await f.Use().AdjustmentAsync(r.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginalEntryId = entry.EntryId, AmountKrw = 50, Reason = "환급 후 추가 보수 합의" }, default);
        var correction = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single(x => x.OriginalEntryId == entry.EntryId && x.AmountKrw > 0);
        r = await f.Confirm(r, correction.EntryId, "owner"); r = await f.Confirm(r, correction.EntryId, "picker");
        r = await f.Use().ReportPaymentAsync(r.AgreementId, "owner", f.Report(r, correction.EntryId, 50), default);
        r = await f.Use().ConfirmReceiptAsync(r.AgreementId, "picker", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            PaymentId = r.PaymentId!, ConfirmedAmountKrw = 50, Reason = "추가 보수 수령" }, default);
        final = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Equal(100m, final.Totals.KnownConfirmedAmountKrw); Assert.Equal(100m, final.Totals.RecipientConfirmedAmountKrw);
        Assert.Equal(0m, final.Totals.OutstandingAmountKrw);
    }

    [Fact]
    public async Task 아직받지않은원금의감액은미지급액만줄이고환급대금을만들지않는다()
    {
        await using var f = await Fixture.Create(); var r = await f.Activate(); r = await f.Accrue(r, 1);
        var entry = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single();
        r = await f.Confirm(r, entry.EntryId, "owner"); r = await f.Confirm(r, entry.EntryId, "picker");
        r = await f.Use().AdjustmentAsync(r.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginalEntryId = entry.EntryId, AmountKrw = -50, Reason = "미지급 감액" }, default);
        var adjustment = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single(x => x.AmountKrw < 0);
        r = await f.Confirm(r, adjustment.EntryId, "owner"); r = await f.Confirm(r, adjustment.EntryId, "picker");
        await Error("NeighborhoodSettlementRefundExceedsReceivedAmount", () => f.Use().ReportPaymentAsync(r.AgreementId, "picker", f.Report(r, adjustment.EntryId, 50), default));
        r = await f.Use().ReportPaymentAsync(r.AgreementId, "owner", f.Report(r, entry.EntryId, 50), default);
        Assert.Equal(50m, (await f.Use().GetAsync(r.AgreementId, "owner", default)).Totals.OutstandingAmountKrw);
    }

    [Fact]
    public async Task 저장길이경계와원본당하나의약정을검사한다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms(); terms.Lines[0].LineKey = new string('a', 65);
        await Error("NeighborhoodSettlementInputInvalid", () => f.Use().CreateAsync("owner", terms, default));
        terms = f.Terms(); terms.SourceId = new string('s', 121);
        await Error("NeighborhoodSettlementInputInvalid", () => f.Use().CreateAsync("owner", terms, default));
        await f.Use().CreateAsync("owner", f.Terms(), default);
        await Error("NeighborhoodSettlementDuplicateOrConcurrentCommand", () => f.Use().CreateAsync("owner", f.Terms(), default));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 실물보관구간이없거나전량출고뒤인기간은과거입고량이있어도청구하지못한다()
    {
        await using var f = await Fixture.Create();
        f.Resolver.Snapshot = f.Source(sourceKind: NeighborhoodSettlementCodes.DistributedLot);
        var terms = f.Terms();
        terms.Lines = [new() { LineKey = "period", Kind = NeighborhoodSettlementCodes.Storage, Billing = NeighborhoodSettlementCodes.StoragePeriod,
            AmountKrw = 100, PayerUserId = "owner", PayeeUserId = "space", BeneficiaryRole = "SpaceOwner",
            PeriodStartUtc = f.Clock.Now.AddHours(-2), PeriodEndUtc = f.Clock.Now.AddMinutes(-1) }];
        f.Resolver.Snapshot = f.Source(storageIntervals: [], sourceKind: NeighborhoodSettlementCodes.DistributedLot); var r = await f.Activate(terms);
        var request = new NeighborhoodSettlementAccrualRequest { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginKind = NeighborhoodSettlementCodes.DistributedLot, OriginId = "lot-1", Lines = [new() { LineKey = "period",
                PeriodStartUtc = terms.Lines[0].PeriodStartUtc, PeriodEndUtc = terms.Lines[0].PeriodEndUtc }] };
        await Error("NeighborhoodSettlementStoragePeriodEvidenceRequired", () => f.Use().AccrueAsync(r.AgreementId, "owner", request, default));
        f.Resolver.Snapshot = f.Source(storageIntervals: [new(f.Clock.Now.AddHours(-2), f.Clock.Now.AddHours(-1), 5)], sourceKind: NeighborhoodSettlementCodes.DistributedLot);
        await Error("NeighborhoodSettlementStoragePeriodEvidenceRequired", () => f.Use().AccrueAsync(r.AgreementId, "owner", request, default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 서로다른주문이같은정본피킹이력을참조해도처리량을두번청구하지못한다()
    {
        await using var f = await Fixture.Create(); var first = await f.Activate(); await f.Accrue(first, 4);
        f.Resolver.Snapshot = f.Source(sourceKind: NeighborhoodSettlementCodes.DistributedOrder, sourceId: "order-1");
        var second = await f.Activate();
        await Error("NeighborhoodSettlementCompletedQuantityExceeded", () => f.Accrue(second, 2));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 배송료에포함된피킹은명시적무료만허용하고양수유료항목을숨기지않는다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms();
        terms.Lines[0].IncludedInDelivery = true; terms.Lines[0].OriginalLineKey = "delivery";
        terms.Lines = [terms.Lines[0], new() { LineKey = "delivery", Kind = NeighborhoodSettlementCodes.Delivery,
            AmountKrw = 4000, PayerUserId = "buyer", PayeeUserId = "driver", BeneficiaryRole = "Driver" }];
        await Error("NeighborhoodSettlementIncludedWorkMustBeFree", () => f.Use().CreateAsync("owner", terms, default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 공동배송비는분담자모두동의한기존원장참고이며자기분담만조회한다()
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(delivery: true);
        var terms = f.DeliveryTerms(); var r = await f.Use().CreateAsync("owner", terms, default);
        foreach (var actor in new[] { "buyer", "driver" })
            r = await f.Use().ConsentAsync(r.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision, TermsVersion = 1, Granted = true }, default);
        await Error("NeighborhoodSettlementLatestConsentRequired", () => f.Accrue(r, 1, lineKey: "delivery"));
        r = await f.Use().ConsentAsync(r.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision, TermsVersion = 1, Granted = true }, default);
        r = await f.Accrue(r, 1, lineKey: "delivery");
        var owner = await f.Use().GetAsync(r.AgreementId, "owner", default);
        var entry = Assert.Single(owner.Entries); Assert.True(entry.IncludedInDelivery);
        Assert.Equal(4000m, entry.AmountKrw); Assert.Empty(entry.AllowedActions);
        var share = Assert.Single(Assert.Single(owner.Agreement.Terms.Lines).DeliveryCostShares);
        Assert.Equal("owner", share.PayerUserId); Assert.Equal(2000m, share.AmountKrw);
        var buyer = await f.Use().GetAsync(r.AgreementId, "buyer", default);
        Assert.Equal("buyer", Assert.Single(Assert.Single(buyer.Agreement.Terms.Lines).DeliveryCostShares).PayerUserId);
        Assert.Empty(Assert.Single((await f.Use().GetAsync(r.AgreementId, "driver", default)).Agreement.Terms.Lines).DeliveryCostShares);
        Assert.Equal(0m, owner.Totals.KnownAccruedAmountKrw); Assert.Empty(owner.Payments);
        await Error("NeighborhoodSettlementEntryNotConfirmable", () => f.Confirm(r, entry.EntryId, "buyer"));
        await Error("NeighborhoodSettlementEntryNotPayable", () => f.Use().ReportPaymentAsync(r.AgreementId, "buyer", f.Report(r, entry.EntryId, 4000), default));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 분담합계와중복지급자와원본에없는당사자를거부하고미확정분담은무료로채우지않는다()
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(delivery: true);
        var terms = f.DeliveryTerms(); terms.Lines[0].DeliveryCostShares[1].AmountKrw = 1900;
        await Error("NeighborhoodSettlementDeliveryShareTotalMismatch", () => f.Use().CreateAsync("owner", terms, default));
        terms = f.DeliveryTerms(); terms.Lines[0].DeliveryCostShares[1].PayerUserId = "buyer";
        await Error("NeighborhoodSettlementDeliverySharePayerDuplicate", () => f.Use().CreateAsync("owner", terms, default));
        terms = f.DeliveryTerms(); terms.Lines[0].DeliveryCostShares[1].PayerUserId = "outsider";
        await Error("NeighborhoodSettlementDeliverySharePartyMismatch", () => f.Use().CreateAsync("owner", terms, default));
        terms = f.DeliveryTerms(); terms.Lines[0].DeliveryCostShares[1].AmountKrw = null;
        var r = await f.Activate(terms);
        var unresolved = await f.Use().GetAsync(r.AgreementId, "owner", default);
        Assert.Null(Assert.Single(Assert.Single(unresolved.Agreement.Terms.Lines).DeliveryCostShares).AmountKrw);
        Assert.Equal(1, unresolved.Totals.UnresolvedLineCount);
        await Error("NeighborhoodSettlementAmountUnresolved", () => f.Accrue(r, 1, lineKey: "delivery"));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 무료보관은실제보관구간수량근거가있어야영원항목으로확정한다()
    {
        await using var f = await Fixture.Create();
        f.Resolver.Snapshot = f.Source(sourceKind: NeighborhoodSettlementCodes.DistributedLot);
        var terms = f.Terms();
        terms.Lines = [new() { LineKey = "free-storage", Kind = NeighborhoodSettlementCodes.Storage, Billing = NeighborhoodSettlementCodes.Free,
            AmountKrw = 0, Quantity = 3, PayerUserId = "owner", PayeeUserId = "space", BeneficiaryRole = "SpaceOwner" }];
        f.Resolver.Snapshot = f.Source(storageIntervals: [], sourceKind: NeighborhoodSettlementCodes.DistributedLot); var r = await f.Activate(terms);
        var request = new NeighborhoodSettlementAccrualRequest { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginKind = NeighborhoodSettlementCodes.DistributedLot, OriginId = "lot-1", Lines = [new() { LineKey = "free-storage", Quantity = 3 }] };
        await Error("NeighborhoodSettlementStoragePeriodEvidenceRequired", () => f.Use().AccrueAsync(r.AgreementId, "owner", request, default));
        f.Resolver.Snapshot = f.Source(storageIntervals: [new(f.Clock.Now.AddHours(-2), null, 2)], sourceKind: NeighborhoodSettlementCodes.DistributedLot);
        await Error("NeighborhoodSettlementStoragePeriodEvidenceRequired", () => f.Use().AccrueAsync(r.AgreementId, "owner", request, default));
        f.Resolver.Snapshot = f.Source(sourceKind: NeighborhoodSettlementCodes.DistributedLot); r = await f.Use().AccrueAsync(r.AgreementId, "owner", request, default);
        var entry = Assert.Single((await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries);
        Assert.Equal(0m, entry.AmountKrw); Assert.Equal(3m, entry.Quantity);
        r = await f.Confirm(r, entry.EntryId, "owner"); r = await f.Confirm(r, entry.EntryId, "space");
        Assert.Equal(NeighborhoodSettlementCodes.Confirmed, Assert.Single((await f.Use().GetAsync(r.AgreementId, "space", default)).Entries).StatusCode);
    }

    [Fact]
    public async Task 외부인에게원본가격일치여부를노출하지않고미지급조정의분쟁은허용한다()
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(delivery: true);
        var terms = f.DeliveryTerms(); terms.Lines[0].AmountKrw = 100;
        await Error("NeighborhoodSettlementPartyRequired", () => f.Use().CreateAsync("outsider", terms, default));
        f.Resolver.Snapshot = f.Source(); var r = await f.Activate(); r = await f.Accrue(r, 1);
        var entry = Assert.Single((await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries);
        r = await f.Use().AdjustmentAsync(r.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            OriginalEntryId = entry.EntryId, AmountKrw = -50, Reason = "미지급 감액 제안" }, default);
        var adjustment = (await f.Use().GetAsync(r.AgreementId, "owner", default)).Entries.Single(x => x.AmountKrw < 0);
        r = await f.Use().DisputeAsync(r.AgreementId, "picker", new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
            EntryId = adjustment.EntryId, Reason = "작업량 재확인 필요" }, default);
        Assert.Equal(NeighborhoodSettlementCodes.Disputed, (await f.Use().GetAsync(r.AgreementId, "picker", default)).Entries.Single(x => x.EntryId == adjustment.EntryId).StatusCode);
    }

    private static async Task Error(string code, Func<Task> call)
        => Assert.Equal(code, (await Assert.ThrowsAsync<NeighborhoodSettlementException>(call)).Code);
    private sealed class Clock : TimeProvider
    { public DateTime Now { get; set; } = new(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc); public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Resolver : INeighborhoodSettlementSourceResolver
    {
        public NeighborhoodSettlementSourceSnapshot Snapshot { get; set; } = new();
        public Exception? Failure { get; set; }
        public Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string kind, string id, CancellationToken ct)
            => Failure is null ? Task.FromResult(Snapshot) : Task.FromException<NeighborhoodSettlementSourceSnapshot>(Failure);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db; public Clock Clock { get; } = new(); public Resolver Resolver { get; } = new();
        public SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        public NeighborhoodSettlementUseCase Use(SsalddelContext? context = null) => Use(true, NeighborhoodSettlementOptions.RequiredProfile, SsalddelExecutionMode.Simulation, context);
        public NeighborhoodSettlementUseCase Use(bool enabled, string profile, SsalddelExecutionMode mode, SsalddelContext? context = null)
            => new(context ?? Db, Resolver, new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })),
                Options.Create(new NeighborhoodSettlementOptions { Enabled = enabled, Profile = profile }), Clock);
        public NeighborhoodSettlementSourceSnapshot Source(string digest = "source-v1", string? workActor = "picker", DateTime? storageStart = null,
            decimal? saleTotal = 1000, bool saleWork = false, DateTime? workTime = default, bool delivery = false, bool internalPicker = false,
            string sourceId = "lot-1", string originId = "pick-1", IReadOnlyList<NeighborhoodSettlementStorageInterval>? storageIntervals = null,
            string sourceKind = NeighborhoodSettlementCodes.DistributedOrder)
            => new()
            {
                SourceKind = sourceKind, SourceId = sourceId, SourceVersion = "1", SourceDigest = digest,
                AllowedActivityKinds = [NeighborhoodSettlementCodes.StoredGoods],
                Participants = [new("owner", ["Owner"]), new("seller", ["Seller"]), new("buyer", ["Buyer"]), new("space", ["SpaceOwner"]),
                    new("picker", internalPicker ? ["Picker", "Owner"] : ["Picker"]), new("receiver", ["Receiver"]), new("driver", ["Driver"])],
                ActualCompletedUnits = 5, HistoricalSaleTotalKrw = saleTotal, ObservedAtUtc = Clock.Now, StorageStartedAtUtc = storageStart ?? Clock.Now.AddHours(-2),
                StorageIntervals = storageIntervals ?? [new(storageStart ?? Clock.Now.AddHours(-2), null, 5)],
                StorageEvidenceReferences = ["actual-receipt"],
                CompletionEvidenceReferences = ["actual-receipt"], FrozenDriverFareKrw = delivery ? 4000m : null,
                RewardOutcome = sourceKind == NeighborhoodSettlementCodes.DistributedOrder ? new(5, ["delivered", "customer-received"]) : null,
                Workflow = new() { Kind = sourceKind == NeighborhoodSettlementCodes.DistributedLot
                    ? NeighborhoodSettlementWorkflowCodes.InboundStorage : NeighborhoodSettlementWorkflowCodes.OutboundDelivery,
                    StateCode = "Completed", CompletionConfirmed = true, PaymentReleaseReady = true, InventoryLotIds = ["lot-1"] },
                CanonicalDeliveryOrigin = delivery ? "cargo:original-delivery" : null, OriginalExternalPaymentStatus = delivery ? "CollectionPending" : null,
                CompletedWork = [new(delivery ? NeighborhoodSettlementCodes.Delivery : saleWork ? NeighborhoodSettlementCodes.ProductSale
                    : sourceKind == NeighborhoodSettlementCodes.DistributedLot ? NeighborhoodSettlementCodes.Inbound : NeighborhoodSettlementCodes.Picking,
                    "ProofReceipt", originId, saleWork ? 5m : delivery ? 1m : 5m, workTime ?? (saleWork ? null : Clock.Now.AddMinutes(-1)), ["actual-completion"], workActor,
                    GoodsOwnerUserId: internalPicker ? "picker" : "owner", SpaceOwnerUserId: "space", SellerUserId: "seller")]
            };
        public NeighborhoodSettlementAgreementRequest Terms() => new()
        {
            RequestId = Guid.NewGuid(), SourceKind = Resolver.Snapshot.SourceKind, SourceId = Resolver.Snapshot.SourceId,
            ActivityKind = NeighborhoodSettlementCodes.StoredGoods,
            Lines = [new() { LineKey = "pick", Kind = NeighborhoodSettlementCodes.Picking, Billing = NeighborhoodSettlementCodes.PerUnit,
                UnitPriceKrw = 100, Quantity = 5, PayerUserId = "owner", PayeeUserId = "picker", BeneficiaryRole = "Picker" }]
        };
        public NeighborhoodSettlementAgreementRequest DeliveryTerms()
        {
            var terms = Terms();
            terms.Lines = [new() { LineKey = "delivery", Kind = NeighborhoodSettlementCodes.Delivery, AmountKrw = 4000,
                PayerUserId = "buyer", PayeeUserId = "driver", BeneficiaryRole = "Driver",
                DeliveryCostShares = [new() { PayerUserId = "buyer", AmountKrw = 2000 }, new() { PayerUserId = "owner", AmountKrw = 2000 }] }];
            return terms;
        }
        public async Task<NeighborhoodSettlementReceiptDto> Activate(NeighborhoodSettlementAgreementRequest? terms = null, string actor = "owner")
        {
            terms ??= Terms(); terms.RequestId = Guid.NewGuid(); terms.ExpectedRevision = 0;
            var r = await Use().CreateAsync(actor, terms, default);
            foreach (var party in terms.Lines.SelectMany(x => new[] { x.PayerUserId, x.PayeeUserId }.Concat(x.DeliveryCostShares.Select(s => s.PayerUserId))).Distinct())
                r = await Use().ConsentAsync(r.AgreementId, party, new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision, TermsVersion = 1, Granted = true }, default);
            return r;
        }
        public async Task<NeighborhoodSettlementReceiptDto> Reconsent(NeighborhoodSettlementReceiptDto r, NeighborhoodSettlementAgreementRequest terms)
        {
            var version = (await Use().GetAsync(r.AgreementId, terms.Lines[0].PayerUserId, default)).Agreement.TermsVersion;
            foreach (var party in terms.Lines.SelectMany(x => new[] { x.PayerUserId, x.PayeeUserId }.Concat(x.DeliveryCostShares.Select(s => s.PayerUserId))).Distinct())
                r = await Use().ConsentAsync(r.AgreementId, party, new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision, TermsVersion = version, Granted = true }, default);
            return r;
        }
        public Task<NeighborhoodSettlementReceiptDto> Accrue(NeighborhoodSettlementReceiptDto r, decimal quantity, string actor = "owner", string lineKey = "pick", string originId = "pick-1")
            => Use().AccrueAsync(r.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision,
                OriginKind = "ProofReceipt", OriginId = originId, Lines = [new() { LineKey = lineKey, Quantity = quantity }] }, default);
        public Task<NeighborhoodSettlementReceiptDto> Confirm(NeighborhoodSettlementReceiptDto r, string entryId, string actor)
            => Use().ConfirmEntryAsync(r.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision, EntryId = entryId }, default);
        public NeighborhoodSettlementPaymentReportRequest Report(NeighborhoodSettlementReceiptDto r, string entryId, decimal amount)
            => new() { RequestId = Guid.NewGuid(), ExpectedRevision = r.Revision, AmountKrw = amount, EvidenceReference = "당사자 지급 진술",
                Allocations = [new() { EntryId = entryId, AmountKrw = amount }] };
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(connection, db); f.Resolver.Snapshot = f.Source(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
