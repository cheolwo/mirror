using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>선택 주문의 명시적 취소 검토, 동일 시도 재전송과 정본 확인만 담당합니다.</summary>
public sealed class 주문자음식주문취소ViewModel(
    I주문자음식주문읽기Service readService,
    I주문자음식주문취소Service cancellationService) : ObservableObject, IDisposable
{
    private CancellationTokenSource _contextCancellation = new();
    private 주문자음식주문상세응답? _detail;
    private 취소시도? _attempt;
    private long _generation;
    private bool _active;
    private bool _disposed;
    private string? _orderNo;
    private string? _userId;
    private string _reasonCode = string.Empty;
    private string _reason = string.Empty;
    private bool _reviewConfirmed;
    private bool _opened;
    private bool _processing;
    private Api작업오류? _error;
    private string? _notice;

    public string? 주문번호 => _orderNo;
    public bool 작성열림 => _active && _opened;
    public bool 처리중 => _processing;
    public bool 입력잠김 => _processing || _attempt is not null;
    public bool 재확인필요 => _attempt is not null;
    public Api작업오류? 오류 => _error;
    public string? 안내 => _notice;
    public bool 취소가능 => _active && 취소행동 is not null;
    public bool 제출가능 => 취소가능 && !_processing && _opened
        && (_attempt is not null || (_reviewConfirmed && 유효사유));
    public Guid? 현재시도요청Id => _attempt?.요청.클라이언트요청Id;
    public 주문자음식주문상세응답? 확인한정본 { get; private set; }

    public string 사유Code
    {
        get => _reasonCode;
        set
        {
            if (!입력잠김 && SetProperty(ref _reasonCode, value ?? string.Empty))
            {
                _reviewConfirmed = false;
                변경알림();
            }
        }
    }

    public string 사유
    {
        get => _reason;
        set
        {
            if (!입력잠김 && SetProperty(ref _reason, value ?? string.Empty))
            {
                _reviewConfirmed = false;
                변경알림();
            }
        }
    }

    public bool 검토확인
    {
        get => _reviewConfirmed;
        set
        {
            if (!입력잠김 && SetProperty(ref _reviewConfirmed, value)) 변경알림();
        }
    }

    private 업무가능행동Dto? 취소행동 => _detail?.AvailableActions.FirstOrDefault(action =>
        action.ActionId == 음식배달가능행동Ids.주문취소
        && action.RevisionKindCode == 업무Revision종류Codes.음식주문
        && action.ExpectedRevision.HasValue);

    private bool 유효사유 => _reason.Length <= 500
        && (_reasonCode is 운영배차주문자취소사유Code.단순변심
            or 운영배차주문자취소사유Code.중복주문
            or 운영배차주문자취소사유Code.주소문제
            or 운영배차주문자취소사유Code.기타)
        && (_reasonCode != 운영배차주문자취소사유Code.기타 || !string.IsNullOrWhiteSpace(_reason));

    public void 문맥반영(string? orderNo, string? userId, 주문자음식주문상세응답? detail)
    {
        if (_disposed) return;
        var order = orderNo?.Trim();
        var owner = userId?.Trim();
        if (!string.Equals(order, _orderNo, StringComparison.Ordinal)
            || !string.Equals(owner, _userId, StringComparison.Ordinal))
        {
            초기화();
            _orderNo = order;
            _userId = owner;
        }
        if (!_active && _error?.Http상태코드 == 401)
        {
            _error = null;
            _notice = "로그인한 계정의 최신 주문을 확인했습니다. 취소는 직접 다시 확인해 주세요.";
        }
        _active = !string.IsNullOrWhiteSpace(order) && !string.IsNullOrWhiteSpace(owner);
        // 다른 주문의 응답으로 취소 행동을 열지 않습니다.
        _detail = detail is not null && detail.주문.주문번호 == order ? detail : null;
        if (_attempt is not null && _detail?.주문.상태 == 음식주문상태코드.취소)
        {
            확인한정본 = _detail;
            취소확정반영();
        }
        변경알림();
    }

    public void 작성시작()
    {
        if (!취소가능 || _processing) return;
        _opened = true;
        _error = null;
        변경알림();
    }

    public void 작성닫기()
    {
        if (_processing) return;
        _opened = false;
        // 결과 미확인 시도의 키는 닫았다 다시 열어도 보존합니다.
        if (_attempt is null)
        {
            _reasonCode = string.Empty;
            _reason = string.Empty;
            _reviewConfirmed = false;
        }
        변경알림();
    }

    public void 인증대기()
    {
        if (_disposed) return;
        수명무효화();
        _active = false;
        _detail = null;
        변경알림();
    }

    public void 세션초기화()
    {
        if (_disposed) return;
        초기화();
        _orderNo = null;
        _userId = null;
        변경알림();
    }

    public async Task<bool> 취소Async(CancellationToken cancellationToken = default)
    {
        if (_processing || _disposed) return false;
        if (!제출가능)
        {
            _error = new Api작업오류("validation", "취소 사유와 선택 주문을 확인해 주세요.");
            변경알림();
            return false;
        }

        _attempt ??= new 취소시도(_orderNo!, _userId!, new 주문자음식주문취소요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            예상Revision = 취소행동!.ExpectedRevision,
            사유Code = _reasonCode,
            사유 = _reason.Trim()
        });
        var attempt = _attempt!;
        var generation = _generation;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _contextCancellation.Token);
        var token = linked.Token;
        _processing = true;
        _error = null;
        _notice = null;
        확인한정본 = null;
        변경알림();

        try
        {
            Exception? failure = null;
            try
            {
                // 저장된 동일 시도의 값만 전송합니다. 입력/판본을 재시도 도중 바꾸지 않습니다.
                await cancellationService.취소Async(attempt.OrderNo, 복사(attempt.요청), token);
                현재문맥확인(generation, attempt, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            {
                현재문맥확인(generation, attempt, token);
                failure = ex;
                if (Api작업오류.변환(ex).Http상태코드 == 401)
                {
                    _error = Api작업오류.변환(ex);
                    _notice = "다시 로그인한 뒤 같은 주문의 취소 결과를 확인해 주세요.";
                    return false;
                }
            }

            주문자음식주문상세응답? canonical;
            try
            {
                canonical = await readService.상세Async(attempt.OrderNo, token);
                현재문맥확인(generation, attempt, token);
                if (canonical is null || canonical.주문.주문번호 != attempt.OrderNo)
                    throw new InvalidOperationException("선택한 주문의 취소 결과를 확인하지 못했습니다.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            {
                현재문맥확인(generation, attempt, token);
                _error = Api작업오류.변환(ex);
                _notice = "취소 결과를 확인하지 못했습니다. 같은 주문을 다시 확인해 주세요.";
                return false;
            }

            확인한정본 = canonical;
            _detail = canonical;
            if (canonical.주문.상태 == 음식주문상태코드.취소)
            {
                취소확정반영();
                return true;
            }

            _error = failure is not null ? Api작업오류.변환(failure)
                : new Api작업오류("result-unconfirmed", "현재 주문에서 취소가 확인되지 않았습니다.");
            if (_error.Http상태코드 == 409 || 취소행동 is null)
            {
                // 경합은 자동 재전송하지 않습니다. 최신 상태를 읽고 별도로 다시 검토합니다.
                _attempt = null;
                _reviewConfirmed = false;
                _notice = "주문 상태가 변경됐습니다. 최신 상태를 확인하고 취소 가능 여부를 다시 검토해 주세요.";
            }
            else
            {
                _notice = "취소 결과가 미확인입니다. 다시 확인하면 같은 취소 요청으로 조회·재시도합니다.";
            }
            return false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || generation != _generation)
        {
            return false;
        }
        finally
        {
            if (generation == _generation && !_disposed)
            {
                _processing = false;
                변경알림();
            }
        }
    }

    private void 현재문맥확인(long generation, 취소시도 attempt, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_disposed || !_active || generation != _generation
            || attempt.OrderNo != _orderNo || attempt.UserId != _userId)
            throw new OperationCanceledException(token);
    }

    private void 수명무효화()
    {
        _generation++;
        _contextCancellation.Cancel();
        _contextCancellation.Dispose();
        _contextCancellation = new CancellationTokenSource();
        _processing = false;
    }

    private void 초기화()
    {
        수명무효화();
        _detail = null;
        _active = false;
        _attempt = null;
        _reasonCode = string.Empty;
        _reason = string.Empty;
        _reviewConfirmed = false;
        _opened = false;
        _error = null;
        _notice = null;
        확인한정본 = null;
    }

    private void 변경알림() => OnPropertyChanged(string.Empty);

    private void 취소확정반영()
    {
        _attempt = null;
        _opened = false;
        _reviewConfirmed = false;
        _reason = string.Empty;
        _reasonCode = string.Empty;
        _error = null;
        _notice = "주문 취소를 확인했습니다.";
    }

    private static 주문자음식주문취소요청 복사(주문자음식주문취소요청 request)
        => new()
        {
            클라이언트요청Id = request.클라이언트요청Id,
            예상Revision = request.예상Revision,
            사유Code = request.사유Code,
            사유 = request.사유
        };

    public void Dispose()
    {
        if (_disposed) return;
        초기화();
        _disposed = true;
        _contextCancellation.Dispose();
    }

    private sealed record 취소시도(string OrderNo, string UserId, 주문자음식주문취소요청 요청);
}
