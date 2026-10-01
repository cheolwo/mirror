using CommunityToolkit.Mvvm.ComponentModel;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class CommunityActionJourneyNavigationViewModel : ObservableObject
{
    private CommunityCollectiveActionPageDefinition _currentPage
        = CommunityCollectiveActionPageCatalog.Find(CommunityCollectiveActionPageKeys.Gathering);

    public IReadOnlyList<CommunityCollectiveActionPageDefinition> JourneyStages { get; }
        = CommunityCollectiveActionPageCatalog.All.Where(page => page.IsJourneyStage).ToArray();

    public IReadOnlyList<CommunityCollectiveActionPageDefinition> PersonalPages { get; }
        = CommunityCollectiveActionPageCatalog.All.Where(page => !page.IsJourneyStage).ToArray();

    public CommunityCollectiveActionPageDefinition CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public void Select(string? pageKey)
        => CurrentPage = CommunityCollectiveActionPageCatalog.Find(pageKey);

    public bool IsCurrent(string pageKey)
        => string.Equals(CurrentPage.Key, pageKey, StringComparison.OrdinalIgnoreCase);
}

public sealed class CommunityActionCollectionViewModel : ObservableObject
{
    private IReadOnlyList<CommunityCollectiveActionSnapshot> _items = [];
    private CommunityCollectiveActionSnapshot? _selected;

    public IReadOnlyList<CommunityCollectiveActionSnapshot> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public CommunityCollectiveActionSnapshot? Selected
    {
        get => _selected;
        private set => SetProperty(ref _selected, value);
    }

    public void Replace(IEnumerable<CommunityCollectiveActionSnapshot> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items.ToArray();
        if (Selected is not null && Items.All(item => item.Id != Selected.Id))
        {
            Selected = null;
        }
    }

    public bool Select(Guid id)
    {
        var item = Items.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null)
        {
            return false;
        }

        Selected = item;
        return true;
    }

    public bool Select(CommunityCollectiveActionSnapshot? item)
    {
        if (item is null || Items.All(candidate => candidate.Id != item.Id))
        {
            return false;
        }

        Selected = item;
        return true;
    }

    public IReadOnlyList<CommunityCollectiveActionSnapshot> ForPage(string pageKey)
    {
        var normalized = CommunityCollectiveActionPageKeys.Normalize(pageKey);
        return normalized switch
        {
            CommunityCollectiveActionPageKeys.Mine => Items.Where(item => item.IsMine).ToArray(),
            CommunityCollectiveActionPageKeys.Stories => Items.Where(item =>
                item.CurrentPageKey == CommunityCollectiveActionPageKeys.Completed).ToArray(),
            CommunityCollectiveActionPageKeys.Professionals => Items.Where(item =>
                item.RoleSlots.Any(slot => !slot.Accepted
                                           && slot.RoleCode is not "buyer"
                                           && slot.RoleCode is not "participants")).ToArray(),
            _ => Items
        };
    }
}

public sealed class CommunityActionConditionsViewModel : ObservableObject
{
    private IReadOnlyList<CommunityActionConditionSnapshot> _items = [];

    public IReadOnlyList<CommunityActionConditionSnapshot> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public int ConfirmedCount => Items.Count(item => item.Confirmed);
    public int TotalCount => Items.Count;
    public bool AllConfirmed => Items.Count > 0 && Items.All(item => item.Confirmed);

    public void Apply(CommunityCollectiveActionSnapshot snapshot)
    {
        Items = snapshot.Conditions;
        OnPropertyChanged(nameof(ConfirmedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(AllConfirmed));
    }
}

public sealed class CommunityActionPartyViewModel : ObservableObject
{
    private IReadOnlyList<CommunityActionRoleSlotSnapshot> _slots = [];

    public IReadOnlyList<CommunityActionRoleSlotSnapshot> Slots
    {
        get => _slots;
        private set => SetProperty(ref _slots, value);
    }

    public int RequiredCount => Slots.Count(slot => slot.Required);
    public int AcceptedRequiredCount => Slots.Count(slot => slot.Required && slot.Accepted);
    public int OpenProfessionalCount => Slots.Count(slot =>
        !slot.Accepted && slot.Category is "통관·문서" or "운송 중개·주선" or "실제 운송" or "현장 이행");

