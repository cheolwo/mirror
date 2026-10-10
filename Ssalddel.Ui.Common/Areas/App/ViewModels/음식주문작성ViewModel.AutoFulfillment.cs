using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed partial class 음식주문작성ViewModel
{
    private AutoFulfillmentConditionsViewModel? _autoConditions;
    private long _autoQuoteGeneration;
    private long? _autoQuotedRestaurant;
    private long? _autoAttemptedRestaurant;
    public AutoFulfillmentConditionsViewModel AutoConditions => _autoConditions ??= CreateAutoConditions();
    public bool 자동배송사용 { get; private set; }
    public bool 자동배송확인중 { get; private set; }
    public string? 자동배송오류 { get; private set; }
    public bool 자동배송확인됨 => _autoQuotedRestaurant == 음식점Id;
    public bool 자동배송제출가능 => !자동배송확인중 && 자동배송오류 is null && (!자동배송사용 || AutoConditions.IsComplete);
    private AutoFulfillmentConditionsViewModel CreateAutoConditions()
    {
        var model = new AutoFulfillmentConditionsViewModel();
        model.PropertyChanged += (_, _) => { 요청내용변경됨(preserveAutomaticAuthorization: true); NotifyOrderChanged(); };
        return model;
    }
    public async Task 자동배송조건확인Async(bool force = false, CancellationToken ct = default)
    {
        if (음식점Id is not { } restaurant || 자동배송확인중 || 입력잠금 || (!force && _autoAttemptedRestaurant == restaurant)) return;
        _autoAttemptedRestaurant = restaurant;
        var generation = ++_autoQuoteGeneration;
        자동배송확인중 = true; 자동배송오류 = null; OnPropertyChanged(string.Empty);
        try
        {
            var quote = await service.자동배송조건Async(현재주문요청(), ct);
            if (generation != _autoQuoteGeneration || restaurant != 음식점Id) return;
            if (quote.Enabled && quote.RestaurantId != restaurant) throw new InvalidOperationException("음식점 확인 불일치");
            자동배송사용 = quote.Enabled;
            _autoQuotedRestaurant = restaurant;
            if (quote.Enabled)
                AutoConditions.Configure(quote.Providers.Select(p => new AutoFulfillmentProviderChoice(p.ProviderId, p.ProviderName, p.IsSimulation)).ToArray(),
                    AutoFulfillmentNotice.Version, quote.RestaurantSupportKrw, quote.RestaurantSupportRevision, requiredDirectPayment: false, costSharingPolicy: quote.CostSharingPolicy);
            else _autoConditions?.Clear();
        }
        catch (Exception) when (generation == _autoQuoteGeneration)
        { 자동배송오류 = "음식점의 배송 지원 조건을 확인하지 못했습니다. 다시 확인해 주세요."; }
        finally
        { if (generation == _autoQuoteGeneration) { 자동배송확인중 = false; OnPropertyChanged(string.Empty); NotifyOrderChanged(); } }
    }
    private void 자동배송초기화()
    {
        ++_autoQuoteGeneration; _autoQuotedRestaurant = null; _autoAttemptedRestaurant = null; 자동배송사용 = false; 자동배송확인중 = false; 자동배송오류 = null;
        _autoConditions?.Clear(); OnPropertyChanged(string.Empty);
    }
    private 음식주문등록요청 현재주문요청() => new()
    {
        클라이언트요청Id = 클라이언트요청Id, 음식점Id = 음식점Id ?? 0, 주문자UserId = string.Empty,
        수령인정보 = new() { 수령인명 = 수령인명.Trim(), 연락처 = 연락처.Trim(), 주소 = 주소.Trim(), 상세주소 = 상세주소.Trim(),
            요청사항 = 요청사항.Trim(), 주문자본인수령여부 = 주문자본인수령여부 },
        상품목록 = 선택항목목록.Select(item => new 음식주문상품Dto { 메뉴Id = item.메뉴Id, 상품명 = item.메뉴명, 수량 = item.수량, 단가 = item.단가 }).ToArray(),
        메뉴가격확인필요 = true, 결제수단 = 결제수단,
        CommerceProtection = 거래보호확인 is null ? null : new 거래보호확인Request
            { NoticeVersion = 거래보호확인.NoticeVersion, NoticeAccepted = 거래보호확인.NoticeAccepted, SellerRevision = 거래보호확인.SellerRevision },
        AutoFulfillment = 자동배송사용 ? AutoConditions.BuildConsent() : null,
        DestinationIdentity = 선택주소식별복사()
    };
}
