using System.Globalization;

namespace Ssalddel.WebApp.Services;

public sealed record FoodDeliveryReplayMilestone(TimeSpan Elapsed, string Role, string Label);

public sealed record FoodDeliveryReplayFrame(string OrderNumber, TimeSpan Elapsed, int VisibleMilestoneCount);

public static class FoodDeliveryReplay
{
    public const int DurationSeconds = 60;
    public const int ReceiptAtSeconds = 50;

    // docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-phone-result.r2.md
    // 완료 원본 SHA-256 8E3DF0E292BB18E2A7CC2EC5D4426F45847CEFFAC658D067A22EB68AE6DFB37A
    // 주문 등록 기준으로 원본 수령 확인까지의 상대 시간만 사용한다.
    private const long RecordedReceiptTicks = 789_036_100;

    public static IReadOnlyList<FoodDeliveryReplayMilestone> Milestones { get; } =
        Array.AsReadOnly<FoodDeliveryReplayMilestone>(
        [
            new(ReplayTime(0), "주문자", "주문 등록"),
            new(ReplayTime(6_455_365), "음식점", "수락·조리 시작"),
            new(ReplayTime(29_654_551), "기사", "제안 수락"),
            new(ReplayTime(616_334_741), "음식점", "픽업 준비"),
            new(ReplayTime(617_954_167), "기사", "음식 픽업"),
            new(ReplayTime(787_817_209), "기사", "고객 전달"),
            new(ReplayTime(RecordedReceiptTicks), "주문자", "수령 확인")
        ]);

    public static FoodDeliveryReplayFrame GetFrame(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        var elapsed = TimeSpan.FromTicks(utc.Ticks % (DurationSeconds * TimeSpan.TicksPerSecond));
        var orderNumber = "DEMO-" + utc.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var visibleMilestoneCount = 0;
        foreach (var milestone in Milestones)
        {
            if (milestone.Elapsed > elapsed)
            {
                break;
            }

            visibleMilestoneCount++;
        }

        return new(orderNumber, elapsed, visibleMilestoneCount);
    }

    private static TimeSpan ReplayTime(long recordedTicks)
        => TimeSpan.FromTicks(recordedTicks * ReceiptAtSeconds * TimeSpan.TicksPerSecond / RecordedReceiptTicks);
}
