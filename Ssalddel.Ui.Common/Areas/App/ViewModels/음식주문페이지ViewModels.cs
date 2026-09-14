using CommunityToolkit.Mvvm.ComponentModel;
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
        => 작업실행Async(
            async token =>
            {
                응답 = await service.목록Async(new 주문자음식주문목록조회요청
                {
                    검색어 = 검색어,
                    상태 = 상태필터,
                    Page = Math.Max(1, page),
                    PageSize = DefaultPageSize
                }, token);
                초기화됨 = true;
            },
            "내 음식 주문 목록을 불러왔습니다.",
            cancellationToken,
            ex => $"내 음식 주문 목록을 불러오지 못했습니다. {ex.Message}");

    public void 필터초기화()
    {
        검색어 = string.Empty;
        상태필터 = null;
    }

    public void 세션초기화()
    {
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

    [ObservableProperty]
    public partial string? 요청OrderNo { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(수령확인가능))]
    public partial 주문자음식주문상세응답? 상세 { get; private set; }

    [ObservableProperty]
    public partial bool 찾을수없음 { get; private set; }

    [ObservableProperty]
    public partial string 수령확인메모 { get; set; } = string.Empty;

    public bool 수령확인가능 => 업무가능행동목록.포함(
        상세?.AvailableActions,
        음식배달가능행동Ids.주문수령확인);

    public Task<bool> 조회Async(string orderNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return Task.FromResult(유효성실패("조회할 음식 주문번호를 확인해 주세요."));
        }

        요청OrderNo = orderNo.Trim();
        상세 = null;
        찾을수없음 = false;
        return 작업실행Async(
            async token =>
            {
                상세 = await service.상세Async(요청OrderNo, token);
                찾을수없음 = 상세 is null;
            },
            "음식 주문 상세를 불러왔습니다.",
            cancellationToken,
            ex => $"음식 주문 상세를 불러오지 못했습니다. {ex.Message}");
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

        return 작업실행Async(
            async token =>
            {
                await receiptConfirmationService.수령확인Async(
                    요청OrderNo,
                    new 주문자음식주문수령확인요청
                    {
                        클라이언트요청Id = _수령확인요청Id.Value,
                        확인메모 = 수령확인메모?.Trim() ?? string.Empty
                    },
                    token);
                상세 = await service.상세Async(요청OrderNo, token)
                    ?? throw new InvalidOperationException("수령 확인한 음식 주문을 다시 조회할 수 없습니다.");
                찾을수없음 = false;
                _수령확인요청Id = null;
                수령확인메모 = string.Empty;
            },
            "음식 수령을 확인했습니다.",
            cancellationToken,
            ex => $"음식 수령을 확인하지 못했습니다. {ex.Message}");
    }

    public void 선택해제()
    {
        요청OrderNo = null;
        상세 = null;
        찾을수없음 = false;
        _수령확인요청Id = null;
        수령확인메모 = string.Empty;
        작업상태초기화();
    }
}

/// <summary>기능 접근, 인증, 목록과 정확한 상세를 조립하고 음식 주문 내역 페이지 흐름만 조율합니다.</summary>
public sealed class 주문자음식주문PageViewModel : 조립ViewModelBase
{
    public 주문자음식주문PageViewModel(
        음식배달페이지접근ViewModel access,
        주문자앱인증ViewModel authentication,
        주문자음식주문목록ViewModel list,
        주문자음식주문상세ViewModel detail)
    {
        접근 = 하위ViewModel등록(access);
        인증 = 하위ViewModel등록(authentication);
        목록 = 하위ViewModel등록(list);
        상세 = 하위ViewModel등록(detail);
    }

    public 음식배달페이지접근ViewModel 접근 { get; }
    public 주문자앱인증ViewModel 인증 { get; }
    public 주문자음식주문목록ViewModel 목록 { get; }
    public 주문자음식주문상세ViewModel 상세 { get; }

    public async Task 초기화Async(
        string? orderNo,
        CancellationToken cancellationToken = default)
    {
        if (!await 접근.확인Async(cancellationToken) || !접근.사용가능)
        {
            return;
        }

        if (!인증.초기화됨 && !await 인증.복원Async(cancellationToken))
        {
            return;
        }

        if (인증.로그인됨)
        {
            await 인증콘텐츠조회Async(orderNo, cancellationToken);
        }
    }

    public Task 경로선택반영Async(
        string? orderNo,
        CancellationToken cancellationToken = default)
    {
        if (!접근.사용가능 || !인증.로그인됨)
        {
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(orderNo))
        {
            var normalizedOrderNo = orderNo.Trim();
            return string.Equals(상세.요청OrderNo, normalizedOrderNo, StringComparison.Ordinal)
                ? Task.CompletedTask
                : 상세.조회Async(normalizedOrderNo, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(상세.요청OrderNo))
        {
            상세.선택해제();
        }

        return Task.CompletedTask;
    }

    public async Task<bool> 로그인Async(
        공통로그인요청 request,
        string? orderNo,
        CancellationToken cancellationToken = default)
    {
        if (!await 인증.로그인Async(
                request.UserNameOrEmail,
                request.Password,
                cancellationToken))
        {
            return false;
        }

        await 인증콘텐츠조회Async(orderNo, cancellationToken);
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
        return true;
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

    public Task 주문선택Async(string orderNo, CancellationToken cancellationToken = default)
        => 상세.조회Async(orderNo, cancellationToken);

    public async Task<bool> 주문수령확인Async(CancellationToken cancellationToken = default)
    {
        if (!await 상세.수령확인Async(cancellationToken))
        {
            return false;
        }

        await 목록.페이지조회Async(Math.Max(1, 목록.현재페이지), cancellationToken);
        return true;
    }

    public async Task 주문진행새로고침Async(CancellationToken cancellationToken = default)
    {
        var orderNo = 상세.요청OrderNo;
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return;
        }

        await Task.WhenAll(
            상세.조회Async(orderNo, cancellationToken),
            목록.페이지조회Async(Math.Max(1, 목록.현재페이지), cancellationToken));
    }

    public void 주문선택해제() => 상세.선택해제();

    private async Task 인증콘텐츠조회Async(
        string? orderNo,
        CancellationToken cancellationToken)
    {
        var listTask = 목록.조회Async(cancellationToken);
        var detailTask = string.IsNullOrWhiteSpace(orderNo)
            ? Task.FromResult(true)
            : 상세.조회Async(orderNo.Trim(), cancellationToken);
        await Task.WhenAll(listTask, detailTask);
    }
}
