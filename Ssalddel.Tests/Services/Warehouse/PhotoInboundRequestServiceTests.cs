using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SkiaSharp;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Warehouse;

public sealed class PhotoInboundRequestServiceTests
{
    private const string Actor = "photo-author";

    [Fact]
    public async Task 사진만신청하면_창고상품계약없이_다른컨텍스트에서이어본다()
    {
        await using var fixture = await Fixture.Create();
        var id = Guid.NewGuid();
        await using (var db = fixture.Open())
        {
            var result = await fixture.Service(db).CreateAsync(id, null, Photo(), default);
            Assert.Equal(id, result.Id); Assert.Empty(result.이름); Assert.Null(result.입고요청Id); Assert.Equal(1, result.Revision);
            Assert.Empty(await db.입고요청.ToArrayAsync()); Assert.Empty(await db.입고상품.ToArrayAsync());
            Assert.Empty(await db.재고이력.ToArrayAsync());
        }
        await using var read = fixture.Open();
        var service = fixture.Service(read);
        Assert.Equal(id, (await service.GetAsync(id, default))!.Id);
        var photo = await service.GetPhotoAsync(id, default);
        Assert.Equal("image/png", photo!.ContentType); Assert.Equal(Image(), photo.Bytes);
        var list = await service.GetListAsync(0, 25, default);
        Assert.Equal(1, list.TotalCount); Assert.Equal(id, Assert.Single(list.Items).Id);
        var stored = await read.Set<가입고사진신청>().SingleAsync();
        Assert.DoesNotContain("secret-original-file", stored.사진ObjectName);
        Assert.All(fixture.Storage.Accesses, access => Assert.Equal(ObjectStorageAccess.Private, access));
    }

