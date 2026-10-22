using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Migrations;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Warehouse;

public sealed class WarehouseInboundSubmissionRecoveryTests
{
    private const string Author = "inbound-author";
    private const string OtherAuthor = "other-author";
    private const string WarehouseOwner = "warehouse-owner";
    private const string MigrationId = "20261022010000_AddInboundSubmissionRecovery";

    [Fact]
    public async Task 응답을잃어도_새컨텍스트에서_본인원요청번호로_같은원장을찾는다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var request = Request(Guid.NewGuid());
        long id;
        await using (var write = fixture.Open())
        {
            var created = await Service(write).CreateInboundAsync(request, default);
            id = created.Id;
            Assert.Equal(request.ClientRequestId, created.ClientRequestId);
            Assert.False(created.멱등재시도여부);
        }

        await using var read = fixture.Open();
        var restored = await Service(read).GetInboundByClientRequestIdAsync(request.ClientRequestId!.Value, default);

        Assert.NotNull(restored);
        Assert.Equal(id, restored.Id);
        Assert.Equal(request.ClientRequestId, restored.ClientRequestId);
        Assert.Single(await read.입고요청.ToArrayAsync());
        Assert.Empty(await read.입고상품.ToArrayAsync());
        Assert.Matches("^[0-9A-F]{64}$", (await read.입고요청.SingleAsync()).일반입고최초요청해시!);
    }

    [Fact]
    public async Task 같은원요청과정규화된내용은_새컨텍스트에서도_한건만유지한다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var requestId = Guid.NewGuid();
        long id;
        await using (var write = fixture.Open())
        {
            var request = Request(requestId);
            request.공급처명 = "  공급처  ";
            request.공급처코드 = " SUP-1 ";
            id = (await Service(write).CreateInboundAsync(request, default)).Id;
        }

        await using var replayDb = fixture.Open();
        var replay = await Service(replayDb).CreateInboundAsync(Request(requestId), default);

        Assert.Equal(id, replay.Id);
        Assert.True(replay.멱등재시도여부);
        Assert.Single(await replayDb.입고요청.ToArrayAsync());
    }

    [Theory]
    [InlineData("supplier")]
    [InlineData("warehouse")]
    [InlineData("notes")]
    [InlineData("arrival")]
    [InlineData("contract")]
    [InlineData("order")]
    public async Task 같은원요청의다른내용은_기존원장을바꾸지않고충돌한다(string changed)
    {
        await using var fixture = await DatabaseFixture.Create();
        var requestId = Guid.NewGuid();
        await using (var write = fixture.Open())
            await Service(write).CreateInboundAsync(Request(requestId), default);
        await using var replayDb = fixture.Open();
        var request = Request(requestId);
        switch (changed)
        {
            case "supplier": request.공급처명 = "다른 공급처"; break;
            case "warehouse": request.창고Id = 8; break;
            case "notes": request.비고 = "다른 요청"; break;
            case "arrival": request.예정도착일 = request.예정도착일!.Value.AddHours(1); break;
            case "contract": request.계약정보.보관료일단가++; break;
            case "order": request.주문참조번호 = "ORDER-2"; break;
        }

        await Assert.ThrowsAsync<InboundSubmissionConflictException>(
            () => Service(replayDb).CreateInboundAsync(request, default));

        var stored = await replayDb.입고요청.AsNoTracking().SingleAsync();
        Assert.Equal(7, stored.창고Id);
        Assert.Equal("공급처", stored.공급처명);
        Assert.Equal("최초 요청", stored.비고);
        Assert.Empty(await replayDb.입고상품.ToArrayAsync());
    }

    [Fact]
    public async Task 같은창고관리권한이있어도_다른작성자의원요청번호로탐색하지않는다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var requestId = Guid.NewGuid();
        long id;
        await using (var write = fixture.Open())
            id = (await Service(write).CreateInboundAsync(Request(requestId), default)).Id;
        await using var read = fixture.Open();

        Assert.NotNull(await Service(read, WarehouseOwner).GetInboundAsync(id, default));
        Assert.Null(await Service(read, WarehouseOwner).GetInboundByClientRequestIdAsync(requestId, default));
        Assert.Null(await Service(read, OtherAuthor).GetInboundByClientRequestIdAsync(requestId, default));
    }

    [Fact]
    public async Task 작성자는_창고회원상태가바뀌어도_기존정본조회와같은범위로원요청을확인한다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var requestId = Guid.NewGuid();
        await using (var write = fixture.Open())
        {
            await Service(write).CreateInboundAsync(Request(requestId), default);
            write.창고사용자.RemoveRange(await write.창고사용자.Where(x => x.UserId == Author).ToArrayAsync());
            await write.SaveChangesAsync();
        }
        await using var read = fixture.Open();

        var restored = await Service(read).GetInboundByClientRequestIdAsync(requestId, default);
        Assert.NotNull(restored);
        Assert.Equal(restored.Id, (await Service(read).GetInboundAsync(restored.Id, default))?.Id);
    }

    [Fact]
    public async Task DB가대소문자를무시해도_원요청작성자는_인증ID와정확히같아야한다()
    {
        await using var fixture = await DatabaseFixture.Create(caseInsensitiveOwner: true);
        var requestId = Guid.NewGuid();
        await using (var write = fixture.Open())
            await Service(write).CreateInboundAsync(Request(requestId), default);
        await using var read = fixture.Open();
        var differentCase = Author.ToUpperInvariant();
        Assert.NotNull(await read.입고요청.AsNoTracking().SingleOrDefaultAsync(x =>
            x.주문자UserId == differentCase && x.일반입고클라이언트요청Id == requestId));
        Assert.Null(await Service(read, differentCase).GetInboundByClientRequestIdAsync(requestId, default));
    }

    [Fact]
    public async Task 다른작성자는_같은요청번호를써도_본인의별도원장만찾는다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var requestId = Guid.NewGuid();
        long firstId;
        await using (var first = fixture.Open())
            firstId = (await Service(first).CreateInboundAsync(Request(requestId), default)).Id;
        await using var otherDb = fixture.Open();
        var second = await Service(otherDb, OtherAuthor).CreateInboundAsync(Request(requestId), default);

        Assert.NotEqual(firstId, second.Id);
        Assert.Equal(second.Id, (await Service(otherDb, OtherAuthor).GetInboundByClientRequestIdAsync(requestId, default))!.Id);
        Assert.Equal(2, await otherDb.입고요청.CountAsync());
    }

    [Fact]
    public async Task 수정한현재내용이아니라_최초접수해시로만_동일요청을판정한다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var requestId = Guid.NewGuid();
        string? hash;
        long id;
        await using (var write = fixture.Open())
        {
            var service = Service(write);
            id = (await service.CreateInboundAsync(Request(requestId), default)).Id;
            hash = (await write.입고요청.SingleAsync()).일반입고최초요청해시;
            var edited = Request(requestId);
            edited.공급처명 = "수정한 공급처";
            edited.비고 = "수정한 비고";
            await service.UpdateInboundAsync(id, edited, default);
        }

        await using var read = fixture.Open();
        var replay = await Service(read).CreateInboundAsync(Request(requestId), default);
        Assert.Equal(id, replay.Id);
        Assert.True(replay.멱등재시도여부);
        Assert.Equal("수정한 공급처", replay.공급처명);
        Assert.Equal(hash, (await read.입고요청.SingleAsync()).일반입고최초요청해시);
        var different = Request(requestId); different.공급처명 = "수정한 공급처";
        await Assert.ThrowsAsync<InboundSubmissionConflictException>(() => Service(read).CreateInboundAsync(different, default));
        Assert.Single(await read.입고요청.ToArrayAsync());
    }

    [Fact]
    public async Task 구판의요청번호없는입고는_복수접수가능하며_새요청조회대상이아니다()
    {
        await using var fixture = await DatabaseFixture.Create();
        await using var db = fixture.Open();
        var service = Service(db);

        var first = await service.CreateInboundAsync(Request(null), default);
        var second = await service.CreateInboundAsync(Request(null), default);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(first.ClientRequestId);
        Assert.Null(second.ClientRequestId);
        Assert.All(await db.입고요청.ToArrayAsync(), item =>
        {
            Assert.Null(item.일반입고클라이언트요청Id);
            Assert.Null(item.일반입고최초요청해시);
        });
    }

    [Fact]
    public async Task 현장입고와일반입고는_같은Guid라도_서로다른번호영역을유지한다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var requestId = Guid.NewGuid();
        await using var db = fixture.Open();
        db.입고요청.Add(new 입고요청
        {
            창고Id = 7, 주문자UserId = Author, 공급처명 = "현장 공급처",
            현장입고클라이언트요청Id = requestId, 입고흐름유형 = 입고흐름유형코드.현장임시입고
        });
        await db.SaveChangesAsync();

        var created = await Service(db).CreateInboundAsync(Request(requestId), default);
        var byRequest = await Service(db).GetInboundByClientRequestIdAsync(requestId, default);

        Assert.Equal(created.Id, byRequest!.Id);
        Assert.Equal(2, await db.입고요청.CountAsync());
        Assert.Null((await db.입고요청.SingleAsync(x => x.Id == created.Id)).현장입고클라이언트요청Id);
        Assert.Null((await db.입고요청.SingleAsync(x => x.현장입고클라이언트요청Id == requestId)).일반입고클라이언트요청Id);
    }

    [Fact]
    public async Task 빈Guid는_저장전에거절하고_없는번호와빈번호조회는노출하지않는다()
    {
        await using var fixture = await DatabaseFixture.Create();
        await using var db = fixture.Open();
        var service = Service(db);

        await Assert.ThrowsAsync<InboundSubmissionInvalidRequestException>(() => service.CreateInboundAsync(Request(Guid.Empty), default));
        Assert.Null(await service.GetInboundByClientRequestIdAsync(Guid.Empty, default));
        Assert.Null(await service.GetInboundByClientRequestIdAsync(Guid.NewGuid(), default));
        Assert.Empty(await db.입고요청.ToArrayAsync());
    }

    [Fact]
    public async Task 별도컨텍스트의경합은_SQLite유일제약이_같은작성자의중복행을막는다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var requestId = Guid.NewGuid();
        await using var first = fixture.Open();
        await using var stale = fixture.Open();
        first.입고요청.Add(new() { 창고Id = 7, 주문자UserId = Author, 일반입고클라이언트요청Id = requestId });
        stale.입고요청.Add(new() { 창고Id = 7, 주문자UserId = Author, 일반입고클라이언트요청Id = requestId });
        await first.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => stale.SaveChangesAsync());

        Assert.Equal(19, Assert.IsType<SqliteException>(exception.InnerException).SqliteErrorCode);
        await using var verification = fixture.Open();
        Assert.Single(await verification.입고요청.ToArrayAsync());
    }

    [Fact]
    public async Task 두서비스가동시에접수해도_유일키충돌을같은원장응답으로복구한다()
    {
        await using var fixture = await DatabaseFixture.Create();
        var gate = new ConcurrentSaveGate();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var first = fixture.Open(gate);
        await using var second = fixture.Open(gate);
        var requestId = Guid.NewGuid();
        var firstTask = Service(first).CreateInboundAsync(Request(requestId), timeout.Token);
        await gate.FirstReady.Task.WaitAsync(timeout.Token);
        var secondTask = Service(second).CreateInboundAsync(Request(requestId), timeout.Token);

        var responses = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(responses[0].Id, responses[1].Id);
        Assert.Single(responses.Where(x => x.멱등재시도여부));
        await using var verification = fixture.Open();
        Assert.Single(await verification.입고요청.ToArrayAsync());
    }

    [Fact]
    public void MySQL마이그레이션은_기존행수정없이_nullable두열과작성자별유일키만추가한다()
    {
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=127.0.0.1;Database=fixture_not_opened;User ID=fixture;Password=fixture;",
                new MySqlServerVersion(new Version(8, 4, 0)), options => options.MigrationsAssembly("Ssalddel"))
            .Options, new Encryption());

        Assert.Contains(MigrationId, db.Database.GetMigrations());
        var script = db.GetService<IMigrator>().GenerateScript("20261019190000_AddCargoHandlingMinutes", MigrationId);

        Assert.Contains("ADD `일반입고_클라이언트_요청_id` char(36) NULL", script);
        Assert.Contains("ADD `일반입고_최초_요청_해시` varchar(64)", script);
        Assert.Contains("CREATE UNIQUE INDEX `IX_입고요청_주문자_user_id_일반입고_클라이언트_요청_id`", script);
        Assert.Contains("(`주문자_user_id`, `일반입고_클라이언트_요청_id`)", script);
        Assert.DoesNotContain("UPDATE `입고요청`", script);
        Assert.DoesNotContain("DROP TABLE", script);
        Assert.DoesNotContain("현장입고_클라이언트_요청_id", script);

        var migration = new AddInboundSubmissionRecovery();
        var target = migration.TargetModel.FindEntityType("살뜰.도메인.창고.입고요청")!;
        Assert.True(target.FindProperty(nameof(입고요청.일반입고클라이언트요청Id))!.IsNullable);
        Assert.Equal(64, target.FindProperty(nameof(입고요청.일반입고최초요청해시))!.GetMaxLength());
        Assert.Contains(target.GetIndexes(), index => index.IsUnique && index.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { nameof(입고요청.주문자UserId), nameof(입고요청.일반입고클라이언트요청Id) }));
    }

    private static WarehouseOperationService Service(SsalddelContext db, string owner = Author)
        => new(db, new User(owner), null!);

    private static 입고요청저장요청 Request(Guid? requestId) => new()
    {
        ClientRequestId = requestId, 창고Id = 7, 공급처코드 = "SUP-1", 공급처명 = "공급처",
        원주문참조번호 = "ORDER-1", 비고 = "최초 요청",
        예정도착일 = new DateTime(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc),
        계약정보 = 입고계약스냅샷.Default("공급처")
    };

    private sealed record User(string? UserId) : ICurrentUserAccessor { public string? Role => null; }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        private readonly string _name = $"inbound-recovery-{Guid.NewGuid():N}";
        private readonly SqliteConnection _keeper;
        private readonly bool _caseInsensitiveOwner;
        private DatabaseFixture(bool caseInsensitiveOwner = false)
        {
            _caseInsensitiveOwner = caseInsensitiveOwner;
            _keeper = new SqliteConnection(ConnectionString);
        }
        private string ConnectionString => $"Data Source={_name};Mode=Memory;Cache=Shared";

        public static async Task<DatabaseFixture> Create(bool caseInsensitiveOwner = false)
        {
            var fixture = new DatabaseFixture(caseInsensitiveOwner);
            await fixture._keeper.OpenAsync();
            await using var db = fixture.Open();
            await db.Database.EnsureCreatedAsync();
            db.창고.AddRange(new 창고 { Id = 7, 창고명 = "공동 창고", 소유자UserId = WarehouseOwner },
                new 창고 { Id = 8, 창고명 = "다른 공동 창고", 소유자UserId = WarehouseOwner });
            foreach (var warehouseId in new long[] { 7, 8 })
            foreach (var author in new[] { Author, OtherAuthor })
                db.창고사용자.Add(new() { 창고Id = warehouseId, UserId = author });
            await db.SaveChangesAsync();
            return fixture;
        }

        public SsalddelContext Open(IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(ConnectionString);
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return _caseInsensitiveOwner ? new CaseInsensitiveOwnerContext(builder.Options) : new SsalddelContext(builder.Options, new Encryption());
        }
        public ValueTask DisposeAsync() => _keeper.DisposeAsync();
    }

    private sealed class CaseInsensitiveOwnerContext(DbContextOptions<SsalddelContext> options) : SsalddelContext(options, new Encryption())
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<입고요청>().Property(x => x.주문자UserId).UseCollation("NOCASE");
        }
    }

    private sealed class ConcurrentSaveGate : SaveChangesInterceptor
    {
        private int _entered;
        public TaskCompletionSource FirstReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _bothReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstSaved = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _entered) == 1)
            {
                FirstReady.TrySetResult();
                await _bothReady.Task.WaitAsync(cancellationToken);
            }
            else
            {
                _bothReady.TrySetResult();
                await _firstSaved.Task.WaitAsync(cancellationToken);
            }
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            _firstSaved.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}
