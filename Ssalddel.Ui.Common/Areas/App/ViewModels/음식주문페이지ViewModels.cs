using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>로그인 주문자의 음식 주문 검색·상태 필터·서버 페이징만 담당합니다.</summary>
public sealed partial class 주문자음식주문목록ViewModel(
    I주문자음식주문읽기Service service) : 업무작업ViewModelBase
{
    private const int DefaultPageSize = 12;
    private long _sessionGeneration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(검색조건있음))]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial string 검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(검색조건있음))]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial string? 상태필터 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(주문목록))]
    [NotifyPropertyChangedFor(nameof(전체건수))]
    [NotifyPropertyChangedFor(nameof(현재페이지))]
    [NotifyPropertyChangedFor(nameof(총페이지수))]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial 주문자음식주문목록응답 응답 { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(원장없음))]
    [NotifyPropertyChangedFor(nameof(검색결과없음))]
    public partial bool 초기화됨 { get; private set; }

    public IReadOnlyList<주문자음식주문요약응답> 주문목록 => 응답.Items;
    public int 전체건수 => 응답.TotalCount;
    public int 현재페이지 => 응답.Page;
    public int 총페이지수 => Math.Max(1, (int)Math.Ceiling(전체건수 / (double)Math.Max(1, 응답.PageSize)));
    public bool 검색조건있음 => !string.IsNullOrWhiteSpace(검색어) || !string.IsNullOrWhiteSpace(상태필터);
    public bool 원장없음 => 초기화됨 && 전체건수 == 0 && !검색조건있음;
    public bool 검색결과없음 => 초기화됨 && 전체건수 == 0 && 검색조건있음;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 페이지조회Async(1, cancellationToken);

    public Task<bool> 페이지조회Async(int page, CancellationToken cancellationToken = default)
    {
        var generation = _sessionGeneration;
        return 작업실행Async(
            async token =>
            {
                var response = await service.목록Async(new 주문자음식주문목록조회요청
                {
                    검색어 = 검색어,
                    상태 = 상태필터,
                    Page = Math.Max(1, page),
                    PageSize = DefaultPageSize
                }, token);
                token.ThrowIfCancellationRequested();
                if (generation != _sessionGeneration) throw new OperationCanceledException(token);
                응답 = response;
                초기화됨 = true;
            },
            "내 음식 주문 목록을 불러왔습니다.",
            cancellationToken,
            ex => $"내 음식 주문 목록을 불러오지 못했습니다. {ex.Message}");
    }

    public void 필터초기화()
    {
        검색어 = string.Empty;
        상태필터 = null;
    }

    public void 세션초기화()
    {
        _sessionGeneration++;
        작업취소();
        응답 = new 주문자음식주문목록응답();
        초기화됨 = false;
        필터초기화();
        작업상태초기화();
    }
}

