using System.Text.Json;
using Ssalddel.Community.Collaboration;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

/// <summary>
/// 내부 연결 조회가 배송·보호지원 쓰기 UseCase를 생성하지 않도록 보호된 저장소에서 직접 읽습니다.
/// 기존 연결 조회의 조건 동의 판정과 만료 CAS·예약 보호를 유지하며 공개 API 권한을 새로 부여하지 않습니다.
/// </summary>
public sealed class 생활협업연결Query(I생활협업Store store, TimeProvider clock,
    I생활협업보관상태Source? storage = null) : I생활협업연결Query
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<생활협업연결Context?> 조회Async(string stableId, CancellationToken cancellationToken = default)
    {
        var item = await LoadCurrentAsync(stableId, cancellationToken);
        return item is null ? null : new(item.StableId, item.Revision, item.TermsRevision, item.OwnerUserId,
            item.RequesterUserId, item.Kind, item.StatusCode,
            생활협업UseCase.CurrentAgreement(item) && !NeighborhoodCollaborationStates.IsTerminal(item.StatusCode),
            JsonSerializer.Deserialize<NeighborhoodCollaborationTerms>(JsonSerializer.Serialize(item.Terms, Json), Json)!,
            item.LinkedDeliveryRequestId, item.SourcePostId, item.SpaceRevision);
    }

    // 조회 시 이미 수행하던 만료 전이만 보존합니다. 보관·배송·보호지원 명령이나 후속 업무는 실행하지 않습니다.
    private async Task<생활협업Record?> LoadCurrentAsync(string stableId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stableId) || stableId.Length > 100) return null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var record = await store.조회Async(stableId, cancellationToken);
            if (record is null || !생활협업Policy.시작전(record.StatusCode)
                || record.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime) return record;
            if (ReservationLocked(record)) return record;
            // 물품을 둔 합의의 인계 권한은 기존 예약의 취소·반환보다 먼저 닫지 않습니다.
            if (record.Kind == NeighborhoodCollaborationKinds.Storage)
            {
                if (storage is null) throw new InvalidOperationException("Storage reservation authority is required for expiry.");
                var reservation = await storage.예약조회Async(record.StableId, cancellationToken);
                if (reservation?.TermsRevision == record.TermsRevision
                    && reservation.StatusCode is "reserved" or "in-custody" or "returned") return record;
            }
            var revision = record.Revision;
            var now = clock.GetUtcNow().UtcDateTime;
            record.StatusCode = NeighborhoodCollaborationStates.Expired;
            record.Revision++;
            record.UpdatedAtUtc = now;
            record.History.Add(new()
            {
                Revision = record.Revision, Action = "expire", StatusCode = record.StatusCode,
                ActorRoleCode = "system", RecordedAtUtc = now
            });
            if (await store.교체Async(record, revision, cancellationToken)) return record;
        }
        throw new InvalidOperationException("Collaboration expiry could not be stored due to concurrent changes.");
    }

    private static bool ReservationLocked(생활협업Record item) => item.DeliveryIntent?.StateCode == "pending"
        || item.ReceiptConfirmationIntent?.StateCode == "pending"
        || item.StorageReservationIntents.Any(x => x.StateCode is "pending" or "confirmed");
}
