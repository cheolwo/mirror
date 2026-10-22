using System.Text.Json;
using System.Data.Common;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Audit;
using 살뜰.Services.Notifications;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class WarehouseWorkflowProgressTests
{
    [Fact]
    public async Task 입고신청은_같은저장에서_창고소유자에게만예약하고_원요청재조회는추가하지않는다()
    {
        await using var fixture = await Fixture.Create();
        var request = Request(Guid.NewGuid());
        await using (var db = fixture.Open())
        {
            var created = await Service(db, "customer").CreateInboundAsync(request, default);
            Assert.True(created.Id > 0);
        }
        await using var replayDb = fixture.Open();
        var replay = await Service(replayDb, "customer").CreateInboundAsync(request, default);
        Assert.True(replay.멱등재시도여부);
        var row = Assert.Single(await Notices(replayDb));
        Assert.Equal("warehouse.requested", row.Stage);
        Assert.Equal("warehouse-owner", row.Recipient);
        Assert.Equal(InboundRequestPageRoutes.Root, row.Link);
        Assert.Contains(request.ClientRequestId!.Value.ToString("N"), row.Source);
    }

    [Fact]
    public async Task 작성자가다르면_같은원요청Guid라도_별도신청알림을보존한다()
    {
        await using var fixture = await Fixture.Create();
        var id = Guid.NewGuid();
        await using var db = fixture.Open();
        await Service(db, "customer").CreateInboundAsync(Request(id), default);
        await Service(db, "worker").CreateInboundAsync(Request(id), default);
        var rows = await Notices(db);
        Assert.Equal(2, rows.Length);
        Assert.Equal(2, rows.Select(row => row.EventId).Distinct().Count());
        Assert.All(rows, row => Assert.Equal("warehouse-owner", row.Recipient));
    }

    [Fact]
    public async Task 수령부터출고준비까지_확정단계와접근가능화면을저장하고_완료재요청은중복하지않는다()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Open();
        var inbound = await Service(db, "customer").CreateInboundAsync(Request(Guid.NewGuid()), default);
        var worker = Service(db, "worker");
        var receive = new 입고완료요청 { Items = [new 입고상품저장요청 { 상품명 = "개인표시금지상품", SKU = "SKU-A", 입고수량 = 2 }] };
        var received = await worker.CompleteInboundAsync(inbound.Id, receive, default);
        Assert.True((await worker.CompleteInboundAsync(inbound.Id, receive, default)).멱등재시도여부);
        var itemId = Assert.Single(received.Items).Id;
        var inspection = new 입고검수요청 { 검수수량 = 2, 검수메모 = "개인표시금지메모" };
        await worker.InspectInboundItemAsync(itemId, inspection, default);
        Assert.True((await worker.InspectInboundItemAsync(itemId, inspection, default)).멱등재시도여부);
        var location = new 적재위치배정요청 { 보관위치 = "비공개-적재대" };
        await worker.PutAwayInventoryItemAsync(itemId, location, default);
        Assert.True((await worker.PutAwayInventoryItemAsync(itemId, location, default)).멱등재시도여부);
        var packaging = new 포장작업요청 { 포장수량 = 2 };
        await worker.PackInventoryItemAsync(itemId, packaging, default);
        Assert.True((await worker.PackInventoryItemAsync(itemId, packaging, default)).멱등재시도여부);
        var ready = new 출고인계준비UseCase(db, new User("worker"), new Log(), new Publisher());
        var readyRequest = new 출고인계준비완료요청 { HandoffQuantity = 2, PackageSealConfirmed = true, TransportConditionsConfirmed = true };
        Assert.True((await ready.완료Async(itemId, readyRequest, Context(), default)).IsSuccess);
        Assert.True((await ready.완료Async(itemId, readyRequest, Context(), default)).Value.IdempotentReplay);

        var notices = (await Notices(db)).Where(row => row.Stage != "warehouse.requested").ToArray();
        foreach (var stage in new[] { "warehouse.received", "warehouse.inspected", "warehouse.stored", "warehouse.packed", "warehouse.ready" })
        {
            var group = notices.Where(row => row.Stage == stage).ToArray();
            Assert.Equal(new[] { "customer", "seller", "warehouse-owner" }, group.Select(row => row.Recipient).Order().ToArray());
            Assert.All(group.Where(row => (row.Recipient is "customer" or "seller") && row.Stage != "warehouse.received"),
                row => Assert.Equal("/shipper/warehouse/inventory", row.Link));
            Assert.All(group.Where(row => row.Recipient == "seller"), row => Assert.Equal("/shipper/warehouse/inventory", row.Link));
        }
        Assert.DoesNotContain(notices, row => row.Recipient is "worker" or "bystander");
        var payloads = string.Join("\n", await db.Command알림Outbox.Select(row => row.PayloadJson).ToArrayAsync());
        foreach (var secret in new[] { "개인표시금지상품", "개인표시금지메모", "비공개-적재대", "010-1234-5678", "상세주소" })
            Assert.DoesNotContain(secret, payloads, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 피킹완료는_지정된다음포장담당자에게만알리고_시작완료재호출은중복하지않는다()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Open();
        await SeedInventory(db, "적재완료");
        await SeedPicking(db);
        var useCase = new 피킹작업UseCase(db, new User("worker"), new Log(), new Publisher());
        Assert.True((await useCase.시작Async("PICK-A", Context(), default)).IsSuccess);
        Assert.True((await useCase.시작Async("PICK-A", Context(), default)).Value.IdempotentReplay);
        var request = new 피킹작업완료요청 { RackCode = "RACK-A", ProductConfirmed = true, QuantityConfirmed = true };
        Assert.True((await useCase.완료Async("PICK-A", request, Context(), default)).IsSuccess);
        Assert.True((await useCase.완료Async("PICK-A", request, Context(), default)).Value.IdempotentReplay);

        var rows = await Notices(db);
        Assert.Equal("warehouse-owner", Assert.Single(rows.Where(row => row.Stage == "warehouse.picking")).Recipient);
        var completed = rows.Where(row => row.Stage == "warehouse.picked").ToArray();
        Assert.Equal(new[] { "customer", "packer", "seller", "warehouse-owner" }, completed.Select(row => row.Recipient).Order().ToArray());
        Assert.Equal("/work/outbound/packing", Assert.Single(completed.Where(row => row.Recipient == "packer")).Link);
        Assert.DoesNotContain(rows, row => row.Recipient is "worker" or "bystander");
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("previous-task")]
    [InlineData("cancelled")]
    public async Task 포장연결이나접근권한이불확실하면_후속담당자알림을추측하지않는다(string broken)
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Open();
        await SeedInventory(db, "적재완료");
        await SeedPicking(db, 피킹포장작업상태.진행중);
        var next = await db.피킹포장작업.SingleAsync(task => task.작업Key == "PACK-A");
        if (broken == "membership") db.창고사용자.Remove(await db.창고사용자.SingleAsync(member => member.UserId == "packer"));
        if (broken == "previous-task") next.이전작업Key = "OTHER";
        if (broken == "cancelled") next.상태 = 피킹포장작업상태.취소;
        await db.SaveChangesAsync();
        var useCase = new 피킹작업UseCase(db, new User("worker"), new Log(), new Publisher());
        Assert.True((await useCase.완료Async("PICK-A", new() { RackCode = "RACK-A", ProductConfirmed = true, QuantityConfirmed = true }, Context(), default)).IsSuccess);
        Assert.DoesNotContain(await Notices(db), row => row.Recipient == "packer");
    }

    [Fact]
    public async Task 관계형저장실패는_업무상태와알림을함께롤백하고_추적알림도남기지않는다()
    {
        await using var fixture = await Fixture.Create();
        await using (var seed = fixture.Open()) await SeedInventory(seed, "검수완료");
        await using (var write = fixture.Open())
        {
            await write.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_progress BEFORE INSERT ON Command_알림_Outbox BEGIN SELECT RAISE(ABORT, 'isolated notification failure'); END;");
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(write, "worker").PutAwayInventoryItemAsync(31, new() { 보관위치 = "RACK-A" }, default));
            Assert.Empty(write.ChangeTracker.Entries<살뜰.도메인.설정.Command알림Outbox>());
        }
        await using var read = fixture.Open();
        Assert.Equal("검수완료", (await read.입고상품.SingleAsync()).상태);
        Assert.Empty(await Notices(read));
        Assert.Empty(await read.재고이력.ToArrayAsync());
    }

    [Fact]
    public async Task 호출자가연트랜잭션은_완료명령이커밋하지않고_업무와알림이함께되돌아간다()
    {
        await using var fixture = await Fixture.Create();
        await using (var seed = fixture.Open()) await SeedInventory(seed, "검수완료");
        await using (var write = fixture.Open())
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            await Service(write, "worker").PutAwayInventoryItemAsync(31, new() { 보관위치 = "RACK-A" }, default);
            Assert.Same(transaction, write.Database.CurrentTransaction);
            Assert.Equal(3, (await Notices(write)).Length);
            await transaction.RollbackAsync();
        }
        await using var read = fixture.Open();
        Assert.Equal("검수완료", (await read.입고상품.SingleAsync()).상태);
        Assert.Empty(await Notices(read));
    }

    [Fact]
    public async Task 저장후감사오류에도_진행알림의도는남고_다시완료해도중복하지않는다()
    {
        await using var fixture = await Fixture.Create();
        await using (var seed = fixture.Open()) await SeedInventory(seed, "검수완료");
        var request = new 적재작업완료요청 { StorageLocation = "RACK-A", InspectionResultConfirmed = true, LocationLabelConfirmed = true };
        await using (var write = fixture.Open())
        {
            var useCase = new 적재작업UseCase(write, new User("worker"), new Log { Fail = true }, new Publisher());
            await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.완료Async(31, request, Context(), default));
        }
        await using var read = fixture.Open();
        Assert.Equal("적재완료", (await read.입고상품.SingleAsync()).상태);
        Assert.Equal(3, (await Notices(read)).Length);
        var retry = new 적재작업UseCase(read, new User("worker"), new Log(), new Publisher());
        Assert.True((await retry.완료Async(31, request, Context(), default)).Value.IdempotentReplay);
        Assert.Equal(3, (await Notices(read)).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 커밋뒤감사이벤트의일시오류는_자동재시도성공으로숨기지않고_원예외를반환한다(bool failEvent)
    {
        await using var fixture = await Fixture.Create();
        await using (var seed = fixture.Open()) await SeedInventory(seed, "검수완료");
        await using var db = fixture.Open(retrying: true);
        var failure = new TimeoutException("isolated post-commit timeout");
        var log = new Log { BeforeRecord = () => Assert.Null(db.Database.CurrentTransaction), Failure = failEvent ? null : failure };
        var publisher = new Publisher { BeforePublish = () => Assert.Null(db.Database.CurrentTransaction), Failure = failEvent ? failure : null };
        var useCase = new 적재작업UseCase(db, new User("worker"), log, publisher);
        var error = await Assert.ThrowsAsync<TimeoutException>(() => useCase.완료Async(31,
            new() { StorageLocation = "RACK-A", InspectionResultConfirmed = true, LocationLabelConfirmed = true }, Context(), default));

        Assert.Same(failure, error);
        Assert.Equal(1, log.Calls);
        Assert.Equal(failEvent ? 1 : 0, publisher.Calls);
        Assert.Null(db.Database.CurrentTransaction);
        await using var read = fixture.Open();
        Assert.Equal("적재완료", (await read.입고상품.SingleAsync()).상태);
        Assert.Equal(3, (await Notices(read)).Length);
        Assert.Single(await read.재고이력.ToArrayAsync());
    }

    [Fact]
    public async Task 두번째수신자예약오류는_이번호출의알림만정리하고_같은범위의다음저장을오염시키지않는다()
    {
        await using var fixture = await Fixture.Create();
        var queryFault = new SecondProgressQueryFault();
        await using var db = fixture.Open(queryFault);
        await SeedInventory(db, "검수완료");
        var unrelated = new 살뜰.도메인.설정.Command알림Outbox { FeatureName = "other-feature", TraceId = "other-request", PayloadJson = "{}" };
        db.Command알림Outbox.Add(unrelated);
        var item = await db.입고상품.SingleAsync();
        queryFault.Enabled = true;

        await Assert.ThrowsAsync<TimeoutException>(() => 창고업무진행알림.재고Async(
            db, item, "warehouse.inspected", "inspected", "worker", DateTime.UtcNow, default));

        queryFault.Enabled = false;
        Assert.Equal(2, queryFault.Queries);
        var pending = Assert.Single(db.ChangeTracker.Entries<살뜰.도메인.설정.Command알림Outbox>());
        Assert.Same(unrelated, pending.Entity);
        Assert.Equal(EntityState.Added, pending.State);
        (await db.창고.SingleAsync()).창고명 = "별도 변경";
        await db.SaveChangesAsync();
        Assert.Empty(await Notices(db));
        Assert.Equal("other-request", (await db.Command알림Outbox.SingleAsync()).TraceId);
    }

    [Fact]
    public async Task 권한이없거나확인절차가빠지면_상태와알림을변경하지않는다()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Open();
        await SeedInventory(db, "검수완료");
        var forbidden = new 적재작업UseCase(db, new User("stranger"), new Log(), new Publisher());
        var request = new 적재작업완료요청 { StorageLocation = "RACK-A", InspectionResultConfirmed = true, LocationLabelConfirmed = true };
        Assert.True((await forbidden.완료Async(31, request, Context(), default)).IsFailed);
        var worker = new 적재작업UseCase(db, new User("worker"), new Log(), new Publisher());
        request.LocationLabelConfirmed = false;
        Assert.True((await worker.완료Async(31, request, Context(), default)).IsFailed);
        Assert.Equal("검수완료", (await db.입고상품.SingleAsync()).상태);
        Assert.Empty(await Notices(db));
    }

    [Theory]
    [InlineData("customer", 2)]
    [InlineData("warehouse-owner", 1)]
    [InlineData("worker", 1)]
    public async Task 같은사람의여러역할은_한번만알리고_본인행동알림은제외한다(string owner, int expected)
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Open();
        await SeedInventory(db, "검수완료");
        var item = await db.입고상품.SingleAsync();
        item.소유자UserId = owner;
        item.판매자UserId = owner;
        await db.SaveChangesAsync();
        await Service(db, "worker").PutAwayInventoryItemAsync(31, new() { 보관위치 = "RACK-A" }, default);
        var rows = await Notices(db);
        Assert.Equal(expected, rows.Length);
        Assert.Equal(expected, rows.Select(row => row.Recipient).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(rows, row => row.Recipient == "worker");
    }

    [Fact]
    public async Task 동시적재요청도_관계형원장에_수신자별한건의알림만남긴다()
    {
        await using var fixture = await Fixture.Create();
        await using (var seed = fixture.Open()) await SeedInventory(seed, "검수완료");
        async Task<bool> Attempt()
        {
            await using var db = fixture.Open();
            try
            {
                await Service(db, "worker").PutAwayInventoryItemAsync(31, new() { 보관위치 = "RACK-A" }, default);
                return true;
            }
            catch (SqliteException error) when (error.SqliteErrorCode is 5 or 6) { return false; }
        }
        var outcomes = await Task.WhenAll(Task.Run(Attempt), Task.Run(Attempt));
        Assert.Contains(true, outcomes);
        await using var read = fixture.Open();
        var result = await Service(read, "worker").PutAwayInventoryItemAsync(31, new() { 보관위치 = "RACK-A" }, default);
        Assert.True(result.멱등재시도여부);
        var rows = await Notices(read);
        Assert.Equal(3, rows.Length);
        Assert.Equal(3, rows.Select(row => row.EventId).Distinct().Count());
        Assert.Single(await read.재고이력.ToArrayAsync());
    }

    private static 입고요청저장요청 Request(Guid id) => new()
    {
        ClientRequestId = id, 창고Id = 7, 공급처명 = "공급처", 판매자UserId = "seller",
        계약정보 = 입고계약스냅샷.Default("공급처")
    };
    private static WarehouseOperationService Service(SsalddelContext db, string actor) => new(db, new User(actor), null!);
    private static 창고작업요청Context Context() => new("WarehouseManagerApp", "worker", "작업자", "창고관리자", "/work/inbound/put-away", "test-workflow-progress", "127.0.0.1", "test");

    private static async Task SeedInventory(SsalddelContext db, string state)
    {
        db.입고요청.Add(new 입고요청 { Id = 21, 창고Id = 7, 주문자UserId = "customer", 판매자UserId = "seller", 상태 = "입고완료" });
        db.입고상품.Add(new 입고상품 { Id = 31, 입고요청Id = 21, 창고Id = 7, 소유자UserId = "customer", 판매자UserId = "seller", 상품명 = "상품", SKU = "SKU-A", 입고수량 = 2, 가용수량 = 2, 상태 = state, 보관위치 = "RACK-A" });
        await db.SaveChangesAsync();
    }
    private static async Task SeedPicking(SsalddelContext db, string state = 피킹포장작업상태.대기)
    {
        db.피킹포장작업.AddRange(
            new 피킹포장작업 { 작업Key = "PICK-A", 작업유형 = 피킹포장작업유형.피킹, 창고Id = 7, 입고상품Id = 31, 작업자UserId = "worker", 다음작업Key = "PACK-A", 상태 = state, 적재대코드 = "RACK-A", 상품명 = "상품", SKU = "SKU-A", 수량 = 2 },
            new 피킹포장작업 { 작업Key = "PACK-A", 작업유형 = 피킹포장작업유형.포장, 창고Id = 7, 입고상품Id = 31, 작업자UserId = "packer", 이전작업Key = "PICK-A", 상태 = 피킹포장작업상태.대기, 수량 = 2 });
        await db.SaveChangesAsync();
    }
    private sealed record Notice(string Stage, string Recipient, string Link, string Source, string EventId);
    private static async Task<Notice[]> Notices(SsalddelContext db)
    {
        var rows = await db.Command알림Outbox.AsNoTracking().Where(row => row.FeatureName == 업무진행알림Recorder.FeatureName).OrderBy(row => row.Id).ToArrayAsync();
        return rows.Select(row =>
        {
            using var json = JsonDocument.Parse(row.PayloadJson);
            var root = json.RootElement;
            return new Notice(row.EventName, root.GetProperty("targetUserId").GetString()!, root.GetProperty("deepLink").GetString()!, root.GetProperty("sourceId").GetString()!, root.GetProperty("eventId").GetString()!);
        }).ToArray();
    }
    private sealed record User(string? UserId) : ICurrentUserAccessor { public string? Role => "창고관리자"; }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Log : I사용자행위로그Service
    {
        public bool Fail { get; init; }
        public Action? BeforeRecord { get; init; }
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }
        public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default)
        {
            Calls++;
            BeforeRecord?.Invoke();
            return Failure is not null ? Task.FromException(Failure)
                : Fail ? Task.FromException(new InvalidOperationException("isolated audit failure")) : Task.CompletedTask;
        }
    }
    private sealed class Publisher : IPublisher
    {
        public Action? BeforePublish { get; init; }
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Calls++;
            BeforePublish?.Invoke();
            return Failure is not null ? Task.FromException(Failure) : Task.CompletedTask;
        }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Publish((object)notification, cancellationToken);
    }
    private sealed class SecondProgressQueryFault : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public int Queries { get; private set; }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            if (Enabled && command.CommandText.Contains("Command_알림_Outbox", StringComparison.Ordinal)
                && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.Ordinal) && ++Queries == 2)
                throw new TimeoutException("isolated second recipient query failure");
            return result;
        }
    }
    private sealed class TimeoutStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new TimeoutStrategy(dependencies);
    }
    private sealed class TimeoutStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _name = $"warehouse-progress-{Guid.NewGuid():N}";
        private readonly SqliteConnection _keeper;
        private Fixture() => _keeper = new SqliteConnection(ConnectionString);
        private string ConnectionString => $"Data Source={_name};Mode=Memory;Cache=Shared;Default Timeout=2";
        public SsalddelContext Open(IInterceptor? interceptor = null, bool retrying = false)
        {
            var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(ConnectionString);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            if (retrying) options.ReplaceService<IExecutionStrategyFactory, TimeoutStrategyFactory>();
            return new(options.Options, new Encryption());
        }
        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            await fixture._keeper.OpenAsync();
            await using var db = fixture.Open();
            await db.Database.EnsureCreatedAsync();
            db.창고.Add(new 창고 { Id = 7, 소유자UserId = "warehouse-owner", 창고명 = "창고", 주소 = "비공개 상세주소", 연락처 = "010-1234-5678" });
            foreach (var user in new[] { "worker", "customer", "packer", "bystander" }) db.창고사용자.Add(new 창고사용자 { 창고Id = 7, UserId = user });
            await db.SaveChangesAsync();
            return fixture;
        }
        public ValueTask DisposeAsync() => _keeper.DisposeAsync();
    }
}
