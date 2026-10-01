using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 공동수입선적단계옵션(string 코드, string 표시명);

public sealed class 공동수입선적초안ViewModel : 업무입력ViewModelBase
{
    private string? _추적Id;
    private string _공동구매Id = string.Empty;
    private string _주문자집단배송권키 = string.Empty;
    private string _주문자집단배송권명 = string.Empty;
    private string _상품요약 = string.Empty;
    private string _문서관리번호 = string.Empty;
    private string _운송문서유형 = 공동구매선적문서유형코드.선하증권;
    private string _운송문서번호 = string.Empty;
    private string _운송수단 = 공동구매선적운송수단코드.해상;
    private string _운송사명 = string.Empty;
    private string _선박명 = string.Empty;
    private string _항차번호 = string.Empty;
    private string _항공편번호 = string.Empty;
    private string _출발국가코드 = string.Empty;
    private string _출발항코드 = string.Empty;
    private string _도착항코드 = string.Empty;
    private DateTime? _예상출발시각Utc;
    private DateTime? _실제출발시각Utc;
    private DateTime? _예상도착시각Utc;
    private DateTime? _실제도착시각Utc;
    private string _현재상태코드 = 공동구매선적상태코드.문서등록;
    private string _현재위치요약 = string.Empty;
    private DateTime? _마지막단계시각Utc;
    private string _관리자메모 = string.Empty;

    public string? 추적Id { get => _추적Id; set => 입력값설정(ref _추적Id, 선택(value)); }

    [Required, MaxLength(100)]
    public string 공동구매Id { get => _공동구매Id; set => 입력값설정(ref _공동구매Id, 필수(value)); }

    [Required, MaxLength(200)]
    public string 주문자집단배송권키 { get => _주문자집단배송권키; set => 입력값설정(ref _주문자집단배송권키, 필수(value)); }

    [MaxLength(200)]
    public string 주문자집단배송권명 { get => _주문자집단배송권명; set => 입력값설정(ref _주문자집단배송권명, 필수(value)); }

    [MaxLength(500)]
    public string 상품요약 { get => _상품요약; set => 입력값설정(ref _상품요약, 필수(value)); }

    [Required, MaxLength(100)]
    public string 문서관리번호 { get => _문서관리번호; set => 입력값설정(ref _문서관리번호, 필수(value)); }

    [Required]
    public string 운송문서유형 { get => _운송문서유형; set => 입력값설정(ref _운송문서유형, 필수(value)); }

    [Required, MaxLength(100)]
    public string 운송문서번호 { get => _운송문서번호; set => 입력값설정(ref _운송문서번호, 필수(value)); }

    [Required]
    public string 운송수단 { get => _운송수단; set => 입력값설정(ref _운송수단, 필수(value)); }

    [MaxLength(200)]
    public string 운송사명 { get => _운송사명; set => 입력값설정(ref _운송사명, 필수(value)); }

    [MaxLength(200)]
    public string 선박명 { get => _선박명; set => 입력값설정(ref _선박명, 필수(value)); }

    [MaxLength(100)]
    public string 항차번호 { get => _항차번호; set => 입력값설정(ref _항차번호, 필수(value)); }

    [MaxLength(100)]
    public string 항공편번호 { get => _항공편번호; set => 입력값설정(ref _항공편번호, 필수(value)); }

    [MaxLength(3)]
    public string 출발국가코드 { get => _출발국가코드; set => 입력값설정(ref _출발국가코드, 코드(value)); }

    [MaxLength(10)]
    public string 출발항코드 { get => _출발항코드; set => 입력값설정(ref _출발항코드, 코드(value)); }

    [MaxLength(10)]
    public string 도착항코드 { get => _도착항코드; set => 입력값설정(ref _도착항코드, 코드(value)); }

    public DateTime? 예상출발시각Utc { get => _예상출발시각Utc; set => 입력값설정(ref _예상출발시각Utc, value); }
    public DateTime? 실제출발시각Utc { get => _실제출발시각Utc; set => 입력값설정(ref _실제출발시각Utc, value); }
    public DateTime? 예상도착시각Utc { get => _예상도착시각Utc; set => 입력값설정(ref _예상도착시각Utc, value); }
    public DateTime? 실제도착시각Utc { get => _실제도착시각Utc; set => 입력값설정(ref _실제도착시각Utc, value); }

