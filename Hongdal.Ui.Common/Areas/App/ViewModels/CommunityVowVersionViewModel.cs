using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class CommunityVowVersionViewModel : ObservableObject
{
    private string _selectedCode = CommunityVowVersionCatalog.CurrentVersionCode;

    public IReadOnlyList<CommunityVowVersionDefinition> Versions { get; } =
        CommunityVowVersionCatalog.All;

    public string SelectedCode
    {
        get => _selectedCode;
        set
        {
            var normalized = CommunityVowVersionCatalog.Find(value).Code;
            if (SetProperty(ref _selectedCode, normalized))
            {
                OnPropertyChanged(nameof(Selected));
            }
        }
    }

    public CommunityVowVersionDefinition Selected
        => CommunityVowVersionCatalog.Find(SelectedCode);

    public void RestoreFromWorkflowTag(string? workflowTag)
    {
        var version = CommunityVowVersionCatalog.FindByWorkflowTag(workflowTag);
        if (version is not null)
        {
            SelectedCode = version.Code;
        }
    }

}
