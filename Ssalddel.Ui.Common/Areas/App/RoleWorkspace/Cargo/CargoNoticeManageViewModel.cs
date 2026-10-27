using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>내 공고의 서버 상태와 입력값을 분리하고, 기존 계정·서버별 복구 저장소로 응답 유실을 보존합니다.</summary>
public sealed class CargoNoticeManageViewModel : IDisposable
{
    private const string RecoveryScope = "community.cargo-notice";
    private readonly ICargoNoticeClient client;
    private readonly ISsalddel현재사용자Context user;
    private readonly IWorkflowRecoveryContext context;
    private readonly IWorkflowRecoveryStore store;
    private readonly CargoNoticeDraftSession session;
    private readonly TimeProvider time;
    private WorkflowRecoverySnapshot? snapshot;
    private WorkflowRecoveryRecord? pending;
    private CargoNoticePendingCommand? pendingCommand;
    private long generation;
    private bool disposed;

    public CargoNoticeManageViewModel(ICargoNoticeClient client, ISsalddel현재사용자Context user,
        IWorkflowRecoveryContext context, IWorkflowRecoveryStore store, CargoNoticeDraftSession session, TimeProvider? time = null)
    {
        this.client = client; this.user = user; this.context = context; this.store = store; this.session = session;
        this.time = time ?? TimeProvider.System;
        if (user is ISsalddel현재사용자변경알림 signal) signal.Changed += IdentityChanged;
    }
    public event Action? Changed;
    public bool IsAuthenticated => user.현재사용자.인증됨;
    public bool HasActiveContext => CurrentContext && HasLoaded;
    public bool IsBusy { get; private set; }
    public bool HasLoaded { get; private set; }
    public bool FeatureDisabled { get; private set; }
    public bool RequiresRefresh { get; private set; }
    public bool HasPending => pending is not null;
    public bool CanRetryPending => pendingCommand is not null && !IsBusy && CurrentContext;
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public CargoNoticeForm Form { get; private set; } = new();
    public CargoNoticeOwnerDto? Current { get; private set; }
    public IReadOnlyList<CargoNoticeOwnerDto> Items { get; private set; } = [];
    public IReadOnlyList<CargoNoticeRegion> Regions { get; private set; } = [];
    public int Page { get; private set; } = 1;
    public bool HasNext { get; private set; }
    public bool Consent { get; set; }
    public string ExpiresLocal { get; set; } = "";
    private bool CurrentContext => snapshot is not null && context.IsCurrent(snapshot);
    public bool CanEdit => IsAuthenticated && HasLoaded && CurrentContext && !IsBusy && !FeatureDisabled && !HasPending && !RequiresRefresh;
    public bool IsDirty
    {
        get
        {
            if (Current is null) return true;
            try { return CargoNoticePresentation.Key(Form.Content()) != CargoNoticePresentation.Key(Current.Content); }
            catch (ArgumentException) { return true; }
        }
    }
    public bool CanPublish => CanEdit && Current is not null && Current.Status != CargoNoticeStatus.Published && !IsDirty && Consent;

    public async Task LoadAsync(int page = 1)
    {
        if (disposed || IsBusy) return;
        if (!IsAuthenticated) { IdentityChanged(); return; }
        var ticket = ++generation; IsBusy = true; HasLoaded = false; Error = null; Message = null; Changed?.Invoke();
        try
        {
            var captured = context.Capture(); snapshot = captured;
            var ownerScope = captured.Owner + "|" + captured.Scope(RecoveryScope);
            if (session.OwnerScope != ownerScope)
            {
                session.OwnerScope = ownerScope; session.Form = new(); session.SelectedNoticeId = null; Current = null;
            }
            Form = session.Form;
            pending = await store.LoadAsync(captured.Owner, captured.Scope(RecoveryScope), CancellationToken.None);
            if (!Valid(ticket, captured)) return;
            pendingCommand = ReadPending(pending, captured);
            Regions = await client.RegionsAsync() ?? throw new InvalidDataException();
            if (!Valid(ticket, captured)) return;
            var response = await client.MineAsync(Math.Max(1, page)) ?? throw new InvalidDataException();
            if (!Valid(ticket, captured)) return;
            SetList(response); HasLoaded = true; FeatureDisabled = false;
            if (pending is not null)
            {
                await ResolveCoreAsync(ticket, captured);
            }
            else if (session.SelectedNoticeId is { } id)
            {
                // A successful list read cannot replace the selected notice's latest-owner read.
                // Keep writes blocked after a receipt whose follow-up detail response was lost.
                RequiresRefresh = true;
                var latest = await client.OwnerAsync(id) ?? throw new InvalidDataException();
                if (!Valid(ticket, captured)) return;
                // Unsaved form survives same-account navigation; the server remains the state authority.
                Current = latest; Consent = false; RequiresRefresh = false;
            }
            else RequiresRefresh = false;
            if (ExpiresLocal.Length == 0) ExpiresLocal = CargoNoticePresentation.InputTime(time.GetUtcNow().AddDays(1));
        }
        catch (Exception exception) { if (CurrentTicket(ticket)) Fail(exception); }
        finally { Finish(ticket); }
    }

