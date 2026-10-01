using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum 업무조각유형
{
    목록조회,
    상세조회,
    등록,
    수정,
    처리,
    삭제
}

public enum 명령위험수준
{
    일반,
    주의,
    파괴적
}

/// <summary>업무 ViewModel이 UI 종류와 무관하게 선택 항목을 제공하는 공통 표현입니다.</summary>
public sealed record 업무선택항목<TValue>(TValue 값, string 표시명, string? 설명 = null);

/// <summary>
/// MudBlazor 같은 UI 라이브러리에 종속되지 않고 명령 다이얼로그가 따라야 할 표현 정책을 제공합니다.
/// 실제 다이얼로그 컴포넌트는 이 값만 읽어 버튼 색상·확인 문구·성공 후 닫기를 결정할 수 있습니다.
/// </summary>
public sealed record 명령다이얼로그정책(
    string 제목,
    string 안내문구,
    string 확인버튼문구,
    string 취소버튼문구,
    명령위험수준 위험수준,
    bool 성공시닫기 = true)
{
    public bool 파괴적명령 => 위험수준 == 명령위험수준.파괴적;

    public static 명령다이얼로그정책 기본(string 업무명, 업무조각유형 업무유형)
        => 업무유형 switch
        {
            업무조각유형.등록 => new(
                업무명,
                "입력한 내용을 확인한 뒤 등록해 주세요.",
                "등록",
                "취소",
                명령위험수준.일반),
            업무조각유형.수정 => new(
                업무명,
                "변경한 내용을 확인한 뒤 저장해 주세요.",
                "저장",
                "취소",
                명령위험수준.일반),
            업무조각유형.삭제 => new(
                업무명,
                "삭제하면 되돌리기 어려울 수 있습니다. 대상을 다시 확인해 주세요.",
                "삭제",
                "취소",
                명령위험수준.파괴적),
            업무조각유형.처리 => new(
                업무명,
                "이 작업은 업무 상태를 변경할 수 있습니다. 실행 전에 내용을 확인해 주세요.",
                "실행",
                "취소",
                명령위험수준.주의),
            _ => throw new ArgumentOutOfRangeException(
                nameof(업무유형),
                업무유형,
                "조회 업무에는 명령 다이얼로그 정책을 만들 수 없습니다.")
        };
}

/// <summary>
/// Razor Class Library의 작은 화면 조각이 공통으로 주입받을 수 있는 업무 계약입니다.
/// 표현 방식은 테이블·카드·폼 중 자유롭게 선택하고 ViewModel은 업무 상태만 제공합니다.
/// </summary>
public interface I업무조각ViewModel : INotifyPropertyChanged
{
    string 업무코드 { get; }
    string 업무명 { get; }
    업무조각유형 업무유형 { get; }
    Api작업상태 상태 { get; }
    bool 처리중 { get; }
    bool 실행가능 { get; }
    string? 오류메시지 { get; }
    string? 성공메시지 { get; }
    현재사용자Snapshot 현재사용자 { get; }
    bool 사용자확인됨 { get; }
}

public interface I조회ViewModel : I업무조각ViewModel
{
}

public interface I목록조회ViewModel<out TItem> : I조회ViewModel
{
    IReadOnlyList<TItem> 항목목록 { get; }
    Task<bool> 조회Async(CancellationToken cancellationToken = default);
}

public interface I상세조회ViewModel<out TItem> : I조회ViewModel
{
    TItem? 항목 { get; }
    Task<bool> 조회Async(CancellationToken cancellationToken = default);
}

public interface I명령ViewModel<out TDraft> : I업무조각ViewModel
{
    TDraft 초안 { get; }
    명령다이얼로그정책 다이얼로그정책 => 명령다이얼로그정책.기본(업무명, 업무유형);
    Task<bool> 실행Async(CancellationToken cancellationToken = default);
}

public interface I등록ViewModel : I업무조각ViewModel
{
}

/// <summary>새 업무 대상을 만드는 명령 ViewModel입니다.</summary>
public interface I등록ViewModel<out TDraft> : I등록ViewModel, I명령ViewModel<TDraft>
{
}

public interface I수정ViewModel : I업무조각ViewModel
{
}

/// <summary>선택한 업무 대상을 정정하거나 변경하는 명령 ViewModel입니다.</summary>
public interface I수정ViewModel<out TDraft> : I수정ViewModel, I명령ViewModel<TDraft>
{
}

public interface I삭제ViewModel : I업무조각ViewModel
{
}

/// <summary>
/// 선택한 업무 대상을 삭제하거나, 이력 보존 대상인 경우 취소·폐기하는 명령 ViewModel입니다.
/// </summary>
public interface I삭제ViewModel<out TDraft> : I삭제ViewModel, I명령ViewModel<TDraft>
{
}

/// <summary>
/// 화면이 업무 단위의 조회·등록·수정·삭제 기능을 일관된 방법으로 조립할 수 있게 하는 계약입니다.
/// 원장형 업무의 수정·삭제는 정정·취소 명령으로 구현할 수 있습니다.
/// </summary>
public interface I업무단위CrudViewModel : INotifyPropertyChanged
{
    string 업무단위코드 { get; }
    string 업무단위명 { get; }
    I업무조각ViewModel 조회업무 { get; }
    I업무조각ViewModel 등록업무 { get; }
    I업무조각ViewModel 수정업무 { get; }
    I업무조각ViewModel 삭제업무 { get; }
    IReadOnlyList<I업무조각ViewModel> Crud업무목록 { get; }
    bool 처리중 { get; }
}

