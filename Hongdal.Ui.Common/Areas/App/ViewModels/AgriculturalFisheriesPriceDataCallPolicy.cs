using System.Text.Json;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

internal static class 농수산공공데이터호출정책
{
    public static bool 연결실패예외(Exception exception)
        => exception is HttpRequestException or JsonException or TaskCanceledException;

    public static async Task<bool> 초기화시도Async(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (연결실패예외(ex))
        {
            return false;
        }
    }
}
