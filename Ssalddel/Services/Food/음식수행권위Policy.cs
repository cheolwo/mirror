using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Food;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Food;

/// <summary>기존 음식 실행과 모의 외부 수행을 합치지 않는 현재 원천/시도 결속입니다.</summary>
public static class 음식수행권위Policy
{
    public static string Leg(음식주문 order) => $"preparation:{음식주문현재조리Policy.계산(order).Round}";
    public static string AssignmentMarker(string attemptId, string leg, long assignmentRevision)
        => $"모의 외부 기사 배정 · {attemptId} · {leg} · {assignmentRevision}";

    public static bool 내부허용(SsalddelContext db, 음식주문 order)
    {
        if (!order.모의처리처수행) return true;
        var current = db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefault(x => x.SourceKind == FulfillmentSourceKinds.FoodOrder && x.RequestId == order.주문번호);
        if (current is not { IsAutomatic: true } || string.IsNullOrWhiteSpace(current.AttemptId)
            || current.StateCode is not (AutoFulfillmentStates.Submitting or AutoFulfillmentStates.Accepted
                or AutoFulfillmentStates.DriverAssigned or AutoFulfillmentStates.InTransit)) return false;
        var attempt = db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleOrDefault(x => x.AttemptId == current.AttemptId);
        return attempt is not null && attempt.SourceKind == FulfillmentSourceKinds.FoodOrder && attempt.RequestId == order.주문번호
            && attempt.ProviderId == "internal" && attempt.Leg == Leg(order)
            && attempt.StateCode is AutoFulfillmentStates.Submitting or AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned or AutoFulfillmentStates.InTransit;
    }

    public static bool 외부배정유효(SsalddelContext db, 음식주문 order)
    {
        if (!order.모의처리처수행 || order.배차대기Id.HasValue || order.배차상태 != 음식주문배차상태코드.기사배정) return false;
        var current = db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefault(x => x.SourceKind == FulfillmentSourceKinds.FoodOrder && x.RequestId == order.주문번호);
        if (current is not { IsAutomatic: true } || string.IsNullOrWhiteSpace(current.AttemptId)) return false;
        var attempt = db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleOrDefault(x => x.AttemptId == current.AttemptId);
        if (attempt is null || attempt.SourceKind != FulfillmentSourceKinds.FoodOrder || attempt.RequestId != order.주문번호
            || attempt.ProviderId == "internal" || attempt.Leg != Leg(order) || attempt.StateCode != AutoFulfillmentStates.DriverAssigned) return false;
        var job = db.FulfillmentMockJobs.AsNoTracking().SingleOrDefault(x => x.AttemptId == attempt.AttemptId);
        if (job is null || job.SourceKind != FulfillmentSourceKinds.FoodOrder || job.RequestId != order.주문번호 || job.Leg != attempt.Leg
            || job.ProviderId != attempt.ProviderId || job.StateCode != AutoFulfillmentStates.DriverAssigned || job.AssignmentRevision <= 0
            || string.IsNullOrWhiteSpace(job.ExternalDriverReference)) return false;
        return order.상태이력.Any(x => x.사유 == AssignmentMarker(attempt.AttemptId, attempt.Leg, job.AssignmentRevision));
    }
}
