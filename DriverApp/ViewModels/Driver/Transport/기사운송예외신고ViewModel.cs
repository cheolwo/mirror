using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Services;
using MudBlazor;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사운송예외신고ViewModel : ObservableObject, IDisposable
{
    private readonly IDriverTransportExceptionService _exceptionService;
    private readonly 기사운송작업상태ViewModel _상태;
    private readonly string _단계;
    private readonly string _기본유형;
    private readonly Func<string, string> _코드변환;
    private readonly Func<bool> _현재문맥;
    private readonly IDriverCargoExceptionRecovery? _복구;
    private WorkflowRecoverySnapshot? _복구문맥;
    private Guid? _보호기록Id;
    private bool _복구확인;
    private long _운송Id;
    private long _문맥판본;
    private CancellationTokenSource? _신고취소;
    private bool _disposed;

    public 기사운송예외신고ViewModel(
        IDriverTransportExceptionService exceptionService,
        기사운송작업상태ViewModel 상태,
        string 단계,
        IReadOnlyList<string> 유형목록,
        string 기본유형,
        Func<string, string> 코드변환,
        Func<bool>? isContextCurrent = null,
        IDriverCargoExceptionRecovery? recovery = null)
    {
        _exceptionService = exceptionService;
        _상태 = 상태;
        _단계 = 단계;
        _기본유형 = 기본유형;
        _코드변환 = 코드변환;
        _현재문맥 = isContextCurrent ?? (() => true);
        _복구 = recovery;
        _복구확인 = recovery is null;
        this.유형목록 = 유형목록;
        유형 = 기본유형;
    }

    public IReadOnlyList<string> 유형목록 { get; }

    [ObservableProperty]
    public partial bool 열림 { get; set; }

    [ObservableProperty]
    public partial string 유형 { get; set; }

    [ObservableProperty]
    public partial string? 메모 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(신고가능))]
    [NotifyCanExecuteChangedFor(nameof(신고Command))]
    public partial bool 신고중 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(신고가능))]
    [NotifyCanExecuteChangedFor(nameof(신고Command))]
    public partial bool 결과확인필요 { get; private set; }

    public bool 신고가능 => !_disposed && _운송Id > 0 && _현재문맥() && !신고중 && !결과확인필요
        && _복구확인 && (_복구 is null || _복구문맥 is not null && _복구.IsCurrent(_복구문맥));

    public void 운송설정(long transportId)
    {
        ++_문맥판본;
        _신고취소?.Cancel();
        _신고취소 = null;
        _운송Id = transportId;
        열림 = false;
        유형 = _기본유형;
        메모 = null;
        신고중 = false;
        결과확인필요 = false;
        _복구문맥 = null;
        _보호기록Id = null;
        _복구확인 = _복구 is null;
        OnPropertyChanged(nameof(신고가능));
        신고Command.NotifyCanExecuteChanged();
    }

    /// <summary>같은 운송 GET 뒤 보호 기록을 복원합니다. 서버의 과거 신고 표시로 이 기록을 해제하지 않습니다.</summary>
    public async Task 복원Async(CancellationToken cancellationToken = default)
    {
        if (_복구 is null || _disposed || _운송Id <= 0 || !_현재문맥() || 신고중) return;
        var version = _문맥판본;
        var transportId = _운송Id;
        WorkflowRecoverySnapshot? snapshot = null;
        bool Current() => !_disposed && version == _문맥판본 && transportId == _운송Id && _현재문맥()
            && (snapshot is null || _복구.IsCurrent(snapshot));
        _복구확인 = false;
        try
        {
            snapshot = _복구.Capture();
            if (_복구문맥 is not null && _복구문맥 != snapshot)
            {
                _보호기록Id = null;
                메모 = null;
                유형 = _기본유형;
                열림 = false;
            }
            _복구문맥 = snapshot;
            var record = await _복구.LoadAsync(snapshot, _단계, transportId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!Current()) return;
            if (record is null && _보호기록Id is not null)
                throw new InvalidDataException("이전 신고 보호 기록을 다시 확인하지 못했습니다.");
            _보호기록Id = record?.RequestId;
            _복구확인 = true;
            결과확인필요 = record is not null;
            if (결과확인필요) 미확정안내();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || !Current()) { }
        catch (Exception)
        {
            if (!Current()) return;
            결과확인필요 = true;
            _상태.설정("이전 신고 보호 기록을 읽지 못했습니다. 새 신고를 보내지 않고 운송을 다시 불러와 확인해 주세요.", Severity.Warning);
        }
        finally
        {
            if (Current())
            {
                OnPropertyChanged(nameof(신고가능));
                신고Command.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(신고가능))]
    public async Task 신고Async()
    {
        if (!신고가능) return;
        var transportId = _운송Id;
        var version = _문맥판본;
        var issue = 유형;
        var memo = string.IsNullOrWhiteSpace(메모) ? "메모 없음" : 메모.Trim();
        var snapshot = _복구문맥;
        var requestStarted = false;
        using var cancellation = new CancellationTokenSource();
        _신고취소 = cancellation;
        bool Current() => !_disposed && version == _문맥판본 && transportId == _운송Id
            && ReferenceEquals(_신고취소, cancellation) && !cancellation.IsCancellationRequested && _현재문맥()
            && (_복구 is null || snapshot is not null && _복구.IsCurrent(snapshot));
        try
        {
            신고중 = true;
            _상태.설정($"{_단계} 문제를 서버에 신고하는 중입니다.", Severity.Info);

            if (_복구 is not null)
            {
                _복구확인 = false;
                var record = await _복구.BeginAsync(snapshot!, _단계, transportId, Guid.NewGuid(), cancellation.Token);
                if (!Current()) return;
                _보호기록Id = record.RequestId;
                _복구확인 = true;
            }

            requestStarted = true;
            var result = await _exceptionService.ReportAsync(new DriverTransportExceptionReport(
                TransportId: transportId,
                Stage: _단계,
                ExceptionCode: _코드변환(issue),
                Reason: issue,
                Memo: memo,
                RequestAdminReview: true), cancellation.Token);

            if (!Current()) return;
            결과확인필요 = result.OutcomeUnknown || result.Reported && result.Transport?.Id != transportId;
            if (결과확인필요)
            {
                미확정안내();
                return;
            }

            if (_복구 is not null && _보호기록Id is { } recordId)
            {
                await _복구.ClearAsync(snapshot!, _단계, transportId, recordId, cancellation.Token);
                if (!Current()) return;
                _보호기록Id = null;
            }

            _상태.설정(
                result.Reported
                    ? $"{result.Message} {issue} · {memo}"
                    : result.Message,
                result.Reported ? Severity.Warning : Severity.Error);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!Current()) return;
            if (requestStarted)
            {
                결과확인필요 = true;
                미확정안내();
            }
            else
            {
                _복구확인 = false;
                _상태.설정("신고 전 보호 보관을 확인하지 못해 전송하지 않았습니다. 운송을 다시 불러와 확인해 주세요.", Severity.Warning);
            }
        }
        finally
        {
            if (ReferenceEquals(_신고취소, cancellation))
            {
                _신고취소 = null;
                신고중 = false;
                OnPropertyChanged(nameof(신고가능));
                신고Command.NotifyCanExecuteChanged();
            }
        }
    }

    private void 미확정안내()
        => _상태.설정("이전 신고의 처리 결과를 확인하지 못했습니다. 중복 신고하지 말고 현재 운송을 확인하거나 운영자에게 확인해 주세요.", Severity.Warning);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        운송설정(0);
    }
}
