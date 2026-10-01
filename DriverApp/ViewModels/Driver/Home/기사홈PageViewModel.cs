using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Models.Driver.Home;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Features;

namespace DriverApp.ViewModels.Driver.Home;

public sealed partial class 기사홈PageViewModel : 조립ViewModelBase
{
    private bool _조회중;

    public 기사홈PageViewModel(기사프로필기능ViewModel 프로필기능)
    {
        this.프로필기능 = 하위ViewModel등록(프로필기능);
        화면 = 기사홈ViewModel.Empty();
        화면상태 = 기사홈화면상태.불러오는중;
    }

    public 기사프로필기능ViewModel 프로필기능 { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(주요행동라벨))]
    [NotifyPropertyChangedFor(nameof(주요행동경로))]
    public partial 기사홈ViewModel 화면 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(불러오는중))]
    [NotifyPropertyChangedFor(nameof(오류발생))]
    [NotifyPropertyChangedFor(nameof(표시준비됨))]
    public partial 기사홈화면상태 화면상태 { get; private set; }

    [ObservableProperty]
    public partial string? 오류메시지 { get; private set; }

    public bool 불러오는중 => 화면상태 == 기사홈화면상태.불러오는중;
    public bool 오류발생 => 화면상태 == 기사홈화면상태.오류;
    public bool 표시준비됨 => 화면상태 == 기사홈화면상태.준비됨;

    public string 주요행동라벨 => 화면.주요행동문구;
    public string 주요행동경로 => Resolve주요행동경로(화면.주요행동코드);

    public Task InitializeAsync() => RefreshAsync();

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RefreshAsync()
    {
        if (_조회중)
        {
            return;
        }

        _조회중 = true;
        화면상태 = 기사홈화면상태.불러오는중;
        오류메시지 = null;

        try
        {
            await 프로필기능.홈조회.실행Async();
            if (프로필기능.홈조회.오류발생)
            {
                Set오류(프로필기능.홈조회.오류메시지 ?? "기사 홈 정보를 불러오지 못했습니다.");
                return;
            }

            var response = 프로필기능.홈조회.결과;
            if (response is null)
            {
                Set오류("기사 홈 정보를 불러오지 못했습니다.");
                return;
            }

            화면 = 기사홈ViewModel.From(response);
            화면상태 = 기사홈화면상태.준비됨;
        }
        catch (Exception ex)
        {
            Set오류(ex.Message);
        }
        finally
        {
            _조회중 = false;
        }
    }

    private void Set오류(string message)
    {
        오류메시지 = message;
        화면상태 = 기사홈화면상태.오류;
    }

    private static string Resolve주요행동경로(string actionCode) => actionCode switch
    {
        "VIEW_CURRENT_TRANSPORT" => DriverRoutes.CurrentTransport,
        "START_WORK" => DriverRoutes.WorkStart,
        "VIEW_RECOMMENDATIONS" => DriverRoutes.Recommendations,
        "VIEW_RESERVATION" => DriverRoutes.Reservations,
        "CHECK_NOTIFICATION" => DriverRoutes.NotificationSettings,
        "VIEW_SETTLEMENT" => DriverRoutes.CurrentMonthSettlement,
        _ => DriverRoutes.Recommendations
    };
}

public enum 기사홈화면상태
{
    불러오는중,
    준비됨,
    오류
}
