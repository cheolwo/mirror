using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Food;

/// <summary>선택한 주문의 자동 조회를 위치 공유와 분리하고, 선택·인증 작업과 겹치지 않게 조율합니다.</summary>
public sealed class 주문자음식주문새로고침Controller(
    주문자음식주문PageViewModel viewModel,
    bool appQueryEnabled = true) : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _appGate = new();
    private readonly List<CancellationTokenSource> _retiredAppCancellations = [];
    private CancellationTokenSource _appCancellation = new();
    private bool _appQueryEnabled = appQueryEnabled;
    private Task? _refreshTask;
    private int _stopped;
    private int _disposed;

    public bool 중지됨 => Volatile.Read(ref _stopped) != 0;

    public void 시작(Func<Func<Task>, Task> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (!중지됨)
        {
            _refreshTask ??= RefreshLoopAsync(dispatch, _lifetimeCancellation.Token);
        }
    }

    public Task<bool> 자동새로고침Async(CancellationToken cancellationToken = default)
        => ExecuteAsync(viewModel.주문진행새로고침Async, automatic: true, cancellationToken, requireActiveApp: true);

    /// <summary>비활성/연결 없음은 현재 자동 조회를 취소하고, 전경 복귀는 대기 중인 명령 뒤 정본을 읽습니다.</summary>
    public async Task 앱조회상태변경Async(bool enabled, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cancel = null;
        lock (_appGate)
        {
            if (중지됨 || _appQueryEnabled == enabled) return;
            _appQueryEnabled = enabled;
            if (!enabled)
            {
                cancel = _appCancellation;
            }
            else
            {
                _retiredAppCancellations.Add(_appCancellation);
                _appCancellation = new();
            }
        }

        if (!enabled)
        {
            cancel!.Cancel();
            viewModel.상세.조회상태무효화();
            return;
        }

        await ExecuteAsync(token => !viewModel.개인주문조회가능
            ? Task.CompletedTask
            : string.IsNullOrWhiteSpace(viewModel.상세.요청OrderNo)
                ? viewModel.목록새로고침Async(token)
                : viewModel.주문진행새로고침Async(token),
            automatic: false, cancellationToken, requireActiveApp: true);
    }

    public async Task 작업실행Async(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(action, automatic: false, cancellationToken);

    public Task 목록작업실행Async(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        // 로그아웃 뒤에 실행 순서를 얻은 이전 화면의 목록 요청은 조회하지 않습니다.
        return 작업실행Async(token => viewModel.개인주문조회가능
            ? action(token)
            : Task.CompletedTask, cancellationToken);
    }

    private bool 자동갱신가능
        => viewModel.개인주문조회가능
           && !viewModel.인증.처리중
           && !viewModel.상세.처리중
           && !viewModel.목록.처리중
           && !string.IsNullOrWhiteSpace(viewModel.상세.요청OrderNo)
           && !viewModel.상세.찾을수없음
           && viewModel.상세.상세?.주문.상태 is not (음식주문상태코드.전달완료
               or 음식주문상태코드.수령확인
               or 음식주문상태코드.거절
               or 음식주문상태코드.취소);

    private async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task> action,
        bool automatic,
        CancellationToken cancellationToken,
        bool requireActiveApp = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (중지됨)
        {
            return false;
        }

        CancellationToken appToken;
        lock (_appGate)
        {
            if (requireActiveApp && !_appQueryEnabled) return false;
            appToken = requireActiveApp ? _appCancellation.Token : CancellationToken.None;
        }
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetimeCancellation.Token, appToken);
        var token = linkedCancellation.Token;
        var entered = false;
        try
        {
            if (automatic)
            {
                entered = await _operationGate.WaitAsync(0, token);
            }
            else
            {
                await _operationGate.WaitAsync(token);
                entered = true;
            }

            if (!entered || 중지됨 || (automatic && !자동갱신가능))
            {
                return false;
            }

            token.ThrowIfCancellationRequested();
            await action(token);
            token.ThrowIfCancellationRequested();
            await viewModel.인증오류복구Async(token);
            token.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            if (entered)
            {
                _operationGate.Release();
            }
        }
    }

    private async Task RefreshLoopAsync(Func<Func<Task>, Task> dispatch, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await dispatch(async () => await 자동새로고침Async(cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public void 중지()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            _lifetimeCancellation.Cancel();
            lock (_appGate) _appCancellation.Cancel();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        중지();
        if (_refreshTask is not null)
        {
            await _refreshTask;
        }

        // 진행 중인 수동 조회도 취소·종료된 뒤 수명 자원을 정리합니다.
        await _operationGate.WaitAsync();
        _operationGate.Release();
        _lifetimeCancellation.Dispose();
        lock (_appGate)
        {
            _appCancellation.Dispose();
            foreach (var cancellation in _retiredAppCancellations) cancellation.Dispose();
            _retiredAppCancellations.Clear();
        }
    }
}
