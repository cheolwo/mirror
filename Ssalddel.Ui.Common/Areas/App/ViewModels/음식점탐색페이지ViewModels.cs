using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public enum 음식배달페이지접근상태
{
    확인전,
    사용가능,
    기능비활성,
    오류
}

/// <summary>음식점 탐색 페이지의 서버 기능 플래그만 판정합니다.</summary>
public sealed partial class 음식배달페이지접근ViewModel(
    I음식배달페이지접근Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(사용가능))]
    [NotifyPropertyChangedFor(nameof(기능비활성))]
    public partial 음식배달페이지접근상태 화면상태 { get; private set; }

    public bool 사용가능 => 화면상태 == 음식배달페이지접근상태.사용가능;
    public bool 기능비활성 => 화면상태 == 음식배달페이지접근상태.기능비활성;

    public async Task<bool> 확인Async(CancellationToken cancellationToken = default)
    {
        화면상태 = 음식배달페이지접근상태.확인전;
        var succeeded = await 작업실행Async(
            async token =>
            {
                화면상태 = await service.기능활성여부Async(token)
                    ? 음식배달페이지접근상태.사용가능
                    : 음식배달페이지접근상태.기능비활성;
            },
            "음식 배달 기능 상태를 확인했습니다.",
            cancellationToken,
            ex => $"음식 배달 기능 상태를 확인하지 못했습니다. {ex.Message}");
        if (!succeeded)
        {
            화면상태 = 취소됨
                ? 음식배달페이지접근상태.확인전
                : 음식배달페이지접근상태.오류;
        }

        return succeeded;
    }
}

/// <summary>서버 탐색 정책, 공개 권역과 사용자가 명시한 조회 기준만 관리합니다.</summary>
public sealed partial class 음식점탐색기준ViewModel(
    I음식점탐색정책읽기Service policyService,
    I음식점공개읽기Service restaurantService) : 업무작업ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(조회가능))]
    public partial RestaurantSearchPolicyDto? 정책 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(조회가능))]
    public partial IReadOnlyList<음식점탐색권역응답> 권역목록 { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(선택권역))]
    [NotifyPropertyChangedFor(nameof(조회가능))]
    public partial string? 선택배달권키 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(조회가능))]
    public partial double 반경Km { get; set; } = 7d;

    [ObservableProperty]
    public partial bool 초기화됨 { get; private set; }

    public 음식점탐색권역응답? 선택권역 => 권역목록.FirstOrDefault(item =>
        string.Equals(item.배달권키, 선택배달권키, StringComparison.Ordinal));

    public bool 조회가능 => 초기화됨
                           && 정책 is not null
                           && 선택권역 is not null
                           && 반경Km >= 정책.MinRadiusKm
                           && 반경Km <= 정책.MaxRadiusKm;

    public Task<bool> 준비Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token =>
            {
                var policyTask = policyService.조회Async(token);
                var scopeTask = restaurantService.권역목록Async(token);
                await Task.WhenAll(policyTask, scopeTask);
                정책 = await policyTask;
                권역목록 = await scopeTask;
                반경Km = 정책.DefaultRadiusKm;
                초기화됨 = true;
            },
            "음식점 탐색 기준을 불러왔습니다.",
            cancellationToken,
            ex => $"음식점 탐색 기준을 불러오지 못했습니다. {ex.Message}");

    public void 빠른반경설정(double radiusKm)
    {
        if (정책 is null)
        {
            return;
        }

        반경Km = Math.Clamp(radiusKm, 정책.MinRadiusKm, 정책.MaxRadiusKm);
    }
}

