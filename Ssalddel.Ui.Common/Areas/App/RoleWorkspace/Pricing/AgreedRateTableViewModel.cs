using System.Globalization;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Pricing;

/// <summary>합의 관리의 계정 수명·입력·동일 요청 재확인을 소유합니다. 상태 전이와 계산은 서버에 맡깁니다.</summary>
public sealed class AgreedRateTableViewModel : IDisposable
{
    private readonly AgreedRateTableClient client;
    private readonly IRoleWorkspaceAccess access;
    private CancellationTokenSource lifetime = new();
    private RoleWorkspaceIdentity? owner;
    private long generation;
    private bool disposed;
    private PendingCommand? pending;
    public AgreedRateTableViewModel(IRoleWorkspaceApi api, IRoleWorkspaceAccess access, string roleKey)
    {
        client = new(api, roleKey); this.access = access;
        access.Changed += AuthenticationChanged;
    }
    public event Action? Changed;
    public string RoleKey => client.RoleKey;
    public bool IsFood => client.BusinessKind == AgreedRateBusinessKinds.FoodDelivery;
    public bool IsBusy { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool RequiresLogin { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public IReadOnlyList<AgreedRateTableDto> Items { get; private set; } = [];
    public AgreedRateTableDto? Selected { get; private set; }
    public AgreedRateTablePreviewDto? Preview { get; private set; }
    public AgreedRateTableSelectionDto? Selection { get; private set; }
    public AgreedRateProposalDraft Draft { get; private set; } = new();
    public string? WorkId { get; private set; }
    public decimal? DistanceKm { get; set; }
    public string DistanceBasisCode { get; set; } = "";
    public decimal? WaitingFeeKrw { get; set; }
    public decimal? ManualWorkFeeKrw { get; set; }
    public decimal? SurchargeKrw { get; set; }
    public bool IncludeCostSharing { get; set; }
    public string CostSharingMode { get; set; } = "";
    public decimal? FixedAmountKrw { get; set; }
    public decimal? RestaurantSharePercent { get; set; }
    public decimal? MaximumRestaurantChargeKrw { get; set; }
    public decimal? MaxUserChargeKrw { get; set; }
    public bool Confirmed { get; set; }
    public bool ProposalConfirmed { get; set; }
    public bool SelectionConfirmed { get; set; }
    public bool HasPendingCommand => pending is not null;
    public Guid? RequestId => pending?.RequestId;
    public bool CanEdit => !IsBusy && !RequiresLogin && !HasPendingCommand;
    public bool CanAct(string action) => CanEdit && Error is null && Selected is { } table && (action switch
    {
        "Accept" => table.CanAccept,
        "Reject" => table.CanReject,
        "Withdraw" => table.CanWithdraw,
        _ => false
    });
    public bool CanPreview => CanEdit && Error is null && Selected?.CanPreview == true;
    public bool CanSelect => CanEdit && Error is null && Selected?.CanSelect == true && !string.IsNullOrWhiteSpace(WorkId) && Selection is null;
    public bool CanRetrySynchronization => CanEdit && Error is null && Selection is { CanRetrySynchronization: true, StatusCode: "LedgerSyncPending" }
        && Selection.SelectionRequestId != Guid.Empty && Selected is not null;
    public IReadOnlyList<AgreedRateTableDto> Sent => Items.Where(value => value.Proposer == value.CurrentParty).ToArray();
    public IReadOnlyList<AgreedRateTableDto> Received => Items.Where(value => value.Counterparty == value.CurrentParty).ToArray();

    public void SetWork(string? workId)
    {
        var clean = RoleWorkspaceNavigation.StableId(workId);
        if (WorkId == clean) return;
        Invalidate(); Clear(); WorkId = clean; owner = null;
    }
    public Task LoadAsync(string? selectedId = null) => RunAsync(async ct =>
    {
        if (pending is not null) throw new InvalidOperationException("같은 요청의 결과를 먼저 확인해 주세요.");
        Preview = null; Confirmed = ProposalConfirmed = SelectionConfirmed = false;
        await RefreshAsync(selectedId ?? Selected?.AgreementId, ct);
    });
    public Task ChooseAsync(string id)
    {
        if (HasPendingCommand || IsBusy) return Task.CompletedTask;
        Confirmed = SelectionConfirmed = false; Preview = null; Selection = null;
        return RunAsync(ct => RefreshAsync(id, ct));
    }
    private async Task RefreshAsync(string? selectedId, CancellationToken ct, bool readSelection = true)
    {
        var values = await client.ListAsync(ct); RequireCurrent(ct);
        foreach (var value in values) Validate(value);
        AgreedRateTableDto? selected = null;
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            selected = await client.DetailAsync(selectedId, ct); RequireCurrent(ct); Validate(selected, selectedId);
        }
        AgreedRateTableSelectionDto? selection = null;
        if (readSelection && selected is not null && WorkId is { } work)
        {
            try
            {
                selection = await client.SelectionAsync(selected.AgreementId, work, ct); RequireCurrent(ct);
                ValidateSelection(selection, selected.AgreementId, work);
                if (selection.Agreement.Revision > selected.Revision || !MatchesSnapshot(selection.Agreement, selected, selection.Agreement.Revision))
                    throw new InvalidOperationException("업무에 동결된 요금표의 당사자와 단가를 확인하지 못했습니다.");
            }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 404) { RequireCurrent(ct); }
        }
        Items = values; Selected = selected; IsLoaded = true;
        if (readSelection) Selection = selection;
    }
    private void Validate(AgreedRateTableDto value, string? expectedId = null)
    {
        if (string.IsNullOrWhiteSpace(value.AgreementId) || expectedId is not null && value.AgreementId != expectedId
            || value.BusinessKind != client.BusinessKind || value.CurrentParty.PartyKind != client.PartyKind
            || string.IsNullOrWhiteSpace(value.CurrentParty.PartyId)
            || value.CurrentParty != value.Proposer && value.CurrentParty != value.Counterparty
            || IsFood && (value.FoodPricing is null || value.CargoPricing is not null)
            || !IsFood && (value.CargoPricing is null || value.FoodPricing is not null))
            throw new InvalidOperationException("조회한 요금표의 당사자와 대상을 확인하지 못했습니다.");
    }
    public Task ProposeAsync()
    {
        if (!CanEdit || !ProposalConfirmed) return Task.CompletedTask;
        return RunAsync(async ct =>
        {
            var body = Draft.Build(client.PartyKind, client.CounterpartyKind, client.BusinessKind);
            pending = new("Propose", body.RequestId, null, body, null, null);
            await SendPendingAsync(ct);
        });
    }
    public Task ActAsync(string action)
    {
        if (!CanAct(action) || !Confirmed) return Task.CompletedTask;
        var body = new AgreedRateTableActionRequest
        { ActorPartyKind = client.PartyKind, ActionCode = action, ExpectedRevision = Selected!.Revision, RequestId = Guid.NewGuid() };
        pending = new(action, body.RequestId, Selected.AgreementId, null, body, null);
        pending.ExpectedTable = CopyTerms(Selected);
        return RunAsync(SendPendingAsync);
    }
    public Task SelectAsync()
    {
        if (!CanSelect || !SelectionConfirmed) return Task.CompletedTask;
        var body = new AgreedRateTableSelectionRequest
        { ActorPartyKind = client.PartyKind, ExpectedRevision = Selected!.Revision, WorkId = WorkId!, RequestId = Guid.NewGuid() };
        pending = new("Select", body.RequestId, Selected.AgreementId, null, null, body);
        pending.ExpectedTable = CopyTerms(Selected);
        return RunAsync(SendPendingAsync);
    }
    public Task RetryPendingAsync() => pending is null ? Task.CompletedTask : RunAsync(SendPendingAsync);
    public Task RetrySynchronizationAsync()
    {
        if (!CanRetrySynchronization) return Task.CompletedTask;
        var selected = Selection!;
        var body = new AgreedRateTableSelectionRequest
        {
            ActorPartyKind = client.PartyKind, ExpectedRevision = selected.Agreement.Revision,
            WorkId = selected.WorkId, RequestId = selected.SelectionRequestId
        };
        pending = new("Select", body.RequestId, selected.Agreement.AgreementId, null, null, body)
        { ExpectedTable = CopyTerms(Selected!) };
        return RunAsync(SendPendingAsync);
    }
    private async Task SendPendingAsync(CancellationToken ct)
    {
        var command = pending ?? throw new InvalidOperationException("확인할 요청이 없습니다.");
        if (!command.Acknowledged)
        {
            if (command.Proposal is { } proposal)
            {
                var response = await client.ProposeAsync(proposal, ct); RequireCurrent(ct);
                if (response is null) throw new InvalidOperationException("제안 응답을 확인하지 못했습니다.");
                Validate(response);
                if (response.StatusCode != "Proposed" || response.Revision != 1 || response.Proposer.PartyKind != proposal.ActorPartyKind
                    || response.CurrentParty != response.Proposer || response.Counterparty.PartyKind != proposal.CounterpartyPartyKind
                    || response.Counterparty.PartyId != proposal.CounterpartyPartyId || response.VehicleCode != proposal.VehicleCode
                    || response.FoodPricing != proposal.FoodPricing || response.CargoPricing != proposal.CargoPricing
                    || response.EffectiveFromUtc != proposal.EffectiveFromUtc || response.EffectiveUntilUtc != proposal.EffectiveUntilUtc)
                    throw new InvalidOperationException("제안한 당사자·단가·기간과 저장 응답이 일치하지 않습니다.");
                command.AgreementId = response.AgreementId; command.AcknowledgedRevision = response.Revision;
            }
            else if (command.Action is { } action)
            {
                var response = await client.ActAsync(command.AgreementId!, action, ct); RequireCurrent(ct);
                if (response is null) throw new InvalidOperationException("처리 응답을 확인하지 못했습니다.");
                Validate(response, command.AgreementId);
                var expectedStatus = action.ActionCode switch { "Accept" => "Accepted", "Reject" => "Rejected", _ => "Withdrawn" };
                if (response.Revision != action.ExpectedRevision + 1 || response.StatusCode != expectedStatus
                    || !SameTerms(response, command.ExpectedTable!))
                    throw new InvalidOperationException("요청한 행동과 처리 판본을 확인하지 못했습니다.");
                command.AcknowledgedRevision = response.Revision;
            }
            else
            {
                var response = await client.SelectAsync(command.AgreementId!, command.Selection!, ct); RequireCurrent(ct);
                if (response is null || response.WorkId != command.Selection!.WorkId || response.BusinessKind != client.BusinessKind
                    || response.SelectionRequestId != command.Selection.RequestId
                    || !MatchesSnapshot(response.Agreement, command.ExpectedTable!, command.Selection.ExpectedRevision))
                    throw new InvalidOperationException("업무의 요금표 선택 응답을 확인하지 못했습니다.");
                ValidateSelection(response, command.AgreementId!, command.Selection.WorkId);
                command.SelectionResult = response;
            }
            command.Acknowledged = true;
        }
        await RefreshAsync(command.AgreementId, ct, false); RequireCurrent(ct);
        if (command.AcknowledgedRevision is { } acknowledgedRevision && Selected!.Revision < acknowledgedRevision)
            throw new InvalidOperationException("처리 후 요금표의 최신 판본을 확인하지 못했습니다.");
        if (command.SelectionResult is { } selectedResult)
        {
            var reread = await client.SelectionAsync(command.AgreementId!, command.Selection!.WorkId, ct); RequireCurrent(ct);
            ValidateSelection(reread, command.AgreementId!, command.Selection.WorkId);
            if (reread.BusinessKind != selectedResult.BusinessKind || reread.WorkId != selectedResult.WorkId
                || reread.Agreement != selectedResult.Agreement || reread.AgreedAmountKrw != selectedResult.AgreedAmountKrw
                || reread.DistanceKm != selectedResult.DistanceKm || reread.DistanceBasisCode != selectedResult.DistanceBasisCode
                || reread.SelectionRequestId != selectedResult.SelectionRequestId)
                throw new InvalidOperationException("같은 업무와 동결 판본의 선택 결과를 다시 확인하지 못했습니다.");
            command.SelectionResult = reread;
        }
        Selection = command.SelectionResult;
        Message = command.Kind == "Select" ? SelectionStatus(Selection!.StatusCode)
            : "처리 후 최신 요금표를 다시 확인했습니다.";
        pending = null; Confirmed = ProposalConfirmed = SelectionConfirmed = false; Preview = null;
        if (command.Kind == "Propose") Draft = new();
    }
    public Task PreviewAsync()
    {
        if (!CanPreview) return Task.CompletedTask;
        Preview = null;
        return RunAsync(async ct =>
        {
            if (DistanceKm is null or < 0 || string.IsNullOrWhiteSpace(DistanceBasisCode))
                throw new InvalidOperationException("거리와 거리 기준을 입력해 주세요.");
            if (!IsFood && (WaitingFeeKrw is null or < 0 || ManualWorkFeeKrw is null or < 0 || SurchargeKrw is null or < 0))
                throw new InvalidOperationException("대기료·수작업료·할증을 입력해 주세요. 없으면 0을 입력해 주세요.");
            DeliveryCostSharingPolicy? policy = IncludeCostSharing && IsFood ? new()
            {
                Mode = CostSharingMode, FixedAmountKrw = CostSharingMode == DeliveryCostSharingModes.FixedContribution ? FixedAmountKrw : null,
                RestaurantSharePercent = CostSharingMode == DeliveryCostSharingModes.PercentageShare ? RestaurantSharePercent : null,
                MaximumRestaurantChargeKrw = CostSharingMode is DeliveryCostSharingModes.PercentageShare or DeliveryCostSharingModes.RestaurantPays
                    ? MaximumRestaurantChargeKrw : null
            } : null;
            if (policy is not null && DeliveryCostSharingCalculator.ValidationError(policy) is not null)
                throw new InvalidOperationException("비용 분담 조건을 확인해 주세요.");
            var id = Selected!.AgreementId; var revision = Selected.Revision;
            var result = await client.PreviewAsync(id, new()
            {
                ActorPartyKind = client.PartyKind, ExpectedRevision = revision, DistanceKm = DistanceKm.Value,
                DistanceBasisCode = DistanceBasisCode.Trim(), WaitingFeeKrw = WaitingFeeKrw ?? 0,
                ManualWorkFeeKrw = ManualWorkFeeKrw ?? 0, SurchargeKrw = SurchargeKrw ?? 0,
                CostSharingPolicy = policy, MaxUserChargeKrw = policy is not null ? MaxUserChargeKrw : null
            }, ct);
            RequireCurrent(ct);
            if (result is null || result.StatusCode != "PreviewOnly" || result.Agreement.AgreementId != id
                || !MatchesSnapshot(result.Agreement, Selected, revision))
                throw new InvalidOperationException("같은 요금표 판본의 계산 결과를 확인하지 못했습니다.");
            Preview = result;
        });
    }
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        if (disposed || IsBusy) return;
        var epoch = generation; var ct = lifetime.Token;
        IsBusy = true; Error = Message = null; Changed?.Invoke();
        try
        {
            await access.EnsureInitializedAsync(RoleKey, ct);
            if (disposed || epoch != generation) return;
            var identity = access.GetIdentity(RoleKey);
            if (!identity.IsAuthenticated || string.IsNullOrWhiteSpace(identity.OwnerId))
            { Clear(); RequiresLogin = true; return; }
            if (owner is not null && owner != identity) Clear();
            owner = identity; RequiresLogin = false;
            await work(ct); RequireCurrent(ct);
        }
        catch (OperationCanceledException) { }
        catch (RoleWorkspaceAccessException ex) when (epoch == generation && !disposed)
        {
            if (ex.StatusCode is 401 or 403) { Clear(); RequiresLogin = ex.StatusCode == 401; }
            else if (ex.StatusCode is 400 or 404 or 409 or 410 or 422 && pending?.Acknowledged != true)
            { pending = null; Confirmed = SelectionConfirmed = false; }
            Error = ex.ErrorCode switch
            {
                "RateTableWorkAlreadyFrozen" => "이미 배차나 금액이 확정된 업무에는 요금표를 바꿀 수 없습니다. 기존 금액과 근거를 확인해 주세요.",
                "RateTableRequestContentConflict" => "같은 요청 번호의 내용이 일치하지 않습니다. 원래 요청과 결과를 다시 확인해 주세요.",
                _ => ex.StatusCode switch
            {
                401 => "로그인이 만료되었습니다. 다시 로그인해 주세요.",
                403 => "현재 계정으로 이 요금표를 처리할 수 없습니다.",
                409 or 410 => "요금표 판본이나 적용 상태가 바뀌었습니다. 다시 조회하고 확인해 주세요.",
                400 or 422 => "요금표 조건 또는 업무 선택을 확인해 주세요.",
                404 => "요금표 또는 대상 업무를 찾지 못했습니다. 다시 조회해 주세요.",
                _ => "처리 결과를 확인하지 못했습니다. 같은 요청으로 다시 확인해 주세요."
                }
            };
        }
        catch (InvalidOperationException ex) when (epoch == generation && !disposed)
        { Error = ex.Message; }
        catch (Exception) when (epoch == generation && !disposed)
        { Error = "처리 결과를 확인하지 못했습니다. 연결을 확인한 뒤 다시 시도해 주세요."; }
        catch (Exception) { }
        finally { if (!disposed && epoch == generation) { IsBusy = false; Changed?.Invoke(); } }
    }
    private void RequireCurrent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (disposed || owner is null || access.GetIdentity(RoleKey) != owner) throw new OperationCanceledException(ct);
    }
    private static bool MatchesSnapshot(AgreedRateTableSnapshotDto snapshot, AgreedRateTableDto table, long revision)
        => snapshot.AgreementId == table.AgreementId && snapshot.Revision == revision && snapshot.BusinessKind == table.BusinessKind
            && snapshot.Proposer == table.Proposer && snapshot.Counterparty == table.Counterparty && snapshot.VehicleCode == table.VehicleCode
            && snapshot.FoodPricing == table.FoodPricing && snapshot.CargoPricing == table.CargoPricing
            && snapshot.EffectiveFromUtc == table.EffectiveFromUtc && snapshot.EffectiveUntilUtc == table.EffectiveUntilUtc
            && table.AcceptedAtUtc == snapshot.AcceptedAtUtc && !string.IsNullOrWhiteSpace(snapshot.TermsDigest);
    private static bool SameTerms(AgreedRateTableDto left, AgreedRateTableDto right)
        => left.AgreementId == right.AgreementId && left.BusinessKind == right.BusinessKind && left.VehicleCode == right.VehicleCode
            && left.Proposer == right.Proposer && left.Counterparty == right.Counterparty
            && left.FoodPricing == right.FoodPricing && left.CargoPricing == right.CargoPricing
            && left.EffectiveFromUtc == right.EffectiveFromUtc && left.EffectiveUntilUtc == right.EffectiveUntilUtc;
    private static AgreedRateTableDto CopyTerms(AgreedRateTableDto value) => new()
    {
        AgreementId = value.AgreementId, BusinessKind = value.BusinessKind, VehicleCode = value.VehicleCode,
        Proposer = value.Proposer, Counterparty = value.Counterparty, FoodPricing = value.FoodPricing, CargoPricing = value.CargoPricing,
        EffectiveFromUtc = value.EffectiveFromUtc, EffectiveUntilUtc = value.EffectiveUntilUtc, AcceptedAtUtc = value.AcceptedAtUtc,
        Revision = value.Revision, StatusCode = value.StatusCode
    };
    private void ValidateSelection(AgreedRateTableSelectionDto value, string agreementId, string workId)
    {
        if (value.BusinessKind != client.BusinessKind || value.WorkId != workId || value.Agreement.AgreementId != agreementId
            || value.Agreement.BusinessKind != client.BusinessKind || string.IsNullOrWhiteSpace(value.Agreement.TermsDigest)
            || value.SelectionRequestId == Guid.Empty
            || IsFood && value.StatusCode != "SelectedForNextOffer"
            || !IsFood && value.StatusCode is not ("Applied" or "LedgerSyncPending"))
            throw new InvalidOperationException("업무 선택의 판본과 처리 상태를 확인하지 못했습니다.");
    }
    private void AuthenticationChanged()
    {
        if (disposed || owner is null || access.GetIdentity(RoleKey) == owner) return;
        Invalidate(); Clear(); owner = null; RequiresLogin = !access.GetIdentity(RoleKey).IsAuthenticated;
        Message = "계정이 변경되었습니다. 요금표를 다시 조회해 주세요."; Changed?.Invoke();
    }
    private void Invalidate() { ++generation; lifetime.Cancel(); lifetime.Dispose(); lifetime = new(); IsBusy = false; }
    private void Clear()
    {
        Items = []; Selected = null; Preview = null; Selection = null; pending = null; Draft = new();
        IsLoaded = Confirmed = ProposalConfirmed = SelectionConfirmed = false; DistanceKm = WaitingFeeKrw = ManualWorkFeeKrw = SurchargeKrw = null;
        DistanceBasisCode = CostSharingMode = ""; IncludeCostSharing = false;
        FixedAmountKrw = RestaurantSharePercent = MaximumRestaurantChargeKrw = MaxUserChargeKrw = null;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; access.Changed -= AuthenticationChanged;
        lifetime.Cancel(); lifetime.Dispose(); Clear();
    }
    public static string Money(decimal? amount) => amount?.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + (amount.HasValue ? "원" : "미확인");
    public static string Status(string code) => code switch
    { "Proposed" => "제안됨", "Accepted" => "합의됨", "Rejected" => "거절됨", "Withdrawn" => "철회됨", _ => "상태 확인 필요" };
    public static string EffectiveStatus(string code) => code switch
    { "Active" => "현재 적용 가능", "NotAccepted" => "상대 확인 전", "NotYetEffective" => "적용 시작 전", "Expired" => "기간 만료", "Withdrawn" => "철회됨", "Rejected" => "거절됨", _ => "적용 상태 확인 필요" };
    public static string SelectionStatus(string code) => code switch
    {
        "SelectedForNextOffer" => "다음 기사 제안에 사용할 합의 요금표를 선택했습니다.",
        "Applied" => "화물 수행대가에 합의 요금표를 적용하고 원장을 확인했습니다.",
        "LedgerSyncPending" => "화물 수행대가를 저장했으며 원장 동기화 확인을 기다리고 있습니다.",
        _ => "요금표 선택 결과를 다시 확인해 주세요."
    };
    private sealed class PendingCommand(string kind, Guid requestId, string? agreementId,
        AgreedRateTableProposalRequest? proposal, AgreedRateTableActionRequest? action, AgreedRateTableSelectionRequest? selection)
    {
        public string Kind { get; } = kind;
        public Guid RequestId { get; } = requestId;
        public string? AgreementId { get; set; } = agreementId;
        public AgreedRateTableProposalRequest? Proposal { get; } = proposal;
        public AgreedRateTableActionRequest? Action { get; } = action;
        public AgreedRateTableSelectionRequest? Selection { get; } = selection;
        public bool Acknowledged { get; set; }
        public long? AcknowledgedRevision { get; set; }
        public AgreedRateTableSelectionDto? SelectionResult { get; set; }
        public AgreedRateTableDto? ExpectedTable { get; set; }
    }
}

