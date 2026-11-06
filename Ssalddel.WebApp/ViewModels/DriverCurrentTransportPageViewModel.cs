using System.Net;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.WebApp.Services;

namespace Ssalddel.WebApp.ViewModels;

public enum DriverCurrentTransportMessageTone
{
    Info,
    Success,
    Warning,
    Error
}

public enum DriverCurrentTransportReadState
{
    Loading,
    Loaded,
    Empty,
    LoginRequired,
    AccessDenied,
    Failed
}

public sealed class DriverCurrentTransportPageViewModel : 조립ViewModelBase
{
    private const int AcceptedTransportRetryCount = 5;
    private static readonly TimeSpan AcceptedTransportRetryDelay = TimeSpan.FromMilliseconds(700);

    private readonly Func<CancellationToken, Task<기사운송요약응답?>> _loadCurrentTransport;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly DriverCurrentTransportRefreshSession _refreshSession;
    private 기사운송요약응답? _currentTransport;
    private DriverCurrentTransportReadState _readState;
    private string? _acceptedRequestId;
    private string? _statusMessage;
    private DriverCurrentTransportMessageTone _statusTone = DriverCurrentTransportMessageTone.Info;
    private bool _isWaitingForAcceptedTransport;
    private int _acceptedTransportRetryAttempt;
    private bool _initialized;

    public DriverCurrentTransportPageViewModel(
        기사운송증빙Service transportService,
        ITransportRequestLedgerObserver ledgerObserver)
        : this(
            transportService.현재운송조회Async,
            ledgerObserver)
    {
    }

    public DriverCurrentTransportPageViewModel(
        Func<CancellationToken, Task<기사운송요약응답?>> loadCurrentTransport,
        ITransportRequestLedgerObserver ledgerObserver)
    {
        _loadCurrentTransport = loadCurrentTransport;
        Refresh = 하위ViewModel등록(
            new DriverCurrentTransportRefreshViewModel(
                LoadCurrentTransportCoreAsync,
                SetStatus,
                _lifetimeCancellation.Token),
            수명소유: true);
        _refreshSession = new DriverCurrentTransportRefreshSession(
            ledgerObserver,
            IsCurrentTransportRequest,
            () => !Refresh.IsBusy && (CurrentTransport is not null || HasAcceptedRequestContext));
        _refreshSession.RefreshRequested += HandleRefreshRequested;
    }

    public DriverCurrentTransportRefreshViewModel Refresh { get; }
    public DriverCurrentTransportReadState ReadState
    {
        get => _readState;
        private set => SetProperty(ref _readState, value);
    }


    public 기사운송요약응답? CurrentTransport
    {
        get => _currentTransport;
        private set
        {
            if (!SetProperty(ref _currentTransport, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAcceptedRequestLoaded));
            OnPropertyChanged(nameof(AcceptedTransportGuideText));
        }
    }