    [Required]
    public string 현재상태코드 { get => _현재상태코드; set => 입력값설정(ref _현재상태코드, 필수(value)); }

    [MaxLength(500)]
    public string 현재위치요약 { get => _현재위치요약; set => 입력값설정(ref _현재위치요약, 필수(value)); }

    public DateTime? 마지막단계시각Utc { get => _마지막단계시각Utc; set => 입력값설정(ref _마지막단계시각Utc, value); }

    [MaxLength(2000)]
    public string 관리자메모 { get => _관리자메모; set => 입력값설정(ref _관리자메모, 필수(value)); }

    public bool 업무규칙유효
        => !(string.Equals(운송문서유형, 공동구매선적문서유형코드.항공화물운송장, StringComparison.OrdinalIgnoreCase)
             && !string.Equals(운송수단, 공동구매선적운송수단코드.항공, StringComparison.OrdinalIgnoreCase));

    public bool 필수입력완료
        => !string.IsNullOrWhiteSpace(공동구매Id)
           && !string.IsNullOrWhiteSpace(주문자집단배송권키)
           && !string.IsNullOrWhiteSpace(문서관리번호)
           && !string.IsNullOrWhiteSpace(운송문서유형)
           && !string.IsNullOrWhiteSpace(운송문서번호)
           && !string.IsNullOrWhiteSpace(운송수단);

    public 공동구매해외선적추적저장요청 요청생성()
        => new()
        {
            추적Id = 추적Id,
            공동구매Id = 공동구매Id,
            주문자집단배송권키 = 주문자집단배송권키,
            주문자집단배송권명 = 주문자집단배송권명,
            상품요약 = 상품요약,
            문서관리번호 = 문서관리번호,
            운송문서유형 = 운송문서유형,
            운송문서번호 = 운송문서번호,
            운송수단 = 운송수단,
            운송사명 = 운송사명,
            선박명 = 선박명,
            항차번호 = 항차번호,
            항공편번호 = 항공편번호,
            출발국가코드 = 출발국가코드,
            출발항코드 = 출발항코드,
            도착항코드 = 도착항코드,
            예상출발시각Utc = 예상출발시각Utc,
            실제출발시각Utc = 실제출발시각Utc,
            예상도착시각Utc = 예상도착시각Utc,
            실제도착시각Utc = 실제도착시각Utc,
            현재상태코드 = 현재상태코드,
            현재위치요약 = 현재위치요약,
            마지막단계시각Utc = 마지막단계시각Utc,
            관리자메모 = 관리자메모
        };

    public void 초기화(CommunityVoteResponse? campaign, 공동구매해외선적추적Dto? source = null)
    {
        _추적Id = source?.추적Id;
        _공동구매Id = source?.공동구매Id ?? campaign?.Id.ToString("D") ?? string.Empty;
        _주문자집단배송권키 = source?.주문자집단배송권키
            ?? campaign?.GroupPurchase?.ServiceAreaKey
            ?? (campaign is null ? string.Empty : $"group-purchase:{campaign.Id:N}");
        _주문자집단배송권명 = source?.주문자집단배송권명
            ?? campaign?.GroupPurchase?.ServiceAreaLabel
            ?? string.Empty;
        _상품요약 = source?.상품요약
            ?? (campaign is null ? string.Empty : string.Join(", ", campaign.Options.Select(option => option.Text)));
        _문서관리번호 = source?.문서관리번호 ?? string.Empty;
        _운송문서유형 = source?.운송문서유형 ?? 공동구매선적문서유형코드.선하증권;
        _운송문서번호 = source?.운송문서번호 ?? string.Empty;
        _운송수단 = source?.운송수단 ?? 공동구매선적운송수단코드.해상;
        _운송사명 = source?.운송사명 ?? string.Empty;
        _선박명 = source?.선박명 ?? string.Empty;
        _항차번호 = source?.항차번호 ?? string.Empty;
        _항공편번호 = source?.항공편번호 ?? string.Empty;
        _출발국가코드 = source?.출발국가코드 ?? campaign?.GroupPurchase?.ShipFromCountryCode ?? string.Empty;
        _출발항코드 = source?.출발항코드 ?? string.Empty;
        _도착항코드 = source?.도착항코드 ?? string.Empty;
        _예상출발시각Utc = source?.예상출발시각Utc;
        _실제출발시각Utc = source?.실제출발시각Utc;
        _예상도착시각Utc = source?.예상도착시각Utc;
        _실제도착시각Utc = source?.실제도착시각Utc;
        _현재상태코드 = source?.현재상태코드 ?? 공동구매선적상태코드.문서등록;
        _현재위치요약 = source?.현재위치요약 ?? string.Empty;
        _마지막단계시각Utc = source?.마지막단계시각Utc;
        _관리자메모 = source?.관리자메모 ?? string.Empty;
        검증초기화();
        변경확정();
        OnPropertyChanged(string.Empty);
    }

