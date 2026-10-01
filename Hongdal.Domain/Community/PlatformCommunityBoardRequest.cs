namespace Hongdal.Domain.Community;

public sealed class PlatformCommunityBoardRequest
{
    public long Id { get; set; }
    public string AppKey { get; set; } = "platform";
    public string BoardKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string RequestReason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? OperatorMemo { get; set; }
    public string? ReviewedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
}
