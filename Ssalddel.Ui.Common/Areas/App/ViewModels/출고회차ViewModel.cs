using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>창고와 조회 구간을 현재 로그인·서버에 결속해 출고 진행을 읽습니다.</summary>
public sealed class 출고회차ViewModel : ObservableObject, IDisposable
{
    private readonly I출고예정검토페이지Service service;
    private readonly ISsalddelJsonApiClient warehouses;
    private readonly ISsalddel현재사용자Context user;
    private readonly IWorkflowRecoveryContext recovery;
    private CancellationTokenSource? request;
    private WorkflowRecoverySnapshot? scope;
    private long generation;
    private bool disposed;
    private long warehouseId;
    private DateTime startLocal;
    private DateTime endLocal;
    private OutboundPeriodResponse? snapshot;
    private string stageCode = OutboundWorkflowStageCodes.Picking;
    private int batchPage;
    private string? batchKey;
    private int memberPage;
    private int sourcePage;

    public 출고회차ViewModel(I출고예정검토페이지Service service, ISsalddelJsonApiClient warehouses,
        ISsalddel현재사용자Context user, IWorkflowRecoveryContext recovery, TimeProvider? clock = null)
    {
        this.service = service;
        this.warehouses = warehouses;
        this.user = user;
        this.recovery = recovery;
        var now = (clock ?? TimeProvider.System).GetUtcNow().ToOffset(TimeSpan.FromHours(9));
        startLocal = now.Date.AddHours(now.Hour < 12 ? 0 : 12);
        endLocal = startLocal.AddHours(12);
        if (user is ISsalddel현재사용자변경알림 changes) changes.Changed += AuthenticationChanged;
    }

