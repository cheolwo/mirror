using System.Security.Cryptography;
using System.Text;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>
/// 전송 가능성이 있는 입력의 관측 번호만 보관합니다. 서버 멱등 키나 처리 성공 증거가 아니며
/// 사진·서명·주소·신고 원문을 저장하거나 미확정 명령을 다시 전송하지 않습니다.
/// </summary>
public sealed class CargoInputRecovery(IRoleWorkspaceAccess access, IWorkflowRecoveryStore? store = null,
    IWorkflowRecoveryContext? context = null)
{
    private readonly IWorkflowRecoveryStore records = store ?? new VolatileWorkflowRecoveryStore();
    private WorkflowRecoverySnapshot? snapshot;
    private string target = "";
    private string logicalScope = "";
    private long generation;
    public WorkflowRecoveryRecord? Pending { get; private set; }
    public bool Checked { get; private set; }
    public bool CanSubmit => Checked && Pending is null;

    public void Reset(string targetId, string action)
    {
        generation++; target = targetId;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(targetId))).ToLowerInvariant()[..24];
        logicalScope = $"cargo.cargo-driver.{action}.{hash}.v1";
        snapshot = null; Pending = null; Checked = false;
    }

    public async Task LoadAsync(CancellationToken ct)
    {
        var expected = Capture(); var version = generation;
        Checked = false;
        var record = await records.LoadAsync(expected.Owner, expected.Scope(logicalScope), ct);
        RequireCurrent(version, expected, ct);
        if (record is not null && (record.Owner != expected.Owner || record.Scope != expected.Scope(logicalScope)
            || record.Target != target || record.RequestId == Guid.Empty || record.PayloadJson is not null))
            throw new InvalidDataException("이전 입력의 업무 확인 기록이 일치하지 않습니다.");
        if (record is null && Pending is not null && snapshot == expected)
            throw new InvalidDataException("이전에 보낸 입력의 확인 기록을 찾지 못했습니다.");
        snapshot = expected; Pending = record; Checked = true;
    }

    public async Task BeginAsync(CancellationToken ct)
    {
        if (!CanSubmit || snapshot is null) throw new InvalidOperationException("이전 처리 결과를 먼저 확인해 주세요.");
        var expected = snapshot; var version = generation;
        RequireCurrent(version, expected, ct);
        Pending = new(expected.Owner, expected.Scope(logicalScope), Guid.NewGuid(), null, DateTime.UtcNow, target);
        Checked = false;
        await records.SaveAsync(Pending, ct);
        RequireCurrent(version, expected, ct); Checked = true;
    }

    public async Task ConfirmAsync(CancellationToken ct)
    {
        if (Pending is null || snapshot is null) return;
        var expected = snapshot; var version = generation; var id = Pending.RequestId;
        RequireCurrent(version, expected, ct);
        await records.RemoveAsync(expected.Owner, expected.Scope(logicalScope), id, ct);
        RequireCurrent(version, expected, ct); Pending = null; Checked = true;
    }

    public void RequireCurrent(CancellationToken ct)
    {
        if (snapshot is null) throw new InvalidOperationException("현재 업무의 확인 문맥이 없습니다.");
        RequireCurrent(generation, snapshot, ct);
    }

    private WorkflowRecoverySnapshot Capture()
    {
        if (context is not null) return context.Capture();
        // Constructor-only consumers have a volatile store, never durable records without a server scope.
        if (store is not null) throw new InvalidOperationException("요청을 보낸 서버의 확인 문맥이 필요합니다.");
        var identity = access.GetIdentity(RoleWorkspaceCatalog.CargoDriver);
        if (!identity.IsAuthenticated || string.IsNullOrWhiteSpace(identity.OwnerId))
            throw new InvalidOperationException("로그인한 기사의 업무만 확인할 수 있습니다.");
        return new(identity.OwnerId, identity.Revision, new string('0', 32));
    }

    private void RequireCurrent(long version, WorkflowRecoverySnapshot expected, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (version != generation || Capture() != expected || context is not null && !context.IsCurrent(expected))
            throw new OperationCanceledException("계정·서버 또는 선택한 업무가 변경되었습니다.", ct);
    }
}
