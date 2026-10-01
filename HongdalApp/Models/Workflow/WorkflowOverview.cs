using System.Collections.ObjectModel;

namespace HongdalApp.Models.Workflow;

public sealed class WorkflowOverview
{
    public string RoleTitle { get; set; } = "화주 업무 흐름";
    public string CurrentStateLabel { get; set; } = string.Empty;
    public string NextActionLabel { get; set; } = string.Empty;
    public string PrimaryActionKey { get; set; } = string.Empty;
    public ObservableCollection<WorkflowStepItem> Steps { get; set; } = new();
}
