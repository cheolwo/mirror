using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

public interface I가입고사진신청Service
{
    Task<가입고사진신청응답> CreateAsync(Guid requestId, string? name, IFormFile? photo, CancellationToken ct);
    Task<가입고사진신청목록응답> GetListAsync(int page, int pageSize, CancellationToken ct);
    Task<가입고사진신청응답?> GetAsync(Guid id, CancellationToken ct);
    Task<가입고사진내용응답?> GetPhotoAsync(Guid id, CancellationToken ct);
    Task<가입고사진신청응답> RenameAsync(Guid id, 가입고사진이름변경요청 request, CancellationToken ct);
}

/// <summary>사진으로 시작한 본인 준비 기록만 보관한다. 실제 입고·재고·계약을 만들지 않는다.</summary>
public sealed class 가입고사진신청Service(SsalddelContext db, ICurrentUserAccessor currentUser,
    IObjectStorageService storage, TimeProvider clock) : I가입고사진신청Service
{
    private const string Prefix = "inbound-photo-preparation";
    private IQueryable<가입고사진신청> Requests => db.Set<가입고사진신청>();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<가입고사진신청응답> CreateAsync(Guid requestId, string? name, IFormFile? photo, CancellationToken ct)
    {
        var actor = Actor();
        Require(requestId != Guid.Empty, "PhotoInboundRequestIdInvalid", "신청 번호를 확인해 주세요.");
        var normalizedName = Name(name);
        var bytes = await ReadPhoto(photo, ct);
        var contentType = PhotoContentType(bytes);
        var hash = Sha(bytes);
        var digest = Sha(JsonSerializer.SerializeToUtf8Bytes(new { schema = "photo-inbound-request.v1", photo = hash, name = normalizedName }));
        var existing = await Owned(actor, requestId, ct);
        if (existing is not null) return Replay(existing, digest);
        Require(storage.IsConfigured(ObjectStorageAccess.Private), "PhotoInboundStorageUnavailable", "사진 보관을 준비하지 못했습니다.", 503, definitivelyNotApplied: true);
        var container = storage.GetContainerName(ObjectStorageAccess.Private);
        Require(!string.IsNullOrWhiteSpace(container), "PhotoInboundStorageUnavailable", "사진 보관을 준비하지 못했습니다.", 503, definitivelyNotApplied: true);

        // 서버가 만든 위치만 사용한다. 원본 파일명·사용자 입력 경로는 보관하지 않는다.
        var objectName = $"{Prefix}/{Sha(Encoding.UTF8.GetBytes(actor))}/{requestId:N}/{hash}{(contentType == "image/png" ? ".png" : ".jpg")}";
        await using var stream = new MemoryStream(bytes, writable: false);
        var uploaded = await storage.UploadImmutableAsync(stream, objectName, contentType, ObjectStorageAccess.Private,
            new Dictionary<string, string> { ["sha256"] = hash }, ct);
        Require(uploaded.ContainerName == container && uploaded.ObjectName == objectName,
            "PhotoInboundStorageIdentityMismatch", "사진 보관 결과를 확인해 주세요.", 503);
        var stored = await storage.DownloadAsync(container!, objectName, ct);
        Require(stored.LongLength == bytes.LongLength && Sha(stored) == hash,
            "PhotoInboundStorageIntegrityMismatch", "사진을 다시 확인해 주세요.", 503);

        var row = new 가입고사진신청
        {
            Id = requestId, 작성자UserId = actor, 이름 = normalizedName, Revision = 1,
            사진Container = container!, 사진ObjectName = objectName, 사진Sha256 = hash, 최초요청해시 = digest,
            ContentType = contentType, Bytes = bytes.LongLength, CreatedAtUtc = Now, UpdatedAtUtc = Now
        };
        db.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(row).State = EntityState.Detached;
            var concurrent = await Owned(actor, requestId, ct);
            if (concurrent is null) throw;
            return Replay(concurrent, digest);
        }
        return Response(row);
    }

    public async Task<가입고사진신청목록응답> GetListAsync(int page, int pageSize, CancellationToken ct)
    {
        var actor = Actor();
        Require(page >= 0 && pageSize is >= 1 and <= 100, "PhotoInboundPageInvalid", "목록 범위를 확인해 주세요.");
        // DB의 대소문자 무시 정렬 규칙으로 다른 인증 ID의 기록을 노출하지 않는다.
        var candidates = await Requests.AsNoTracking().Include(x => x.입고요청)
            .Where(x => x.작성자UserId == actor).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToArrayAsync(ct);
        var owned = candidates.Where(x => string.Equals(x.작성자UserId, actor, StringComparison.Ordinal)).ToArray();
        return new() { Items = owned.Skip((int)Math.Min((long)page * pageSize, int.MaxValue)).Take(pageSize).Select(Response).ToArray(),
            TotalCount = owned.Length, Page = page, PageSize = pageSize };
    }

    public async Task<가입고사진신청응답?> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await Owned(Actor(), id, ct);
        return row is null ? null : Response(row);
    }

    public async Task<가입고사진내용응답?> GetPhotoAsync(Guid id, CancellationToken ct)
    {
        var row = await Owned(Actor(), id, ct);
        if (row is null) return null;
        var bytes = await storage.DownloadAsync(row.사진Container, row.사진ObjectName, ct);
        Require(bytes.LongLength == row.Bytes && Sha(bytes) == row.사진Sha256,
            "PhotoInboundStorageIntegrityMismatch", "사진을 다시 확인해 주세요.", 503);
        return new() { Bytes = bytes, ContentType = row.ContentType };
    }

    public async Task<가입고사진신청응답> RenameAsync(Guid id, 가입고사진이름변경요청 request, CancellationToken ct)
    {
        var actor = Actor();
        Require(request is not null && request.Revision > 0, "PhotoInboundRevisionInvalid", "신청 내용을 다시 확인해 주세요.");
        var row = await Requests.Include(x => x.입고요청).SingleOrDefaultAsync(x => x.Id == id && x.작성자UserId == actor, ct);
        Require(row is not null && string.Equals(row.작성자UserId, actor, StringComparison.Ordinal),
            "PhotoInboundNotFound", "신청을 찾을 수 없습니다.", 404);
        Require(row!.입고요청 is null, "PhotoInboundAlreadyConverted", "연결된 입고 내역을 확인해 주세요.", 409);
        Require(row!.Revision == request.Revision, "PhotoInboundRevisionConflict", "변경된 신청 내용을 다시 확인해 주세요.", 409);
        row.이름 = Name(request.이름); row.Revision++; row.UpdatedAtUtc = Now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(row).State = EntityState.Detached;
            throw new 가입고사진신청Exception("PhotoInboundRevisionConflict", "변경된 신청 내용을 다시 확인해 주세요.", 409);
        }
        return Response(row);
    }

    private async Task<가입고사진신청?> Owned(string actor, Guid id, CancellationToken ct)
    {
        var row = await Requests.AsNoTracking().Include(x => x.입고요청)
            .SingleOrDefaultAsync(x => x.Id == id && x.작성자UserId == actor, ct);
        return row is not null && string.Equals(row.작성자UserId, actor, StringComparison.Ordinal) ? row : null;
    }
    private string Actor() => !string.IsNullOrWhiteSpace(currentUser.UserId) ? currentUser.UserId
        : throw new 가입고사진신청Exception("PhotoInboundAuthenticationRequired", "로그인 후 다시 확인해 주세요.", 401);
    private static 가입고사진신청응답 Replay(가입고사진신청 row, string digest)
    {
        Require(row.최초요청해시 == digest, "PhotoInboundRequestConflict", "기존 신청 결과를 확인해 주세요.", 409);
        return Response(row);
    }
    private static 가입고사진신청응답 Response(가입고사진신청 row) => new()
        { Id = row.Id, 이름 = row.이름, Revision = row.Revision, CreatedAtUtc = row.CreatedAtUtc,
          UpdatedAtUtc = row.UpdatedAtUtc, 입고요청Id = row.입고요청?.Id };
    private static string Name(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        Require(value.Length <= 가입고사진신청Policy.MaximumNameLength, "PhotoInboundNameInvalid", "이름은 100자 이내로 입력해 주세요.");
        return value;
    }
    private static async Task<byte[]> ReadPhoto(IFormFile? photo, CancellationToken ct)
    {
        Require(photo is not null && photo.Length is > 0 and <= 가입고사진신청Policy.MaximumPhotoBytes,
            "PhotoInboundPhotoSizeInvalid", "5MB 이하 사진 한 장을 선택해 주세요.");
        await using var input = photo!.OpenReadStream();
        using var buffer = new MemoryStream();
        var block = new byte[81920]; int read;
        while ((read = await input.ReadAsync(block, ct)) != 0)
        {
            Require(buffer.Length + read <= 가입고사진신청Policy.MaximumPhotoBytes,
                "PhotoInboundPhotoSizeInvalid", "5MB 이하 사진 한 장을 선택해 주세요.");
            await buffer.WriteAsync(block.AsMemory(0, read), ct);
        }
        Require(buffer.Length == photo.Length, "PhotoInboundPhotoSizeInvalid", "사진을 다시 선택해 주세요.");
        return buffer.ToArray();
    }
    private static string PhotoContentType(byte[] bytes)
    {
        var png = bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        Require(png || jpeg, "PhotoInboundPhotoTypeInvalid", "JPEG 또는 PNG 사진을 선택해 주세요.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        Require(codec is not null && codec.Info.Width > 0 && codec.Info.Height > 0
            && (long)codec.Info.Width * codec.Info.Height <= 36_000_000,
            "PhotoInboundPhotoTypeInvalid", "사진을 다시 선택해 주세요.");
        using var bitmap = new SKBitmap(codec!.Info);
        Require(codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success,
            "PhotoInboundPhotoTypeInvalid", "사진을 다시 선택해 주세요.");
        return png ? "image/png" : "image/jpeg";
    }
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool condition, string code, string message, int status = 400, bool definitivelyNotApplied = false)
    { if (!condition) throw new 가입고사진신청Exception(code, message, status, definitivelyNotApplied); }
}

public sealed class 가입고사진신청Exception(string code, string message, int statusCode, bool definitivelyNotApplied = false) : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public bool DefinitivelyNotApplied { get; } = definitivelyNotApplied;
}
