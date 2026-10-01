using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace HumanResourcesManagerApp.ViewModels;

public sealed class 인사Api기능모음ViewModel : 조립ViewModelBase
{
    public 인사Api기능모음ViewModel(
        고용계약기능ViewModel 고용계약,
        참여혜택기능ViewModel 참여혜택,
        인사역할기능ViewModel 역할,
        사회보험신고기능ViewModel 사회보험,
        인사Controller기능모음ViewModel 인사Controllers,
        공통Controller기능모음ViewModel 공통Controllers)
    {
        this.고용계약 = 하위ViewModel등록(고용계약);
        this.참여혜택 = 하위ViewModel등록(참여혜택);
        this.역할 = 하위ViewModel등록(역할);
        this.사회보험 = 하위ViewModel등록(사회보험);
        업무목록 = [고용계약, 참여혜택, 역할, 사회보험];
        this.인사Controllers = 하위ViewModel등록(인사Controllers);
        this.공통Controllers = 하위ViewModel등록(공통Controllers);
    }

    public 고용계약기능ViewModel 고용계약 { get; }
    public 참여혜택기능ViewModel 참여혜택 { get; }
    public 인사역할기능ViewModel 역할 { get; }
    public 사회보험신고기능ViewModel 사회보험 { get; }
    public IReadOnlyList<인사업무ViewModelBase> 업무목록 { get; }
    public 인사Controller기능모음ViewModel 인사Controllers { get; }
    public 공통Controller기능모음ViewModel 공통Controllers { get; }
}
