using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Notification;

/// <summary>Same recommendation recovery only; never creates acceptance, a new round, or a new fee.</summary>
public static class FoodRecommendationNotificationRecoveryPolicy
{
    public const int MaxAttempts = 5;
    public const int LeaseSeconds = 60;
    public const int SendTimeoutSeconds = 10;
    public static TimeSpan RetryDelay(int attempts) => TimeSpan.FromSeconds(attempts switch
        { <= 1 => 5, 2 => 10, 3 => 20, _ => 30 });
    public static bool IsCurrentRecommendation(운송원장 queue, 배차추천알림Outbox item, DateTime now)
        => queue.Id == item.배차대기Id && queue.의뢰Id == item.의뢰Id
            && queue.배차업무유형 == 상태값.배차업무유형.음식배달
            && queue.상태 == 상태값.배차대기상태.대기
            && queue.배차큐단계 == 상태값.배차큐단계.배차추천
            && queue.배차노출상태 == 상태값.배차노출상태.추천중
            && queue.현재추천대상기사Id == item.기사Id && queue.추천라운드 == item.추천라운드
            && queue.추천만료시각.HasValue && queue.추천만료시각 > now;
    public static bool RequiresOperatorReview(배차추천알림Outbox item, DateTime now)
        => item.시도횟수 >= MaxAttempts && (item.발송상태 == "Failed"
            || (item.발송상태 == "Sending" && (item.마지막시도시각 ?? item.UpdatedAt) <= now.AddSeconds(-LeaseSeconds)));
}
