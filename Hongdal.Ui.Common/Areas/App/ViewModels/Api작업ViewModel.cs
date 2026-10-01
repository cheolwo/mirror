using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum Api작업상태
{
    대기,
    처리중,
    성공,
    실패,
    취소됨
}

public sealed record Api작업완료
{
    public static Api작업완료 값 { get; } = new();

    private Api작업완료()
    {
    }
}

/// <summary>
/// 매개변수 없는 조회/명령 API 한 개의 실행 상태를 담당합니다.
/// </summary>
public sealed partial class Api작업ViewModel<TResult> : ObservableObject, IDisposable
{
    private readonly Func<CancellationToken, Task<TResult>> _실행;
    private CancellationTokenSource? _실행취소;

    public Api작업ViewModel(Func<CancellationToken, Task<TResult>> 실행)
    {
        _실행 = 실행;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(처리중))]
    [NotifyPropertyChangedFor(nameof(완료됨))]
    [NotifyPropertyChangedFor(nameof(성공함))]
    [NotifyPropertyChangedFor(nameof(오류발생))]
    [NotifyPropertyChangedFor(nameof(취소됨))]
    public partial Api작업상태 상태 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(결과있음))]
    public partial TResult? 결과 { get; private set; }

    [ObservableProperty]
    public partial string? 오류메시지 { get; private set; }

    [ObservableProperty]
    public partial Api작업오류? 오류 { get; private set; }

    [ObservableProperty]
    public partial DateTimeOffset? 마지막완료시각 { get; private set; }

    public bool 처리중 => 상태 == Api작업상태.처리중;
    public bool 완료됨 => 상태 is Api작업상태.성공 or Api작업상태.실패 or Api작업상태.취소됨;
    public bool 성공함 => 상태 == Api작업상태.성공;
    public bool 오류발생 => 상태 == Api작업상태.실패;
    public bool 취소됨 => 상태 == Api작업상태.취소됨;
    public bool 결과있음 => 결과 is not null;
    public bool 취소가능 => 처리중 && _실행취소 is not null;

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task 실행Async(CancellationToken cancellationToken = default)
    {
        if (처리중)
        {
            return;
        }

        결과 = default;
        오류메시지 = null;
        오류 = null;
        마지막완료시각 = null;
        상태 = Api작업상태.처리중;
        _실행취소?.Dispose();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _실행취소 = linkedCancellation;
        OnPropertyChanged(nameof(취소가능));

        try
        {
            결과 = await _실행(linkedCancellation.Token);
            상태 = Api작업상태.성공;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            상태 = Api작업상태.취소됨;
        }
        catch (Exception ex)
        {
            오류 = Api작업오류.변환(ex);
            오류메시지 = 오류.메시지;
            상태 = Api작업상태.실패;
        }
        finally
        {
            if (ReferenceEquals(_실행취소, linkedCancellation))
            {
                _실행취소 = null;
            }

            OnPropertyChanged(nameof(취소가능));
            마지막완료시각 = DateTimeOffset.Now;
        }
    }

    [RelayCommand]
    public void 취소() => _실행취소?.Cancel();

    [RelayCommand]
    public void 초기화()
    {
        if (처리중)
        {
            return;
        }

        결과 = default;
        오류메시지 = null;
        오류 = null;
        마지막완료시각 = null;
        상태 = Api작업상태.대기;
    }

    public void Dispose()
    {
        _실행취소?.Cancel();
        _실행취소?.Dispose();
        _실행취소 = null;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 요청 값 하나를 받아 실행하는 조회/명령 API 한 개의 실행 상태를 담당합니다.
/// </summary>
public sealed partial class Api작업ViewModel<TParameter, TResult> : ObservableObject, IDisposable
{
    private readonly Func<TParameter, CancellationToken, Task<TResult>> _실행;
    private CancellationTokenSource? _실행취소;

    public Api작업ViewModel(Func<TParameter, CancellationToken, Task<TResult>> 실행)
    {
        _실행 = 실행;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(처리중))]
    [NotifyPropertyChangedFor(nameof(완료됨))]
    [NotifyPropertyChangedFor(nameof(성공함))]
    [NotifyPropertyChangedFor(nameof(오류발생))]
    [NotifyPropertyChangedFor(nameof(취소됨))]
    public partial Api작업상태 상태 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(결과있음))]
    public partial TResult? 결과 { get; private set; }

    [ObservableProperty]
    public partial string? 오류메시지 { get; private set; }

    [ObservableProperty]
    public partial Api작업오류? 오류 { get; private set; }

    [ObservableProperty]
    public partial DateTimeOffset? 마지막완료시각 { get; private set; }

    public bool 처리중 => 상태 == Api작업상태.처리중;
    public bool 완료됨 => 상태 is Api작업상태.성공 or Api작업상태.실패 or Api작업상태.취소됨;
    public bool 성공함 => 상태 == Api작업상태.성공;
    public bool 오류발생 => 상태 == Api작업상태.실패;
    public bool 취소됨 => 상태 == Api작업상태.취소됨;
    public bool 결과있음 => 결과 is not null;
    public bool 취소가능 => 처리중 && _실행취소 is not null;

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task 실행Async(TParameter parameter, CancellationToken cancellationToken = default)
    {
        if (처리중)
        {
            return;
        }

        결과 = default;
        오류메시지 = null;
        오류 = null;
        마지막완료시각 = null;
        상태 = Api작업상태.처리중;
        _실행취소?.Dispose();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _실행취소 = linkedCancellation;
        OnPropertyChanged(nameof(취소가능));

        try
        {
            결과 = await _실행(parameter, linkedCancellation.Token);
            상태 = Api작업상태.성공;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            상태 = Api작업상태.취소됨;
        }
        catch (Exception ex)
        {
            오류 = Api작업오류.변환(ex);
            오류메시지 = 오류.메시지;
            상태 = Api작업상태.실패;
        }
        finally
        {
            if (ReferenceEquals(_실행취소, linkedCancellation))
            {
                _실행취소 = null;
            }

            OnPropertyChanged(nameof(취소가능));
            마지막완료시각 = DateTimeOffset.Now;
        }
    }

    [RelayCommand]
    public void 취소() => _실행취소?.Cancel();

    [RelayCommand]
    public void 초기화()
    {
        if (처리중)
        {
            return;
        }

        결과 = default;
        오류메시지 = null;
        오류 = null;
        마지막완료시각 = null;
        상태 = Api작업상태.대기;
    }

    public void Dispose()
    {
        _실행취소?.Cancel();
        _실행취소?.Dispose();
        _실행취소 = null;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 하위 ViewModel의 변경을 상위 PageViewModel까지 전달하는 조립용 기반 클래스입니다.
/// </summary>
public abstract class 조립ViewModelBase : ObservableObject, IDisposable
{
    private readonly HashSet<INotifyPropertyChanged> _children = [];
    private readonly HashSet<IDisposable> _ownedDisposables = [];
    private bool _disposed;

    protected T 하위ViewModel등록<T>(T child, bool 수명소유 = false)
        where T : class, INotifyPropertyChanged
    {
        if (_children.Add(child))
        {
            child.PropertyChanged += 하위ViewModel변경;
        }

        if (수명소유 && child is IDisposable disposable)
        {
            _ownedDisposables.Add(disposable);
        }

        return child;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var child in _children)
        {
            child.PropertyChanged -= 하위ViewModel변경;
        }

        foreach (var disposable in _ownedDisposables)
        {
            disposable.Dispose();
        }

        _children.Clear();
        _ownedDisposables.Clear();
    }

    private void 하위ViewModel변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);
}
