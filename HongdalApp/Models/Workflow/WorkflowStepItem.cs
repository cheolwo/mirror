namespace HongdalApp.Models.Workflow;

public sealed class WorkflowStepItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsAvailable { get; set; }
    public string AccentColor { get; set; } = "#94A3B8";
}