    private static string 필수(string? value) => value?.Trim() ?? string.Empty;
    private static string? 선택(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string 코드(string? value) => 필수(value).ToUpperInvariant();
}

public sealed class 공동수입선적이벤트초안ViewModel : 업무입력ViewModelBase
{
    private string _이벤트코드 = string.Empty;
    private string _표시명 = string.Empty;
    private string _위치요약 = string.Empty;
    private DateTime? _발생시각Utc;
    private string _출처주체코드 = string.Empty;
    private string _증빙참조 = string.Empty;
    private string _메모 = string.Empty;
    private bool _주문자공개여부 = true;

    [Required, MaxLength(100)]
    public string 이벤트코드 { get => _이벤트코드; set => 입력값설정(ref _이벤트코드, 정리(value)); }
    [Required, MaxLength(200)]
    public string 표시명 { get => _표시명; set => 입력값설정(ref _표시명, 정리(value)); }
    [MaxLength(500)]
    public string 위치요약 { get => _위치요약; set => 입력값설정(ref _위치요약, 정리(value)); }
    public DateTime? 발생시각Utc { get => _발생시각Utc; set => 입력값설정(ref _발생시각Utc, value); }
    [Required, MaxLength(100)]
    public string 출처주체코드 { get => _출처주체코드; set => 입력값설정(ref _출처주체코드, 정리(value)); }
    [MaxLength(500)]
    public string 증빙참조 { get => _증빙참조; set => 입력값설정(ref _증빙참조, 정리(value)); }
    [MaxLength(2000)]
    public string 메모 { get => _메모; set => 입력값설정(ref _메모, 정리(value)); }
    public bool 주문자공개여부 { get => _주문자공개여부; set => 입력값설정(ref _주문자공개여부, value); }

    public 공동구매해외선적추적이벤트추가요청 요청생성() => new()
    {
        이벤트코드 = 이벤트코드,
        표시명 = 표시명,
        위치요약 = 위치요약,
        발생시각Utc = 발생시각Utc,
        출처주체코드 = 출처주체코드,
        증빙참조 = 증빙참조,
        메모 = 메모,
        주문자공개여부 = 주문자공개여부
    };

    public bool 필수입력완료
        => !string.IsNullOrWhiteSpace(이벤트코드)
           && !string.IsNullOrWhiteSpace(표시명)
           && !string.IsNullOrWhiteSpace(출처주체코드);

    public void 초기화()
    {
        _이벤트코드 = string.Empty;
        _표시명 = string.Empty;
        _위치요약 = string.Empty;
        _발생시각Utc = null;
        _출처주체코드 = string.Empty;
        _증빙참조 = string.Empty;
        _메모 = string.Empty;
        _주문자공개여부 = true;
        검증초기화();
        변경확정();
        OnPropertyChanged(string.Empty);
    }

    private static string 정리(string? value) => value?.Trim() ?? string.Empty;
}

public sealed class 공동수입통관동기화초안ViewModel : 업무입력ViewModelBase
{
    private string _문서관리번호 = string.Empty;
    private string _통관화물관리번호 = string.Empty;
    private string _마스터선하증권번호 = string.Empty;
    private string _하우스선하증권번호 = string.Empty;
    private int? _선하증권연도;
    private bool _주문자공개여부 = true;