/// <summary>아직 제안하지 않은 단가에는 기본 금액을 넣지 않습니다.</summary>
public sealed class AgreedRateProposalDraft
{
    public string CounterpartyId { get; set; } = "";
    public string VehicleCode { get; set; } = "";
    public DateTime? EffectiveFromLocal { get; set; }
    public DateTime? EffectiveUntilLocal { get; set; }
    public int UtcOffsetMinutes { get; set; } = 540;
    public string PricingRevision { get; set; } = "";
    public decimal? PickupFeeKrw { get; set; }
    public decimal? DropoffFeeKrw { get; set; }
    public int? DistanceUnitMeters { get; set; }
    public decimal? DistanceUnitFeeKrw { get; set; }
    public string DistanceRoundingCode { get; set; } = "";
    public int? IncludedDistanceMeters { get; set; }
    public decimal? MinimumFareKrw { get; set; }
    public decimal? BaseFareKrw { get; set; }
    public decimal? PerKmKrw { get; set; }
    public AgreedRateTableProposalRequest Build(string actor, string counterpart, string business)
    {
        if (string.IsNullOrWhiteSpace(CounterpartyId) || string.IsNullOrWhiteSpace(VehicleCode)
            || EffectiveFromLocal is null || EffectiveUntilLocal is null || EffectiveUntilLocal <= EffectiveFromLocal)
            throw new InvalidOperationException("상대 계정·차량 구분·적용 기간을 입력해 주세요.");
        if (UtcOffsetMinutes is < -720 or > 840 || UtcOffsetMinutes % 15 != 0)
            throw new InvalidOperationException("적용 시각의 시간대를 확인해 주세요.");
        var offset = TimeSpan.FromMinutes(UtcOffsetMinutes);
        var fromUtc = new DateTimeOffset(DateTime.SpecifyKind(EffectiveFromLocal.Value, DateTimeKind.Unspecified), offset).UtcDateTime;
        var untilUtc = new DateTimeOffset(DateTime.SpecifyKind(EffectiveUntilLocal.Value, DateTimeKind.Unspecified), offset).UtcDateTime;
        FoodDeliveryPayoutPricingPolicy? food = null; AgreedCargoPricingDto? cargo = null;
        if (business == AgreedRateBusinessKinds.FoodDelivery)
        {
            if (string.IsNullOrWhiteSpace(PricingRevision) || PickupFeeKrw is null or < 0 || DropoffFeeKrw is null or < 0
                || DistanceUnitMeters is null or <= 0 || DistanceUnitFeeKrw is null or < 0
                || string.IsNullOrWhiteSpace(DistanceRoundingCode) || IncludedDistanceMeters is null or < 0 || MinimumFareKrw is null or < 0)
                throw new InvalidOperationException("음식 배달 단가와 거리 계산 조건을 모두 입력해 주세요.");
            food = new(PricingRevision.Trim(), DateOnly.FromDateTime(fromUtc), DateOnly.FromDateTime(untilUtc.AddTicks(-1)),
                PickupFeeKrw.Value, DropoffFeeKrw.Value, DistanceUnitMeters.Value, DistanceUnitFeeKrw.Value,
                DistanceRoundingCode, IncludedDistanceMeters.Value, MinimumFareKrw.Value);
        }
        else
        {
            if (BaseFareKrw is null or < 0 || PerKmKrw is null or < 0 || MinimumFareKrw is null or < 0)
                throw new InvalidOperationException("기본 운임·거리 단가·최소 운임을 입력해 주세요.");
            cargo = new(BaseFareKrw.Value, PerKmKrw.Value, MinimumFareKrw.Value);
        }
        return new()
        {
            ActorPartyKind = actor, CounterpartyPartyKind = counterpart, CounterpartyPartyId = CounterpartyId.Trim(),
            BusinessKind = business, VehicleCode = VehicleCode.Trim(), FoodPricing = food, CargoPricing = cargo,
            EffectiveFromUtc = fromUtc, EffectiveUntilUtc = untilUtc, RequestId = Guid.NewGuid()
        };
    }
}
