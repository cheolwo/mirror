using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public enum InboundRequestPageMessageTone
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record InboundRequestListFilter(string? Search = null, string? Status = null, string? FlowType = null);

public sealed class InboundRequestPageState
{
    public bool IsBusy { get; internal set; }
    public string? Message { get; internal set; }
    public InboundRequestPageMessageTone MessageTone { get; internal set; } = InboundRequestPageMessageTone.Info;
    public IReadOnlyList<입고요청항목응답> Items { get; internal set; } = [];
    public int TotalCount { get; internal set; }
    public IReadOnlyList<창고요약응답> Warehouses { get; internal set; } = [];
    public 입고요청항목응답? Current { get; internal set; }
    public IReadOnlyList<입고상품항목응답> CompletionItems { get; internal set; } = [];
    public InboundRequestListFilter ListFilter { get; internal set; } = new();
    public bool Created { get; internal set; }
    public Api작업오류? Failure { get; internal set; }
    public long? SavedInboundId { get; internal set; }
    public bool RegistrationResultPending { get; internal set; }
    public long? CompletionResultPendingId { get; internal set; }
    public bool CompletionApplied { get; internal set; }
    public bool RecoveryBlocked { get; internal set; }
    public bool CanQueryRegistrationResult { get; internal set; }
    public bool HasPendingMutation => RecoveryBlocked || RegistrationResultPending || CompletionResultPendingId is > 0;
    public long? RecoveryInboundId => CompletionResultPendingId ?? (RegistrationResultPending ? SavedInboundId : null);
}

public sealed class InboundRequestCreateDraft
{
    public Guid? PhotoRequestId { get; set; }
    public Guid? ApplicationPrivacyConsentEvidenceId { get; set; }
    public string ApplicationSourceCode { get; set; } = string.Empty;
    public long? WarehouseId { get; set; }
    public string FlowType { get; set; } = 입고흐름유형코드.계약기반입고;
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string OrderReference { get; set; } = string.Empty;
    public DateTime? ExpectedArrivalDate { get; set; } = DateTime.Today;
    public string Notes { get; set; } = string.Empty;
    public string ContractNo { get; set; } = string.Empty;
    public string ContractType { get; set; } = 입고계약유형코드.보관대행;
    public string ContractCounterpartyName { get; set; } = string.Empty;
    public string ContractSettlementType { get; set; } = string.Empty;
    public decimal ContractCommissionRate { get; set; }
    public decimal ContractDailyStorageFee { get; set; }
}