/// <summary>명시적으로 선택한 정확한 orderNo 한 건의 소유자 상세만 담당합니다.</summary>
public sealed partial class 주문자음식주문상세ViewModel(
    I주문자음식주문읽기Service service,
    I주문자음식주문수령확인Service receiptConfirmationService) : 업무작업ViewModelBase
{
    private Guid? _수령확인요청Id;
    private long _selectionGeneration;

    [ObservableProperty]
    public partial string? 요청OrderNo { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(수령확인가능))]
    [NotifyPropertyChangedFor(nameof(수령확인표시))]
    public partial 주문자음식주문상세응답? 상세 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(수령확인가능))]
    public partial bool 최신상태확인됨 { get; private set; }

    [ObservableProperty]
    public partial bool 찾을수없음 { get; private set; }

    [ObservableProperty]
    public partial string 수령확인메모 { get; set; } = string.Empty;

    public bool 수령확인표시 => 업무가능행동목록.포함(
        상세?.AvailableActions,
        음식배달가능행동Ids.주문수령확인);

    public bool 수령확인가능 => 최신상태확인됨 && !처리중 && 수령확인표시;

    public void 조회상태무효화()
    {
        작업취소();
        최신상태확인됨 = false;
    }

    public Task<bool> 조회Async(string orderNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return Task.FromResult(유효성실패("조회할 음식 주문번호를 확인해 주세요."));
        }

        var normalizedOrderNo = orderNo.Trim();
        if (!string.Equals(요청OrderNo, normalizedOrderNo, StringComparison.Ordinal))
        {
            _selectionGeneration++;
            작업취소();
            // 같은 주문의 실패 재시도는 보존하고, 다른 주문에는 입력과 멱등 키를 넘기지 않습니다.
            _수령확인요청Id = null;
            수령확인메모 = string.Empty;
            상세 = null;
        }

        요청OrderNo = normalizedOrderNo;
        var generation = _selectionGeneration;
        // 같은 주문의 이전 성공 정보는 읽을 수 있지만 정본 확인 전 변경 행동은 잠급니다.
        최신상태확인됨 = false;
        찾을수없음 = false;
        return 작업실행Async(
            async token =>
            {
                var response = await service.상세Async(normalizedOrderNo, token);
                token.ThrowIfCancellationRequested();
                if (generation != _selectionGeneration) throw new OperationCanceledException(token);
                if (response is not null && response.주문.주문번호 != normalizedOrderNo)
                    throw new InvalidOperationException("선택한 음식 주문과 상세 응답이 일치하지 않습니다.");
                상세 = response;
                찾을수없음 = 상세 is null;
                최신상태확인됨 = 상세 is not null;
            },
            "음식 주문 상세를 불러왔습니다.",
            cancellationToken,
            ex =>
            {
                접근오류상세제거(ex);
                return $"음식 주문 상세를 불러오지 못했습니다. {ex.Message}";
            });
    }

    public Task<bool> 수령확인Async(CancellationToken cancellationToken = default)
    {
        if (!수령확인가능 || string.IsNullOrWhiteSpace(요청OrderNo))
        {
            return Task.FromResult(유효성실패("기사 전달 완료 뒤에만 주문 수령을 확인할 수 있습니다."));
        }

        if (수령확인메모?.Length > 500)
        {
            return Task.FromResult(유효성실패("수령 확인 메모는 500자 이내로 입력해 주세요."));
        }

        if (!_수령확인요청Id.HasValue)
        {
            _수령확인요청Id = Guid.NewGuid();
        }

        var orderNo = 요청OrderNo;
        var requestId = _수령확인요청Id.Value;
        var memo = 수령확인메모?.Trim() ?? string.Empty;
        var generation = _selectionGeneration;
        최신상태확인됨 = false;

        return 작업실행Async(
            async token =>
            {
                await receiptConfirmationService.수령확인Async(
                    orderNo,
                    new 주문자음식주문수령확인요청
                    {
                        클라이언트요청Id = requestId,
                        확인메모 = memo
                    },
                    token);
                token.ThrowIfCancellationRequested();
                if (generation != _selectionGeneration) throw new OperationCanceledException(token);
                var response = await service.상세Async(orderNo, token)
                    ?? throw new InvalidOperationException("수령 확인한 음식 주문을 다시 조회할 수 없습니다.");
                token.ThrowIfCancellationRequested();
                if (generation != _selectionGeneration) throw new OperationCanceledException(token);
                if (response.주문.주문번호 != orderNo)
                    throw new InvalidOperationException("수령 확인한 음식 주문과 상세 응답이 일치하지 않습니다.");
                상세 = response;
                찾을수없음 = false;
                최신상태확인됨 = true;
                _수령확인요청Id = null;
                수령확인메모 = string.Empty;
            },
            "음식 수령을 확인했습니다.",
            cancellationToken,
            ex =>
            {
                접근오류상세제거(ex);
                return $"음식 수령을 확인하지 못했습니다. {ex.Message}";
            });
    }

    private void 접근오류상세제거(Exception exception)
    {
        // 만료·소유권 거절은 일시적 연결 실패와 달리 이전 개인정보도 남기지 않습니다.
        if (Api작업오류.변환(exception).Http상태코드 is 401 or 403) 상세 = null;
    }

    public void 선택해제()
    {
        _selectionGeneration++;
        작업취소();
        요청OrderNo = null;
        상세 = null;
        최신상태확인됨 = false;
        찾을수없음 = false;
        _수령확인요청Id = null;
        수령확인메모 = string.Empty;
        작업상태초기화();
    }
}

