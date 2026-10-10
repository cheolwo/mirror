namespace Ssalddel.Domain.Notifications;
public sealed class OperationsBudgetUsage
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public DateTime DayUtc { get; set; }
    public int Count { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