    [Required, MaxLength(100)]
    public string 문서관리번호 { get => _문서관리번호; set => 입력값설정(ref _문서관리번호, 정리(value)); }
    [Required, MaxLength(100)]
    public string 통관화물관리번호 { get => _통관화물관리번호; set => 입력값설정(ref _통관화물관리번호, 정리(value)); }
    [MaxLength(100)]
    public string 마스터선하증권번호 { get => _마스터선하증권번호; set => 입력값설정(ref _마스터선하증권번호, 정리(value)); }
    [MaxLength(100)]
    public string 하우스선하증권번호 { get => _하우스선하증권번호; set => 입력값설정(ref _하우스선하증권번호, 정리(value)); }
    [Range(2000, 2200)]
    public int? 선하증권연도 { get => _선하증권연도; set => 입력값설정(ref _선하증권연도, value); }
    public bool 주문자공개여부 { get => _주문자공개여부; set => 입력값설정(ref _주문자공개여부, value); }

    public 공동구매해외선적통관동기화요청 요청생성() => new()
    {
        문서관리번호 = 문서관리번호,
        통관화물관리번호 = 통관화물관리번호,
        마스터선하증권번호 = 마스터선하증권번호,
        하우스선하증권번호 = 하우스선하증권번호,
        선하증권연도 = 선하증권연도,
        주문자공개여부 = 주문자공개여부
    };

    public bool 필수입력완료
        => !string.IsNullOrWhiteSpace(문서관리번호)
           && !string.IsNullOrWhiteSpace(통관화물관리번호);

    public void 초기화(string? documentManagementNumber = null)
    {
        _문서관리번호 = documentManagementNumber?.Trim() ?? string.Empty;
        _통관화물관리번호 = string.Empty;
        _마스터선하증권번호 = string.Empty;
        _하우스선하증권번호 = string.Empty;
        _선하증권연도 = null;
        _주문자공개여부 = true;
        검증초기화();
        변경확정();
        OnPropertyChanged(string.Empty);
    }

    private static string 정리(string? value) => value?.Trim() ?? string.Empty;
}

/// <summary>
/// 공동수입 원장 뒤의 해외 선적, 이벤트, 통관 동기화를 한 흐름으로 관리합니다.
/// 공개 조회는 일반 사용자용이며 저장·동기화 메서드는 서버 관리자 권한 API가 최종 통제합니다.
/// </summary>
public sealed class 공동수입선적통관ViewModel : 공동구매작업ViewModelBase, IDisposable
{
    private readonly I공동수입선적통관Client _client;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private 공동수입원장물류ViewModel? _원장물류;
    private Guid? _대상공동구매Id;
    private 공동구매해외선적공개Dto? _공개선적;
    private IReadOnlyList<공동구매해외선적추적Dto> _관리목록 = [];
    private 공동구매해외선적추적Dto? _현재선적;
    private 공동구매해외선적통관동기화결과? _통관결과;

    public 공동수입선적통관ViewModel(
        I공동수입선적통관Client client,
        공동구매화면상태ViewModel 화면상태,
        공동구매거래경로분기ViewModel 분기)
    {
        _client = client;
        _화면상태 = 화면상태;
        _분기 = 분기;
        현재사용자Context연결(화면상태.현재사용자Context);
        선적초안 = new 공동수입선적초안ViewModel();
        이벤트초안 = new 공동수입선적이벤트초안ViewModel();
        통관초안 = new 공동수입통관동기화초안ViewModel();
        선적초안.PropertyChanged += 하위상태변경;
        이벤트초안.PropertyChanged += 하위상태변경;
        통관초안.PropertyChanged += 하위상태변경;
        _화면상태.PropertyChanged += 화면상태변경;
        _분기.PropertyChanged += 하위상태변경;
        공동구매변경동기화();
    }