/// <summary>기능 접근, 인증, 목록과 정확한 상세를 조립하고 음식 주문 내역 페이지 흐름만 조율합니다.</summary>
public sealed class 주문자음식주문PageViewModel : 조립ViewModelBase
{
    private bool _재로그인필요;
    private bool _인증복구중;
    private string? _콘텐츠UserId;
    private string? _복구UserId;
    private bool _disposed;

    public 주문자음식주문PageViewModel(
        음식배달페이지접근ViewModel access,
        주문자앱인증ViewModel authentication,
        주문자음식주문목록ViewModel list,
        주문자음식주문상세ViewModel detail,
        주문자음식주문취소ViewModel? cancellation = null)
    {
        접근 = 하위ViewModel등록(access);
        인증 = 하위ViewModel등록(authentication);
        목록 = 하위ViewModel등록(list);
        상세 = 하위ViewModel등록(detail);
        취소 = cancellation is null ? null : 하위ViewModel등록(cancellation, 수명소유: true);
        인증.PropertyChanged += 인증변경;
        상세.PropertyChanged += 상세변경;
    }

    public 음식배달페이지접근ViewModel 접근 { get; }
    public 주문자앱인증ViewModel 인증 { get; }
    public 주문자음식주문목록ViewModel 목록 { get; }
    public 주문자음식주문상세ViewModel 상세 { get; }
    public 주문자음식주문취소ViewModel? 취소 { get; }
    public bool 인증화면표시 => 접근.사용가능 && (!인증.초기화됨 || !인증.로그인됨 || 재로그인필요);

    public bool 재로그인필요
    {
        get => _재로그인필요;
        private set
        {
            if (SetProperty(ref _재로그인필요, value))
            {
                OnPropertyChanged(nameof(개인주문조회가능));
            }
        }
    }

    public bool 개인주문조회가능 => !_disposed && 접근.사용가능 && 인증.로그인됨 && !재로그인필요;

