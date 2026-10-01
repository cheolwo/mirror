using Hongdal.Ui.Common.Areas.App.ViewModels;
using HongdalApp.Services;
using HongdalApp.Services.Commerce;

namespace HongdalApp.ViewModels.Shipper;

/// <summary>판매채널 계정 페이지에서 사용하는 업무 조각과 채널 메타데이터를 조립합니다.</summary>
public sealed class 화주판매채널계정PageViewModel : 조립ViewModelBase, ICrudPageViewModel
{
    private IReadOnlyList<CommerceChannelDescriptor> _지원채널상세 = [];
    private IReadOnlyList<업무선택항목<string>> _지원채널목록 = [];

    public 화주판매채널계정PageViewModel(
        IShipperSalesService sales,
        판매채널계정CrudViewModel 계정Crud)
    {
        this.계정Crud = 하위ViewModel등록(계정Crud);
        지원채널조회 = 하위ViewModel등록(
            new Api작업ViewModel<IReadOnlyList<CommerceChannelDescriptor>>(sales.GetSupportedChannelsAsync),
            수명소유: true);
        Crud업무단위목록 = [계정Crud];
    }

    public 판매채널계정CrudViewModel 계정Crud { get; }
    public Api작업ViewModel<IReadOnlyList<CommerceChannelDescriptor>> 지원채널조회 { get; }
    public IReadOnlyList<I업무단위CrudViewModel> Crud업무단위목록 { get; }
    public IReadOnlyList<업무선택항목<string>> 지원채널목록 => _지원채널목록;
    public bool 초기화중 => 지원채널조회.처리중 || 계정Crud.조회.처리중;
    public string? 초기화오류메시지 => 지원채널조회.오류메시지;

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        if (초기화중)
        {
            return false;
        }

        await 지원채널조회.실행Async(cancellationToken);
        if (!지원채널조회.성공함)
        {
            return false;
        }

        _지원채널상세 = 지원채널조회.결과 ?? [];
        _지원채널목록 = _지원채널상세
            .Select(channel => new 업무선택항목<string>(
                channel.ChannelKey,
                channel.DisplayName,
                channel.IntegrationStatus))
            .ToArray();

        계정Crud.조회.지원채널설정(_지원채널상세.Select(channel => channel.ChannelKey));
        OnPropertyChanged(nameof(지원채널목록));
        OnPropertyChanged(nameof(초기화오류메시지));

        return await 계정Crud.조회.조회Async(cancellationToken);
    }
}
