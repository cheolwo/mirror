using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Hosting;

public sealed record SimulationWorldTileArtifactFile(
    string FullPath,
    long ByteLength,
    string ContentType);

/// <summary>
/// 파생 DB가 가리킨 로컬 개발 산출물을 경로 이탈과 SHA-256 변조 없이 제공한다.
/// 운영 객체 저장소 전송 경계는 별도 구현으로 교체한다.
/// </summary>
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "구성 요소의 공통 Core·Server 또는 Adapter 실행 경계를 제공한다.",
    Boundary = "운영 상태와 Simulation 상태의 권위 경계를 유지한다.")]
public sealed class SimulationWorldTileArtifactContentService(
    IOptions<SimulationWorldDerivationDatabaseOptions> options,
    IWebHostEnvironment environment)
{
    public const string RootMissing = "SimulationWorldArtifactRootMissing";
    public const string InvalidPath = "SimulationWorldArtifactPathInvalid";
    public const string FileMissing = "SimulationWorldArtifactFileMissing";
    public const string IntegrityMismatch = "SimulationWorldArtifactIntegrityMismatch";

    public bool TryResolve(
        SimulationWorldTileArtifactDescriptorResponse descriptor,
        out SimulationWorldTileArtifactFile value,
        out string errorCode)
    {
        value = new SimulationWorldTileArtifactFile(string.Empty, 0, "application/octet-stream");
        errorCode = string.Empty;
        var configuredRoot = options.Value.ArtifactRootPath;
        if (string.IsNullOrWhiteSpace(configuredRoot))
            return Fail(RootMissing, out errorCode);
        if (descriptor.StatusCode != SimulationWorldStreamCodes.Available
            || string.IsNullOrWhiteSpace(descriptor.ArtifactRelativePath)
            || string.IsNullOrWhiteSpace(descriptor.ArtifactHashSha256)
            || descriptor.ArtifactByteLength == null)
            return Fail(FileMissing, out errorCode);

        var root = Path.GetFullPath(configuredRoot, environment.ContentRootPath);
        var relative = descriptor.ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return Fail(InvalidPath, out errorCode);
        if (!File.Exists(fullPath))
            return Fail(FileMissing, out errorCode);

        var info = new FileInfo(fullPath);
        if (info.Length != descriptor.ArtifactByteLength.Value)
            return Fail(IntegrityMismatch, out errorCode);
        using var stream = File.OpenRead(fullPath);
        var actualHash = Convert.ToHexString(SHA256.HashData(stream));
        if (!actualHash.Equals(descriptor.ArtifactHashSha256, StringComparison.OrdinalIgnoreCase))
            return Fail(IntegrityMismatch, out errorCode);

        value = new SimulationWorldTileArtifactFile(fullPath, info.Length, "application/octet-stream");
        return true;
    }

    private static bool Fail(string value, out string errorCode)
    {
        errorCode = value;
        return false;
    }
}