    public static IReadOnlyList<공동수입선적단계옵션> 선적단계 { get; } =
    [
        new(공동구매선적상태코드.문서등록, "선적 문서 등록"),
        new(공동구매선적상태코드.해외포장완료, "해외 포장 완료"),
        new(공동구매선적상태코드.선박항공편적재, "선박·항공편 적재"),
        new(공동구매선적상태코드.운송중, "국제 운송 중"),
        new(공동구매선적상태코드.항만도착, "국내 항만·공항 도착"),
        new(공동구매선적상태코드.통관진행중, "통관 진행 중"),
        new(공동구매선적상태코드.통관완료, "통관 완료"),
        new(공동구매선적상태코드.물류대행입고준비, "국내 입고 준비"),
        new(공동구매선적상태코드.완료, "선적·통관 완료"),
        new(공동구매선적상태코드.예외, "예외·보류")
    ];

    public 공동수입선적초안ViewModel 선적초안 { get; }
    public 공동수입선적이벤트초안ViewModel 이벤트초안 { get; }
    public 공동수입통관동기화초안ViewModel 통관초안 { get; }
    public 공동구매해외선적공개Dto? 공개선적 { get => _공개선적; private set => SetProperty(ref _공개선적, value); }
    public IReadOnlyList<공동구매해외선적추적Dto> 관리목록 { get => _관리목록; private set => SetProperty(ref _관리목록, value); }
    public 공동구매해외선적추적Dto? 현재선적 { get => _현재선적; private set => SetProperty(ref _현재선적, value); }
    public 공동구매해외선적통관동기화결과? 통관결과 { get => _통관결과; private set => SetProperty(ref _통관결과, value); }
    public bool 활성 => _분기.공동수입활성;
    public bool 원장준비완료 => 활성 && _원장물류?.저장된원장?.Created == true;
    public bool 선적저장가능
        => 원장준비완료 && 선적초안.필수입력완료 && 선적초안.유효함 && 선적초안.업무규칙유효;
    public bool 이벤트추가가능
        => 원장준비완료 && 현재선적 is not null && 이벤트초안.필수입력완료 && 이벤트초안.유효함;
    public bool 통관동기화가능
        => 원장준비완료 && 현재선적 is not null && 통관초안.필수입력완료 && 통관초안.유효함;
    public string 다음작업안내
        => !활성
            ? "공동수입으로 판정된 공동구매에서 선적·통관 흐름을 사용할 수 있습니다."
            : !원장준비완료
                ? "공동수입 원장을 먼저 생성한 뒤 해외 선적 문서를 등록해 주세요."
                : 현재선적 is null
                    ? "해외 선적 문서를 등록하고 운송문서번호를 연결해 주세요."
                    : 현재선적.현재상태코드 == 공동구매선적상태코드.통관완료
                        ? "통관 완료를 확인했습니다. 선택한 국내 입고·직배송 경로로 인계해 주세요."
                        : "선적 이벤트를 갱신하고 통관화물관리번호로 통관 상태를 동기화해 주세요.";

    public void 원장물류연결(공동수입원장물류ViewModel 원장물류)
    {
        ArgumentNullException.ThrowIfNull(원장물류);
        if (ReferenceEquals(_원장물류, 원장물류))
        {
            return;
        }

        if (_원장물류 is not null)
        {
            _원장물류.PropertyChanged -= 하위상태변경;
        }

        _원장물류 = 원장물류;
        _원장물류.PropertyChanged += 하위상태변경;
        OnPropertyChanged(string.Empty);
    }

    public async Task<bool> 공개조회Async(string documentManagementNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentManagementNumber))
        {
            return 유효성실패("조회할 문서관리번호를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token => 공개선적 = await _client.공개조회Async(documentManagementNumber.Trim(), token),
            "공동수입 선적 진행 상태를 조회했습니다.",
            cancellationToken);
    }

    public async Task<bool> 관리자목록조회Async(CancellationToken cancellationToken = default)
    {
        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("선적 목록을 조회할 공동구매를 선택해 주세요.");
        }

