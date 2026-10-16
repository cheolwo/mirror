using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.DispatchTraining;

/// <summary>제품 시계를 가속하지 않는 고정 실시간 훈련 일정입니다. 인덱스는 0부터 시작합니다.</summary>
public static class DispatchTrainingScenario
{
    public const int DurationSeconds = 30 * 60;
    public const int OrderIntervalSeconds = 2 * 60;
    public const int MaximumOrders = 15;
    public const int ActiveDrivers = 6;
    public const int SupportDrivers = 2;

    public static DateTime DueAt(DateTime startedAtUtc, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, MaximumOrders);
        return startedAtUtc.AddSeconds(index * OrderIntervalSeconds);
    }

    public static Guid OrderRequestId(string runId, int index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, MaximumOrders);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"dispatch-training|{runId}|order|{index}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    public static IReadOnlyList<DispatchTrainingScenarioEpisode> Episodes { get; } =
    [
        new(2, "RejectOnce", "배차 제안 거절 뒤 기존 규칙으로 다시 추천"),
        new(5, "OfferExpiresOnce", "배차 제안 만료 뒤 기존 규칙으로 다시 추천"),
        new(9, "ReceivingOffShortage", "신규 수신 중단에 따른 기사 부족; 지원 참여는 직접 선택")
    ];
}

public sealed record DispatchTrainingScenarioEpisode(int OrderIndex, string KindCode, string Label);
