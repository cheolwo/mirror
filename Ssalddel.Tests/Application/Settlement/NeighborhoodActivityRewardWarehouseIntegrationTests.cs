using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Ssalddel.Application.Settlement;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Storage;
using Ssalddel.Tests.Application.Warehouse;
using 살뜰.Services.Options;
using 살뜰.도메인.정산;
using 살뜰.도메인.창고;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using R = Ssalddel.Contracts.Common.Settlement.NeighborhoodActivityRewardCodes;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class NeighborhoodActivityRewardWarehouseIntegrationTests
{
    [Fact]
    public async Task 실제입고피킹사진전달수령근거로만성과금을계산하고기본보수와별도로발생시킨다()
        => await Run(false);

    [Fact]
    public async Task 입고정산은독립완료하고출고보수는기록을보존하며고객수령뒤지급신고한다()
        => await Run(true);

    [Fact]
    public async Task 실제입고비를출고원가로만귀속하고플랫폼몫과참여자성과금을함께기록한다()
        => await Run(true, true);

    [Fact]
    public async Task 출고후에도과거의유료보관기간과입고비를실제재고수량으로귀속한다()
        => await Run(true, true, true);

    private static async Task Run(bool verifySeparatedSettlement, bool verifyEconomics = false, bool verifyStoragePeriodCost = false)
    {
        // 업무·사진·정산 정본을 같은 격리 SQLite로 연결합니다. 사진은 합성 시험 이미지입니다.
        await using var f = await Create(); var site = (await f.Sites())[0]; var location = site.Locations.Single();
        var execution = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
        var photos = new DistributedPhotoEvidenceService(f.Db,
            new DevelopmentLocalStorageService(new PhotoEnvironment(), new HttpContextAccessor()), execution,
            Options.Create(new DistributedFulfillmentOptions { Enabled = true, Profile = DistributedFulfillmentCodes.Profile }), f.Clock);
        var resolver = new NeighborhoodSettlementSourceResolver(f.Db, f.Clock);
        var settings = new NeighborhoodSettlementOptions { Enabled = true, Profile = NeighborhoodSettlementOptions.RequiredProfile };
        if (verifyEconomics)
        {
            settings.PlatformPayeeUserId = "integration-platform";
            f.Db.Users.Add(new 살뜰.Data.ApplicationUser { Id = settings.PlatformPayeeUserId, UserName = settings.PlatformPayeeUserId });
            f.Db.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole { Id = "integration-platform-role", Name = 살뜰.Data.역할명.서버관리자 });
            f.Db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = settings.PlatformPayeeUserId, RoleId = "integration-platform-role" });
            await f.Db.SaveChangesAsync();
        }
        var settlement = new NeighborhoodSettlementUseCase(f.Db, resolver, execution, Options.Create(settings), f.Clock);
        var owner = new DistributedActor("goods-owner-a");
        await f.EnsureAgreement(site, owner, "same-sku");
        var inbound = await f.Use(SellerA).InboundAsync(SellerA, new()
        {
            RequestId = Guid.NewGuid(), SourceReference = "integration-explicit-stock",
            Lines = [new() { LineKey = "first", SourceInboundId = 101, SiteId = site.SiteId, LocationId = location.LocationId,
                Sku = "same-sku", ProductName = "합성 시험 물품", LotCode = "integration-lot", ExpectedQuantity = 2 }]
        }, default);
        var receive = new DistributedInboundReceiptBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = inbound.Revision,
            LocationBarcode = location.Barcode, Lines = [new() { LineKey = "first", Sku = "same-sku", Quantity = 2 }] };
        receive.PhotoEvidenceIds = await Upload(f, photos, Operator, new() { Kind = "Receive", AggregateId = inbound.AggregateId, Receive = receive });
        await f.Use(Operator, binder: photos).ReceiveBatchAsync(Operator, inbound.AggregateId, receive, default);
        if (verifyStoragePeriodCost) f.Clock.Now = f.Clock.Now.AddHours(1);
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync();
        var stored = await resolver.ResolveAsync(C.DistributedLot, lot.LotId, default);
        Assert.Equal(2, Assert.Single(stored.CompletedWork.Where(x => x.Kind == C.Inbound)).Quantity);
        Assert.NotEmpty(stored.StorageIntervals);

        string? inboundAgreementId = null;
        IReadOnlyList<NeighborhoodInventoryCostSelectionDto> costSelections = [];
        if (verifySeparatedSettlement)
        {
            var inboundTerms = new NeighborhoodSettlementAgreementRequest
            {
                RequestId = Guid.NewGuid(), SourceKind = C.DistributedLot, SourceId = lot.LotId, ActivityKind = C.StoredGoods,
                Lines = [new() { LineKey = "inbound", Kind = C.Inbound, Billing = C.PerUnit, Quantity = 2, UnitPriceKrw = 100,
                    PayerUserId = owner.UserId, PayeeUserId = Operator.UserId, BeneficiaryRole = "Receiver" }]
            };
            var storageOwner = stored.Participants.Single(x => x.Roles.Contains("SpaceOwner")).UserId;
            if (verifyEconomics)
                inboundTerms.Lines = [.. inboundTerms.Lines, new() { LineKey = "storage", Kind = C.Storage,
                    Billing = verifyStoragePeriodCost ? C.StoragePeriod : C.Free, AmountKrw = verifyStoragePeriodCost ? 60 : 0,
                    PeriodStartUtc = verifyStoragePeriodCost ? stored.StorageStartedAtUtc : null,
                    PeriodEndUtc = verifyStoragePeriodCost ? f.Clock.Now : null,
                    Quantity = 2, PayerUserId = owner.UserId, PayeeUserId = storageOwner, BeneficiaryRole = "SpaceOwner" }];
            var received = await settlement.CreateAsync(owner.UserId, inboundTerms, default);
            inboundAgreementId = received.AgreementId;
            foreach (var actor in (verifyEconomics ? new[] { owner.UserId, Operator.UserId, storageOwner } : new[] { owner.UserId, Operator.UserId }).Distinct())
                received = await settlement.ConsentAsync(received.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = received.Revision, TermsVersion = 1, Granted = true }, default);
            var inboundWork = Assert.Single(stored.CompletedWork);
            received = await settlement.AccrueAsync(received.AgreementId, owner.UserId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = received.Revision,
                OriginKind = inboundWork.OriginKind, OriginId = inboundWork.OriginId, Lines = [new() { LineKey = "inbound", Quantity = 2 }] }, default);
            var inboundEntry = Assert.Single((await settlement.GetAsync(received.AgreementId, owner.UserId, default)).Entries);
            foreach (var actor in new[] { owner.UserId, Operator.UserId })
                received = await settlement.ConfirmEntryAsync(received.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = received.Revision, EntryId = inboundEntry.EntryId }, default);
            await settlement.ReportPaymentAsync(received.AgreementId, owner.UserId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = received.Revision,
                AmountKrw = 200, EvidenceReference = "simulation-inbound-payment-statement",
                Allocations = [new() { EntryId = inboundEntry.EntryId, AmountKrw = 200 }] }, default);
            if (verifyEconomics)
            {
                var storageLine = inboundTerms.Lines.Single(x => x.Kind == C.Storage);
                received = await settlement.AccrueAsync(received.AgreementId, owner.UserId, new() { RequestId = Guid.NewGuid(),
                    ExpectedRevision = (await settlement.GetAsync(received.AgreementId, owner.UserId, default)).Agreement.Revision,
                    OriginKind = C.DistributedLot, OriginId = lot.LotId, Lines = [new() { LineKey = "storage", Quantity = 2,
                        PeriodStartUtc = storageLine.PeriodStartUtc, PeriodEndUtc = storageLine.PeriodEndUtc }] }, default);
                var storage = (await settlement.GetAsync(received.AgreementId, owner.UserId, default)).Entries.Single(x => x.Kind == C.Storage);
                foreach (var actor in new[] { owner.UserId, storageOwner }.Distinct())
                    received = await settlement.ConfirmEntryAsync(received.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = received.Revision, EntryId = storage.EntryId }, default);
                costSelections = (await settlement.GetAsync(received.AgreementId, owner.UserId, default)).Entries
                    .Select(x => new NeighborhoodInventoryCostSelectionDto { SourceEntryId = x.EntryId, ExpectedEntryRevision = x.Revision }).ToArray();
            }
            // 아직 출고 주문이 없어도 실제 입고 근거의 보수는 독립적으로 확정·지급 신고 가능합니다.
            Assert.Empty(await f.Db.Set<분산배송주문Record>().ToArrayAsync());
            var inboundRead = await settlement.GetAsync(received.AgreementId, owner.UserId, default);
            Assert.Equal(NeighborhoodSettlementWorkflowCodes.InboundStorage, inboundRead.Workflow!.Kind);
            Assert.Equal(NeighborhoodSettlementWorkflowCodes.WorkCompleted, inboundRead.Agreement.Terms.PaymentReleaseCondition);
            Assert.True(inboundRead.Workflow.PaymentReleaseReady);
            Assert.Single(inboundRead.Payments);
        }

        var wave = await f.Use(Operator).CreateWaveAsync(Operator, new() { RequestId = Guid.NewGuid(), Label = "정산 연결 시험",
            CutoffUtc = f.Clock.Now.AddMinutes(1), PickupStartUtc = f.Clock.Now.AddMinutes(2), PickupEndUtc = f.Clock.Now.AddHours(3),
            DeliveryStartUtc = f.Clock.Now.AddMinutes(2), DeliveryEndUtc = f.Clock.Now.AddHours(4), PickupServiceMinutes = 1, DeliveryServiceMinutes = 1 }, default);
        await f.Cargo("integration-reward-cargo", SellerA, CustomerA, 2);
        wave = await f.Use(SellerA).OrderAsync(SellerA, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision,
            OrderReference = "integration-reward-cargo", CargoRequestId = "integration-reward-cargo", Lines = [new() { LotId = lot.LotId, Quantity = 2 }] }, default);
        wave = await f.Use(Operator).CloseAsync(Operator, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision }, default);
        f.RefreshLocation();
        wave = await f.Use(Operator).PlanAsync(Operator, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = DistributedTestFixture.Driver.UserId }, default);
        await f.Use(DistributedTestFixture.Driver).AcceptAsync(DistributedTestFixture.Driver, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision }, default);
        var route = await f.Use(Operator).WaveAsync(Operator, wave.AggregateId, "warehouse", default);
        var order = Assert.Single(route.Orders); var reservation = Assert.Single(route.Reservations);

        // 기본 피킹 200원 + 원배송비 15,000원을 비용 하한에 포함합니다. 경제 입력은 Simulation 당사자 진술입니다.
        var terms = Terms(order.OrderId, owner.UserId);
        if (verifyEconomics)
        {
            terms.ActivityRewardTerms!.Policy.ReturnRatePercent = 90;
            terms.ActivityRewardTerms.Economics = new() { PlatformPayeeUserId = settings.PlatformPayeeUserId,
                PlatformRetentionRatePercent = 10, CostAllocations = costSelections };
        }
        var agreement = await settlement.CreateAsync(owner.UserId, terms, default);
        foreach (var actor in verifyEconomics ? new[] { owner.UserId, DistributedTestFixture.Driver.UserId, settings.PlatformPayeeUserId } : new[] { owner.UserId, DistributedTestFixture.Driver.UserId })
            agreement = await settlement.ConsentAsync(agreement.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = agreement.Revision, TermsVersion = 1, Granted = true }, default);
        await AssertWaitingForReceipt(settlement, agreement.AgreementId, owner.UserId);

        var pickup = route.Stops.Single(x => x.StopId == route.CurrentStopId); f.Clock.Now = pickup.ExpectedArrivalUtc; f.RefreshLocation();
        var pick = new DistributedPickBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = route.Revision, StopId = pickup.StopId,
            LocationBarcode = location.Barcode, Lines = [new() { ReservationId = reservation.ReservationId, OwnerUserId = owner.UserId, Sku = "same-sku", Quantity = 2 }] };
        pick.PhotoEvidenceIds = await Upload(f, photos, DistributedTestFixture.Driver, new() { Kind = "Pick", AggregateId = route.WaveId, Pick = pick });
        await f.Use(DistributedTestFixture.Driver, binder: photos).PickBatchAsync(DistributedTestFixture.Driver, route.WaveId, pick, default);
        route = await f.Use(Operator).WaveAsync(Operator, route.WaveId, "warehouse", default);
        await AssertWaitingForReceipt(settlement, agreement.AgreementId, owner.UserId);
        NeighborhoodSettlementPaymentReportRequest? pendingPayment = null;
        if (verifySeparatedSettlement)
        {
            var pickingSource = await resolver.ResolveAsync(C.DistributedOrder, order.OrderId, default);
            var pickingWork = Assert.Single(pickingSource.CompletedWork.Where(x => x.Kind == C.Picking));
            agreement = await settlement.AccrueAsync(agreement.AgreementId, owner.UserId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = agreement.Revision,
                OriginKind = pickingWork.OriginKind, OriginId = pickingWork.OriginId, Lines = [new() { LineKey = "basic-pick", Quantity = 2 }] }, default);
            var basic = Assert.Single((await settlement.GetAsync(agreement.AgreementId, owner.UserId, default)).Entries);
            foreach (var actor in new[] { owner.UserId, DistributedTestFixture.Driver.UserId })
                agreement = await settlement.ConfirmEntryAsync(agreement.AgreementId, actor, new() { RequestId = Guid.NewGuid(), ExpectedRevision = agreement.Revision, EntryId = basic.EntryId }, default);
            var read = await settlement.GetAsync(agreement.AgreementId, owner.UserId, default);
            Assert.Equal(200, Assert.Single(read.Entries).AmountKrw); Assert.False(Assert.Single(read.Entries).PaymentReleaseReady);
            Assert.False(read.Workflow!.PaymentReleaseReady);
            pendingPayment = new() { RequestId = Guid.NewGuid(), ExpectedRevision = agreement.Revision, AmountKrw = 200,
                EvidenceReference = "simulation-outbound-payment-statement", Allocations = [new() { EntryId = basic.EntryId, AmountKrw = 200 }] };
            Assert.Equal("NeighborhoodSettlementDeliveryReceiptRequired", (await Assert.ThrowsAsync<NeighborhoodSettlementException>(
                () => settlement.ReportPaymentAsync(agreement.AgreementId, owner.UserId, pendingPayment, default))).Code);
        }

        var dropoff = route.Stops.Single(x => x.StopId == route.CurrentStopId); f.Clock.Now = dropoff.ExpectedArrivalUtc; f.RefreshLocation();
        var deliver = new DistributedDeliveryBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = route.Revision, StopId = dropoff.StopId,
            Lines = [new() { ReservationId = reservation.ReservationId, Quantity = 2 }] };
        deliver.PhotoEvidenceIds = await Upload(f, photos, DistributedTestFixture.Driver, new() { Kind = "Deliver", AggregateId = route.WaveId, Deliver = deliver });
        await f.Use(DistributedTestFixture.Driver, binder: photos).DeliverBatchAsync(DistributedTestFixture.Driver, route.WaveId, deliver, default);
        await AssertWaitingForReceipt(settlement, agreement.AgreementId, owner.UserId);
        if (pendingPayment is not null)
            Assert.Equal("NeighborhoodSettlementDeliveryReceiptRequired", (await Assert.ThrowsAsync<NeighborhoodSettlementException>(
                () => settlement.ReportPaymentAsync(agreement.AgreementId, owner.UserId, pendingPayment, default))).Code);
        route = await f.Use(Operator).WaveAsync(Operator, route.WaveId, "warehouse", default);
        await f.Use(CustomerA).AcknowledgeAsync(CustomerA, route.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = route.Revision,
            OrderId = order.OrderId, EvidenceReference = "integration-explicit-customer-receipt" }, default);

        if (pendingPayment is not null)
        {
            agreement = await settlement.ReportPaymentAsync(agreement.AgreementId, owner.UserId, pendingPayment, default);
            var read = await settlement.GetAsync(agreement.AgreementId, owner.UserId, default);
            Assert.True(read.Workflow!.PaymentReleaseReady); Assert.True(Assert.Single(read.Entries).PaymentReleaseReady);
            Assert.Equal(NeighborhoodSettlementWorkflowCodes.OutboundDelivery, read.Workflow.Kind);
            Assert.Equal(new[] { lot.LotId }, read.Workflow.InventoryLotIds);
            var inboundRead = await settlement.GetAsync(inboundAgreementId!, owner.UserId, default);
            Assert.Equal(C.Active, inboundRead.Agreement.StatusCode); // 출고 기사가 입고 약정의 동의 판본을 바꾸지 않습니다.
        }

        var source = await resolver.ResolveAsync(C.DistributedOrder, order.OrderId, default);
        Assert.Equal(2, source.RewardOutcome!.ConfirmedQuantity);
        var picked = Assert.Single(source.CompletedWork.Where(x => x.Kind == C.Picking));
        var delivered = Assert.Single(source.CompletedWork.Where(x => x.Kind == C.Delivery));
        Assert.Equal(2, picked.Quantity); Assert.Equal(1, delivered.Quantity);
        Assert.Equal("InventoryHistory", picked.OriginKind); Assert.Equal("CargoTransport", delivered.OriginKind);
        Assert.All(new[] { picked, delivered }, work => { Assert.Equal(DistributedTestFixture.Driver.UserId, work.ActorUserId);
            Assert.Equal(order.OrderId, work.RelatedOrderId); Assert.True(work.OriginRevision > 0); });
        Assert.Contains("photo:" + Assert.Single(pick.PhotoEvidenceIds), picked.EvidenceReferences);
        Assert.Contains("photo:" + Assert.Single(deliver.PhotoEvidenceIds), delivered.EvidenceReferences);
        Assert.All(await f.Db.Set<분산사진증빙Record>().AsNoTracking().ToArrayAsync(), x => Assert.Equal("Bound", x.StatusCode));

        var preview = await settlement.ActivityRewardPreviewAsync(agreement.AgreementId, owner.UserId, default);
        var expectedReward = verifyStoragePeriodCost ? 2286 : verifyEconomics ? 2340 : 280;
        var expectedPlatform = verifyStoragePeriodCost ? 254 : 260;
        Assert.Equal(R.Ready, preview.StatusCode); Assert.Equal(expectedReward, preview.PoolAmountKrw); Assert.False(preview.IsActualBankVerified);
        if (verifyEconomics)
        {
            Assert.Equal(verifyStoragePeriodCost ? 260 : 200, preview.CostAttributionAmountKrw); Assert.Equal(expectedPlatform, preview.PlatformRetentionAmountKrw);
            Assert.Equal(verifyStoragePeriodCost ? 60 : 0, Assert.Single(preview.CostAttributions.Where(x => x.Kind == C.Storage)).AmountKrw);
            Assert.Equal(lot.LotId, Assert.Single(preview.CostAttributions.Where(x => x.Kind == C.Inbound)).LotId);
        }
        Assert.Equal(new[] { C.Delivery, C.Picking }, preview.ContributionShares.Select(x => x.Contribution.Kind).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(preview.ContributionShares, x => x.Contribution.Kind is C.Inbound or C.Storage);
        Assert.Equal(2, Assert.Single(preview.ContributionShares.Where(x => x.Contribution.Kind == C.Picking)).Contribution.Quantity);
        Assert.Equal(1, Assert.Single(preview.ContributionShares.Where(x => x.Contribution.Kind == C.Delivery)).Contribution.Quantity);
        var reward = await settlement.AccrueActivityRewardAsync(agreement.AgreementId, owner.UserId,
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = agreement.Revision, ExpectedCalculationDigest = preview.CalculationDigest }, default);
        var entry = Assert.Single((await settlement.GetAsync(agreement.AgreementId, owner.UserId, default)).Entries.Where(x => x.Kind == C.ActivityReward));
        Assert.Equal(C.ActivityReward, entry.Kind); Assert.Equal(expectedReward, entry.AmountKrw); Assert.Equal(C.Accrued, entry.StatusCode);
        Assert.Equal(DistributedTestFixture.Driver.UserId, entry.PayeeUserId); Assert.Equal(preview.CalculationDigest, entry.ActivityRewardCalculationDigest);
        Assert.Equal(preview.CalculationDigest, reward.ActivityRewardCalculation!.CalculationDigest);
        Assert.Equal(verifySeparatedSettlement ? 2 : 0, await f.Db.Set<NeighborhoodSettlementPaymentRecord>().CountAsync());
        Assert.Empty(await f.Db.기사운송대금지급요청.ToArrayAsync());
        if (verifyEconomics)
        {
            Assert.Equal(2, await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
            Assert.Equal(expectedPlatform, (await f.Db.PlatformRevenueEntries.SingleAsync()).PlatformRevenueAmount);
            Assert.Equal(200, (await settlement.GetAsync(inboundAgreementId!, owner.UserId, default)).Entries.Single(x => x.Kind == C.Inbound).AmountKrw);
        }
    }

    private static NeighborhoodSettlementAgreementRequest Terms(string orderId, string owner) => new()
    {
        RequestId = Guid.NewGuid(), SourceKind = C.DistributedOrder, SourceId = orderId, ActivityKind = C.StoredGoods,
        GoodsOwnerUserId = owner, SellerUserId = SellerA.UserId,
        Lines = [new() { LineKey = "basic-pick", Kind = C.Picking, Billing = C.PerUnit, Quantity = 2, UnitPriceKrw = 100,
            PayerUserId = owner, PayeeUserId = DistributedTestFixture.Driver.UserId, BeneficiaryRole = "Picker" },
            new() { LineKey = "basic-delivery", Kind = C.Delivery, Billing = C.Fixed, AmountKrw = 15000,
                PayerUserId = owner, PayeeUserId = DistributedTestFixture.Driver.UserId, BeneficiaryRole = "Driver" }],
        ActivityRewardTerms = new()
        {
            PayerUserId = owner, ParticipantUserIds = [DistributedTestFixture.Driver.UserId],
            Policy = new() { PolicyVersion = "integration-agreed-v1", ReturnRatePercent = 10,
                Weights = [new() { Kind = C.Picking, UnitCode = R.Unit, Weight = 1 }, new() { Kind = C.Delivery, UnitCode = R.Unit, Weight = 1 },
                    // 주문별 귀속이 없는 입고·보관은 사전 합의에서 명시적으로 제외합니다.
                    new() { Kind = C.Inbound, UnitCode = R.Unit, Weight = 0 }, new() { Kind = C.Storage, UnitCode = R.StorageDay, Weight = 0 }] },
            FinancialBasis = new() { BasisKind = R.SimulationPartyStatement, BasisReference = "integration-party-statement-v1",
                RevenueAmountKrw = 20000, BasicWorkCostAmountKrw = 15200, OtherCostAmountKrw = 1000, ReserveAmountKrw = 1000 }
        }
    };

    private static async Task AssertWaitingForReceipt(NeighborhoodSettlementUseCase settlement, string agreementId, string owner)
    {
        var waiting = await settlement.ActivityRewardPreviewAsync(agreementId, owner, default);
        Assert.Equal(R.WaitingForEvidence, waiting.StatusCode); Assert.Contains("ActivityRewardCustomerReceiptRequired", waiting.ReasonCodes);
    }
    private static async Task<IReadOnlyList<string>> Upload(DistributedTestFixture f, DistributedPhotoEvidenceService service,
        DistributedActor actor, DistributedEvidenceWorkRequest work)
    {
        using var bitmap = new SKBitmap(16, 16); bitmap.Erase(SKColors.ForestGreen);
        using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        var binding = await f.Use(actor).AuthorizeEvidenceAsync(actor, work, default);
        var saved = await service.UploadAsync(actor, binding, new() { UploadRequestId = Guid.NewGuid(), Work = work,
            ContentType = "image/jpeg", ImageBase64 = Convert.ToBase64String(bytes.ToArray()) }, default);
        return [saved.EvidenceId];
    }
    private sealed class PhotoEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ssalddel.Tests";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine("artifacts", "local", "activity-reward-warehouse-integration", Guid.NewGuid().ToString("N")));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
