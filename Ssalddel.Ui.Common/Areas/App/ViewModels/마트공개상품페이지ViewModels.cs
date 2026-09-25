using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Mart;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public enum 마트페이지접근상태
{
    확인전,
    사용가능,
    기능비활성,
    오류
}

/// <summary>마트 공개 상품 페이지의 서버 기능 플래그만 판정합니다.</summary>
public sealed partial class 마트페이지접근ViewModel(
    I마트페이지접근Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(사용가능))]
    [NotifyPropertyChangedFor(nameof(기능비활성))]
    public partial 마트페이지접근상태 화면상태 { get; private set; }

    public bool 사용가능 => 화면상태 == 마트페이지접근상태.사용가능;
    public bool 기능비활성 => 화면상태 == 마트페이지접근상태.기능비활성;

    public async Task<bool> 확인Async(CancellationToken cancellationToken = default)
    {
        화면상태 = 마트페이지접근상태.확인전;
        var succeeded = await 작업실행Async(
            async token =>
            {
                화면상태 = await service.기능활성여부Async(token)
                    ? 마트페이지접근상태.사용가능
                    : 마트페이지접근상태.기능비활성;
            },
            "알뜰살뜰 마트 기능 상태를 확인했습니다.",
            cancellationToken,
            ex => $"알뜰살뜰 마트 기능 상태를 확인하지 못했습니다. {ex.Message}");
        if (!succeeded)
        {
            화면상태 = 취소됨 ? 마트페이지접근상태.확인전 : 마트페이지접근상태.오류;
        }

        return succeeded;
    }
}

/// <summary>공개 상품 검색 조건, 서버 페이징과 목록 결과만 관리합니다.</summary>
public sealed partial class 마트공개상품목록ViewModel(
    I마트공개상품읽기Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    public partial string 검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool 판매가능만 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(상품목록))]
    [NotifyPropertyChangedFor(nameof(전체건수))]
    [NotifyPropertyChangedFor(nameof(현재페이지))]
    [NotifyPropertyChangedFor(nameof(총페이지수))]
    [NotifyPropertyChangedFor(nameof(결과없음))]
    public partial 마트공개상품목록응답 응답 { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(결과없음))]
    public partial bool 초기화됨 { get; private set; }

    public IReadOnlyList<마트공개상품요약응답> 상품목록 => 응답.Items;
    public int 전체건수 => 응답.TotalCount;
    public int 현재페이지 => 응답.Page;
    public int 총페이지수 => Math.Max(1, (int)Math.Ceiling(전체건수 / (double)Math.Max(1, 응답.PageSize)));
    public bool 결과없음 => 초기화됨 && 전체건수 == 0;
    public bool 검색조건사용중 => !string.IsNullOrWhiteSpace(검색어) || 판매가능만;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 조회CoreAsync(1, cancellationToken);

    public Task<bool> 페이지조회Async(int page, CancellationToken cancellationToken = default)
        => 조회CoreAsync(Math.Max(1, page), cancellationToken);

    public Task<bool> 새로고침Async(CancellationToken cancellationToken = default)
        => 조회CoreAsync(Math.Max(1, 현재페이지), cancellationToken);

    /// <summary>
    /// 실제 API 결과를 대체하는 fallback이 아니라, 호출자가 명시적으로 연 화면 검토용 자료를 표시합니다.
    /// </summary>
    public void 화면검토자료적용(마트공개상품목록응답 previewResponse)
    {
        ArgumentNullException.ThrowIfNull(previewResponse);
        응답 = previewResponse;
        초기화됨 = true;
        작업상태초기화();
    }

    private Task<bool> 조회CoreAsync(int page, CancellationToken cancellationToken)
        => 작업실행Async(
            async token =>
            {
                응답 = await service.목록Async(new 마트공개상품목록조회요청
                {
                    검색어 = 검색어,
                    판매가능만 = 판매가능만,
                    Page = page,
                    PageSize = 12
                }, token);
                초기화됨 = true;
            },
            "마트 공개 상품 목록을 불러왔습니다.",
            cancellationToken,
            ex => $"마트 공개 상품 목록을 불러오지 못했습니다. {ex.Message}");
}

/// <summary>주소나 목록에서 선택한 정확한 productId 한 건만 조회합니다.</summary>
public sealed partial class 마트공개상품상세ViewModel(
    I마트공개상품읽기Service service) : 업무작업ViewModelBase
{
    [ObservableProperty]
    public partial long? 요청ProductId { get; private set; }

    [ObservableProperty]
    public partial 마트공개상품상세응답? 상세 { get; private set; }

    [ObservableProperty]
    public partial bool 찾을수없음 { get; private set; }

    public Task<bool> 조회Async(long productId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return Task.FromResult(유효성실패("조회할 마트 상품 ID를 확인해 주세요."));
        }

        요청ProductId = productId;
        상세 = null;
        찾을수없음 = false;
        return 작업실행Async(
            async token =>
            {
                상세 = await service.상세Async(productId, token);
                찾을수없음 = 상세 is null;
            },
            "마트 공개 상품 상세를 불러왔습니다.",
            cancellationToken,
            ex => $"마트 공개 상품 상세를 불러오지 못했습니다. {ex.Message}");
    }

    public void 선택해제()
    {
        요청ProductId = null;
        상세 = null;
        찾을수없음 = false;
        작업상태초기화();
    }
}

