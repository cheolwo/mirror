using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using System.Net;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Storage;

namespace 살뜰.Services.External.Google
{
    public sealed class GoogleCloudStorageService : IObjectStorageService
    {
        private readonly GoogleCloudStorageOptions _options;
        private readonly Lazy<StorageClient> _storageClient;

        public GoogleCloudStorageService(IOptions<GoogleCloudStorageOptions> options)
        {
            _options = options.Value;
            _storageClient = new Lazy<StorageClient>(
                () => CreateStorageClient(_options),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public bool IsConfigured(ObjectStorageAccess access)
            => !string.IsNullOrWhiteSpace(ResolveBucketName(access));

        public async Task<ObjectStorageUploadResult> UploadAsync(
            Stream stream,
            string originalFileName,
            string? contentType,
            string? folder,
            ObjectStorageAccess access,
            CancellationToken cancellationToken = default)
        {
            var bucketName = ResolveBucketName(access);
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                throw new InvalidOperationException($"Google Cloud Storage bucket configuration is required for {access} objects.");
            }

            if (stream == null || !stream.CanRead)
            {
                throw new ArgumentException("Readable stream is required.", nameof(stream));
            }

            var objectName = ObjectStorageObjectName.Create(originalFileName, folder);
            var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType;

            var uploaded = await _storageClient.Value.UploadObjectAsync(
                bucket: bucketName,
                objectName: objectName,
                contentType: resolvedContentType,
                source: stream,
                cancellationToken: cancellationToken);

            var url = $"{_options.PublicBaseUrl.TrimEnd('/')}/{bucketName}/{objectName}";
            return new ObjectStorageUploadResult(bucketName, objectName, url, uploaded.ETag ?? string.Empty);
        }

        public async Task<ObjectStorageUploadResult> UploadImmutableAsync(
            Stream stream,
            string objectName,
            string? contentType,
            ObjectStorageAccess access,
            IReadOnlyDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            var bucketName = ResolveBucketName(access);
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                throw new InvalidOperationException($"Google Cloud Storage bucket configuration is required for {access} objects.");
            }
            if (stream is null || !stream.CanRead)
            {
                throw new ArgumentException("Readable stream is required.", nameof(stream));
            }

            var normalizedObjectName = ObjectStorageObjectName.NormalizeProvided(objectName);
            var destination = new global::Google.Apis.Storage.v1.Data.Object
            {
                Bucket = bucketName,
                Name = normalizedObjectName,
                ContentType = string.IsNullOrWhiteSpace(contentType)
                    ? "application/octet-stream"
                    : contentType,
                Metadata = metadata is null
                    ? null
                    : new Dictionary<string, string>(metadata, StringComparer.Ordinal)
            };
            global::Google.Apis.Storage.v1.Data.Object immutableObject;
            try
            {
                immutableObject = await _storageClient.Value.UploadObjectAsync(
                    destination,
                    stream,
                    new UploadObjectOptions { IfGenerationMatch = 0 },
                    cancellationToken);
            }
            catch (global::Google.GoogleApiException exception) when (
                exception.HttpStatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
            {
                immutableObject = await _storageClient.Value.GetObjectAsync(
                    bucketName,
                    normalizedObjectName,
                    cancellationToken: cancellationToken);
            }

            var url = $"{_options.PublicBaseUrl.TrimEnd('/')}/{bucketName}/{normalizedObjectName}";
            return new ObjectStorageUploadResult(
                bucketName,
                normalizedObjectName,
                url,
                immutableObject.ETag ?? string.Empty);
        }

        public async Task<byte[]> DownloadAsync(
            string bucketName,
            string objectName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
            ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
            EnsureKnownBucket(bucketName);

            await using var stream = new MemoryStream();
            await _storageClient.Value.DownloadObjectAsync(
                bucketName,
                objectName,
                stream,
                cancellationToken: cancellationToken);
            return stream.ToArray();
        }

        public async Task<ObjectStorageDeleteResult> DeleteAsync(string bucketName, string objectName, string expectedOwnedPrefix,
            CancellationToken cancellationToken = default)
        {
            EnsureKnownBucket(bucketName);
            var name = ObjectStorageObjectName.RequireOwnedDeletionName(objectName, expectedOwnedPrefix);
            try { await _storageClient.Value.DeleteObjectAsync(bucketName, name, cancellationToken: cancellationToken); }
            catch (global::Google.GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound) { }
            try
            {
                await _storageClient.Value.GetObjectAsync(bucketName, name, cancellationToken: cancellationToken);
                return new ObjectStorageDeleteResult(false, false, "ActiveObjectStillExists");
            }
            catch (global::Google.GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return new ObjectStorageDeleteResult(true, false, "GoogleHistoricalCopiesRequireLifecycleVerification");
            }
        }

        private static StorageClient CreateStorageClient(GoogleCloudStorageOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.ServiceAccountJsonPath))
            {
                var credential = CredentialFactory.FromFile<GoogleCredential>(options.ServiceAccountJsonPath);
                return StorageClient.Create(credential);
            }

            return StorageClient.Create();
        }

        private string ResolveBucketName(ObjectStorageAccess access)
            => access == ObjectStorageAccess.Public
                ? FirstConfigured(_options.PublicBucketName, _options.BucketName)
                : FirstConfigured(_options.PrivateBucketName, _options.BucketName);

        private void EnsureKnownBucket(string bucketName)
        {
            var publicBucket = ResolveBucketName(ObjectStorageAccess.Public);
            var privateBucket = ResolveBucketName(ObjectStorageAccess.Private);
            if (!string.Equals(bucketName, publicBucket, StringComparison.Ordinal)
                && !string.Equals(bucketName, privateBucket, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The requested bucket is outside the configured object storage boundary.");
            }
        }

        private static string FirstConfigured(string? preferred, string? fallback)
            => string.IsNullOrWhiteSpace(preferred)
                ? fallback?.Trim() ?? string.Empty
                : preferred.Trim();
    }
}



