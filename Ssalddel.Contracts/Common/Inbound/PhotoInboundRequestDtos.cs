namespace Ssalddel.Contracts.Common.Inbound;

/// <summary>가 입고 준비와 실제 입고 처리를 구분하는 공통 제한입니다.</summary>
public static class 가입고사진신청Policy
{
    public const long MaximumPhotoBytes = 5 * 1024 * 1024;
    public const int MaximumNameLength = 100;
    public const string ApiPath = "api/v1/warehouse-operations/inbound-photo-requests";
}

public sealed class 가입고사진신청응답
{
    public Guid Id { get; set; }
    public string 이름 { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public long? 입고요청Id { get; set; }
}

public sealed class 가입고사진신청목록응답
{
    public IReadOnlyList<가입고사진신청응답> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class 가입고사진이름변경요청
{
    public long Revision { get; set; }
    public string 이름 { get; set; } = string.Empty;
}

public sealed class 가입고사진내용응답
{
    public string ContentType { get; set; } = string.Empty;
    public byte[] Bytes { get; set; } = [];
}