/// <summary>사용자가 선택한 권역·반경·검색 조건의 서버 목록과 페이징만 담당합니다.</summary>
public sealed partial class 음식점공개목록ViewModel(
    I음식점공개읽기Service service) : 업무작업ViewModelBase
{
    private string? _배달권키;
    private decimal _반경Km;

    [ObservableProperty]
    public partial string 검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool 주문가능만 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(음식점목록))]
    [NotifyPropertyChangedFor(nameof(전체건수))]
    [NotifyPropertyChangedFor(nameof(현재페이지))]
    [NotifyPropertyChangedFor(nameof(총페이지수))]
    [NotifyPropertyChangedFor(nameof(결과없음))]
    public partial 음식점공개목록응답 응답 { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(결과없음))]
    public partial bool 초기화됨 { get; private set; }

    public IReadOnlyList<음식점공개요약응답> 음식점목록 => 응답.Items;
    public int 전체건수 => 응답.TotalCount;
    public int 현재페이지 => 응답.Page;
    public int 총페이지수 => Math.Max(1, (int)Math.Ceiling(전체건수 / (double)Math.Max(1, 응답.PageSize)));
    public bool 결과없음 => 초기화됨 && 전체건수 == 0;
    public bool 검색조건사용중 => !string.IsNullOrWhiteSpace(검색어) || 주문가능만;

    public Task<bool> 조회Async(
        string? deliveryScopeKey,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deliveryScopeKey))
        {
            return Task.FromResult(유효성실패("조회할 공개 행정권역을 선택해 주세요."));
        }

        _배달권키 = deliveryScopeKey.Trim();
        _반경Km = (decimal)radiusKm;
        return 조회CoreAsync(1, cancellationToken);
    }

    public Task<bool> 페이지조회Async(int page, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_배달권키))
        {
            return Task.FromResult(유효성실패("먼저 공개 행정권역을 선택해 조회해 주세요."));
        }

        return 조회CoreAsync(Math.Max(1, page), cancellationToken);
    }

    public Task<bool> 새로고침Async(CancellationToken cancellationToken = default)
        => 페이지조회Async(Math.Max(1, 현재페이지), cancellationToken);

    private Task<bool> 조회CoreAsync(int page, CancellationToken cancellationToken)
        => 작업실행Async(
            async token =>
            {
                응답 = await service.목록Async(new 음식점공개목록조회요청
                {
                    배달권키 = _배달권키!,
                    반경Km = _반경Km,
                    검색어 = 검색어,
                    주문가능만 = 주문가능만,
                    Page = page,
                    PageSize = 12
                }, token);
                초기화됨 = true;
            },
            "공개 음식점 목록을 불러왔습니다.",
            cancellationToken,
            ex => $"공개 음식점 목록을 불러오지 못했습니다. {ex.Message}");
}

/// <summary>주소나 목록에서 선택한 정확한 restaurantId 한 건과 공개 메뉴만 조회합니다.</summary>
public sealed partial class 음식점공개상세ViewModel(
    I음식점공개읽기Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    public partial long? 요청RestaurantId { get; private set; }

    [ObservableProperty]
    public partial 음식점공개상세응답? 상세 { get; private set; }

    [ObservableProperty]
    public partial bool 찾을수없음 { get; private set; }

    public Task<bool> 조회Async(long restaurantId, CancellationToken cancellationToken = default)
    {
        if (restaurantId <= 0)
        {
            return Task.FromResult(유효성실패("조회할 음식점 ID를 확인해 주세요."));
        }

        요청RestaurantId = restaurantId;
        상세 = null;
        찾을수없음 = false;
        return 작업실행Async(
            async token =>
            {
                상세 = await service.상세Async(restaurantId, token);
                찾을수없음 = 상세 is null;
            },
            "음식점과 공개 메뉴를 불러왔습니다.",
            cancellationToken,
            ex => $"음식점 상세를 불러오지 못했습니다. {ex.Message}");
    }

    public void 선택해제()
    {
        요청RestaurantId = null;
        상세 = null;
        찾을수없음 = false;
        작업상태초기화();
    }
}

public sealed record 음식주문선택항목ViewModel(
    long 메뉴Id,
    string 메뉴명,
    int 수량,
    decimal 단가)
{
    public decimal 합계 => 단가 * 수량;
}

