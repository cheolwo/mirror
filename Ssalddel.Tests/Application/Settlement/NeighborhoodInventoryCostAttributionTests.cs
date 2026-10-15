using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.정산;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using R = Ssalddel.Contracts.Common.Settlement.NeighborhoodActivityRewardCodes;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class NeighborhoodInventoryCostAttributionTests
{
    [Fact]
    public async Task 고정입고비의실제처리량을분모로쓰고세주문에33_34_34원을귀속해원가를보존한다()
    {
        await using var f = await Fixture.Create();
        var originals = await f.SourceEntries();
        Assert.Equal(1m, originals.Single(x => x.Kind == C.Inbound).Quantity);
        Assert.Equal(3m, originals.Single(x => x.Kind == C.Storage).Quantity);
        var allocated = new List<decimal>();
        for (var i = 1; i <= 3; i++)
        {
            var order = await f.Activate("order-" + i); var preview = await f.Preview(order);
            Assert.Equal(R.Ready, preview.StatusCode); Assert.Equal(2, preview.CostAttributions.Count);
            var inbound = preview.CostAttributions.Single(x => x.Kind == C.Inbound);
            Assert.Equal(3m, inbound.SourceQuantity); Assert.Equal(1m, inbound.AllocatedQuantity);
            Assert.Equal("lot-1", inbound.LotId); Assert.Equal("order-" + i, inbound.OrderId);
            Assert.Equal(0m, preview.CostAttributions.Single(x => x.Kind == C.Storage).AmountKrw);
            Assert.Equal(inbound.AmountKrw, preview.CostAttributionAmountKrw);
            Assert.Equal(9901m - inbound.AmountKrw, preview.SurplusAmountKrw);
            allocated.Add(inbound.AmountKrw);
            await f.Accrue(order, preview);
        }
        Assert.Equal([33m, 34m, 34m], allocated);
        Assert.Equal(101m, allocated.Sum());
        Assert.Equal(6, await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
        var after = await f.SourceEntries();
        Assert.Equal(originals.Select(x => (x.EntryId, x.AmountKrw, x.Quantity, x.Revision)),
            after.Select(x => (x.EntryId, x.AmountKrw, x.Quantity, x.Revision)));
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData("NoSelection", "InventoryCostEvidenceRequired")]
    [InlineData("MissingStorage", "InventoryCostIncomplete")]
    [InlineData("NotConfirmed", "InventoryCostNotConfirmed")]
    [InlineData("ChangedRevision", "InventoryCostSelectionChanged")]
    [InlineData("MultipleInbound", "InventoryCostAmbiguous")]
    [InlineData("PartialCostBasis", "InventoryCostAmbiguous")]
    [InlineData("TooManyUnits", "InventoryCostQuantityExceeded")]
    [InlineData("PendingAdjustment", "InventoryCostNotConfirmed")]
    public async Task 원천비용의선택확정수량조정근거가불완전하면추가배분을보류한다(string condition, string reason)
    {
        await using var f = await Fixture.Create(confirm: condition != "NotConfirmed", inboundBasis: condition == "PartialCostBasis" ? 2 : 3);
        var terms = f.Terms("order-1", quantity: condition == "TooManyUnits" ? 4 : 1);
        switch (condition)
        {
            case "NoSelection": terms.ActivityRewardTerms!.Economics!.CostAllocations = []; break;
            case "MissingStorage": terms.ActivityRewardTerms!.Economics!.CostAllocations = f.Selections
                .Where(x => x.SourceEntryId == f.InboundEntryId).ToArray(); break;
            case "ChangedRevision": await f.ConfirmSourceEntry(f.InboundEntryId, "owner"); break;
            case "MultipleInbound": f.Resolver.Lot = f.Lot([1m, 2m]); break;
            case "PendingAdjustment": await f.AdjustSource(-1, confirm: false); break;
        }
        var order = await f.Activate(terms); var preview = await f.Preview(order);
        Assert.Equal(R.WaitingForEvidence, preview.StatusCode); Assert.Contains(reason, preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(order, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().ToArrayAsync());
        Assert.Empty(await f.Db.PlatformRevenueEntries.ToArrayAsync());
        Assert.Equal(2 + (condition == "PendingAdjustment" ? 1 : 0), await f.Db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
    }

    [Fact]
    public async Task 완료된보관기간전체가주문의최초피킹이전이면수량비례원가를함께차감한다()
    {
        await using var f = await Fixture.Create(storagePeriod: true); var order = await f.Activate("order-1");
        var preview = await f.Preview(order); Assert.Equal(R.Ready, preview.StatusCode);
        var storage = preview.CostAttributions.Single(x => x.Kind == C.Storage);
        Assert.Equal(20m, storage.AmountKrw); Assert.Equal(3m, storage.SourceQuantity); Assert.Equal(1m, storage.AllocatedQuantity);
        Assert.Equal(f.StorageStart, storage.PeriodStartUtc); Assert.Equal(f.StorageEnd, storage.PeriodEndUtc);
        Assert.Equal(53m, preview.CostAttributionAmountKrw); Assert.Equal(9848m, preview.SurplusAmountKrw);
        await f.Accrue(order, preview); Assert.Equal(2, await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
    }

    [Fact]
    public async Task 피킹이후까지걸치는보관기간을주문의비용으로임의일할계산하지않는다()
    {
        await using var f = await Fixture.Create(storagePeriod: true);
        var order = await f.Activate(f.Terms("order-1", pickedAt: f.Clock.Now.AddHours(-1)));
        var preview = await f.Preview(order);
        Assert.Contains("InventoryStoragePeriodNotAttributable", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(order, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 연속보관구간만으로과거기간의실제수량을추정해원가귀속하지않는다()
    {
        await using var f = await Fixture.Create(storagePeriod: true);
        f.Resolver.Lot = f.Lot(storageQuantityEvidence: false);
        var order = await f.Activate("order-1"); var preview = await f.Preview(order);
        Assert.Contains("InventoryStoragePeriodNotAttributable", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(order, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 같은원천의기귀속수량과다음주문수량이실제기준량을넘으면발생시키지않는다()
    {
        await using var f = await Fixture.Create(); var first = await f.Activate("order-1", 2);
        var preview = await f.Preview(first); Assert.Equal(67m, preview.CostAttributionAmountKrw); await f.Accrue(first, preview);
        var second = await f.Activate("order-2", 2); var next = await f.Preview(second);
        Assert.Contains("InventoryCostQuantityExceeded", next.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(second, next));
        Assert.Equal(2, await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
        Assert.Single(await f.Db.PlatformRevenueEntries.ToArrayAsync());
    }

    [Fact]
    public async Task 확인한계산뒤다른주문이먼저귀속되면이전계산으로배분하지않고현재잔액을다시보여준다()
    {
        await using var f = await Fixture.Create(); var first = await f.Activate("order-1"); var second = await f.Activate("order-2");
        var stale = await f.Preview(second); Assert.Equal(33m, stale.CostAttributionAmountKrw);
        await f.Accrue(first, await f.Preview(first));
        await Error("ActivityRewardCalculationChanged", () => f.Accrue(second, stale));
        Assert.Equal(2, await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
        var current = await f.Preview(second); Assert.Equal(34m, current.CostAttributionAmountKrw);
        Assert.NotEqual(stale.CalculationDigest, current.CalculationDigest);
        await f.Accrue(second, current); Assert.Equal(4, await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
    }

    [Fact]
    public async Task 원천의확정조정이바뀌면그조정을포함한원가와계산지문을다시확인한다()
    {
        await using var f = await Fixture.Create(); var order = await f.Activate("order-1"); var stale = await f.Preview(order);
        await f.AdjustSource(3, confirm: true);
        var current = await f.Preview(order); Assert.Equal(R.Ready, current.StatusCode);
        Assert.Equal(34m, current.CostAttributionAmountKrw); Assert.NotEqual(stale.CalculationDigest, current.CalculationDigest);
        await Error("ActivityRewardCalculationChanged", () => f.Accrue(order, stale));
        await f.Accrue(order, current); Assert.Equal(2, await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
    }

    [Fact]
    public async Task 귀속후감액으로원천순액이이미귀속한금액보다작아지면기록을보존하고후속배분을보류한다()
    {
        await using var f = await Fixture.Create(); var first = await f.Activate("order-1"); var firstPreview = await f.Preview(first);
        await f.Accrue(first, firstPreview); var originalClaims = await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().AsNoTracking().ToArrayAsync();
        await f.AdjustSource(-80, confirm: true);
        var second = await f.Activate("order-2"); var preview = await f.Preview(second);
        Assert.Contains("InventoryCostBudgetUnderstated", preview.ReasonCodes);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(second, preview));
        var after = await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().AsNoTracking().ToArrayAsync();
        Assert.Equal(originalClaims.Select(x => (x.AttributionId, x.DetailsJson)), after.Select(x => (x.AttributionId, x.DetailsJson)));
        var frozen = await f.Preview(first); Assert.True(frozen.AlreadyAccrued);
        Assert.Equal(firstPreview.CalculationDigest, frozen.CalculationDigest); Assert.Equal(33m, frozen.CostAttributionAmountKrw);
        Assert.Single(await f.Db.PlatformRevenueEntries.ToArrayAsync());
    }

    [Fact]
    public async Task 같은발생요청의재조회는비용귀속이나입고보수를다시만들지않는다()
    {
        await using var f = await Fixture.Create(); var order = await f.Activate("order-1"); var preview = await f.Preview(order);
        var request = f.Request(order, preview); var first = await f.Use().AccrueActivityRewardAsync(order.AgreementId, "owner", request, default);
        var replay = await f.Use().AccrueActivityRewardAsync(order.AgreementId, "owner", request, default);
        Assert.True(replay.Replayed); Assert.Equal(first.EntryIds, replay.EntryIds);
        Assert.Equal(2, await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
        Assert.Equal(2, (await f.SourceEntries()).Length); Assert.Single(await f.Db.PlatformRevenueEntries.ToArrayAsync());
        Assert.Empty(await f.Db.Set<NeighborhoodSettlementPaymentRecord>().ToArrayAsync());
        var actor = await f.Use().ActivityRewardPreviewAsync(order.AgreementId, "worker", default);
        Assert.Empty(actor.CostAttributions);
    }

    [Fact]
    public async Task 경제성선택이없는기존약정에는원가귀속이나플랫폼몫을소급추가하지않는다()
    {
        await using var f = await Fixture.Create(); var terms = f.Terms("order-1");
        terms.ActivityRewardTerms!.Economics = null; terms.ActivityRewardTerms.Policy.ReturnRatePercent = 10m;
        var order = await f.Activate(terms); var preview = await f.Preview(order);
        Assert.Equal(R.Ready, preview.StatusCode); Assert.Equal(990m, preview.PoolAmountKrw); Assert.Empty(preview.CostAttributions);
        await f.Accrue(order, preview);
        Assert.Empty(await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().ToArrayAsync());
        Assert.Empty(await f.Db.PlatformRevenueEntries.ToArrayAsync()); Assert.Equal(2, (await f.SourceEntries()).Length);
    }

    [Fact]
    public async Task 다른부담자가지급한입고비를현주문의부담자비용으로몰래귀속하지않는다()
    {
        await using var f = await Fixture.Create(sourceOwner: "other-owner"); var order = await f.Activate("order-1");
        var preview = await f.Preview(order); Assert.NotEqual(R.Ready, preview.StatusCode); Assert.Empty(preview.CostAttributions);
        await Error("ActivityRewardEvidenceNotReady", () => f.Accrue(order, preview));
        Assert.Empty(await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().ToArrayAsync());
        Assert.Empty(await f.Db.PlatformRevenueEntries.ToArrayAsync());
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
        public NeighborhoodSettlementSourceSnapshot Lot { get; set; } = new();
        public Dictionary<string, NeighborhoodSettlementSourceSnapshot> Orders { get; } = new(StringComparer.Ordinal);
        public Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string kind, string id, CancellationToken ct)
            => Task.FromResult(kind == C.DistributedLot ? Lot : Orders[id]);
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
        public string SourceOwner { get; private set; } = "owner";
        public string SourceAgreementId { get; private set; } = "";
        public string InboundEntryId { get; private set; } = "";
        public IReadOnlyList<NeighborhoodInventoryCostSelectionDto> Selections { get; private set; } = [];
        public DateTime StorageStart => Clock.Now.AddHours(-2);
        public DateTime StorageEnd => Clock.Now.AddMinutes(-30);
        public NeighborhoodSettlementUseCase Use() => new(Db, Resolver,
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            Options.Create(new NeighborhoodSettlementOptions { Enabled = true, Profile = NeighborhoodSettlementOptions.RequiredProfile,
                PlatformPayeeUserId = "platform" }), Clock);

        public NeighborhoodSettlementAgreementRequest Terms(string orderId, int quantity = 1, DateTime? pickedAt = null)
        {
            var pick = pickedAt ?? Clock.Now.AddMinutes(-1);
            Resolver.Orders[orderId] = new()
            {
                SourceKind = C.DistributedOrder, SourceId = orderId, SourceVersion = "1", SourceDigest = "order-basis:" + orderId,
                Participants = [new("owner", ["Owner"]), new("worker", ["Picker"]), new("buyer", ["Buyer"])],
                AllowedActivityKinds = [C.StoredGoods], ActualCompletedUnits = quantity, ObservedAtUtc = Clock.Now,
                CompletionEvidenceReferences = ["received:" + orderId],
                CompletedWork = [new(C.Picking, "InventoryHistory", "pick:" + orderId, quantity, pick, ["pick-photo:" + orderId],
                    "worker", "owner", OriginRevision: 1, RelatedOrderId: orderId)],
                RewardOutcome = new(quantity, ["received:" + orderId]),
                Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.OutboundDelivery, StateCode = "Received",
                    CompletionConfirmed = true, PaymentReleaseReady = true, InventoryLotIds = ["lot-1"] },
                InventoryReservations = [new("reservation:" + orderId, "lot-1", quantity, pick)]
            };
            return new()
            {
                RequestId = Guid.NewGuid(), SourceKind = C.DistributedOrder, SourceId = orderId, ActivityKind = C.StoredGoods,
                Lines = [new() { LineKey = "pick", Kind = C.Picking, Billing = C.PerUnit, UnitPriceKrw = 100,
                    Quantity = quantity, PayerUserId = "owner", PayeeUserId = "worker", BeneficiaryRole = "Picker" }],
                ActivityRewardTerms = new()
                {
                    PayerUserId = "owner", ParticipantUserIds = ["worker"],
                    Economics = new() { PlatformPayeeUserId = "platform", PlatformRetentionRatePercent = 10,
                        BasisCode = "PostCostSurplus", CostAllocations = Selections },
                    Policy = new() { PolicyVersion = "inventory-cost-v1", ReturnRatePercent = 90,
                        Weights = [new() { Kind = C.Picking, UnitCode = R.Unit, Weight = 1 }] },
                    FinancialBasis = new() { BasisKind = R.SimulationPartyStatement, BasisReference = "simulation-financial-basis",
                        RevenueAmountKrw = 10001, BasicWorkCostAmountKrw = 100 * quantity, OtherCostAmountKrw = 0, ReserveAmountKrw = 0 }
                }
            };
        }
        public Task<NeighborhoodSettlementReceiptDto> Activate(string orderId, int quantity = 1) => Activate(Terms(orderId, quantity));
        public async Task<NeighborhoodSettlementReceiptDto> Activate(NeighborhoodSettlementAgreementRequest terms)
        {
            var result = await Use().CreateAsync("owner", terms, default);
            foreach (var actor in terms.ActivityRewardTerms!.Economics is null ? new[] { "owner", "worker" } : new[] { "owner", "worker", "platform" })
                result = await Consent(result, actor);
            return result;
        }
        public Task<NeighborhoodActivityRewardPreviewDto> Preview(NeighborhoodSettlementReceiptDto order)
            => Use().ActivityRewardPreviewAsync(order.AgreementId, "owner", default);
        public Task<NeighborhoodSettlementReceiptDto> Accrue(NeighborhoodSettlementReceiptDto order, NeighborhoodActivityRewardPreviewDto preview)
            => Use().AccrueActivityRewardAsync(order.AgreementId, "owner", Request(order, preview), default);
        public NeighborhoodSettlementActivityRewardAccrualRequest Request(NeighborhoodSettlementReceiptDto order, NeighborhoodActivityRewardPreviewDto preview)
            => new() { RequestId = Guid.NewGuid(), ExpectedRevision = order.Revision, ExpectedCalculationDigest = preview.CalculationDigest };
        public async Task<NeighborhoodSettlementEntryDto[]> SourceEntries()
            => (await Use().GetAsync(SourceAgreementId, SourceOwner, default)).Entries.OrderBy(x => x.EntryId, StringComparer.Ordinal).ToArray();
        public async Task ConfirmSourceEntry(string entryId, string actor)
        {
            var summary = await Use().GetAsync(SourceAgreementId, SourceOwner, default);
            await Use().ConfirmEntryAsync(SourceAgreementId, actor, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = summary.Agreement.Revision, EntryId = entryId }, default);
        }
        public async Task AdjustSource(decimal amount, bool confirm)
        {
            var summary = await Use().GetAsync(SourceAgreementId, SourceOwner, default);
            var receipt = await Use().AdjustmentAsync(SourceAgreementId, SourceOwner, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = summary.Agreement.Revision, OriginalEntryId = InboundEntryId,
                AmountKrw = amount, Reason = "Simulation 원천 원가 정정" }, default);
            if (confirm)
                foreach (var actor in new[] { SourceOwner, "worker" }) await ConfirmSourceEntry(Assert.Single(receipt.EntryIds), actor);
        }
        private Task<NeighborhoodSettlementReceiptDto> Consent(NeighborhoodSettlementReceiptDto result, string actor)
            => Use().ConsentAsync(result.AgreementId, actor, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = result.Revision, TermsVersion = 1, Granted = true }, default);
        public NeighborhoodSettlementSourceSnapshot Lot(IReadOnlyList<decimal>? receiveQuantities = null, bool storageQuantityEvidence = true)
        {
            receiveQuantities ??= [3m];
            var work = receiveQuantities.Select((qty, i) => new NeighborhoodSettlementCompletedWork(C.Inbound, "InventoryHistory", "receive-" + (i + 1),
                qty, Clock.Now.AddHours(-3).AddMinutes(i), ["receive-photo-" + (i + 1)], "worker", SourceOwner, "worker", OriginRevision: 1)).ToArray();
            return new()
            {
                SourceKind = C.DistributedLot, SourceId = "lot-1", SourceVersion = "1", SourceDigest = "lot-basis-v1",
                Participants = [new(SourceOwner, ["Owner"]), new("worker", ["Receiver", "SpaceOwner"])],
                AllowedActivityKinds = [C.StoredGoods], ActualCompletedUnits = receiveQuantities.Sum(), ObservedAtUtc = Clock.Now,
                CompletionEvidenceReferences = work.SelectMany(x => x.EvidenceReferences).ToArray(),
                StorageEvidenceReferences = ["receive-photo-1", "storage-history-proof"], StorageStartedAtUtc = Clock.Now.AddHours(-3),
                StorageIntervals = [new(Clock.Now.AddHours(-3), Clock.Now.AddMinutes(-1), receiveQuantities.Sum())],
                StorageQuantityIntervals = storageQuantityEvidence
                    ? [new(Clock.Now.AddHours(-3), Clock.Now.AddMinutes(-1), receiveQuantities.Sum())] : [], CompletedWork = work,
                Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.InboundStorage, StateCode = "Received",
                    CompletionConfirmed = true, PaymentReleaseReady = true, InventoryLotIds = ["lot-1"] }
            };
        }
        public static async Task<Fixture> Create(bool confirm = true, int inboundBasis = 3, bool storagePeriod = false, string sourceOwner = "owner")
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(connection, db) { SourceOwner = sourceOwner };
            db.Users.Add(new ApplicationUser { Id = "platform", UserName = "platform" });
            db.Roles.Add(new IdentityRole { Id = "admin-role", Name = 역할명.서버관리자 });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = "platform", RoleId = "admin-role" }); await db.SaveChangesAsync();
            f.Resolver.Lot = f.Lot(); await f.CreateSourceCosts(confirm, inboundBasis, storagePeriod); return f;
        }
        private async Task CreateSourceCosts(bool confirm, int inboundBasis, bool storagePeriod)
        {
            var storage = new NeighborhoodSettlementTermLine { LineKey = "storage", Kind = C.Storage,
                Billing = storagePeriod ? C.StoragePeriod : C.Free, AmountKrw = storagePeriod ? 60 : 0, Quantity = 3,
                PayerUserId = SourceOwner, PayeeUserId = "worker", BeneficiaryRole = "SpaceOwner",
                PeriodStartUtc = storagePeriod ? StorageStart : null, PeriodEndUtc = storagePeriod ? StorageEnd : null };
            var receipt = await Use().CreateAsync(SourceOwner, new()
            {
                RequestId = Guid.NewGuid(), SourceKind = C.DistributedLot, SourceId = "lot-1", ActivityKind = C.StoredGoods,
                Lines = [new() { LineKey = "receive", Kind = C.Inbound, Billing = C.Fixed, AmountKrw = 101, Quantity = inboundBasis,
                    PayerUserId = SourceOwner, PayeeUserId = "worker", BeneficiaryRole = "Receiver" }, storage]
            }, default);
            SourceAgreementId = receipt.AgreementId;
            foreach (var actor in new[] { SourceOwner, "worker" }) receipt = await Consent(receipt, actor);
            receipt = await Use().AccrueAsync(SourceAgreementId, SourceOwner, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, OriginKind = "InventoryHistory", OriginId = "receive-1",
                Lines = [new() { LineKey = "receive", Quantity = 1 }] }, default);
            InboundEntryId = Assert.Single(receipt.EntryIds);
            receipt = await Use().AccrueAsync(SourceAgreementId, SourceOwner, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = receipt.Revision, OriginKind = C.DistributedLot, OriginId = "lot-1",
                Lines = [new() { LineKey = "storage", Quantity = 3, PeriodStartUtc = storage.PeriodStartUtc, PeriodEndUtc = storage.PeriodEndUtc }] }, default);
            var storageId = Assert.Single(receipt.EntryIds);
            if (confirm)
                foreach (var entryId in new[] { InboundEntryId, storageId })
                    foreach (var actor in new[] { SourceOwner, "worker" }) await ConfirmSourceEntry(entryId, actor);
            Selections = (await SourceEntries()).Select(x => new NeighborhoodInventoryCostSelectionDto
            { SourceEntryId = x.EntryId, ExpectedEntryRevision = x.Revision }).ToArray();
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