    public void Apply(CommunityCollectiveActionSnapshot snapshot)
    {
        Slots = snapshot.RoleSlots;
        OnPropertyChanged(nameof(RequiredCount));
        OnPropertyChanged(nameof(AcceptedRequiredCount));
        OnPropertyChanged(nameof(OpenProfessionalCount));
    }
}

public sealed class CommunityActionDeliveryViewModel : ObservableObject
{
    private CommunityCollectiveImportDeliverySnapshot _snapshot = new();

    public CommunityCollectiveImportDeliverySnapshot Snapshot
    {
        get => _snapshot;
        private set => SetProperty(ref _snapshot, value);
    }

    public bool IsApplicable => Snapshot.IsApplicable;
    public string RecruitmentScopeLabel => Snapshot.RecruitmentScopeLabel;
    public string RecruitmentScopeKey => Snapshot.RecruitmentScopeKey;
    public bool RecruitmentScopeVerified => Snapshot.RecruitmentScopeVerified;
    public int CompletedStageCount => Snapshot.CompletedStageCount;
    public int TotalStageCount => Snapshot.Stages.Count;
    public CommunityCollectiveImportDeliveryStageSnapshot? CurrentStage
        => Snapshot.CurrentStage;
    public IReadOnlyList<CommunityCollectiveImportDeliveryStageSnapshot> Stages
        => Snapshot.Stages;

    public void Apply(CommunityCollectiveActionSnapshot snapshot)
    {
        Snapshot = snapshot.Delivery;
        OnPropertyChanged(nameof(IsApplicable));
        OnPropertyChanged(nameof(RecruitmentScopeLabel));
        OnPropertyChanged(nameof(RecruitmentScopeKey));
        OnPropertyChanged(nameof(RecruitmentScopeVerified));
        OnPropertyChanged(nameof(CompletedStageCount));
        OnPropertyChanged(nameof(TotalStageCount));
        OnPropertyChanged(nameof(CurrentStage));
        OnPropertyChanged(nameof(Stages));
    }
}

public sealed class CommunityActionTraditionalMarketImportedMeatViewModel : ObservableObject
{
    private CommunityTraditionalMarketImportedMeatFulfillmentSnapshot _snapshot = new();

    public CommunityTraditionalMarketImportedMeatFulfillmentSnapshot Snapshot
    {
        get => _snapshot;
        private set => SetProperty(ref _snapshot, value);
    }

    public bool IsApplicable => Snapshot.IsApplicable;

    public void Apply(CommunityCollectiveActionSnapshot snapshot)
    {
        Snapshot = snapshot.TraditionalMarketImportedMeatFulfillment;
        OnPropertyChanged(nameof(IsApplicable));
    }
}

public sealed class CommunityActionMarketDayViewModel : ObservableObject
{
    private CommunityMarketDaySnapshot _snapshot = new();

    public CommunityMarketDaySnapshot Snapshot
    {
        get => _snapshot;
        private set => SetProperty(ref _snapshot, value);
    }

    public bool IsApplicable => Snapshot.IsApplicable;
    public bool CanAdvertiseWalkInSale => Snapshot.CanAdvertiseWalkInSale;
    public CommunityMarketDayStageSnapshot? CurrentStage => Snapshot.CurrentStage;
    public CommunityDomesticMarketSupplySnapshot DomesticSupply => Snapshot.DomesticSupply;
    public CommunityMarketIngredientSupplySnapshot MarketIngredientSupply
        => DomesticSupply.MarketIngredientSupply;

    public void Apply(CommunityCollectiveActionSnapshot snapshot)
    {
        Snapshot = snapshot.MarketDay;
        OnPropertyChanged(nameof(IsApplicable));
        OnPropertyChanged(nameof(CanAdvertiseWalkInSale));
        OnPropertyChanged(nameof(CurrentStage));
        OnPropertyChanged(nameof(DomesticSupply));
        OnPropertyChanged(nameof(MarketIngredientSupply));
    }
}

public sealed class CommunityActionReadinessViewModel : ObservableObject
{
    private IReadOnlyList<CommunityActionReadinessCheckSnapshot> _checks = [];