/// <summary>
/// 정확한 음식점 공개 메뉴의 수량과 수령 정보를 모으고 인증된 음식 주문
/// 등록 API 한 건만 실행합니다.
/// </summary>
public sealed partial class 음식주문작성ViewModel(
    I주문자음식주문쓰기Service service) : 업무작업ViewModelBase
{
    private readonly Dictionary<long, int> _수량목록 = [];
    private 음식점공개상세응답? _음식점;
    private CancellationTokenSource? _등록취소;
    private bool _등록진행;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial string 수령인명 { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial string 연락처 { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial string 주소 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 상세주소 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 요청사항 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool 주문자본인수령여부 { get; set; } = true;

    [ObservableProperty]
    public partial string 결제수단 { get; set; } = "현장결제";

    [ObservableProperty]
    public partial Guid 클라이언트요청Id { get; private set; } = Guid.NewGuid();

    [ObservableProperty]
    public partial 음식주문응답? 등록응답 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial bool 재로그인필요 { get; private set; }

    public IReadOnlyList<음식주문선택항목ViewModel> 선택항목목록
        => _음식점?.메뉴목록
               .Where(menu => _수량목록.GetValueOrDefault(menu.Id) > 0)
               .Select(menu => new 음식주문선택항목ViewModel(
                   menu.Id,
                   menu.메뉴명,
                   _수량목록[menu.Id],
                   menu.판매가))
               .ToArray()
           ?? [];

    public decimal 주문금액 => 선택항목목록.Sum(item => item.합계);
    public decimal 최소주문금액 => _음식점?.음식점.최소주문금액 ?? 0;
    public bool 최소주문충족 => 주문금액 >= 최소주문금액;
    public bool 메뉴선택됨 => 선택항목목록.Count > 0;
    public bool 제출가능
        => _음식점?.음식점.주문가능여부 == true
           && 메뉴선택됨
           && 최소주문충족
           && !string.IsNullOrWhiteSpace(수령인명)
           && !string.IsNullOrWhiteSpace(연락처)
           && !string.IsNullOrWhiteSpace(주소)
           && !재로그인필요
           && !_등록진행
           && !처리중;

    public int 메뉴수량(long menuId) => _수량목록.GetValueOrDefault(menuId);

    public void 음식점설정(음식점공개상세응답? detail)
    {
        if (_음식점?.음식점.Id == detail?.음식점.Id)
        {
            _음식점 = detail;
            NotifyOrderChanged();
            return;
        }

        _음식점 = detail;
        새요청준비(clearRecipient: false);
    }

    public void 메뉴수량변경(long menuId, int delta)
    {
        var menu = _음식점?.메뉴목록.FirstOrDefault(item => item.Id == menuId);
        if (menu is null || menu.품절여부 || delta == 0)
        {
            return;
        }

        var current = _수량목록.GetValueOrDefault(menuId);
        var next = Math.Clamp(current + delta, 0, 100);
        if (next == current)
        {
            return;
        }

        if (next == 0)
        {
            _수량목록.Remove(menuId);
        }
        else
        {
            _수량목록[menuId] = next;
        }

        요청내용변경됨();
        NotifyOrderChanged();
    }

    public async Task<bool> 등록Async(CancellationToken cancellationToken = default)
    {
        if (_등록진행 || 처리중 || 재로그인필요 || cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (_음식점 is null || _음식점.음식점.Id <= 0)
        {
            return 유효성실패("주문할 음식점을 다시 선택해 주세요.");
        }

        if (!메뉴선택됨)
        {
            return 유효성실패("주문할 메뉴를 한 개 이상 선택해 주세요.");
        }

        if (!최소주문충족)
        {
            return 유효성실패($"최소 주문 금액 {최소주문금액:N0}원을 충족해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(수령인명)
            || string.IsNullOrWhiteSpace(연락처)
            || string.IsNullOrWhiteSpace(주소))
        {
            return 유효성실패("수령인 이름, 연락처와 주소를 입력해 주세요.");
        }

        var requestId = 클라이언트요청Id;
        var request = new 음식주문등록요청
        {
            클라이언트요청Id = requestId,
            음식점Id = _음식점.음식점.Id,
            주문자UserId = string.Empty,
            수령인정보 = new()
            {
                수령인명 = 수령인명.Trim(),
                연락처 = 연락처.Trim(),
                주소 = 주소.Trim(),
                상세주소 = 상세주소.Trim(),
                요청사항 = 요청사항.Trim(),
                주문자본인수령여부 = 주문자본인수령여부
            },
            상품목록 = 선택항목목록.Select(item => new 음식주문상품Dto
            {
                메뉴Id = item.메뉴Id,
                상품명 = item.메뉴명,
                수량 = item.수량,
                단가 = item.단가
            }).ToArray(),
            결제수단 = 결제수단
        };
        using var submissionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _등록취소 = submissionCancellation;
        _등록진행 = true;
        OnPropertyChanged(nameof(제출가능));
        try
        {
            var succeeded = await 작업실행Async(
                async token =>
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        var response = await service.등록Async(request, token);
                        if (requestId != 클라이언트요청Id)
                        {
                            submissionCancellation.Cancel();
                        }

                        token.ThrowIfCancellationRequested();
                        등록응답 = response;
                    }
                    catch (Exception) when (token.IsCancellationRequested || requestId != 클라이언트요청Id)
                    {
                        // 취소를 무시한 서버의 늦은 HTTP 오류도 현재 작성의 실패로 남기지 않습니다.
                        submissionCancellation.Cancel();
                        throw new OperationCanceledException(token);
                    }
                },
                "음식 주문을 등록했습니다.",
                submissionCancellation.Token,
                ex => $"음식 주문을 등록하지 못했습니다. {ex.Message}");
            return succeeded && requestId == 클라이언트요청Id;
        }
        finally
        {
            if (ReferenceEquals(_등록취소, submissionCancellation))
            {
                if (requestId != 클라이언트요청Id)
                {
                    등록응답 = null;
                    작업상태초기화();
                }

                _등록취소 = null;
                _등록진행 = false;
                OnPropertyChanged(nameof(제출가능));
            }
        }
    }

    public void 새요청준비(bool clearRecipient = true)
    {
        _수량목록.Clear();
        요청내용변경됨();
        if (clearRecipient)
        {
            수령인명 = string.Empty;
            연락처 = string.Empty;
            주소 = string.Empty;
            상세주소 = string.Empty;
            요청사항 = string.Empty;
            주문자본인수령여부 = true;
        }

        작업상태초기화();
        NotifyOrderChanged();
    }

    internal void 재로그인필요설정(bool required) => 재로그인필요 = required;

    private void NotifyOrderChanged()
    {
        OnPropertyChanged(nameof(선택항목목록));
        OnPropertyChanged(nameof(주문금액));
        OnPropertyChanged(nameof(최소주문금액));
        OnPropertyChanged(nameof(최소주문충족));
        OnPropertyChanged(nameof(메뉴선택됨));
        OnPropertyChanged(nameof(제출가능));
    }

    partial void On수령인명Changed(string value) => 요청내용변경됨();
    partial void On연락처Changed(string value) => 요청내용변경됨();
    partial void On주소Changed(string value) => 요청내용변경됨();
    partial void On상세주소Changed(string value) => 요청내용변경됨();
    partial void On요청사항Changed(string value) => 요청내용변경됨();
    partial void On주문자본인수령여부Changed(bool value) => 요청내용변경됨();
    partial void On결제수단Changed(string value) => 요청내용변경됨();

    private void 요청내용변경됨()
    {
        클라이언트요청Id = Guid.NewGuid();
        등록응답 = null;
        _등록취소?.Cancel();
        작업취소();
        작업상태초기화();
    }
}

/// <summary>접근·인증·탐색 기준·목록·정확한 상세·주문 작성을 조립합니다.</summary>
public sealed partial class 음식점탐색PageViewModel : 조립ViewModelBase
{
    private bool _disposed;
    private long _로그인세대;

    public 음식점탐색PageViewModel(
        음식배달페이지접근ViewModel access,
        주문자앱인증ViewModel authentication,
        음식점탐색기준ViewModel criteria,
        음식점공개목록ViewModel list,
        음식점공개상세ViewModel detail,
        음식주문작성ViewModel writer)
    {
        접근 = 하위ViewModel등록(access);
        인증 = 하위ViewModel등록(authentication);
        기준 = 하위ViewModel등록(criteria);
        목록 = 하위ViewModel등록(list);
        상세 = 하위ViewModel등록(detail);
        작성 = 하위ViewModel등록(writer);
    }

    public 음식배달페이지접근ViewModel 접근 { get; }
    public 주문자앱인증ViewModel 인증 { get; }
    public 음식점탐색기준ViewModel 기준 { get; }
    public 음식점공개목록ViewModel 목록 { get; }
    public 음식점공개상세ViewModel 상세 { get; }
    public 음식주문작성ViewModel 작성 { get; }

    [ObservableProperty]
    public partial bool 인증화면표시 { get; private set; }

    public bool 인증화면진입()
    {
        if (_disposed || !접근.사용가능 || 인증.처리중 || 작성.처리중)
        {
            return false;
        }

        인증화면표시 = true;
        return true;
    }

    public bool 탐색화면복귀()
    {
        if (_disposed || 인증.처리중)
        {
            return false;
        }

        // 화면만 전환합니다. 음식점 선택·메뉴·수령 정보와 요청ID는 그대로 유지합니다.
        인증화면표시 = false;
        return true;
    }

    public async Task<bool> 로그인Async(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || cancellationToken.IsCancellationRequested || 인증.처리중)
        {
            return false;
        }

        // 새 로그인으로 넘어간 이전 제출은 취소하되 작성 내용과 재시도 요청ID는 유지합니다.
        var loginGeneration = ++_로그인세대;
        작성.작업취소();
        var succeeded = await 인증.로그인Async(userNameOrEmail, password, cancellationToken);
        if (_disposed || cancellationToken.IsCancellationRequested || loginGeneration != _로그인세대
            || !succeeded || !인증.로그인됨)
        {
            return false;
        }

        작성.재로그인필요설정(false);
        인증화면표시 = false;
        return true;
    }

    public async Task<bool> 주문등록Async(CancellationToken cancellationToken = default)
    {
        if (_disposed || cancellationToken.IsCancellationRequested
            || !접근.사용가능 || 인증화면표시 || !인증.로그인됨 || 인증.처리중 || 작성.재로그인필요)
        {
            return false;
        }

        var session = 인증.세션;
        var loginGeneration = _로그인세대;
        var requestId = 작성.클라이언트요청Id;
        var succeeded = await 작성.등록Async(cancellationToken);
        if (_disposed || cancellationToken.IsCancellationRequested
            // 같은 계정의 record 값은 같을 수 있으므로 참조 비교만으로 재로그인을 판별하지 않습니다.
            || loginGeneration != _로그인세대
            || !ReferenceEquals(session, 인증.세션) || requestId != 작성.클라이언트요청Id)
        {
            return false;
        }

        if (작성.오류?.Http상태코드 == 401)
        {
            // 저장소 정리가 실패해도 재로그인 입력으로 복귀하고 작성 내용/요청ID는 보존합니다.
            작성.재로그인필요설정(true);
            인증화면표시 = true;
            await 인증.로그아웃Async(cancellationToken);
            return false;
        }

        return succeeded;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            작성.작업취소();
        }

        base.Dispose(disposing);
    }
}
