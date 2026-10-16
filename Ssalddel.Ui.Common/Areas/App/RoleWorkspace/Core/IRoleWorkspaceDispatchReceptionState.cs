namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>역할별 신규 배차 수신 안내를 공통 화면에 전달하며 실제 수신 상태를 변경하지 않습니다.</summary>
public interface IRoleWorkspaceDispatchReceptionState
{
    string? DispatchReceptionNotice { get; }
}
