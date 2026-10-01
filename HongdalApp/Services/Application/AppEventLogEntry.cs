namespace HongdalApp.Services.Application;

public sealed class AppEventLogEntry
{
    public long Id { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}
