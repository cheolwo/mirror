namespace Ssalddel.Contracts.Admin.Operations;

public static class 운영후속처리복구원천Codes
{
    public const string 음식마트원장동기화 = "FoodMartLedgerSync";
    public const string 운영체제업무인계 = "OperatingSystemHandoff";
}

public static class 운영후속처리복구상태Codes
{
    public const string 자동재시도대기 = "AutoRetryPending";
    public const string 처리중 = "Processing";
    public const string 운영자확인필요 = "OperatorReviewRequired";
    public const string 정상완료 = "Completed";
}

public sealed record 운영후속처리복구항목Dto
{
    public required string 복구StableId { get; init; }
    public required string 원천Code { get; init; }
    public long 원천항목Id { get; init; }
    public required string 업무유형Code { get; init; }
    public required string 상태Code { get; init; }
    public required string 현재책임운영체제Id { get; init; }
    public int 처리시도수 { get; init; }
    public int? 최대자동시도수 { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? 다음처리예정시각Utc { get; init; }
    public bool 운영자확인필요 { get; init; }
    public bool 재시도예약가능 { get; init; }
    public required string 안전요약 { get; init; }
}

public sealed record 운영후속처리복구목록Dto
{
    public DateTime 조회시각Utc { get; init; }
    public int 전체항목수 { get; init; }
    public int 자동재시도대기수 { get; init; }
    public int 처리중수 { get; init; }
    public int 운영자확인필요수 { get; init; }
    public IReadOnlyList<운영후속처리복구항목Dto> 항목 { get; init; } = [];
}

public sealed record 운영후속처리재시도요청Dto
{
    public int 예상처리시도수 { get; init; }
}

public sealed record 운영후속처리재시도응답Dto
{
    public required string 복구StableId { get; init; }
    public required string 상태Code { get; init; }
    public int 처리시도수 { get; init; }
    public DateTime 재시도예약시각Utc { get; init; }
}
