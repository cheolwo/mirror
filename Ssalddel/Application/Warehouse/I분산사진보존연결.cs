using Ssalddel.Services.PrivacySupport;

namespace Ssalddel.Application.Warehouse;

public interface I분산사진보존연결 : I보호지원보존연결
{
    Task AcquireOpenCaseAsync(string caseId, string sourceKind, string sourceId, CancellationToken ct = default);
}
