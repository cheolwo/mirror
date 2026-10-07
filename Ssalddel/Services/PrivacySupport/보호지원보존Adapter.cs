using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacyRetention;

namespace Ssalddel.Services.PrivacySupport;

/// <summary>확인된 원장 단위의 개인정보 파기를 분쟁 재검토 동안 멈춥니다. 계정 전체 삭제 완료로 확대하지 않습니다.</summary>
public sealed class 보호지원보존Adapter(I개인정보보존조정Service retention) : I보호지원보존연결
{
    public Task 보존정지Async(string caseId, string sourceKind, string sourceId, string legalBasis, DateTime reviewAtUtc, CancellationToken cancellationToken = default)
        => retention.보존정지Async(Map(sourceKind), sourceId, caseId, "transaction-dispute-review", reviewAtUtc, cancellationToken);

    public Task 보존정지해제Async(string caseId, string sourceKind, string sourceId, CancellationToken cancellationToken = default)
        => retention.보존정지해제Async(Map(sourceKind), sourceId, caseId, cancellationToken);

    public async Task<DateTime?> 보존정지조회Async(string caseId, string sourceKind, string sourceId, CancellationToken cancellationToken = default)
        => (await retention.보존정지조회Async(Map(sourceKind), sourceId, caseId, cancellationToken))?.ReviewAtUtc;

    public Task<bool> 보존정지해제확인Async(string caseId, string sourceKind, string sourceId, CancellationToken cancellationToken = default)
        => retention.보존정지해제확인Async(Map(sourceKind), sourceId, caseId, cancellationToken);

    public Task 보존상태적용Async(string caseId, string sourceKind, string sourceId, long sequence, bool hold, string legalBasis,
        DateTime? reviewAtUtc, CancellationToken cancellationToken = default)
        => retention.보존상태적용Async(Map(sourceKind), sourceId, caseId, sequence, hold, "transaction-dispute-review", reviewAtUtc, cancellationToken);

    public async Task<보호지원보존적용?> 보존적용조회Async(string caseId, string sourceKind, string sourceId, CancellationToken cancellationToken = default)
    {
        var receipt = await retention.보존적용조회Async(Map(sourceKind), sourceId, caseId, cancellationToken);
        return receipt is null ? null : new(receipt.Sequence, receipt.Hold, receipt.ReviewAtUtc);
    }

    private static string Map(string sourceKind) => sourceKind switch
    {
        보호지원출처Codes.음식주문 => "FoodOrder",
        보호지원출처Codes.생활배송 => "NeighborhoodDelivery",
        _ => throw new 보호지원Exception("RetentionSourceUnsupported", "이 거래 유형의 실제 보존 작업은 아직 연결되지 않았습니다.", 409)
    };
}
