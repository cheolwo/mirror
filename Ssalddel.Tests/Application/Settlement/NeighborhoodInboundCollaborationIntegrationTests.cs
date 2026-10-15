using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Ssalddel.Application.Settlement;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Storage;
using Ssalddel.Tests.Application.Warehouse;
using 살뜰.Services.Options;
using 살뜰.도메인.창고;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class NeighborhoodInboundCollaborationIntegrationTests
{
    [Fact]
    public async Task 승인예약은보수가아니며사진으로받은실제수량만입고정산에연결한다()
    {
        await using var f = await Create();
        var site = (await f.Sites())[0]; var location = site.Locations.Single(); var owner = new DistributedActor("goods-owner-a");
        await f.Use(Operator).SiteAcceptancePolicyAsync(Operator, site.SiteId, new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = site.Revision,
            Policy = new() { AllowedCategoryCodes = [DistributedInboundCodes.Industrial],
                AllowedPackagingCodes = [DistributedInboundCodes.Boxed], AllowedTemperatureCodes = [DistributedInboundCodes.Ambient],
                CapacityUnit = "box", MaximumCapacity = 10, AccessHours = "합성 시험 출입 협의" }
        }, default);
        await f.EnsureAgreement(site, owner, "same-sku");
        var agreement = await f.Db.Set<분산보관합의Record>().AsNoTracking().SingleAsync();
        var inbound = await f.Use(SellerA).InboundAsync(SellerA, new()
        {
            RequestId = Guid.NewGuid(), SourceReference = "collaboration-integration",
            Lines = [new() { LineKey = "first", SourceInboundId = 101, SiteId = site.SiteId, LocationId = location.LocationId,
                Sku = "same-sku", ProductName = "합성 공산품", LotCode = "integration", ExpectedQuantity = 3 }]
        }, default);
        inbound = await f.Use(SellerA).InboundProposalAsync(SellerA, inbound.AggregateId, "first", new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = inbound.Revision,
            Terms = new() { AgreementId = agreement.AgreementId, AgreementTermsVersion = agreement.TermsVersion,
                SupplierUserId = SellerA.UserId, SellerUserId = SellerA.UserId, GoodsOwnerUserId = owner.UserId, SupplierTypeCode = "factory",
                Goods = new() { CategoryCodes = [DistributedInboundCodes.Industrial], IsFood = false, IsProcessed = true,
                    PackagingCode = DistributedInboundCodes.Boxed, TemperatureCode = DistributedInboundCodes.Ambient,
                    CapacityUnit = "box", CapacityPerItem = 1 }, Quantity = 3,
                ArrivalStartUtc = f.Clock.Now.AddMinutes(-1), ArrivalEndUtc = f.Clock.Now.AddHours(1), StorageEndsAtUtc = f.Clock.Now.AddDays(1),
                AccessHoursConfirmed = true, SupplyPriceOrFreeTerms = "공급대금 별도 협의", SupplierResponsibilityTerms = "합성 수량 확인",
                ReturnTerms = "합성 반품 협의" }
        }, default);
        foreach (var actor in new[] { owner, Operator })
            inbound = await f.Use(actor).InboundConsentAsync(actor, inbound.AggregateId, "first", new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = inbound.Revision, TermsVersion = 1, Granted = true }, default);
        Assert.Equal("Confirmed", inbound.StatusCode);
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync();
        var resolver = new NeighborhoodSettlementSourceResolver(f.Db, f.Clock);
        var reserved = await resolver.ResolveAsync(C.DistributedLot, lot.LotId, default);
        Assert.Empty(reserved.CompletedWork);
        Assert.False(reserved.Workflow!.PaymentReleaseReady);
        Assert.Equal(0, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId)).가용수량);

        var execution = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
        var photos = new DistributedPhotoEvidenceService(f.Db,
            new DevelopmentLocalStorageService(new PhotoEnvironment(), new HttpContextAccessor()), execution,
            Options.Create(new DistributedFulfillmentOptions { Enabled = true, Profile = DistributedFulfillmentCodes.Profile }), f.Clock);
        var receive = new DistributedInboundReceiptBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = inbound.Revision,
            LocationBarcode = location.Barcode, Lines = [new() { LineKey = "first", Sku = "same-sku", Quantity = 2 }] };
        var proof = new DistributedEvidenceWorkRequest { Kind = "Receive", AggregateId = inbound.AggregateId, Receive = receive };
        using var bitmap = new SKBitmap(16, 16); bitmap.Erase(SKColors.ForestGreen);
        using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        var binding = await f.Use(Operator).AuthorizeEvidenceAsync(Operator, proof, default);
        var saved = await photos.UploadAsync(Operator, binding, new() { UploadRequestId = Guid.NewGuid(), Work = proof,
            ContentType = "image/jpeg", ImageBase64 = Convert.ToBase64String(bytes.ToArray()) }, default);
        receive.PhotoEvidenceIds = [saved.EvidenceId];
        var received = await f.Use(Operator, binder: photos).ReceiveBatchAsync(Operator, inbound.AggregateId, receive, default);
        Assert.Equal("PartiallyReceived", received.StatusCode);
        var replay = await f.Use(Operator, binder: photos).ReceiveBatchAsync(Operator, inbound.AggregateId, receive, default);
        Assert.Equal(received.Revision, replay.Revision);
        await using (var other = f.IndependentContext())
        {
            var stock = await other.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId);
            Assert.Equal(2, stock.가용수량); Assert.Equal(2, stock.입고수량);
            Assert.Equal(2, (await other.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).ReceivedQuantity);
        }
        var source = await resolver.ResolveAsync(C.DistributedLot, lot.LotId, default);
        var work = Assert.Single(source.CompletedWork, x => x.Kind == C.Inbound);
        Assert.Equal(2, work.Quantity); Assert.NotEmpty(work.EvidenceReferences); Assert.NotEmpty(source.StorageIntervals);
        var settlement = new NeighborhoodSettlementUseCase(f.Db, resolver, execution,
            Options.Create(new NeighborhoodSettlementOptions { Enabled = true, Profile = NeighborhoodSettlementOptions.RequiredProfile }), f.Clock);
        var terms = await settlement.CreateAsync(owner.UserId, new()
        {
            RequestId = Guid.NewGuid(), SourceKind = C.DistributedLot, SourceId = lot.LotId, ActivityKind = C.StoredGoods,
            Lines = [new() { LineKey = "inbound", Kind = C.Inbound, Billing = C.PerUnit, Quantity = 3, UnitPriceKrw = 100,
                PayerUserId = owner.UserId, PayeeUserId = Operator.UserId, BeneficiaryRole = "Receiver" }]
        }, default);
        foreach (var actor in new[] { owner.UserId, Operator.UserId })
            terms = await settlement.ConsentAsync(terms.AgreementId, actor, new()
            { RequestId = Guid.NewGuid(), ExpectedRevision = terms.Revision, TermsVersion = 1, Granted = true }, default);
        terms = await settlement.AccrueAsync(terms.AgreementId, owner.UserId, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = terms.Revision, OriginKind = work.OriginKind, OriginId = work.OriginId,
            Lines = [new() { LineKey = "inbound", Quantity = 2 }] }, default);
        var result = await settlement.GetAsync(terms.AgreementId, owner.UserId, default);
        Assert.Equal(200, Assert.Single(result.Entries).AmountKrw);
        Assert.Empty(result.Payments);
        Assert.Empty(await f.Db.Set<분산배송주문Record>().ToArrayAsync());
    }

    private sealed class PhotoEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ssalddel.Tests";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine("artifacts", "local", "inbound-collaboration-integration", Guid.NewGuid().ToString("N")));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
