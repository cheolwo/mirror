using SkiaSharp;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Services.Storage;
using 살뜰.Data;

namespace Ssalddel.Tests.Application.Driver.Transport;

internal sealed class CargoPhotoTestStorage : IObjectStorageService
{
    public Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
    public string NextObjectName { get; set; } = "proof/photo.jpg";
    public bool Configured { get; set; } = true;
    public bool DownloadFails { get; set; }
    public int UploadCount { get; private set; }
    public bool IsConfigured(ObjectStorageAccess access) => Configured;
    public string? GetContainerName(ObjectStorageAccess access) => "private-cargo-test";
    public async Task<ObjectStorageUploadResult> UploadAsync(Stream stream, string fileName, string? contentType,
        string? folder, ObjectStorageAccess access, CancellationToken ct = default)
    {
        Assert.Equal(ObjectStorageAccess.Private, access);
        using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes, ct);
        Objects[NextObjectName] = bytes.ToArray(); UploadCount++;
        return new("private-cargo-test", NextObjectName, "https://private.invalid/" + NextObjectName);
    }
    public Task<byte[]> DownloadAsync(string container, string name, CancellationToken ct = default)
    {
        if (DownloadFails) throw new IOException("unavailable");
        Assert.Equal("private-cargo-test", container);
        return Task.FromResult(Objects.TryGetValue(name, out var value) ? value : throw new FileNotFoundException());
    }
    public static byte[] Photo()
    {
        using var bitmap = new SKBitmap(2, 2); bitmap.Erase(SKColors.Gray);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }
    public async Task SeedReceiptAsync(SsalddelContext db, long id, string actor, string kind, string name)
    {
        var row = await db.운송원장.FindAsync(id) ?? throw new InvalidOperationException();
        var bytes = Photo(); Objects[name] = bytes;
        화물운송사진증빙Policy.RecordUpload(row, actor, kind,
            new("private-cargo-test", name, "https://private.invalid/" + name), bytes);
        await db.SaveChangesAsync();
    }
}
