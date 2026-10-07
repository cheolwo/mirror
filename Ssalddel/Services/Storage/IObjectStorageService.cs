namespace Ssalddel.Services.Storage;

public enum ObjectStorageAccess
{
    Public,
    Private
}

public interface IObjectStorageService
{
    bool IsConfigured(ObjectStorageAccess access);

    Task<ObjectStorageUploadResult> UploadAsync(
        Stream stream,
        string originalFileName,
        string? contentType,
        string? folder,
        ObjectStorageAccess access,
        CancellationToken cancellationToken = default);

    Task<ObjectStorageUploadResult> UploadImmutableAsync(
        Stream stream,
        string objectName,
        string? contentType,
        ObjectStorageAccess access,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This object storage adapter does not support immutable named uploads.");

    Task<byte[]> DownloadAsync(
        string containerName,
        string objectName,
        CancellationToken cancellationToken = default);

    Task<ObjectStorageDeleteResult> DeleteAsync(
        string containerName, string objectName, string expectedOwnedPrefix,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This object storage adapter does not support verified deletion.");
}

public sealed record ObjectStorageDeleteResult(bool ActiveObjectAbsent, bool HistoricalCopiesVerifiedAbsent, string LimitationCode = "");

public sealed record ObjectStorageUploadResult(
    string ContainerName,
    string ObjectName,
    string Url,
    string ETag = "");

internal static class ObjectStorageObjectName
{
    public static string RequireOwnedDeletionName(string objectName, string expectedOwnedPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedOwnedPrefix);
        var name = objectName.Replace('\\', '/');
        var prefix = expectedOwnedPrefix.Replace('\\', '/').TrimEnd('/') + "/";
        if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(x => x is "" or "." or "..")
            || prefix.StartsWith('/') || prefix.Contains(':') || prefix.Split('/').SkipLast(1).Any(x => x is "" or "." or "..")
            || !name.StartsWith(prefix, StringComparison.Ordinal) || name.Length > 1024)
            throw new InvalidOperationException("Object deletion requires an exact object inside the server-owned prefix.");
        return name;
    }
    public static string Create(string originalFileName, string? folder)
    {
        var extension = Path.GetExtension(Path.GetFileName(originalFileName));
        var generatedFileName = $"{Guid.NewGuid():N}{extension}";
        var normalizedFolder = NormalizeFolder(folder);

        return string.IsNullOrWhiteSpace(normalizedFolder)
            ? generatedFileName
            : $"{normalizedFolder}/{generatedFileName}";
    }

    public static string NormalizeProvided(string objectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        var segments = objectName
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFileName)
            .Where(segment => !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..")
            .ToArray();
        var normalized = string.Join('/', segments);
        if (normalized.Length == 0 || normalized.Length > 1024)
        {
            throw new ArgumentException("Object storage object name is invalid.", nameof(objectName));
        }
        return normalized;
    }

    private static string NormalizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return string.Empty;
        }

        var segments = folder
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFileName)
            .Where(segment => !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..");

        return string.Join('/', segments);
    }
}
