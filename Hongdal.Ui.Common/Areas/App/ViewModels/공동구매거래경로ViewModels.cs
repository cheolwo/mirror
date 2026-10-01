using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 판매자 국적이 아닌 상품의 실제 출발지·배송지·통관 상태로 공동구매 거래경로를 판정합니다.
/// </summary>
public sealed partial class 공동구매거래경로판정ViewModel : ObservableObject
{
    private readonly IOperatingMarketProfileClient? _operatingMarketProfileClient;

    public 공동구매거래경로판정ViewModel(
        IOperatingMarketProfileClient? operatingMarketProfileClient = null)
    {
        _operatingMarketProfileClient = operatingMarketProfileClient;
    }

    [ObservableProperty]
    public partial string 운영국가코드 { get; set; }
        = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode;

    [ObservableProperty]
    public partial string 판매자국가코드 { get; set; }
        = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode;

    [ObservableProperty]
    public partial string 상품출발국가코드 { get; set; }
        = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode;

    [ObservableProperty]
    public partial string 최종배송국가코드 { get; set; }
        = CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode;

    [ObservableProperty]
    public partial string 국내통관상태코드 { get; set; }
        = CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared;

    public CommunityGroupPurchaseTradeRouteDecision 판정
        => CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                판매자국가코드,
                상품출발국가코드,
                최종배송국가코드,
                국내통관상태코드,
                운영국가코드));

    public string 거래경로코드 => 판정.RouteCode;

    public bool 공동수입후보 => 판정.IsGroupImportCandidate;

    public bool 해외수출후보
        => 거래경로코드 == CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder
           && string.Equals(
               CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(상품출발국가코드),
               정규화운영국가코드,
               StringComparison.OrdinalIgnoreCase)
           && !string.Equals(
               CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(최종배송국가코드),
               정규화운영국가코드,
               StringComparison.OrdinalIgnoreCase);

    public string 정규화운영국가코드
        => CommunityGroupPurchaseTradeRoutePolicy.NormalizeOperatingMarketCountryCode(
            운영국가코드);

    public string 운영국가표시명
        => 정규화운영국가코드 switch
        {
            CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode => "한국",
            CommunityGroupPurchaseTradeRoutePolicy.UnitedStatesCountryCode => "미국",
            _ => 정규화운영국가코드
        };

    public bool 검토필요 => 판정.RequiresManualReview;

    public bool 입력유효
        => 판정.InvalidFieldCodes.Count == 0
           && !판정.MissingFieldCodes.Contains(
               CommunityGroupPurchaseTradeRouteFieldCodes.ShipFromCountryCode,
               StringComparer.Ordinal)
           && !판정.MissingFieldCodes.Contains(
               CommunityGroupPurchaseTradeRouteFieldCodes.DeliveryCountryCode,
               StringComparer.Ordinal);

    public string 판정명
        => 거래경로코드 switch
        {
            CommunityGroupPurchaseTradeRouteCodes.Domestic
                => 정규화운영국가코드 == CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode
                    ? "국내 공동구매"
                    : $"{운영국가표시명} 내 공동구매",
            CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate => "공동수입 후보",
            CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder when 해외수출후보 => "해외 수출",
            CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder => "기타 국경 간 거래",
            _ => "거래경로 확인 필요"
        };

    public string 판정안내
        => 거래경로코드 switch
        {
            CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate
                => CommunityGroupPurchaseTradeRoutePolicy.GroupImportCandidateNotice,
            CommunityGroupPurchaseTradeRouteCodes.Domestic
                => $"상품이 {운영국가표시명} 안에서 이행되거나 이미 통관된 재고이므로 운영 국가 내 공동구매 흐름을 유지합니다.",
            CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder when 해외수출후보
                => $"{운영국가표시명}에서 출발해 다른 국가로 배송하므로 수출·국제운송 검토 흐름으로 전환합니다.",
            CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder
                => $"최종 배송국가가 {운영국가표시명}이 아니므로 운영 국가 반입이 아닌 별도 국경 간 거래 흐름에서 검토합니다.",
            _ => "상품 출발국가, 최종 배송국가와 도착국 통관 상태를 확인하면 거래경로를 판정할 수 있습니다."
        };

    public IReadOnlyList<string> 판정근거
        => 판정.ReasonCodes.Select(근거표시).ToArray();

    public IReadOnlyList<string> 누락정보
        => 판정.MissingFieldCodes.Select(필드표시).ToArray();

    public IReadOnlyList<string> 잘못된정보
        => 판정.InvalidFieldCodes.Select(필드표시).ToArray();

    public string? 입력오류메시지
    {
        get
        {
            if (잘못된정보.Count > 0)
            {
                return $"{string.Join(", ", 잘못된정보)} 값을 지원하는 코드로 입력해 주세요.";
            }

            var requiredMissing = 누락정보
                .Where(item => item is "상품 출발국가" or "최종 배송국가")
                .ToArray();
            return requiredMissing.Length > 0
                ? $"{string.Join(", ", requiredMissing)}를 입력해 주세요."
                : null;
        }
    }

    public async Task<bool> 운영시장초기화Async(
        CancellationToken cancellationToken = default)
    {
        if (_operatingMarketProfileClient is null)
        {
            return false;
        }

        try
        {
            var profile = await _operatingMarketProfileClient.GetCurrentAsync(cancellationToken);
            if (profile is null)
            {
                return false;
            }

            운영시장적용(
                string.IsNullOrWhiteSpace(profile.CountryCode)
                    ? profile.MarketCode
                    : profile.CountryCode);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public void 운영시장적용(string? countryCode)
    {
        var previousMarketCode = 정규화운영국가코드;
        var nextMarketCode = CommunityGroupPurchaseTradeRoutePolicy
            .NormalizeOperatingMarketCountryCode(countryCode);
        var resetSeller = 기본국가값(판매자국가코드, previousMarketCode);
        var resetOrigin = 기본국가값(상품출발국가코드, previousMarketCode);
        var resetDestination = 기본국가값(최종배송국가코드, previousMarketCode);

        운영국가코드 = nextMarketCode;
        if (resetSeller)
        {
            판매자국가코드 = nextMarketCode;
        }

        if (resetOrigin)
        {
            상품출발국가코드 = nextMarketCode;
        }

        if (resetDestination)
        {
            최종배송국가코드 = nextMarketCode;
        }
    }

    public void 요청에적용(CommunityGroupPurchaseVoteSettingsRequest settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.SellerCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            판매자국가코드);
        settings.ShipFromCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            상품출발국가코드);
        settings.DeliveryCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            최종배송국가코드);
        settings.CustomsClearanceStatusCode = CommunityGroupPurchaseTradeRoutePolicy
            .NormalizeCustomsClearanceStatusCode(국내통관상태코드);
    }

    partial void On판매자국가코드Changed(string oldValue, string newValue)
    {
        var previousSellerCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(oldValue);
        var currentShipFromCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            상품출발국가코드);
        if (string.IsNullOrWhiteSpace(currentShipFromCountryCode)
            || string.Equals(
                currentShipFromCountryCode,
                previousSellerCountryCode,
                StringComparison.OrdinalIgnoreCase))
        {
            상품출발국가코드 = newValue;
        }

        판정속성변경알림();
    }

    partial void On상품출발국가코드Changed(string value)
        => 판정속성변경알림();

    partial void On최종배송국가코드Changed(string value)
        => 판정속성변경알림();

    partial void On국내통관상태코드Changed(string value)
        => 판정속성변경알림();

    partial void On운영국가코드Changed(string value)
        => 판정속성변경알림();

    private void 판정속성변경알림()
    {
        OnPropertyChanged(nameof(판정));
        OnPropertyChanged(nameof(거래경로코드));
        OnPropertyChanged(nameof(공동수입후보));
        OnPropertyChanged(nameof(해외수출후보));
        OnPropertyChanged(nameof(정규화운영국가코드));
        OnPropertyChanged(nameof(운영국가표시명));
        OnPropertyChanged(nameof(검토필요));
        OnPropertyChanged(nameof(입력유효));
        OnPropertyChanged(nameof(판정명));
        OnPropertyChanged(nameof(판정안내));
        OnPropertyChanged(nameof(판정근거));
        OnPropertyChanged(nameof(누락정보));
        OnPropertyChanged(nameof(잘못된정보));
        OnPropertyChanged(nameof(입력오류메시지));
    }

    private static string 필드표시(string fieldCode)
        => fieldCode switch
        {
            CommunityGroupPurchaseTradeRouteFieldCodes.SellerCountryCode => "판매자 국가",
            CommunityGroupPurchaseTradeRouteFieldCodes.ShipFromCountryCode => "상품 출발국가",
            CommunityGroupPurchaseTradeRouteFieldCodes.DeliveryCountryCode => "최종 배송국가",
            CommunityGroupPurchaseTradeRouteFieldCodes.CustomsClearanceStatusCode => "도착국 통관 상태",
            CommunityGroupPurchaseTradeRouteFieldCodes.HsCode => "HS 코드",
            _ => fieldCode
        };

    private static string 근거표시(string reasonCode)
        => reasonCode switch
        {
            CommunityGroupPurchaseTradeRouteReasonCodes.SellerOutsideKorea => "판매자 소재 국가가 한국이 아님",
            CommunityGroupPurchaseTradeRouteReasonCodes.SameCountryFulfillment => "상품 출발국가와 최종 배송국가가 같음",
            CommunityGroupPurchaseTradeRouteReasonCodes.GoodsShipFromOutsideKorea => "상품이 해외에서 출발함",
            CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryToKorea => "최종 배송국가가 한국임",
            CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryOutsideKorea => "최종 배송국가가 한국이 아님",
            CommunityGroupPurchaseTradeRouteReasonCodes.SellerOutsideOperatingMarket => "판매자가 운영 국가 밖에 있음",
            CommunityGroupPurchaseTradeRouteReasonCodes.GoodsShipFromOutsideOperatingMarket => "상품이 운영 국가 밖에서 출발함",
            CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryToOperatingMarket => "최종 배송지가 운영 국가임",
            CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryOutsideOperatingMarket => "최종 배송지가 운영 국가 밖임",
            CommunityGroupPurchaseTradeRouteReasonCodes.AlreadyCustomsCleared => "상품이 이미 국내 통관됨",
            CommunityGroupPurchaseTradeRouteReasonCodes.CustomsClearanceRequired => "국내 반입 통관이 필요함",
            CommunityGroupPurchaseTradeRouteReasonCodes.CustomsClearanceStatusRequired => "국내 통관 상태 확인이 필요함",
            CommunityGroupPurchaseTradeRouteReasonCodes.InvalidTradeRouteInput => "거래경로 입력 코드가 올바르지 않음",
            _ => "거래경로 필수정보가 부족함"
        };

    private static bool 기본국가값(string? countryCode, string marketCode)
    {
        var normalized = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(countryCode);
        return string.IsNullOrWhiteSpace(normalized)
               || string.Equals(normalized, marketCode, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 공동수입 후보가 계약 확정 단계로 넘어가기 전에 필요한 정보를 관리합니다.
/// 제안 단계에서는 HS 코드가 없어도 후보를 만들 수 있지만 계약 확정 준비는 완료되지 않습니다.
/// </summary>
public sealed partial class 공동수입전환준비ViewModel : 조립ViewModelBase
{
    public 공동수입전환준비ViewModel(공동구매거래경로판정ViewModel 거래경로)
    {
        this.거래경로 = 하위ViewModel등록(거래경로, 수명소유: false);
    }

    public 공동구매거래경로판정ViewModel 거래경로 { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(정규화HS코드))]
    [NotifyPropertyChangedFor(nameof(HS코드유효))]
    [NotifyPropertyChangedFor(nameof(계약확정준비완료))]
    [NotifyPropertyChangedFor(nameof(계약확정누락정보))]
    public partial string HS코드 { get; set; } = string.Empty;

    public string 정규화HS코드
        => new(HS코드.Where(char.IsDigit).ToArray());

    public bool HS코드유효 => 정규화HS코드.Length is >= 2 and <= 10;

    public bool 공동수입전환대상 => 거래경로.공동수입후보;

    public bool 계약확정준비완료
        => 공동수입전환대상
           && !거래경로.검토필요
           && HS코드유효;

    public IReadOnlyList<string> 계약확정누락정보
    {
        get
        {
            var missing = 거래경로.누락정보.ToList();
            if (공동수입전환대상 && !HS코드유효)
            {
                missing.Add("HS 코드");
            }

            return missing;
        }
    }

    public void 요청에적용(
        CommunityGroupPurchaseVoteSettingsRequest settings,
        CommunityVoteOptionCreateRequest option)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(option);

        거래경로.요청에적용(settings);
        settings.HsCode = string.IsNullOrWhiteSpace(HS코드) ? string.Empty : HS코드.Trim();
        option.HsCode = settings.HsCode;
    }
}
