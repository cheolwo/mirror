using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class 공동구매업무분기코드
{
    public const string 미선택 = "none";
    public const string 국내공동구매 = "domestic-group-purchase";
    public const string 공동수입 = "group-import";
    public const string 해외수출 = "overseas-export";
    public const string 기타국경간거래 = "other-cross-border";
    public const string 검토필요 = "review-required";
}

/// <summary>
/// 선택된 공동구매가 있으면 서버가 확정한 거래경로를 사용하고,
/// 제안 작성 중이면 제안 하위 ViewModel의 실시간 판정을 사용합니다.
/// </summary>
public sealed class 공동구매거래경로분기ViewModel : 공동구매의사결정업무ViewModelBase, IDisposable
{
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로판정ViewModel _제안거래경로;

    public 공동구매거래경로분기ViewModel(
        공동구매화면상태ViewModel 화면상태,
        공동구매거래경로판정ViewModel 제안거래경로)
        : base(화면상태)
    {
        _화면상태 = 화면상태;
        _제안거래경로 = 제안거래경로;
        _화면상태.PropertyChanged += 입력변경;
        _제안거래경로.PropertyChanged += 입력변경;
    }

    public string 현재거래경로코드
    {
        get
        {
            var campaign = _화면상태.선택된공동구매;
            if (campaign is null)
            {
                return _제안거래경로.거래경로코드;
            }

            var explicitRouteCode = campaign.GroupPurchase?.TradeRouteCode;
            if (!string.IsNullOrWhiteSpace(explicitRouteCode))
            {
                return explicitRouteCode;
            }

            return CommunityVoteWorkflowClassifier.IsGroupImport(campaign)
                ? CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate
                : CommunityGroupPurchaseTradeRouteCodes.Domestic;
        }
    }

    public string 활성분기코드
    {
        get
        {
            var routeCode = 현재거래경로코드;
            if (string.Equals(
                    routeCode,
                    CommunityGroupPurchaseTradeRouteCodes.Domestic,
                    StringComparison.OrdinalIgnoreCase))
            {
                return 공동구매업무분기코드.국내공동구매;
            }

            if (CommunityGroupPurchaseTradeRouteCodes.IsGroupImport(routeCode))
            {
                return 공동구매업무분기코드.공동수입;
            }

            if (string.Equals(
                    routeCode,
                    CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder,
                    StringComparison.OrdinalIgnoreCase))
            {
                return 해외수출판정
                    ? 공동구매업무분기코드.해외수출
                    : 공동구매업무분기코드.기타국경간거래;
            }

            return string.Equals(
                    routeCode,
                    CommunityGroupPurchaseTradeRouteCodes.ReviewRequired,
                    StringComparison.OrdinalIgnoreCase)
                ? 공동구매업무분기코드.검토필요
                : 공동구매업무분기코드.미선택;
        }
    }

    public bool 국내공동구매활성
        => 활성분기코드 == 공동구매업무분기코드.국내공동구매;

    public bool 공동수입활성
        => 활성분기코드 == 공동구매업무분기코드.공동수입;

    public bool 국내판매활성
        => string.Equals(
            현재거래경로코드,
            CommunityGroupPurchaseTradeRouteCodes.Domestic,
            StringComparison.OrdinalIgnoreCase);

    public bool 해외수출활성
        => 활성분기코드 == 공동구매업무분기코드.해외수출;

    public bool 기타국경간거래활성
        => 활성분기코드 == 공동구매업무분기코드.기타국경간거래;

    public bool 검토필요
        => 활성분기코드 is 공동구매업무분기코드.검토필요
            or 공동구매업무분기코드.미선택;

    public string 활성분기명
        => 활성분기코드 switch
        {
            공동구매업무분기코드.국내공동구매 => "국내 공동구매",
            공동구매업무분기코드.공동수입 => "공동수입",
            공동구매업무분기코드.해외수출 => "해외 수출",
            공동구매업무분기코드.기타국경간거래 => "기타 국경 간 거래",
            공동구매업무분기코드.검토필요 => "거래경로 검토 필요",
            _ => "공동구매 미선택"
        };

    public string 분기안내
        => 활성분기코드 switch
        {
            공동구매업무분기코드.국내공동구매
                => "운영 국가 안의 생산자·공동구매 대표 연결, 공급 협상과 이행계획을 사용합니다.",
            공동구매업무분기코드.공동수입
                => "해외 판매자 조건, HS 코드, 통관과 공동수입 원장 준비 흐름을 사용합니다.",
            공동구매업무분기코드.해외수출
                => "운영 국가 상품의 해외 출품, 수출 신고, 국제운송과 현지 이행 준비 흐름을 사용합니다.",
            공동구매업무분기코드.기타국경간거래
                => "운영 국가가 출발지나 도착지가 아닌 거래이므로 별도 국경 간 거래 흐름이 필요합니다.",
            _ => "상품 출발국가, 최종 배송국가와 도착국 통관 상태를 확인해 주세요."
        };

