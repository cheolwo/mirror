using 홍달.도메인.배차;

namespace 홍달.Services.Dispatch.Queue;

public sealed partial class 배차대기원장전환Service
{
    private void 배차판단감사추가(
        운송원장 queue,
        배차추천후보선정결과? selection,
        string followUpTransition,
        string transitionResultCode,
        DateTime occurredAtUtc)
    {
        if (selection?.감사Context is null)
        {
            return;
        }

        _db.운송이벤트.Add(배차엔진판단감사이벤트Factory.생성(
            queue,
            selection,
            followUpTransition,
            transitionResultCode,
            occurredAtUtc));
    }

    private async Task<배차대기원장전환결과> 감사기록후반환Async(
        운송원장 queue,
        배차추천후보선정결과 selection,
        string followUpTransition,
        배차대기원장전환결과 result,
        CancellationToken cancellationToken)
    {
        if (selection.감사Context is null)
        {
            return result;
        }

        배차판단감사추가(
            queue,
            selection,
            followUpTransition,
            result.결과코드,
            DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static void 공개배차상태적용(운송원장 queue, DateTime changedAtUtc)
    {
        queue.배차큐단계 = 상태값.배차큐단계.공개배차;
        queue.배차노출상태 = 상태값.배차노출상태.공개중;
        queue.공개전환시각 = changedAtUtc;
        queue.현재추천대상기사Id = null;
        queue.추천시작시각 = null;
        queue.추천만료시각 = null;
        queue.UpdatedAt = changedAtUtc;
    }
}
