using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Ui.Common.Areas.BackOffice.ViewModels;

public sealed class AdminPageCatalogListViewModel : 조립ViewModelBase
{
    private IReadOnlyList<AdminManagedPageSnapshot> _items = [];
    private AdminPageCatalogSummary _summary = new(0, 0, 0, 0, 0);

    public IReadOnlyList<AdminManagedPageSnapshot> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public AdminPageCatalogSummary Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public void Replace(
        IReadOnlyList<AdminManagedPageSnapshot> items,
        AdminPageCatalogSummary summary)
    {
        Items = items;
        Summary = summary;
    }
}

public sealed class AdminPageCatalogDetailViewModel : 조립ViewModelBase
{
    private AdminManagedPageSnapshot? _selectedPage;
    private AdminPageReviewState _reviewState;
    private AdminPageNavigationState _navigationState;
    private bool _desktopVerified;
    private bool _mobileVerified;
    private string _adminNote = string.Empty;

    public AdminManagedPageSnapshot? SelectedPage
    {
        get => _selectedPage;
        private set
        {
            if (SetProperty(ref _selectedPage, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => SelectedPage is not null;

    public AdminPageReviewState ReviewState
    {
        get => _reviewState;
        set => SetProperty(ref _reviewState, value);
    }

    public AdminPageNavigationState NavigationState
    {
        get => _navigationState;
        set => SetProperty(ref _navigationState, value);
    }

    public bool DesktopVerified
    {
        get => _desktopVerified;
        set => SetProperty(ref _desktopVerified, value);
    }

    public bool MobileVerified
    {
        get => _mobileVerified;
        set => SetProperty(ref _mobileVerified, value);
    }

    public string AdminNote
    {
        get => _adminNote;
        set => SetProperty(ref _adminNote, value);
    }

    public void Select(AdminManagedPageSnapshot page)
    {
        SelectedPage = page;
        ReviewState = page.ReviewState;
        NavigationState = page.NavigationState;
        DesktopVerified = page.DesktopVerified;
        MobileVerified = page.MobileVerified;
        AdminNote = page.AdminNote;
    }

    public void Clear()
        => SelectedPage = null;
}
