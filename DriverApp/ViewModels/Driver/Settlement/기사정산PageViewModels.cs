using CommunityToolkit.Mvvm.ComponentModel;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Features;
using Ssalddel.Contracts.Driver.Settlement;

namespace DriverApp.ViewModels.Driver.Settlement;

public abstract partial class 기사정산PageViewModelBase : 기사PageViewModelBase
{
    private DateTime? _verifiedAtUtc;
    protected 기사정산PageViewModelBase(기사정산기능ViewModel 정산기능)
    {
        this.정산기능 = 하위ViewModel등록(정산기능);
    }

    public 기사정산기능ViewModel 정산기능 { get; }
    public DriverWorkspaceSliceReadState 정산조회상태 => 초기화됨
        ? new(DriverWorkspaceReadStatus.Current, string.Empty, _verifiedAtUtc)
        : 상태 == Ssalddel.Ui.Common.Areas.App.ViewModels.PageViewModel상태.실패
            ? DriverWorkspaceSliceReadState.Failed("월 정산") : DriverWorkspaceSliceReadState.Unknown("월 정산");

    [ObservableProperty]
    public partial 기사정산응답 정산 { get; private set; } = new();

    protected override async Task 불러오기Async(bool 새로고침, CancellationToken cancellationToken)
    {
        await 정산기능.현재월조회.실행Async(cancellationToken);
        if (정산기능.현재월조회.오류발생)
        {
            throw new InvalidOperationException(정산기능.현재월조회.오류메시지 ?? "현재 월 정산을 불러오지 못했습니다.");
        }

        정산 = 정산기능.현재월조회.결과
            ?? throw new InvalidOperationException("현재 월 정산을 불러오지 못했습니다.");
        _verifiedAtUtc = DateTime.UtcNow;
    }

    protected override bool 하위ViewModel처리중 => 정산기능.현재월조회.처리중;
}

public sealed class 기사월정산PageViewModel(기사정산기능ViewModel 정산기능)
    : 기사정산PageViewModelBase(정산기능);

public sealed class 기사계좌정보PageViewModel(기사정산기능ViewModel 정산기능)
    : 기사정산PageViewModelBase(정산기능);

public sealed class 기사이용료안내PageViewModel(기사정산기능ViewModel 정산기능)
    : 기사정산PageViewModelBase(정산기능);
