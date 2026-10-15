using CommunityToolkit.Mvvm.ComponentModel;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Features;
using Ssalddel.Contracts.Driver.Reservation;

namespace DriverApp.ViewModels.Driver.Reservation;

public sealed partial class 기사예약PageViewModel : 기사PageViewModelBase
{
    private DateTime? _verifiedAtUtc;
    public 기사예약PageViewModel(기사예약기능ViewModel 예약기능)
    {
        this.예약기능 = 하위ViewModel등록(예약기능);
    }

    public 기사예약기능ViewModel 예약기능 { get; }
    public DriverWorkspaceSliceReadState 예약조회상태 => 초기화됨
        ? new(DriverWorkspaceReadStatus.Current, string.Empty, _verifiedAtUtc)
        : 상태 == Ssalddel.Ui.Common.Areas.App.ViewModels.PageViewModel상태.실패
            ? DriverWorkspaceSliceReadState.Failed("예약") : DriverWorkspaceSliceReadState.Unknown("예약");

    [ObservableProperty]
    public partial IReadOnlyList<기사예약목록응답> 예약목록 { get; private set; } = [];

    protected override async Task 불러오기Async(bool 새로고침, CancellationToken cancellationToken)
    {
        await 예약기능.목록조회.실행Async(cancellationToken);
        if (예약기능.목록조회.오류발생)
        {
            throw new InvalidOperationException(예약기능.목록조회.오류메시지 ?? "예약 목록을 불러오지 못했습니다.");
        }

        예약목록 = 예약기능.목록조회.결과
            ?? throw new InvalidOperationException("예약 목록을 확인하지 못했습니다. 예약이 없는 것으로 표시하지 않습니다.");
        _verifiedAtUtc = DateTime.UtcNow;
    }

    protected override bool 하위ViewModel처리중 => 예약기능.목록조회.처리중;
}
