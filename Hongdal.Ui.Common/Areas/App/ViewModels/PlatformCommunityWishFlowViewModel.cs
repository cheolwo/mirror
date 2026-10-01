using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class PlatformCommunityWishFlowViewModel : ObservableObject
{
    private string _wish = string.Empty;
    private string _condition = string.Empty;
    private CommunityLedgerFlowAnalysisResponse? _analysis;

    public string Wish
    {
        get => _wish;
        set => SetProperty(ref _wish, value ?? string.Empty);
    }

    public string Condition
    {
        get => _condition;
        set => SetProperty(ref _condition, value ?? string.Empty);
    }

    public CommunityLedgerFlowAnalysisResponse? Analysis
    {
        get => _analysis;
        set => SetProperty(ref _analysis, value);
    }

    public void Reset()
    {
        Wish = string.Empty;
        Condition = string.Empty;
        Analysis = null;
    }
}