        return await 작업실행Async(
            async token => 관리목록 = await _client.관리자목록Async(
                new 공동구매해외선적추적조회조건 { 공동구매Id = campaignId.Value.ToString("D") },
                token) ?? [],
            "공동수입 선적 관리 목록을 조회했습니다.",
            cancellationToken);
    }

    public void 현재선적선택(공동구매해외선적추적Dto shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        현재선적 = shipment;
        선적초안.초기화(_화면상태.선택된공동구매, shipment);
        이벤트초안.초기화();
        통관초안.초기화(shipment.문서관리번호);
        통관결과 = null;
        OnPropertyChanged(string.Empty);
    }

    public async Task<bool> 관리자선적저장Async(CancellationToken cancellationToken = default)
    {
        if (!원장준비완료)
        {
            return 유효성실패("공동수입 원장을 생성한 뒤 선적 문서를 저장할 수 있습니다.");
        }
        if (!선적초안.전체검증())
        {
            return 유효성실패("선적 문서의 필수 입력값을 확인해 주세요.");
        }
        if (!선적초안.업무규칙유효)
        {
            return 유효성실패("항공화물운송장은 운송수단을 항공으로 선택해야 합니다.");
        }

        return await 작업실행Async(
            async token =>
            {
                현재선적 = await _client.관리자저장Async(선적초안.요청생성(), token)
                    ?? throw new InvalidOperationException("공동수입 선적 저장 응답이 비어 있습니다.");
                선적초안.초기화(_화면상태.선택된공동구매, 현재선적);
                통관초안.초기화(현재선적.문서관리번호);
            },
            "해외 선적 문서를 공동수입 원장 흐름에 연결했습니다.",
            cancellationToken);
    }

    public async Task<bool> 관리자이벤트추가Async(CancellationToken cancellationToken = default)
    {
        if (!원장준비완료 || 현재선적 is null)
        {
            return 유효성실패("공동수입 원장과 선적 문서를 먼저 준비해 주세요.");
        }
        if (!이벤트초안.전체검증())
        {
            return 유효성실패("선적 이벤트의 코드, 표시명과 출처 주체를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                현재선적 = await _client.관리자이벤트추가Async(
                    현재선적.문서관리번호,
                    이벤트초안.요청생성(),
                    token) ?? throw new InvalidOperationException("공동수입 선적 이벤트 응답이 비어 있습니다.");
                선적초안.초기화(_화면상태.선택된공동구매, 현재선적);
                이벤트초안.초기화();
            },
            "해외 선적 진행 이벤트를 추가했습니다.",
            cancellationToken);
    }

    public async Task<bool> 관리자통관동기화Async(CancellationToken cancellationToken = default)
    {
        if (!원장준비완료 || 현재선적 is null)
        {
            return 유효성실패("공동수입 원장과 선적 문서를 먼저 준비해 주세요.");
        }
        if (!통관초안.전체검증())
        {
            return 유효성실패("문서관리번호와 통관화물관리번호를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                통관결과 = await _client.관리자통관동기화Async(통관초안.요청생성(), token)
                    ?? throw new InvalidOperationException("공동수입 통관 동기화 응답이 비어 있습니다.");
                if (통관결과.선적 is not null)
                {
                    현재선적 = 통관결과.선적;
                    선적초안.초기화(_화면상태.선택된공동구매, 현재선적);
                }
            },
            "공동수입 통관 상태를 선적 원장에 동기화했습니다.",
            cancellationToken);
    }

    public void Dispose()
    {
        선적초안.PropertyChanged -= 하위상태변경;
        이벤트초안.PropertyChanged -= 하위상태변경;
        통관초안.PropertyChanged -= 하위상태변경;
        _화면상태.PropertyChanged -= 화면상태변경;
        _분기.PropertyChanged -= 하위상태변경;
        if (_원장물류 is not null)
        {
            _원장물류.PropertyChanged -= 하위상태변경;
        }
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e) => 공동구매변경동기화();

    private void 하위상태변경(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(string.Empty);

    private void 공동구매변경동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        if (_대상공동구매Id == campaign?.Id)
        {
            return;
        }

        _대상공동구매Id = campaign?.Id;
        공개선적 = null;
        관리목록 = [];
        현재선적 = null;
        통관결과 = null;
        선적초안.초기화(campaign);
        이벤트초안.초기화();
        통관초안.초기화();
        OnPropertyChanged(string.Empty);
    }
}
