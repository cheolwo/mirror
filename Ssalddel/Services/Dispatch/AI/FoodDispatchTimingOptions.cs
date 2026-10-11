using Ssalddel.Contracts.Common.Dispatch;

namespace 살뜰.Services.Dispatch.AI;

/// <summary>음식 주문을 짧게 모을지 판단하는 읽기 전용 정책입니다. 운영 활성화는 별도입니다.</summary>
public sealed class FoodDispatchTimingOptions
{
    public const string SectionName = "FoodDispatchTiming";
    public bool Enabled { get; set; }
    public FoodDispatchTimingSettings Policy { get; set; } = new();
    public int MaximumQueueRows { get; set; } = 500;
    public int ObservationSeconds { get; set; } = 300;

    public bool Validate() => Policy is not null && MaximumQueueRows is > 0 and <= 5000
        && ObservationSeconds is >= 30 and <= 3600 && Policy.CollectionSeconds >= 0
        && Policy.MaxWaitSeconds is > 0 and <= 120 && Policy.CollectionSeconds <= Policy.MaxWaitSeconds
        && Policy.MinimumCompatibleOrders > 0 && Policy.DeadlineBufferSeconds >= 0
        && Policy.DeadlineBufferSeconds <= Policy.MaxWaitSeconds && Policy.MaxSnapshotAgeSeconds is > 0 and <= 120;
}

/// <summary>서버가 허용한 수집/즉시 처리 선택만 로컬 모델에 묻습니다. 기본값은 꺼짐입니다.</summary>
public sealed class FoodDispatchTimingOllamaOptions
{
    public const string SectionName = "FoodDispatchTimingOllama";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:12681/";
    public string Model { get; set; } = FoodDispatchOllamaOptions.AllowedModel;
    public int TimeoutSeconds { get; set; } = 2;

    public bool Validate() => new FoodDispatchOllamaOptions
    {
        BaseUrl = BaseUrl, Model = Model, TimeoutSeconds = TimeoutSeconds, MaxCandidates = 2
    }.Validate();
}
