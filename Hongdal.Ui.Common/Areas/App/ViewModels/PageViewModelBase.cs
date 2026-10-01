using System.ComponentModel;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum PageViewModel상태
{
    대기,
    불러오는중,
    준비됨,
    실패,
    취소됨
}

public interface IPageViewModel : INotifyPropertyChanged, IDisposable
{
    PageViewModel상태 상태 { get; }
    bool 처리중 { get; }
    bool 초기화됨 { get; }
    string? 오류메시지 { get; }
    Task<bool> 초기화Async(CancellationToken cancellationToken = default);
    Task<bool> 새로고침Async(CancellationToken cancellationToken = default);
    void 취소();
}

/// <summary>
/// 페이지 단위 초기화, 새로고침, 취소와 오류 상태를 일관되게 관리합니다.
/// 실제 업무 상태는 하위 ViewModel이 소유하고 이 형식은 화면 수명만 조정합니다.
/// </summary>
public abstract class PageViewModelBase : 조립ViewModelBase, IPageViewModel
{
    private CancellationTokenSource? _현재작업취소;
    private PageViewModel상태 _상태 = PageViewModel상태.대기;
    private string? _오류메시지;

    public PageViewModel상태 상태
    {
        get => _상태;
        private set
        {
            if (SetProperty(ref _상태, value))
            {
                OnPropertyChanged(nameof(처리중));
                OnPropertyChanged(nameof(초기화됨));
            }
        }
    }

    public bool 처리중 => 상태 == PageViewModel상태.불러오는중;
    public bool 초기화됨 => 상태 == PageViewModel상태.준비됨;

    public string? 오류메시지
    {
        get => _오류메시지;
        private set => SetProperty(ref _오류메시지, value);
    }

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 초기화됨
            ? Task.FromResult(true)
            : 실행Async(새로고침: false, cancellationToken);

    public Task<bool> 새로고침Async(CancellationToken cancellationToken = default)
        => 실행Async(새로고침: true, cancellationToken);

    public void 취소()
        => _현재작업취소?.Cancel();

    protected abstract Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken);

    protected void 오류설정(string message)
    {
        오류메시지 = message;
        상태 = PageViewModel상태.실패;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _현재작업취소?.Cancel();
            _현재작업취소?.Dispose();
            _현재작업취소 = null;
        }

        base.Dispose(disposing);
    }

    private async Task<bool> 실행Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        if (처리중)
        {
            return false;
        }

        _현재작업취소?.Dispose();
        var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _현재작업취소 = operationCancellation;
        오류메시지 = null;
        상태 = PageViewModel상태.불러오는중;

        try
        {
            await 불러오기Async(새로고침, operationCancellation.Token);
            상태 = PageViewModel상태.준비됨;
            return true;
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            상태 = PageViewModel상태.취소됨;
            return false;
        }
        catch (Exception ex)
        {
            오류메시지 = ex.Message;
            상태 = PageViewModel상태.실패;
            return false;
        }
        finally
        {
            if (ReferenceEquals(_현재작업취소, operationCancellation))
            {
                _현재작업취소 = null;
            }

            operationCancellation.Dispose();
        }
    }
}
