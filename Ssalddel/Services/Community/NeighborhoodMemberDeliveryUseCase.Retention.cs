using System.Data;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacySupport;
using Ssalddel.Services.Storage;

namespace Ssalddel.Services.Community;

public sealed partial class NeighborhoodMemberDeliveryUseCase
{
    /// <summary>열람 제한과 별도로 실제 비공개 객체를 파기한다. 지원 상태 미확인·진행 업무·과거 복사 미확인은 완료로 기록하지 않는다.</summary>
    public async Task<int> SweepPhotosAsync(I보호지원Store cases, CancellationToken ct = default)
    {
        if (!Enabled || !storage.IsConfigured(ObjectStorageAccess.Private)) return 0;
        var threshold = Now.AddHours(-24);
        var ids = await Photos.AsNoTracking().Where(x => x.CreatedAtUtc < threshold && x.StateCode != "Deleted")
            .OrderBy(x => x.CreatedAtUtc).Take(50).Select(x => x.EvidenceId).ToListAsync(ct);
        var removed = 0;
        foreach (var id in ids)
        {
            // 잠금을 풀어 업로드/파기가 교차하게 하지 않는다. 같은 호출에서 SQL만
            // 재시도해도 이미 보낸 물리 삭제는 다시 전송하지 않는다.
            Task<ObjectStorageDeleteResult>? deletionTask = null;
            async Task<ObjectStorageDeleteResult> DeleteOnceAsync(PhotoPointer pointer, string prefix)
                => await storage.DeleteAsync(pointer.Container, pointer.ObjectName, prefix, ct);
            removed += await ExecuteDatabaseMutationAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                // 같은 SQL 행을 잠가 불명확한 upload 재시도와 파기가 교차하지 않도록 한다.
                if (await Photos.Where(x => x.EvidenceId == id && x.StateCode != "Deleted")
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.StateCode, x => x.StateCode), ct) != 1)
                    return deletionTask is { IsCompletedSuccessfully: true } && deletionTask.Result.HistoricalCopiesVerifiedAbsent ? 1 : 0;
                var photo = await Photos.SingleAsync(x => x.EvidenceId == id, ct);
                var work = await Deliveries.AsNoTracking().SingleAsync(x => x.RequestId == photo.RequestId, ct);
                var terminal = work.StateCode is "Delivered" or "Cancelled";
                var orphan = photo.StateCode is "Uploading" or "Uploaded" && photo.EvidenceId != work.PickupEvidenceId && photo.EvidenceId != work.DeliveryEvidenceId;
                if ((!terminal || work.UpdatedAtUtc > Now.AddDays(-30)) && !orphan) return 0;
                if (await db.Set<살뜰.도메인.창고.분산사진보존정지Record>().AsNoTracking().AnyAsync(x => x.SourceKind == 보호지원출처Codes.생활배송
                    && x.SourceId == photo.RequestId && x.Active, ct)) return 0;
                // null은 Mongo 지원 색인의 상태 미확인이다. 보존 여부를 추정하지 않는다.
                if (await cases.HasOpenCaseAsync(보호지원출처Codes.생활배송, photo.RequestId, ct) != false) return 0;
                var pointer = Open<PhotoPointer>(photo.PrivatePayload);
                var prefix = "neighborhood-member-proof/" + Stable("", photo.ActorUserId);
                var result = await (deletionTask ??= DeleteOnceAsync(pointer, prefix));
                if (!result.ActiveObjectAbsent) return 0;
                photo.StateCode = result.HistoricalCopiesVerifiedAbsent ? "Deleted" : "ActiveDeleted";
                if (result.HistoricalCopiesVerifiedAbsent) photo.PrivatePayload = "";
                await SaveAsync(ct); await tx.CommitAsync(ct);
                return result.HistoricalCopiesVerifiedAbsent ? 1 : 0;
            }, ct);
        }
        return removed;
    }
}
