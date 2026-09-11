namespace Ssalddel.Contracts.Common.Operations;

public static class 운영체제업무인계상태Codes
{
    public const string 요청됨 = "Requested";
    public const string 수락됨 = "Accepted";
    public const string 거절됨 = "Rejected";
    public const string 보류됨 = "Held";
    public const string 만료됨 = "Expired";

    public static bool Is종결(string value)
        => value is 수락됨 or 거절됨 or 만료됨;
}

public static class 운영체제업무인계결정Codes
{
    public const string 수락 = "Accept";
    public const string 거절 = "Reject";
    public const string 보류 = "Hold";

    public static bool IsKnown(string value)
        => value is 수락 or 거절 or 보류;
}

public static class 운영체제업무인계공개범위Codes
{
    public const string 최소필요업무자료 = "MinimumRequiredOperationalData";
}

public sealed class 운영체제업무인계생성요청
{
    public Guid 클라이언트요청Id { get; init; }
    public string 출발운영체제Id { get; init; } = string.Empty;
    public string 도착운영체제Id { get; init; } = string.Empty;
    public string 출발업무유형Code { get; init; } = string.Empty;
    public string 출발업무StableId { get; init; } = string.Empty;
    public long 출발업무Revision { get; init; }
    public string 인계계약Code { get; init; } = string.Empty;
    public string 인계계약Revision { get; init; } = string.Empty;
    public string 최소상태사본Json { get; init; } = "{}";
    public string 공개범위Code { get; init; } = 운영체제업무인계공개범위Codes.최소필요업무자료;
    public DateTime 만료시각Utc { get; init; }
}

public sealed class 운영체제업무인계결정요청
{
    public Guid 클라이언트요청Id { get; init; }
    public long 예상Revision { get; init; }
    public string 응답운영체제Id { get; init; } = string.Empty;
    public string 결정Code { get; init; } = string.Empty;
    public string 도착업무StableId { get; init; } = string.Empty;
    public string 사유Code { get; init; } = string.Empty;
}

public sealed class 운영체제업무인계Dto
{
    public string 인계StableId { get; init; } = string.Empty;
    public string 출발운영체제Id { get; init; } = string.Empty;
    public string 도착운영체제Id { get; init; } = string.Empty;
    public string 현재책임운영체제Id { get; init; } = string.Empty;
    public string 출발업무유형Code { get; init; } = string.Empty;
    public string 출발업무StableId { get; init; } = string.Empty;
    public long 출발업무Revision { get; init; }
    public string 도착업무StableId { get; init; } = string.Empty;
    public string 인계계약Code { get; init; } = string.Empty;
    public string 인계계약Revision { get; init; } = string.Empty;
    public string 공개범위Code { get; init; } = string.Empty;
    public string 상태Code { get; init; } = string.Empty;
    public string 응답사유Code { get; init; } = string.Empty;
    public long Revision { get; init; }
    public DateTime 요청시각Utc { get; init; }
    public DateTime 만료시각Utc { get; init; }
    public DateTime? 응답시각Utc { get; init; }
}
