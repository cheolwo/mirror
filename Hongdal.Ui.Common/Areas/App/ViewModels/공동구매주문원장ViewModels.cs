using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class 공동구매주문원장보기코드
{
    public const string 주문자보호 = 주문원장보기코드.주문자보호;
    public const string 주문자 = 주문원장보기코드.주문자;
    public const string 판매자 = 주문원장보기코드.판매자;
    public const string 창고담당자 = 주문원장보기코드.창고담당자;
    public const string 운송담당자 = 주문원장보기코드.운송담당자;

    public static IReadOnlyList<string> 전체 => 주문원장보기코드.전체;
}

/// <summary>
/// 주문 루트 원장의 주문자 보호형 보기와 역할별 공개 보기를 전환합니다.
/// </summary>
public sealed partial class 공동구매주문원장조회ViewModel : 공동구매주문원장실행업무ViewModelBase, IDisposable
{
    private readonly I주문원장Service _service;
    private readonly 공동구매실행상태ViewModel _실행상태;

    public 공동구매주문원장조회ViewModel(
        I주문원장Service service,
        공동구매실행상태ViewModel 실행상태,
        공동구매화면상태ViewModel 화면상태)
        : base(화면상태, 실행상태)
    {
        _service = service;
        _실행상태 = 실행상태;
        _실행상태.PropertyChanged += 실행상태변경;
    }

    [ObservableProperty]
    public partial string 보기코드 { get; private set; } = 공동구매주문원장보기코드.주문자보호;

    public string? 주문원장Id => _실행상태.선택된주문원장Id;
    public string? 공동구매주문집계원장Id => _실행상태.공동구매주문집계원장Id;
    public string? 원본커뮤니티원장Id => _실행상태.원본커뮤니티원장Id;
    public 주문원장통합공개Dto? 통합결과 => _실행상태.주문원장통합결과;
    public 주문원장역할별조회공개Dto? 역할별결과 => _실행상태.주문원장역할결과;
    public bool 조회가능 => !string.IsNullOrWhiteSpace(주문원장Id);
    public bool 주문집계연결됨 => !string.IsNullOrWhiteSpace(공동구매주문집계원장Id);

    /// <summary>
    /// 현재 공개 API에는 주문 루트 원장 생성 엔드포인트가 없으므로 발주 단계의 생성 결과를 연결해야 합니다.
    /// </summary>
    public bool 루트원장생성Api지원됨 => false;

    public string 연결안내 => 조회가능
        ? "선택한 주문 루트 원장을 통합 또는 역할별 범위로 조회할 수 있습니다."
        : "발주·원장 생성 단계에서 받은 주문 루트 원장 ID를 입력해 주세요. 커뮤니티 투표 원장 ID는 자동 대입하지 않습니다.";

    public void 주문원장선택(string? orderLedgerId)
        => _실행상태.주문원장선택(orderLedgerId);

    public bool 보기선택(string viewCode)
    {
        if (!공동구매주문원장보기코드.전체.Contains(viewCode, StringComparer.OrdinalIgnoreCase))
        {
            return 유효성실패("지원되는 주문원장 보기를 선택해 주세요.");
        }

        보기코드 = viewCode;
        return true;
    }

    public async Task<bool> 조회Async(CancellationToken cancellationToken = default)
    {
        var orderLedgerId = 주문원장Id;
        if (string.IsNullOrWhiteSpace(orderLedgerId))
        {
            return 유효성실패(연결안내);
        }

        return await 작업실행Async(
            async token =>
            {
                if (string.Equals(보기코드, 공동구매주문원장보기코드.주문자보호, StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _service.주문원장보호조회Async(orderLedgerId, token)
                        ?? throw new InvalidOperationException("주문원장 보호형 조회 응답이 비어 있습니다.");
                    _실행상태.주문원장역할적용(result);
                    return;
                }

                var roleResult = await _service.주문원장역할조회Async(orderLedgerId, 보기코드, token)
                    ?? throw new InvalidOperationException("주문원장 역할별 조회 응답이 비어 있습니다.");
                _실행상태.주문원장역할적용(roleResult);
            },
            보기코드 == 공동구매주문원장보기코드.주문자보호
                ? "하위 원장 상세를 권한에 따라 가린 주문자 보호형 원장을 조회했습니다."
                : "선택한 역할의 공개 범위로 주문원장을 조회했습니다.",
            cancellationToken);
    }

    public void Dispose()
    {
        _실행상태.PropertyChanged -= 실행상태변경;
        GC.SuppressFinalize(this);
    }

    private void 실행상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName)
            && e.PropertyName is not nameof(공동구매실행상태ViewModel.선택된주문원장Id)
                and not nameof(공동구매실행상태ViewModel.공동구매주문집계원장Id)
                and not nameof(공동구매실행상태ViewModel.원본커뮤니티원장Id)
                and not nameof(공동구매실행상태ViewModel.주문원장통합결과)
                and not nameof(공동구매실행상태ViewModel.주문원장역할결과))
        {
            return;
        }

        OnPropertyChanged(nameof(주문원장Id));
        OnPropertyChanged(nameof(공동구매주문집계원장Id));
        OnPropertyChanged(nameof(주문집계연결됨));
        OnPropertyChanged(nameof(원본커뮤니티원장Id));
        OnPropertyChanged(nameof(통합결과));
        OnPropertyChanged(nameof(역할별결과));
        OnPropertyChanged(nameof(조회가능));
        OnPropertyChanged(nameof(연결안내));
    }
}

