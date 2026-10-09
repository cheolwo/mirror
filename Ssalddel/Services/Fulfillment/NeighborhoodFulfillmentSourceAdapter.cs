using System.Data;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Services.Fulfillment;

/// <summary>영속 사전동의와 현재 내부 수행을 확인한 서버 업무 경로만 자체 큐를 생성합니다.</summary>
public sealed class 생활배송수행원천Adapter(SsalddelContext db, I운송의뢰배차대기Service queueService,
    I운송원장Mongo동기화Service ledgerSync, TimeProvider clock, ISsalddelExecutionModePolicy? execution = null) : I수행원천Adapter
{
    public string SourceKind => FulfillmentSourceKinds.NeighborhoodDelivery;
    public async Task<FulfillmentSourceSnapshot?> 읽기Async(FulfillmentSourceKey key, CancellationToken ct)
    {
        var s = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == key.SourceId
            && x.자동처리처수행 && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), ct);
        if (s is null) return null;
        var q = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == key.SourceId, ct);
        var fare = await db.운임구성.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == key.SourceId, ct);
        return new(key, s.주문자UserId ?? "", s.UpdatedAt.Ticks, 입력지문(s),
            s.상태 == 상태값.의뢰상태.취소 || s.배차상태 == 상태값.배차상태.인수완료, s.자동처리처수행,
            false, q?.확정기사Id is null && s.배차상태 is 상태값.배차상태.미시작 or 상태값.배차상태.매칭중,
            fare?.예상거리Km, s.최종운임, DateTime.SpecifyKind(s.픽업_시간창_시작일시, DateTimeKind.Utc),
            WeightKg: s.화물중량Kg, Quantity: s.화물수량 ?? 1, VehicleType: s.차량종류,
            TravelMinutes: fare?.예상거리Km is { } distance ? decimal.Ceiling(distance * 3) : null,
            PackageSizeCode: s.생활배송포장크기Code);
    }
    internal static string 입력지문(화주운송의뢰 source)
    {
        var original = 자동수행Coordinator.Digest(new { source.화물종류, source.화물수량,
            source.화물중량Kg, source.차량종류, source.픽업_도로명주소, source.픽업_상세주소, source.하차_도로명주소, source.하차_상세주소,
            source.픽업_연락처_전화번호, source.하차_연락처_전화번호, source.픽업_시간창_시작일시, source.픽업_시간창_종료일시 });
        return source.생활배송포장크기Code is null ? original
            : 자동수행Coordinator.Digest(new { InputDigest = original, PackageSizeCode = source.생활배송포장크기Code });
    }
    public async Task<FulfillmentExecutionResult> 내부접수Async(FulfillmentAttemptContext attempt, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var c = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == attempt.Key.SourceId, ct);
        var a = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.AttemptId == attempt.AttemptId, ct);
        var consent = await db.AutoFulfillmentConsents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == attempt.Key.SourceId, ct);
        var s = await db.화주운송의뢰.SingleAsync(x => x.의뢰Id == attempt.Key.SourceId, ct);
        var quote = a is null ? null : await db.NeighborhoodFulfillmentQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.QuoteId == a.QuoteId, ct);
        var input = await 읽기Async(attempt.Key, ct);
        if (c?.AttemptId != attempt.AttemptId || c.Revision != attempt.Revision || a?.ProviderId != "internal"
            || a.StateCode != AutoFulfillmentStates.Submitting || consent is null || consent.Revoked || !consent.DirectPaymentAgreed
            || consent.Revision != attempt.ConsentRevision || input?.InputDigest != consent.InputDigest || input.SourceRevision != a.SourceRevision
            || quote is null || quote.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime
            || s.상태 == 상태값.의뢰상태.취소 || !s.자동처리처수행) return new(AutoFulfillmentStates.Failed, ConfirmedIncurredCostKrw: 0);
        if (await db.비정상운송사건.AsNoTracking().AnyAsync(x => x.운송의뢰Id == s.의뢰Id && x.정산보류적용여부, ct))
            return new(AutoFulfillmentStates.Failed, ConfirmedIncurredCostKrw: 0);
        s.정산상태 = "모의 수행·정산 제외"; s.배차상태 = 상태값.배차상태.매칭중; s.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        var queue = await queueService.생성또는조회Async(화주운송의뢰출고예정정규화.To출고예정운송대상(s),
            new 운송의뢰배차대기생성옵션 { 픽업상세주소 = s.픽업_상세주소, 하차상세주소 = s.하차_상세주소 }, ct);
        if (queue.확정기사Id is null)
        {
            queue.생활배송수락준비완료 = true; queue.생활배송선택판본++;
            queue.배차큐단계 = 상태값.배차큐단계.계획배차; 생활배송배차Policy.대기적용(queue);
        }
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        await ledgerSync.화주운송의뢰동기화Async(s, consent.OwnerId, ct);
        return await 내부상태Async(attempt, ct);
    }
    private async Task<FulfillmentExecutionResult> 내부상태Async(FulfillmentAttemptContext attempt, CancellationToken ct)
    {
        var s = await db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == attempt.Key.SourceId, ct);
        var q = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == attempt.Key.SourceId, ct);
        if (q is null) return new(AutoFulfillmentStates.ResultUnknown);
        if (q.배차큐단계 == 상태값.배차큐단계.종료 && q.확정기사Id is null) return new(AutoFulfillmentStates.Cancelled, ConfirmedIncurredCostKrw: 0);
        return new(s.배차상태 is 상태값.배차상태.인수완료 or 상태값.배차상태.하차완료 ? AutoFulfillmentStates.Completed
            : s.배차상태 is 상태값.배차상태.상차완료 or 상태값.배차상태.운송중 ? AutoFulfillmentStates.InTransit
            : q.확정기사Id is not null ? AutoFulfillmentStates.DriverAssigned : AutoFulfillmentStates.Accepted);
    }
    public Task<FulfillmentExecutionResult> 내부조회Async(FulfillmentAttemptContext attempt, CancellationToken ct) => 내부상태Async(attempt, ct);
    public async Task<FulfillmentExecutionResult> 내부취소Async(FulfillmentAttemptContext attempt, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == attempt.Key.SourceId, ct);
        if (current?.AttemptId != attempt.AttemptId || current.Revision != attempt.Revision) return new(AutoFulfillmentStates.CancelPending);
        var q = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == attempt.Key.SourceId, ct);
        var s = await db.화주운송의뢰.SingleAsync(x => x.의뢰Id == attempt.Key.SourceId, ct);
        if (q?.확정기사Id is not null || s.배차상태 is not (상태값.배차상태.미시작 or 상태값.배차상태.매칭중))
            return new(AutoFulfillmentStates.DriverAssigned);
        if (q is not null)
        {
            q.생활배송수락준비완료 = false; q.생활배송선택판본++; q.추천라운드++;
            q.배차큐단계 = 상태값.배차큐단계.종료; q.현재추천대상기사Id = null; q.추천만료시각 = null;
        }
        s.배차상태 = 상태값.배차상태.미시작; await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        return new(AutoFulfillmentStates.Cancelled, ConfirmedIncurredCostKrw: 0);
    }
    public async Task<Result> 외부증거반영Async(FulfillmentExecutionEvidence evidence, CancellationToken ct)
    {
        // 외부 모의 증거는 현재 영속 시도에만 결속합니다. 내부 기사·큐·배차 상태를 대신 만들지 않습니다.
        var attempt = evidence.Attempt;
        if (execution?.IsSimulation != true || attempt.Key.SourceKind != SourceKind || attempt.ProviderId == "internal"
            || string.IsNullOrWhiteSpace(evidence.EvidenceId)) return 증거거절();
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == attempt.Key.SourceId, ct);
        var stored = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.AttemptId == attempt.AttemptId, ct);
        var job = await db.FulfillmentMockJobs.AsNoTracking().SingleOrDefaultAsync(x => x.AttemptId == attempt.AttemptId, ct);
        var consent = await db.AutoFulfillmentConsents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == SourceKind && x.RequestId == attempt.Key.SourceId, ct);
        var input = await 읽기Async(attempt.Key, ct);
        if (input is null || current is null || stored is null || job is null || consent is null
            || !current.IsAutomatic || current.AttemptId != attempt.AttemptId || current.Revision != attempt.Revision
            || stored.SourceKind != SourceKind || stored.RequestId != attempt.Key.SourceId || !stored.IsSimulation
            || stored.ProviderId != attempt.ProviderId || stored.ExternalRequestId != attempt.ExternalRequestId
            || job.SourceKind != SourceKind || job.RequestId != stored.RequestId || job.ProviderId != stored.ProviderId
            || string.IsNullOrWhiteSpace(job.ExternalRequestId) || job.ExternalRequestId != stored.ExternalRequestId
            || current.ActorId != input.OwnerId || consent.OwnerId != input.OwnerId
            || stored.Leg != attempt.Leg || job.Leg != stored.Leg || stored.ConsentRevision != attempt.ConsentRevision
            || !stored.SharingAgreed) return 증거거절();
        // 동의 철회·원천 변경 뒤에도 원 접수 취소는 허용하되, 진행·완료를 새로 확정하지 않습니다.
        if (evidence.EvidenceKind != FulfillmentEvidenceKinds.Cancelled
            && (input.IsClosed || consent.Revoked || !consent.SharingAgreed || !consent.AutomaticSubmissionAgreed
                || consent.Revision != stored.ConsentRevision || input.SourceRevision != stored.SourceRevision
                || input.InputDigest != consent.InputDigest)) return 증거거절();
        return Result.Ok();
    }
    private static Result 증거거절() => Result.Fail(new Error("원천·동의·현재 시도와 모의 증거가 일치하지 않습니다.")
        .WithMetadata("ErrorCode", "MockEvidenceSourceChanged").WithMetadata("StatusCode", 409));
}
