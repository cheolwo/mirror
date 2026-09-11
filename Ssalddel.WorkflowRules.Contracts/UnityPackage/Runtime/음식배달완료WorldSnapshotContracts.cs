using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class 음식배달완료WorldSnapshot정책
    {
        public const string SchemaVersion = "food-delivery-completed-world-snapshot.v1";
        public const string OutcomeReceiptConfirmed = "ReceiptConfirmed";
        public const string OnlineEphemeral = "OnlineEphemeral";
        public const int RefreshAfterSeconds = 30;
    }

    /// <summary>
    /// 실제 주문·사용자·기사 식별자와 정확 위치를 포함하지 않는 표현 전용 역할 식별자입니다.
    /// 한 상태 사본의 수명이 끝나면 함께 폐기하며 실제 사람과의 역추적 계약을 제공하지 않습니다.
    /// </summary>
    public sealed class 음식배달완료WorldActorRefs
    {
        public string OrdererActorStableId { get; set; } = string.Empty;
        public string RestaurantActorStableId { get; set; } = string.Empty;
        public string DriverActorStableId { get; set; } = string.Empty;
    }

    public sealed class 음식배달완료WorldMilestone
    {
        public int Sequence { get; set; }
        public string StageCode { get; set; } = string.Empty;
        public long ElapsedSeconds { get; set; }
    }

    /// <summary>
    /// FoodDeliveryOS의 정상 완료 주기를 모두 검증한 뒤 발행되는 온라인 전용 상태 사본입니다.
    /// Unity는 이 사본을 저장·재생하거나 운영 상태를 변경하는 권위로 사용하지 않습니다.
    /// </summary>
    public sealed class 음식배달완료WorldSnapshot
    {
        public string SchemaVersion { get; set; } = 음식배달완료WorldSnapshot정책.SchemaVersion;
        public string SnapshotStableId { get; set; } = string.Empty;
        public string AreaStableId { get; set; } = string.Empty;
        public long LifecycleRevision { get; set; }
        public string OutcomeCode { get; set; } = 음식배달완료WorldSnapshot정책.OutcomeReceiptConfirmed;
        public DateTime CompletedAtUtc { get; set; }
        public DateTime PublishedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string DataPolicyCode { get; set; } = 음식배달완료WorldSnapshot정책.OnlineEphemeral;
        public bool LocalStorageAllowed { get; set; }
        public bool ReplayAllowed { get; set; }
        public 음식배달완료WorldActorRefs Actors { get; set; } = new 음식배달완료WorldActorRefs();
        public 음식배달완료WorldMilestone[] Milestones { get; set; } = Array.Empty<음식배달완료WorldMilestone>();
    }

    public sealed class 음식배달완료WorldSnapshot목록응답
    {
        public string SchemaVersion { get; set; } = 음식배달완료WorldSnapshot정책.SchemaVersion;
        public string AreaStableId { get; set; } = string.Empty;
        public DateTime AsOfUtc { get; set; }
        public int RefreshAfterSeconds { get; set; } = 음식배달완료WorldSnapshot정책.RefreshAfterSeconds;
        public 음식배달완료WorldSnapshot[] Items { get; set; } = Array.Empty<음식배달완료WorldSnapshot>();
    }
}
