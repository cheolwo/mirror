namespace Ssalddel.Contracts.Common.Workflow;

/// <summary>클라이언트가 오류 원문 추측 없이 복구 흐름을 선택하기 위한 실패 분류입니다.</summary>
public static class 업무실패분류Codes
{
    public const string 일시기술장애 = "TransientTechnical";
    public const string 판본충돌 = "RevisionConflict";
    public const string 업무규칙차단 = "BusinessRuleBlocked";
    public const string 최종수동검토 = "TerminalManualReview";
}

public static class 업무재시도정책Codes
{
    public const string 자동재시도안함 = "NotAutomatic";
    public const string 동일멱등요청1회 = "SameIdempotentRequestOnce";
    public const string 상태재조회후사용자재시도 = "AfterStateRefresh";
    public const string 운영자안전재시도 = "OperatorSafeRetry";
}

public static class 업무복구행동Ids
{
    public const string 상태전체재조회 = "RefreshState";
    public const string 동일요청멱등재시도 = "RetryIdempotent";
    public const string 운영자안전재시도예약 = "ScheduleOperatorSafeRetry";
}

public static class 업무실패책임역할Codes
{
    public const string 음식배달운영체제 = "FoodDeliveryOS";
    public const string 플랫폼운영자 = "PlatformOperator";
}

public static class 업무복구행동목록
{
    public static bool 포함(IEnumerable<string>? actions, string actionId)
        => actions?.Any(action => string.Equals(action, actionId, StringComparison.Ordinal)) == true;
}
