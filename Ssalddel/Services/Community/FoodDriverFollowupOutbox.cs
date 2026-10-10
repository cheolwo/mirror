using Ssalddel.Contracts.Common.Metadata;

using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Services.Community;

/// <summary>현재 SQL 업무 전이와 함께 예약하고 commit 뒤 재처리하는 음식 기사 후속 의도입니다.</summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "음식 기사 상태 변경의 후속 원천 식별자와 사건 정보를 정의한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 17,
    StepKey = "contract.driver-followup-intent",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "고객 주소·연락처 사본을 담지 않는다. 원장 동기화·알림 예약은 실제 발송이나 수령·실지급 완료가 아니다.")]
public sealed record 음식기사후속의도(
    string 주문번호,
    long 운송Id,
    string 배차의뢰Id,
    string 처리기사Id,
    string 상태,
    DateTime 발생시각Utc,
    string? 음식점알림사유,
    bool 원장동기화 = true,
    bool 활동발행 = true)
{
    public string EventId => $"food-delivery:{배차의뢰Id}:{상태}:{발생시각Utc.Ticks}";
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "상태 변경과 함께 예약하고 저장 후 처리하는 음식 기사 후속 계약을 정의한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 18,
    StepKey = "contract.driver-followup-port",
    DependsOnStepKeys = ["contract.driver-followup-intent"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "예약은 호출자의 SaveChanges와 transaction으로 확정하며 즉시처리는 별도다. 인터페이스 선언 자체는 저장·발송을 실행하지 않는다.")]
public interface I음식기사후속OutboxService
{
    /// <summary>추적 항목만 추가합니다. 호출자의 업무 SaveChanges/transaction과 함께 확정합니다.</summary>
    Task 예약Async(음식기사후속의도 intent, CancellationToken cancellationToken = default);

    /// <summary>commit 이후 같은 멱등 키를 처리합니다. 실패는 기존 Outbox 재시도로 남습니다.</summary>
    Task 즉시처리Async(음식기사후속의도 intent, CancellationToken cancellationToken = default);
}

internal sealed record 음식기사후속Payload(string Kind, 음식기사후속의도 Intent);

internal static class 음식기사후속Kinds
{
    public const string FoodLedger = "FoodLedger";
    public const string TransportLedger = "TransportLedger";
    public const string RestaurantNotification = "RestaurantNotification";
    public const string CommunityActivity = "CommunityActivity";

    public static IEnumerable<string> For(음식기사후속의도 intent)
    {
        if (intent.원장동기화) { yield return FoodLedger; yield return TransportLedger; }
        if (intent.음식점알림사유 is not null) yield return RestaurantNotification;
        if (intent.활동발행) yield return CommunityActivity;
    }

    public static string Key(음식기사후속의도 intent, string kind)
        => "food-driver-followup:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{intent.EventId}|{kind}")));
}