    public IReadOnlyList<CommunityActionReadinessCheckSnapshot> Checks
    {
        get => _checks;
        private set => SetProperty(ref _checks, value);
    }

    public int CompleteCount => Checks.Count(check => check.Complete);
    public int TotalCount => Checks.Count;
    public bool ExecutionReviewReady
        => Checks.Count > 0 && Checks.Where(check => check.BlocksExecution).All(check => check.Complete);

    public void Apply(CommunityCollectiveActionSnapshot snapshot)
    {
        Checks = snapshot.ReadinessChecks;
        OnPropertyChanged(nameof(CompleteCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ExecutionReviewReady));
    }
}

public sealed class CommunityActionExecutionViewModel : ObservableObject
{
    private decimal _currentCommittedQuantity;
    private decimal _currentPotentialQuantity;
    private decimal _minimumOrderQuantity;
    private decimal _additionalQuantity = 1m;
    private string _quantityUnit = "개";
    private DateTimeOffset? _closesAt;
    private decimal? _estimatedCurrentUnitCost;
    private decimal? _estimatedSafeMaximumUnitCost;
    private IReadOnlyList<CommunityCapacityEvidenceSnapshot> _capacityEvidence = [];
    private IReadOnlyList<CommunityActionTimelineItemSnapshot> _timeline = [];
    private string? _message;
    private CommunityComposerMessageKind _messageKind = CommunityComposerMessageKind.Info;

    public decimal CurrentCommittedQuantity
    {
        get => _currentCommittedQuantity;
        private set => SetProperty(ref _currentCommittedQuantity, value);
    }

    public decimal CurrentPotentialQuantity
    {
        get => _currentPotentialQuantity;
        private set => SetProperty(ref _currentPotentialQuantity, value);
    }

    public decimal MinimumOrderQuantity
    {
        get => _minimumOrderQuantity;
        private set => SetProperty(ref _minimumOrderQuantity, value);
    }

    public decimal AdditionalQuantity
    {
        get => _additionalQuantity;
        set
        {
            var normalized = value < 1m ? 1m : decimal.Ceiling(value);
            if (SetProperty(ref _additionalQuantity, normalized))
            {
                OnPropertyChanged(nameof(ProjectedPotentialQuantity));
                OnPropertyChanged(nameof(SelectedQuantityFitsConfirmedCapacity));
            }
        }
    }

    public string QuantityUnit
    {
        get => _quantityUnit;
        private set => SetProperty(ref _quantityUnit, value);
    }

    public DateTimeOffset? ClosesAt
    {
        get => _closesAt;
        private set => SetProperty(ref _closesAt, value);
    }

    public decimal? EstimatedCurrentUnitCost
    {
        get => _estimatedCurrentUnitCost;
        private set => SetProperty(ref _estimatedCurrentUnitCost, value);
    }

    public decimal? EstimatedSafeMaximumUnitCost
    {
        get => _estimatedSafeMaximumUnitCost;
        private set => SetProperty(ref _estimatedSafeMaximumUnitCost, value);
    }

    public IReadOnlyList<CommunityCapacityEvidenceSnapshot> CapacityEvidence
    {
        get => _capacityEvidence;
        private set => SetProperty(ref _capacityEvidence, value);
    }

    public IReadOnlyList<CommunityActionTimelineItemSnapshot> Timeline
    {
        get => _timeline;
        private set => SetProperty(ref _timeline, value);
    }

    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public CommunityComposerMessageKind MessageKind
    {
        get => _messageKind;
        private set => SetProperty(ref _messageKind, value);
    }

    public bool AllRequiredCapacityConfirmed
    {
        get
        {
            var required = CapacityEvidence.Where(item => item.Required).ToArray();
            return required.Length > 0
                   && required.All(item => item.Status == CommunityCapacityEvidenceStatus.Confirmed);
        }
    }

    public decimal? EstimatedMaximumTotalQuantity
        => MinimumKnownCapacity(CapacityEvidence);

    public decimal? ConfirmedMaximumTotalQuantity
        => AllRequiredCapacityConfirmed
            ? MinimumKnownCapacity(CapacityEvidence.Where(item => item.Required))
            : null;

    public decimal EstimatedRemainingQuantity
        => Remaining(EstimatedMaximumTotalQuantity, CurrentPotentialQuantity);