    [Fact]
    public async Task 같은요청의같은사진과최초이름은_이름수정후에도_중복생성하지않는다()
    {
        await using var fixture = await Fixture.Create(); var id = Guid.NewGuid();
        await using (var db = fixture.Open())
        {
            var service = fixture.Service(db);
            var first = await service.CreateAsync(id, "  첫 이름  ", Photo(), default);
            await service.RenameAsync(id, new() { Revision = first.Revision, 이름 = "나중 이름" }, default);
        }
        await using var replay = fixture.Open();
        var restored = await fixture.Service(replay).CreateAsync(id, "첫 이름", Photo(), default);
        Assert.Equal("나중 이름", restored.이름); Assert.Equal(2, restored.Revision);
        Assert.Single(await replay.Set<가입고사진신청>().ToArrayAsync()); Assert.Equal(1, fixture.Storage.UploadCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 같은요청의다른사진또는최초이름은_기존기록을덮지않는다(bool otherPhoto)
    {
        await using var fixture = await Fixture.Create(); await using var db = fixture.Open();
        var service = fixture.Service(db); var id = Guid.NewGuid();
        await service.CreateAsync(id, "첫 이름", Photo(), default);
        var ex = await Assert.ThrowsAsync<가입고사진신청Exception>(() => service.CreateAsync(id,
            otherPhoto ? "첫 이름" : "다른 이름", Photo(otherPhoto), default));
        Assert.Equal("PhotoInboundRequestConflict", ex.Code);
        Assert.Equal("첫 이름", (await service.GetAsync(id, default))!.이름); Assert.Equal(1, fixture.Storage.UploadCalls);
    }

    [Fact]
    public async Task 다른작성자는_목록상세사진이름에_접근하지못한다()
    {
        await using var fixture = await Fixture.Create(); var id = Guid.NewGuid();
        await using (var write = fixture.Open()) await fixture.Service(write).CreateAsync(id, null, Photo(), default);
        await using var read = fixture.Open(); var other = fixture.Service(read, "other-author");
        Assert.Null(await other.GetAsync(id, default)); Assert.Null(await other.GetPhotoAsync(id, default));
        Assert.Empty((await other.GetListAsync(0, 25, default)).Items);
        var ex = await Assert.ThrowsAsync<가입고사진신청Exception>(() => other.RenameAsync(id, new() { Revision = 1, 이름 = "변경" }, default));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task 대소문자무시DB라도_인증작성자가정확히같아야한다()
    {
        await using var fixture = await Fixture.Create(caseInsensitive: true); var id = Guid.NewGuid();
        await using (var write = fixture.Open()) await fixture.Service(write).CreateAsync(id, null, Photo(), default);
        await using var read = fixture.Open(); var other = fixture.Service(read, Actor.ToUpperInvariant());
        Assert.NotNull(await read.Set<가입고사진신청>().AsNoTracking().SingleOrDefaultAsync(x => x.작성자UserId == Actor.ToUpperInvariant()));
        Assert.Null(await other.GetAsync(id, default)); Assert.Null(await other.GetPhotoAsync(id, default));
        Assert.Equal(0, (await other.GetListAsync(0, 25, default)).TotalCount);
        await Assert.ThrowsAsync<가입고사진신청Exception>(() => other.RenameAsync(id, new() { Revision = 1 }, default));
    }

    [Theory]
    [InlineData("empty-id")]
    [InlineData("empty")]
    [InlineData("oversize")]
    [InlineData("bad-type")]
    [InlineData("name")]
    public async Task 유효하지않은사진신청은_저장소업로드나준비기록을만들지않는다(string invalid)
    {
        await using var fixture = await Fixture.Create(); await using var db = fixture.Open();
        var photo = invalid switch
        {
            "empty" => File([]), "oversize" => File(new byte[가입고사진신청Policy.MaximumPhotoBytes + 1]),
            "bad-type" => File("not an image"u8.ToArray()), _ => Photo()
        };
        await Assert.ThrowsAsync<가입고사진신청Exception>(() => fixture.Service(db).CreateAsync(
            invalid == "empty-id" ? Guid.Empty : Guid.NewGuid(), invalid == "name" ? new string('가', 101) : null, photo, default));
        Assert.Equal(0, fixture.Storage.UploadCalls); Assert.Empty(await db.Set<가입고사진신청>().ToArrayAsync());
    }

    [Fact]
    public async Task 저장한사진의바이트대조가실패하면_접수로저장하지않는다()
    {
        await using var fixture = await Fixture.Create(); await using var db = fixture.Open();
        fixture.Storage.CorruptRead = true;
        var ex = await Assert.ThrowsAsync<가입고사진신청Exception>(() => fixture.Service(db).CreateAsync(Guid.NewGuid(), null, Photo(), default));
        Assert.Equal("PhotoInboundStorageIntegrityMismatch", ex.Code); Assert.Empty(await db.Set<가입고사진신청>().ToArrayAsync());
    }

    [Fact]
    public async Task 사진보관미설정은_업로드시작전확정미적용이며_기존신청재조회는보존한다()
    {
        await using var fixture = await Fixture.Create(); await using var db = fixture.Open();
        var service = fixture.Service(db); var id = Guid.NewGuid();
        await service.CreateAsync(id, null, Photo(), default);
        fixture.Storage.Configured = false;
        Assert.Equal(id, (await service.CreateAsync(id, null, Photo(), default)).Id);
        var ex = await Assert.ThrowsAsync<가입고사진신청Exception>(() => service.CreateAsync(Guid.NewGuid(), null, Photo(), default));
        Assert.Equal(503, ex.StatusCode); Assert.True(ex.DefinitivelyNotApplied); Assert.Equal(1, fixture.Storage.UploadCalls);
        Assert.Single(await db.Set<가입고사진신청>().ToArrayAsync());
    }

    [Fact]
    public async Task 사진조회시보관무결성이다르면_잘못된바이트를반환하지않는다()
    {
        await using var fixture = await Fixture.Create(); await using var db = fixture.Open(); var id = Guid.NewGuid();
        var service = fixture.Service(db); await service.CreateAsync(id, null, Photo(), default);
        fixture.Storage.CorruptRead = true;
        await Assert.ThrowsAsync<가입고사진신청Exception>(() => service.GetPhotoAsync(id, default));
    }

    [Fact]
    public async Task 이름은개정을비교하고_늦은수정으로덮어쓰지않는다()
    {
        await using var fixture = await Fixture.Create(); var id = Guid.NewGuid();
        await using (var create = fixture.Open()) await fixture.Service(create).CreateAsync(id, null, Photo(), default);
        await using (var first = fixture.Open()) await fixture.Service(first).RenameAsync(id, new() { Revision = 1, 이름 = "먼저 수정" }, default);
        await using var stale = fixture.Open(); var service = fixture.Service(stale);
        var ex = await Assert.ThrowsAsync<가입고사진신청Exception>(() => service.RenameAsync(id, new() { Revision = 1, 이름 = "늦은 수정" }, default));
        Assert.Equal("PhotoInboundRevisionConflict", ex.Code); Assert.Equal("먼저 수정", (await service.GetAsync(id, default))!.이름);
    }

    [Fact]
    public async Task 정식신청은_원요청Id로입고한건만연결하고_사진신청개정을함께올린다()
    {
        await using var fixture = await Fixture.Create(); var id = Guid.NewGuid(); long inboundId;
        await using (var create = fixture.Open()) await fixture.Service(create).CreateAsync(id, null, Photo(), default);
        await using (var submit = fixture.Open())
        {
            var result = await Warehouse(submit).CreateInboundAsync(Request(id), default);
            inboundId = result.Id; Assert.Equal(id, result.가입고사진신청Id);
        }
        await using var replay = fixture.Open();
        var restored = await Warehouse(replay).CreateInboundAsync(Request(id), default);
        Assert.Equal(inboundId, restored.Id); Assert.True(restored.멱등재시도여부);
        var draft = await fixture.Service(replay).GetAsync(id, default);
        Assert.Equal(inboundId, draft!.입고요청Id); Assert.Equal(2, draft.Revision);
        Assert.Single(await replay.입고요청.ToArrayAsync()); Assert.Empty(await replay.입고상품.ToArrayAsync());
        Assert.Equal(inboundId, (await Warehouse(replay).GetInboundByClientRequestIdAsync(id, default))!.Id);
        var rename = await Assert.ThrowsAsync<가입고사진신청Exception>(() => fixture.Service(replay).RenameAsync(id,
            new() { Revision = draft.Revision, 이름 = "연결 후 변경" }, default));
        Assert.Equal("PhotoInboundAlreadyConverted", rename.Code);
        Assert.Empty((await fixture.Service(replay).GetAsync(id, default))!.이름);
    }

    [Fact]
    public async Task 사진연결은_본인사진과동일한원요청번호로만가능하다()
    {
        await using var fixture = await Fixture.Create(); var id = Guid.NewGuid();
        await using (var create = fixture.Open()) await fixture.Service(create).CreateAsync(id, null, Photo(), default);
        await using var submit = fixture.Open();
        var wrongId = Request(id); wrongId.ClientRequestId = Guid.NewGuid();
        await Assert.ThrowsAsync<InboundSubmissionInvalidRequestException>(() => Warehouse(submit).CreateInboundAsync(wrongId, default));
        await Assert.ThrowsAsync<InboundSubmissionConflictException>(() => Warehouse(submit, "other-author").CreateInboundAsync(Request(id), default));
        Assert.Empty(await submit.입고요청.ToArrayAsync()); Assert.Equal(1, (await fixture.Service(submit).GetAsync(id, default))!.Revision);
    }

    [Fact]
    public async Task 정식입고저장이실패하면_사진연결과개정도_함께미적용이다()
    {
        await using var fixture = await Fixture.Create(); var id = Guid.NewGuid();
        await using (var create = fixture.Open()) await fixture.Service(create).CreateAsync(id, null, Photo(), default);
        await using (var failed = fixture.Open(new FailInboundSave()))
            await Assert.ThrowsAsync<DbUpdateException>(() => Warehouse(failed).CreateInboundAsync(Request(id), default));
        await using var read = fixture.Open(); var result = await fixture.Service(read).GetAsync(id, default);
        Assert.Null(result!.입고요청Id); Assert.Equal(1, result.Revision); Assert.Empty(await read.입고요청.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 최초조회직후다른요청이사진을연결하면_동일내용만현재정본으로복귀한다(bool changedContent)
    {
        await using var fixture = await Fixture.Create(); var id = Guid.NewGuid(); long committedId = 0;
        await using (var create = fixture.Open()) await fixture.Service(create).CreateAsync(id, null, Photo(), default);
        var barrier = new BeforePhotoRead(async () =>
        {
            // 앞선 원요청 조회가 빈 결과를 반환한 뒤, 사진 연결 조회 직전에 다른 컨텍스트가 저장한다.
            await using var other = fixture.Open();
            committedId = (await Warehouse(other).CreateInboundAsync(Request(id), default)).Id;
        });
        await using var reader = fixture.Open(barrier); var request = Request(id);
        if (changedContent) request.공급처명 = "다른 공급처";
        if (changedContent)
            await Assert.ThrowsAsync<InboundSubmissionConflictException>(() => Warehouse(reader).CreateInboundAsync(request, default));
        else
        {
            var replay = await Warehouse(reader).CreateInboundAsync(request, default);
            Assert.True(replay.멱등재시도여부); Assert.Equal(committedId, replay.Id);
        }
        Assert.True(barrier.Triggered); Assert.True(committedId > 0);
        await using var check = fixture.Open();
        Assert.Single(await check.입고요청.ToArrayAsync());
        var photo = await fixture.Service(check).GetAsync(id, default);
        Assert.Equal(committedId, photo!.입고요청Id); Assert.Equal(2, photo.Revision);
    }

    private static 입고요청저장요청 Request(Guid id) => new()
        { ClientRequestId = id, 가입고사진신청Id = id, 창고Id = 7, 공급처명 = "확인한 공급처", 계약정보 = 입고계약스냅샷.Default() };
    private static WarehouseOperationService Warehouse(SsalddelContext db, string actor = Actor) => new(db, new User(actor), null!);
    private static IFormFile Photo(bool other = false) => File(Image(other));
    private static IFormFile File(byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "secret-original-file.png")
        { Headers = new HeaderDictionary(), ContentType = "image/png" };
    private static byte[] Image(bool other = false)
    {
        using var bitmap = new SKBitmap(2, 2); bitmap.Erase(other ? SKColors.Blue : SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
    private sealed record User(string? UserId) : ICurrentUserAccessor { public string? Role => null; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _name = "photo-inbound-" + Guid.NewGuid().ToString("N");
        private readonly SqliteConnection _keeper;
        private readonly bool _caseInsensitive;
        public Storage Storage { get; } = new();
        private Fixture(bool caseInsensitive) { _caseInsensitive = caseInsensitive; _keeper = new(ConnectionString); }
        private string ConnectionString => $"Data Source={_name};Mode=Memory;Cache=Shared";
        public static async Task<Fixture> Create(bool caseInsensitive = false)
        {
            var fixture = new Fixture(caseInsensitive); await fixture._keeper.OpenAsync();
            await using var db = fixture.Open(); await db.Database.EnsureCreatedAsync();
            db.창고.Add(new 창고 { Id = 7, 창고명 = "확인한 창고", 소유자UserId = Actor });
            db.창고사용자.Add(new() { 창고Id = 7, UserId = "other-author" });
            await db.SaveChangesAsync(); return fixture;
        }
        public SsalddelContext Open(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(ConnectionString);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return _caseInsensitive ? new CaseInsensitiveContext(options.Options) : new SsalddelContext(options.Options, new Encryption());
        }
        public 가입고사진신청Service Service(SsalddelContext db, string actor = Actor) => new(db, new User(actor), Storage, TimeProvider.System);
        public ValueTask DisposeAsync() => _keeper.DisposeAsync();
    }
    private sealed class CaseInsensitiveContext(DbContextOptions<SsalddelContext> options) : SsalddelContext(options, new Encryption())
    {
        protected override void OnModelCreating(ModelBuilder builder)
        { base.OnModelCreating(builder); builder.Entity<가입고사진신청>().Property(x => x.작성자UserId).UseCollation("NOCASE"); }
    }
    private sealed class Storage : IObjectStorageService
    {
        private readonly Dictionary<string, byte[]> _photos = [];
        public bool CorruptRead { get; set; }
        public bool Configured { get; set; } = true;
        public int UploadCalls { get; private set; }
        public List<ObjectStorageAccess> Accesses { get; } = [];
        public bool IsConfigured(ObjectStorageAccess access) => Configured;
        public string? GetContainerName(ObjectStorageAccess access) => "private-test";
        public async Task<ObjectStorageUploadResult> UploadImmutableAsync(Stream stream, string objectName, string? contentType,
            ObjectStorageAccess access, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
        {
            UploadCalls++; Accesses.Add(access); using var output = new MemoryStream(); await stream.CopyToAsync(output, cancellationToken);
            var bytes = output.ToArray();
            if (_photos.TryGetValue(objectName, out var existing) && !existing.SequenceEqual(bytes)) throw new IOException("immutable conflict");
            _photos[objectName] = bytes;
            return new("private-test", objectName, "https://must-not-be-returned.invalid/private");
        }
        public Task<byte[]> DownloadAsync(string containerName, string objectName, CancellationToken cancellationToken = default)
            => Task.FromResult(CorruptRead ? new byte[] { 1 } : _photos[objectName].ToArray());
        public Task<ObjectStorageUploadResult> UploadAsync(Stream stream, string originalFileName, string? contentType, string? folder,
            ObjectStorageAccess access, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FailInboundSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<입고요청>().Any(x => x.State == EntityState.Added))
                throw new DbUpdateException("controlled save failure");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class BeforePhotoRead(Func<Task> beforeRead) : DbCommandInterceptor
    {
        private int _triggered;
        public bool Triggered => Volatile.Read(ref _triggered) == 1;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"가입고사진신청\"", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref _triggered, 1, 0) == 0)
                await beforeRead();
            return result;
        }
    }
}
