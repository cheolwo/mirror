namespace DriverApp.ViewModels.Driver.Features;

/// <summary>
/// 전체 기사 기능이 필요한 페이지를 위한 기본 조립 예시입니다.
/// 가벼운 페이지는 이 모음 대신 필요한 기능 ViewModel만 직접 주입하면 됩니다.
/// </summary>
public sealed class 기사Api기능모음ViewModel : 조립ViewModelBase
{
    public 기사Api기능모음ViewModel(
        기사프로필기능ViewModel 프로필,
        기사근무기능ViewModel 근무,
        기사추천기능ViewModel 추천,
        기사탐색캠페인기능ViewModel 탐색캠페인,
        기사배차액션기능ViewModel 배차액션,
        기사예약기능ViewModel 예약,
        기사운송기능ViewModel 운송,
        기사설정기능ViewModel 설정,
        기사정산기능ViewModel 정산,
        기사알림기능ViewModel 알림,
        기사Command기능설정ViewModel Command기능설정,
        기사개발도구기능ViewModel 개발도구,
        기사Controller기능모음ViewModel 기사Controllers,
        공통Controller기능모음ViewModel 공통Controllers)
    {
        this.프로필 = 하위ViewModel등록(프로필);
        this.근무 = 하위ViewModel등록(근무);
        this.추천 = 하위ViewModel등록(추천);
        this.탐색캠페인 = 하위ViewModel등록(탐색캠페인);
        this.배차액션 = 하위ViewModel등록(배차액션);
        this.예약 = 하위ViewModel등록(예약);
        this.운송 = 하위ViewModel등록(운송);
        this.설정 = 하위ViewModel등록(설정);
        this.정산 = 하위ViewModel등록(정산);
        this.알림 = 하위ViewModel등록(알림);
        this.Command기능설정 = 하위ViewModel등록(Command기능설정);
        this.개발도구 = 하위ViewModel등록(개발도구);
        this.기사Controllers = 하위ViewModel등록(기사Controllers);
        this.공통Controllers = 하위ViewModel등록(공통Controllers);
    }

    public 기사프로필기능ViewModel 프로필 { get; }
    public 기사근무기능ViewModel 근무 { get; }
    public 기사추천기능ViewModel 추천 { get; }
    public 기사탐색캠페인기능ViewModel 탐색캠페인 { get; }
    public 기사배차액션기능ViewModel 배차액션 { get; }
    public 기사예약기능ViewModel 예약 { get; }
    public 기사운송기능ViewModel 운송 { get; }
    public 기사설정기능ViewModel 설정 { get; }
    public 기사정산기능ViewModel 정산 { get; }
    public 기사알림기능ViewModel 알림 { get; }
    public 기사Command기능설정ViewModel Command기능설정 { get; }
    public 기사개발도구기능ViewModel 개발도구 { get; }
    public 기사Controller기능모음ViewModel 기사Controllers { get; }
    public 공통Controller기능모음ViewModel 공통Controllers { get; }
}