    public decimal ConfirmedRemainingQuantity
        => Remaining(ConfirmedMaximumTotalQuantity, CurrentPotentialQuantity);

    public decimal ProjectedPotentialQuantity => CurrentPotentialQuantity + AdditionalQuantity;
    public bool IsClosed => ClosesAt.HasValue && ClosesAt.Value <= DateTimeOffset.UtcNow;
    public bool CanReviewCurrentBatch => !IsClosed && (ConfirmedRemainingQuantity > 0m || EstimatedRemainingQuantity > 0m);
    public bool SelectedQuantityFitsConfirmedCapacity
        => AllRequiredCapacityConfirmed && AdditionalQuantity <= ConfirmedRemainingQuantity;

    public string CapacityHeadline
        => ConfirmedRemainingQuantity > 0m
            ? $"현재 배치에 {ConfirmedRemainingQuantity:N0}{QuantityUnit} 더 함께할 수 있어요"
            : EstimatedRemainingQuantity > 0m
                ? $"최대 {EstimatedRemainingQuantity:N0}{QuantityUnit}의 여력을 확인하고 있어요"
                : "현재 배치의 추가 여력을 확인하고 있어요";

    public void Apply(CommunityCollectiveActionSnapshot snapshot)
    {
        CurrentCommittedQuantity = snapshot.CurrentCommittedQuantity;
        CurrentPotentialQuantity = snapshot.CurrentPotentialQuantity;
        MinimumOrderQuantity = snapshot.MinimumOrderQuantity;
        QuantityUnit = snapshot.QuantityUnit;
        ClosesAt = snapshot.AdditionalParticipationClosesAt;
        EstimatedCurrentUnitCost = snapshot.EstimatedCurrentUnitCost;
        EstimatedSafeMaximumUnitCost = snapshot.EstimatedSafeMaximumUnitCost;
        CapacityEvidence = snapshot.CapacityEvidence;
        Timeline = snapshot.Timeline;
        AdditionalQuantity = 1m;
        Message = null;
        MessageKind = CommunityComposerMessageKind.Info;
        NotifyCalculatedProperties();
    }

    public void ReviewAdditionalParticipation()
    {
        if (IsClosed)
        {
            SetMessage(
                "현재 배치의 참여 창이 닫혔습니다. 다음 배치 대기로 이어갈 수 있습니다.",
                CommunityComposerMessageKind.Warning);
            return;
        }

        if (SelectedQuantityFitsConfirmedCapacity)
        {
            SetMessage(
                $"선택한 추가 참여 수량은 {AdditionalQuantity:N0}{QuantityUnit}입니다. 확인된 여력 안에 있으며, 아직 예약이나 주문 확정은 아닙니다.",
                CommunityComposerMessageKind.Success);
            return;
        }

        if (EstimatedRemainingQuantity > 0m && AdditionalQuantity <= EstimatedRemainingQuantity)
        {
            SetMessage(
                $"{AdditionalQuantity:N0}{QuantityUnit}의 참여 의향을 검토할 수 있습니다. 확인되지 않은 공급·창고·운송·서류 여력이 남아 있습니다.",
                CommunityComposerMessageKind.Warning);
            return;
        }

        SetMessage(
            "현재 배치의 확인 가능 범위를 넘습니다. 수량을 줄이거나 다음 배치 대기로 이어가 주세요.",
            CommunityComposerMessageKind.Warning);
    }

    public void PrepareNextBatchWaitlist()
        => SetMessage(
            $"다음 배치 참여 의향 수량은 {AdditionalQuantity:N0}{QuantityUnit}입니다. 이 화면에서는 신청을 확정하지 않습니다.",
            CommunityComposerMessageKind.Info);

    private void SetMessage(string message, CommunityComposerMessageKind kind)
    {
        MessageKind = kind;
        Message = message;
    }

