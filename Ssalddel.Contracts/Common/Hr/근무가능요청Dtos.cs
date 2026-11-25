using Ssalddel.Contracts.Common.Inventory;

namespace Ssalddel.Contracts.Common.Hr;

public static class 근무가능상태코드
{
    public const string Pending = "Pending", Available = "Available", Unavailable = "Unavailable",
        Confirmed = "Confirmed", Withdrawn = "Withdrawn";
    public static string 표시명(string value) => value switch
    {
        Pending => "답변 대기", Available => "가능 · 확정 전", Unavailable => "불가",
        Confirmed => "인력 계획 확정", Withdrawn => "철회", _ => "확인 필요"
    };
}

public static class 근무연락채널
{
    public const string InApp = "InApp", Kakao = "Kakao", Sms = "Sms";
}

public sealed class 근무연락동의
{
    public bool AllowInApp { get; set; }
    public bool AllowKakao { get; set; }
    public Guid Revision { get; set; }
}

public class 근무가능업무범위
{
    public long WarehouseId { get; set; }
    public string Stage { get; set; } = OutboundWorkflowStageCodes.Picking;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public string GroupKey { get; set; } = "";
    public int GroupPage { get; set; }
}

public sealed class 근무가능요청등록요청 : 근무가능업무범위
{
    public Guid ClientRequestId { get; set; }
    public DateTime WorkStartUtc { get; set; }
    public DateTime WorkEndUtc { get; set; }
    public DateTime ReplyByUtc { get; set; }
    public int NeededWorkers { get; set; } = 1;
    public decimal? ProposedHourlyAmount { get; set; }
    public string Channel { get; set; } = 근무연락채널.InApp;
    public IReadOnlyList<string> RecipientUserIds { get; set; } = [];
    public bool ConfirmedAvailabilityOnly { get; set; }
}

public sealed record 근무연락채널응답(string Code, string Name, bool Available, string Reason);
public sealed record 근무가능후보(string UserId, string DisplayName, string RoleName,
    bool AllowInApp, bool AllowKakao, bool HasVerifiedPhone);

public sealed class 근무가능문맥응답
{
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public DateTime ObservedAtUtc { get; set; }
    public IReadOnlyList<OutboundPeriodGroup> Groups { get; set; } = [];
    public bool HasNextGroupPage { get; set; }
    public IReadOnlyList<근무가능후보> Candidates { get; set; } = [];
    public IReadOnlyList<근무연락채널응답> Channels { get; set; } = [];
}

public sealed class 근무가능대상응답
{
    public Guid InvitationId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = 근무가능상태코드.Pending;
    public Guid Revision { get; set; }
    public string DeliveryStatus { get; set; } = "";
    public bool CanReply { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanConfirm { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}

public sealed class 근무가능요청응답
{
    public Guid RequestId { get; set; }
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public string Stage { get; set; } = "";
    public string GroupKey { get; set; } = "";
    public string GroupLabel { get; set; } = "";
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
    public string Channel { get; set; } = "";
    public bool Expired { get; set; }
    public IReadOnlyList<근무가능대상응답> Recipients { get; set; } = [];
}

public sealed class 근무가능목록응답
{
    public IReadOnlyList<근무가능요청응답> Items { get; set; } = [];
    public bool HasMore { get; set; }
}

public sealed class 근무가능답변요청
{
    public string Status { get; set; } = "";
    public Guid Revision { get; set; }
}

public static class 근무인력추정
{
    public static int? 필요인원(long remaining, decimal? unitsPerHour, decimal? netHours)
    {
        if (remaining < 0 || unitsPerHour is not > 0 || netHours is not > 0) return null;
        try
        {
            var capacity = checked(unitsPerHour.Value * netHours.Value);
            if (capacity <= 0) return null;
            var people = decimal.Ceiling(remaining / capacity);
            return people <= int.MaxValue ? (int)people : null;
        }
        catch (OverflowException) { return null; }
    }
}