    private bool 해외수출판정
    {
        get
        {
            var settings = _화면상태.선택된공동구매?.GroupPurchase;
            var shipFrom = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
                settings?.ShipFromCountryCode ?? _제안거래경로.상품출발국가코드);
            var delivery = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
                settings?.DeliveryCountryCode ?? _제안거래경로.최종배송국가코드);
            var operatingMarket = CommunityGroupPurchaseTradeRoutePolicy
                .NormalizeOperatingMarketCountryCode(
                    settings?.OperatingMarketCountryCode ?? _제안거래경로.운영국가코드);
            return string.Equals(
                       shipFrom,
                       operatingMarket,
                       StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(delivery)
                   && !string.Equals(
                       delivery,
                       operatingMarket,
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 입력변경;
        _제안거래경로.PropertyChanged -= 입력변경;
        GC.SuppressFinalize(this);
    }

    private void 입력변경(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);
}

/// <summary>
/// 국내 공동구매에서만 사용하는 공급 연결·협상·국내 물류 하위 기능입니다.
/// </summary>
public sealed class 국내공동구매분기ViewModel : 조립ViewModelBase
{
    private readonly 공동구매화면상태ViewModel _화면상태;
    private string? _단계오류메시지;

    public 국내공동구매분기ViewModel(
        공동구매거래경로분기ViewModel 분기,
        공동구매화면상태ViewModel 화면상태,
        공동구매공급기능ViewModel 공급,
        공동구매물류기능ViewModel 물류,
        공동구매가격의사결정ViewModel 가격의사결정)
    {
        this.분기 = 하위ViewModel등록(분기, 수명소유: false);
        _화면상태 = 하위ViewModel등록(화면상태, 수명소유: false);
        this.공급 = 하위ViewModel등록(공급);
        this.물류 = 하위ViewModel등록(물류);
        this.가격의사결정 = 하위ViewModel등록(가격의사결정, 수명소유: false);
    }

    public 공동구매거래경로분기ViewModel 분기 { get; }
    public 공동구매공급기능ViewModel 공급 { get; }
    public 공동구매물류기능ViewModel 물류 { get; }
    public 공동구매가격의사결정ViewModel 가격의사결정 { get; }
    public bool 활성 => 분기.국내공동구매활성;
    public bool 처리중 => 공급.처리중 || 물류.처리중 || 가격의사결정.처리중;

    public string? 단계오류메시지
    {
        get => _단계오류메시지;
        private set => SetProperty(ref _단계오류메시지, value);
    }

    public async Task<bool> 거래상대연결완료Async(CancellationToken cancellationToken = default)
    {
        if (_화면상태.선택된공동구매 is null)
        {
            return 실패("거래 상대를 연결할 국내 공동구매를 먼저 선택해 주세요.");
        }

        if (!활성)
        {
            return 실패("국내 공동구매 분기가 활성화된 경우에만 국내 거래 상대를 연결할 수 있습니다.");
        }

        if (공급.생산자연결.저장된연락요청 is null
            && 공급.공급제안.저장된공급제안 is null)
        {
            return 실패("생산자 연락 요청 또는 생산자의 공급 제안 초안을 먼저 저장해 주세요.");
        }

        단계오류메시지 = null;
        await _화면상태.단계도달Async(
            공동구매절차코드.공급조건협상,
            "국내 생산자 또는 공동구매 대표 연결을 완료했습니다.",
            cancellationToken);
        return true;
    }

    public async Task<bool> 공급조건확정Async(CancellationToken cancellationToken = default)
    {
        if (_화면상태.선택된공동구매 is null)
        {
            return 실패("공급 조건을 확정할 국내 공동구매를 먼저 선택해 주세요.");
        }

        if (!활성)
        {
            return 실패("국내 공동구매 분기가 활성화된 경우에만 국내 공급 조건을 확정할 수 있습니다.");
        }

        if (공급.공급적합성.판정결과 is null)
        {
            return 실패("구매자와 생산자의 공급 적합성을 먼저 확인해 주세요.");
        }

        if (!공급.공급적합성.상호공급가능)
        {
            return 실패("상호 이행 가능한 공급 조건으로 조정한 뒤 다시 판정해 주세요.");
        }

        if (공급.협상.미해결쟁점수 > 0)
        {
            return 실패($"아직 합의되지 않은 협상 쟁점이 {공급.협상.미해결쟁점수}건 있습니다.");
        }

        단계오류메시지 = null;
        await _화면상태.단계도달Async(
            공동구매절차코드.이의검토,
            "국내 공동구매 공급 조건 협상을 마치고 최종 이의 검토 단계로 진행했습니다.",
            cancellationToken);
        return true;
    }

