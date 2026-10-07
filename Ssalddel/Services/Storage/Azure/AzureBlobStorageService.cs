using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Storage.Azure;

public sealed class AzureBlobStorageService : IObjectStorageService
{
    private readonly AzureBlobStorageOptions _options;
    private readonly Lazy<BlobServiceClient> _client;

    public AzureBlobStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        _options = options.Value;
        _client = new Lazy<BlobServiceClient>(
            CreateClient,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsConfigured(ObjectStorageAccess access)
        => Uri.TryCreate(_options.ServiceUri, UriKind.Absolute, out _)
           && !string.IsNullOrWhiteSpace(ResolveContainerName(access));

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

        EnsureConfigured(access);
        var containerName = ResolveContainerName(access);
        var objectName = ObjectStorageObjectName.Create(originalFileName, folder);
        var blob = _client.Value
            .GetBlobContainerClient(containerName)
            .GetBlobClient(objectName);

        var response = await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(contentType)
                        ? "application/octet-stream"
                        : contentType
                }
            },
            cancellationToken);

        return new ObjectStorageUploadResult(
            containerName,
            objectName,
            blob.Uri.AbsoluteUri,
            response.Value.ETag.ToString());
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

        EnsureConfigured(access);
        var containerName = ResolveContainerName(access);
        var normalizedObjectName = ObjectStorageObjectName.NormalizeProvided(objectName);
        var blob = _client.Value
            .GetBlobContainerClient(containerName)
            .GetBlobClient(normalizedObjectName);
        try
        {
            var response = await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = string.IsNullOrWhiteSpace(contentType)
                            ? "application/octet-stream"
                            : contentType
                    },
                    Metadata = metadata is null
                        ? null
                        : new Dictionary<string, string>(metadata, StringComparer.Ordinal),
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                },
                cancellationToken);
            return new ObjectStorageUploadResult(
                containerName,
                normalizedObjectName,
                blob.Uri.AbsoluteUri,
                response.Value.ETag.ToString());
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            return new ObjectStorageUploadResult(
                containerName,
                normalizedObjectName,
                blob.Uri.AbsoluteUri,
                properties.Value.ETag.ToString());
        }
    }

    public async Task<byte[]> DownloadAsync(
        string containerName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        EnsureKnownContainer(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        var response = await _client.Value
            .GetBlobContainerClient(containerName)
            .GetBlobClient(objectName)
            .DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToArray();
    }

    public async Task<ObjectStorageDeleteResult> DeleteAsync(string containerName, string objectName, string expectedOwnedPrefix,
        CancellationToken cancellationToken = default)
    {
        EnsureKnownContainer(containerName);
        var name = ObjectStorageObjectName.RequireOwnedDeletionName(objectName, expectedOwnedPrefix);
        var blob = _client.Value.GetBlobContainerClient(containerName).GetBlobClient(name);
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        var exists = await blob.ExistsAsync(cancellationToken);
        // Storage-account soft deletion, previous versions and backups require a separately verified lifecycle policy.
        return new ObjectStorageDeleteResult(!exists.Value, false, "AzureHistoricalCopiesRequireLifecycleVerification");
    }

    private BlobServiceClient CreateClient()
    {
        if (!Uri.TryCreate(_options.ServiceUri, UriKind.Absolute, out var serviceUri))
        {
            throw new InvalidOperationException("AzureBlobStorage:ServiceUri configuration is required.");
        }

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(_options.ManagedIdentityClientId))
        {
            credentialOptions.ManagedIdentityClientId = _options.ManagedIdentityClientId.Trim();
        }

        return new BlobServiceClient(serviceUri, new DefaultAzureCredential(credentialOptions));
    }

    private string ResolveContainerName(ObjectStorageAccess access)
        => access == ObjectStorageAccess.Public
            ? (_options.PublicContainerName ?? string.Empty).Trim()
            : (_options.PrivateContainerName ?? string.Empty).Trim();

    private void EnsureConfigured(ObjectStorageAccess access)
    {
        if (!IsConfigured(access))
        {
            throw new InvalidOperationException(
                $"Azure Blob Storage configuration is incomplete for {access} objects.");
        }
    }

    private void EnsureKnownContainer(string containerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        if (!string.Equals(containerName, ResolveContainerName(ObjectStorageAccess.Public), StringComparison.Ordinal)
            && !string.Equals(containerName, ResolveContainerName(ObjectStorageAccess.Private), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The requested container is outside the configured object storage boundary.");
        }
    }
}