    public IReadOnlyList<창고요약응답> Warehouses { get; private set; } = [];
    public bool WarehousesLoaded { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public string? Warning { get; private set; }
    public bool IsAuthenticated => user.현재사용자.인증됨;
    public OutboundPeriodResponse? Snapshot => scope is not null && recovery.IsCurrent(scope) ? snapshot : null;
    public string StageCode => stageCode;
    public int BatchPage => batchPage;
    public string? BatchKey => batchKey;
    public int MemberPage => memberPage;
    public int PlanPage => sourcePage;
    public long QueryVersion => generation;
    public long WarehouseId { get => warehouseId; set { if (SetProperty(ref warehouseId, value)) Invalidate(); } }
    public DateTime StartLocal { get => startLocal; set { if (SetProperty(ref startLocal, value)) Invalidate(); } }
    public DateTime EndLocal { get => endLocal; set { if (SetProperty(ref endLocal, value)) Invalidate(); } }

    public async Task InitializeAsync()
    {
        if (disposed || (WarehousesLoaded && scope is not null && recovery.IsCurrent(scope))) return;
        var currentScope = Capture();
        if (currentScope is null) { Error = "로그인 후 창고를 다시 확인해 주세요."; Changed(); return; }
        if (scope != currentScope)
        {
            snapshot = null;
            Warehouses = [];
            WarehousesLoaded = false;
            warehouseId = 0;
        }
        var current = Begin(currentScope);
        var active = request!;
        try
        {
            var result = await warehouses.GetAsync<창고목록응답>("api/v1/warehouse-operations/warehouses",
                "창고 목록 조회", allowNotFound: false, cancellationToken: active.Token)
                ?? throw new InvalidDataException();
            if (!Current(current, active, currentScope)) return;
            Warehouses = result.Items.Where(item => item.Id > 0).ToArray();
            WarehousesLoaded = true;
            if (!Warehouses.Any(item => item.Id == warehouseId)) warehouseId = 0;
        }
        catch (Exception exception)
        {
            if (Current(current, active, currentScope)) Error = ReadError(exception);
        }
        finally { Finish(current, active, currentScope); }
    }

    public async Task LoadAsync(int page = 0)
    {
        if (disposed) return;
        var currentScope = Capture();
        if (currentScope is null) { AuthenticationChanged(); Error = "로그인 후 다시 조회해 주세요."; Changed(); return; }
        if (!Warehouses.Any(item => item.Id == WarehouseId))
        { Error = "조회할 창고를 선택해 주세요."; Changed(); return; }
        if (StartLocal < DateTime.MinValue.AddDays(9) || EndLocal > DateTime.MaxValue.AddDays(-1))
        { Error = "조회할 날짜와 시각을 확인해 주세요."; Changed(); return; }
        var start = ToUtc(StartLocal);
        var end = ToUtc(EndLocal);
        if (end <= start || end - start > TimeSpan.FromHours(24))
        { Error = "조회 시작 이후부터 24시간 이내의 종료 시각을 입력해 주세요."; Changed(); return; }
        var selectedWarehouse = WarehouseId;
        sourcePage = Math.Max(0, page);
        var selectedStage = stageCode;
        var selectedBatchPage = batchPage;
        var selectedBatchKey = batchKey;
        var selectedMemberPage = memberPage;
        var previous = Snapshot;
        var retain = previous?.WarehouseId == selectedWarehouse && previous.PeriodStartUtc == start && previous.PeriodEndUtc == end
            && (previous.Workflow is null || MatchesFlow(previous.Workflow, selectedStage, selectedBatchPage, selectedBatchKey, selectedMemberPage));
        if (!retain) snapshot = null;
        var current = Begin(currentScope);
        var active = request!;
        try
        {
            var result = await service.목록조회Async(new()
            {
                WarehouseId = selectedWarehouse, PeriodStartUtc = start, PeriodEndUtc = end,
                Status = 출고예정검토조회상태코드.전체, Page = Math.Max(0, page), PageSize = 12,
                PeriodStage = selectedStage, BatchPage = selectedBatchPage, BatchPageSize = 6,
                BatchKey = selectedBatchKey, MemberPage = selectedMemberPage, MemberPageSize = 12
            }, active.Token);
            if (!Current(current, active, currentScope)) return;
            var period = result.Period;
            if (period is null || period.WarehouseId != selectedWarehouse || period.PeriodStartUtc != start
                || period.PeriodEndUtc != end || period.ObservedAtUtc == default
                || period.Workflow is { } flow && !MatchesFlow(flow, selectedStage, selectedBatchPage, selectedBatchKey, selectedMemberPage)
                || period.Workflow is null && selectedBatchKey is not null)
                throw new InvalidDataException();
            snapshot = period;
        }
        catch (Exception exception)
        {
            if (!Current(current, active, currentScope)) return;
            if (exception is InvalidDataException or SsalddelApiException { StatusCode: 401 or 403 or 404 }) snapshot = null;
            Error = exception is SsalddelApiException { StatusCode: 404 } && selectedBatchKey is not null
                ? "선택한 작업 묶음을 확인하지 못했습니다. 묶음 목록으로 돌아가 다시 선택해 주세요."
                : ReadError(exception);
            if (snapshot is not null) Warning = "이전에 조회한 결과입니다. 아래 확인 시각을 참고하고 다시 조회해 주세요.";
        }
        finally { Finish(current, active, currentScope); }
    }

    public Task LoadNavigationAsync(OutboundBatchNavigationState state)
    {
        if (!OutboundBatchNavigationState.TryCreate(state.Stage, state.WarehouseId,
            state.StartUtc?.ToString("O"), state.EndUtc?.ToString("O"), state.BatchPage,
            state.BatchKey, state.MemberPage, state.PlanPage, out var validated, out var error) || validated is null)
        {
            Invalidate();
            Error = error ?? "조회할 업무 단계를 다시 확인해 주세요.";
            Changed();
            return Task.CompletedTask;
        }
        Invalidate();
        stageCode = validated.Stage;
        warehouseId = validated.WarehouseId;
        if (validated.StartUtc is { } start && validated.EndUtc is { } end)
        {
            startLocal = DateTime.SpecifyKind(start.AddHours(9), DateTimeKind.Unspecified);
            endLocal = DateTime.SpecifyKind(end.AddHours(9), DateTimeKind.Unspecified);
        }
        batchPage = validated.BatchPage;
        batchKey = validated.BatchKey;
        memberPage = validated.MemberPage;
        sourcePage = validated.PlanPage;
        Changed();
        return warehouseId > 0 ? LoadAsync(sourcePage) : Task.CompletedTask;
    }

    public Task SelectStageAsync(string code)
    {
        if (!OutboundWorkflowStageCodes.All.Contains(code, StringComparer.Ordinal))
        { Error = "조회할 공정을 다시 확인해 주세요."; Changed(); return Task.CompletedTask; }
        var page = Snapshot?.Page ?? sourcePage;
        stageCode = code;
        Invalidate();
        return LoadAsync(page);
    }

    public Task SelectBatchAsync(string key)
    {
        var report = Snapshot;
        if (report?.Workflow is not { } flow || !flow.Groups.Any(group => group.Key == key))
        { Error = "조회할 작업 묶음을 다시 확인해 주세요."; Changed(); return Task.CompletedTask; }
        var page = report.Page;
        batchKey = key;
        memberPage = 0;
        Invalidate(resetFlowSelection: false);
        return LoadAsync(page);
    }

    public Task ShowBatchListAsync()
    {
        var page = Snapshot?.Page ?? sourcePage;
        batchKey = null;
        memberPage = 0;
        Invalidate(resetFlowSelection: false);
        return LoadAsync(page);
    }

    public Task ChangeBatchPageAsync(int page)
    {
        var currentPage = Snapshot?.Page ?? sourcePage;
        batchPage = Math.Max(0, page);
        batchKey = null;
        memberPage = 0;
        Invalidate(resetFlowSelection: false);
        return LoadAsync(currentPage);
    }

    public Task ChangeMemberPageAsync(int page)
    {
        if (batchKey is null) return Task.CompletedTask;
        var currentPage = Snapshot?.Page ?? sourcePage;
        memberPage = Math.Max(0, page);
        Invalidate(resetFlowSelection: false);
        return LoadAsync(currentPage);
    }

    public bool CanOpenPlan(long id)
        => !IsLoading && id > 0 && Snapshot is { } report
            && (report.Items.Any(item => item.OutboundPlanId == id && item.CanOpenReview)
                || report.Workflow?.SelectedGroup?.Members.Any(item => item.OutboundPlanId == id && item.CanOpenReview) == true);

    private static bool MatchesFlow(OutboundPeriodWorkflow flow, string stage, int page, string? key, int memberPage)
        => flow.SelectedStage == stage && flow.Page == page && flow.SelectedGroupKey == key
            && (key is null ? flow.SelectedGroup is null : flow.SelectedGroup is { } group
                && group.Key == key && group.MemberPage == memberPage);

    public void MovePeriod(int direction)
    {
        var duration = EndLocal - StartLocal;
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(24)) return;
        var nextTicks = StartLocal.Ticks + duration.Ticks * Math.Sign(direction);
        if (nextTicks < DateTime.MinValue.AddDays(9).Ticks || nextTicks + duration.Ticks > DateTime.MaxValue.AddDays(-1).Ticks) return;
        var next = new DateTime(nextTicks, DateTimeKind.Unspecified);
        StartLocal = next;
        EndLocal = next.Add(duration);
    }