/// <summary>페이지가 주입받은 CRUD 업무 단위를 공통 방식으로 열거하는 조립 계약입니다.</summary>
public interface ICrudPageViewModel : INotifyPropertyChanged
{
    IReadOnlyList<I업무단위CrudViewModel> Crud업무단위목록 { get; }
}

public abstract class 업무조각ViewModelBase(
    string 업무코드,
    string 업무명,
    업무조각유형 업무유형) : 업무작업ViewModelBase, I업무조각ViewModel
{
    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
    public 업무조각유형 업무유형 { get; } = 업무유형;
}

/// <summary>
/// 기능 ViewModel 네 개를 주입받아 하나의 업무 단위로 제공하는 Page ViewModel 조립 기반입니다.
/// </summary>
public abstract class 업무단위CrudViewModelBase<TQuery, TCreate, TUpdate, TDelete>
    : 조립ViewModelBase, I업무단위CrudViewModel
    where TQuery : class, I조회ViewModel
    where TCreate : class, I등록ViewModel
    where TUpdate : class, I수정ViewModel
    where TDelete : class, I삭제ViewModel
{
    protected 업무단위CrudViewModelBase(
        string 업무단위코드,
        string 업무단위명,
        TQuery 조회,
        TCreate 등록,
        TUpdate 수정,
        TDelete 삭제,
        bool 하위수명소유 = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(업무단위코드);
        ArgumentException.ThrowIfNullOrWhiteSpace(업무단위명);
        업무유형확인(조회, 업무조각유형.목록조회, 업무조각유형.상세조회);
        업무유형확인(등록, 업무조각유형.등록);
        업무유형확인(수정, 업무조각유형.수정);
        업무유형확인(삭제, 업무조각유형.삭제);

        this.업무단위코드 = 업무단위코드;
        this.업무단위명 = 업무단위명;
        this.조회 = 하위ViewModel등록(조회, 하위수명소유);
        this.등록 = 하위ViewModel등록(등록, 하위수명소유);
        this.수정 = 하위ViewModel등록(수정, 하위수명소유);
        this.삭제 = 하위ViewModel등록(삭제, 하위수명소유);
        Crud업무목록 = [this.조회, this.등록, this.수정, this.삭제];
    }

    public string 업무단위코드 { get; }
    public string 업무단위명 { get; }
    public TQuery 조회 { get; }
    public TCreate 등록 { get; }
    public TUpdate 수정 { get; }
    public TDelete 삭제 { get; }
    public IReadOnlyList<I업무조각ViewModel> Crud업무목록 { get; }
    public bool 처리중 => Crud업무목록.Any(item => item.처리중);

    I업무조각ViewModel I업무단위CrudViewModel.조회업무 => 조회;
    I업무조각ViewModel I업무단위CrudViewModel.등록업무 => 등록;
    I업무조각ViewModel I업무단위CrudViewModel.수정업무 => 수정;
    I업무조각ViewModel I업무단위CrudViewModel.삭제업무 => 삭제;

    private static void 업무유형확인(I업무조각ViewModel viewModel, params 업무조각유형[] 허용유형)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (!허용유형.Contains(viewModel.업무유형))
        {
            throw new ArgumentException(
                $"'{viewModel.업무명}'의 업무 유형 '{viewModel.업무유형}'은(는) 이 CRUD 위치에 사용할 수 없습니다.",
                nameof(viewModel));
        }
    }
}

/// <summary>
/// 기존 업무 ViewModel의 공개 기능 하나를 RCL 주입 단위로 노출하는 얇은 어댑터입니다.
/// 원본 ViewModel과 실행 상태를 공유하므로 기존 화면과 새 조각 컴포넌트를 함께 사용할 수 있습니다.
/// </summary>
public abstract class 위임업무조각ViewModelBase<TViewModel> : ObservableObject, I업무조각ViewModel, IDisposable
    where TViewModel : 업무작업ViewModelBase
{
    protected 위임업무조각ViewModelBase(
        TViewModel 원본,
        string 업무코드,
        string 업무명,
        업무조각유형 업무유형)
    {
        this.원본 = 원본;
        this.업무코드 = 업무코드;
        this.업무명 = 업무명;
        this.업무유형 = 업무유형;
        원본.PropertyChanged += 원본상태변경;
    }

    protected TViewModel 원본 { get; }
    public string 업무코드 { get; }
    public string 업무명 { get; }
    public 업무조각유형 업무유형 { get; }
    public Api작업상태 상태 => 원본.상태;
    public bool 처리중 => 원본.처리중;
    public bool 실행가능 => 원본.실행가능;
    public string? 오류메시지 => 원본.오류메시지;
    public string? 성공메시지 => 원본.성공메시지;
    public 현재사용자Snapshot 현재사용자 => 원본.현재사용자;
    public bool 사용자확인됨 => 원본.사용자확인됨;

    public void Dispose()
    {
        원본.PropertyChanged -= 원본상태변경;
        GC.SuppressFinalize(this);
    }

    private void 원본상태변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);
}