/// <summary>
/// 판매, 입출고, 배송, 운송 등의 하위 원장을 주문 루트 원장에 연결하거나 분리합니다.
/// </summary>
public sealed partial class 공동구매하위원장ViewModel : 공동구매주문원장실행업무ViewModelBase, IDisposable
{
    private static readonly IReadOnlyList<string> Roles =
    [
        주문원장포함역할.판매,
        주문원장포함역할.창고입고,
        주문원장포함역할.창고출고,
        주문원장포함역할.배송,
        주문원장포함역할.운송
    ];

    private readonly I주문원장Service _service;
    private readonly 공동구매실행상태ViewModel _실행상태;

    public 공동구매하위원장ViewModel(
        I주문원장Service service,
        공동구매실행상태ViewModel 실행상태,
        공동구매화면상태ViewModel 화면상태)
        : base(화면상태, 실행상태)
    {
        _service = service;
        _실행상태 = 실행상태;
        _실행상태.PropertyChanged += 실행상태변경;
    }

    [ObservableProperty]
    public partial 주문하위원장연결ClientRequest 연결초안 { get; private set; } = new()
    {
        역할 = 주문원장포함역할.판매,
        필수여부 = true
    };

    public IReadOnlyList<string> 연결가능역할 => Roles;
    public string? 주문원장Id => _실행상태.선택된주문원장Id;
    public 주문원장통합공개Dto? 통합결과 => _실행상태.주문원장통합결과;
    public long? 현재Revision => 통합결과?.주문원장.Revision
        ?? _실행상태.주문원장역할결과?.주문원장상세?.Revision;

