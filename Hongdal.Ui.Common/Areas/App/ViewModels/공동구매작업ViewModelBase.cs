using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// API 기반 업무 ViewModel에 동일한 실행, 오류, 완료 상태를 제공합니다.
/// </summary>
public abstract class 업무작업ViewModelBase : ObservableObject
{
    private IHongdal현재사용자Context? _현재사용자Context;
    private Api작업상태 _상태;
    private Api작업오류? _오류;
    private string? _오류메시지;
    private string? _성공메시지;
    private DateTimeOffset? _마지막완료시각;
    private CancellationTokenSource? _실행취소;

    public Api작업상태 상태
    {
        get => _상태;
        private set
        {
            if (!SetProperty(ref _상태, value))
            {
                return;
            }

            OnPropertyChanged(nameof(처리중));
            OnPropertyChanged(nameof(실행가능));
            OnPropertyChanged(nameof(성공함));
            OnPropertyChanged(nameof(오류발생));
            OnPropertyChanged(nameof(취소됨));
        }
    }

    public string? 오류메시지
    {
        get => _오류메시지;
        private set => SetProperty(ref _오류메시지, value);
    }

    public Api작업오류? 오류
    {
        get => _오류;
        private set => SetProperty(ref _오류, value);
    }

    public string? 성공메시지
    {
        get => _성공메시지;
        private set => SetProperty(ref _성공메시지, value);
    }

    public DateTimeOffset? 마지막완료시각
    {
        get => _마지막완료시각;
        private set => SetProperty(ref _마지막완료시각, value);
    }

    public bool 처리중 => 상태 == Api작업상태.처리중;
    /// <summary>중복 제출을 막기 위해 현재 작업이 끝난 경우에만 새 조회·명령을 허용합니다.</summary>
    public bool 실행가능 => !처리중;
    public bool 성공함 => 상태 == Api작업상태.성공;
    public bool 오류발생 => 상태 == Api작업상태.실패;
    public bool 취소됨 => 상태 == Api작업상태.취소됨;
    public bool 취소가능 => 처리중 && _실행취소 is not null;
    public 현재사용자Snapshot 현재사용자
        => _현재사용자Context?.현재사용자 ?? 현재사용자Snapshot.익명;
    public bool 사용자확인됨 => 현재사용자.인증됨;

    protected void 현재사용자Context연결(IHongdal현재사용자Context? context)
    {
        _현재사용자Context = context;
        OnPropertyChanged(nameof(현재사용자));
        OnPropertyChanged(nameof(사용자확인됨));
    }

    public void 작업상태초기화()
    {
        if (처리중)
        {
            return;
        }

        오류메시지 = null;
        오류 = null;
        성공메시지 = null;
        마지막완료시각 = null;
        상태 = Api작업상태.대기;
    }

    protected bool 유효성실패(string message)
    {
        오류메시지 = message;
        오류 = new Api작업오류("validation", message);
        성공메시지 = null;
        마지막완료시각 = DateTimeOffset.Now;
        상태 = Api작업상태.실패;
        return false;
    }

    protected async Task<bool> 작업실행Async(
        Func<CancellationToken, Task> action,
        string successMessage,
        CancellationToken cancellationToken = default,
        Func<Exception, string>? errorMessageFactory = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (처리중)
        {
            return false;
        }

        오류메시지 = null;
        오류 = null;
        성공메시지 = null;
        마지막완료시각 = null;
        상태 = Api작업상태.처리중;
        _실행취소?.Dispose();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _실행취소 = linkedCancellation;
        OnPropertyChanged(nameof(취소가능));

        try
        {
            await action(linkedCancellation.Token);
            성공메시지 = successMessage;
            상태 = Api작업상태.성공;
            return true;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            상태 = Api작업상태.취소됨;
            return false;
        }
        catch (Exception ex)
        {
            오류 = Api작업오류.변환(ex);
            오류메시지 = errorMessageFactory?.Invoke(ex) ?? 오류.메시지;
            if (!string.Equals(오류메시지, 오류.메시지, StringComparison.Ordinal))
            {
                오류 = 오류 with { 메시지 = 오류메시지 };
            }
            상태 = Api작업상태.실패;
            return false;
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

    public void 작업취소() => _실행취소?.Cancel();
}

/// <summary>
/// 기존 공동구매 ViewModel의 호환성을 유지하면서 공통 업무 실행 상태를 재사용합니다.
/// 신규 업무 ViewModel은 <see cref="업무작업ViewModelBase"/>를 직접 상속합니다.
/// </summary>
public abstract class 공동구매작업ViewModelBase : 업무작업ViewModelBase
{
}