    public async Task 초기화Async(
        string? orderNo,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        if (!await 접근.확인Async(cancellationToken) || !접근.사용가능)
        {
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return;

        if (!인증.초기화됨 && !await 인증.복원Async(cancellationToken))
        {
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return;

        if (개인주문조회가능)
        {
            await 인증콘텐츠조회Async(orderNo, cancellationToken);
        }
    }

    public async Task 경로선택반영Async(
        string? orderNo,
        CancellationToken cancellationToken = default)
    {
        if (!개인주문조회가능)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(orderNo))
        {
            var normalizedOrderNo = orderNo.Trim();
            if (!string.Equals(상세.요청OrderNo, normalizedOrderNo, StringComparison.Ordinal))
                await 상세.조회Async(normalizedOrderNo, cancellationToken);
            취소문맥반영();
            return;
        }

        if (!string.IsNullOrWhiteSpace(상세.요청OrderNo))
        {
            상세.선택해제();
            취소?.세션초기화();
        }

    }

    public async Task<bool> 로그인Async(
        공통로그인요청 request,
        string? orderNo,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        if (!await 인증.로그인Async(
                request.UserNameOrEmail,
                request.Password,
                cancellationToken))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return false;
        재로그인필요 = false;
        await 인증콘텐츠조회Async(orderNo, cancellationToken);
        _복구UserId = null;
        return true;
    }

    public async Task<bool> 로그아웃Async(CancellationToken cancellationToken = default)
    {
        if (!await 인증.로그아웃Async(cancellationToken))
        {
            return false;
        }

        목록.세션초기화();
        상세.선택해제();
        취소?.세션초기화();
        _복구UserId = null;
        재로그인필요 = false;
        return true;
    }

    public async Task 인증오류복구Async(CancellationToken cancellationToken = default)
    {
        if (목록.오류?.Http상태코드 != 401 && 상세.오류?.Http상태코드 != 401
            && 취소?.오류?.Http상태코드 != 401)
        {
            return;
        }

        // 최종 401만 로그인 복구로 보냅니다. 저장소 정리 실패에도 개인 화면과 대기 작업을 차단합니다.
        재로그인필요 = true;
        _복구UserId = 인증.세션.UserId;
        취소?.인증대기();
        목록.세션초기화();
        상세.선택해제();
        _인증복구중 = true;
        try { await 인증.세션만료Async(cancellationToken); }
        finally { _인증복구중 = false; }
    }

    public Task 목록검색Async(CancellationToken cancellationToken = default)
        => 목록.조회Async(cancellationToken);

    public Task 목록새로고침Async(CancellationToken cancellationToken = default)
        => 목록.페이지조회Async(Math.Max(1, 목록.현재페이지), cancellationToken);

    public Task 페이지변경Async(int page, CancellationToken cancellationToken = default)
        => 목록.페이지조회Async(page, cancellationToken);

    public async Task 검색조건초기화Async(CancellationToken cancellationToken = default)
    {
        목록.필터초기화();
        await 목록.조회Async(cancellationToken);
    }

    public async Task 주문선택Async(string orderNo, CancellationToken cancellationToken = default)
    {
        if (!개인주문조회가능) return;
        취소?.문맥반영(orderNo, 인증.세션.UserId, null);
        await 상세.조회Async(orderNo, cancellationToken);
        취소문맥반영();
    }

    public async Task<bool> 주문취소Async(CancellationToken cancellationToken = default)
    {
        if (!개인주문조회가능 || 취소 is null || !상세.최신상태확인됨 || 상세.처리중) return false;
        var owner = 인증.세션.UserId;
        var orderNo = 상세.요청OrderNo;
        var cancelled = await 취소.취소Async(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!개인주문조회가능 || owner != 인증.세션.UserId || orderNo != 상세.요청OrderNo)
            return false;
        if (취소.오류?.Http상태코드 == 401) return false;
        await 주문진행새로고침Async(cancellationToken);
        return cancelled;
    }

    public async Task<bool> 주문수령확인Async(CancellationToken cancellationToken = default)
    {
        if (!개인주문조회가능 || !await 상세.수령확인Async(cancellationToken))
        {
            return false;
        }

        await 목록.페이지조회Async(Math.Max(1, 목록.현재페이지), cancellationToken);
        return true;
    }

    public async Task 주문진행새로고침Async(CancellationToken cancellationToken = default)
    {
        var orderNo = 상세.요청OrderNo;
        if (!개인주문조회가능 || string.IsNullOrWhiteSpace(orderNo))
        {
            return;
        }

        await Task.WhenAll(
            상세.조회Async(orderNo, cancellationToken),
            목록.페이지조회Async(Math.Max(1, 목록.현재페이지), cancellationToken));
        취소문맥반영();
    }

    public void 주문선택해제()
    {
        상세.선택해제();
        취소?.세션초기화();
    }

    private async Task 인증콘텐츠조회Async(
        string? orderNo,
        CancellationToken cancellationToken)
    {
        var listTask = 목록.조회Async(cancellationToken);
        var detailTask = string.IsNullOrWhiteSpace(orderNo)
            ? Task.FromResult(true)
            : 상세.조회Async(orderNo.Trim(), cancellationToken);
        await Task.WhenAll(listTask, detailTask);
        취소문맥반영();
    }

    private void 취소문맥반영()
    {
        // 만료·로그인 중의 익명/선택 비움은 실제 주문 전환이 아닙니다.
        // 동일 계정의 미확정 시도는 로그인 후 정확한 정본에서 다시 결속합니다.
        if (재로그인필요 || _인증복구중) return;
        취소?.문맥반영(상세.요청OrderNo, 인증.세션.UserId,
            상세.최신상태확인됨 && !상세.처리중 ? 상세.상세 : null);
    }

    private void 상세변경(object? sender, PropertyChangedEventArgs args)
    {
        if (!_disposed && args.PropertyName is nameof(주문자음식주문상세ViewModel.최신상태확인됨)
            or nameof(주문자음식주문상세ViewModel.처리중)
            or nameof(주문자음식주문상세ViewModel.상세))
            취소문맥반영();
    }

    private void 인증변경(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(주문자앱인증ViewModel.세션)) return;
        var userId = 인증.세션.UserId;
        if (_콘텐츠UserId != userId)
        {
            목록.세션초기화();
            상세.선택해제();
            if (!_인증복구중 && (_복구UserId is null || _복구UserId != userId))
                취소?.세션초기화();
        }
        _콘텐츠UserId = userId;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            인증.PropertyChanged -= 인증변경;
            상세.PropertyChanged -= 상세변경;
            목록.세션초기화();
            상세.선택해제();
        }
        base.Dispose(disposing);
    }
}
