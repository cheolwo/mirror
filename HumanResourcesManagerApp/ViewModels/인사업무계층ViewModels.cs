using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace HumanResourcesManagerApp.ViewModels;

/// <summary>인사 기능이 공통으로 제공하는 업무 식별과 조립 수명 계약입니다.</summary>
public abstract class 인사업무ViewModelBase(
    string 업무코드,
    string 업무명,
    string 설명) : 조립ViewModelBase
{
    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
    public string 설명 { get; } = 설명;
}
