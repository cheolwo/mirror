using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 공동구매 제안가격을 국내 도·소매가 및 수입 평균단가와 비교하고,
/// 해외 공공가격은 원문 단위의 별도 참고자료로 유지합니다.
/// </summary>
public sealed partial class 공동구매가격의사결정ViewModel : 공동구매의사결정업무ViewModelBase, IDisposable
{
    private readonly I공동구매가격의사결정Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private readonly 공동수입전환준비ViewModel _공동수입전환;
    private Guid? _입력대상공동구매Id;
    private string _마지막조회조건키 = string.Empty;

    public 공동구매가격의사결정ViewModel(
        I공동구매가격의사결정Service service,
        공동구매화면상태ViewModel 화면상태,
        공동구매거래경로분기ViewModel 분기,
        공동수입전환준비ViewModel 공동수입전환)
        : base(화면상태)
    {
        _service = service;
        _화면상태 = 화면상태;
        _분기 = 분기;
        _공동수입전환 = 공동수입전환;
        _화면상태.PropertyChanged += 화면상태변경;
        _분기.PropertyChanged += 분기변경;
        _공동수입전환.PropertyChanged += 공동수입전환변경;
        선택공동구매동기화();
    }

    [ObservableProperty]
    public partial string 조회HS코드 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal 제안가격Krw { get; set; }

    [ObservableProperty]
    public partial decimal 가격기준중량Kg { get; set; } = 1m;

    [ObservableProperty]
    public partial string 수출국가코드 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int 국내조회기간일수 { get; set; } = 14;

    [ObservableProperty]
    public partial int 수입조회개월수 { get; set; } = 3;

    [ObservableProperty]
    public partial decimal? 가정환율KrwPerUsd { get; set; } = 1_350m;

    [ObservableProperty]
    public partial decimal? 추가수입비용KrwPerKg { get; set; }

    [ObservableProperty]
    public partial string 해외공공가격품목명 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 해외공공가격프로그램 { get; set; } = "SURVEY";

    [ObservableProperty]
    public partial int 해외가격시작연도 { get; set; } = DateTime.UtcNow.Year - 3;

    [ObservableProperty]
    public partial int 해외가격종료연도 { get; set; } = DateTime.UtcNow.Year;

    [ObservableProperty]
    public partial 공동구매가격의사결정결과? 결과 { get; private set; }

    public string 현재유형코드
        => _분기.국내공동구매활성
            ? 공동구매가격의사결정유형코드.국내공동구매
            : _분기.공동수입활성
                ? 공동구매가격의사결정유형코드.공동수입
                : string.Empty;

