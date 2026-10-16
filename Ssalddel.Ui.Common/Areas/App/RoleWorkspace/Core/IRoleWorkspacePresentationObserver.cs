namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>실제 화면에 표시된 현재 항목의 수신 확인만 수행합니다. 사람의 열람·수락은 뜻하지 않습니다.</summary>
public interface IRoleWorkspacePresentationObserver
{
    Task AcknowledgePresentedAsync(RoleWorkspaceItem displayedItem, CancellationToken cancellationToken);
}
