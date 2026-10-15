using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Files;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class CargoPhotoCloseoutR69Tests
{
    [Fact]
    public void 구형첨부원문을보존하며_손상된신규내부영수증은노출하지않는다()
    {
        Assert.Equal("비공개 첨부", 화물운송사진증빙Policy.ClientAttachments("비공개 첨부"));
        Assert.Empty(화물운송사진증빙Policy.ClientAttachments("[{\"kind\":\"cargo-photo-upload-receipt-v1\",\"containerName\":\"internal\""));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 실제업로드영수증과파일을확인한뒤에만현재단계를완료하고_반복시최초완료증빙을보존한다(bool pickup)
    {
        await using var db = Context(); var transport = await Seed(db, pickup);
        var storage = new CargoPhotoTestStorage();
        var uploaded = await Upload(db, storage, transport.Id, pickup, "driver-1");
        Assert.True(uploaded.IsSuccess);
        Assert.Equal(pickup ? "상차지도착" : "하차지도착", transport.상태);
        Assert.Empty(db.운송이벤트);
        var executor = Executor(db);
        async Task<FluentResults.Result<Ssalddel.Contracts.Driver.Transport.기사운송상태변경응답>> Complete()
            => pickup ? await new 운송상차완료CommandHandler(db, executor, new 운송증빙첨부JsonWriter(), storage)
                    .Handle(new("driver-1", transport.Id) { 상차사진ObjectName = uploaded.Value.ObjectName, 상차사진Url = "https://forged.invalid/x" }, default)
                : await new 운송인수완료CommandHandler(executor, new 운송증빙첨부JsonWriter(), db, storage)
                    .Handle(new("driver-1", transport.Id) { 하차사진ObjectName = uploaded.Value.ObjectName, 하차사진Url = "https://forged.invalid/x" }, default);
        Assert.True((await Complete()).IsSuccess);
        var first = transport.첨부_json;
        Assert.True((await Complete()).IsSuccess);
        Assert.Equal(pickup ? "상차완료" : "인수완료", transport.상태);
        Assert.Equal(first, transport.첨부_json);
        Assert.DoesNotContain("forged.invalid", first);
        Assert.Single(db.운송이벤트);
    }

    [Theory]
    [InlineData("missing-receipt", "CargoPhotoUploadReceiptRequired")]
    [InlineData("wrong-driver", "CargoPhotoUploadReceiptRequired")]
    [InlineData("wrong-transport", "CargoPhotoUploadReceiptRequired")]
    [InlineData("wrong-stage", "CargoPhotoUploadReceiptRequired")]
    [InlineData("old-assignment-round", "CargoPhotoUploadReceiptRequired")]
    [InlineData("tampered", "CargoPhotoIntegrityMismatch")]
    [InlineData("deleted-object", "CargoPhotoStorageVerificationPending")]
    [InlineData("storage-failure", "CargoPhotoStorageVerificationPending")]
    [InlineData("legacy-photo", "CargoPhotoUploadReceiptRequired")]
    public async Task 확인되지않은사진으로완료하지않고원상태를보존한다(string scenario, string code)
    {
        await using var db = Context(); var transport = await Seed(db, false);
        var storage = new CargoPhotoTestStorage();
        if (scenario != "missing-receipt" && scenario != "legacy-photo")
        {
            await storage.SeedReceiptAsync(db, transport.Id, scenario == "wrong-driver" ? "another-driver" : "driver-1",
                scenario == "wrong-stage" ? "pickup-complete-photo" : "dropoff-complete-photo", "proof/photo.jpg");
            if (scenario == "wrong-transport") transport.첨부_json = transport.첨부_json.Replace("\"transportId\":" + transport.Id, "\"transportId\":999");
            if (scenario == "old-assignment-round") transport.추천라운드++;
            if (scenario == "tampered") storage.Objects["proof/photo.jpg"] = [1, 2, 3];
            if (scenario == "deleted-object") storage.Objects.Clear();
            if (scenario == "storage-failure") storage.DownloadFails = true;
        }
        else if (scenario == "legacy-photo") transport.첨부_json = "[{\"kind\":\"dropoff-complete-photo\",\"objectName\":\"proof/photo.jpg\"}]";
        await db.SaveChangesAsync(); var before = transport.첨부_json;
        var result = await new 운송인수완료CommandHandler(Executor(db), new 운송증빙첨부JsonWriter(), db, storage)
            .Handle(new("driver-1", transport.Id) { 하차사진ObjectName = "proof/photo.jpg" }, default);
        Assert.True(result.IsFailed); Assert.Equal(code, result.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal("하차지도착", transport.상태); Assert.Null(transport.도착);
        Assert.Equal(before, transport.첨부_json); Assert.Empty(db.운송이벤트);
    }

    [Theory]
    [InlineData("wrong-driver")]
    [InlineData("wrong-stage")]
    [InlineData("not-photo")]
    [InlineData("oversize")]
    public async Task 업로드단계에서타인업무나잘못된단계와파일을차단한다(string scenario)
    {
        await using var db = Context(); var transport = await Seed(db, true);
        var storage = new CargoPhotoTestStorage();
        var bytes = scenario == "not-photo" ? new byte[] { 1, 2, 3 }
            : scenario == "oversize" ? new byte[화물운송사진증빙Policy.MaximumBytes + 1] : CargoPhotoTestStorage.Photo();
        var result = await Upload(db, storage, transport.Id, scenario != "wrong-stage",
            scenario == "wrong-driver" ? "other-driver" : "driver-1", bytes);
        Assert.True(result.IsFailed); Assert.Equal(0, storage.UploadCount);
        Assert.Equal("[]", transport.첨부_json); Assert.Equal("상차지도착", transport.상태);
    }

    [Fact]
    public async Task 일반업무파일업로드는화물사진의단계조건으로확대하지않는다()
    {
        await using var db = Context(); var storage = new CargoPhotoTestStorage();
        using var stream = new MemoryStream([1, 2, 3]);
        var result = await new 파일업로드UseCase(storage, new Paths(), db, new User("user-1", "화주"))
            .업로드Async(new(new FormFile(stream, 0, 3, "file", "example.txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" }, "OtherCommand", "non-transport"), default);
        Assert.True(result.IsSuccess); Assert.Equal(1, storage.UploadCount); Assert.Empty(db.운송원장);
    }

    [Fact]
    public async Task 상세재조회는기존가림을유지하며서버사진영수증을노출하지않는다()
    {
        await using var db = Context(); var row = await Seed(db, true); var storage = new CargoPhotoTestStorage();
        await storage.SeedReceiptAsync(db, row.Id, "driver-1", "pickup-complete-photo", "own/photo.jpg");
        await storage.SeedReceiptAsync(db, row.Id, "previous-driver", "pickup-complete-photo", "other/photo.jpg");
        var detail = await new 운송상세조회QueryHandler(db).Handle(new("driver-1", row.Id), default);
        Assert.NotNull(detail); Assert.Empty(detail.첨부Json);
        Assert.Contains(화물운송사진증빙Policy.UploadReceiptKind, (await db.운송원장.SingleAsync()).첨부_json);
    }

    [Fact]
    public async Task 일반화물상세는기존완료사진첨부를보존하며새서버업로드영수증만제외한다()
    {
        await using var db = Context(); var row = await Seed(db, false); var storage = new CargoPhotoTestStorage();
        await storage.SeedReceiptAsync(db, row.Id, "driver-1", "dropoff-complete-photo", "private/uploaded-photo.jpg");
        new 운송증빙첨부JsonWriter().추가(row, new("dropoff-complete-photo", "completed/photo.jpg", "https://private.invalid/completed",
            "driver-1", DateTime.UtcNow, new Dictionary<string, object?>()));
        await db.SaveChangesAsync(); var original = row.첨부_json;
        var detail = await new 운송상세조회QueryHandler(db).Handle(new("driver-1", row.Id), default);
        Assert.NotNull(detail); Assert.Contains("dropoff-complete-photo", detail.첨부Json);
        Assert.Contains("completed/photo.jpg", detail.첨부Json);
        Assert.DoesNotContain(화물운송사진증빙Policy.UploadReceiptKind, detail.첨부Json);
        Assert.DoesNotContain("containerName", detail.첨부Json); Assert.DoesNotContain("sha256", detail.첨부Json);
        Assert.DoesNotContain("private/uploaded-photo.jpg", detail.첨부Json);
        Assert.Equal(original, (await db.운송원장.AsNoTracking().SingleAsync()).첨부_json);
    }

    private static async Task<FluentResults.Result<파일업로드응답>> Upload(SsalddelContext db, CargoPhotoTestStorage storage,
        long id, bool pickup, string actor, byte[]? bytes = null)
    {
        var photo = bytes ?? CargoPhotoTestStorage.Photo(); using var stream = new MemoryStream(photo);
        return await new 파일업로드UseCase(storage, new Paths(), db, new User(actor, "기사")).업로드Async(
            new(new FormFile(stream, 0, photo.Length, "file", "proof.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" },
                pickup ? "TransportPickupComplete" : "TransportDropoffComplete", id.ToString()), default);
    }
    private static 기사운송상태변경CommandExecutor Executor(SsalddelContext db) => new(db,
        new 기사운송상태전이Service(), new Publisher(), new User("driver-1", "기사"), new 참여자실행권한검사(),
        NullLogger<기사운송상태변경CommandExecutor>.Instance);
    private static async Task<운송원장> Seed(SsalddelContext db, bool pickup)
    {
        var row = new 운송원장 { 의뢰Id = "request", 운송번호 = "request", 기사_운송자 = "driver-1", 확정기사Id = "driver-1",
            상태 = pickup ? "상차지도착" : "하차지도착", 첨부_json = "[]" };
        db.Add(row); await db.SaveChangesAsync(); return row;
    }
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("cargo-photo-r69-" + Guid.NewGuid()).Options, new Encryption());
    private sealed record User(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Paths : ICommandFileStoragePathResolver
    { public string ResolveCommandFolder(string name, string? id = null) => "cargo-test/" + name + "/" + id;
      public string ResolveAdminFilePodFolder(string type, string? id = null) => throw new NotSupportedException(); }
    private sealed class Publisher : IPublisher
    { public Task Publish(object value, CancellationToken ct = default) => Task.CompletedTask;
      public Task Publish<T>(T value, CancellationToken ct = default) where T : INotification => Task.CompletedTask; }
}