    private void NotifyCalculatedProperties()
    {
        OnPropertyChanged(nameof(AllRequiredCapacityConfirmed));
        OnPropertyChanged(nameof(EstimatedMaximumTotalQuantity));
        OnPropertyChanged(nameof(ConfirmedMaximumTotalQuantity));
        OnPropertyChanged(nameof(EstimatedRemainingQuantity));
        OnPropertyChanged(nameof(ConfirmedRemainingQuantity));
        OnPropertyChanged(nameof(ProjectedPotentialQuantity));
        OnPropertyChanged(nameof(IsClosed));
        OnPropertyChanged(nameof(CanReviewCurrentBatch));
        OnPropertyChanged(nameof(SelectedQuantityFitsConfirmedCapacity));
        OnPropertyChanged(nameof(CapacityHeadline));
    }

    private static decimal? MinimumKnownCapacity(IEnumerable<CommunityCapacityEvidenceSnapshot> evidence)
    {
        var values = evidence
            .Where(item => item.Status is CommunityCapacityEvidenceStatus.Confirmed
                or CommunityCapacityEvidenceStatus.Pending)
            .Where(item => item.MaximumTotalQuantity.HasValue)
            .Select(item => item.MaximumTotalQuantity!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Min();
    }

    private static decimal Remaining(decimal? maximum, decimal current)
        => maximum.HasValue ? Math.Max(0m, maximum.Value - current) : 0m;
}

public sealed class CommunityActionOutcomeViewModel : ObservableObject
{
    private IReadOnlyList<CommunityActionOutcomeSnapshot> _items = [];

    public IReadOnlyList<CommunityActionOutcomeSnapshot> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public void Apply(CommunityCollectiveActionSnapshot snapshot)
        => Items = snapshot.Outcomes;
}

public sealed class CommunityCollectiveActionPageViewModel : PageViewModelBase
{
    private readonly ICommunityCollectiveActionSource _source;
    private Guid? _requestedActionId;
    private CommunityCollectiveActionDataMode _dataMode = CommunityCollectiveActionDataMode.Preview;
    private string? _dataNotice;

    public CommunityCollectiveActionPageViewModel(
        ICommunityCollectiveActionSource source,
        CommunityActionJourneyNavigationViewModel navigation,
        CommunityActionCollectionViewModel actions,
        CommunityActionConditionsViewModel conditions,
        CommunityActionPartyViewModel party,
        CommunityActionDeliveryViewModel delivery,
        CommunityActionTraditionalMarketImportedMeatViewModel traditionalMarketImportedMeat,
        CommunityActionMarketDayViewModel marketDay,
        CommunityActionReadinessViewModel readiness,
        CommunityActionExecutionViewModel execution,
        CommunityActionOutcomeViewModel outcome)
    {
        _source = source;
        Navigation = 하위ViewModel등록(navigation);
        Actions = 하위ViewModel등록(actions);
        Conditions = 하위ViewModel등록(conditions);
        Party = 하위ViewModel등록(party);
        Delivery = 하위ViewModel등록(delivery);
        TraditionalMarketImportedMeat = 하위ViewModel등록(traditionalMarketImportedMeat);
        MarketDay = 하위ViewModel등록(marketDay);
        Readiness = 하위ViewModel등록(readiness);
        Execution = 하위ViewModel등록(execution);
        Outcome = 하위ViewModel등록(outcome);
    }

    public CommunityActionJourneyNavigationViewModel Navigation { get; }
    public CommunityActionCollectionViewModel Actions { get; }
    public CommunityActionConditionsViewModel Conditions { get; }
    public CommunityActionPartyViewModel Party { get; }
    public CommunityActionDeliveryViewModel Delivery { get; }
    public CommunityActionTraditionalMarketImportedMeatViewModel TraditionalMarketImportedMeat { get; }
    public CommunityActionMarketDayViewModel MarketDay { get; }
    public CommunityActionReadinessViewModel Readiness { get; }
    public CommunityActionExecutionViewModel Execution { get; }
    public CommunityActionOutcomeViewModel Outcome { get; }

    public CommunityCollectiveActionDataMode DataMode
    {
        get => _dataMode;
        private set
        {
            if (SetProperty(ref _dataMode, value))
            {
                OnPropertyChanged(nameof(IsPreview));
            }
        }
    }

    public string? DataNotice
    {
        get => _dataNotice;
        private set => SetProperty(ref _dataNotice, value);
    }

