namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public abstract class 농수산가격조회ViewModelBase : 조립ViewModelBase
{
    private bool _처리중;
    private string? _오류메시지;

    public bool 처리중
    {
        get => _처리중;
        private set => SetProperty(ref _처리중, value);
    }

    public string? 오류메시지
    {
        get => _오류메시지;
        protected set => SetProperty(ref _오류메시지, value);
    }

    protected async Task 조회실행Async(
        Func<CancellationToken, Task> operation,
        string connectionErrorMessage,
        CancellationToken cancellationToken)
    {
        if (처리중)
        {
            return;
        }

        오류메시지 = null;
        처리중 = true;
        try
        {
            await operation(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (농수산공공데이터호출정책.연결실패예외(ex))
        {
            오류메시지 = connectionErrorMessage;
        }
        finally
        {
            처리중 = false;
        }
    }
}
