using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>화면 이동 중 같은 계정의 입력·원 요청 번호를 보존합니다. 영구 저장이나 실행 승인이 아닙니다.</summary>
public sealed class 공간공동운영DraftSession
{
    private string? _owner;
    public 공간공동운영생성Request Form { get; private set; } = NewForm();
    public 공간공동운영생성Request? PendingCreate { get; internal set; }
    public 공간공동운영명령Request? PendingCommand { get; internal set; }
    public string? PendingLedgerId { get; internal set; }
    public WorkflowRecoverySnapshot? PendingScope { get; internal set; }
    public bool HasPending => PendingCreate is not null || PendingCommand is not null;
    public Guid? PendingRequestId => PendingCreate?.ClientRequestId ?? PendingCommand?.ClientRequestId;
    public void Bind(string? owner)
    {
        // 인증 화면 왕복 동안 같은 계정의 원 요청을 잃지 않습니다. 다른 로그인 계정에는 전달하지 않습니다.
        if (owner is null) return;
        if (string.Equals(_owner, owner, StringComparison.Ordinal)) return;
        _owner = owner;
        Form = NewForm();
        ClearPending();
    }
    internal void ClearPending()
    { PendingCreate = null; PendingCommand = null; PendingLedgerId = null; PendingScope = null; }
    internal void ResetForm() => Form = NewForm();
    private static 공간공동운영생성Request NewForm() => new() { ClientRequestId = Guid.NewGuid() };
}