    public string 적용HS코드
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(조회HS코드))
            {
                return 숫자만(조회HS코드);
            }

            var campaign = _화면상태.선택된공동구매;
            var campaignHsCode = !string.IsNullOrWhiteSpace(campaign?.GroupPurchase?.HsCode)
                ? campaign.GroupPurchase.HsCode
                : campaign?.Options.FirstOrDefault(option => !string.IsNullOrWhiteSpace(option.HsCode))?.HsCode;
            return 숫자만(string.IsNullOrWhiteSpace(campaignHsCode)
                ? _공동수입전환.HS코드
                : campaignHsCode);
        }
    }

    public decimal? 제안단가KrwPerKg
        => 제안가격Krw > 0 && 가격기준중량Kg > 0
            ? decimal.Round(
                제안가격Krw / 가격기준중량Kg,
                2,
                MidpointRounding.AwayFromZero)
            : null;

    public bool 조회가능
        => !string.IsNullOrWhiteSpace(현재유형코드)
           && 적용HS코드.Length is >= 4 and <= 10
           && 제안단가KrwPerKg is > 0
           && (현재유형코드 != 공동구매가격의사결정유형코드.공동수입
               || 국가코드정규화(수출국가코드).Length is >= 2 and <= 3);

    public bool 가격정보최신
        => 결과 is not null
           && string.Equals(_마지막조회조건키, 조회조건키(), StringComparison.Ordinal);

    public bool 의사결정근거충분
    {
        get
        {
            var items = 결과?.기준비교목록 ?? [];
            var hasDomesticMarket = items.Any(item => item.기준코드.StartsWith("domestic-", StringComparison.Ordinal));
            if (현재유형코드 == 공동구매가격의사결정유형코드.국내공동구매)
            {
                return hasDomesticMarket;
            }

            return 현재유형코드 == 공동구매가격의사결정유형코드.공동수입
                   && hasDomesticMarket
                   && items.Any(item => item.기준코드.StartsWith("import-", StringComparison.Ordinal));
        }
    }

    public string 해외공공가격출처안내
        => "현재 해외 농수산 공공가격은 미국 USDA NASS를 지원하며, 원문 통화·단위를 유지합니다.";

    public string 현재단계가격안내
        => _화면상태.진행단계코드 switch
        {
            공동구매절차코드.제안 or 공동구매절차코드.거래경로
                => "제안가격을 입력하고 거래경로에 맞는 시장 기준가격을 확인해 주세요.",
            공동구매절차코드.수요모집
                => "참여자가 수요를 결정할 수 있도록 제안가격과 시장가격의 차이를 계속 공개합니다.",
            공동구매절차코드.거래상대연결 or 공동구매절차코드.공급조건협상
                => "생산자 또는 해외 판매자의 실제 제시가격이 바뀌면 가격정보를 다시 조회해 주세요.",
            공동구매절차코드.이의검토 or 공동구매절차코드.확정안
                => "최종 가격이 시장 기준과 달라진 이유를 이의검토 및 확정안에 함께 남겨 주세요.",
            _ => "확정 당시 가격 근거를 이행·정산 자료와 함께 유지합니다."
        };

    public async Task<bool> 조회Async(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(현재유형코드))
        {
            return 유효성실패("국내 공동구매 또는 공동수입 거래경로를 먼저 확정해 주세요.");
        }

        if (적용HS코드.Length is < 4 or > 10)
        {
            return 유효성실패("가격 비교를 위해 4~10자리 HS 코드를 입력해 주세요.");
        }

        if (제안가격Krw <= 0 || 가격기준중량Kg <= 0 || 제안단가KrwPerKg is not > 0)
        {
            return 유효성실패("공동구매 제안가격과 그 가격이 적용되는 중량(kg)을 0보다 크게 입력해 주세요.");
        }

        if (현재유형코드 == 공동구매가격의사결정유형코드.공동수입
            && 국가코드정규화(수출국가코드).Length is (< 2 or > 3))
        {
            return 유효성실패("공동수입 가격 비교에는 영문 2~3자리 수출국 코드를 입력해 주세요.");
        }

        var requestedKey = 조회조건키();
        return await 작업실행Async(
            async token =>
            {
                결과 = await _service.조회Async(
                    new 공동구매가격의사결정요청(
                        현재유형코드,
                        적용HS코드,
                        제안단가KrwPerKg.Value,
                        국가코드정규화(수출국가코드),
                        Math.Clamp(국내조회기간일수, 1, 31),
                        Math.Clamp(수입조회개월수, 1, 12),
                        가정환율KrwPerUsd,
                        추가수입비용KrwPerKg,
                        해외공공가격품목명.Trim(),
                        해외공공가격프로그램.Trim(),
                        해외가격시작연도,
                        해외가격종료연도),
                    token);
                _마지막조회조건키 = requestedKey;
                결과속성변경알림();
            },
            "공동구매 가격 의사결정 자료를 갱신했습니다.",
            cancellationToken,
            ex => $"가격 의사결정 자료를 조회하지 못했습니다. {ex.Message}");
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        _분기.PropertyChanged -= 분기변경;
        _공동수입전환.PropertyChanged -= 공동수입전환변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(공동구매화면상태ViewModel.선택된공동구매))
        {
            선택공동구매동기화();
        }

        OnPropertyChanged(nameof(현재단계가격안내));
    }

    private void 분기변경(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(현재유형코드));
        입력속성변경알림();
    }

    private void 공동수입전환변경(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(조회HS코드))
        {
            OnPropertyChanged(nameof(적용HS코드));
            입력속성변경알림();
        }
    }

    private void 선택공동구매동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        if (_입력대상공동구매Id == campaign?.Id)
        {
            return;
        }

        _입력대상공동구매Id = campaign?.Id;
        if (campaign is null)
        {
            return;
        }

        조회HS코드 = !string.IsNullOrWhiteSpace(campaign.GroupPurchase?.HsCode)
            ? campaign.GroupPurchase.HsCode
            : campaign.Options.FirstOrDefault(option => !string.IsNullOrWhiteSpace(option.HsCode))?.HsCode
              ?? string.Empty;
        수출국가코드 = string.IsNullOrWhiteSpace(campaign.GroupPurchase?.ShipFromCountryCode)
            ? campaign.GroupPurchase?.SellerCountryCode ?? string.Empty
            : campaign.GroupPurchase.ShipFromCountryCode;
        제안가격Krw = campaign.GroupPurchase?.TargetUnitPriceKrwPerKg ?? 0m;
        가격기준중량Kg = 1m;

        결과 = null;
        _마지막조회조건키 = string.Empty;
        결과속성변경알림();
    }

    private string 조회조건키()
        => string.Join(
            '|',
            현재유형코드,
            적용HS코드,
            제안가격Krw,
            가격기준중량Kg,
            국가코드정규화(수출국가코드),
            Math.Clamp(국내조회기간일수, 1, 31),
            Math.Clamp(수입조회개월수, 1, 12),
            가정환율KrwPerUsd,
            추가수입비용KrwPerKg,
            해외공공가격품목명.Trim().ToUpperInvariant(),
            해외공공가격프로그램.Trim().ToUpperInvariant(),
            해외가격시작연도,
            해외가격종료연도);

    private void 입력속성변경알림()
    {
        OnPropertyChanged(nameof(적용HS코드));
        OnPropertyChanged(nameof(제안단가KrwPerKg));
        OnPropertyChanged(nameof(조회가능));
        OnPropertyChanged(nameof(가격정보최신));
        OnPropertyChanged(nameof(의사결정근거충분));
    }

    private void 결과속성변경알림()
    {
        OnPropertyChanged(nameof(가격정보최신));
        OnPropertyChanged(nameof(의사결정근거충분));
    }

    partial void On조회HS코드Changed(string value) => 입력속성변경알림();
    partial void On제안가격KrwChanged(decimal value) => 입력속성변경알림();
    partial void On가격기준중량KgChanged(decimal value) => 입력속성변경알림();
    partial void On수출국가코드Changed(string value) => 입력속성변경알림();
    partial void On국내조회기간일수Changed(int value) => 입력속성변경알림();
    partial void On수입조회개월수Changed(int value) => 입력속성변경알림();
    partial void On가정환율KrwPerUsdChanged(decimal? value) => 입력속성변경알림();
    partial void On추가수입비용KrwPerKgChanged(decimal? value) => 입력속성변경알림();
    partial void On해외공공가격품목명Changed(string value) => 입력속성변경알림();
    partial void On해외공공가격프로그램Changed(string value) => 입력속성변경알림();
    partial void On해외가격시작연도Changed(int value) => 입력속성변경알림();
    partial void On해외가격종료연도Changed(int value) => 입력속성변경알림();

    private static string 숫자만(string? value)
        => new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string 국가코드정규화(string? value)
        => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