public sealed class WarehouseRegistrationDraft
{
    public string Name { get; set; } = string.Empty;
    public string ProxyType { get; set; } = LogisticsProxySiteTypes.DeliveryAgency;
    public string Address { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public sealed class InboundRequestCompletionDraft
{
    public string ItemName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string OptionName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int DefectQuantity { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
}

/// <summary>
/// 입고 목록·신청·상세·완료와 별도 창고 등록 route의 조회·검증·상태 전이를 담당합니다.
/// Route Page는 URL과 navigation만 조립합니다.
/// </summary>
public sealed partial class InboundRequestPageViewModel(IWarehouseWorkspaceService warehouseService,
    IWorkflowRecoveryStore? recoveryStore = null, IWorkflowRecoveryContext? recoveryContext = null,
    Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core.IRoleWorkspaceAccess? access = null) : IDisposable
{
    private readonly 업무선택ContextViewModel _detailSelection = new();
    private bool _detailLoading;
    private bool _mutationInProgress;
    private bool _disposed;
    public InboundRequestPageState State { get; } = new();
    public InboundRequestCreateDraft CreateDraft { get; } = new();
    public WarehouseRegistrationDraft WarehouseDraft { get; } = new();
    public InboundRequestCompletionDraft CompletionDraft { get; } = new();
    public 판매상품항목응답? SelectedSalesProduct { get; private set; }

    public bool CanComplete
        => State.Current is not null
           && !State.HasPendingMutation
           && State.Failure is null
           && !string.Equals(State.Current.상태, 입고상태코드.완료, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(State.Current.상태, 입고상태코드.취소, StringComparison.OrdinalIgnoreCase);

    public async Task LoadListAsync(
        InboundRequestListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (State.IsBusy)
        {
            return;
        }

        State.IsBusy = true;
        State.ListFilter = filter ?? State.ListFilter;
        WorkflowRecoverySnapshot? recovery = null;
        try
        {
            recovery = await EnsureInboundRecoveryAsync(cancellationToken, refresh: true);
            if (recovery is null) return;
            var response = await warehouseService.QueryInboundsAsync(new 입고요청목록조회요청
            {
                Page = 0,
                PageSize = 100,
                Search = State.ListFilter.Search,
                Status = State.ListFilter.Status,
                FlowType = State.ListFilter.FlowType,
                SortBy = nameof(입고요청항목응답.Id),
                SortDescending = true
            }, cancellationToken);
            if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return; }
            State.Items = response?.Items ?? [];
            State.TotalCount = response?.TotalCount ?? 0;
            ClearQueryFailure();
            SetMessage($"입고 요청 {State.TotalCount:N0}건을 조회했습니다.", InboundRequestPageMessageTone.Success);
        }
        catch (Exception ex)
        {
            if (recovery is not null && !RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return; }
            State.Items = [];
            State.TotalCount = 0;
            SetFailure(ex, OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 업무처리결과Codes.조회,
                "입고 목록을 불러오지 못했습니다.");
        }
        finally
        {
            State.IsBusy = false;
        }
    }

    public async Task LoadCreateAsync(
        InboundRequestNavigationContext context,
        CancellationToken cancellationToken = default)
    {
        await RefreshWarehousesAsync(cancellationToken);
        if (await EnsureInboundRecoveryAsync(cancellationToken) is not null)
            ApplyCreateContext(context);
    }

    public async Task RefreshWarehousesAsync(CancellationToken cancellationToken = default)
    {
        if (State.IsBusy)
        {
            return;
        }

        State.IsBusy = true;
        WorkflowRecoverySnapshot? recovery = null;
        try
        {
            recovery = await EnsureInboundRecoveryAsync(cancellationToken, refresh: true);
            if (recovery is null) return;
            State.Warehouses = (await warehouseService.GetWarehousesAsync(cancellationToken))?.Items
                .Where(item => item.IsActive)
                .OrderByDescending(item => item.기본창고여부)
                .ThenBy(item => item.창고명)
                .ToArray() ?? [];
            if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return; }
            if (CreateDraft.WarehouseId is null && State.Warehouses.Count > 0)
            {
                CreateDraft.WarehouseId = State.Warehouses[0].Id;
            }

            ClearQueryFailure();
            SetMessage(
                State.Warehouses.Count == 0
                    ? "사용 가능한 창고가 없습니다. 창고 등록 화면에서 먼저 등록해 주세요."
                    : $"입고 가능한 창고 {State.Warehouses.Count:N0}곳을 조회했습니다.",
                State.Warehouses.Count == 0 ? InboundRequestPageMessageTone.Warning : InboundRequestPageMessageTone.Info);
        }
        catch (Exception ex)
        {
            if (recovery is not null && !RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return; }
            State.Warehouses = [];
            SetFailure(ex, OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 업무처리결과Codes.조회,
                "입고할 창고를 불러오지 못했습니다.");
        }
        finally
        {
            State.IsBusy = false;
        }
    }

    public async Task LoadDetailAsync(
        long inboundId,
        bool created = false,
        CancellationToken cancellationToken = default)
    {
        var targetKey = inboundId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_disposed || _mutationInProgress
            || (State.IsBusy && (!_detailLoading || _detailSelection.대상Key == targetKey)))
        {
            return;
        }

        if (_detailSelection.선택(targetKey) && State.Current?.Id != inboundId)
        {
            State.Current = null;
            State.Failure = null;
        }
        using var scope = _detailSelection.요청시작(cancellationToken);
        _detailLoading = true;
        State.IsBusy = true;
        State.Created = created;
        WorkflowRecoverySnapshot? recovery = null;
        try
        {
            recovery = await EnsureInboundRecoveryAsync(scope.취소Token, refresh: true);
            if (recovery is null || !scope.현재요청) return;
            var current = inboundId > 0
                ? await warehouseService.GetInboundAsync(inboundId, scope.취소Token)
                : null;
            if (_disposed || !scope.현재요청) return;
            if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return; }
            if (current is null)
            {
                if (State.Current?.Id != inboundId) State.Current = null;
                SetFailure(new SsalddelApiException("입고 요청 조회 범위 확인", 404, "입고 조회", string.Empty, null),
                    DetailStage(inboundId), DetailOutcome(inboundId, created),
                    "입고 요청을 확인하지 못했습니다.");
                return;
            }

            if (current.Id != inboundId)
                throw new InvalidOperationException("입고 요청 조회 대상 불일치");
            State.Current = current;
            await ConfirmInboundRecoveryAsync(current, recovery, scope.취소Token);
            if (!RecoveryCurrent(recovery) || !scope.현재요청) return;
            if (State.CompletionResultPendingId == inboundId)
            {
                if (!string.Equals(current.상태, 입고상태코드.완료, StringComparison.OrdinalIgnoreCase))
                {
                    SetFailure(new InvalidOperationException("입고 완료 결과 확인 대기"),
                        OperatingSystemLifecycleStageIds.WarehouseReceiving,
                        State.CompletionApplied ? 업무처리결과Codes.적용 : 업무처리결과Codes.미확정,
                        "입고 완료 결과를 확인하고 있습니다.");
                    return;
                }
                State.CompletionResultPendingId = null;
                State.CompletionApplied = false;
            }
            if (!State.HasPendingMutation) State.Failure = null;
            SetMessage(
                State.Current is null
                    ? "입고 요청을 찾을 수 없거나 현재 계정의 조회 범위에 없습니다."
                    : created
                        ? $"입고 요청 #{inboundId}을 저장하고 같은 ID로 다시 조회했습니다."
                        : $"입고 요청 #{inboundId}을 같은 ID로 다시 조회했습니다.",
                State.Current is null ? InboundRequestPageMessageTone.Warning : InboundRequestPageMessageTone.Success);
        }
        catch (Exception ex)
        {
            if (_disposed || !scope.현재요청) return;
            if (recovery is not null && !RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return; }
            if (State.Current?.Id != inboundId) State.Current = null;
            SetFailure(ex, DetailStage(inboundId), DetailOutcome(inboundId, created),
                "입고 내역을 불러오지 못했습니다.");
        }
        finally
        {
            if (!_disposed && scope.현재요청)
            {
                State.IsBusy = false;
                _detailLoading = false;
            }
        }
    }

