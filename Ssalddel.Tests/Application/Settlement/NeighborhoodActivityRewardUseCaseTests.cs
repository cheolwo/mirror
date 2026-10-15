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

public sealed class NeighborhoodActivityRewardUseCaseTests
{
    [Fact]
    public async Task 최종수령과_역할별실적에_연결한_성과금은_기본보수와_별도로_발생하고_계산근거를_동결한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodActivityRewardCodes.Ready, preview.StatusCode);
        Assert.Equal(70m, preview.PoolAmountKrw);
        Assert.Equal(new[] { 53m, 17m }, preview.Allocations.Select(x => x.AmountKrw));
        Assert.False(preview.IsActualBankVerified); Assert.False(preview.AlreadyAccrued);
        Assert.False(string.IsNullOrWhiteSpace(preview.CalculationDigest));

        var accrued = await f.Accrue(receipt, preview);
        Assert.Equal(2, accrued.EntryIds.Count);
        Assert.NotNull(accrued.ActivityRewardCalculation);
        Assert.Equal(preview.CalculationDigest, accrued.ActivityRewardCalculation.CalculationDigest);
        var entries = (await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries;
        Assert.All(entries, entry =>
        {
            Assert.Equal(NeighborhoodSettlementCodes.ActivityReward, entry.Kind);
            Assert.Equal(preview.CalculationDigest, entry.ActivityRewardCalculationDigest);
            Assert.NotEmpty(entry.WorkEvidenceReferences);
            Assert.Equal(NeighborhoodSettlementCodes.Accrued, entry.StatusCode);
        });
        Assert.Equal(70m, entries.Sum(x => x.AmountKrw));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
        var again = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.True(again.AlreadyAccrued); Assert.Equal(preview.CalculationDigest, again.CalculationDigest);
    }

    [Fact]
    public async Task 기본보수당사자만_동의해도_성과금의_추가참여자_동의를_대신하지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(omitConsent: "picker-b");
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.NotEqual(NeighborhoodActivityRewardCodes.Ready, preview.StatusCode);
        Assert.Contains("ActivityRewardLatestConsentRequired", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 최종수령이_없어도_이미수행한_기본피킹보수는_발생하고_추가성과금만_대기한다()
    {
        await using var f = await Fixture.Create(); f.Resolver.Snapshot = f.Source(outcome: false);
        var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.NotEqual(NeighborhoodActivityRewardCodes.Ready, preview.StatusCode);
        Assert.Contains("ActivityRewardCustomerReceiptRequired", preview.ReasonCodes);
        var basic = await f.Use().AccrueAsync(receipt.AgreementId, "owner", new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
            OriginKind = "InventoryHistory", OriginId = "pick-a", Lines = [new() { LineKey = "basic-pick", Quantity = 1 }]
        }, default);
        var entry = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries);
        Assert.Equal(NeighborhoodSettlementCodes.Picking, entry.Kind); Assert.Equal(100m, entry.AmountKrw);
        Assert.Single(basic.EntryIds);
    }

    [Theory]
    [InlineData("OrderMissing")]
    [InlineData("WrongOrder")]
    [InlineData("RevisionMissing")]
    [InlineData("ActorMissing")]
    [InlineData("EvidenceMissing")]
    public async Task 미귀속이나_미검증_선행작업을_최종주문실적으로_추정하지_않는다(string defect)
    {
        await using var f = await Fixture.Create();
        var work = f.Work();
        work[0] = defect switch
        {
            "OrderMissing" => work[0] with { RelatedOrderId = null },
            "WrongOrder" => work[0] with { RelatedOrderId = "other-order" },
            "RevisionMissing" => work[0] with { OriginRevision = 0 },
            "ActorMissing" => work[0] with { ActorUserId = null },
            _ => work[0] with { EvidenceReferences = [] }
        };
        f.Resolver.Snapshot = f.Source(work: work); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.NotEqual(NeighborhoodActivityRewardCodes.Ready, preview.StatusCode);
        Assert.Contains("ActivityRewardOrderWorkEvidenceRequired", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 선택한_모든작업종류의_기여가_있어야_계산하고_없는작업을_자동제외하지_않는다()
    {
        await using var f = await Fixture.Create();
        f.Resolver.Snapshot = f.Source(work: f.Work().Where(x => x.Kind != NeighborhoodSettlementCodes.Inbound).ToArray());
        var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.NotEqual(NeighborhoodActivityRewardCodes.Ready, preview.StatusCode);
        Assert.Contains("ActivityRewardOrderWorkEvidenceRequired", preview.ReasonCodes);
    }

    [Theory]
    [InlineData("Inbound")]
    [InlineData("Picking")]
    public async Task 부분수령수량을_넘는_각단계의_선행실적을_성과금에_과다계상하지_않는다(string kind)
    {
        await using var f = await Fixture.Create(); var work = f.Work();
        var index = Array.FindIndex(work, item => item.Kind == kind);
        work[index] = work[index] with { Quantity = work[index].Quantity + 1m };
        f.Resolver.Snapshot = f.Source(work: work); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, preview.StatusCode);
        Assert.Contains("ActivityRewardCompletedQuantityExceeded", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 기여자의_조회에는_다른사람의_배분내역이_없다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var owner = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        var worker = await f.Use().GetAsync(receipt.AgreementId, "picker-a", default);
        Assert.NotNull(worker.ActivityReward);
        Assert.Single(worker.ActivityReward.Allocations);
        Assert.All(worker.ActivityReward.Allocations, x => Assert.Equal("picker-a", x.ActorUserId));
        Assert.All(worker.ActivityReward.ContributionShares, x => Assert.Equal("picker-a", x.Contribution.ActorUserId));
        Assert.Equal(owner.Allocations.Single(x => x.ActorUserId == "picker-a").AmountKrw,
            worker.ActivityReward.Allocations[0].AmountKrw);
        await Error("NeighborhoodSettlementNotFound", () => f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "outsider", default));
    }

    [Fact]
    public async Task 지급자만_발생시킬수있고_참여자가_다른사람의_채무를_만들지_못한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        await Error("ActivityRewardPayerRequired", () => f.Accrue(receipt, preview, actor: "picker-a"));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 같은요청의_응답유실복구는_동결영수증을_반환하고_항목을_늘리지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        var request = f.Request(receipt, preview);
        var first = await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", request, default);
        var replay = await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", request, default);
        Assert.True(replay.Replayed); Assert.Equal(first.EntryIds, replay.EntryIds);
        Assert.Equal(first.Revision, replay.Revision);
        Assert.Equal(preview.CalculationDigest, replay.ActivityRewardCalculation!.CalculationDigest);
        Assert.Equal(2, await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
        var recovered = await f.Use().ReceiptAsync("owner", request.RequestId, default);
        Assert.Equal(preview.CalculationDigest, recovered.ActivityRewardCalculation!.CalculationDigest);
    }

    [Fact]
    public async Task 같은요청ID의_내용을_바꾸면_원래계산의_복구로_취급하지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        var request = f.Request(receipt, preview);
        await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", request, default);
        request.ExpectedCalculationDigest = "different-calculation";
        await Error("NeighborhoodSettlementRequestContentConflict",
            () => f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", request, default));
        Assert.Equal(2, await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
    }

    [Fact]
    public async Task 발생한_성과금은_이후_원천과_완료상태가_변해도_동결계산을_보존한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        await f.Accrue(receipt, preview);
        f.Resolver.Snapshot = f.Source(outcome: false, digest: "later-source-v2");
        var frozen = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.True(frozen.AlreadyAccrued); Assert.Equal(NeighborhoodActivityRewardCodes.Ready, frozen.StatusCode);
        Assert.Equal(preview.CalculationDigest, frozen.CalculationDigest);
        Assert.Equal(preview.PoolAmountKrw, frozen.PoolAmountKrw);
        Assert.Equal(preview.Allocations.Select(x => x.AmountKrw), frozen.Allocations.Select(x => x.AmountKrw));
    }

    [Fact]
    public async Task 새약정의_지급자에게_과거다른지급자의_성과금을_공개하지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        receipt = await f.Accrue(receipt, preview);
        var terms = f.Terms(); terms.RequestId = Guid.NewGuid(); terms.ExpectedRevision = receipt.Revision;
        terms.ActivityRewardTerms!.PayerUserId = "seller";
        await f.Use().TermsAsync(receipt.AgreementId, "owner", terms, default);
        await Error("ActivityRewardNotFound",
            () => f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "seller", default));
        Assert.Null((await f.Use().GetAsync(receipt.AgreementId, "seller", default)).ActivityReward);
        var originalPayer = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.True(originalPayer.AlreadyAccrued); Assert.Equal("owner", originalPayer.FundingPayerUserId);
        Assert.Equal(preview.CalculationDigest, originalPayer.CalculationDigest);
        Assert.Equal(preview.Allocations.Select(x => x.AmountKrw), originalPayer.Allocations.Select(x => x.AmountKrw));
    }

    [Fact]
    public async Task 같은주문에_새약정과_새정책을_만들어도_성과금을_두번_발생하지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        receipt = await f.Accrue(receipt, preview);
        var terms = f.Terms(); terms.ActivityRewardTerms!.Policy.PolicyVersion = "agreed-v2";
        await Error("NeighborhoodSettlementDuplicateOrConcurrentCommand", () => f.Use().CreateAsync("owner", terms, default));
        terms.RequestId = Guid.NewGuid(); terms.ExpectedRevision = receipt.Revision;
        receipt = await f.Use().TermsAsync(receipt.AgreementId, "owner", terms, default);
        foreach (var actor in new[] { "owner", "picker-a", "picker-b" })
            receipt = await f.Use().ConsentAsync(receipt.AgreementId, actor, new()
            {
                RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, TermsVersion = 2, Granted = true
            }, default);
        var secondPreview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.True(secondPreview.AlreadyAccrued); Assert.Equal(preview.CalculationDigest, secondPreview.CalculationDigest);
        await Error("ActivityRewardAlreadyAccrued", () => f.Accrue(receipt, secondPreview));
        Assert.Equal(2, await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
    }

    [Fact]
    public async Task 미리보기후_수행기여가_바뀌면_오래된_계산지문으로_발생하지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        var work = f.Work(); work[1] = work[1] with { OriginRevision = 2 };
        f.Resolver.Snapshot = f.Source(work: work);
        await Error("ActivityRewardCalculationChanged", () => f.Accrue(receipt, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 원천약정근거가_바뀌면_이전판본_성과금을_발생하지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        f.Resolver.Snapshot = f.Source(digest: "changed-price-or-parties");
        await Error("NeighborhoodSettlementSourceChangedReconfirmationRequired", () => f.Accrue(receipt, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 동의철회후에는_기존미리보기로_성과금을_발생하지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        receipt = await f.Use().ConsentAsync(receipt.AgreementId, "picker-b", new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, TermsVersion = 1, Granted = false
        }, default);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 기간이_만료되면_과거미리보기로_발생하지_않는다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms(); terms.ExpiresAtUtc = f.Clock.Now.AddMinutes(1);
        var receipt = await f.Activate(terms);
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        f.Clock.Now = f.Clock.Now.AddMinutes(2);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 미확인_경제입력은_0원으로_바꾸어_발생하지_않는다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms();
        terms.ActivityRewardTerms!.FinancialBasis.RevenueAmountKrw = null;
        var receipt = await f.Activate(terms);
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.NotEqual(NeighborhoodActivityRewardCodes.Ready, preview.StatusCode); Assert.Null(preview.PoolAmountKrw);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
    }

    [Fact]
    public async Task 합의된_기본작업비보다_적은비용을_신고해_성과금을_부풀리지_않는다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms();
        terms.ActivityRewardTerms!.FinancialBasis.BasicWorkCostAmountKrw = 99;
        var receipt = await f.Activate(terms);
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, preview.StatusCode);
        Assert.Contains("ActivityRewardBasicWorkCostUnderstated", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
    }

    [Fact]
    public async Task 새판본의_미래보수를_무료로_바꾸어도_과거에_발생한_기본보수는_비용에서_사라지지_않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        receipt = await f.AccrueBasic(receipt);
        var terms = f.Terms(); var basic = terms.Lines[0];
        basic.Billing = NeighborhoodSettlementCodes.Free; basic.UnitPriceKrw = null; basic.AmountKrw = 0;
        terms.ActivityRewardTerms!.FinancialBasis.BasicWorkCostAmountKrw = 0;
        receipt = await f.Revise(receipt, terms);
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, preview.StatusCode);
        Assert.Contains("ActivityRewardBasicWorkCostUnderstated", preview.ReasonCodes);
        Assert.Equal(100m, Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries).AmountKrw);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
    }

    [Fact]
    public async Task 기본보수의_양측확정_증액은_성과금_비용하한에_반영한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); receipt = await f.AccrueBasic(receipt);
        var original = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries);
        receipt = await f.Use().AdjustmentAsync(receipt.AgreementId, "owner", new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, OriginalEntryId = original.EntryId,
            AmountKrw = 100, Reason = "확인한 추가 피킹 보수"
        }, default);
        var adjustment = (await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries.Single(x => x.OriginalEntryId == original.EntryId);
        foreach (var actor in new[] { "owner", "picker-a" })
            receipt = await f.Use().ConfirmEntryAsync(receipt.AgreementId, actor, new()
            {
                RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, EntryId = adjustment.EntryId
            }, default);
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, preview.StatusCode);
        Assert.Contains("ActivityRewardBasicWorkCostUnderstated", preview.ReasonCodes);
        Assert.Equal(200m, (await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries.Sum(x => x.AmountKrw));
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
    }

    [Theory]
    [InlineData("DisputedOriginal")]
    [InlineData("UnconfirmedAdjustment")]
    public async Task 기본보수_분쟁과_미확정조정을_무시하고_성과금만_먼저_발생하지_않는다(string situation)
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); receipt = await f.AccrueBasic(receipt);
        var original = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries);
        if (situation == "DisputedOriginal")
            receipt = await f.Use().DisputeAsync(receipt.AgreementId, "picker-a", new()
            {
                RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, EntryId = original.EntryId, Reason = "처리량 대조 중"
            }, default);
        else
            receipt = await f.Use().AdjustmentAsync(receipt.AgreementId, "owner", new()
            {
                RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, OriginalEntryId = original.EntryId,
                AmountKrw = 50, Reason = "추가 보수 확인 대기"
            }, default);
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, preview.StatusCode);
        Assert.Contains("ActivityRewardBasicWorkCostDisputed", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
    }

    [Fact]
    public async Task 새근거와_구형미귀속_피킹이_섞이면_확인된사람에게_전체성과금을_몰아주지_않는다()
    {
        await using var f = await Fixture.Create(); var work = f.Work();
        work[2] = work[2] with { OriginRevision = 0, RelatedOrderId = null };
        f.Resolver.Snapshot = f.Source(work: work);
        var terms = f.Terms(); terms.ActivityRewardTerms!.ParticipantUserIds = ["picker-a"];
        var receipt = await f.Activate(terms);
        var preview = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, preview.StatusCode);
        Assert.Contains("ActivityRewardCompletedQuantityIncomplete", preview.ReasonCodes);
        Assert.Empty(preview.Allocations);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(receipt, preview));
    }

    [Fact]
    public async Task 최종수령_근거의_순서와_중복표현만_바뀌면_같은_계산지문을_유지한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var first = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        f.Resolver.Snapshot = f.Source(outcomeEvidence: ["delivered:order-1", "received:order-1", "received:order-1"]);
        var reordered = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(NeighborhoodActivityRewardCodes.Ready, reordered.StatusCode);
        Assert.Equal(first.CalculationDigest, reordered.CalculationDigest);
        Assert.Equal(first.Allocations.Select(x => x.AmountKrw), reordered.Allocations.Select(x => x.AmountKrw));
        var accrued = await f.Accrue(receipt, first);
        Assert.Equal(2, accrued.EntryIds.Count);
    }

    [Theory]
    [InlineData(false, "neighborhood-settlement-r38", SsalddelExecutionMode.Simulation)]
    [InlineData(true, "wrong-profile", SsalddelExecutionMode.Simulation)]
    [InlineData(true, "neighborhood-settlement-r38", SsalddelExecutionMode.Operational)]
    public async Task 기본OFF와_잘못된profile과_Operational에서는_성과금_계산과_발생이_차단된다(
        bool enabled, string profile, SsalddelExecutionMode mode)
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate();
        var useCase = f.Use(enabled, profile, mode);
        await Error("NeighborhoodSettlementDisabled", () => useCase.ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default));
        await Error("NeighborhoodSettlementDisabled", () => useCase.AccrueActivityRewardAsync(receipt.AgreementId, "owner",
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, ExpectedCalculationDigest = "old" }, default));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
    }

    private static async Task Error(string code, Func<Task> call)
        => Assert.Equal(code, (await Assert.ThrowsAsync<NeighborhoodSettlementException>(call)).Code);

    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Resolver : INeighborhoodSettlementSourceResolver
    {
        public NeighborhoodSettlementSourceSnapshot Snapshot { get; set; } = new();
        public Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string kind, string id, CancellationToken ct)
            => Task.FromResult(Snapshot);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public Clock Clock { get; } = new();
        public Resolver Resolver { get; } = new();
        public NeighborhoodSettlementUseCase Use() => Use(true, NeighborhoodSettlementOptions.RequiredProfile, SsalddelExecutionMode.Simulation);
        public NeighborhoodSettlementUseCase Use(bool enabled, string profile, SsalddelExecutionMode mode)
            => new(Db, Resolver, new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })),
                Options.Create(new NeighborhoodSettlementOptions { Enabled = enabled, Profile = profile }), Clock);

        public NeighborhoodSettlementCompletedWork[] Work() =>
        [
            new(NeighborhoodSettlementCodes.Inbound, "InventoryHistory", "receive-a", 2m, Clock.Now.AddHours(-2), ["receive-proof"],
                "picker-a", "owner", "space", "seller", 1, "order-1"),
            new(NeighborhoodSettlementCodes.Picking, "InventoryHistory", "pick-a", 1m, Clock.Now.AddMinutes(-2), ["pick-a-proof"],
                "picker-a", "owner", "space", "seller", 1, "order-1"),
            new(NeighborhoodSettlementCodes.Picking, "InventoryHistory", "pick-b", 1m, Clock.Now.AddMinutes(-1), ["pick-b-proof"],
                "picker-b", "owner", "space", "seller", 1, "order-1")
        ];

        public NeighborhoodSettlementSourceSnapshot Source(bool outcome = true,
            IReadOnlyList<NeighborhoodSettlementCompletedWork>? work = null, string digest = "terms-source-v1",
            IReadOnlyList<string>? outcomeEvidence = null) => new()
        {
            SourceKind = NeighborhoodSettlementCodes.DistributedOrder, SourceId = "order-1", SourceVersion = "1", SourceDigest = digest,
            AllowedActivityKinds = [NeighborhoodSettlementCodes.StoredGoods],
            Participants = [new("owner", ["Owner"]), new("seller", ["Seller"]), new("buyer", ["Buyer"]),
                new("space", ["SpaceOwner"]), new("picker-a", ["Receiver", "Picker"]), new("picker-b", ["Picker"])],
            ActualCompletedUnits = 2, HistoricalSaleTotalKrw = 1000, ObservedAtUtc = Clock.Now,
            CompletedWork = work ?? Work(), CompletionEvidenceReferences = ["delivery-proof"],
            RewardOutcome = outcome ? new(2m, outcomeEvidence ?? ["received:order-1", "delivered:order-1"]) : null
        };

        public NeighborhoodSettlementAgreementRequest Terms() => new()
        {
            RequestId = Guid.NewGuid(), SourceKind = NeighborhoodSettlementCodes.DistributedOrder, SourceId = "order-1",
            ActivityKind = NeighborhoodSettlementCodes.StoredGoods,
            Lines = [new() { LineKey = "basic-pick", Kind = NeighborhoodSettlementCodes.Picking,
                Billing = NeighborhoodSettlementCodes.PerUnit, UnitPriceKrw = 100, Quantity = 1,
                PayerUserId = "owner", PayeeUserId = "picker-a", BeneficiaryRole = "Picker" }],
            ActivityRewardTerms = new()
            {
                PayerUserId = "owner", ParticipantUserIds = ["picker-a", "picker-b"],
                Policy = new() { PolicyVersion = "agreed-v1", ReturnRatePercent = 10,
                    Weights = [new() { Kind = NeighborhoodSettlementCodes.Inbound, UnitCode = NeighborhoodActivityRewardCodes.Unit, Weight = 1 },
                        new() { Kind = NeighborhoodSettlementCodes.Picking, UnitCode = NeighborhoodActivityRewardCodes.Unit, Weight = 1 }] },
                FinancialBasis = new()
                {
                    BasisKind = NeighborhoodActivityRewardCodes.SimulationPartyStatement, BasisReference = "agreed-financial-v1",
                    RevenueAmountKrw = 1000, BasicWorkCostAmountKrw = 100, OtherCostAmountKrw = 100, ReserveAmountKrw = 100
                }
            }
        };

        public async Task<NeighborhoodSettlementReceiptDto> Activate(NeighborhoodSettlementAgreementRequest? terms = null, string? omitConsent = null)
        {
            terms ??= Terms(); terms.RequestId = Guid.NewGuid(); terms.ExpectedRevision = 0;
            var receipt = await Use().CreateAsync("owner", terms, default);
            foreach (var actor in ConsentActors(terms).Where(x => x != omitConsent))
                receipt = await Use().ConsentAsync(receipt.AgreementId, actor, new()
                {
                    RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, TermsVersion = 1, Granted = true
                }, default);
            return receipt;
        }

        private static IEnumerable<string> ConsentActors(NeighborhoodSettlementAgreementRequest terms)
            => terms.Lines.SelectMany(x => new[] { x.PayerUserId, x.PayeeUserId })
                .Concat(terms.ActivityRewardTerms!.ParticipantUserIds.Append(terms.ActivityRewardTerms.PayerUserId))
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
        public async Task<NeighborhoodSettlementReceiptDto> Revise(NeighborhoodSettlementReceiptDto receipt, NeighborhoodSettlementAgreementRequest terms)
        {
            terms.RequestId = Guid.NewGuid(); terms.ExpectedRevision = receipt.Revision;
            receipt = await Use().TermsAsync(receipt.AgreementId, "owner", terms, default);
            foreach (var actor in ConsentActors(terms))
                receipt = await Use().ConsentAsync(receipt.AgreementId, actor, new()
                {
                    RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, TermsVersion = 2, Granted = true
                }, default);
            return receipt;
        }
        public Task<NeighborhoodSettlementReceiptDto> AccrueBasic(NeighborhoodSettlementReceiptDto receipt)
            => Use().AccrueAsync(receipt.AgreementId, "owner", new()
            {
                RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, OriginKind = "InventoryHistory", OriginId = "pick-a",
                Lines = [new() { LineKey = "basic-pick", Quantity = 1 }]
            }, default);

        public NeighborhoodSettlementActivityRewardAccrualRequest Request(NeighborhoodSettlementReceiptDto receipt, NeighborhoodActivityRewardPreviewDto preview)
            => new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, ExpectedCalculationDigest = preview.CalculationDigest };
        public Task<NeighborhoodSettlementReceiptDto> Accrue(NeighborhoodSettlementReceiptDto receipt, NeighborhoodActivityRewardPreviewDto preview, string actor = "owner")
            => Use().AccrueActivityRewardAsync(receipt.AgreementId, actor, Request(receipt, preview), default);
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); var fixture = new Fixture(connection, db);
            fixture.Resolver.Snapshot = fixture.Source(); return fixture;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
