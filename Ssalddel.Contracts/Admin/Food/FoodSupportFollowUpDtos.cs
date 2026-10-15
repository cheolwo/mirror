namespace Ssalddel.Contracts.Admin.Food;

public static class FoodSupportFollowUpActions
{
    public const string RetryDispatch = "RetryDispatch";
    public const string OperatorReview = "OperatorReview";
}

/// <summary>사건 안내와 전문 업무 실행을 구분하며 금전 완료를 요청하지 않습니다.</summary>
public sealed class FoodSupportFollowUpRequest
{
    public Guid ClientRequestId { get; set; }
    public string SupportCaseId { get; set; } = string.Empty;
    public long ExpectedCaseRevision { get; set; }
    public long ExpectedOrderRevision { get; set; }
    public string ActionCode { get; set; } = string.Empty;
}

public sealed class FoodSupportFollowUpResponse
{
    public Guid ClientRequestId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string SupportCaseId { get; set; } = string.Empty;
    public long SupportCaseRevision { get; set; }
    public long OrderRevision { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string WorkStatusCode { get; set; } = string.Empty;
    public string FinanceStatusCode { get; set; } = "OperatorReviewRequired";
    public string Message { get; set; } = string.Empty;
    public bool Replay { get; set; }
}

public sealed class FoodSupportFollowUpContext
{
    public string OrderNo { get; set; } = string.Empty;
    public string SupportCaseId { get; set; } = string.Empty;
    public long CaseRevision { get; set; }
    public long OrderRevision { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public bool ResultNoticeVerified { get; set; }
    public bool CanRetryDispatch { get; set; }
    public bool CanRecordOperatorReview { get; set; }
    public string BlockReason { get; set; } = string.Empty;
    public FoodSupportFollowUpResponse? Receipt { get; set; }
    public FoodSupportFollowUpResponse? LastFollowUp { get; set; }
}
