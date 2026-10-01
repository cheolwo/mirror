using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed partial class 공동구매이행계획ViewModel : 공동구매물류업무ViewModelBase, IDisposable
{
    private readonly I공동구매물류Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private Guid? _대상공동구매Id;

    public 공동구매이행계획ViewModel(
        I공동구매물류Service service,
        공동구매화면상태ViewModel 화면상태,
        공동구매거래경로분기ViewModel 분기)
        : base(화면상태)
    {
        _service = service;
        _화면상태 = 화면상태;
        _분기 = 분기;
        _화면상태.PropertyChanged += 화면상태변경;
        공동구매변경동기화();
    }

    [ObservableProperty]
    public partial DomesticGroupPurchaseFulfillmentPlanRequest 초안 { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(발주초안생성가능))]
    public partial DomesticGroupPurchaseFulfillmentPlanResponse? 계획 { get; private set; }

    [ObservableProperty]
    public partial DomesticGroupPurchaseFulfillmentOrderDraftResponse? 저장된발주초안 { get; private set; }

    public bool 발주초안생성가능 => 계획?.OrderPlacementReady == true;

    public bool 경로선택(string routeCode)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 이행 경로는 국내 공동구매 분기에서만 선택할 수 있습니다.");
        }

        if (!DomesticGroupPurchaseFulfillmentRouteCodes.All.Contains(routeCode))
        {
            return 유효성실패("지원되는 공동구매 이행 경로를 선택해 주세요.");
        }

        초안.RouteCode = routeCode;
        저장된발주초안 = null;
        거점능력초기화();

        switch (routeCode)
        {
            case DomesticGroupPurchaseFulfillmentRouteCodes.TraditionalMarketHub:
                초안.HubDisplayName = "지역 전통시장 공동물류 거점";
                초안.RequiresSorting = true;
                초안.RequiresStorage = false;
                초안.RequiresLastMileDelivery = true;
                break;
            case DomesticGroupPurchaseFulfillmentRouteCodes.ThirdPartyLogistics:
                초안.HubDisplayName = "지역 3PL 업체";
                초안.RequiresSorting = true;
                초안.RequiresStorage = true;
                초안.RequiresLastMileDelivery = true;
                break;
            case DomesticGroupPurchaseFulfillmentRouteCodes.DedicatedWarehouse:
                초안.HubDisplayName = "공동구매 전용 창고";
                초안.RequiresSorting = false;
                초안.RequiresStorage = true;
                초안.RequiresLastMileDelivery = false;
                break;
            default:
                초안.HubDisplayName = string.Empty;
                초안.HubReferenceKey = string.Empty;
                초안.RequiresSorting = false;
                초안.RequiresStorage = false;
                초안.RequiresLastMileDelivery = false;
                break;
        }

        로컬미리보기();
        OnPropertyChanged(nameof(초안));
        return true;
    }

    /// <summary>
    /// 입력 중 즉시 보여 줄 다이어그램을 계약 프로젝트의 순수 계획기로 계산합니다.
    /// 실제 발주 저장 전에는 서버 미리보기로 동일한 조건을 다시 확인합니다.
    /// </summary>
    public DomesticGroupPurchaseFulfillmentPlanResponse 로컬미리보기()
    {
        계획 = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(초안);
        저장된발주초안 = null;
        return 계획;
    }

    public async Task<bool> 서버미리보기Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 이행계획은 국내 공동구매 분기에서만 확인할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("이행 경로를 계획할 공동구매를 선택해 주세요.");
        }

        초안.GroupPurchaseCampaignId = campaignId.Value;
        return await 작업실행Async(
            async token =>
            {
                계획 = await _service.이행계획미리보기Async(
                    campaignId.Value,
                    초안,
                    token)
                    ?? throw new InvalidOperationException("공동구매 이행계획 미리보기 응답이 비어 있습니다.");
                저장된발주초안 = null;
            },
            "공동구매 이행계획을 서버 조건으로 확인했습니다.",
            cancellationToken);
    }

    public async Task<bool> 발주초안저장Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 발주 초안은 국내 공동구매 분기에서만 만들 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("발주 초안을 만들 공동구매를 선택해 주세요.");
        }

        초안.GroupPurchaseCampaignId = campaignId.Value;
        var currentPlan = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(초안);
        계획 = currentPlan;
        if (!currentPlan.OrderPlacementReady)
        {
            return 유효성실패(string.Join(" ", currentPlan.PlanningWarnings));
        }

        return await 작업실행Async(
            async token =>
            {
                저장된발주초안 = await _service.발주초안생성Async(
                    campaignId.Value,
                    초안,
                    token)
                    ?? throw new InvalidOperationException("발주·원장 생성 초안 응답이 비어 있습니다.");
                계획 = 저장된발주초안.Plan;
                await _화면상태.단계도달Async(
                    공동구매절차코드.실행,
                    "발주 주문 원장과 후속 물류 원장 생성 초안을 저장했습니다.",
                    token);
            },
            "발주 주문 원장과 후속 물류 원장 생성 초안을 저장했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
    {
        OnPropertyChanged(nameof(초안));
        로컬미리보기();
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
        => 공동구매변경동기화();

    private void 공동구매변경동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        if (_대상공동구매Id == campaign?.Id)
        {
            return;
        }

        _대상공동구매Id = campaign?.Id;
        var quantity = campaign?.GroupPurchase?.TotalRequestedQuantity ?? 0;
        var unit = campaign?.GroupPurchase?.QuantityUnit ?? string.Empty;
        초안 = new DomesticGroupPurchaseFulfillmentPlanRequest
        {
            GroupPurchaseCampaignId = campaign?.Id ?? Guid.Empty,
            CampaignTitle = campaign?.Title ?? string.Empty,
            RouteCode = DomesticGroupPurchaseFulfillmentRouteCodes.DirectCollectionPoint,
            ProducerDisplayName = "회원 생산자",
            ProductSummary = campaign is null
                ? string.Empty
                : string.Join(", ", campaign.Options.Select(option => option.Text)),
            QuantitySummary = quantity <= 0 ? string.Empty : $"{quantity}{unit}",
            PlannedQuantity = quantity,
            QuantityUnit = unit,
            DestinationLabel = campaign?.GroupPurchase?.ServiceAreaLabel ?? string.Empty,
            RequiresLastMileDelivery = false,
            HubCapabilities = new DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot
            {
                CapacityUnit = unit
            }
        };
        저장된발주초안 = null;
        계획 = campaign is null ? null : DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(초안);
    }

    private void 거점능력초기화()
    {
        초안.HubReferenceKey = string.Empty;
        초안.HubCapabilities = new DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot
        {
            CapacityUnit = 초안.QuantityUnit
        };
    }
}

public sealed class 공동구매물류기능ViewModel : 조립ViewModelBase
{
    public 공동구매물류기능ViewModel(
        공동구매이행계획ViewModel 이행계획,
        공동구매이행계획미리보기ViewModel 이행계획미리보기,
        공동구매발주초안등록ViewModel 발주초안등록)
    {
        this.이행계획 = 하위ViewModel등록(이행계획, 수명소유: false);
        세부업무목록 =
        [
            하위ViewModel등록(이행계획미리보기, 수명소유: false),
            하위ViewModel등록(발주초안등록, 수명소유: false)
        ];
    }

    public 공동구매이행계획ViewModel 이행계획 { get; }
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }
    public bool 처리중 => 이행계획.처리중;
}
