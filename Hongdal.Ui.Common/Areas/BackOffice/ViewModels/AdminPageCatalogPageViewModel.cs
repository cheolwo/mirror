using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Ui.Common.Areas.BackOffice.ViewModels;

public sealed class AdminPageCatalogPageViewModel : PageViewModelBase
{
    private readonly IAdminPageCatalogClient _client;
    private IReadOnlyList<AdminManagedPageSnapshot> _allPages = [];
    private IReadOnlyList<AdminPageCatalogOption> _appOptions = [];
    private IReadOnlyList<AdminPageCatalogOption> _areaOptions = [];
    private string _appFilter = string.Empty;
    private string _areaFilter = string.Empty;
    private string _reviewFilter = string.Empty;
    private string _executionFilter = string.Empty;
    private string _searchText = string.Empty;
    private bool _needsAttentionOnly;
    private bool _commandInProgress;
    private string? _message;
    private AdminPageCatalogMessageKind _messageKind = AdminPageCatalogMessageKind.Info;

    public AdminPageCatalogPageViewModel(
        IAdminPageCatalogClient client,
        AdminPageCatalogListViewModel list,
        AdminPageCatalogDetailViewModel detail)
    {
        _client = client;
        List = 하위ViewModel등록(list);
        Detail = 하위ViewModel등록(detail);
    }

    public AdminPageCatalogListViewModel List { get; }
    public AdminPageCatalogDetailViewModel Detail { get; }

    public IReadOnlyList<AdminPageCatalogOption> AppOptions
    {
        get => _appOptions;
        private set => SetProperty(ref _appOptions, value);
    }

    public IReadOnlyList<AdminPageCatalogOption> AreaOptions
    {
        get => _areaOptions;
        private set => SetProperty(ref _areaOptions, value);
    }

    public string AppFilter
    {
        get => _appFilter;
        set
        {
            if (SetProperty(ref _appFilter, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    public string AreaFilter
    {
        get => _areaFilter;
        set
        {
            if (SetProperty(ref _areaFilter, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    public string ReviewFilter
    {
        get => _reviewFilter;
        set
        {
            if (SetProperty(ref _reviewFilter, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    public string ExecutionFilter
    {
        get => _executionFilter;
        set
        {
            if (SetProperty(ref _executionFilter, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    public bool NeedsAttentionOnly
    {
        get => _needsAttentionOnly;
        set
        {
            if (SetProperty(ref _needsAttentionOnly, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool CommandInProgress
    {
        get => _commandInProgress;
        private set => SetProperty(ref _commandInProgress, value);
    }

    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public AdminPageCatalogMessageKind MessageKind
    {
        get => _messageKind;
        private set => SetProperty(ref _messageKind, value);
    }

    public bool SelectPage(string pageKey)
    {
        var selected = _allPages.FirstOrDefault(page => page.PageKey == pageKey);
        if (selected is null)
        {
            return false;
        }

        Detail.Select(selected);
        Message = null;
        return true;
    }

    public void ResetFilters()
    {
        _appFilter = string.Empty;
        _areaFilter = string.Empty;
        _reviewFilter = string.Empty;
        _executionFilter = string.Empty;
        _searchText = string.Empty;
        _needsAttentionOnly = false;
        OnPropertyChanged(nameof(AppFilter));
        OnPropertyChanged(nameof(AreaFilter));
        OnPropertyChanged(nameof(ReviewFilter));
        OnPropertyChanged(nameof(ExecutionFilter));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(NeedsAttentionOnly));
        ApplyFilter();
    }

    public async Task<bool> SaveSelectedAsync(
        bool canManage,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        if (!canManage)
        {
            SetMessage("서버관리자 로그인이 필요합니다.", AdminPageCatalogMessageKind.Warning);
            return false;
        }

        if (Detail.SelectedPage is not { } selected)
        {
            SetMessage("관리할 페이지를 먼저 선택해 주세요.", AdminPageCatalogMessageKind.Warning);
            return false;
        }

        if (Detail.AdminNote.Length > 500)
        {
            SetMessage("관리 메모는 500자 이내로 입력해 주세요.", AdminPageCatalogMessageKind.Warning);
            return false;
        }

        CommandInProgress = true;
        try
        {
            var updated = await _client.UpdatePageAsync(
                new AdminPageCatalogUpdateRequest(
                    selected.PageKey,
                    Detail.ReviewState,
                    Detail.NavigationState,
                    Detail.DesktopVerified,
                    Detail.MobileVerified,
                    Detail.AdminNote.Trim(),
                    string.IsNullOrWhiteSpace(reviewer) ? "서버관리자" : reviewer.Trim()),
                cancellationToken);

            _allPages = _allPages
                .Select(page => page.PageKey == updated.PageKey ? updated : page)
                .ToArray();
            Detail.Select(updated);
            ApplyFilter(updated.PageKey);
            SetMessage(
                $"{updated.Title} 페이지의 관리 메타데이터를 저장했습니다.",
                AdminPageCatalogMessageKind.Success);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, AdminPageCatalogMessageKind.Error);
            return false;
        }
        finally
        {
            CommandInProgress = false;
        }
    }

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        var selectedKey = Detail.SelectedPage?.PageKey;
        _allPages = AdminPageCatalogProjection.Order(
            await _client.GetPagesAsync(cancellationToken));

        AppOptions = AdminPageCatalogProjection.BuildAppOptions(_allPages);
        AreaOptions = AdminPageCatalogProjection.BuildAreaOptions(_allPages);

        Message = null;
        ApplyFilter(selectedKey);
    }

    private void ApplyFilter(string? preferredPageKey = null)
    {
        if (_allPages.Count == 0)
        {
            List.Replace([], AdminPageCatalogProjection.Summarize([]));
            Detail.Clear();
            return;
        }

        var filtered = AdminPageCatalogProjection.Filter(
            _allPages,
            new AdminPageCatalogQuery(
                AppFilter,
                AreaFilter,
                ReviewFilter,
                ExecutionFilter,
                SearchText,
                NeedsAttentionOnly));
        List.Replace(filtered, AdminPageCatalogProjection.Summarize(_allPages));

        var selectedKey = preferredPageKey ?? Detail.SelectedPage?.PageKey;
        var selected = filtered.FirstOrDefault(page => page.PageKey == selectedKey)
                       ?? filtered.FirstOrDefault();
        if (selected is null)
        {
            Detail.Clear();
        }
        else if (Detail.SelectedPage?.PageKey != selected.PageKey || preferredPageKey is not null)
        {
            Detail.Select(selected);
        }
    }

    private void SetMessage(string message, AdminPageCatalogMessageKind kind)
    {
        MessageKind = kind;
        Message = message;
    }
}