    public async Task<bool> 최종이의검토완료Async(CancellationToken cancellationToken = default)
    {
        if (_화면상태.선택된공동구매 is null)
        {
            return 실패("이의를 검토할 국내 공동구매를 먼저 선택해 주세요.");
        }

        if (!활성)
        {
            return 실패("국내 공동구매 분기가 활성화된 경우에만 최종안을 확정할 수 있습니다.");
        }

        단계오류메시지 = null;
        await _화면상태.단계도달Async(
            공동구매절차코드.확정안,
            "국내 공동구매 최종 이의 검토를 완료했습니다.",
            cancellationToken);
        return true;
    }

    private bool 실패(string message)
    {
        단계오류메시지 = message;
        return false;
    }
}

/// <summary>
/// 공동수입에서만 사용하는 해외 판매자·HS 코드·통관 조건 확인 분기입니다.
/// 실제 원장 생성 전까지는 후보 상태를 유지합니다.
/// </summary>
public sealed class 공동수입분기ViewModel : 조립ViewModelBase
{
    private readonly 공동구매화면상태ViewModel _화면상태;
    private string? _단계오류메시지;

    public 공동수입분기ViewModel(
        공동구매거래경로분기ViewModel 분기,
        공동구매화면상태ViewModel 화면상태,
        공동수입전환준비ViewModel 전환준비,
        공동수입원장물류ViewModel 원장물류,
        공동수입선적통관ViewModel 선적통관,
        공동구매가격의사결정ViewModel 가격의사결정,
        공동수입원장조회ViewModel 원장조회,
        공동수입원장미리보기ViewModel 원장미리보기,
        공동수입원장전환ViewModel 원장전환,
        공동수입선적공개조회ViewModel 선적공개조회,
        공동수입선적관리목록조회ViewModel 선적관리목록조회,
        공동수입선적등록ViewModel 선적등록,
        공동수입선적이벤트등록ViewModel 선적이벤트등록,
        공동수입통관동기화ViewModel 통관동기화)
    {
        this.분기 = 하위ViewModel등록(분기, 수명소유: false);
        _화면상태 = 하위ViewModel등록(화면상태, 수명소유: false);
        this.전환준비 = 하위ViewModel등록(전환준비, 수명소유: false);
        this.원장물류 = 하위ViewModel등록(원장물류, 수명소유: false);
        this.선적통관 = 하위ViewModel등록(선적통관, 수명소유: false);
        this.선적통관.원장물류연결(this.원장물류);
        this.가격의사결정 = 하위ViewModel등록(가격의사결정, 수명소유: false);
        세부업무목록 =
        [
            하위ViewModel등록(원장조회, 수명소유: false),
            하위ViewModel등록(원장미리보기, 수명소유: false),
            하위ViewModel등록(원장전환, 수명소유: false),
            하위ViewModel등록(선적공개조회, 수명소유: false),
            하위ViewModel등록(선적관리목록조회, 수명소유: false),
            하위ViewModel등록(선적등록, 수명소유: false),
            하위ViewModel등록(선적이벤트등록, 수명소유: false),
            하위ViewModel등록(통관동기화, 수명소유: false)
        ];
    }

    public 공동구매거래경로분기ViewModel 분기 { get; }
    public 공동수입전환준비ViewModel 전환준비 { get; }
    public 공동수입원장물류ViewModel 원장물류 { get; }
    public 공동수입선적통관ViewModel 선적통관 { get; }
    public 공동구매가격의사결정ViewModel 가격의사결정 { get; }
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }
    public bool 활성 => 분기.공동수입활성;
    public bool 처리중 => 원장물류.처리중 || 선적통관.처리중 || 가격의사결정.처리중;

    public string 현재HS코드
    {
        get
        {
            var campaign = _화면상태.선택된공동구매;
            return campaign?.Options.FirstOrDefault(option => !string.IsNullOrWhiteSpace(option.HsCode))?.HsCode
                   ?? campaign?.GroupPurchase?.HsCode
                   ?? 전환준비.HS코드;
        }
    }

    public bool 계약확정준비완료
    {
        get
        {
            if (!활성)
            {
                return false;
            }

            var campaign = _화면상태.선택된공동구매;
            if (campaign is null)
            {
                return 전환준비.계약확정준비완료;
            }

            var hsCode = new string(현재HS코드.Where(char.IsDigit).ToArray());
            return campaign.GroupPurchase?.RequiresTradeRouteReview != true
                   && hsCode.Length is >= 2 and <= 10;
        }
    }