    public void SelectDay()
    {
        var day = StartLocal.Date;
        if (day >= DateTime.MaxValue.Date) { Error = "조회할 날짜를 확인해 주세요."; Changed(); return; }
        StartLocal = day;
        EndLocal = day.AddDays(1);
    }

    public void Suspend()
    {
        generation++;
        CancelRequest();
        IsLoading = false;
        Changed();
    }

    private long Begin(WorkflowRecoverySnapshot currentScope)
    {
        generation++;
        CancelRequest();
        request = new CancellationTokenSource();
        scope = currentScope;
        Error = Warning = null;
        IsLoading = true;
        Changed();
        return generation;
    }

    private void Invalidate(bool resetFlowSelection = true)
    {
        Suspend();
        if (resetFlowSelection) { batchPage = 0; batchKey = null; memberPage = 0; sourcePage = 0; }
        snapshot = null;
        Error = Warning = null;
        Changed();
    }

    private void AuthenticationChanged()
    {
        if (disposed) return;
        Invalidate();
        Warehouses = [];
        WarehousesLoaded = false;
        warehouseId = 0;
        stageCode = OutboundWorkflowStageCodes.Picking;
        scope = null;
        Changed();
    }

    private WorkflowRecoverySnapshot? Capture()
    {
        try
        {
            var captured = IsAuthenticated ? recovery.Capture() : null;
            return captured?.Owner == user.현재사용자.UserId ? captured : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or InvalidDataException) { return null; }
    }

    private bool Current(long current, CancellationTokenSource active, WorkflowRecoverySnapshot captured)
    {
        if (disposed || generation != current || request != active || active.IsCancellationRequested) return false;
        if (recovery.IsCurrent(captured)) return true;
        AuthenticationChanged();
        return false;
    }

    private void Finish(long current, CancellationTokenSource active, WorkflowRecoverySnapshot captured)
    {
        if (!Current(current, active, captured)) return;
        IsLoading = false;
        Changed();
    }

    private void CancelRequest() { request?.Cancel(); request?.Dispose(); request = null; }
    private void Changed() { if (!disposed) OnPropertyChanged(string.Empty); }
    public static DateTime ToUtc(DateTime local) => new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime;
    private static string ReadError(Exception exception) => exception is SsalddelApiException { StatusCode: 401 }
        ? "로그인이 만료되었습니다. 로그인 후 다시 확인해 주세요."
        : exception is SsalddelApiException { StatusCode: 403 or 404 }
        ? "현재 계정에서 이 창고를 확인할 수 없습니다. 창고 목록을 다시 확인해 주세요."
        : "회차 현황을 확인하지 못했습니다. 연결을 확인하고 다시 조회해 주세요.";

    public void Dispose()
    {
        if (disposed) return;
        if (user is ISsalddel현재사용자변경알림 changes) changes.Changed -= AuthenticationChanged;
        Suspend();
        disposed = true;
        snapshot = null;
        Warehouses = [];
    }
}
