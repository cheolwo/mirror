using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Warehouse;

/// <summary>격리 SQLite와 실제 업무/배차/운송 상태 서비스를 사용한다. 경로·외부 발행만 명시적인 합성 fixture다.</summary>
internal sealed class DistributedTestFixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
{
    public static readonly DistributedActor Operator = new("site-owner");
    public static readonly DistributedActor SellerA = new("seller-a");
    public static readonly DistributedActor SellerB = new("seller-b");
    public static readonly DistributedActor Driver = new("driver", true);
    public static readonly DistributedActor CustomerA = new("customer-a");
    public static readonly DistributedActor CustomerB = new("customer-b");
    public SsalddelContext Db { get; } = db;
    public DistributedClock Clock { get; } = new();
    public DistributedLocations Locations { get; } = new();
    public DistributedRoute Routes { get; } = new();
    public RecordingPublisher Publisher { get; } = new();
    public string? FailCargoId { get; set; }
    public bool RequireVehicleAdvisory { get; set; }
    public VerifiedFixturePhotos Photos { get; } = new();
    public IDistributedFulfillmentUseCase Service => Use(Operator);
    public 분산물류UseCase Use(DistributedActor actor, bool enabled = true, SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation, string? profile = null, SsalddelContext? store = null, IDistributedPhotoEvidenceBinder? binder = null, I분산주문원장연결Adapter? orderLedgers = null)
    {
        var workingDb = store ?? Db;
        var current = new Current(actor.UserId, actor.IsDriver ? "기사" : "화주");
        var execution = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode }));
        var options = Options.Create(new DistributedFulfillmentOptions { Enabled = enabled, Profile = profile ?? DistributedFulfillmentCodes.Profile, UseExistingRouteProvider = true });
        var eligibility = new 화물배차수락적격성Service(workingDb, Locations, new 차량화물적합성Service(new 차량적재추천Engine()),
            new 기사운송일정구성Service(workingDb), new 운송일정삽입평가Service(Routes));
        var handler = new 배차수락CommandHandler(workingDb, Publisher, current, new 참여자실행권한검사(), new WorkRelationshipSnapshotCollector(),
            eligibility, new Continuity(), NullLogger<배차수락CommandHandler>.Instance);
        var executor = TransportExecutor(actor, workingDb);
        var cargo = new 분산화물연결Service(workingDb, new Sender(handler, () => FailCargoId), executor, Publisher, NullLogger<분산화물연결Service>.Instance);
        return new(workingDb, execution, options, new 분산배송경로Planner(Routes, options), cargo, Clock, Locations, new EmptyDistributedBuildingCandidateProvider(), binder ?? Photos, orderLedgers);
    }
    public I기사운송상태변경CommandExecutor TransportExecutor(DistributedActor actor, SsalddelContext? store = null)
        => new 기사운송상태변경CommandExecutor(store ?? Db, new 기사운송상태전이Service(), Publisher,
            new Current(actor.UserId, actor.IsDriver ? "기사" : "화주"), new 참여자실행권한검사(), NullLogger<기사운송상태변경CommandExecutor>.Instance);
    public Task<FluentResults.Result<Ssalddel.Contracts.Driver.Transport.기사운송상태변경응답>> GeneralTransportProgressAsync(long id, string target)
    {
        var executor = TransportExecutor(Driver); var attachments = new 운송증빙첨부JsonWriter();
        return target switch
        {
            기사운송상태코드.상차지도착 => new 운송상차지도착CommandHandler(executor).Handle(new(Driver.UserId, id), default),
            기사운송상태코드.상차완료 => new 운송상차완료CommandHandler(Db, executor, attachments).Handle(new(Driver.UserId, id) { 상차사진ObjectName = "fixture-pickup-photo" }, default),
            기사운송상태코드.하차지도착 => new 운송하차지도착CommandHandler(executor).Handle(new(Driver.UserId, id), default),
            기사운송상태코드.인수완료 => new 운송인수완료CommandHandler(executor, attachments).Handle(new(Driver.UserId, id) { 하차사진ObjectName = "fixture-dropoff-photo" }, default),
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
    }
    public static async Task<DistributedTestFixture> Create()
    {
        var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
        var d = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(c).Options, new Encryption());
        await d.Database.EnsureCreatedAsync(); var f = new DistributedTestFixture(c, d);
        f.RefreshLocation();
        d.창고.AddRange(new 창고 { Id = 11, 소유자UserId = Operator.UserId, 창고명 = "동쪽 공간", 주소 = "합성 공개도로 가", 위도 = 37, 경도 = 127 },
            new 창고 { Id = 12, 소유자UserId = Operator.UserId, 창고명 = "서쪽 공간", 주소 = "합성 공개도로 나", 위도 = 37.01m, 경도 = 127.01m });
        d.용달기사.Add(new() { 기사Id = Driver.UserId, 기사명 = "합성 기사", 차량 = "fixture-vehicle" });
        d.차량제원.Add(new() { 차량코드 = "fixture-vehicle", 차량명 = "합성 카고", 최대적재중량Kg = 1000, 운영권장중량Kg = 1000,
            권장최대CBM = 10, 팔레트적재개수 = 10, 적재함길이Mm = 3000, 적재함폭Mm = 2000, 적재함높이Mm = 2000 });
        d.입고요청.AddRange(new 입고요청 { Id = 101, 창고Id = 11, 판매자UserId = SellerA.UserId, 주문자UserId = CustomerA.UserId, 예정SKU = "same-sku", 예정상품명 = "A 기업 물품", 예정수량 = 10 },
            new 입고요청 { Id = 102, 창고Id = 11, 판매자UserId = SellerB.UserId, 주문자UserId = CustomerB.UserId, 예정SKU = "same-sku", 예정상품명 = "B 기업 물품", 예정수량 = 10 });
        // Seller A is a delegated seller; the actual stock owner is deliberately different.
        d.입고상품.Add(new() { 입고요청Id = 101, 창고Id = 11, 소유자UserId = "goods-owner-a", 판매자UserId = SellerA.UserId,
            SKU = "same-sku", 상품명 = "A 기업 물품", 입고수량 = 0, 가용수량 = 0 });
        d.살뜰참여자.AddRange(new 살뜰.도메인.사용자.살뜰참여자 { Id = "goods-owner-a", 표시이름 = "A 기업 소유자" },
            new 살뜰.도메인.사용자.살뜰참여자 { Id = SellerB.UserId, 표시이름 = "B 기업 소유자" });
        await d.SaveChangesAsync(); d.ChangeTracker.Clear(); return f;
    }
    public void RefreshLocation() => Locations.Upsert(new(Driver.UserId, 37, 127, 5, "대기", Clock.Now, Clock.Now));
    public SsalddelContext IndependentContext() => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
    public async Task Export(string role, DistributedActor actor, string stage)
    {
        var directory = Environment.GetEnvironmentVariable("DISTRIBUTED_FIXTURE_EXPORT_DIR"); if (string.IsNullOrWhiteSpace(directory)) return;
        directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory);
        var workspace = await Use(actor).WorkspaceAsync(actor, role, default);
        await File.WriteAllTextAsync(Path.Combine(directory, role + "-" + stage + ".json"), System.Text.Json.JsonSerializer.Serialize(workspace, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
    public async Task<DistributedSiteDto[]> Sites()
    {
        foreach (var warehouseId in new long[] { 11, 12 })
        {
            var created = await Use(Operator).SiteAsync(Operator, new() { RequestId = Guid.NewGuid(), WarehouseId = warehouseId,
                PlaceId = "warehouse:" + warehouseId, PublicLabel = warehouseId == 11 ? "동쪽 공간" : "서쪽 공간", ManagerUserId = Operator.UserId,
                Floor = "지하", Subspace = "합성 구역", PickupInstructions = "합성 출입 안내", SpaceConfirmed = true, PickupPointConfirmed = true }, default);
            await Use(Operator).ConsentAsync(Operator, created.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = 1, ConsentKind = "Owner", Granted = true, EvidenceReference = "fixture-explicit-owner-consent" }, default);
            await Use(Operator).ConsentAsync(Operator, created.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = 2, ConsentKind = "Manager", Granted = true, EvidenceReference = "fixture-explicit-manager-consent" }, default);
            await Use(Operator).LocationAsync(Operator, created.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = 3, LocationLabel = "선반" }, default);
        }
        return (await Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).Sites.OrderBy(x => x.WarehouseId).ToArray();
    }
    public async Task<DistributedLotDto[]> Stock(DistributedSiteDto[] sites)
    {
        foreach (var seller in new[] { SellerA, SellerB })
        {
            foreach (var site in sites) await EnsureAgreement(site, new(seller == SellerA ? "goods-owner-a" : seller.UserId), "same-sku");
            var sourceId = seller == SellerA ? 101 : 102;
            var plan = await Use(seller).InboundAsync(seller, new() { RequestId = Guid.NewGuid(), SourceReference = "fixture-source-" + sourceId,
                Lines = sites.Select((site, index) => new DistributedInboundLineRequest { LineKey = "line-" + index, SourceInboundId = sourceId,
                    SiteId = site.SiteId, LocationId = site.Locations.Single().LocationId, Sku = "same-sku", ProductName = seller.UserId,
                    LotCode = "fixture-lot-" + sourceId + "-" + index, ExpectedQuantity = 5, StockOwnerIsCurrentSellerConfirmed = seller == SellerB,
                    OwnershipEvidenceReference = seller == SellerB ? "fixture-explicit-self-ownership" : "" }).ToArray() }, default);
            var revision = plan.Revision;
            for (var index = 0; index < sites.Length; index++)
                revision = (await Use(Operator).ReceiveAsync(Operator, plan.AggregateId, await Proof(Operator, plan.AggregateId, new DistributedInboundReceiptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = revision,
                    LineKey = "line-" + index, LocationBarcode = sites[index].Locations.Single().Barcode, Sku = "same-sku", Quantity = 5, EvidenceReference = "fixture-actual-count" }), default)).Revision;
        }
        return (await Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).Lots.ToArray();
    }
    public async Task EnsureAgreement(DistributedSiteDto site, DistributedActor goodsOwner, string sku)
    {
        var existing = await Db.Set<분산보관합의Record>().SingleOrDefaultAsync(x => x.LocationId == site.Locations.Single().LocationId && x.GoodsOwnerUserId == goodsOwner.UserId);
        if (existing is not null) return;
        var ack = await Use(Operator).AgreementAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), LocationId = site.Locations.Single().LocationId,
            GoodsOwnerUserId = goodsOwner.UserId, Terms = new() { AllowedSkus = [sku], MaximumStoredQuantity = 100,
                StartsAtUtc = Clock.Now.AddDays(-1), EndsAtUtc = Clock.Now.AddDays(3), AccessHours = "fixture-allowed-hours", PriceOrFreeTerms = "fixture-free",
                CustodianAndCare = "fixture-named-custodian", LossDamageAndReturnTerms = "fixture-explicit-return-terms" } }, default);
        ack = await Use(Operator).AgreementConsentAsync(Operator, ack.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = ack.Revision, TermsVersion = 1, Granted = true }, default);
        if (goodsOwner.UserId != Operator.UserId) await Use(goodsOwner).AgreementConsentAsync(goodsOwner, ack.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = ack.Revision, TermsVersion = 1, Granted = true }, default);
    }
    public async Task<T> Proof<T>(DistributedActor actor, string aggregate, T request) where T : DistributedCommandRequest
    {
        DistributedEvidenceWorkRequest work = request switch
        {
            DistributedInboundReceiptRequest r => new() { Kind = "Receive", AggregateId = aggregate, Receive = new() { RequestId = r.RequestId, ExpectedRevision = r.ExpectedRevision, LocationBarcode = r.LocationBarcode,
                Lines = [new() { LineKey = r.LineKey, Sku = r.Sku, Quantity = r.Quantity }] } },
            DistributedPickRequest r => new() { Kind = "Pick", AggregateId = aggregate, Pick = new() { RequestId = r.RequestId, ExpectedRevision = r.ExpectedRevision, StopId = r.StopId, LocationBarcode = r.LocationBarcode,
                Lines = [new() { ReservationId = r.ReservationId, OwnerUserId = r.OwnerUserId, Sku = r.Sku, Quantity = r.Quantity }] } },
            DistributedDeliveryRequest r => new() { Kind = "Deliver", AggregateId = aggregate, Deliver = new() { RequestId = r.RequestId, ExpectedRevision = r.ExpectedRevision, StopId = r.StopId,
                Lines = (await Db.Set<분산재고예약Record>().Where(x => x.WaveId == aggregate && x.OrderId == r.OrderId).ToArrayAsync()).Select(x => new DistributedDeliveryLineRequest { ReservationId = x.ReservationId, Quantity = x.Quantity - x.DeliveredQuantity - x.ReturnedQuantity }).ToArray() } },
            DistributedReturnRequest r => new() { Kind = "Return", AggregateId = aggregate, Return = new() { RequestId = r.RequestId, ExpectedRevision = r.ExpectedRevision, LocationBarcode = r.LocationBarcode,
                Lines = [new() { ReservationId = r.ReservationId, Quantity = r.Quantity }] } },
            DistributedInboundReceiptBatchRequest r => new() { Kind = "Receive", AggregateId = aggregate, Receive = r },
            DistributedPickBatchRequest r => new() { Kind = "Pick", AggregateId = aggregate, Pick = r },
            DistributedDeliveryBatchRequest r => new() { Kind = "Deliver", AggregateId = aggregate, Deliver = r },
            DistributedReturnBatchRequest r => new() { Kind = "Return", AggregateId = aggregate, Return = r },
            DistributedExistingLotRequest r => new() { Kind = "ImportLot", AggregateId = aggregate, ImportLot = r },
            _ => throw new InvalidOperationException("unsupported fixture photo request")
        };
        var binding = await Use(actor).AuthorizeEvidenceAsync(actor, work, default); var ids = Photos.Register(binding);
        request.GetType().GetProperty("PhotoEvidenceIds")!.SetValue(request, ids); return request;
    }
    public sealed class VerifiedFixturePhotos : IDistributedPhotoEvidenceBinder
    {
        private readonly Dictionary<string, (string Actor, string Digest)> uploads = new(StringComparer.Ordinal);
        public IReadOnlyList<string> Register(DistributedEvidenceBinding binding)
        {
            var id = "fixture-photo-" + Guid.NewGuid().ToString("N"); uploads[id] = (binding.ActorUserId, DistributedEvidenceDigests.Binding(binding)); return new[] { id };
        }
        public Task BindAsync(DistributedEvidenceBinding binding, IReadOnlyList<string> ids, CancellationToken ct)
        {
            foreach (var id in ids)
            {
                if (!uploads.TryGetValue(id, out var proof)) throw new DistributedFulfillmentException("DistributedPhotoEvidenceNotFound", 404);
                if (proof.Actor != binding.ActorUserId) throw new DistributedFulfillmentException("DistributedPhotoEvidenceActorMismatch", 403);
                if (proof.Digest != DistributedEvidenceDigests.Binding(binding)) throw new DistributedFulfillmentException("DistributedPhotoEvidenceBindingMismatch");
            }
            return Task.CompletedTask;
        }
        public Task AttachImportedLineageAsync(Guid requestId, string actor, string planId, IReadOnlyList<string> lots, CancellationToken ct) => Task.CompletedTask;
    }
    public async Task Cargo(string id, DistributedActor seller, DistributedActor customer, int quantity = 2)
    {
        var fare = new 살뜰.도메인.운송.운임구성 { 의뢰Id = id, 기사지급예정운임 = 15000, 예상거리Km = 3, 단가출처 = "synthetic-test-fixture", 최종운임 = 15000 };
        Db.운임구성.Add(fare); await Db.SaveChangesAsync();
        Db.화주운송의뢰.Add(new() { 의뢰Id = id, 화주Id = seller.UserId, 주문자UserId = customer.UserId, 화물종류 = "일반 상온 물품", 차량종류 = "fixture-vehicle",
            상태 = 상태값.의뢰상태.생성됨, 결제상태 = 상태값.결제상태.결제대기, 정산시점 = "현장지급", 정산상태 = "현장수금예정", 운임구성Id = fare.Id,
            화물수량 = quantity, 화물중량Kg = 10, 화물부피Cbm = 0.1m, 화물팔레트개수 = 0, 화물온도조건 = "상온", 화물길이Mm = RequireVehicleAdvisory ? 100 : null,
            픽업_도로명주소 = "합성 공개도로 가", 픽업_위도 = 37, 픽업_경도 = 127, 픽업_시간창_시작일시 = Clock.Now, 픽업_시간창_종료일시 = Clock.Now.AddHours(5),
            하차_도로명주소 = customer == CustomerA ? "합성 고객주소 A" : "합성 고객주소 B", 하차_위도 = customer == CustomerA ? 37.02m : 37.03m,
            하차_경도 = 127.02m, 하차_시간창_시작일시 = Clock.Now, 하차_시간창_종료일시 = Clock.Now.AddHours(5) });
        Db.운송원장.Add(new() { 의뢰Id = id, 배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중, 현재추천대상기사Id = Driver.UserId, 추천만료시각 = DateTime.UtcNow.AddHours(1) });
        await Db.SaveChangesAsync(); Db.ChangeTracker.Clear();
    }
    public async Task<DistributedWaveDto> Round(bool accept = true, int quantityPerLot = 1)
    {
        var sites = await Sites(); var lots = await Stock(sites);
        var wave = await Use(Operator).CreateWaveAsync(Operator, new() { RequestId = Guid.NewGuid(), Label = "합성 시간대",
            CutoffUtc = Clock.Now.AddMinutes(1), PickupStartUtc = Clock.Now.AddMinutes(2), PickupEndUtc = Clock.Now.AddHours(3),
            DeliveryStartUtc = Clock.Now.AddMinutes(2), DeliveryEndUtc = Clock.Now.AddHours(4), PickupServiceMinutes = 1, DeliveryServiceMinutes = 1 }, default);
        var revision = wave.Revision;
        foreach (var pair in new[] { (SellerA, CustomerA, "cargo-a"), (SellerB, CustomerB, "cargo-b") })
        {
            await Cargo(pair.Item3, pair.Item1, pair.Item2, quantityPerLot * 2);
            revision = (await Use(pair.Item1).OrderAsync(pair.Item1, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = revision,
                OrderReference = pair.Item3, CargoRequestId = pair.Item3, Lines = lots.Where(x => x.SellerUserId == pair.Item1.UserId).Select(x => new DistributedOrderLineRequest { LotId = x.LotId, Quantity = quantityPerLot }).ToArray() }, default)).Revision;
        }
        revision = (await Use(Operator).CloseAsync(Operator, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = revision }, default)).Revision;
        RefreshLocation(); revision = (await Use(Operator).PlanAsync(Operator, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = revision, DriverId = Driver.UserId }, default)).Revision;
        if (accept) await Use(Driver).AcceptAsync(Driver, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = revision,
            AcknowledgedWarningCodes = [] }, default);
        return await Use(Operator).WaveAsync(Operator, wave.AggregateId, "warehouse", default);
    }
    public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? v) => v; public string? Unprotect(string? v) => v; }
    private sealed record Current(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Sender(배차수락CommandHandler handler, Func<string?> fail) : ISender
    {
        public async Task<T> Send<T>(IRequest<T> request, CancellationToken ct = default)
        {
            if (request is not 배차수락Command cmd) throw new NotSupportedException();
            object result = cmd.RequestId == fail() ? FluentResults.Result.Fail<배차수락결과>("fixture assignment failure") : await handler.Handle(cmd, ct);
            return (T)result;
        }
        public Task Send<T>(T request, CancellationToken ct = default) where T : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Continuity : I화물연속배차UseCase
    {
        public Task<화물연속배차상태Dto> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물연속배차상태Dto { 기사Id = id });
        public Task<화물연속배차상태Dto> 의사변경Async(string id, 화물연속배차의사변경요청 r, CancellationToken ct = default) => 조회Async(id, ct);
        public Task<화물다음콜예약Dto?> 추천탐색Async(string id, CancellationToken ct = default) => Task.FromResult<화물다음콜예약Dto?>(null);
        public Task<화물예약검증결과> 수락예약검증Async(string id, string work, string? reservation, long? rev, CancellationToken ct = default) => Task.FromResult(new 화물예약검증결과(true));
        public Task 수락완료Async(string id, string work, CancellationToken ct = default) => Task.CompletedTask;
        public Task 시간약속잠금Async(string id, 화주운송의뢰 r, CancellationToken ct = default) => Task.CompletedTask;
        public Task 완료기록Async(string id, DateTime at, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<화물운송시간약속Dto>>([]);
        public Task<화물경로위험Dto> 현재위험조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물경로위험Dto());
    }
    public sealed class RecordingPublisher : IPublisher
    {
        public List<object> Events { get; } = [];
        public Task Publish(object notification, CancellationToken ct = default) { Assert.Null(DbTransaction?.Invoke()); Events.Add(notification); return Task.CompletedTask; }
        public Func<object?>? DbTransaction { get; set; }
        public Task Publish<T>(T notification, CancellationToken ct = default) where T : INotification => Publish((object)notification, ct);
    }
}
internal sealed class DistributedClock : TimeProvider
{
    public DateTime Now { get; set; } = DateTime.UtcNow;
    public override DateTimeOffset GetUtcNow() => new(Now);
}
internal sealed class DistributedLocations : IDriverLocationStore
{
    private readonly Dictionary<string, DriverLocationSnapshot> values = [];
    public void Upsert(DriverLocationSnapshot snapshot) => values[snapshot.DriverId] = snapshot;
    public bool TryGetLatest(string id, out DriverLocationSnapshot snapshot) => values.TryGetValue(id, out snapshot!);
}
internal sealed class DistributedRoute : I배차추천경로Service
{
    public bool Available { get; set; } = true;
    public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
    public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b)
        => Task.FromResult<배차경로예상결과?>(Available && a is not null && b is not null ? new(Math.Abs(a.Latitude - b.Latitude) * 100,
            TimeSpan.FromMinutes((double)(Math.Abs(a.Latitude - b.Latitude) * 100 + 1)), 0, "ExplicitSyntheticFixture", true) : null);
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b, string option, CancellationToken ct) => EstimateRouteAsync(a, b);
    public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => EstimateRouteAsync(origin, stops.LastOrDefault());
    public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? a, 배차경로좌표? b, 배차경로좌표? c, 배차경로좌표? d) => Task.FromResult<배차삽입경로예상결과?>(null);
    public decimal? CalculateDistanceKm(배차경로좌표 a, 배차경로좌표 b) => Math.Abs(a.Latitude - b.Latitude) * 100;
}
