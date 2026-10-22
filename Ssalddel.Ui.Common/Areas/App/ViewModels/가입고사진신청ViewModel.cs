using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>사진 준비 기록의 선택·접수·이름 수정 수명입니다. 정식 입고 명령은 다음 화면이 소유합니다.</summary>
public sealed class 가입고사진신청ViewModel : IDisposable
{
    private readonly I가입고사진신청Client client;
    private readonly IWorkflowRecoveryStore store;
    private readonly IWorkflowRecoveryContext context;
    private readonly I업무사진촬영Adapter? camera;
    private readonly IRoleWorkspaceAccess? access;
    private const string LogicalScope = "warehouse.photo-inbound.create";
    private const string RejectedTarget = "create-photo-rejected";
    private WorkflowRecoverySnapshot? owner;
    private WorkflowRecoveryRecord? pending;
    private 업무촬영사진? selectedPhoto;
    private bool disposed;
    public 가입고사진신청ViewModel(I가입고사진신청Client client, IWorkflowRecoveryStore store,
        IWorkflowRecoveryContext context, I업무사진촬영Adapter? camera = null, IRoleWorkspaceAccess? access = null)
    {
        this.client = client; this.store = store; this.context = context; this.camera = camera; this.access = access;
        if (access is not null) access.Changed += IdentityChanged;
    }
    public event Action? Changed;
    public bool IsBusy { get; private set; }
    public bool RecoveryBlocked { get; private set; }
    public string? Message { get; private set; }
    public bool HasError { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string Rename { get; set; } = string.Empty;
    public 가입고사진신청응답? Current { get; private set; }
    public 가입고사진신청목록응답 List { get; private set; } = new();
    public string? PreviewUrl { get; private set; }
    public bool HasSelectedPhoto => selectedPhoto is not null;
    public bool CaptureSupported => camera?.IsSupported == true;
    public bool HasPendingRequest => pending is not null;
    public bool CanSubmit => !IsBusy && !RecoveryBlocked && pending is null && Current is null && selectedPhoto is not null;
    public bool CanRename => !IsBusy && !HasError && !RecoveryBlocked && Current is { 입고요청Id: null } && Rename.Trim().Length <= 가입고사진신청Policy.MaximumNameLength;
    public bool CanStartFormal => !IsBusy && !HasError && !RecoveryBlocked && pending is null && Current is { 입고요청Id: null };

    public Task InitializeAsync(Guid? selectedId = null, CancellationToken ct = default)
        => RunAsync(async snapshot =>
        {
            await RestoreAsync(snapshot, ct);
            if (pending is not null) await RecoverAsync(snapshot, ct);
            else if (selectedId is { } id && id != Guid.Empty) await SelectCoreAsync(id, snapshot, ct);
            await LoadListCoreAsync(List.Page, snapshot, ct);
        }, ct);

    public Task RefreshAsync(CancellationToken ct = default)
        => RunAsync(async snapshot =>
        {
            await RestoreAsync(snapshot, ct);
            if (pending is not null) await RecoverAsync(snapshot, ct);
            else if (Current is { } current) await SelectCoreAsync(current.Id, snapshot, ct);
            await LoadListCoreAsync(List.Page, snapshot, ct);
        }, ct);

    public Task LoadPageAsync(int page, CancellationToken ct = default)
        => RunAsync(snapshot => LoadListCoreAsync(page, snapshot, ct), ct);

    public Task SelectAsync(Guid id, CancellationToken ct = default)
        => RunAsync(snapshot => SelectCoreAsync(id, snapshot, ct), ct);

    public Task NewAsync(CancellationToken ct = default)
        => RunAsync(async snapshot =>
        {
            await RestoreAsync(snapshot, ct);
            if (pending is not null || RecoveryBlocked) { Notice("먼저 접수 결과를 확인해 주세요.", true); return; }
            Current = null; PreviewUrl = null; selectedPhoto = null; Name = Rename = string.Empty;
        }, ct);

    public Task SelectPhotoAsync(IBrowserFile file, CancellationToken ct = default)
        => RunAsync(async snapshot =>
        {
            if (pending is not null || RecoveryBlocked || Current is not null) return;
            if (file.Size <= 0 || file.Size > 가입고사진신청Policy.MaximumPhotoBytes)
                throw new ArgumentException("5MB 이하의 JPG·PNG 사진을 선택해 주세요.");
            await using var input = file.OpenReadStream(가입고사진신청Policy.MaximumPhotoBytes, ct);
            using var bytes = new MemoryStream(); await input.CopyToAsync(bytes, ct); RequireCurrent(snapshot);
            ApplyPhoto(new("inbound-photo", file.ContentType, bytes.ToArray()));
        }, ct);

    public Task CaptureAsync(CancellationToken ct = default)
        => RunAsync(async snapshot =>
        {
            if (!CaptureSupported || pending is not null || RecoveryBlocked || Current is not null) return;
            try
            {
                var photo = await camera!.CaptureAsync("입고할 물품 사진", ct); RequireCurrent(snapshot);
                if (photo is null) { Notice("촬영을 취소했습니다."); return; }
                ApplyPhoto(photo);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            { RequireCurrent(snapshot); Notice("촬영하지 못했습니다. 카메라 권한을 확인하거나 사진을 선택해 주세요.", true); }
        }, ct);

    public Task SubmitAsync(CancellationToken ct = default)
        => RunAsync(async snapshot =>
        {
            await RestoreAsync(snapshot, ct);
            if (pending is not null || RecoveryBlocked || Current is not null) return;
            if (selectedPhoto is null) { Notice("사진을 먼저 선택해 주세요.", true); return; }
            if (Name.Trim().Length > 가입고사진신청Policy.MaximumNameLength) { Notice("이름은 100자 이내로 입력해 주세요.", true); return; }
            var photo = selectedPhoto;
            pending = new(snapshot.Owner, snapshot.Scope(LogicalScope), Guid.NewGuid(), null, DateTime.UtcNow, "create-photo");
            RecoveryBlocked = true;
            await store.SaveAsync(pending, ct); RequireCurrent(snapshot);
            var saved = await store.LoadAsync(snapshot.Owner, snapshot.Scope(LogicalScope), ct); RequireCurrent(snapshot);
            if (saved != pending) throw new InvalidOperationException("접수 준비를 확인하지 못했습니다.");
            RecoveryBlocked = false;
            가입고사진신청응답? response;
            try { response = await client.CreateAsync(pending.RequestId, Name.Trim(), photo.ContentType, photo.Bytes, ct); }
            catch (Exception ex) when (ExplicitlyNotApplied(ex))
            {
                RequireCurrent(snapshot);
                await ClearRejectedRequestAsync(snapshot, ct);
                Notice("사진이 등록되지 않았습니다. 사진을 확인하거나 다시 선택해 주세요.", true);
                return;
            }
            RequireCurrent(snapshot); RequireRequest(response, pending.RequestId);
            // 저장 응답과 같은 번호의 정본 조회를 구분합니다. 모호한 응답은 자동 재전송하지 않습니다.
            await RecoverAsync(snapshot, ct);
            if (pending is null) await LoadListCoreAsync(0, snapshot, ct);
        }, ct);

    public Task RenameAsync(CancellationToken ct = default)
        => RunAsync(async snapshot =>
        {
            if (RecoveryBlocked || Current is not { 입고요청Id: null } current) return;
            if (Rename.Trim().Length > 가입고사진신청Policy.MaximumNameLength) { Notice("이름은 100자 이내로 입력해 주세요.", true); return; }
            var result = await client.RenameAsync(current.Id, new() { Revision = current.Revision, 이름 = Rename.Trim() }, ct);
            RequireCurrent(snapshot); RequireRequest(result, current.Id);
            if (result!.Revision <= current.Revision) throw new InvalidOperationException("이름 저장 결과를 확인하지 못했습니다.");
            await SelectCoreAsync(current.Id, snapshot, ct);
            await LoadListCoreAsync(List.Page, snapshot, ct);
            Notice("이름을 저장했습니다.");
        }, ct);

    private void ApplyPhoto(업무촬영사진 photo)
    {
        가입고사진신청Client.ValidatePhoto(photo.ContentType, photo.Bytes);
        selectedPhoto = photo with { FileName = "inbound-photo", Bytes = photo.Bytes.ToArray() };
        PreviewUrl = selectedPhoto.PreviewUrl; Notice("사진을 확인하고 신청해 주세요.");
    }
    private async Task RestoreAsync(WorkflowRecoverySnapshot snapshot, CancellationToken ct)
    {
        RecoveryBlocked = true;
        var wasRejected = pending?.Target == RejectedTarget;
        var record = await store.LoadAsync(snapshot.Owner, snapshot.Scope(LogicalScope), ct); RequireCurrent(snapshot);
        if (record is not null)
        {
            WorkflowRecoveryPolicy.Validate(record);
            if (record.Owner != snapshot.Owner || record.Scope != snapshot.Scope(LogicalScope)
                || record.Target is not ("create-photo" or RejectedTarget) || record.PayloadJson is not null)
                throw new InvalidOperationException("이전 접수 기록을 확인하지 못했습니다.");
        }
        pending = record;
        if (record?.Target == RejectedTarget)
        {
            // 서버의 명시 거절은 별도 표식으로 보존합니다. 삭제 실패 뒤 GET 404를 기다리지 않습니다.
            await RemoveRejectedRequestAsync(snapshot, ct);
            Notice("사진이 등록되지 않았습니다. 사진을 확인하거나 다시 선택해 주세요.", true);
        }
        else
        {
            RecoveryBlocked = false;
            if (record is null && wasRejected)
                Notice("사진이 등록되지 않았습니다. 사진을 확인하거나 다시 선택해 주세요.", true);
        }
    }
    private async Task RecoverAsync(WorkflowRecoverySnapshot snapshot, CancellationToken ct)
    {
        var original = pending!;
        var result = await client.GetAsync(original.RequestId, ct); RequireCurrent(snapshot);
        if (result is null) { Notice("접수 결과를 확인 중입니다. 다시 신청하지 마세요.", true); return; }
        RequireRequest(result, original.RequestId);
        var before = await store.LoadAsync(snapshot.Owner, snapshot.Scope(LogicalScope), ct); RequireCurrent(snapshot);
        if (before != original) { RecoveryBlocked = true; throw new InvalidOperationException("접수 기록이 변경되었습니다."); }
        await store.RemoveAsync(snapshot.Owner, snapshot.Scope(LogicalScope), original.RequestId, ct); RequireCurrent(snapshot);
        var remaining = await store.LoadAsync(snapshot.Owner, snapshot.Scope(LogicalScope), ct); RequireCurrent(snapshot);
        if (remaining is not null) { RecoveryBlocked = true; throw new InvalidOperationException("접수 확인 기록을 정리하지 못했습니다."); }
        pending = null; Current = result; Rename = result.이름; selectedPhoto = null; PreviewUrl = null;
        Notice("가 입고 신청을 저장했습니다. 이름과 입고 정보는 나중에 입력해도 됩니다.");
        await LoadPhotoAsync(result.Id, snapshot, ct);
    }
    private async Task ClearRejectedRequestAsync(WorkflowRecoverySnapshot snapshot, CancellationToken ct)
    {
        var original = pending!; RecoveryBlocked = true;
        var saved = await store.LoadAsync(snapshot.Owner, snapshot.Scope(LogicalScope), ct); RequireCurrent(snapshot);
        if (saved != original) throw new InvalidOperationException("접수 기록이 변경되었습니다.");
        var rejected = original with { Target = RejectedTarget };
        await store.SaveAsync(rejected, ct); RequireCurrent(snapshot);
        var readback = await store.LoadAsync(snapshot.Owner, snapshot.Scope(LogicalScope), ct); RequireCurrent(snapshot);
        if (readback != rejected) throw new InvalidOperationException("접수 거절 기록을 확인하지 못했습니다.");
        pending = rejected;
        await RemoveRejectedRequestAsync(snapshot, ct);
    }
    private async Task RemoveRejectedRequestAsync(WorkflowRecoverySnapshot snapshot, CancellationToken ct)
    {
        var original = pending!; RecoveryBlocked = true;
        if (original.Target != RejectedTarget) throw new InvalidOperationException("접수 거절 기록을 확인하지 못했습니다.");
        var saved = await store.LoadAsync(snapshot.Owner, snapshot.Scope(LogicalScope), ct); RequireCurrent(snapshot);
        if (saved != original) throw new InvalidOperationException("접수 기록이 변경되었습니다.");
        await store.RemoveAsync(snapshot.Owner, snapshot.Scope(LogicalScope), original.RequestId, ct); RequireCurrent(snapshot);
        if (await store.LoadAsync(snapshot.Owner, snapshot.Scope(LogicalScope), ct) is not null)
            throw new InvalidOperationException("접수 확인 기록을 정리하지 못했습니다.");
        RequireCurrent(snapshot); pending = null; RecoveryBlocked = false;
    }
    private static bool ExplicitlyNotApplied(Exception exception)
    {
        var failure = exception switch
        {
            SsalddelApiException api => api.WorkflowContext,
            RoleWorkspaceAccessException role => SsalddelApiProblemParser.Parse(role.ResponseBody ?? string.Empty).WorkflowContext,
            _ => null
        };
        return failure?.유효한가() == true
            && OperatingSystemIds.TryNormalize(failure.OperatingSystemId, out var os)
            && os == OperatingSystemIds.WarehouseCommerceFulfillment
            && failure.StageId == OperatingSystemLifecycleStageIds.WarehouseInboundPlan
            && failure.OutcomeCode == 업무처리결과Codes.미적용;
    }
    private async Task SelectCoreAsync(Guid id, WorkflowRecoverySnapshot snapshot, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new ArgumentException("신청을 선택해 주세요.");
        var result = await client.GetAsync(id, ct); RequireCurrent(snapshot); RequireRequest(result, id);
        Current = result; Rename = result!.이름; PreviewUrl = null; selectedPhoto = null;
        await LoadPhotoAsync(id, snapshot, ct);
    }
    private async Task LoadPhotoAsync(Guid id, WorkflowRecoverySnapshot snapshot, CancellationToken ct)
    {
        var photo = await client.PhotoAsync(id, ct); RequireCurrent(snapshot);
        가입고사진신청Client.ValidatePhoto(photo.ContentType, photo.Bytes);
        PreviewUrl = new 업무촬영사진("inbound-photo", photo.ContentType, photo.Bytes).PreviewUrl;
    }
    private async Task LoadListCoreAsync(int page, WorkflowRecoverySnapshot snapshot, CancellationToken ct)
    {
        if (page < 0) throw new ArgumentOutOfRangeException(nameof(page));
        var result = await client.ListAsync(page, 10, ct); RequireCurrent(snapshot);
        if (result.Page != page || result.PageSize != 10 || result.TotalCount < 0 || result.Items.Count > 10
            || result.Items.Select(item => item.Id).Distinct().Count() != result.Items.Count)
            throw new InvalidOperationException("신청 목록을 확인하지 못했습니다.");
        foreach (var item in result.Items) RequireRequest(item, item.Id);
        List = result;
    }
    private static void RequireRequest(가입고사진신청응답? item, Guid expected)
    {
        if (expected == Guid.Empty || item is null || item.Id != expected || item.Revision <= 0 || item.입고요청Id is <= 0
            || item.이름.Length > 가입고사진신청Policy.MaximumNameLength)
            throw new InvalidOperationException("신청 결과를 확인하지 못했습니다.");
    }
    private async Task RunAsync(Func<WorkflowRecoverySnapshot, Task> action, CancellationToken ct)
    {
        if (disposed || IsBusy) return;
        IsBusy = true; HasError = false; Message = null; Changed?.Invoke();
        WorkflowRecoverySnapshot? snapshot = null;
        try
        {
            if (access is not null) await access.EnsureInitializedAsync(RoleWorkspaceCatalog.Shipper, ct);
            snapshot = context.Capture();
            if (owner != snapshot) { Clear(); owner = snapshot; }
            await action(snapshot); RequireCurrent(snapshot);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { if (snapshot is not null && !context.IsCurrent(snapshot)) Clear(); }
        catch (Exception)
        {
            if (snapshot is null || !context.IsCurrent(snapshot)) { Clear(); Notice("로그인 상태를 확인한 뒤 다시 열어 주세요.", true); }
            else Notice(pending is not null ? "접수 결과를 확인해 주세요. 다시 신청하지 마세요." : "처리를 마치지 못했습니다. 다시 확인해 주세요.", true);
        }
        finally { IsBusy = false; Changed?.Invoke(); }
    }
    private void RequireCurrent(WorkflowRecoverySnapshot snapshot)
    { if (disposed || !context.IsCurrent(snapshot)) throw new OperationCanceledException("로그인 상태가 변경되었습니다."); }
    private void Notice(string message, bool error = false) { Message = message; HasError = error; }
    private void Clear()
    { Current = null; PreviewUrl = null; selectedPhoto = null; pending = null; Name = Rename = string.Empty; List = new(); RecoveryBlocked = true; }
    private void IdentityChanged()
    {
        if (owner is null || context.IsCurrent(owner)) return;
        Clear(); owner = null; Notice("로그인 상태가 변경되었습니다. 다시 열어 주세요.", true); Changed?.Invoke();
    }
    public void Dispose()
    { if (disposed) return; disposed = true; if (access is not null) access.Changed -= IdentityChanged; Clear(); Changed = null; }
}
