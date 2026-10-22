using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Persistence.Configurations.Warehouse;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Infrastructure;

public sealed class PhotoInboundPersistenceTests
{
    private const string MigrationId = "20261022020000_AddPhotoInboundRequest";

    [Fact]
    public async Task 사진만접수하면_이름창고상품없이_본인사진참조만새컨텍스트에서읽는다()
    {
        await using var fixture = await Fixture.Create();
        var requestId = Guid.NewGuid();
        await using (var write = fixture.Open())
        {
            write.Photos.Add(Photo(requestId));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.Open();
        var photo = await read.Photos.SingleAsync();
        Assert.Equal(requestId, photo.Id);
        Assert.Equal("author", photo.작성자UserId);
        Assert.Equal(string.Empty, photo.이름);
        Assert.Equal(1, photo.Revision);
        Assert.Equal("private-photo-container", photo.사진Container);
        Assert.Equal($"photo-inbounds/{requestId:N}/photo", photo.사진ObjectName);
        Assert.Empty(await read.Inbounds.ToArrayAsync());
        Assert.Null(photo.입고요청);
        var properties = read.Model.FindEntityType(typeof(가입고사진신청))!.GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("창고Id", properties);
        Assert.DoesNotContain("상품명", properties);
        Assert.DoesNotContain("수량", properties);
        Assert.DoesNotContain("상태", properties);
        Assert.DoesNotContain("보관료일단가", properties);
    }

    [Fact]
    public async Task 요청번호기본키가_별도컨텍스트의중복사진접수를막는다()
    {
        await using var fixture = await Fixture.Create();
        var requestId = Guid.NewGuid();
        await using var first = fixture.Open();
        await using var second = fixture.Open();
        first.Photos.Add(Photo(requestId));
        second.Photos.Add(Photo(requestId));
        await first.SaveChangesAsync();

        await AssertConstraint(() => second.SaveChangesAsync());

        await using var read = fixture.Open();
        Assert.Single(await read.Photos.ToArrayAsync());
    }

    [Fact]
    public async Task 같은사진신청을_두개의정식입고에연결할수없다()
    {
        await using var fixture = await Fixture.Create();
        var requestId = Guid.NewGuid();
        await using (var first = fixture.Open())
        {
            first.Photos.Add(Photo(requestId));
            first.Inbounds.Add(Inbound(requestId));
            await first.SaveChangesAsync();
        }
        await using var second = fixture.Open();
        second.Inbounds.Add(Inbound(requestId));

        await AssertConstraint(() => second.SaveChangesAsync());

        await using var read = fixture.Open();
        var stored = await read.Photos.Include(x => x.입고요청).SingleAsync();
        Assert.NotNull(stored.입고요청);
        Assert.Equal(requestId, stored.입고요청.가입고사진신청Id);
        Assert.Single(await read.Inbounds.ToArrayAsync());
    }

    [Fact]
    public async Task 사진신청없는_기존입고여러건은_nullable유일키에막히지않는다()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Open();
        db.Inbounds.AddRange(Inbound(null), Inbound(null));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Inbounds.CountAsync());
        Assert.All(await db.Inbounds.ToArrayAsync(), item => Assert.Null(item.가입고사진신청Id));
        Assert.Empty(await db.Photos.ToArrayAsync());
    }

    [Fact]
    public async Task 존재하지않는사진신청으로_정식입고를연결할수없다()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Open();
        db.Inbounds.Add(Inbound(Guid.NewGuid()));

        await AssertConstraint(() => db.SaveChangesAsync());

        await using var read = fixture.Open();
        Assert.Empty(await read.Inbounds.ToArrayAsync());
    }

    [Fact]
    public async Task 연결된사진신청을삭제해도_정식입고가연쇄삭제되거나연결이사라지지않는다()
    {
        await using var fixture = await Fixture.Create();
        var requestId = Guid.NewGuid();
        await using (var write = fixture.Open())
        {
            write.Photos.Add(Photo(requestId));
            write.Inbounds.Add(Inbound(requestId));
            await write.SaveChangesAsync();
        }
        await using var deletion = fixture.Open();
        deletion.Photos.Remove(await deletion.Photos.SingleAsync());

        await AssertConstraint(() => deletion.SaveChangesAsync());

        await using var read = fixture.Open();
        Assert.Single(await read.Photos.ToArrayAsync());
        Assert.Equal(requestId, (await read.Inbounds.SingleAsync()).가입고사진신청Id);
    }

    [Fact]
    public async Task 이름수정의_같은개정경합은_나중저장을거절하고먼저저장한값을유지한다()
    {
        await using var fixture = await Fixture.Create();
        var requestId = await fixture.Seed();
        await using var first = fixture.Open();
        await using var stale = fixture.Open();
        var firstPhoto = await first.Photos.SingleAsync(x => x.Id == requestId);
        var stalePhoto = await stale.Photos.SingleAsync(x => x.Id == requestId);
        firstPhoto.이름 = "먼저 붙인 이름";
        firstPhoto.Revision++;
        await first.SaveChangesAsync();
        stalePhoto.이름 = "뒤늦은 이름";
        stalePhoto.Revision++;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());

        await using var read = fixture.Open();
        var result = await read.Photos.SingleAsync();
        Assert.Equal("먼저 붙인 이름", result.이름);
        Assert.Equal(2, result.Revision);
    }

    [Fact]
    public async Task 사진개정경합이면_같은저장의정식입고생성도_함께롤백된다()
    {
        await using var fixture = await Fixture.Create();
        var requestId = await fixture.Seed();
        await using var first = fixture.Open();
        await using var stale = fixture.Open();
        var firstPhoto = await first.Photos.SingleAsync(x => x.Id == requestId);
        var stalePhoto = await stale.Photos.SingleAsync(x => x.Id == requestId);
        firstPhoto.이름 = "먼저 수정";
        firstPhoto.Revision++;
        await first.SaveChangesAsync();
        stalePhoto.Revision++;
        stale.Inbounds.Add(Inbound(requestId));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());

        await using var read = fixture.Open();
        Assert.Empty(await read.Inbounds.ToArrayAsync());
        Assert.Equal(2, (await read.Photos.SingleAsync()).Revision);
    }

    [Fact]
    public void 실제컨텍스트모델과마이그레이션정본은_같은사진신청과단일연결을갖는다()
    {
        using var db = MySqlContext();
        var current = db.Model.FindEntityType(typeof(가입고사진신청))!;
        Assert.NotNull(current);
        Assert.NotNull(db.가입고사진신청);
        Assert.True(current.FindProperty(nameof(가입고사진신청.Revision))!.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never, current.FindProperty("Id")!.ValueGenerated);
        Assert.Equal(100, current.FindProperty(nameof(가입고사진신청.이름))!.GetMaxLength());
        Assert.Equal(450, current.FindProperty(nameof(가입고사진신청.작성자UserId))!.GetMaxLength());
        var inbound = db.Model.FindEntityType(typeof(입고요청))!;
        Assert.True(inbound.FindProperty(nameof(입고요청.가입고사진신청Id))!.IsNullable);
        var foreignKey = Assert.Single(inbound.GetForeignKeys(), x => x.PrincipalEntityType == current);
        Assert.True(foreignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);

        var migration = new AddPhotoInboundRequest();
        var target = migration.TargetModel.FindEntityType(typeof(가입고사진신청).FullName!)!;
        Assert.True(target.FindProperty(nameof(가입고사진신청.Revision))!.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never, target.FindProperty("Id")!.ValueGenerated);
        var targetInbound = migration.TargetModel.FindEntityType(typeof(입고요청).FullName!)!;
        Assert.True(targetInbound.FindProperty(nameof(입고요청.가입고사진신청Id))!.IsNullable);
        Assert.Contains(targetInbound.GetForeignKeys(), key => key.IsUnique && key.DeleteBehavior == DeleteBehavior.Restrict
            && key.PrincipalEntityType.Name == typeof(가입고사진신청).FullName);
    }

    [Fact]
    public void MySQL오프라인스크립트는_기존입고수정없이_사진준비테이블과선택연결만추가한다()
    {
        using var db = MySqlContext();
        Assert.Contains(MigrationId, db.Database.GetMigrations());
        var script = db.GetService<IMigrator>().GenerateScript("20261022010000_AddInboundSubmissionRecovery", MigrationId);

        Assert.Contains("CREATE TABLE `가입고사진신청`", script);
        Assert.Contains("ADD `가입고사진신청_id` char(36) NULL", script);
        Assert.Contains("CREATE UNIQUE INDEX `IX_입고요청_가입고사진신청_id`", script);
        Assert.Contains("REFERENCES `가입고사진신청` (`id`) ON DELETE RESTRICT", script);
        Assert.Contains("`revision` bigint NOT NULL DEFAULT 1", script);
        Assert.DoesNotContain("UPDATE `입고요청`", script);
        Assert.DoesNotContain("INSERT INTO `입고요청`", script);
        Assert.DoesNotContain("DROP TABLE", script);
        Assert.DoesNotContain("현장입고_클라이언트_요청_id", script);
    }

    private static Task<DbUpdateException> AssertConstraint(Func<Task<int>> action)
        => Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            try { await action(); }
            catch (DbUpdateException exception)
            {
                Assert.Equal(19, Assert.IsType<SqliteException>(exception.InnerException).SqliteErrorCode);
                throw;
            }
        });

    private static 가입고사진신청 Photo(Guid id) => new()
    {
        Id = id, 작성자UserId = "author", 사진Container = "private-photo-container",
        사진ObjectName = $"photo-inbounds/{id:N}/photo", 사진Sha256 = new string('A', 64),
        최초요청해시 = new string('B', 64), ContentType = "image/jpeg", Bytes = 1234
    };

    private static 입고요청 Inbound(Guid? photoId) => new()
    {
        가입고사진신청Id = photoId, 창고Id = 7, 주문자UserId = "author", 공급처명 = "검증 공급처"
    };

    private static SsalddelContext MySqlContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseMySql("Server=127.0.0.1;Database=fixture_not_opened;User ID=fixture;Password=fixture;",
            new MySqlServerVersion(new Version(8, 4, 0)), options => options.MigrationsAssembly("Ssalddel"))
        .Options, new Encryption());

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    // 실제 SQLite의 유일키·FK·개정 WHERE·트랜잭션을 시험한다. 운영 DB는 연결하지 않는다.
    private sealed class PhotoContext(DbContextOptions<PhotoContext> options) : DbContext(options)
    {
        public DbSet<가입고사진신청> Photos => Set<가입고사진신청>();
        public DbSet<입고요청> Inbounds => Set<입고요청>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new 가입고사진신청Configuration());
            modelBuilder.ApplyConfiguration(new 입고요청Configuration());
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _name = $"photo-inbound-{Guid.NewGuid():N}";
        private readonly SqliteConnection _keeper;
        private Fixture() => _keeper = new SqliteConnection(ConnectionString);
        private string ConnectionString => $"Data Source={_name};Mode=Memory;Cache=Shared;Foreign Keys=True";

        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            await fixture._keeper.OpenAsync();
            await using var db = fixture.Open();
            await db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public PhotoContext Open() => new(new DbContextOptionsBuilder<PhotoContext>().UseSqlite(ConnectionString).Options);

        public async Task<Guid> Seed()
        {
            var id = Guid.NewGuid();
            await using var db = Open();
            db.Photos.Add(Photo(id));
            await db.SaveChangesAsync();
            return id;
        }

        public ValueTask DisposeAsync() => _keeper.DisposeAsync();
    }
}
