using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ssalddel.Domain.HumanResources;

[Table("hr_work_contact_preferences")]
public sealed class 근무연락동의Record
{
    [Key, MaxLength(450)] public string UserId { get; set; } = "";
    public bool AllowInApp { get; set; }
    public bool AllowKakao { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public DateTime UpdatedAtUtc { get; set; }
}

[Table("hr_work_availability_requests")]
public sealed class 근무가능요청Record
{
    [Key] public Guid Id { get; set; }
    [MaxLength(450)] public string ManagerUserId { get; set; } = "";
    public Guid ClientRequestId { get; set; }
    [MaxLength(64)] public string PayloadHash { get; set; } = "";
    [MaxLength(64)] public string? ActiveKey { get; set; }
    public long WarehouseId { get; set; }
    [MaxLength(200)] public string WarehouseName { get; set; } = "";
    [MaxLength(32)] public string Stage { get; set; } = "";
    [MaxLength(200)] public string GroupKey { get; set; } = "";
    [MaxLength(300)] public string GroupLabel { get; set; } = "";
    public int GroupPage { get; set; }
    public long RemainingQuantity { get; set; }
    public int NeededWorkers { get; set; }
    public int ConfirmedWorkers { get; set; }
    public DateTime WorkStartUtc { get; set; }
    public DateTime WorkEndUtc { get; set; }
    public DateTime ReplyByUtc { get; set; }
    public DateTime SourceObservedAtUtc { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public decimal? ProposedHourlyAmount { get; set; }
    [MaxLength(32)] public string Channel { get; set; } = "";
    public Guid Revision { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; }
}

[Table("hr_work_availability_recipients")]
public sealed class 근무가능대상Record
{
    [Key] public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = "";
    [MaxLength(200)] public string DisplayName { get; set; } = "";
    [MaxLength(32)] public string Status { get; set; } = "Pending";
    [MaxLength(32)] public string DeliveryStatus { get; set; } = "Inbox";
    public DateTime? RespondedAtUtc { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}
