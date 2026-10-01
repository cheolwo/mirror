using DriverApp.Services;
using Hongdal.Contracts.CommandSettings;

namespace DriverApp.ViewModels.Driver.Features;

public sealed class 기사Command기능설정ViewModel : 조립ViewModelBase
{
    public 기사Command기능설정ViewModel(IDriverCommandFeatureSettingsApiService api)
    {
        목록조회 = 하위ViewModel등록(new Api작업ViewModel<Command기능설정목록응답?>(api.목록조회Async));
        수정 = 하위ViewModel등록(
            new Api작업ViewModel<기사Command기능설정수정조건, Api작업완료>(
                async (condition, cancellationToken) =>
                {
                    await api.수정Async(
                        condition.CommandName,
                        condition.FeatureName,
                        new Command기능설정수정요청 { IsEnabled = condition.IsEnabled },
                        cancellationToken);
                    return Api작업완료.값;
                }));
        기본값복원 = 하위ViewModel등록(
            new Api작업ViewModel<기사Command기능설정키, Api작업완료>(
                async (condition, cancellationToken) =>
                {
                    await api.기본값복원Async(
                        condition.CommandName,
                        condition.FeatureName,
                        cancellationToken);
                    return Api작업완료.값;
                }));
    }

    public Api작업ViewModel<Command기능설정목록응답?> 목록조회 { get; }
    public Api작업ViewModel<기사Command기능설정수정조건, Api작업완료> 수정 { get; }
    public Api작업ViewModel<기사Command기능설정키, Api작업완료> 기본값복원 { get; }
}

public record 기사Command기능설정키(string CommandName, string FeatureName);

public sealed record 기사Command기능설정수정조건(
    string CommandName,
    string FeatureName,
    bool IsEnabled) : 기사Command기능설정키(CommandName, FeatureName);