    public async Task<bool> 연결Async(CancellationToken cancellationToken = default)
    {
        var rootId = 주문원장Id;
        if (string.IsNullOrWhiteSpace(rootId))
        {
            return 유효성실패("하위 원장을 연결할 주문 루트 원장을 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(연결초안.하위원장Id))
        {
            return 유효성실패("연결할 하위 원장 ID를 입력해 주세요.");
        }

        if (string.Equals(rootId, 연결초안.하위원장Id.Trim(), StringComparison.Ordinal))
        {
            return 유효성실패("주문 루트 원장을 자기 자신의 하위 원장으로 연결할 수 없습니다.");
        }

        if (!주문원장포함역할.All.Contains(연결초안.역할))
        {
            return 유효성실패("하위 원장의 업무 역할을 선택해 주세요.");
        }

        연결초안.기대Revision ??= 현재Revision;
        return await 작업실행Async(
            async token =>
            {
                var result = await _service.하위원장연결Async(rootId, 연결초안, token)
                    ?? throw new InvalidOperationException("하위 원장 연결 응답이 비어 있습니다.");
                _실행상태.주문원장통합적용(result);
                연결초안 = new 주문하위원장연결ClientRequest
                {
                    역할 = 연결초안.역할,
                    필수여부 = true,
                    기대Revision = result.주문원장.Revision
                };
            },
            "하위 원장을 주문 루트 원장에 연결했습니다.",
            cancellationToken);
    }

    public async Task<bool> 분리Async(
        string childLedgerId,
        CancellationToken cancellationToken = default)
    {
        var rootId = 주문원장Id;
        if (string.IsNullOrWhiteSpace(rootId))
        {
            return 유효성실패("하위 원장을 분리할 주문 루트 원장을 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(childLedgerId))
        {
            return 유효성실패("분리할 하위 원장 ID를 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.하위원장분리Async(rootId, childLedgerId, 현재Revision, token)
                    ?? throw new InvalidOperationException("하위 원장 분리 응답이 비어 있습니다.");
                _실행상태.주문원장통합적용(result);
                연결초안.기대Revision = result.주문원장.Revision;
                OnPropertyChanged(nameof(연결초안));
            },
            "하위 원장을 주문 루트 원장에서 분리했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
        => OnPropertyChanged(nameof(연결초안));

    public void Dispose()
    {
        _실행상태.PropertyChanged -= 실행상태변경;
        GC.SuppressFinalize(this);
    }

    private void 실행상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName)
            && e.PropertyName is not nameof(공동구매실행상태ViewModel.선택된주문원장Id)
                and not nameof(공동구매실행상태ViewModel.주문원장통합결과)
                and not nameof(공동구매실행상태ViewModel.주문원장역할결과))
        {
            return;
        }

        OnPropertyChanged(nameof(주문원장Id));
        OnPropertyChanged(nameof(통합결과));
        OnPropertyChanged(nameof(현재Revision));
    }
}

/// <summary>
/// 주문 원장의 계약 문서 준비, 서명 제출과 서명 완료 상태를 담당합니다.
/// </summary>
public sealed partial class 공동구매주문원장서명ViewModel : 공동구매주문원장실행업무ViewModelBase, IDisposable
{
    private readonly I주문원장Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매실행상태ViewModel _실행상태;
    private Guid? _대상공동구매Id;
    private string? _대상결의문번호;
    private string? _대상결의문Hash;

    public 공동구매주문원장서명ViewModel(
        I주문원장Service service,
        공동구매화면상태ViewModel 화면상태,
        공동구매실행상태ViewModel 실행상태)
        : base(화면상태, 실행상태)
    {
        _service = service;
        _화면상태 = 화면상태;
        _실행상태 = 실행상태;
        _화면상태.PropertyChanged += 화면상태변경;
        _실행상태.PropertyChanged += 실행상태변경;
        공동구매변경동기화();
    }

    [ObservableProperty]
    public partial 주문원장서명준비ClientRequest 서명준비초안 { get; private set; } = new();

    [ObservableProperty]
    public partial 주문원장서명등록ClientRequest 서명등록초안 { get; private set; } = new();

    public string? 주문원장Id => _실행상태.선택된주문원장Id;
    public 주문원장서명상태공개Dto? 서명상태 => _실행상태.주문원장서명상태;
    public bool 전체서명완료 => 서명상태?.전체서명완료여부 == true;
    public string? 참고공동구매결의문번호 => _대상결의문번호;
    public string? 참고공동구매결의문Hash => _대상결의문Hash;
    public string 계약문서안내
        => $"공동구매 결의문은 합의 근거입니다. 서명에는 발주 단계에서 만든 개별 주문계약의 문서번호와 해시를 입력해 주세요. {CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice}";

    public async Task<bool> 상태조회Async(CancellationToken cancellationToken = default)
    {
        var orderLedgerId = 주문원장Id;
        if (string.IsNullOrWhiteSpace(orderLedgerId))
        {
            return 유효성실패("서명 상태를 확인할 주문 루트 원장을 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await _service.주문원장서명상태조회Async(orderLedgerId, token)
                    ?? throw new InvalidOperationException("주문원장 서명 상태 응답이 비어 있습니다.");
                _실행상태.주문원장서명적용(result);
                Revision동기화(result.Revision);
            },
            "주문원장 서명 상태를 확인했습니다.",
            cancellationToken);
    }

    public async Task<bool> 서명준비Async(CancellationToken cancellationToken = default)
    {
        var orderLedgerId = 주문원장Id;
        if (string.IsNullOrWhiteSpace(orderLedgerId))
        {
            return 유효성실패("서명을 준비할 주문 루트 원장을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(서명준비초안.계약문서번호)
            || string.IsNullOrWhiteSpace(서명준비초안.문서Hash))
        {
            return 유효성실패("계약 문서번호와 문서 해시를 입력해 주세요.");
        }

        서명준비초안.기대Revision ??= _실행상태.주문원장통합결과?.주문원장.Revision;
        return await 작업실행Async(
            async token =>
            {
                var result = await _service.주문원장서명준비Async(orderLedgerId, 서명준비초안, token)
                    ?? throw new InvalidOperationException("주문원장 서명 준비 응답이 비어 있습니다.");
                _실행상태.주문원장서명적용(result);
                서명등록초안.문서Hash = 서명준비초안.문서Hash;
                Revision동기화(result.Revision);
            },
            "주문원장 계약 문서의 서명 요청을 준비했습니다.",
            cancellationToken);
    }

    public async Task<bool> 서명등록Async(CancellationToken cancellationToken = default)
    {
        var orderLedgerId = 주문원장Id;
        if (string.IsNullOrWhiteSpace(orderLedgerId))
        {
            return 유효성실패("서명할 주문 루트 원장을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(서명등록초안.문서Hash)
            || string.IsNullOrWhiteSpace(서명등록초안.동의문Hash)
            || string.IsNullOrWhiteSpace(서명등록초안.서명증적Hash))
        {
            return 유효성실패("문서, 동의문과 서명 증적의 해시를 모두 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(서명등록초안.서명방법Code))
        {
            서명등록초안.서명방법Code = ContractSignatureMethodCode.PlatformClickSign;
        }

        서명등록초안.기대Revision ??= 서명상태?.Revision;
        return await 작업실행Async(
            async token =>
            {
                var result = await _service.주문원장서명등록Async(orderLedgerId, 서명등록초안, token)
                    ?? throw new InvalidOperationException("주문원장 서명 등록 응답이 비어 있습니다.");
                _실행상태.주문원장서명적용(result);
                Revision동기화(result.Revision);
            },
            "주문원장 계약 문서에 서명했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
    {
        OnPropertyChanged(nameof(서명준비초안));
        OnPropertyChanged(nameof(서명등록초안));
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        _실행상태.PropertyChanged -= 실행상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(공동구매화면상태ViewModel.선택된공동구매)
                or nameof(공동구매화면상태ViewModel.선택된공동구매Id))
        {
            공동구매변경동기화();
        }
    }

    private void 실행상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName)
            && e.PropertyName is not nameof(공동구매실행상태ViewModel.선택된주문원장Id)
                and not nameof(공동구매실행상태ViewModel.주문원장서명상태))
        {
            return;
        }

        OnPropertyChanged(nameof(주문원장Id));
        OnPropertyChanged(nameof(서명상태));
        OnPropertyChanged(nameof(전체서명완료));
    }

    private void 공동구매변경동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        var resolution = campaign?.ResolutionDocument;
        var campaignChanged = _대상공동구매Id != campaign?.Id;
        var resolutionChanged = !string.Equals(_대상결의문번호, resolution?.DocumentNumber, StringComparison.Ordinal)
                                || !string.Equals(_대상결의문Hash, resolution?.DocumentHash, StringComparison.Ordinal);
        if (!campaignChanged && !resolutionChanged)
        {
            return;
        }

        _대상공동구매Id = campaign?.Id;
        _대상결의문번호 = resolution?.DocumentNumber;
        _대상결의문Hash = resolution?.DocumentHash;
        OnPropertyChanged(nameof(참고공동구매결의문번호));
        OnPropertyChanged(nameof(참고공동구매결의문Hash));

        if (!campaignChanged)
        {
            return;
        }

        서명준비초안 = new 주문원장서명준비ClientRequest();
        서명등록초안 = new 주문원장서명등록ClientRequest
        {
            서명방법Code = ContractSignatureMethodCode.PlatformClickSign
        };
        작업상태초기화();
    }

    private void Revision동기화(long revision)
    {
        서명준비초안.기대Revision = revision;
        서명등록초안.기대Revision = revision;
        OnPropertyChanged(nameof(서명준비초안));
        OnPropertyChanged(nameof(서명등록초안));
    }
}

public sealed class 공동구매주문원장ViewModel : 조립ViewModelBase
{
    public 공동구매주문원장ViewModel(
        주문ViewModel 기본주문,
        공동구매주문원장조회ViewModel 조회,
        공동구매하위원장ViewModel 하위원장,
        공동구매주문원장서명ViewModel 서명)
    {
        this.기본주문 = 하위ViewModel등록(기본주문);
        this.조회 = 하위ViewModel등록(조회, 수명소유: false);
        this.하위원장 = 하위ViewModel등록(하위원장, 수명소유: false);
        this.서명 = 하위ViewModel등록(서명, 수명소유: false);
    }

    public 주문ViewModel 기본주문 { get; }
    public 공동구매주문원장조회ViewModel 조회 { get; }
    public 공동구매하위원장ViewModel 하위원장 { get; }
    public 공동구매주문원장서명ViewModel 서명 { get; }
    public bool 처리중 => 기본주문.처리중 || 조회.처리중 || 하위원장.처리중 || 서명.처리중;

    public void 주문원장선택(string? orderLedgerId)
    {
        기본주문.조회.주문원장선택(orderLedgerId);
        조회.주문원장선택(orderLedgerId);
    }
}