    public IReadOnlyList<string> 계약확정누락정보
    {
        get
        {
            var settings = _화면상태.선택된공동구매?.GroupPurchase;
            var missing = settings is null
                ? 전환준비.계약확정누락정보.ToList()
                : settings.TradeRouteMissingFieldCodes
                    .Concat(settings.TradeRouteInvalidFieldCodes)
                    .Select(필드표시)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            var normalizedHsCode = new string(현재HS코드.Where(char.IsDigit).ToArray());
            if (normalizedHsCode.Length is < 2 or > 10
                && !missing.Contains("유효한 HS 코드", StringComparer.Ordinal))
            {
                missing.Remove("HS 코드");
                missing.Add("유효한 HS 코드");
            }

            if (settings?.RequiresTradeRouteReview == true && missing.Count == 0)
            {
                missing.Add("거래경로 판정");
            }

            return missing;
        }
    }

    public string? 단계오류메시지
    {
        get => _단계오류메시지;
        private set => SetProperty(ref _단계오류메시지, value);
    }

    public string 다음작업안내
        => _화면상태.진행단계코드 switch
        {
            공동구매절차코드.거래상대연결 => "해외 판매자와 상품 출발지·공급 조건을 확인할 차례입니다.",
            공동구매절차코드.공급조건협상 => "HS 코드, 통관 책임, 국제운송과 국내 인도 조건을 확정할 차례입니다.",
            공동구매절차코드.이의검토 => "공동수입 조건을 공개하고 최종 이의를 검토할 차례입니다.",
            공동구매절차코드.이행계획 => "원천 공동구매와 연결된 공동수입 원장을 준비할 차례입니다.",
            _ => CommunityGroupPurchaseTradeRoutePolicy.GroupImportCandidateNotice
        };

    public async Task<bool> 해외판매자연결완료Async(CancellationToken cancellationToken = default)
    {
        if (_화면상태.선택된공동구매 is null)
        {
            return 실패("해외 판매자를 연결할 공동수입을 먼저 선택해 주세요.");
        }

        if (!활성)
        {
            return 실패("공동수입 분기가 활성화된 경우에만 해외 판매자 연결을 확정할 수 있습니다.");
        }

        단계오류메시지 = null;
        await _화면상태.단계도달Async(
            공동구매절차코드.공급조건협상,
            "해외 판매자 연결을 완료하고 공동수입 조건 협상 단계로 진행했습니다.",
            cancellationToken);
        return true;
    }

    public async Task<bool> 수입조건확정Async(CancellationToken cancellationToken = default)
    {
        if (_화면상태.선택된공동구매 is null)
        {
            return 실패("수입 조건을 확정할 공동수입을 먼저 선택해 주세요.");
        }

        if (!활성)
        {
            return 실패("공동수입 분기가 활성화된 경우에만 수입 조건을 확정할 수 있습니다.");
        }

        if (!계약확정준비완료)
        {
            var missing = 계약확정누락정보.Count == 0
                ? "거래경로와 통관 정보"
                : string.Join(", ", 계약확정누락정보);
            return 실패($"공동수입 조건을 확정하려면 {missing}를 확인해 주세요.");
        }

        단계오류메시지 = null;
        await _화면상태.단계도달Async(
            공동구매절차코드.이의검토,
            "공동수입 조건을 확정하고 최종 이의 검토 단계로 진행했습니다.",
            cancellationToken);
        return true;
    }

    public async Task<bool> 최종이의검토완료Async(CancellationToken cancellationToken = default)
    {
        if (_화면상태.선택된공동구매 is null)
        {
            return 실패("이의를 검토할 공동수입을 먼저 선택해 주세요.");
        }

        if (!활성)
        {
            return 실패("공동수입 분기가 활성화된 경우에만 최종안을 확정할 수 있습니다.");
        }

        단계오류메시지 = null;
        await _화면상태.단계도달Async(
            공동구매절차코드.확정안,
            "공동수입 최종 이의 검토를 완료했습니다.",
            cancellationToken);
        return true;
    }

    private bool 실패(string message)
    {
        단계오류메시지 = message;
        return false;
    }

    private static string 필드표시(string fieldCode)
        => fieldCode switch
        {
            CommunityGroupPurchaseTradeRouteFieldCodes.SellerCountryCode => "판매자 국가",
            CommunityGroupPurchaseTradeRouteFieldCodes.ShipFromCountryCode => "상품 출발국가",
            CommunityGroupPurchaseTradeRouteFieldCodes.DeliveryCountryCode => "최종 배송국가",
            CommunityGroupPurchaseTradeRouteFieldCodes.CustomsClearanceStatusCode => "국내 통관 상태",
            CommunityGroupPurchaseTradeRouteFieldCodes.HsCode => "HS 코드",
            _ => fieldCode
        };
}