    public string? AcceptedRequestId
    {
        get => _acceptedRequestId;
        private set
        {
            if (!SetProperty(ref _acceptedRequestId, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasAcceptedRequestContext));
            OnPropertyChanged(nameof(IsAcceptedRequestLoaded));
            OnPropertyChanged(nameof(AcceptedTransportGuideText));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public DriverCurrentTransportMessageTone StatusTone
    {
        get => _statusTone;
        private set => SetProperty(ref _statusTone, value);
    }

    public bool IsWaitingForAcceptedTransport
    {
        get => _isWaitingForAcceptedTransport;
        private set
        {
            if (SetProperty(ref _isWaitingForAcceptedTransport, value))
            {
                OnPropertyChanged(nameof(AcceptedTransportGuideText));
            }
        }
    }

    public int AcceptedTransportRetryAttempt
    {
        get => _acceptedTransportRetryAttempt;
        private set
        {
            if (SetProperty(ref _acceptedTransportRetryAttempt, value))
            {
                OnPropertyChanged(nameof(AcceptedTransportGuideText));
            }
        }
    }

    public bool HasAcceptedRequestContext => !string.IsNullOrWhiteSpace(AcceptedRequestId);

    public bool IsAcceptedRequestLoaded
        => !HasAcceptedRequestContext
           || string.Equals(
               CurrentTransport?.운송번호,
               AcceptedRequestId,
               StringComparison.OrdinalIgnoreCase);

    public string AcceptedTransportGuideText
        => IsWaitingForAcceptedTransport
            ? $"{AcceptedRequestId} 수락을 현재 운송으로 전환하는 중입니다. 자동 조회 {AcceptedTransportRetryAttempt}/{AcceptedTransportRetryCount}"
            : IsAcceptedRequestLoaded
                ? $"{AcceptedRequestId} 수락 건이 현재 운송으로 연결되었습니다."
                : $"{AcceptedRequestId} 수락 건과 다른 현재 운송이 조회되었습니다. 새로고침으로 다시 확인해 주세요.";

    public async Task InitializeAsync(
        string? acceptedRequestId,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequestId = Normalize(acceptedRequestId);
        if (_initialized
            && string.Equals(AcceptedRequestId, normalizedRequestId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AcceptedRequestId = normalizedRequestId;
        AcceptedTransportRetryAttempt = 0;
        _refreshSession.Start();
        _initialized = true;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        await TryLoadCurrentTransportAsync(linkedCancellation.Token);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshSession.RefreshRequested -= HandleRefreshRequested;
            _refreshSession.Dispose();
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task TryLoadCurrentTransportAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadCurrentTransportCoreAsync(cancellationToken);
            if (HasAcceptedRequestContext && !IsAcceptedRequestLoaded)
            {
                await WaitForAcceptedTransportAsync(null, cancellationToken);
                return;
            }

            if (HasAcceptedRequestContext)
            {
                SetStatus(
                    "추천 수락 후 현재 운송을 조회했습니다. 다음 행동을 확인해 주세요.",
                    DriverCurrentTransportMessageTone.Success);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (HasAcceptedRequestContext)
            {
                await WaitForAcceptedTransportAsync(ex.Message, cancellationToken);
                return;
            }

            SetStatus(
                $"현재 운송을 아직 불러오지 못했습니다. {ex.Message}",
                DriverCurrentTransportMessageTone.Info);
        }
    }

    private async Task LoadCurrentTransportCoreAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (CurrentTransport is null)
            {
                ReadState = DriverCurrentTransportReadState.Loading;
            }

            var transport = await _loadCurrentTransport(cancellationToken);
            CurrentTransport = transport;
            ReadState = transport is null ? DriverCurrentTransportReadState.Empty : DriverCurrentTransportReadState.Loaded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            ReadState = Api작업오류.변환(ex).Http상태코드 switch
            {
                (int)HttpStatusCode.Unauthorized => DriverCurrentTransportReadState.LoginRequired,
                (int)HttpStatusCode.Forbidden => DriverCurrentTransportReadState.AccessDenied,
                _ => DriverCurrentTransportReadState.Failed
            };
            throw;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void HandleRefreshRequested()
        => _ = RefreshFromLedgerAsync(_lifetimeCancellation.Token);

    private async Task RefreshFromLedgerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadCurrentTransportCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SetStatus(
                $"현재 운송 자동 갱신을 완료하지 못했습니다. {ex.Message}",
                DriverCurrentTransportMessageTone.Info);
        }
    }

    private bool IsCurrentTransportRequest(string requestId)
        => (!string.IsNullOrWhiteSpace(CurrentTransport?.운송번호)
            && string.Equals(CurrentTransport.운송번호, requestId, StringComparison.OrdinalIgnoreCase))
           || (!string.IsNullOrWhiteSpace(AcceptedRequestId)
               && string.Equals(AcceptedRequestId, requestId, StringComparison.OrdinalIgnoreCase));

    private async Task WaitForAcceptedTransportAsync(
        string? firstFailureMessage,
        CancellationToken cancellationToken)
    {
        IsWaitingForAcceptedTransport = true;

        try
        {
            for (var attempt = 1; attempt <= AcceptedTransportRetryCount; attempt++)
            {
                AcceptedTransportRetryAttempt = attempt;
                if (attempt > 1)
                {
                    await Task.Delay(AcceptedTransportRetryDelay, cancellationToken);
                }

                try
                {
                    await LoadCurrentTransportCoreAsync(cancellationToken);
                    if (IsAcceptedRequestLoaded)
                    {
                        SetStatus(
                            "배차 수락으로 생성된 현재 운송을 확인했습니다.",
                            DriverCurrentTransportMessageTone.Success);
                        return;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var message = attempt == 1 && !string.IsNullOrWhiteSpace(firstFailureMessage)
                        ? firstFailureMessage
                        : ex.Message;
                    SetStatus(
                        $"현재 운송 생성 여부를 확인하는 중입니다. {message}",
                        DriverCurrentTransportMessageTone.Info);
                }
            }

            if (CurrentTransport is null)
            {
                SetStatus(
                    "수락은 전송됐지만 현재 운송 건을 아직 찾지 못했습니다. 잠시 후 현재 운송 새로고침을 눌러 주세요.",
                    DriverCurrentTransportMessageTone.Warning);
                return;
            }

            SetStatus(
                $"{AcceptedRequestId} 수락 건 대신 {CurrentTransport.운송번호} 운송이 조회되었습니다. 기존 진행 운송이 있거나 서버 사후처리를 확인해야 합니다.",
                DriverCurrentTransportMessageTone.Warning);
        }
        finally
        {
            IsWaitingForAcceptedTransport = false;
        }
    }

    private void SetStatus(string message, DriverCurrentTransportMessageTone tone)
    {
        StatusTone = tone;
        StatusMessage = message;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
