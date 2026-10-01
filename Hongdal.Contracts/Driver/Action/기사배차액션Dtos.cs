namespace Hongdal.Contracts.Driver.Action;

public sealed class 기사배차거절요청
{
    public string? 사유 { get; set; }
}

public sealed class 기사배차수락취소요청
{
    public string? 사유 { get; set; }
}

public sealed class 기사배차처리응답
{
    public string RequestId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class 기사배차액션응답
{
    public string RequestId { get; set; } = string.Empty;
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public 기사배차후속처리계획응답 FollowUpPlan { get; set; } = new();
}

public sealed class 기사배차후속처리계획응답
{
    public string RequestId { get; set; } = string.Empty;
    public string DecisionCode { get; set; } = string.Empty;
    public IReadOnlyList<string> ServerActionCodes { get; set; } = [];
    public bool ShouldReopenDispatch { get; set; }
    public bool ShouldNotifyShipper { get; set; }
    public bool ShouldRecalculateRecommendations { get; set; }
    public bool RequiresReason { get; set; }
    public string OperationalMemo { get; set; } = string.Empty;
}
