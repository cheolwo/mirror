using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>
/// 서버 또는 전송 계층이 명시적으로 허용한 멱등 요청만 같은 요청 객체로 한 번 더 실행합니다.
/// 일반 HTTP 실패나 업무 규칙 실패를 임의로 재시도하지 않습니다.
/// </summary>
public static class 업무멱등재시도실행기
{
    public static async Task<T> 한번Async<T>(
        Func<CancellationToken, Task<T>> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            return await command(cancellationToken);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException
            && 업무복구행동목록.포함(
                Api작업오류.변환(ex).복구가능행동,
                업무복구행동Ids.동일요청멱등재시도))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await command(cancellationToken);
        }
    }
}
