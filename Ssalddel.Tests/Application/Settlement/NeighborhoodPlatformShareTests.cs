using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.PlatformProfit;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Settlement;
using 살뜰.도메인.정산;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using R = Ssalddel.Contracts.Common.Settlement.NeighborhoodActivityRewardCodes;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class NeighborhoodPlatformShareTests
{
    [Theory]
    [InlineData(5, 9405, 496)]
    [InlineData(10, 8910, 991)]
    public async Task 플랫폼몫은_기본보수와구별하고_정수성과금과합해_잔여금을보존한다(
        int retention, int pool, int retained)
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(f.Terms(retention));
        receipt = await f.AccrueBasic(receipt);
        var preview = await f.Preview(receipt);
        Assert.Equal(R.Ready, preview.StatusCode); Assert.Equal(9901m, preview.SurplusAmountKrw);
        Assert.Equal(pool, preview.PoolAmountKrw);
        var request = f.Request(receipt, preview);
        var accrued = await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", request, default);
        Assert.Equal(2, accrued.EntryIds.Count);
        var summary = await f.Use().GetAsync(receipt.AgreementId, "owner", default);
        Assert.Equal(100m, summary.Entries.Single(x => x.Kind == C.Picking).AmountKrw);
        var platform = summary.Entries.Single(x => x.Kind == "PlatformShare");
        Assert.Equal(retained, platform.AmountKrw); Assert.Equal("platform", platform.PayeeUserId);
        Assert.Equal("owner", platform.PayerUserId); Assert.Equal("Platform", platform.BeneficiaryRole);
        Assert.Equal(preview.CalculationDigest, platform.ActivityRewardCalculationDigest);
        Assert.NotEmpty(platform.WorkEvidenceReferences);
        var auditBefore = (await f.Db.Set<NeighborhoodSettlementEntryRecord>().SingleAsync(x => x.EntryId == platform.EntryId)).DetailsJson;
        var platformView = await f.Use().GetAsync(receipt.AgreementId, "platform", default);
        var payeeEntry = Assert.Single(platformView.Entries);
        Assert.Equal(platform.EntryId, payeeEntry.EntryId); Assert.Equal(platform.AmountKrw, payeeEntry.AmountKrw);
        Assert.Equal(platform.ActivityRewardCalculationDigest, payeeEntry.ActivityRewardCalculationDigest);
        Assert.Equal(C.DistributedOrder, payeeEntry.OriginKind); Assert.Equal("order-1", payeeEntry.OriginId);
        Assert.Empty(payeeEntry.WorkEvidenceReferences); Assert.Contains("confirm-entry", payeeEntry.AllowedActions);
        Assert.Empty(platformView.ActivityReward!.ContributionShares);
        Assert.Equal(auditBefore, (await f.Db.Set<NeighborhoodSettlementEntryRecord>().SingleAsync(x => x.EntryId == platform.EntryId)).DetailsJson);
        Assert.NotEmpty((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries
            .Single(x => x.Kind == "PlatformShare").WorkEvidenceReferences);
        Assert.Equal(9901m, summary.Entries.Where(x => x.Kind is C.ActivityReward or "PlatformShare").Sum(x => x.AmountKrw));
        var revenue = await f.Db.PlatformRevenueEntries.SingleAsync();
        Assert.Equal("NeighborhoodRetainedSurplus", revenue.RevenueSource);
        Assert.Equal(C.DistributedOrder, revenue.SourceReferenceType); Assert.Equal("order-1", revenue.SourceReferenceId);
        Assert.Equal("owner", revenue.PayerUserId); Assert.Equal("platform", revenue.RelatedParticipantUserId);
        Assert.Equal(9901m, revenue.GrossAmount); Assert.Equal(retained, revenue.PlatformRevenueAmount);
        Assert.Contains("Simulation", revenue.Memo); Assert.False(preview.IsActualBankVerified);
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
        var replay = await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", request, default);
        Assert.True(replay.Replayed); Assert.Equal(accrued.EntryIds, replay.EntryIds);
        Assert.Single(await f.Db.PlatformRevenueEntries.ToArrayAsync());
        Assert.Equal(5, await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
    }

    [Theory]
    [InlineData("Unconfigured", "PlatformShareConfiguredPayeeRequired")]
    [InlineData("WrongConfiguredPayee", "PlatformShareConfiguredPayeeRequired")]
    [InlineData("UnknownPayee", "PlatformShareAdministratorRequired")]
    [InlineData("NoAdministrator", "PlatformShareAdministratorRequired")]
    [InlineData("NoRetention", "PlatformShareRetentionRateInvalid")]
    [InlineData("UnsupportedRetention", "PlatformShareRetentionRateInvalid")]
    [InlineData("WrongBasis", "PlatformShareBasisInvalid")]
    [InlineData("WrongReturnRate", "PlatformShareReturnRateMismatch")]
    public async Task 플랫폼약정은_설정된기존관리자와_명시비율_기준을요구한다(string condition, string code)
    {
        await using var f = await Fixture.Create(); var terms = f.Terms();
        switch (condition)
        {
            case "Unconfigured": f.Settings.PlatformPayeeUserId = ""; break;
            case "WrongConfiguredPayee": f.Settings.PlatformPayeeUserId = "other"; break;
            case "UnknownPayee": terms.ActivityRewardTerms!.Economics!.PlatformPayeeUserId = "unknown"; f.Settings.PlatformPayeeUserId = "unknown"; break;
            case "NoAdministrator": f.Db.UserRoles.RemoveRange(f.Db.UserRoles); await f.Db.SaveChangesAsync(); break;
            case "NoRetention": terms.ActivityRewardTerms!.Economics!.PlatformRetentionRatePercent = null; break;
            case "UnsupportedRetention": terms.ActivityRewardTerms!.Economics!.PlatformRetentionRatePercent = 7m; break;
            case "WrongBasis": terms.ActivityRewardTerms!.Economics!.BasisCode = "GrossSales"; break;
            case "WrongReturnRate": terms.ActivityRewardTerms!.Policy.ReturnRatePercent = 50m; break;
        }
        await Error(code, () => f.Use().CreateAsync("owner", terms, default));
        Assert.Single(await f.Db.Set<NeighborhoodSettlementAgreementRecord>().ToArrayAsync());
        Assert.Empty(await f.Db.PlatformRevenueEntries.ToArrayAsync());
    }

    [Fact]
    public async Task 플랫폼주체의_약정동의도필수이며_업무당사자로자동추가하지않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(omitPlatform: true);
        var preview = await f.Preview(receipt);
        Assert.Contains("ActivityRewardLatestConsentRequired", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Use().AccrueActivityRewardAsync(receipt.AgreementId,
            "owner", f.Request(receipt, preview), default));
        Assert.DoesNotContain(f.Resolver.Snapshot.Participants, x => x.UserId == "platform");
        var outsider = f.Use().GetAsync(receipt.AgreementId, "stranger", default);
        await Error("NeighborhoodSettlementNotFound", () => outsider);
        receipt = await f.Consent(receipt, "platform"); preview = await f.Preview(receipt);
        Assert.Equal(R.Ready, preview.StatusCode);
        Assert.Empty(await f.Db.PlatformRevenueEntries.ToArrayAsync());
    }

    [Fact]
    public async Task 발생직전_관리자역할철회는_플랫폼몫과참여자성과금을_모두남기지않는다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); var preview = await f.Preview(receipt);
        Assert.Equal(R.Ready, preview.StatusCode);
        f.Db.UserRoles.RemoveRange(f.Db.UserRoles); await f.Db.SaveChangesAsync();
        await Error("PlatformShareAdministratorRequired", () => f.Use().AccrueActivityRewardAsync(receipt.AgreementId,
            "owner", f.Request(receipt, preview), default));
        Assert.Equal(2, await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
        Assert.Empty(await f.Db.PlatformRevenueEntries.ToArrayAsync());
        Assert.Equal(receipt.Revision, (await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Agreement.Revision);
    }

    [Fact]
    public async Task 같은원주문의_새약정판본이나정책으로_플랫폼몫을다시발생하지않는다()
    {
        await using var f = await Fixture.Create(); var first = await f.Activate(); var firstPreview = await f.Preview(first);
        first = await f.Use().AccrueActivityRewardAsync(first.AgreementId, "owner", f.Request(first, firstPreview), default);
        var revised = f.Terms(5); revised.ExpectedRevision = first.Revision;
        var second = await f.Use().TermsAsync(first.AgreementId, "owner", revised, default);
        foreach (var actor in new[] { "owner", "worker", "platform" }) second = await f.Consent(second, actor, 2);
        var secondPreview = await f.Preview(second);
        Assert.True(secondPreview.AlreadyAccrued);
        Assert.Equal(firstPreview.CalculationDigest, secondPreview.CalculationDigest);
        await Error("ActivityRewardAlreadyAccrued", () => f.Use().AccrueActivityRewardAsync(second.AgreementId,
            "owner", f.Request(second, secondPreview), default));
        Assert.Single(await f.Db.PlatformRevenueEntries.ToArrayAsync());
        var entries = await f.Db.Set<NeighborhoodSettlementEntryRecord>().Where(x => x.AgreementId == second.AgreementId).ToArrayAsync();
        Assert.Equal(2, entries.Length); Assert.All(entries, x => Assert.Equal(1, x.TermsVersion));
    }

    [Fact]
    public async Task 동결성과금은_최신약정의참여자교체나제거와별도로_당시당사자에게만보인다()
    {
        await using var f = await Fixture.Create(includeReplacementParties: true);
        var receipt = await f.Activate(); var preview = await f.Preview(receipt);
        receipt = await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", f.Request(receipt, preview), default);
        f.Db.Users.Add(new ApplicationUser { Id = "platform-next", UserName = "platform-next" });
        f.Db.UserRoles.Add(new IdentityUserRole<string> { UserId = "platform-next", RoleId = "admin-role" });
        await f.Db.SaveChangesAsync(); f.Settings.PlatformPayeeUserId = "platform-next";
        var revised = f.Terms(5); revised.ExpectedRevision = receipt.Revision;
        revised.Lines[0].PayerUserId = "owner-next"; revised.Lines[0].PayeeUserId = "worker-next";
        revised.ActivityRewardTerms!.PayerUserId = "owner-next";
        revised.ActivityRewardTerms.ParticipantUserIds = ["worker-next"];
        revised.ActivityRewardTerms.Economics!.PlatformPayeeUserId = "platform-next";
        revised.ActivityRewardTerms.FinancialBasis.RevenueAmountKrw = 20001;
        receipt = await f.Use().TermsAsync(receipt.AgreementId, "owner", revised, default);

        foreach (var actor in new[] { "owner-next", "worker-next", "platform-next" })
        {
            await Error("ActivityRewardNotFound", () => f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, actor, default));
            var current = await f.Use().GetAsync(receipt.AgreementId, actor, default);
            Assert.Null(current.ActivityReward); Assert.Empty(current.Entries);
            Assert.Equal(20001m, current.Agreement.Terms.ActivityRewardTerms!.FinancialBasis.RevenueAmountKrw);
            Assert.Contains("consent", current.Agreement.AllowedActions);
        }
        foreach (var actor in new[] { "owner", "worker", "platform" })
        {
            var past = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, actor, default);
            Assert.True(past.AlreadyAccrued); Assert.Equal(preview.CalculationDigest, past.CalculationDigest);
            Assert.Equal(10m, past.PlatformRetentionRatePercent); Assert.Equal("platform", past.PlatformPayeeUserId);
            var summary = await f.Use().GetAsync(receipt.AgreementId, actor, default);
            Assert.Equal(preview.CalculationDigest, summary.ActivityReward!.CalculationDigest);
            Assert.Null(summary.Agreement.Terms.ActivityRewardTerms);
            Assert.DoesNotContain("consent", summary.Agreement.AllowedActions);
            if (actor == "platform")
            {
                var entry = Assert.Single(summary.Entries);
                Assert.Equal("PlatformShare", entry.Kind); Assert.Empty(entry.WorkEvidenceReferences);
                Assert.Equal(preview.CalculationDigest, entry.ActivityRewardCalculationDigest);
                Assert.Equal(preview.PlatformRetentionAmountKrw, entry.AmountKrw);
            }
            if (actor == "owner") Assert.Equal(2, past.CostAttributions.Count);
            else
            {
                Assert.Empty(past.CostAttributions);
                Assert.All(past.Allocations, x => Assert.Equal(actor, x.ActorUserId));
                Assert.All(past.ContributionShares, x => Assert.Equal(actor, x.Contribution.ActorUserId));
            }
        }

        var withoutReward = f.Terms(); withoutReward.ExpectedRevision = receipt.Revision;
        withoutReward.ActivityRewardTerms = null;
        withoutReward.Lines[0].PayerUserId = "owner-next"; withoutReward.Lines[0].PayeeUserId = "worker-next";
        receipt = await f.Use().TermsAsync(receipt.AgreementId, "owner", withoutReward, default);
        foreach (var actor in new[] { "owner", "worker", "platform" })
        {
            var past = await f.Use().ActivityRewardPreviewAsync(receipt.AgreementId, actor, default);
            Assert.Equal(preview.CalculationDigest, past.CalculationDigest);
            var summary = await f.Use().GetAsync(receipt.AgreementId, actor, default);
            Assert.Null(summary.Agreement.Terms.ActivityRewardTerms);
            Assert.Equal(preview.CalculationDigest, summary.ActivityReward!.CalculationDigest);
            if (actor == "platform") Assert.Empty(Assert.Single(summary.Entries).WorkEvidenceReferences);
        }
        Assert.Null((await f.Use().GetAsync(receipt.AgreementId, "platform-next", default)).ActivityReward);
        Assert.Single(await f.Db.PlatformRevenueEntries.ToArrayAsync());
        Assert.Equal(4, await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
    }

    [Fact]
    public async Task 플랫폼과자금부담자가같으면_본인부담계산의감사증빙을유지한다()
    {
        await using var f = await Fixture.Create();
        f.Db.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner" });
        f.Db.UserRoles.Add(new IdentityUserRole<string> { UserId = "owner", RoleId = "admin-role" });
        await f.Db.SaveChangesAsync(); f.Settings.PlatformPayeeUserId = "owner";
        var terms = f.Terms(); terms.ActivityRewardTerms!.Economics!.PlatformPayeeUserId = "owner";
        var receipt = await f.Use().CreateAsync("owner", terms, default);
        foreach (var actor in new[] { "owner", "worker" }) receipt = await f.Consent(receipt, actor);
        var preview = await f.Preview(receipt); Assert.Equal(R.Ready, preview.StatusCode);
        receipt = await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", f.Request(receipt, preview), default);
        var own = await f.Use().GetAsync(receipt.AgreementId, "owner", default);
        var entry = own.Entries.Single(x => x.Kind == "PlatformShare");
        Assert.Equal("owner", entry.PayerUserId); Assert.Equal("owner", entry.PayeeUserId); Assert.True(entry.IsInternal);
        Assert.Equal(991m, entry.AmountKrw); Assert.Equal(preview.CalculationDigest, entry.ActivityRewardCalculationDigest);
        Assert.Contains("pick-photo", entry.WorkEvidenceReferences);
        Assert.NotEmpty(own.ActivityReward!.ContributionShares);
        var original = (await f.Db.Set<NeighborhoodSettlementEntryRecord>().SingleAsync(x => x.EntryId == entry.EntryId)).DetailsJson;
        Assert.Contains("pick-photo", original);
        var worker = await f.Use().GetAsync(receipt.AgreementId, "worker", default);
        Assert.DoesNotContain(worker.Entries, x => x.Kind == "PlatformShare");
        Assert.Contains("pick-photo", Assert.Single(worker.Entries).WorkEvidenceReferences);
        Assert.Equal(original, (await f.Db.Set<NeighborhoodSettlementEntryRecord>().SingleAsync(x => x.EntryId == entry.EntryId)).DetailsJson);
    }

    [Fact]
    public async Task 기존약정은_플랫폼몫을자동생성하거나_기존반환율을변경하지않는다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms(); terms.ActivityRewardTerms!.Economics = null;
        terms.ActivityRewardTerms!.Policy.ReturnRatePercent = 10m;
        var receipt = await f.Activate(terms); var preview = await f.Preview(receipt);
        Assert.Equal(990m, preview.PoolAmountKrw);
        var accrued = await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", f.Request(receipt, preview), default);
        Assert.Single(accrued.EntryIds); Assert.Empty(await f.Db.PlatformRevenueEntries.ToArrayAsync());
        Assert.DoesNotContain((await f.Use().GetAsync(receipt.AgreementId, "owner", default)).Entries, x => x.Kind == "PlatformShare");
    }

    [Theory]
    [InlineData("NeighborhoodRetainedSurplus")]
    [InlineData(" neighborhoodretainedsurplus ")]
    public async Task 수동플랫폼수익API는_주문잔여금전용수익원을가장할수없다(string source)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => new PlatformProfitReturnService(f.Db).RecordRevenueAsync(new()
        { RevenueSource = source, GrossAmount = 1000, PlatformRevenueAmount = 100 }, default));
        Assert.Empty(await f.Db.PlatformRevenueEntries.ToArrayAsync());
    }

    [Fact]
    public async Task 기간별환원은_이미분배한주문잔여금은제외하고_기존수익만사용한다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); var preview = await f.Preview(receipt);
        await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", f.Request(receipt, preview), default);
        var service = new PlatformProfitReturnService(f.Db);
        await service.RecordRevenueAsync(new() { RevenueSource = PlatformRevenueSourceCodes.DriverUsageFee,
            GrossAmount = 2000, PlatformRevenueAmount = 200, OccurredAtUtc = f.Clock.Now }, default);
        var policy = await service.CreatePolicyAsync(new() { PolicyName = "기존 기간별 환원", ReturnRatePercent = 50,
            EffectiveStartDate = new(2026, 10, 1) }, default);
        var plan = await service.CreateReturnSchedulesAsync(new() { PolicyId = policy.Id,
            PeriodStartDate = new(2026, 10, 1), PeriodEndDate = new(2026, 10, 31), ScheduledPaymentDate = new(2026, 11, 1),
            Participants = [new() { ParticipantUserId = "worker", Weight = 1 }] }, default);
        Assert.Equal(200m, plan.TotalPlatformRevenueAmount); Assert.Equal(100m, plan.ReturnPoolAmount);
        Assert.Equal(100m, Assert.Single(plan.Schedules).PlannedReturnAmount);
        Assert.Equal(2, await f.Db.PlatformRevenueEntries.CountAsync());
    }

    [Fact]
    public async Task 플랫폼몫도_양측확정_지급신고_수령확인을_각각거친다()
    {
        await using var f = await Fixture.Create(); var receipt = await f.Activate(); var preview = await f.Preview(receipt);
        receipt = await f.Use().AccrueActivityRewardAsync(receipt.AgreementId, "owner", f.Request(receipt, preview), default);
        var entry = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "platform", default)).Entries);
        foreach (var actor in new[] { "owner", "platform" })
            receipt = await f.Use().ConfirmEntryAsync(receipt.AgreementId, actor, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, EntryId = entry.EntryId }, default);
        receipt = await f.Use().ReportPaymentAsync(receipt.AgreementId, "owner", new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, AmountKrw = entry.AmountKrw!.Value,
            Allocations = [new() { EntryId = entry.EntryId, AmountKrw = entry.AmountKrw.Value }],
            EvidenceReference = "simulation-transfer-statement" }, default);
        Assert.Single(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
        var payment = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "platform", default)).Payments);
        Assert.False(payment.IsActualBankVerified); Assert.Null(payment.RecipientConfirmedAtUtc);
        receipt = await f.Use().ConfirmReceiptAsync(receipt.AgreementId, "platform", new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, PaymentId = payment.PaymentId,
            ConfirmedAmountKrw = entry.AmountKrw.Value, Reason = "Simulation 당사자 수령 확인" }, default);
        payment = Assert.Single((await f.Use().GetAsync(receipt.AgreementId, "platform", default)).Payments);
        Assert.Equal(entry.AmountKrw, payment.RecipientConfirmedAmountKrw); Assert.False(payment.IsActualBankVerified);
        Assert.Single(await f.Db.PlatformRevenueEntries.ToArrayAsync());
    }

    private static async Task Error(string code, Func<Task> call)
        => Assert.Equal(code, (await Assert.ThrowsAsync<NeighborhoodSettlementException>(call)).Code);

    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; } = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Resolver : INeighborhoodSettlementSourceResolver
    {
        public NeighborhoodSettlementSourceSnapshot Snapshot { get; set; } = new();
        public NeighborhoodSettlementSourceSnapshot LotSnapshot { get; set; } = new();
        public Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string kind, string id, CancellationToken ct)
            => Task.FromResult(kind == C.DistributedLot ? LotSnapshot : Snapshot);
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
        public IReadOnlyList<NeighborhoodInventoryCostSelectionDto> CostSelections { get; private set; } = [];
        public NeighborhoodSettlementOptions Settings { get; } = new()
        { Enabled = true, Profile = NeighborhoodSettlementOptions.RequiredProfile, PlatformPayeeUserId = "platform" };
        public NeighborhoodSettlementUseCase Use() => new(Db, Resolver,
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            Options.Create(Settings), Clock);
        public NeighborhoodSettlementAgreementRequest Terms(int retention = 10) => new()
        {
            RequestId = Guid.NewGuid(), SourceKind = C.DistributedOrder, SourceId = "order-1", ActivityKind = C.StoredGoods,
            Lines = [new() { LineKey = "basic", Kind = C.Picking, Billing = C.PerUnit, UnitPriceKrw = 100, Quantity = 1,
                PayerUserId = "owner", PayeeUserId = "worker", BeneficiaryRole = "Picker" }],
            ActivityRewardTerms = new()
            {
                Economics = new() { PlatformPayeeUserId = "platform", PlatformRetentionRatePercent = retention,
                    BasisCode = "PostCostSurplus", CostAllocations = CostSelections },
                PayerUserId = "owner", ParticipantUserIds = ["worker"],
                Policy = new() { PolicyVersion = "explicit-retention-v1", ReturnRatePercent = 100 - retention,
                    Weights = [new() { Kind = C.Picking, UnitCode = R.Unit, Weight = 1 }] },
                FinancialBasis = new() { BasisKind = R.SimulationPartyStatement, BasisReference = "simulation-finance-v1",
                    RevenueAmountKrw = 10001, BasicWorkCostAmountKrw = 100, OtherCostAmountKrw = 0, ReserveAmountKrw = 0 }
            }
        };
        public async Task<NeighborhoodSettlementReceiptDto> Activate(NeighborhoodSettlementAgreementRequest? terms = null,
            bool omitPlatform = false)
        {
            terms ??= Terms(); var receipt = await Use().CreateAsync("owner", terms, default);
            foreach (var actor in terms.ActivityRewardTerms?.Economics is null || omitPlatform ? new[] { "owner", "worker" } : new[] { "owner", "worker", "platform" })
                receipt = await Consent(receipt, actor);
            return receipt;
        }
        public Task<NeighborhoodSettlementReceiptDto> Consent(NeighborhoodSettlementReceiptDto receipt, string actor, int termsVersion = 1)
            => Use().ConsentAsync(receipt.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
                TermsVersion = termsVersion, Granted = true }, default);
        public Task<NeighborhoodSettlementReceiptDto> AccrueBasic(NeighborhoodSettlementReceiptDto receipt)
            => Use().AccrueAsync(receipt.AgreementId, "owner", new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision,
                OriginKind = "InventoryHistory", OriginId = "pick-1", Lines = [new() { LineKey = "basic", Quantity = 1 }] }, default);
        public Task<NeighborhoodActivityRewardPreviewDto> Preview(NeighborhoodSettlementReceiptDto receipt)
            => Use().ActivityRewardPreviewAsync(receipt.AgreementId, "owner", default);
        public NeighborhoodSettlementActivityRewardAccrualRequest Request(NeighborhoodSettlementReceiptDto receipt, NeighborhoodActivityRewardPreviewDto preview)
            => new() { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, ExpectedCalculationDigest = preview.CalculationDigest };
        public static async Task<Fixture> Create(bool includeReplacementParties = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(connection, db);
            db.Users.Add(new ApplicationUser { Id = "platform", UserName = "platform" });
            db.Roles.Add(new IdentityRole { Id = "admin-role", Name = 역할명.서버관리자 });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = "platform", RoleId = "admin-role" }); await db.SaveChangesAsync();
            f.Resolver.Snapshot = new()
            {
                SourceKind = C.DistributedOrder, SourceId = "order-1", SourceVersion = "1", SourceDigest = "order-basis-v1",
                Participants = [new("owner", ["Owner"]), new("worker", ["Picker"]), new("buyer", ["Buyer"]),
                    .. (includeReplacementParties ? new NeighborhoodSettlementSourceParty[]
                        { new("owner-next", ["Owner"]), new("worker-next", ["Picker"]) } : [])],
                AllowedActivityKinds = [C.StoredGoods], ActualCompletedUnits = 1, ObservedAtUtc = f.Clock.Now,
                HistoricalSaleTotalKrw = 10001, CompletionEvidenceReferences = ["delivered-proof"],
                CompletedWork = [new(C.Picking, "InventoryHistory", "pick-1", 1, f.Clock.Now.AddMinutes(-1), ["pick-photo"],
                    "worker", "owner", OriginRevision: 1, RelatedOrderId: "order-1")],
                RewardOutcome = new(1, ["received:order-1", "delivered:order-1"]),
                Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.OutboundDelivery, StateCode = "Received",
                    CompletionConfirmed = true, PaymentReleaseReady = true, InventoryLotIds = ["lot-1"] },
                InventoryReservations = [new("reservation-1", "lot-1", 1, f.Clock.Now.AddMinutes(-1))]
            };
            f.Resolver.LotSnapshot = new()
            {
                SourceKind = C.DistributedLot, SourceId = "lot-1", SourceVersion = "1", SourceDigest = "lot-basis-v1",
                Participants = [new("owner", ["Owner"]), new("worker", ["Receiver", "SpaceOwner"])],
                AllowedActivityKinds = [C.StoredGoods], ActualCompletedUnits = 1, ObservedAtUtc = f.Clock.Now,
                CompletionEvidenceReferences = ["receive-photo"], StorageEvidenceReferences = ["receive-photo", "pick-photo"],
                StorageStartedAtUtc = f.Clock.Now.AddHours(-2),
                StorageIntervals = [new(f.Clock.Now.AddHours(-2), f.Clock.Now.AddMinutes(-1), 1)],
                CompletedWork = [new(C.Inbound, "InventoryHistory", "receive-1", 1, f.Clock.Now.AddHours(-2), ["receive-photo"],
                    "worker", "owner", "worker", OriginRevision: 1)],
                Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.InboundStorage, StateCode = "Received",
                    CompletionConfirmed = true, PaymentReleaseReady = true, InventoryLotIds = ["lot-1"] }
            };
            await f.CreateFreeInventoryCosts();
            return f;
        }
        private async Task CreateFreeInventoryCosts()
        {
            var receipt = await Use().CreateAsync("owner", new()
            {
                RequestId = Guid.NewGuid(), SourceKind = C.DistributedLot, SourceId = "lot-1", ActivityKind = C.StoredGoods,
                Lines = [new() { LineKey = "free-receive", Kind = C.Inbound, Billing = C.Free, AmountKrw = 0, Quantity = 1,
                    PayerUserId = "owner", PayeeUserId = "worker", BeneficiaryRole = "Receiver" },
                    new() { LineKey = "free-storage", Kind = C.Storage, Billing = C.Free, AmountKrw = 0, Quantity = 1,
                    PayerUserId = "owner", PayeeUserId = "worker", BeneficiaryRole = "SpaceOwner" }]
            }, default);
            foreach (var actor in new[] { "owner", "worker" }) receipt = await Consent(receipt, actor);
            receipt = await Use().AccrueAsync(receipt.AgreementId, "owner", new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, OriginKind = "InventoryHistory", OriginId = "receive-1",
                Lines = [new() { LineKey = "free-receive", Quantity = 1 }] }, default);
            var receivedEntry = Assert.Single(receipt.EntryIds);
            receipt = await Use().AccrueAsync(receipt.AgreementId, "owner", new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, OriginKind = C.DistributedLot, OriginId = "lot-1",
                Lines = [new() { LineKey = "free-storage", Quantity = 1 }] }, default);
            var storageEntry = Assert.Single(receipt.EntryIds);
            foreach (var entryId in new[] { receivedEntry, storageEntry })
                foreach (var actor in new[] { "owner", "worker" })
                    receipt = await Use().ConfirmEntryAsync(receipt.AgreementId, actor, new()
                    { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, EntryId = entryId }, default);
            CostSelections = (await Use().GetAsync(receipt.AgreementId, "owner", default)).Entries
                .Select(x => new NeighborhoodInventoryCostSelectionDto { SourceEntryId = x.EntryId, ExpectedEntryRevision = x.Revision }).ToArray();
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
