using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.PrivacySupport;
using Ssalddel.Services.Storage;
using 살뜰.Services.Options;
using 살뜰.도메인.창고;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class DistributedPhotoStorageTests
{
    [Fact]
    public async Task 원업로드번호조회는_본인사진의현재권한을확인하고_새사진이나완료기록을만들지않는다()
    {
        await using var f = await Create(); var setup = await Setup(f); var service = Service(f, out _);
        var binding = await f.Use(Operator).AuthorizeEvidenceAsync(Operator, setup.Work, default); var upload = Upload(setup.Work);
        var image = await service.UploadAsync(Operator, binding, upload, default);
        var own = await service.GetByUploadAsync(Operator, upload.UploadRequestId, default);
        Assert.Equal(image.EvidenceId, own.EvidenceId); Assert.Equal(setup.Request.RequestId, own.CompletionRequestId);
        await Error("DistributedPhotoNotFound", () => service.GetByUploadAsync(SellerA, upload.UploadRequestId, default));
        await Error("DistributedPhotoNotFound", () => service.GetByUploadAsync(Operator, Guid.NewGuid(), default));
        Assert.Equal(1, await f.Db.Set<분산사진증빙Record>().CountAsync());
        Assert.Equal(0, await f.Db.Set<분산물류CommandReceipt>().CountAsync(x => x.Kind == "ReceiveBatch"));
    }

    [Fact]
    public async Task 실제배정이해제된기사는남아있는순회상태만으로사진과사진메타데이터를조회할수없다()
    {
        await using var f = await Create(); var wave = await f.Round(); var workspace = await f.Use(DistributedTestFixture.Driver).WorkspaceAsync(DistributedTestFixture.Driver, "driver", default);
        var stop = wave.Stops.Single(x => x.StopId == wave.CurrentStopId); f.Clock.Now = stop.ExpectedArrivalUtc;
        var item = wave.Reservations.First(x => x.SiteId == stop.SiteId && stop.OrderIds.Contains(x.OrderId));
        var location = workspace.Sites.Single(x => x.SiteId == item.SiteId).Locations.Single(x => x.LocationId == item.LocationId);
        var request = new DistributedPickBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId, LocationBarcode = location.Barcode,
            Lines = [new() { ReservationId = item.ReservationId, OwnerUserId = item.OwnerUserId, Sku = item.Sku, Quantity = 1 }] };
        var work = new DistributedEvidenceWorkRequest { Kind = "Pick", AggregateId = wave.WaveId, Pick = request };
        var service = Service(f, out _); var binding = await f.Use(DistributedTestFixture.Driver).AuthorizeEvidenceAsync(DistributedTestFixture.Driver, work, default);
        var upload = Upload(work); var photo = await service.UploadAsync(DistributedTestFixture.Driver, binding, upload, default);
        Assert.True((await service.GetAsync(DistributedTestFixture.Driver, photo.EvidenceId, default)).ReadAllowed); Assert.NotEmpty((await service.ContentAsync(DistributedTestFixture.Driver, photo.EvidenceId, default)).Bytes);
        var cargoId = wave.Orders.Single(x => x.OrderId == item.OrderId).CargoRequestId;
        await f.Db.운송원장.Where(x => x.의뢰Id == cargoId).ExecuteUpdateAsync(s => s.SetProperty(x => x.확정기사Id, "another-driver"));
        Assert.Equal("Accepted", (await f.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync()).StatusCode);
        await Error("DistributedPhotoNotFound", () => service.GetAsync(DistributedTestFixture.Driver, photo.EvidenceId, default));
        await Error("DistributedPhotoNotFound", () => service.ContentAsync(DistributedTestFixture.Driver, photo.EvidenceId, default));
        await Error("DistributedPhotoNotFound", () => service.GetByUploadAsync(DistributedTestFixture.Driver, upload.UploadRequestId, default));
    }
    [Fact]
    public async Task 실제비공개사진업로드는같은요청으로중복되지않고업무완료영수증과동일트랜잭션에결속된다()
    {
        await using var f = await Create(); var setup = await Setup(f); var service = Service(f, out var environment);
        var binding = await f.Use(Operator).AuthorizeEvidenceAsync(Operator, setup.Work, default); var upload = Upload(setup.Work);
        var image = await service.UploadAsync(Operator, binding, upload, default); var again = await service.UploadAsync(Operator, binding, upload, default);
        Assert.Equal(image.EvidenceId, again.EvidenceId); Assert.Equal("Uploaded", image.StatusCode);
        Assert.Single(Directory.GetFiles(Path.Combine(environment.ContentRootPath, DevelopmentLocalStorageService.PrivateStorageDirectoryName), "*.jpg", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(environment.ContentRootPath, DevelopmentLocalStorageService.PublicStorageDirectoryName)));
        setup.Request.PhotoEvidenceIds = [image.EvidenceId]; var ack = await f.Use(Operator, binder: service).ReceiveBatchAsync(Operator, setup.Plan, setup.Request, default);
        Assert.True((await f.Use(Operator, binder: service).ReceiveBatchAsync(Operator, setup.Plan, setup.Request, default)).Replayed);
        var row = await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync(); Assert.Equal("Bound", row.StatusCode); Assert.Equal(ack.RequestId, row.CompletionRequestId);
        Assert.Equal(2, (await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).ReceivedQuantity);
        Assert.Equal(1, await f.Db.Set<분산물류CommandReceipt>().CountAsync(x => x.Kind == "ReceiveBatch"));
    }
    [Fact]
    public async Task 실제사진의다른기사와다른수량에대한사용과파일변조를막는다()
    {
        await using var f = await Create(); var setup = await Setup(f); var service = Service(f, out var environment);
        var binding = await f.Use(Operator).AuthorizeEvidenceAsync(Operator, setup.Work, default); var image = await service.UploadAsync(Operator, binding, Upload(setup.Work), default);
        await using (var tx = await f.Db.Database.BeginTransactionAsync())
        {
            await Error("DistributedPhotoBindingMismatch", () => service.BindAsync(binding with { ActorUserId = "other-actor" }, [image.EvidenceId], default));
            await Error("DistributedPhotoBindingMismatch", () => service.BindAsync(binding with { InputDigest = "different-count" }, [image.EvidenceId], default));
            await tx.RollbackAsync();
        }
        var file = Assert.Single(Directory.GetFiles(Path.Combine(environment.ContentRootPath, DevelopmentLocalStorageService.PrivateStorageDirectoryName), "*.jpg", SearchOption.AllDirectories));
        await File.WriteAllBytesAsync(file, [1, 2, 3]); setup.Request.PhotoEvidenceIds = [image.EvidenceId];
        await Error("DistributedPhotoStorageIntegrityMismatch", () => f.Use(Operator, binder: service).ReceiveBatchAsync(Operator, setup.Plan, setup.Request, default));
        Assert.Equal("Uploaded", (await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync()).StatusCode);
        Assert.Equal(0, (await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).ReceivedQuantity);
    }
    [Fact]
    public async Task 사진소비뒤입고실패는증빙소비와수량을함께롤백하고같은사진으로복구한다()
    {
        await using var f = await Create(); var setup = await Setup(f); var service = Service(f, out _);
        var binding = await f.Use(Operator).AuthorizeEvidenceAsync(Operator, setup.Work, default); var image = await service.UploadAsync(Operator, binding, Upload(setup.Work), default);
        setup.Request.PhotoEvidenceIds = [image.EvidenceId]; var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync();
        await f.Db.입고상품.Where(x => x.Id == lot.InboundItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.소유자UserId, "wrong-owner"));
        await Error("DistributedInventoryConflict", () => f.Use(Operator, binder: service).ReceiveBatchAsync(Operator, setup.Plan, setup.Request, default));
        Assert.Equal("Uploaded", (await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync()).StatusCode);
        await f.Db.입고상품.Where(x => x.Id == lot.InboundItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.소유자UserId, "goods-owner-a"));
        await f.Use(Operator, binder: service).ReceiveBatchAsync(Operator, setup.Plan, setup.Request, default);
        Assert.Equal("Bound", (await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync()).StatusCode);
    }
    [Fact]
    public async Task 미완료실제사진은24시간이후정리되고남은실재고사진은오래돼도유지한다()
    {
        await using var f = await Create(); var setup = await Setup(f); var service = Service(f, out _); var support = new Support(false);
        var binding = await f.Use(Operator).AuthorizeEvidenceAsync(Operator, setup.Work, default); var image = await service.UploadAsync(Operator, binding, Upload(setup.Work), default);
        f.Clock.Now = f.Clock.Now.AddHours(23); Assert.Equal(0, await service.SweepAsync(support, default));
        f.Clock.Now = f.Clock.Now.AddHours(2); Assert.Equal(1, await service.SweepAsync(support, default));
        var deleted = await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync(); Assert.True(deleted.ActiveObjectAbsent); Assert.True(deleted.HistoricalCopiesVerifiedAbsent); Assert.Equal("Deleted", deleted.StatusCode);
        var next = setup.Request; next.RequestId = Guid.NewGuid(); setup.Work.Receive = next;
        binding = await f.Use(Operator).AuthorizeEvidenceAsync(Operator, setup.Work, default); image = await service.UploadAsync(Operator, binding, Upload(setup.Work), default);
        next.PhotoEvidenceIds = [image.EvidenceId]; await f.Use(Operator, binder: service).ReceiveBatchAsync(Operator, setup.Plan, next, default);
        f.Clock.Now = f.Clock.Now.AddDays(40); Assert.Equal(0, await service.SweepAsync(support, default));
        var preserved = await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync(x => x.EvidenceId == image.EvidenceId);
        Assert.Equal("Bound", preserved.StatusCode); Assert.Null(preserved.CustodyEndedAtUtc);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    [InlineData(false)]
    public async Task 보관종료후30일이지나도열린사건이나사건조회미확인상태면사진을삭제하지않는다(bool? open)
    {
        await using var f = await Create(); var setup = await Setup(f); var service = Service(f, out _);
        var binding = await f.Use(Operator).AuthorizeEvidenceAsync(Operator, setup.Work, default); var image = await service.UploadAsync(Operator, binding, Upload(setup.Work), default);
        setup.Request.PhotoEvidenceIds = [image.EvidenceId]; await f.Use(Operator, binder: service).ReceiveBatchAsync(Operator, setup.Plan, setup.Request, default);
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync();
        // Explicit fixture custody termination; production stock decrements are covered by the real cargo workflow tests.
        await f.Db.입고상품.Where(x => x.Id == lot.InboundItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, 0));
        Assert.Equal(0, await service.SweepAsync(new Support(false), default));
        var preserved = await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync(); Assert.Equal(f.Clock.Now.AddDays(30), preserved.DeleteAfterUtc);
        f.Clock.Now = f.Clock.Now.AddDays(31);
        Assert.Equal(open == false ? 1 : 0, await service.SweepAsync(new Support(open), default));
        Assert.Equal(open == false ? "Deleted" : "Bound", (await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync()).StatusCode);
    }
    private static async Task<(string Plan, DistributedInboundReceiptBatchRequest Request, DistributedEvidenceWorkRequest Work)> Setup(DistributedTestFixture f)
    {
        var site = (await f.Sites())[0]; await f.EnsureAgreement(site, new("goods-owner-a"), "same-sku");
        var plan = await f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), SourceReference = "actual-photo-test",
            Lines = [new() { LineKey = "first", SourceInboundId = 101, SiteId = site.SiteId, LocationId = site.Locations.Single().LocationId, Sku = "same-sku", ProductName = "stock", LotCode = "first", ExpectedQuantity = 5 }] }, default);
        var request = new DistributedInboundReceiptBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, LocationBarcode = site.Locations.Single().Barcode, Lines = [new() { LineKey = "first", Sku = "same-sku", Quantity = 2 }] };
        return (plan.AggregateId, request, new() { Kind = "Receive", AggregateId = plan.AggregateId, Receive = request });
    }
    private static DistributedPhotoUploadRequest Upload(DistributedEvidenceWorkRequest work)
    {
        using var bitmap = new SKBitmap(16, 16); bitmap.Erase(SKColors.ForestGreen); using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return new() { UploadRequestId = Guid.NewGuid(), Work = work, ContentType = "image/jpeg", ImageBase64 = Convert.ToBase64String(bytes.ToArray()) };
    }
    private static DistributedPhotoEvidenceService Service(DistributedTestFixture f, out Environment environment)
    {
        environment = new(); var storage = new DevelopmentLocalStorageService(environment, new HttpContextAccessor());
        return new(f.Db, storage, new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            Options.Create(new DistributedFulfillmentOptions { Enabled = true, Profile = DistributedFulfillmentCodes.Profile }), f.Clock);
    }
    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ssalddel.Tests";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine("artifacts", "local", "distributed-proof-r37-20261008", "server", "actual-photo", Guid.NewGuid().ToString("N")));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Support(bool? open) : I보호지원Store
    {
        public Task<bool?> HasOpenCaseAsync(string kind, string id, CancellationToken ct = default) => Task.FromResult(open);
        public Task<보호지원Record?> 조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 생성Async(보호지원Record record, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(보호지원Record record, long revision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 목록Async(string? actor, string? kind, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime now, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 파기Async(보호지원Record record, DateTime now, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private static async Task Error(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<DistributedFulfillmentException>(action)).Code);
}
