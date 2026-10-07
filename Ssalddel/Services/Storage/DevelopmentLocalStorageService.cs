using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Ssalddel.Services.Storage;

public sealed class DevelopmentLocalStorageService : IObjectStorageService
{
    public const string PublicContainerName = "development-local";
    public const string PrivateContainerName = "development-private";
    public const string PublicRequestPath = "/local-storage";
    public const string PublicStorageDirectoryName = ".local-storage";
    public const string PrivateStorageDirectoryName = ".local-private-storage";

    private readonly string _publicStorageRoot;
    private readonly string _privateStorageRoot;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DevelopmentLocalStorageService(
        IHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor)
    {
        _publicStorageRoot = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, PublicStorageDirectoryName));
        _privateStorageRoot = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, PrivateStorageDirectoryName));
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsConfigured(ObjectStorageAccess access) => true;

    public async Task<ObjectStorageUploadResult> UploadAsync(
        Stream stream,
        string originalFileName,
        string? contentType,
        string? folder,
        ObjectStorageAccess access,
        CancellationToken cancellationToken = default)
    {
        if (stream is null || !stream.CanRead)
        {
            throw new ArgumentException("Readable stream is required.", nameof(stream));
        }

        var containerName = ResolveContainerName(access);
        var objectName = ObjectStorageObjectName.Create(originalFileName, folder);
        var filePath = ResolveStoragePath(containerName, objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await using (var output = new FileStream(
                         filePath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 81920,
                         useAsync: true))
        {
            await stream.CopyToAsync(output, cancellationToken);
        }

        var url = access == ObjectStorageAccess.Public
            ? BuildPublicUrl(objectName)
            : BuildPrivateLocation(containerName, objectName);
        return new ObjectStorageUploadResult(containerName, objectName, url);
    }

    public async Task<ObjectStorageUploadResult> UploadImmutableAsync(
        Stream stream,
        string objectName,
        string? contentType,
        ObjectStorageAccess access,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (stream is null || !stream.CanRead)
        {
            throw new ArgumentException("Readable stream is required.", nameof(stream));
        }
        var containerName = ResolveContainerName(access);
        var normalizedObjectName = ObjectStorageObjectName.NormalizeProvided(objectName);
        var filePath = ResolveStoragePath(containerName, normalizedObjectName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        try
        {
            await using var output = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await stream.CopyToAsync(output, cancellationToken);
        }
        catch (IOException) when (File.Exists(filePath))
        {
            // 같은 불변 object 재시도는 기존 파일을 유지한다.
        }
        var url = access == ObjectStorageAccess.Public
            ? BuildPublicUrl(normalizedObjectName)
            : BuildPrivateLocation(containerName, normalizedObjectName);
        return new ObjectStorageUploadResult(containerName, normalizedObjectName, url);
    }

    public Task<byte[]> DownloadAsync(
        string containerName,
        string objectName,
        CancellationToken cancellationToken = default)
        => File.ReadAllBytesAsync(ResolveStoragePath(containerName, objectName), cancellationToken);

    public Task<ObjectStorageDeleteResult> DeleteAsync(string containerName, string objectName, string expectedOwnedPrefix,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = ObjectStorageObjectName.RequireOwnedDeletionName(objectName, expectedOwnedPrefix);
        var path = ResolveStoragePath(containerName, name);
        // Reparse points can escape the lexical root. Never follow a link during deletion.
        var current = new FileInfo(path);
        if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Linked storage objects cannot be deleted through this adapter.");
        for (var parent = current.Directory; parent is not null; parent = parent.Parent)
        {
            if (parent.Exists && parent.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("Linked storage directories cannot be deleted through this adapter.");
            if (parent.FullName == _publicStorageRoot || parent.FullName == _privateStorageRoot) break;
        }
        File.Delete(path);
        return Task.FromResult(new ObjectStorageDeleteResult(!File.Exists(path), true));
    }

    private string BuildPublicUrl(string objectName)
    {
        var encodedObjectName = EncodeObjectName(objectName);
        var relativeUrl = $"{PublicRequestPath}/{encodedObjectName}";
        var request = _httpContextAccessor.HttpContext?.Request;

        return request is null || !request.Host.HasValue
            ? relativeUrl
            : $"{request.Scheme}://{request.Host}{request.PathBase}{relativeUrl}";
    }

    private static string BuildPrivateLocation(string containerName, string objectName)
        => $"local-storage-private://{containerName}/{EncodeObjectName(objectName)}";

    private static string EncodeObjectName(string objectName)
        => string.Join(
            '/',
            objectName.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    private string ResolveStoragePath(string containerName, string objectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        var storageRoot = containerName switch
        {
            PublicContainerName => _publicStorageRoot,
            PrivateContainerName => _privateStorageRoot,
            _ => throw new FileNotFoundException($"Unknown development storage container: {containerName}")
        };

        var relativePath = objectName.Replace('/', Path.DirectorySeparatorChar);
        var resolvedPath = Path.GetFullPath(Path.Combine(storageRoot, relativePath));
        var rootPrefix = storageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage object path must remain inside the development storage root.");
        }

        return resolvedPath;
    }

    private static string ResolveContainerName(ObjectStorageAccess access)
        => access == ObjectStorageAccess.Public
            ? PublicContainerName
            : PrivateContainerName;
}
