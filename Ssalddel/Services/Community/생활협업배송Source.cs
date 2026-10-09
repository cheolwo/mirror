using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Services.Fulfillment;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.Community;

/// <summary>개인 주소를 복사하지 않고 기존 생활 화물의 본인·출처·실제 수행 상태만 읽습니다.</summary>
public sealed class Ef생활협업배송Source(SsalddelContext db, ISsalddelExecutionModePolicy? execution = null) : I생활협업배송Source
{
    public async Task<생활협업배송Snapshot?> 조회Async(string requestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return null;
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        if (request is null) return null;
        var cancelled = request.상태 == 상태값.의뢰상태.취소 || request.배차상태 == 상태값.배차상태.취소;
        var neighborhood = request.클라이언트요청Id?.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal) == true;
        var completed = !cancelled && request.배차상태 == 상태값.배차상태.인수완료
            && (!(request.모의처리처수행 || request.자동처리처수행) || execution?.IsSimulation == true);
        if (!completed && !cancelled && neighborhood && request.자동처리처수행 && request.모의처리처수행 && execution?.IsSimulation == true)
        {
            // 같은 원천·동의·현재 시도·모의 작업이 한 조회에서 완료로 일치해야 합니다.
            // 내부 운송 상태를 위조하거나 협업 양측 완료·지급을 자동 확정하지 않습니다.
            var proof = await (from current in db.NeighborhoodFulfillments.AsNoTracking()
                join attempt in db.NeighborhoodFulfillmentAttempts.AsNoTracking() on current.AttemptId equals attempt.AttemptId
                join job in db.FulfillmentMockJobs.AsNoTracking() on attempt.AttemptId equals job.AttemptId
                join consent in db.AutoFulfillmentConsents.AsNoTracking() on new { current.SourceKind, current.RequestId } equals new { consent.SourceKind, consent.RequestId }
                join source in db.화주운송의뢰.AsNoTracking() on current.RequestId equals source.의뢰Id
                where current.SourceKind == FulfillmentSourceKinds.NeighborhoodDelivery && current.RequestId == requestId
                    && current.IsAutomatic && current.ActorId == request.주문자UserId && current.StateCode == AutoFulfillmentStates.Completed
                    && attempt.SourceKind == current.SourceKind && attempt.RequestId == requestId && attempt.IsSimulation && attempt.SharingAgreed
                    && attempt.ProviderId != "internal" && attempt.StateCode == AutoFulfillmentStates.Completed && attempt.SourceRevision == request.UpdatedAt.Ticks
                    && attempt.Leg == "initial" && attempt.ConsentRevision == consent.Revision
                    && job.SourceKind == current.SourceKind && job.RequestId == requestId && job.ProviderId == attempt.ProviderId && job.Leg == attempt.Leg
                    && job.ExternalRequestId != "" && job.ExternalRequestId == attempt.ExternalRequestId
                    && job.StateCode == AutoFulfillmentStates.Completed && job.LastClientRequestId != null && job.ExternalDriverReference != null
                    && consent.OwnerId == current.ActorId && !consent.Revoked && consent.SharingAgreed && consent.AutomaticSubmissionAgreed
                    && source.UpdatedAt == request.UpdatedAt && source.상태 != 상태값.의뢰상태.취소 && source.배차상태 != 상태값.배차상태.취소
                select new { Source = source, consent.InputDigest }).SingleOrDefaultAsync(cancellationToken);
            completed = proof is not null && proof.InputDigest == 생활배송수행원천Adapter.입력지문(proof.Source);
        }
        return new(request.의뢰Id, request.주문자UserId ?? string.Empty,
            생활배송출처Evidence.Read(request.정산메모),
            neighborhood, completed, cancelled);
    }
}

/// <summary>신규 신청의 출처만 보존하며 과거 지문에서 게시글 번호를 역추정하지 않습니다.</summary>
public static class 생활배송출처Evidence
{
    public const string Marker = ":source-post:";
    public static long? Read(string? memo)
    {
        if (memo is null || !memo.StartsWith("neighborhood-delivery:v1:", StringComparison.Ordinal)) return null;
        var markerAt = memo.IndexOf(Marker, StringComparison.Ordinal);
        if (markerAt < 0 || markerAt != memo.LastIndexOf(Marker, StringComparison.Ordinal)) return null;
        var suffix = memo.AsSpan(markerAt + Marker.Length);
        var policyAt = suffix.IndexOf(생활배송배차Policy.Marker, StringComparison.Ordinal);
        if (policyAt >= 0) suffix = suffix[..policyAt];
        return long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }
}
