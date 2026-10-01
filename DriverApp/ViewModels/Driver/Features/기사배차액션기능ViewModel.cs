using DriverApp.Services;
using Hongdal.Contracts.Driver.Action;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사배차액션기능ViewModel : 조립ViewModelBase
{
    public 기사배차액션기능ViewModel(IDriverDispatchActionApiService api)
    {
        수락 = 하위ViewModel등록(new Api작업ViewModel<string, 기사배차처리응답?>(api.수락Async));
        거절 = 하위ViewModel등록(
            new Api작업ViewModel<기사배차거절조건, Api작업완료>(async (condition, cancellationToken) =>
            {
                await api.거절Async(
                    condition.의뢰Id,
                    new 기사배차거절요청 { 사유 = condition.사유 },
                    cancellationToken);
                return Api작업완료.값;
            }));
        수락취소 = 하위ViewModel등록(
            new Api작업ViewModel<기사배차수락취소조건, Api작업완료>(async (condition, cancellationToken) =>
            {
                await api.수락취소Async(
                    condition.의뢰Id,
                    new 기사배차수락취소요청 { 사유 = condition.사유 },
                    cancellationToken);
                return Api작업완료.값;
            }));
    }

    public Api작업ViewModel<string, 기사배차처리응답?> 수락 { get; }
    public Api작업ViewModel<기사배차거절조건, Api작업완료> 거절 { get; }
    public Api작업ViewModel<기사배차수락취소조건, Api작업완료> 수락취소 { get; }
}

public sealed record 기사배차거절조건(string 의뢰Id, string? 사유 = null);
public sealed record 기사배차수락취소조건(string 의뢰Id, string? 사유 = null);
