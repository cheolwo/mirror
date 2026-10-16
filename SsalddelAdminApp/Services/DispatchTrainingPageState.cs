using System.Net;
using Ssalddel.Contracts.Admin.DispatchTraining;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelAdminApp.Services;

public sealed class DispatchTrainingPageState : IDisposable
{
    private readonly IDispatchTrainingClient client;
    private readonly IDispatchTrainingIdentity identity;
    private readonly DispatchTrainingPendingSession pending;
    private readonly 역할앱생명주기State lifecycle;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operationCancellation;
    private long generation;
    private string? owner;
    private bool disposed;
    public DispatchTrainingPageState(IDispatchTrainingClient client, IDispatchTrainingIdentity identity,
        DispatchTrainingPendingSession pending, 역할앱생명주기State lifecycle)
    {
        this.client = client; this.identity = identity; this.pending = pending; this.lifecycle = lifecycle;
        identity.Changed += IdentityChanged; lifecycle.Changed += LifecycleChanged;
        pending.Bind(identity.IsAdmin ? identity.Owner : null); VerifyIdentity();
    }
    public event Action? Changed;
    public bool CapabilityChecked { get; private set; }
    public bool Enabled { get; private set; }
    public bool IsAdmin => identity.IsAdmin && owner == identity.Owner;
    public bool RequiresLogin { get; private set; }
    public bool IsForeground => lifecycle.단계 == 역할앱생명주기단계.활성;
    public bool IsBusy { get; private set; }
    public bool Loaded { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public DispatchTrainingSessionDto? Session { get; private set; }
    public IReadOnlyList<DispatchTrainingDecisionDto> Decisions { get; private set; } = [];
    public bool HasPending => pending.Request is not null;
    public bool CanCommand(string action) => Enabled && IsAdmin && IsForeground && Loaded && !RequiresLogin && !IsBusy && !HasPending && Error is null
        && Session?.AllowedCommands.Contains(action, StringComparer.Ordinal) == true;
    public async Task InitializeAsync(bool decisions = false)
    {
        if (disposed) return;
        Error = null;
        try
        {
            var capability = await client.CapabilityAsync(lifetime.Token);
            if (disposed) return;
            Enabled = capability.Enabled; CapabilityChecked = true;
            if (!Enabled) { Session = null; Decisions = []; Loaded = false; Notify(); return; }
            var authenticationError = await identity.EnsureAsync(lifetime.Token);
            if (disposed) return;
            VerifyIdentity();
            if (authenticationError is not null || !IsAdmin)
            { RequiresLogin = true; Error = "훈련을 관리할 서버관리자 계정으로 로그인해 주세요."; Notify(); return; }
            RequiresLogin = false;
            await RefreshAsync(decisions);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception) { if (!disposed) { Error = "훈련 기능을 확인하지 못했습니다. 연결을 확인하고 다시 조회해 주세요."; Notify(); } }
    }
    public async Task RefreshAsync(bool decisions = false)
    {
        VerifyIdentity();
        if (disposed || !Enabled || !IsAdmin || !IsForeground || IsBusy || RequiresLogin) return;
        var operation = Begin(); IsBusy = true; Error = null; Notify();
        try
        {
            var snapshot = await client.CurrentAsync(operation.Token);
            if (!Current(operation.Generation, operation.Owner)) return;
            Validate(snapshot);
            if (Session?.RunId != snapshot.RunId) Decisions = [];
            Session = snapshot; Loaded = true;
            if (pending.Request is { } request && snapshot.AppliedCommandIds.Contains(request.ClientRequestId)
                && (request.RunId is null || snapshot.RunId == request.RunId))
            { pending.Clear(); Notice = "이전 명령의 처리 결과와 현재 훈련 상태를 확인했습니다."; }
            else if (HasPending) Notice = "이전 명령의 결과가 아직 확인되지 않았습니다. 새 명령을 보내기 전에 현재 상태를 다시 확인해 주세요.";
            if (decisions)
            {
                var feed = await client.DecisionsAsync(operation.Token);
                if (!Current(operation.Generation, operation.Owner)) return;
                if (feed.RunId != snapshot.RunId) throw new InvalidOperationException();
                Decisions = feed.Items;
            }
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { }
        catch (Exception ex) { if (Current(operation.Generation, operation.Owner)) ApplyError(ex); }
        finally { if (Current(operation.Generation, operation.Owner)) { IsBusy = false; Notify(); } }
    }
    public async Task ExecuteAsync(string action)
    {
        VerifyIdentity();
        if (!CanCommand(action) || Session is not { } snapshot) return;
        var request = action == DispatchTrainingCommandCodes.SupportDrivers
            ? (DispatchTrainingCommandRequest)new DispatchTrainingSupportDriversRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = snapshot.Revision, RunId = snapshot.RunId, Count = 1 }
            : new DispatchTrainingCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = snapshot.Revision,
                RunId = action == DispatchTrainingCommandCodes.Start ? null : snapshot.RunId };
        pending.Prepare(action, request);
        var operation = Begin(); IsBusy = true; Error = null; Notice = null; Notify();
        try
        {
            var result = await client.CommandAsync(action, request, operation.Token);
            if (!Current(operation.Generation, operation.Owner)) return;
            if (result.ClientRequestId != request.ClientRequestId || result.Session.Revision <= request.ExpectedRevision
                || request.RunId is not null && result.Session.RunId != request.RunId) throw new InvalidOperationException();
            // 명령 응답만으로 화면 상태를 확정하지 않고 같은 회차의 정본 GET으로 처리 번호를 확인합니다.
            var latest = await client.CurrentAsync(operation.Token);
            if (!Current(operation.Generation, operation.Owner)) return;
            Validate(latest);
            if (latest.RunId != result.Session.RunId || latest.Revision < result.Session.Revision
                || !latest.AppliedCommandIds.Contains(request.ClientRequestId)) throw new InvalidOperationException();
            if (Session?.RunId != latest.RunId) Decisions = [];
            Session = latest; Loaded = true; pending.Clear(); Notice = "현재 훈련 상태를 다시 확인했습니다.";
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (Current(operation.Generation, operation.Owner))
            {
                if (ex is AdminApiException { StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity }) pending.Clear();
                ApplyError(ex, writing: true);
            }
        }
        finally { if (Current(operation.Generation, operation.Owner)) { IsBusy = false; Notify(); } }
    }
    private (long Generation, string? Owner, CancellationToken Token) Begin()
    {
        operationCancellation?.Cancel(); operationCancellation?.Dispose();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        return (++generation, owner, operationCancellation.Token);
    }
    private bool Current(long version, string? expectedOwner)
    {
        VerifyIdentity();
        return !disposed && version == generation && owner == expectedOwner && IsAdmin && IsForeground;
    }
    public void VerifyIdentity()
    {
        var next = identity.IsAdmin ? identity.Owner : null;
        if (owner == next) return;
        owner = next; ++generation; operationCancellation?.Cancel(); IsBusy = false; Loaded = false;
        Session = null; Decisions = []; Error = null; Notice = null; pending.Bind(owner); RequiresLogin = !identity.IsAdmin;
        Notify();
    }
    private void IdentityChanged() { VerifyIdentity(); Notify(); }
    private void LifecycleChanged()
    {
        if (!IsForeground) { ++generation; operationCancellation?.Cancel(); IsBusy = false; }
        Notify();
    }
    private void ApplyError(Exception ex, bool writing = false)
    {
        if (ex is AdminApiException { StatusCode: HttpStatusCode.Unauthorized })
        { RequiresLogin = true; Session = null; Decisions = []; Loaded = false; Error = "로그인이 만료되었습니다. 다시 로그인하고 훈련 상태를 확인해 주세요."; }
        else if (ex is AdminApiException { StatusCode: HttpStatusCode.Forbidden }) Error = "훈련 관리자 권한을 확인해 주세요.";
        else if (ex is AdminApiException { StatusCode: HttpStatusCode.Conflict }) Error = "훈련 상태가 바뀌었습니다. 현재 상태를 다시 조회해 주세요.";
        else Error = writing ? "명령 결과를 확인하지 못했습니다. 다시 보내지 말고 현재 훈련 상태를 조회해 주세요." : "현재 훈련 상태를 불러오지 못했습니다. 다시 조회해 주세요.";
    }
    private static void Validate(DispatchTrainingSessionDto snapshot)
    {
        if (snapshot.Revision < 0 || snapshot.Counts.GeneratedOrders < 0 || snapshot.Counts.CompletedOrders < 0
            || snapshot.MaximumOrders != 15 || snapshot.DurationSeconds != 1800 || snapshot.OrderIntervalSeconds != 120
            || snapshot.Counts.GeneratedOrders > snapshot.MaximumOrders || snapshot.RemainingGenerationSeconds < 0
            || snapshot.ActiveDriverCount < 0 || snapshot.ActivatedSupportDriverCount is < 0 or > 2
            || snapshot.AvailableSupportDriverCount is < 0 or > 2
            || snapshot.RunId is null && snapshot.PhaseCode != DispatchTrainingPhaseCodes.Idle) throw new InvalidOperationException();
    }
    private void Notify() { if (!disposed) Changed?.Invoke(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; ++generation; identity.Changed -= IdentityChanged; lifecycle.Changed -= LifecycleChanged;
        operationCancellation?.Cancel(); operationCancellation?.Dispose(); lifetime.Cancel(); lifetime.Dispose();
        Session = null; Decisions = [];
    }
}