    public bool IsPreview => DataMode == CommunityCollectiveActionDataMode.Preview;
    public CommunityCollectiveActionPageDefinition CurrentPage => Navigation.CurrentPage;
    public CommunityCollectiveActionSnapshot? SelectedAction => Actions.Selected;
    public IReadOnlyList<CommunityCollectiveActionSnapshot> VisibleActions
        => Actions.ForPage(CurrentPage.Key);
    public string PageTitleText => $"{CurrentPage.Title} · 살뜰 커뮤니티";

    public void Configure(string? pageKey, Guid? actionId)
    {
        Navigation.Select(pageKey);
        _requestedActionId = actionId;
        if (Actions.Items.Count > 0)
        {
            SelectBestAction();
        }

        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(VisibleActions));
        OnPropertyChanged(nameof(PageTitleText));
    }

    public bool SelectAction(Guid actionId)
    {
        if (!Actions.Select(actionId))
        {
            return false;
        }

        _requestedActionId = actionId;
        ApplySelectedAction();
        return true;
    }

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CommunityCollectiveActionSnapshot> snapshots;
        try
        {
            if (_source is ICommunityCollectiveActionSnapshotSource snapshotSource)
            {
                var sourceItems = await snapshotSource.LoadSnapshotsAsync(cancellationToken);
                if (sourceItems.Count == 0)
                {
                    snapshots = CommunityCollectiveActionPreviewCatalog.Create();
                    DataMode = CommunityCollectiveActionDataMode.Preview;
                    DataNotice = "진행 중인 공동구매가 없어 둘러보기 예시를 표시합니다.";
                }
                else
                {
                    snapshots = sourceItems
                        .Select(item => CommunityCollectiveActionSnapshotFactory.FromCampaign(
                            item.Campaign,
                            item.Journey))
                        .ToArray();
                    DataMode = CommunityCollectiveActionDataMode.Live;
                    DataNotice = null;
                }
            }
            else
            {
                var campaigns = await _source.LoadAsync(cancellationToken);
                if (campaigns.Count == 0)
                {
                    snapshots = CommunityCollectiveActionPreviewCatalog.Create();
                    DataMode = CommunityCollectiveActionDataMode.Preview;
                    DataNotice = "진행 중인 공동구매가 없어 둘러보기 예시를 표시합니다.";
                }
                else
                {
                    snapshots = campaigns
                        .Select(campaign => CommunityCollectiveActionSnapshotFactory.FromCampaign(campaign))
                        .ToArray();
                    DataMode = CommunityCollectiveActionDataMode.Live;
                    DataNotice = null;
                }
            }
        }
        catch (HttpRequestException)
        {
            snapshots = CommunityCollectiveActionPreviewCatalog.Create();
            DataMode = CommunityCollectiveActionDataMode.Preview;
            DataNotice = "로그인하거나 서버에 연결하면 실제 함께 하는 일을 볼 수 있습니다. 지금은 둘러보기 예시입니다.";
        }

        Actions.Replace(snapshots);
        SelectBestAction();
        OnPropertyChanged(nameof(VisibleActions));
    }

    private void SelectBestAction()
    {
        var visible = VisibleActions;
        var selected = _requestedActionId.HasValue
            ? visible.FirstOrDefault(item => item.Id == _requestedActionId.Value)
            : null;
        selected ??= visible.FirstOrDefault(item =>
            string.Equals(item.CurrentPageKey, CurrentPage.Key, StringComparison.OrdinalIgnoreCase));
        selected ??= visible.FirstOrDefault();
        selected ??= Actions.Items.FirstOrDefault(item =>
            item.CurrentPageKey == CommunityCollectiveActionPageKeys.InProgress);
        selected ??= Actions.Items.FirstOrDefault();

        if (Actions.Select(selected))
        {
            _requestedActionId = selected?.Id;
            ApplySelectedAction();
        }
    }

    private void ApplySelectedAction()
    {
        if (Actions.Selected is not { } selected)
        {
            return;
        }

        Conditions.Apply(selected);
        Party.Apply(selected);
        Delivery.Apply(selected);
        TraditionalMarketImportedMeat.Apply(selected);
        MarketDay.Apply(selected);
        Readiness.Apply(selected);
        Execution.Apply(selected);
        Outcome.Apply(selected);
        OnPropertyChanged(nameof(SelectedAction));
    }
}
