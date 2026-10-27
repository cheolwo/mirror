namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;

/// <summary>같은 역할 화면의 확인된 수행 결과를 표시 계층에 제공합니다.</summary>
public interface IRoleWorkspaceCompletionFeedback
{
    WorkflowCompletionFeedback CompletionFeedback { get; }
}
