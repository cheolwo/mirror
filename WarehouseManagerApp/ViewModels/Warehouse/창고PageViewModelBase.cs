using System.ComponentModel;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public abstract class 창고PageViewModelBase : 조립ViewModelBase
{
    private readonly IReadOnlySet<string> _지원ProfileCodes;

    protected 창고PageViewModelBase(
        창고작업세션상태ViewModel 세션,
        string 페이지코드,
        string 페이지명,
        params string[] 지원ProfileCodes)
    {
        ArgumentNullException.ThrowIfNull(세션);
        ArgumentException.ThrowIfNullOrWhiteSpace(페이지코드);
        ArgumentException.ThrowIfNullOrWhiteSpace(페이지명);

        this.세션 = 하위ViewModel등록(세션);
        this.페이지코드 = 페이지코드;
        this.페이지명 = 페이지명;
        _지원ProfileCodes = new HashSet<string>(
            지원ProfileCodes.Length == 0 ? 창고운영ProfileCodes.전체 : 지원ProfileCodes,
            StringComparer.OrdinalIgnoreCase);
    }

    public 창고작업세션상태ViewModel 세션 { get; }
    public string 페이지코드 { get; }
    public string 페이지명 { get; }
    public IReadOnlySet<string> 지원ProfileCodes => _지원ProfileCodes;
    public bool 현재창고에서사용가능 => _지원ProfileCodes.Contains(세션.운영ProfileCode);

    protected T 구성요소등록<T>(T viewModel)
        where T : class, INotifyPropertyChanged
        => 하위ViewModel등록(viewModel);
}