    public void DraftChanged() { Consent = false; session.Form = Form; Changed?.Invoke(); }
    public void NewDraft()
    {
        if (!CanEdit) return;
        Current = null; session.SelectedNoticeId = null; Form = session.Form = new(); Consent = false; Message = null; Error = null; Changed?.Invoke();
    }
    public async Task SelectAsync(string noticeId)
    {
        if (!CanEdit) return;
        var captured = snapshot!; var ticket = ++generation; IsBusy = true; Error = null; Message = null; Changed?.Invoke();
        try
        {
            var latest = await client.OwnerAsync(noticeId) ?? throw new InvalidDataException();
            if (Valid(ticket, captured)) SetCurrent(latest);
        }
        catch (Exception exception) { if (Valid(ticket, captured)) Fail(exception); }
        finally { Finish(ticket); }
    }
    public async Task SaveAsync()
    {
        if (!CanEdit) return;
        try
        {
            var content = Form.Content();
            if (content.Title.Length > 80 || content.GoodsName.Length > 80 || content.Quantity > 1_000_000 || content.WeightKg > 1_000_000
                || !Regions.Any(r => r.RegionKey == content.PickupRegionKey) || !Regions.Any(r => r.RegionKey == content.DropoffRegionKey))
                throw new ArgumentException("글자 수·수량·중량과 제공되는 지역을 다시 확인해 주세요.");
            await StartAsync(new CargoNoticePendingCommand
            {
                Action = Current is null ? "Create" : "Update", NoticeId = Current?.NoticeId,
                Draft = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Current?.Revision ?? 0, Content = content }
            });
        }
        catch (ArgumentException exception) { Error = exception.Message; Changed?.Invoke(); }
    }
    public async Task PublishAsync()
    {
        if (!CanPublish) return;
        try
        {
            var expiry = CargoNoticePresentation.ParseTime(ExpiresLocal);
            if (expiry is null || expiry <= time.GetUtcNow() || expiry > time.GetUtcNow().AddDays(30))
                throw new ArgumentException("게시 만료는 현재 이후부터 30일 이내로 선택해 주세요.");
            await StartAsync(new CargoNoticePendingCommand { Action = "Publish", NoticeId = Current!.NoticeId,
                Publish = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Current.Revision,
                    ExpectedContentRevision = Current.ContentRevision, ConsentToPublicPublication = true, ExpiresAtUtc = expiry.Value } });
        }
        catch (ArgumentException exception) { Error = exception.Message; Changed?.Invoke(); }
    }
    public Task WithdrawAsync() => !CanEdit || Current?.Status != CargoNoticeStatus.Published ? Task.CompletedTask
        : StartAsync(new CargoNoticePendingCommand { Action = "Withdraw", NoticeId = Current.NoticeId,
            Mutation = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Current.Revision } });

    private async Task StartAsync(CargoNoticePendingCommand command)
    {
        if (!CanEdit) return;
        var captured = snapshot!; var ticket = ++generation; IsBusy = true; Error = null; Message = null; Changed?.Invoke();
        try
        {
            var payload = JsonSerializer.Serialize(command);
            var record = new WorkflowRecoveryRecord(captured.Owner, captured.Scope(RecoveryScope), command.RequestId,
                payload, time.GetUtcNow().UtcDateTime, command.NoticeId);
            // No HTTP mutation before the original payload is durably accepted and read back in the same scope.
            await store.SaveAsync(record, CancellationToken.None);
            if (!Valid(ticket, captured)) return;
            var saved = await store.LoadAsync(record.Owner, record.Scope, CancellationToken.None);
            if (!Valid(ticket, captured)) return;
            if (saved?.RequestId != record.RequestId || saved.PayloadJson != payload || saved.Target != record.Target)
                throw new InvalidDataException("이전 요청 저장을 확인하지 못했습니다.");
            pending = saved; pendingCommand = command; Consent = false;
            await SendCoreAsync(command, ticket, captured);
        }
        catch (Exception exception) { if (Valid(ticket, captured)) Fail(exception); }
        finally { Finish(ticket); }
    }
    public async Task ResolvePendingAsync()
    {
        if (!HasPending || IsBusy || !CurrentContext) return;
        var captured = snapshot!; var ticket = ++generation; IsBusy = true; Error = null; Changed?.Invoke();
        try { await ResolveCoreAsync(ticket, captured); }
        catch (Exception exception) { if (Valid(ticket, captured)) Fail(exception); }
        finally { Finish(ticket); }
    }
    public async Task RetryPendingAsync()
    {
        if (!CanRetryPending) return;
        var command = pendingCommand!; var captured = snapshot!; var ticket = ++generation;
        IsBusy = true; Error = null; Changed?.Invoke();
        try
        {
            // Explicit user retry only, after one more read. Never manufacture a fresh request id.
            if (!await ResolveCoreAsync(ticket, captured) && Valid(ticket, captured) && pendingCommand is not null)
                await SendCoreAsync(command, ticket, captured);
        }
        catch (Exception exception) { if (Valid(ticket, captured)) Fail(exception); }
        finally { Finish(ticket); }
    }
    private async Task SendCoreAsync(CargoNoticePendingCommand command, long ticket, WorkflowRecoverySnapshot captured)
    {
        if (!Valid(ticket, captured)) return;
        CargoNoticeCommandResponse? response = command.Action switch
        {
            "Create" => await client.CreateAsync(command.Draft!),
            "Update" => await client.UpdateAsync(command.NoticeId!, command.Draft!),
            "Publish" => await client.PublishAsync(command.NoticeId!, command.Publish!),
            "Withdraw" => await client.WithdrawAsync(command.NoticeId!, command.Mutation!),
            _ => throw new InvalidDataException()
        };
        if (!Valid(ticket, captured)) return;
        if (response is null) { Error = "저장 응답을 확인하지 못했습니다. 이전 요청의 결과부터 확인해 주세요."; return; }
        await AcceptReceiptAsync(response, ticket, captured);
    }
    private async Task<bool> ResolveCoreAsync(long ticket, WorkflowRecoverySnapshot captured)
    {
        if (pending is null || !Valid(ticket, captured)) return false;
        CargoNoticeCommandResponse? result;
        try { result = await client.ResultAsync(pending.RequestId, pending.Target); }
        catch (SsalddelApiException exception) when (exception.StatusCode == 404 && exception.ErrorCode == "CargoNotice.NotFound")
        {
            if (Valid(ticket, captured)) Error = "아직 저장 결과가 확인되지 않습니다. 잠시 후 다시 확인하거나 이전 요청을 같은 내용으로 다시 보내세요.";
            return false;
        }
        if (!Valid(ticket, captured) || result is null) return false;
        await AcceptReceiptAsync(result, ticket, captured); return true;
    }
    private async Task AcceptReceiptAsync(CargoNoticeCommandResponse result, long ticket, WorkflowRecoverySnapshot captured)
    {
        if (!Valid(ticket, captured) || pending is null) return;
        if (result.ClientRequestId != pending.RequestId || result.Notice is null
            || pending.Target is not null && result.Notice.NoticeId != pending.Target
            || pendingCommand is not null && !string.Equals(result.Operation, pendingCommand.Action, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException();
        CargoNoticeClient.Id(result.Notice.NoticeId);
        // An immutable command receipt is not the current notice. Keep writes blocked until latest-owner GET succeeds.
        RequiresRefresh = true;
        await store.RemoveAsync(captured.Owner, captured.Scope(RecoveryScope), pending.RequestId, CancellationToken.None);
        if (!Valid(ticket, captured)) return;
        pending = null; pendingCommand = null;
        session.SelectedNoticeId = result.Notice.NoticeId;
        var latest = await client.OwnerAsync(result.Notice.NoticeId) ?? throw new InvalidDataException();
        if (!Valid(ticket, captured)) return;
        SetCurrent(latest); RequiresRefresh = false; Error = null; Message = "처리 결과와 최신 공고를 확인했습니다.";
        var list = await client.MineAsync(Page) ?? throw new InvalidDataException();
        if (Valid(ticket, captured)) SetList(list);
    }
    private CargoNoticePendingCommand? ReadPending(WorkflowRecoveryRecord? record, WorkflowRecoverySnapshot captured)
    {
        if (record is null) return null;
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != captured.Owner || record.Scope != captured.Scope(RecoveryScope)) throw new InvalidDataException();
        if (record.Target is not null) CargoNoticeClient.Id(record.Target);
        if (record.PayloadJson is null) return null; // Retention-expired payload: result GET remains available, no retry.
        var command = JsonSerializer.Deserialize<CargoNoticePendingCommand>(record.PayloadJson) ?? throw new InvalidDataException();
        var count = (command.Draft is null ? 0 : 1) + (command.Publish is null ? 0 : 1) + (command.Mutation is null ? 0 : 1);
        if (count != 1 || command.RequestId != record.RequestId || command.NoticeId != record.Target
            || command.Action is not ("Create" or "Update" or "Publish" or "Withdraw")
            || command.Action == "Create" && (command.Draft is null || command.NoticeId is not null || command.Draft.ExpectedRevision != 0)
            || command.Action == "Update" && (command.Draft is null || command.NoticeId is null)
            || command.Action == "Publish" && (command.Publish is null || command.NoticeId is null)
            || command.Action == "Withdraw" && (command.Mutation is null || command.NoticeId is null))
            throw new InvalidDataException();
        return command;
    }
    private void SetCurrent(CargoNoticeOwnerDto notice)
    {
        Current = notice; session.SelectedNoticeId = notice.NoticeId; Form = session.Form = CargoNoticeForm.From(notice.Content); Consent = false;
    }
    private void SetList(CargoNoticeOwnerListResponse response)
    { Items = response.Items; Page = response.Page; HasNext = (long)Page * response.PageSize < response.TotalCount; }
    private bool CurrentTicket(long ticket) => !disposed && generation == ticket;
    private bool Valid(long ticket, WorkflowRecoverySnapshot captured) => CurrentTicket(ticket) && context.IsCurrent(captured);
    private void Finish(long ticket) { if (CurrentTicket(ticket)) { IsBusy = false; Changed?.Invoke(); } }
    private void Fail(Exception error)
    { FeatureDisabled = CargoNoticePresentation.Disabled(error); Error = FeatureDisabled ? null : CargoNoticePresentation.Error(error); }
    private void IdentityChanged()
    {
        generation++; snapshot = null; IsBusy = false; HasLoaded = false; FeatureDisabled = false; RequiresRefresh = false;
        Current = null; Items = []; Regions = []; pending = null; pendingCommand = null; Form = new(); Consent = false; Error = null; Message = null;
        // The old owner's protected recovery record is preserved. Never remove it on logout/account change.
        Changed?.Invoke();
    }
    public void Dispose()
    {
        disposed = true; generation++;
        if (user is ISsalddel현재사용자변경알림 signal) signal.Changed -= IdentityChanged;
    }
}

public sealed class CargoNoticePendingCommand
{
    public string Action { get; set; } = "";
    public string? NoticeId { get; set; }
    public CargoNoticeDraftRequest? Draft { get; set; }
    public CargoNoticePublishRequest? Publish { get; set; }
    public CargoNoticeMutationRequest? Mutation { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid RequestId => Draft?.ClientRequestId ?? Publish?.ClientRequestId ?? Mutation?.ClientRequestId ?? Guid.Empty;
}