    public async Task LoadCompletionAsync(long inboundId, CancellationToken cancellationToken = default)
    {
        await LoadDetailAsync(inboundId, cancellationToken: cancellationToken);
        if (State.Current?.Id == inboundId && string.IsNullOrWhiteSpace(CompletionDraft.ItemName))
        {
            CompletionDraft.ItemName = string.IsNullOrWhiteSpace(State.Current.예정상품명)
                ? State.Current.공급처명
                : State.Current.예정상품명;
            CompletionDraft.Sku = string.IsNullOrWhiteSpace(State.Current.예정SKU)
                ? State.Current.원주문참조번호
                : State.Current.예정SKU;
            CompletionDraft.Quantity = Math.Max(1, State.Current.예정수량 ?? 1);
        }
    }

    public async Task<long?> CreateInboundAsync(CancellationToken cancellationToken = default)
    {
        if (State.IsBusy)
        {
            return null;
        }

        var recovery = await PrepareRecoveryForMutationAsync(cancellationToken);
        if (recovery is null) return null;

        // 저장 응답을 받은 신청은 새 POST가 아니라 같은 원장의 조회로만 이어갑니다.
        if (State.SavedInboundId is > 0)
        {
            await LoadDetailAsync(State.SavedInboundId.Value, created: true, cancellationToken);
            return State.SavedInboundId;
        }
        if (State.RegistrationResultPending)
        {
            return null;
        }

        if (CreateDraft.WarehouseId is not > 0)
        {
            SetMessage("입고할 창고를 먼저 선택해 주세요.", InboundRequestPageMessageTone.Warning);
            return null;
        }

        if (string.IsNullOrWhiteSpace(CreateDraft.SupplierName))
        {
            SetMessage("입고 공급처명을 입력해 주세요.", InboundRequestPageMessageTone.Warning);
            return null;
        }

        var requestedFlowType = 입고흐름유형코드.Normalize(CreateDraft.FlowType);
        if (requestedFlowType != 입고흐름유형코드.계약기반입고)
        {
            SetMessage(
                "이 신청 화면은 계약 기반 입고만 등록합니다. 현장 임시 입고는 안내 동의·멱등 ID가 있는 입고상품 수령 화면에서, 주문 자동 예정은 주문 workflow에서 생성해 주세요.",
                InboundRequestPageMessageTone.Warning);
            return null;
        }

        State.IsBusy = true;
        _mutationInProgress = true;
        State.Failure = null;
        var posted = false;
        try
        {
            if (!await WriteInboundAheadAsync(recovery, false, null, cancellationToken)) return null;
            var flowType = requestedFlowType;
            posted = true;
            var response = await warehouseService.CreateInboundAsync(new 입고요청저장요청
            {
                ClientRequestId = _inboundPending!.RequestId,
                가입고사진신청Id = CreateDraft.PhotoRequestId,
                신청개인정보동의증적Id = CreateDraft.ApplicationPrivacyConsentEvidenceId,
                신청출처Code = CreateDraft.ApplicationSourceCode,
                창고Id = CreateDraft.WarehouseId.Value,
                입고흐름유형 = flowType,
                입고생성경로 = BuildInboundSourceLabel(flowType),
                계약선행여부 = 입고흐름유형코드.RequiresExistingContract(flowType),
                자동생성여부 = 입고흐름유형코드.IsOrderGenerated(flowType),
                공급처코드 = CreateDraft.SupplierCode.Trim(),
                공급처명 = CreateDraft.SupplierName.Trim(),
                원주문참조번호 = CreateDraft.OrderReference.Trim(),
                예정도착일 = CreateDraft.ExpectedArrivalDate,
                비고 = CreateDraft.Notes.Trim(),
                계약정보 = new 입고계약스냅샷
                {
                    계약번호 = CreateDraft.ContractNo,
                    계약유형 = CreateDraft.ContractType,
                    계약상대방명 = string.IsNullOrWhiteSpace(CreateDraft.ContractCounterpartyName)
                        ? CreateDraft.SupplierName
                        : CreateDraft.ContractCounterpartyName,
                    정산방식 = CreateDraft.ContractSettlementType,
                    판매수수료율 = CreateDraft.ContractCommissionRate,
                    보관료일단가 = CreateDraft.ContractDailyStorageFee,
                    통관필요여부 = 입고계약유형코드.RequiresCustoms(CreateDraft.ContractType),
                    계약시작일 = DateTime.Today,
                    계약메모 = CreateDraft.Notes
                }.Normalize()
            }, cancellationToken) ?? throw new InvalidOperationException("입고 요청 생성 응답이 비어 있습니다.");
            RequireRecoveryCurrent(recovery);
            if (response.Id <= 0)
                throw new InvalidOperationException("생성한 입고 요청 ID를 확인하지 못했습니다.");
            if (CreateDraft.PhotoRequestId is { } photoRequestId && response.가입고사진신청Id != photoRequestId)
                throw new InvalidOperationException("사진 신청의 입고 연결을 확인하지 못했습니다.");

            // POST의 확정 응답과 후속 GET은 서로 다른 결과입니다.
            State.SavedInboundId = response.Id;
            State.Current = response;
            State.Created = true;
            try
            {
                await PersistInboundOutcomeAsync(recovery, false, response.Id, 업무처리결과Codes.적용, cancellationToken);
                var current = await warehouseService.GetInboundAsync(response.Id, cancellationToken)
                    ?? throw new InvalidOperationException("저장한 입고 요청 조회 대기");
                RequireRecoveryCurrent(recovery);
                if (current.Id != response.Id)
                    throw new InvalidOperationException("저장한 입고 요청 조회 대상 불일치");
                State.Current = current;
                await ConfirmInboundRecoveryAsync(current, recovery, cancellationToken);
                State.Failure = null;
                SetMessage($"입고 요청 #{response.Id}을 저장하고 같은 ID로 다시 조회했습니다.",
                    InboundRequestPageMessageTone.Success);
            }
            catch (Exception ex)
            {
                if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return null; }
                SetFailure(ex, OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 업무처리결과Codes.적용,
                    "입고는 등록됐습니다. 내역을 확인해 주세요.");
            }
            return response.Id;
        }
        catch (Exception ex)
        {
            if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return null; }
            if (!posted)
            {
                State.RecoveryBlocked = true; _inboundRecoveryLoaded = false;
                SetFailure(ex, OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 업무처리결과Codes.조회,
                    "이전 신청을 확인하지 못했습니다. 다시 확인해 주세요.");
                return null;
            }
            var outcome = MutationOutcome(ex, OperatingSystemLifecycleStageIds.WarehouseInboundPlan);
            try
            {
                await PersistInboundOutcomeAsync(recovery, false, null, outcome, cancellationToken);
                if (outcome == 업무처리결과Codes.미적용)
                    await RemoveInboundRecoveryAsync(recovery, _inboundPending!, cancellationToken);
            }
            catch
            {
                if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return null; }
                State.RecoveryBlocked = true; _inboundRecoveryLoaded = false;
            }
            State.RegistrationResultPending = outcome != 업무처리결과Codes.미적용;
            State.Created = outcome == 업무처리결과Codes.적용;
            SetFailure(ex, OperatingSystemLifecycleStageIds.WarehouseInboundPlan,
                outcome, outcome == 업무처리결과Codes.적용
                    ? "입고는 등록됐습니다. 목록에서 내역을 확인해 주세요."
                    : State.RegistrationResultPending ? "입고 접수 결과를 확인해 주세요. 다시 등록하지 마세요." : "입고 신청 내용을 확인해 주세요.");
            return null;
        }
        finally
        {
            State.IsBusy = false;
            _mutationInProgress = false;
        }
    }

    public async Task<long?> CreateWarehouseAsync(CancellationToken cancellationToken = default)
    {
        if (State.IsBusy)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(WarehouseDraft.Name) || string.IsNullOrWhiteSpace(WarehouseDraft.Address))
        {
            SetMessage("창고명과 주소를 입력해 주세요.", InboundRequestPageMessageTone.Warning);
            return null;
        }

        State.IsBusy = true;
        _mutationInProgress = true;
        try
        {
            var response = await warehouseService.CreateWarehouseAsync(new 창고저장요청
            {
                창고명 = WarehouseDraft.Name.Trim(),
                물류대행지분류 = LogisticsProxySiteTypes.Normalize(WarehouseDraft.ProxyType),
                주소 = WarehouseDraft.Address.Trim(),
                담당자명 = WarehouseDraft.ManagerName.Trim(),
                연락처 = WarehouseDraft.Phone.Trim()
            }, cancellationToken) ?? throw new InvalidOperationException("창고 생성 응답이 비어 있습니다.");
            SetMessage($"창고 #{response.Id}을 등록했습니다. 입고 신청서로 돌아갑니다.", InboundRequestPageMessageTone.Success);
            return response.Id;
        }
        catch (Exception ex)
        {
            SetMessage($"창고를 등록하지 못했습니다. {ex.Message}", InboundRequestPageMessageTone.Error);
            return null;
        }
        finally
        {
            State.IsBusy = false;
            _mutationInProgress = false;
        }
    }

    public async Task<bool> CompleteInboundAsync(long inboundId, CancellationToken cancellationToken = default)
    {
        if (State.IsBusy || State.Current?.Id != inboundId)
        {
            return false;
        }
        var recovery = await PrepareRecoveryForMutationAsync(cancellationToken);
        if (recovery is null || !CanComplete) return false;

        if (string.IsNullOrWhiteSpace(CompletionDraft.ItemName)
            || CompletionDraft.Quantity <= 0
            || CompletionDraft.DefectQuantity < 0
            || CompletionDraft.DefectQuantity > CompletionDraft.Quantity)
        {
            SetMessage("상품명, 1개 이상의 입고 수량과 그 이하의 불량 수량을 확인해 주세요.", InboundRequestPageMessageTone.Warning);
            return false;
        }

        State.IsBusy = true;
        _mutationInProgress = true;
        State.Failure = null;
        var posted = false;
        try
        {
            if (!await WriteInboundAheadAsync(recovery, true, inboundId, cancellationToken)) return false;
            posted = true;
            var result = await warehouseService.CompleteInboundAsync(inboundId, new 입고완료요청
            {
                Items =
                [
                    new 입고상품저장요청
                    {
                        상품명 = CompletionDraft.ItemName.Trim(),
                        SKU = CompletionDraft.Sku.Trim(),
                        옵션명 = CompletionDraft.OptionName.Trim(),
                        입고수량 = CompletionDraft.Quantity,
                        불량수량 = CompletionDraft.DefectQuantity,
                        보관위치 = CompletionDraft.StorageLocation.Trim()
                    }
                ]
            }, cancellationToken) ?? throw new InvalidOperationException("입고 완료 응답이 비어 있습니다.");
            RequireRecoveryCurrent(recovery);
            State.CompletionItems = result.Items;
            State.CompletionResultPendingId = inboundId;
            State.CompletionApplied = true;
            try
            {
                await PersistInboundOutcomeAsync(recovery, true, inboundId, 업무처리결과Codes.적용, cancellationToken);
                var current = await warehouseService.GetInboundAsync(inboundId, cancellationToken)
                    ?? throw new InvalidOperationException("완료한 입고 요청 조회 대기");
                RequireRecoveryCurrent(recovery);
                if (current.Id != inboundId)
                    throw new InvalidOperationException("완료한 입고 요청 조회 대상 불일치");
                State.Current = current;
                if (!string.Equals(current.상태, 입고상태코드.완료, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("완료한 입고 요청 상태 반영 대기");
                await ConfirmInboundRecoveryAsync(current, recovery, cancellationToken);
                State.CompletionResultPendingId = null;
                State.CompletionApplied = false;
                State.Failure = null;
                SetMessage($"입고 요청 #{inboundId}을 완료하고 같은 ID로 다시 조회했습니다.",
                    InboundRequestPageMessageTone.Success);
            }
            catch (Exception ex)
            {
                if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return false; }
                SetFailure(ex, OperatingSystemLifecycleStageIds.WarehouseReceiving, 업무처리결과Codes.적용,
                    "입고 완료는 처리됐습니다. 내역을 확인해 주세요.");
            }
            return true;
        }
        catch (Exception ex)
        {
            if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return false; }
            if (!posted)
            {
                State.RecoveryBlocked = true; _inboundRecoveryLoaded = false;
                SetFailure(ex, OperatingSystemLifecycleStageIds.WarehouseReceiving, 업무처리결과Codes.조회,
                    "이전 입고를 확인하지 못했습니다. 다시 확인해 주세요.");
                return false;
            }
            var outcome = MutationOutcome(ex, OperatingSystemLifecycleStageIds.WarehouseReceiving);
            try
            {
                await PersistInboundOutcomeAsync(recovery, true, inboundId, outcome, cancellationToken);
                if (outcome == 업무처리결과Codes.미적용)
                    await RemoveInboundRecoveryAsync(recovery, _inboundPending!, cancellationToken);
            }
            catch
            {
                if (!RecoveryCurrent(recovery)) { DiscardForeignRecovery(recovery); return false; }
                State.RecoveryBlocked = true; _inboundRecoveryLoaded = false;
            }
            var rejected = outcome == 업무처리결과Codes.미적용;
            State.CompletionResultPendingId = rejected ? null : inboundId;
            State.CompletionApplied = outcome == 업무처리결과Codes.적용;
            SetFailure(ex, OperatingSystemLifecycleStageIds.WarehouseReceiving,
                outcome, outcome == 업무처리결과Codes.적용
                    ? "입고 완료는 처리됐습니다. 내역을 확인해 주세요."
                    : rejected ? "입고 완료 조건을 확인해 주세요." : "입고 완료 결과를 확인해 주세요. 다시 처리하지 마세요.");
            return false;
        }
        finally
        {
            State.IsBusy = false;
            _mutationInProgress = false;
        }
    }

    public void SelectSalesProduct(판매상품항목응답? product)
    {
        SelectedSalesProduct = product;
        if (product is not null)
        {
            CompletionDraft.ItemName = product.대표상품명;
            CompletionDraft.Sku = product.판매SKU;
        }
    }

    public void ApplyWarehouseContext(InboundRequestNavigationContext context)
    {
        WarehouseDraft.Name = context.WarehouseName ?? string.Empty;
        WarehouseDraft.ProxyType = LogisticsProxySiteTypes.Normalize(context.ProxyType);
        WarehouseDraft.Address = context.WarehouseAddress ?? string.Empty;
    }

    private void ApplyCreateContext(InboundRequestNavigationContext context)
    {
        var fromDiagram = string.Equals(context.Source, "diagram-warehouse-proxy", StringComparison.OrdinalIgnoreCase);
        if (context.WarehouseId is > 0 && State.Warehouses.Any(item => item.Id == context.WarehouseId.Value))
        {
            CreateDraft.WarehouseId = context.WarehouseId;
        }
        else if (fromDiagram)
        {
            CreateDraft.WarehouseId = null;
        }

        CreateDraft.SupplierCode = context.SupplierCode ?? CreateDraft.SupplierCode;
        CreateDraft.SupplierName = context.SupplierName
                                   ?? (fromDiagram
                                       ? "다이어그램 물류 대행 신청"
                                       : CreateDraft.SupplierName);
        CreateDraft.OrderReference = context.OrderReference
                                     ?? (fromDiagram
                                         ? $"DIAGRAM-{DateTime.Today:yyyyMMdd}"
                                         : CreateDraft.OrderReference);
        CreateDraft.ExpectedArrivalDate = context.ExpectedArrivalDate ?? CreateDraft.ExpectedArrivalDate;
        CreateDraft.Notes = context.Notes ?? BuildDiagramMemo(context) ?? CreateDraft.Notes;
        CreateDraft.ContractNo = context.ContractNo ?? CreateDraft.ContractNo;
        CreateDraft.ContractType = string.IsNullOrWhiteSpace(context.ContractType)
            ? ResolveContractType(context.ProxyType)
            : 입고계약유형코드.Normalize(context.ContractType);
        CreateDraft.ContractCounterpartyName = context.ContractCounterpartyName
                                               ?? context.WarehouseName
                                               ?? CreateDraft.ContractCounterpartyName;
        CreateDraft.ContractSettlementType = context.ContractSettlementType ?? CreateDraft.ContractSettlementType;
        CreateDraft.ContractCommissionRate = context.ContractCommissionRate ?? CreateDraft.ContractCommissionRate;
        CreateDraft.ContractDailyStorageFee = context.ContractDailyStorageFee ?? CreateDraft.ContractDailyStorageFee;

        if (fromDiagram)
        {
            SetMessage(
                CreateDraft.WarehouseId is > 0
                    ? "다이어그램 창고 후보와 신청 초안을 불러왔습니다. 검토한 뒤 명시적으로 등록해 주세요."
                    : "다이어그램 후보에 실제 창고 ID가 없습니다. 창고를 등록한 뒤 신청서를 계속 작성해 주세요.",
                CreateDraft.WarehouseId is > 0 ? InboundRequestPageMessageTone.Info : InboundRequestPageMessageTone.Warning);
        }
    }

    private static string BuildInboundSourceLabel(string flowType)
        => flowType switch
        {
            입고흐름유형코드.현장임시입고 => "창고 관리자 수기 등록",
            입고흐름유형코드.주문자동입고예정 => "주문/구매 흐름 자동 생성",
            _ => "계약 DB 기반 등록"
        };

    private static string ResolveContractType(string? proxyType)
        => LogisticsProxySiteTypes.Normalize(proxyType) switch
        {
            LogisticsProxySiteTypes.MarketFulfillment => 입고계약유형코드.마켓풀필먼트,
            LogisticsProxySiteTypes.OverseasCustomsAgency => 입고계약유형코드.수입통관풀필먼트,
            _ => 입고계약유형코드.보관대행
        };

    private static string? BuildDiagramMemo(InboundRequestNavigationContext context)
    {
        if (!string.Equals(context.Source, "diagram-warehouse-proxy", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return string.Join(Environment.NewLine, new[]
        {
            "다이어그램 창고 블록에서 시작한 판매자 물류 대행 신청입니다.",
            $"- 창고 후보: {context.WarehouseName}",
            $"- 후보 범위: {context.Scope}",
            $"- 다이어그램 노드: {context.NodeTitle}",
            $"- 블록 그룹: {context.NodeGroup}",
            $"- 노드 설명: {context.NodeDescription}"
        });
    }

    private void SetMessage(string message, InboundRequestPageMessageTone tone)
    {
        State.Message = message;
        State.MessageTone = tone;
    }

    private string DetailStage(long inboundId)
        => State.CompletionResultPendingId == inboundId
            ? OperatingSystemLifecycleStageIds.WarehouseReceiving
            : OperatingSystemLifecycleStageIds.WarehouseInboundPlan;

    private string DetailOutcome(long inboundId, bool created)
        => State.CompletionResultPendingId == inboundId
            ? State.CompletionApplied ? 업무처리결과Codes.적용 : 업무처리결과Codes.미확정
            : created || State.SavedInboundId == inboundId ? 업무처리결과Codes.적용 : 업무처리결과Codes.조회;

    private void ClearQueryFailure()
    {
        if (State.Failure?.WorkflowContext?.OutcomeCode == 업무처리결과Codes.조회)
            State.Failure = null;
    }

    private void SetFailure(Exception exception, string stage, string outcome, string message)
    {
        // 창고 목록의 후속 조회가 접수 결과 미확정 경고를 덮지 않습니다.
        if (State.RegistrationResultPending && outcome == 업무처리결과Codes.조회 && State.Failure is not null)
            return;
        State.Failure = Api작업오류.변환(exception) with
        {
            WorkflowContext = new 업무실패맥락Dto(OperatingSystemIds.WarehouseCommerceFulfillment, stage, outcome),
            재시도정책Code = 업무재시도정책Codes.상태재조회후사용자재시도,
            복구가능행동 = [업무복구행동Ids.상태전체재조회]
        };
        SetMessage(message, InboundRequestPageMessageTone.Warning);
    }

    private static string MutationOutcome(Exception exception, string stage)
    {
        if (exception is not SsalddelApiException api) return 업무처리결과Codes.미확정;
        var context = api.WorkflowContext;
        if (context?.유효한가() == true
            && OperatingSystemIds.TryNormalize(context.OperatingSystemId, out var operatingSystem)
            && operatingSystem == OperatingSystemIds.WarehouseCommerceFulfillment
            && context.StageId == stage
            && context.OutcomeCode is 업무처리결과Codes.미적용 or 업무처리결과Codes.적용 or 업무처리결과Codes.미확정)
            return context.OutcomeCode;

        // 입고 저장 후 로그/Event 예외도 HTTP 400으로 응답할 수 있어 숫자로 미적용을 추측하지 않습니다.
        return 업무처리결과Codes.미확정;
    }

    public void Dispose()
    {
        _disposed = true;
        _detailSelection.Dispose();
    }
}
