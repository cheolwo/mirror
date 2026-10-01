using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 글에 연결할 내 원장과 공개 원장 탐색, 상세 보기와 공유 설정 상태를 소유합니다.
/// </summary>
public sealed class PlatformCommunityLedgerPickerViewModel(
    ICommunityLedgerClient communityService) : ObservableObject
{
    private string _searchText = string.Empty;
    private string _scope = "전체";
    private string? _pendingLedgerId;
    private bool _isLoading;
    private bool _isPickerOpen;
    private bool _isDetailOpen;
    private bool _isHierarchyOpen;
    private bool _isDetailLoading;
    private bool _detailOpenedFromHierarchy;
    private bool _isSharingSaving;
    private bool _isSharedLedgerReusing;
    private string? _loadMessage;
    private string? _detailErrorMessage;
    private 커뮤니티원장공개설정Response? _sharingSettings;
    private PlatformCommunityPostLedgerContextResponse? _detailContext;
    private PlatformCommunityPostLedgerContextResponse? _hierarchyContext;

    public List<PlatformCommunityPostLedgerChoiceResponse> Items { get; } = [];

    public IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse> FilteredItems
        => Items
            .Where(MatchesScope)
            .Where(MatchesSearch)
            .OrderByDescending(ledger => ledger.내접근원장여부)
            .ThenByDescending(ledger => ledger.수정시각Utc)
            .ToArray();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(FilteredItems));
            }
        }
    }

    public string Scope
    {
        get => _scope;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "전체" : value.Trim();
            if (SetProperty(ref _scope, normalized))
            {
                OnPropertyChanged(nameof(FilteredItems));
            }
        }
    }

    public string? PendingLedgerId { get => _pendingLedgerId; set => SetProperty(ref _pendingLedgerId, value); }
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public bool IsPickerOpen { get => _isPickerOpen; set => SetProperty(ref _isPickerOpen, value); }
    public bool IsDetailOpen { get => _isDetailOpen; set => SetProperty(ref _isDetailOpen, value); }
    public bool IsHierarchyOpen { get => _isHierarchyOpen; set => SetProperty(ref _isHierarchyOpen, value); }
    public bool IsDetailLoading { get => _isDetailLoading; set => SetProperty(ref _isDetailLoading, value); }
    public bool DetailOpenedFromHierarchy { get => _detailOpenedFromHierarchy; set => SetProperty(ref _detailOpenedFromHierarchy, value); }
    public bool IsSharingSaving { get => _isSharingSaving; set => SetProperty(ref _isSharingSaving, value); }
    public bool IsSharedLedgerReusing { get => _isSharedLedgerReusing; set => SetProperty(ref _isSharedLedgerReusing, value); }
    public string? LoadMessage { get => _loadMessage; set => SetProperty(ref _loadMessage, value); }
    public string? DetailErrorMessage { get => _detailErrorMessage; set => SetProperty(ref _detailErrorMessage, value); }
    public 커뮤니티원장공개설정Response? SharingSettings { get => _sharingSettings; set => SetProperty(ref _sharingSettings, value); }
    public PlatformCommunityPostLedgerContextResponse? DetailContext { get => _detailContext; set => SetProperty(ref _detailContext, value); }
    public PlatformCommunityPostLedgerContextResponse? HierarchyContext { get => _hierarchyContext; set => SetProperty(ref _hierarchyContext, value); }

    public void ReplaceItems(IEnumerable<PlatformCommunityPostLedgerChoiceResponse> items)
    {
        Items.Clear();
        Items.AddRange(items);
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(FilteredItems));
    }

    public void ResetFilters()
    {
        SearchText = string.Empty;
        Scope = "전체";
    }

    public bool IsPending(PlatformCommunityPostLedgerChoiceResponse ledger)
        => string.Equals(ledger.원장Id, PendingLedgerId, StringComparison.OrdinalIgnoreCase);

    public void NotifyItemsChanged()
    {
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(FilteredItems));
    }

    public void Open(string? attachedLedgerId)
    {
        IsPickerOpen = true;
        IsDetailOpen = false;
        IsHierarchyOpen = false;
        DetailOpenedFromHierarchy = false;
        DetailContext = null;
        HierarchyContext = null;
        DetailErrorMessage = null;
        SharingSettings = null;
        ResetFilters();
        PendingLedgerId = attachedLedgerId;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        LoadMessage = null;
        var items = new List<PlatformCommunityPostLedgerChoiceResponse>();
        var loginRequired = false;
        var sharedLoadFailed = false;
        try
        {
            items.AddRange(await communityService.GetMyLedgersAsync(
                cancellationToken: cancellationToken));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            loginRequired = true;
        }
        catch (Exception)
        {
            loginRequired = true;
        }

        try
        {
            var sharedLedgers = await communityService.GetSharedLedgersAsync(
                cancellationToken: cancellationToken);
            foreach (var ledger in sharedLedgers)
            {
                if (!items.Any(item => string.Equals(
                        item.원장Id,
                        ledger.원장Id,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    items.Add(ledger);
                }
            }
        }
        catch (Exception)
        {
            sharedLoadFailed = true;
        }

        ReplaceItems(items);
        if (Items.Count == 0)
        {
            LoadMessage = sharedLoadFailed
                ? "원장 목록을 불러오지 못했습니다. 서버 연결 상태를 확인해 주세요."
                : loginRequired
                    ? "로그인하면 내 원장을 함께 볼 수 있습니다. 현재 재공유가 허용된 공개 원장은 없습니다."
                    : "내 원장과 재공유가 허용된 공개 원장이 아직 없습니다.";
        }

        IsLoading = false;
    }

    public async Task<PlatformCommunityCommandResult> OpenPendingDetailAsync(
        CancellationToken cancellationToken = default)
    {
        var ledger = Items.FirstOrDefault(item => IsPending(item));
        if (ledger is null)
        {
            return new(
                false,
                "내부 데이터를 확인할 원장을 먼저 선택해 주세요.",
                CommunityComposerMessageKind.Warning);
        }

        IsDetailOpen = true;
        IsHierarchyOpen = false;
        DetailOpenedFromHierarchy = false;
        IsDetailLoading = true;
        DetailContext = null;
        DetailErrorMessage = null;
        try
        {
            DetailContext = await communityService.GetLedgerContextAsync(ledger.원장Id, cancellationToken);
            if (DetailContext is null)
            {
                DetailErrorMessage = "이 원장의 공개 범위 또는 참여 권한을 확인해 주세요.";
            }
            else if (DetailContext.포함원장목록.Count > 0)
            {
                HierarchyContext = DetailContext;
                IsHierarchyOpen = true;
                IsDetailOpen = false;
            }

            return new(true);
        }
        catch (Exception)
        {
            DetailErrorMessage = "원장 내부 데이터를 불러오지 못했습니다. 서버 연결 상태를 확인해 주세요.";
            return new(false);
        }
        finally
        {
            IsDetailLoading = false;
        }
    }

    public void OpenHierarchyLedgerDiagram(PlatformCommunityPostLedgerContextResponse context)
    {
        DetailContext = context;
        DetailErrorMessage = null;
        IsDetailLoading = false;
        DetailOpenedFromHierarchy = true;
        IsHierarchyOpen = false;
        IsDetailOpen = true;
    }

    public async Task RefreshDetailAsync(CancellationToken cancellationToken = default)
    {
        var ledgerId = DetailContext?.원장Id ?? PendingLedgerId;
        if (string.IsNullOrWhiteSpace(ledgerId))
        {
            return;
        }

        try
        {
            var refreshed = await communityService.GetLedgerContextAsync(ledgerId, cancellationToken);
            if (refreshed is not null)
            {
                DetailContext = refreshed;
                DetailErrorMessage = null;
            }
        }
        catch (Exception)
        {
            DetailErrorMessage = "원장 최신 상태를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }
    }

    public void ReturnToCompose()
    {
        IsDetailOpen = false;
        IsHierarchyOpen = false;
        IsPickerOpen = false;
        PendingLedgerId = null;
        DetailOpenedFromHierarchy = false;
        HierarchyContext = null;
    }

    public void ReturnToPicker()
    {
        IsDetailOpen = false;
        IsHierarchyOpen = false;
        IsPickerOpen = true;
        DetailContext = null;
        DetailErrorMessage = null;
        DetailOpenedFromHierarchy = false;
        HierarchyContext = null;
    }

    public bool ReturnFromDetail()
    {
        if (DetailOpenedFromHierarchy && HierarchyContext is not null)
        {
            IsDetailOpen = false;
            IsHierarchyOpen = true;
            DetailContext = null;
            DetailErrorMessage = null;
            DetailOpenedFromHierarchy = false;
            return true;
        }

        ReturnToPicker();
        return false;
    }

    public async Task<PlatformCommunityCommandResult> LoadSharingSettingsAsync(
        string? attachedLedgerId,
        CancellationToken cancellationToken = default)
    {
        var selected = Items.FirstOrDefault(item => string.Equals(
            item.원장Id,
            attachedLedgerId,
            StringComparison.OrdinalIgnoreCase));
        if (selected?.내가만든원장 != true)
        {
            return new(
                false,
                "원장 생성자만 공개 설정을 변경할 수 있습니다.",
                CommunityComposerMessageKind.Warning);
        }

        try
        {
            SharingSettings = await communityService.GetLedgerSharingSettingsAsync(
                selected.원장Id,
                cancellationToken);
            return new(true);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"원장 공개 설정을 불러오지 못했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> SaveSharingSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        if (SharingSettings is null)
        {
            return new(false);
        }

        IsSharingSaving = true;
        try
        {
            SharingSettings = await communityService.UpdateLedgerSharingSettingsAsync(
                SharingSettings.원장Id,
                new 커뮤니티원장공개설정변경Request
                {
                    공개범위 = SharingSettings.공개범위,
                    재사용허용여부 = SharingSettings.재사용허용여부,
                    재공유허용여부 = SharingSettings.재공유허용여부,
                    기대Revision = SharingSettings.Revision,
                    공개항목Key목록 = SharingSettings.항목목록
                        .Where(item => item.공개여부)
                        .Select(item => item.항목Key)
                        .ToArray()
                },
                cancellationToken);
            var message = SharingSettings?.공개범위 == 커뮤니티원장공개범위.비공개
                ? "원장을 비공개로 전환했습니다."
                : "선택한 항목만 공개되도록 원장 공유 설정을 저장했습니다.";
            await LoadAsync(cancellationToken);
            return new(true, message, CommunityComposerMessageKind.Success);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"원장 공개 설정 저장에 실패했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
        finally
        {
            IsSharingSaving = false;
        }
    }

    public async Task<PlatformCommunityLedgerReuseResult> ReuseSharedLedgerAsync(
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        if (IsSharedLedgerReusing)
        {
            return new(new(false));
        }

        IsSharedLedgerReusing = true;
        try
        {
            var reused = await communityService.ReuseSharedLedgerAsync(
                ledgerId,
                cancellationToken: cancellationToken);
            if (reused is null)
            {
                return new(new(
                    false,
                    "원장 사본을 만들지 못했습니다.",
                    CommunityComposerMessageKind.Error));
            }

            await LoadAsync(cancellationToken);
            return new(
                new(
                    true,
                    $"'{reused.제목}'을 내 비공개 원장으로 가져와 글에 첨부했습니다.",
                    CommunityComposerMessageKind.Success),
                reused);
        }
        catch (Exception ex)
        {
            return new(new(
                false,
                $"원장을 가져오지 못했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error));
        }
        finally
        {
            IsSharedLedgerReusing = false;
        }
    }

    private bool MatchesScope(PlatformCommunityPostLedgerChoiceResponse ledger)
        => Scope switch
        {
            "내 원장" => ledger.내접근원장여부,
            "공개 원장" => !ledger.내접근원장여부,
            _ => true
        };

    private bool MatchesSearch(PlatformCommunityPostLedgerChoiceResponse ledger)
    {
        var searchText = SearchText.Trim();
        return searchText.Length == 0
               || Contains(ledger.제목, searchText)
               || Contains(ledger.원장템플릿명, searchText)
               || Contains(ledger.상태, searchText)
               || Contains(ledger.WorkflowTag, searchText)
               || Contains(ledger.참여역할, searchText)
               || Contains(ledger.원장Id, searchText);
    }

    private static bool Contains(string? value, string searchText)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(searchText, StringComparison.OrdinalIgnoreCase);
}
