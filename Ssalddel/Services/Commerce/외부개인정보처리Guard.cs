using Microsoft.Extensions.Options;
using Ssalddel.Services.Storage;

namespace Ssalddel.Services.Commerce;

/// <summary>클라우드 이름으로 처리 국가·법적 근거를 추정하지 않습니다. 실제 처리 경로별 검토가 필요합니다.</summary>
public sealed class 외부개인정보처리Options
{
    public const string SectionName = "ExternalPrivacyProcessing";
    public Dictionary<string, 외부처리검토> Providers { get; set; } = new(StringComparer.Ordinal);
}
public sealed class 외부처리검토
{
    public string Purpose { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string ProcessingCountry { get; set; } = string.Empty;
    public string DataFields { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string ContractReviewReference { get; set; } = string.Empty;
    public string NoticeVersion { get; set; } = string.Empty;
    public string TransferReviewReference { get; set; } = string.Empty;
}
public interface I외부개인정보처리Guard { bool 허용(string provider); }
public sealed class 외부개인정보처리Guard(IOptions<외부개인정보처리Options> options) : I외부개인정보처리Guard
{
    public bool 허용(string provider)
        => options.Value.Providers.TryGetValue(provider, out var review)
           && new[] { review.Purpose, review.Recipient, review.ProcessingCountry, review.DataFields, review.LegalBasis,
               review.ContractReviewReference, review.NoticeVersion, review.TransferReviewReference }.All(x => !string.IsNullOrWhiteSpace(x));
}

/// <summary>외부 전송 전 운영 경로 검토를 확인합니다. 개인별 동의·전송 법적 근거 검증을 대신하지 않습니다.</summary>
public sealed class 검토된ObjectStorageService(IObjectStorageService inner, I외부개인정보처리Guard guard, string provider) : IObjectStorageService
{
    private bool Allowed => provider == "Local" || guard.허용(provider);
    public bool IsConfigured(ObjectStorageAccess access) => Allowed && inner.IsConfigured(access);
    private void Require() { if (!Allowed) throw new InvalidOperationException("외부 저장소 처리 경로·위탁·국외 이전 검토가 필요합니다."); }
    public Task<ObjectStorageUploadResult> UploadAsync(Stream stream, string originalFileName, string? contentType, string? folder,
        ObjectStorageAccess access, CancellationToken cancellationToken = default)
    { Require(); return inner.UploadAsync(stream, originalFileName, contentType, folder, access, cancellationToken); }
    public Task<ObjectStorageUploadResult> UploadImmutableAsync(Stream stream, string objectName, string? contentType,
        ObjectStorageAccess access, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    { Require(); return inner.UploadImmutableAsync(stream, objectName, contentType, access, metadata, cancellationToken); }
    public Task<byte[]> DownloadAsync(string containerName, string objectName, CancellationToken cancellationToken = default)
    { Require(); return inner.DownloadAsync(containerName, objectName, cancellationToken); }
    // 동의 철회·파기 경로는 운영 전송 활성화 여부와 무관하게 이용할 수 있습니다.
    public Task<ObjectStorageDeleteResult> DeleteAsync(string containerName, string objectName, string expectedOwnedPrefix,
        CancellationToken cancellationToken = default) => inner.DeleteAsync(containerName, objectName, expectedOwnedPrefix, cancellationToken);
}
