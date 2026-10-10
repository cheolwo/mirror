using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

internal sealed record DistributedPhotoPrivate(DistributedEvidenceBinding Binding, string Container,
    string ObjectName, DateTime? CapturedAtUtc, string? ImportedPlanId = null, string[]? ImportedLotIds = null);

/// <summary>사진 바이트의 보관과 업무 명령의 소비는 별개이며 소비는 업무 SQL 트랜잭션 안에서만 가능합니다.</summary>
public sealed partial class DistributedPhotoEvidenceService(SsalddelContext db, IObjectStorageService storage,
    ISsalddelExecutionModePolicy execution, IOptions<DistributedFulfillmentOptions> options, TimeProvider clock,
    Ssalddel.Services.OperationsAlerts.OperationsBudgetGate? operationsBudget = null)
    : IDistributedPhotoEvidenceBinder
{
    internal const string OwnedPrefix = "distributed-proof";
    public const int MaximumBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private IQueryable<분산사진증빙Record> Photos => db.Set<분산사진증빙Record>();
    private static void Require(bool condition, string code, int status = 409)
    { if (!condition) throw new DistributedFulfillmentException(code, status); }
    private void Gate(string actor)
    {
        Require(options.Value.Enabled && execution.IsSimulation && options.Value.Profile == DistributedFulfillmentCodes.Profile,
            "DistributedFulfillmentDisabled", 404);
        Require(!string.IsNullOrWhiteSpace(actor), "DistributedAuthenticationRequired", 401);
        Require(db.Database.IsRelational(), "DistributedRelationalStorageRequired");
    }
    internal static DistributedPhotoPrivate Details(분산사진증빙Record row)
        => JsonSerializer.Deserialize<DistributedPhotoPrivate>(row.PrivateDetailsJson, Json)
           ?? throw new DistributedFulfillmentException("DistributedPhotoMetadataInvalid");
    internal static string Encode(DistributedPhotoPrivate value) => JsonSerializer.Serialize(value, Json);
    internal static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public async Task<DistributedPhotoEvidenceDto> UploadAsync(DistributedActor actor, DistributedEvidenceBinding binding,
        DistributedPhotoUploadRequest request, CancellationToken ct)
    {
        Gate(actor.UserId);
        Require(binding.ActorUserId == actor.UserId && binding.RequestId != Guid.Empty && request.UploadRequestId != Guid.Empty,
            "DistributedPhotoRequestInvalid", 400);
        Require(request.CapturedAtUtc is null || (request.CapturedAtUtc.Value.Kind == DateTimeKind.Utc
            && request.CapturedAtUtc <= Now.AddMinutes(5)), "DistributedPhotoCaptureTimeInvalid", 400);
        Require(storage.IsConfigured(ObjectStorageAccess.Private), "DistributedPrivatePhotoStorageUnavailable", 503);
        var container = storage.GetContainerName(ObjectStorageAccess.Private);
        Require(!string.IsNullOrWhiteSpace(container), "DistributedPrivatePhotoStorageUnavailable", 503);
        var raw = Decode(request.ImageBase64, request.ContentType);
        var inputHash = Sha(raw); var digest = DistributedEvidenceDigests.Binding(binding);
        var row = await Photos.SingleOrDefaultAsync(x => x.ActorUserId == actor.UserId && x.UploadRequestId == request.UploadRequestId, ct);
        if (row is not null)
        {
            Require(row.InputSha256 == inputHash && row.BindingDigest == digest && Details(row).CapturedAtUtc == request.CapturedAtUtc,
                "DistributedPhotoUploadContentConflict");
            Require(row.CreatedAtUtc > Now.AddHours(-24) || row.StatusCode == "Bound", "DistributedPhotoUploadExpired");
            if (row.StatusCode is "Uploaded" or "Bound") return Dto(row, true);
            Require(row.StatusCode == "Uploading", "DistributedPhotoUploadUnavailable");
        }
        else
        {
            if (operationsBudget is not null) await operationsBudget.ConsumeAsync("photos", true, ct);
            var id = "DPH-" + Guid.NewGuid().ToString("N");
            var actorHash = Sha(Encoding.UTF8.GetBytes(actor.UserId));
            row = new() { EvidenceId = id, ActorUserId = actor.UserId, UploadRequestId = request.UploadRequestId,
                CompletionRequestId = binding.RequestId, Kind = binding.Kind, AggregateId = binding.AggregateId,
                BindingDigest = digest, InputSha256 = inputHash, CreatedAtUtc = Now,
                PrivateDetailsJson = Encode(new(binding, container!, $"{OwnedPrefix}/{actorHash}/{id}.jpg", request.CapturedAtUtc)) };
            db.Add(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(row).State = EntityState.Detached;
                row = await Photos.SingleOrDefaultAsync(x => x.ActorUserId == actor.UserId && x.UploadRequestId == request.UploadRequestId, ct);
                Require(row is not null && row.InputSha256 == inputHash && row.BindingDigest == digest
                    && Details(row).CapturedAtUtc == request.CapturedAtUtc, "DistributedPhotoUploadContentConflict");
                Require(row!.CreatedAtUtc > Now.AddHours(-24) || row.StatusCode == "Bound", "DistributedPhotoUploadExpired");
                if (row!.StatusCode is "Uploaded" or "Bound") return Dto(row, true);
                Require(row.StatusCode == "Uploading", "DistributedPhotoUploadUnavailable");
            }
        }
        var bytes = Normalize(raw); var hash = Sha(bytes); var details = Details(row!);
        db.Entry(row!).State = EntityState.Detached;
        await using var uploadTransaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        // Hold the SQL row lock through the object write, so a 24-hour orphan sweep cannot delete and then be followed by a late write.
        var uploadClaim = await Photos.Where(x => x.EvidenceId == row!.EvidenceId && x.StatusCode == "Uploading" && x.Revision == row.Revision)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revision, x => x.Revision + 1), ct);
        Require(uploadClaim == 1, "DistributedPhotoUploadChanged"); row!.Revision++;
        using var stream = new MemoryStream(bytes, writable: false);
        var uploaded = await storage.UploadImmutableAsync(stream, details.ObjectName, "image/jpeg", ObjectStorageAccess.Private,
            new Dictionary<string, string> { ["sha256"] = hash }, ct);
        Require(uploaded.ObjectName == details.ObjectName && uploaded.ContainerName == details.Container, "DistributedPhotoStorageIdentityMismatch", 503);
        var stored = await storage.DownloadAsync(uploaded.ContainerName, uploaded.ObjectName, ct);
        Require(stored.Length == bytes.Length && Sha(stored) == hash, "DistributedPhotoStorageIntegrityMismatch", 503);
        var encoded = Encode(details with { Container = uploaded.ContainerName });
        var changed = await Photos.Where(x => x.EvidenceId == row!.EvidenceId && x.StatusCode == "Uploading" && x.Revision == row.Revision)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PrivateDetailsJson, encoded).SetProperty(x => x.StoredSha256, hash)
                .SetProperty(x => x.Bytes, bytes.LongLength).SetProperty(x => x.ContentType, "image/jpeg")
                .SetProperty(x => x.StatusCode, "Uploaded").SetProperty(x => x.UploadedAtUtc, Now)
                .SetProperty(x => x.Revision, x => x.Revision + 1), ct);
        Require(changed == 1, "DistributedPhotoUploadChanged");
        await uploadTransaction.CommitAsync(ct);
        return Dto(await Photos.AsNoTracking().SingleAsync(x => x.EvidenceId == row!.EvidenceId, ct), true);
    }

    public async Task BindAsync(DistributedEvidenceBinding binding, IReadOnlyList<string> photoEvidenceIds, CancellationToken ct)
    {
        Gate(binding.ActorUserId);
        Require(db.Database.CurrentTransaction is not null, "DistributedPhotoTransactionRequired");
        Require(photoEvidenceIds is { Count: >= 1 and <= 3 } && photoEvidenceIds.Distinct(StringComparer.Ordinal).Count() == photoEvidenceIds.Count,
            "DistributedPhotoEvidenceRequired", 400);
        var digest = DistributedEvidenceDigests.Binding(binding);
        foreach (var id in photoEvidenceIds)
        {
            var row = await Photos.AsNoTracking().SingleOrDefaultAsync(x => x.EvidenceId == id, ct);
            Require(row is not null && row.ActorUserId == binding.ActorUserId && row.CompletionRequestId == binding.RequestId
                && row.BindingDigest == digest && row.StatusCode == "Uploaded" && row.CreatedAtUtc > Now.AddHours(-24),
                "DistributedPhotoBindingMismatch");
            var details = Details(row!);
            var bytes = await storage.DownloadAsync(details.Container, details.ObjectName, ct);
            Require(bytes.LongLength == row!.Bytes && Sha(bytes) == row.StoredSha256, "DistributedPhotoStorageIntegrityMismatch", 503);
            var changed = await Photos.Where(x => x.EvidenceId == id && x.Revision == row.Revision && x.StatusCode == "Uploaded")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StatusCode, "Bound").SetProperty(x => x.BoundAtUtc, Now)
                    .SetProperty(x => x.Revision, x => x.Revision + 1), ct);
            Require(changed == 1, "DistributedPhotoBindingChanged");
        }
    }

    public async Task AttachImportedLineageAsync(Guid completionRequestId, string actorUserId, string planId,
        IReadOnlyList<string> lotIds, CancellationToken ct)
    {
        Require(db.Database.CurrentTransaction is not null && lotIds.Count > 0, "DistributedPhotoTransactionRequired");
        var rows = await Photos.AsNoTracking().Where(x => x.ActorUserId == actorUserId && x.CompletionRequestId == completionRequestId
            && x.Kind == "ImportLot" && x.StatusCode == "Bound").ToArrayAsync(ct);
        Require(rows.Length > 0, "DistributedPhotoEvidenceRequired");
        foreach (var row in rows)
        {
            var value = Encode(Details(row) with { ImportedPlanId = planId, ImportedLotIds = lotIds.ToArray() });
            Require(await Photos.Where(x => x.EvidenceId == row.EvidenceId && x.Revision == row.Revision)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PrivateDetailsJson, value).SetProperty(x => x.Revision, x => x.Revision + 1), ct) == 1,
                "DistributedPhotoBindingChanged");
        }
    }

    public async Task<DistributedPhotoEvidenceDto> GetAsync(DistributedActor actor, string id, CancellationToken ct)
    {
        Gate(actor.UserId); var row = await Photos.AsNoTracking().SingleOrDefaultAsync(x => x.EvidenceId == id, ct);
        Require(row is not null && await CanRead(actor, row, ct), "DistributedPhotoNotFound", 404);
        return Dto(row!, row!.StatusCode is "Uploaded" or "Bound");
    }
    public async Task<DistributedPhotoEvidenceDto> GetByUploadAsync(DistributedActor actor, Guid uploadRequestId, CancellationToken ct)
    {
        Gate(actor.UserId);
        var row = await Photos.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor.UserId && x.UploadRequestId == uploadRequestId, ct);
        Require(row is not null && await CanRead(actor, row, ct), "DistributedPhotoNotFound", 404);
        return Dto(row!, row!.StatusCode is "Uploaded" or "Bound");
    }
    public async Task<(byte[] Bytes, string ContentType)> ContentAsync(DistributedActor actor, string id, CancellationToken ct)
    {
        Gate(actor.UserId); var row = await Photos.AsNoTracking().SingleOrDefaultAsync(x => x.EvidenceId == id, ct);
        Require(row is not null && (row.StatusCode is "Uploaded" or "Bound") && await CanRead(actor, row, ct), "DistributedPhotoNotFound", 404);
        var details = Details(row!); var bytes = await storage.DownloadAsync(details.Container, details.ObjectName, ct);
        Require(bytes.LongLength == row!.Bytes && Sha(bytes) == row.StoredSha256, "DistributedPhotoStorageIntegrityMismatch", 503);
        return (bytes, row.ContentType);
    }
    private async Task<bool> CanRead(DistributedActor actor, 분산사진증빙Record row, CancellationToken ct)
    {
        if (actor.IsAdmin) return true;
        var d = Details(row); var b = d.Binding;
        var lotIds = d.ImportedLotIds ?? b.LotIds.ToArray();
        var lots = await db.Set<분산재고LotRecord>().AsNoTracking().Where(x => Enumerable.Contains(lotIds, x.LotId)).ToArrayAsync(ct);
        var sites = await db.Set<분산거점Record>().AsNoTracking().Where(x => Enumerable.Contains(b.SiteIds, x.SiteId)).ToArrayAsync(ct);
        // All represented subjects must grant access. Merely having uploaded a photograph is not a lasting grant.
        if (sites.Length == b.SiteIds.Count && sites.Length > 0 && sites.All(x => x.OwnerUserId == actor.UserId || x.ManagerUserId == actor.UserId)) return true;
        if (lots.Length == lotIds.Length && lots.Length > 0 && lots.All(x => x.OwnerUserId == actor.UserId || x.SellerUserId == actor.UserId)) return true;
        var orders = await db.Set<분산배송주문Record>().AsNoTracking().Where(x => Enumerable.Contains(b.OrderIds, x.OrderId)).ToArrayAsync(ct);
        if (b.Kind == "Deliver" && orders.Length == b.OrderIds.Count && orders.Length > 0 && orders.All(x => x.CustomerUserId == actor.UserId)) return true;
        if (actor.IsDriver && row.ActorUserId == actor.UserId && row.BoundAtUtc.GetValueOrDefault(row.CreatedAtUtc) > Now.AddDays(-3))
        {
            var wave = await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.WaveId == b.AggregateId, ct);
            var grant = wave is not null && wave.DriverId == actor.UserId && wave.StatusCode == "Accepted"
                && wave.DeliveryEndUtc > Now && orders.Length == b.OrderIds.Count && orders.Length > 0
                && orders.All(x => x.StatusCode is not ("Delivered" or "Cancelled"));
            if (!grant) return false;
            var cargoIds = orders.Select(x => x.CargoRequestId).ToArray();
            if (await db.운송원장.CountAsync(x => Enumerable.Contains(cargoIds, x.의뢰Id)
                && x.기사_운송자 == actor.UserId && x.확정기사Id == actor.UserId && x.상태 != "인수완료"
                && x.배차큐단계 != 살뜰.도메인.공통.상태값.배차큐단계.종료, ct) != orders.Length) return false;
            var sources = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(cargoIds, x.의뢰Id)).ToDictionaryAsync(x => x.의뢰Id, ct);
            return orders.All(x => sources.TryGetValue(x.CargoRequestId, out var source) && 분산물류UseCase.CargoDigest(source) == x.CargoEvidenceDigest);
        }
        return false;
    }
    private static DistributedPhotoEvidenceDto Dto(분산사진증빙Record r, bool read) => new(r.EvidenceId, r.Kind, r.AggregateId,
        r.CompletionRequestId, r.BindingDigest, r.StatusCode, r.UploadedAtUtc ?? r.CreatedAtUtc, read, r.ContentType, r.Bytes,
        Details(r).Binding.InputDigest);

    internal static byte[] Decode(string? value, string contentType)
    {
        Require((contentType is "image/jpeg" or "image/png") && value is not null && value.Length <= (MaximumBytes + 2) / 3 * 4,
            "DistributedPhotoInvalid", 400);
        byte[] raw;
        try { raw = Convert.FromBase64String(value!); }
        catch (FormatException) { throw new DistributedFulfillmentException("DistributedPhotoInvalid", 400); }
        Require(raw.Length is > 0 and <= MaximumBytes, "DistributedPhotoSizeInvalid", 400);
        using var codec = SKCodec.Create(new MemoryStream(raw, writable: false));
        Require(codec is not null && (codec.EncodedFormat is SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png)
            && codec.Info.Width is > 0 and <= 8192 && codec.Info.Height is > 0 and <= 8192
            && (long)codec.Info.Width * codec.Info.Height <= 32_000_000
            && ((codec.EncodedFormat == SKEncodedImageFormat.Jpeg && contentType == "image/jpeg")
                || (codec.EncodedFormat == SKEncodedImageFormat.Png && contentType == "image/png")), "DistributedPhotoInvalid", 400);
        return raw;
    }
    internal static byte[] Normalize(byte[] raw)
    {
        using var original = SKBitmap.Decode(raw);
        Require(original is not null, "DistributedPhotoInvalid", 400);
        var scale = Math.Min(1d, 2048d / Math.Max(original!.Width, original.Height));
        using var result = new SKBitmap(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale)));
        using var canvas = new SKCanvas(result); canvas.Clear(SKColors.White);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(original, new SKRect(0, 0, result.Width, result.Height), new SKSamplingOptions(SKFilterMode.Linear), paint);
        using var image = SKImage.FromBitmap(result); using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        var bytes = data.ToArray(); Require(bytes.Length <= MaximumBytes, "DistributedPhotoSizeInvalid", 400);
        return bytes;
    }
}