/// <summary>완료된 구매 원장에 접근할 수 있는 사용자의 명시적 후기 작성만 수행합니다.</summary>
public sealed partial class 마트공개상품후기작성ViewModel : 업무작업ViewModelBase
{
    private readonly I마트공개상품후기작성Service _service;

    public 마트공개상품후기작성ViewModel(
        I마트공개상품후기작성Service service,
        ISsalddel현재사용자Context currentUserContext)
    {
        _service = service;
        현재사용자Context연결(currentUserContext);
    }

    [ObservableProperty]
    public partial long? 상품Id { get; private set; }

    [ObservableProperty]
    public partial string 작성자표시명 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 글비밀번호 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 제목 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 본문 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial 마트공개상품구매후기응답? 작성결과 { get; private set; }

    public void 준비(long productId, string? productName)
    {
        if (productId <= 0)
        {
            return;
        }

        if (상품Id == productId)
        {
            return;
        }

        상품Id = productId;
        작성자표시명 = 현재사용자.UserName?.Trim() ?? string.Empty;
        글비밀번호 = string.Empty;
        제목 = string.IsNullOrWhiteSpace(productName)
            ? "구매 후기"
            : $"{productName.Trim()} 구매 후기";
        본문 = string.Empty;
        작성결과 = null;
        작업상태초기화();
    }

    public void 선택해제()
    {
        if (처리중)
        {
            return;
        }

        상품Id = null;
        글비밀번호 = string.Empty;
        본문 = string.Empty;
        작성결과 = null;
        작업상태초기화();
    }

    public async Task<bool> 작성Async(CancellationToken cancellationToken = default)
    {
        if (!사용자확인됨)
        {
            return 유효성실패("구매후기는 로그인한 원장 참여자만 작성할 수 있습니다.");
        }

        if (상품Id is not long productId || productId <= 0)
        {
            return 유효성실패("후기를 작성할 상품을 다시 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(작성자표시명) || 작성자표시명.Trim().Length > 40)
        {
            return 유효성실패("표시명은 1자 이상 40자 이하로 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(글비밀번호)
            || 글비밀번호.Trim().Length is < 4 or > 100)
        {
            return 유효성실패("글 비밀번호는 4자 이상 100자 이하로 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(제목) || 제목.Trim().Length > 160)
        {
            return 유효성실패("후기 제목은 1자 이상 160자 이하로 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(본문) || 본문.Trim().Length > 4_000)
        {
            return 유효성실패("후기 내용은 1자 이상 4,000자 이하로 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                작성결과 = await _service.작성Async(productId, new 마트공개상품구매후기작성요청
                {
                    작성자표시명 = 작성자표시명.Trim(),
                    글비밀번호 = 글비밀번호,
                    제목 = 제목.Trim(),
                    본문 = 본문.Trim()
                }, token);
                글비밀번호 = string.Empty;
                본문 = string.Empty;
            },
            "구매후기를 완료 사례·후기 게시판에 등록했습니다.",
            cancellationToken,
            ex => $"구매후기를 등록하지 못했습니다. {ex.Message}");
    }
}

/// <summary>정확한 공개 상품 상세와 그 상품의 명시적 후기 작성 순서만 조정합니다.</summary>
public sealed class 마트공개상품후기PageViewModel : 조립ViewModelBase
{
    public 마트공개상품후기PageViewModel(
        마트공개상품상세ViewModel detail,
        마트공개상품후기작성ViewModel review)
    {
        상세 = 하위ViewModel등록(detail);
        후기 = 하위ViewModel등록(review);
    }

    public 마트공개상품상세ViewModel 상세 { get; }
    public 마트공개상품후기작성ViewModel 후기 { get; }

    public async Task<bool> 상세조회Async(long productId, CancellationToken cancellationToken = default)
    {
        var succeeded = await 상세.조회Async(productId, cancellationToken);
        if (succeeded && 상세.상세 is { } product)
        {
            후기.준비(product.Id, product.상품명);
        }
        else
        {
            후기.선택해제();
        }

        return succeeded;
    }

    public async Task<bool> 작성후같은상품재조회Async(CancellationToken cancellationToken = default)
    {
        if (상세.상세 is not { } product || !await 후기.작성Async(cancellationToken))
        {
            return false;
        }

        return await 상세조회Async(product.Id, cancellationToken);
    }
}
