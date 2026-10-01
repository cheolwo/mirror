using System.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 공동수입물류경로옵션(
    string 코드,
    string 이름,
    string 설명);

/// <summary>
/// 공동수입 원장과 그 아래 국제운송·국내운송·입고·출고 원장을
/// 사용자가 선택한 물류 경로에 맞춰 계획하고 생성합니다.
/// </summary>
public sealed class 공동수입원장물류ViewModel : 공동구매작업ViewModelBase, IDisposable
{
    private readonly I공동수입원장전환Client _client;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private Guid? _대상공동구매Id;
    private CommunityGroupImportLedgerConversionRequest _초안 = new();
    private CommunityGroupImportLedgerPlanResponse? _계획;
    private CommunityGroupImportLedgerPlanResponse? _저장된원장;

    public 공동수입원장물류ViewModel(
        I공동수입원장전환Client client,
        공동구매화면상태ViewModel 화면상태,
        공동구매거래경로분기ViewModel 분기)
    {
        _client = client;
        _화면상태 = 화면상태;
        _분기 = 분기;
        현재사용자Context연결(화면상태.현재사용자Context);
        _화면상태.PropertyChanged += 화면상태변경;
        _분기.PropertyChanged += 분기변경;
        공동구매변경동기화();
    }

    public static IReadOnlyList<공동수입물류경로옵션> 경로옵션 { get; } =
    [
        new(
            CommunityGroupImportLogisticsRouteCodes.ThreePlWarehouse,
            "3PL 입출고·배송",
            "통관 후 3PL에 입고하고 피킹·출고한 뒤 최종 도착지까지 배송합니다."),
        new(
            CommunityGroupImportLogisticsRouteCodes.DirectDestination,
            "최종 도착지 직배송",
            "보세구역 또는 항만에서 별도 창고 입고 없이 최종 도착지로 바로 운송합니다."),
        new(
            CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse,
            "전용 창고 입고·보관",
            "지정한 전용 창고에 우선 입고하며, 필요할 때 출고·최종 배송 단계를 추가합니다.")
    ];

    public CommunityGroupImportLedgerConversionRequest 초안
    {
        get => _초안;
        private set
        {
            if (SetProperty(ref _초안, value))
            {
                OnPropertyChanged(nameof(선택한경로안내));
            }
        }
    }

    public CommunityGroupImportLedgerPlanResponse? 계획
    {
        get => _계획;
        private set
        {
            if (SetProperty(ref _계획, value))
            {
                OnPropertyChanged(nameof(전환가능));
            }
        }
    }

    public CommunityGroupImportLedgerPlanResponse? 저장된원장
    {
        get => _저장된원장;
        private set => SetProperty(ref _저장된원장, value);
    }

    public bool 전환가능 => _분기.공동수입활성 && 계획?.Ready == true;

    public string 선택한경로안내
        => 경로옵션.FirstOrDefault(option =>
               string.Equals(option.코드, 초안.LogisticsRouteCode, StringComparison.OrdinalIgnoreCase))?.설명
           ?? "공동수입 물류 경로를 선택해 주세요.";

    public bool 물류경로선택(string routeCode)
    {
        if (!_분기.공동수입활성)
        {
            return 유효성실패("공동수입 분기가 활성화된 경우에만 수입 물류 경로를 선택할 수 있습니다.");
        }

        if (!CommunityGroupImportLogisticsRouteCodes.All.Contains(routeCode))
        {
            return 유효성실패("지원되는 공동수입 물류 경로를 선택해 주세요.");
        }

        초안.LogisticsRouteCode = routeCode;
        저장된원장 = null;
        창고조건초기화();
        switch (routeCode)
        {
            case CommunityGroupImportLogisticsRouteCodes.ThreePlWarehouse:
                초안.WarehouseDisplayName = "선택한 3PL 물류센터";
                초안.RequiresWarehouseOutbound = true;
                초안.RequiresFinalDestinationDelivery = true;
                break;
            case CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse:
                초안.WarehouseDisplayName = "선택한 전용 창고";
                초안.RequiresWarehouseOutbound = false;
                초안.RequiresFinalDestinationDelivery = false;
                break;
            default:
                초안.RequiresWarehouseOutbound = false;
                초안.RequiresFinalDestinationDelivery = false;
                break;
        }

        입력변경알림();
        return true;
    }

    public void 후속출고선택(bool enabled, bool deliverToFinalDestination)
    {
        초안.RequiresWarehouseOutbound = enabled;
        초안.RequiresFinalDestinationDelivery = enabled && deliverToFinalDestination;
        입력변경알림();
    }

    public CommunityGroupImportLedgerPlanResponse 로컬미리보기()
    {
        계획 = CommunityGroupImportLedgerPlanBuilder.Preview(초안);
        return 계획;
    }

    public async Task<bool> 서버미리보기Async(CancellationToken cancellationToken = default)
    {
        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("공동수입 물류 경로를 계획할 공동구매를 선택해 주세요.");
        }
        if (!_분기.공동수입활성)
        {
            return 유효성실패("해외 출발·국내 반입 공동수입에서만 이 물류 계획을 사용할 수 있습니다.");
        }

        초안.GroupPurchaseCampaignId = campaignId.Value;
        return await 작업실행Async(
            async token =>
            {
                계획 = await _client.미리보기Async(campaignId.Value, 초안, token)
                    ?? throw new InvalidOperationException("공동수입 물류 경로 미리보기 응답이 비어 있습니다.");
            },
            "선택한 공동수입 물류 경로를 확인했습니다.",
            cancellationToken);
    }

    public async Task<bool> 기존원장조회Async(CancellationToken cancellationToken = default)
    {
        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("공동수입 원장을 조회할 공동구매를 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                저장된원장 = await _client.조회Async(campaignId.Value, token);
                if (저장된원장 is not null)
                {
                    계획 = 저장된원장;
                }
            },
            "공동수입 원장 상태를 확인했습니다.",
            cancellationToken);
    }

    public async Task<bool> 공동수입원장전환Async(CancellationToken cancellationToken = default)
    {
        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("공동수입 원장으로 전환할 공동구매를 선택해 주세요.");
        }
        if (!_분기.공동수입활성)
        {
            return 유효성실패("해외 출발·국내 반입으로 판정된 공동구매만 공동수입 원장으로 전환할 수 있습니다.");
        }

        초안.GroupPurchaseCampaignId = campaignId.Value;
        초안.ExpectedRevision = 저장된원장?.Revision;
        var preview = 로컬미리보기();
        if (!preview.Ready)
        {
            return 유효성실패(string.Join(" ", preview.Warnings));
        }

        return await 작업실행Async(
            async token =>
            {
                저장된원장 = await _client.전환Async(campaignId.Value, 초안, token)
                    ?? throw new InvalidOperationException("공동수입 원장 전환 응답이 비어 있습니다.");
                계획 = 저장된원장;
                await _화면상태.단계도달Async(
                    공동구매절차코드.실행,
                    $"공동수입 원장 {저장된원장.GroupImportLedgerId}으로 인계했습니다.",
                    token);
            },
            "공동수입 원장과 선택한 하위 물류 원장을 생성했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
    {
        저장된원장 = null;
        OnPropertyChanged(nameof(초안));
        OnPropertyChanged(nameof(선택한경로안내));
        로컬미리보기();
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        _분기.PropertyChanged -= 분기변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
        => 공동구매변경동기화();

    private void 분기변경(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(전환가능));
        OnPropertyChanged(nameof(선택한경로안내));
    }

    private void 공동구매변경동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        if (_대상공동구매Id == campaign?.Id)
        {
            return;
        }

        _대상공동구매Id = campaign?.Id;
        var settings = campaign?.GroupPurchase;
        var quantity = settings?.TotalRequestedQuantity
                       ?? campaign?.Options.Sum(option => option.RequestedQuantity)
                       ?? 0;
        var quantityUnit = settings?.QuantityUnit
                           ?? campaign?.Options.FirstOrDefault(option =>
                               !string.IsNullOrWhiteSpace(option.QuantityUnit))?.QuantityUnit
                           ?? string.Empty;
        초안 = new CommunityGroupImportLedgerConversionRequest
        {
            GroupPurchaseCampaignId = campaign?.Id ?? Guid.Empty,
            LogisticsRouteCode = CommunityGroupImportLogisticsRouteCodes.DirectDestination,
            ProductSummary = campaign is null
                ? string.Empty
                : string.Join(", ", campaign.Options.Select(option => option.Text)),
            PlannedQuantity = quantity,
            QuantityUnit = quantityUnit,
            InternationalTransportMode = "LCL",
            FinalDestinationLabel = settings?.ServiceAreaLabel
                                    ?? settings?.DeliveryCountryCode
                                    ?? string.Empty
        };
        저장된원장 = null;
        계획 = campaign is null ? null : CommunityGroupImportLedgerPlanBuilder.Preview(초안);
    }

    private void 창고조건초기화()
    {
        초안.WarehouseReferenceKey = string.Empty;
        초안.WarehouseDisplayName = string.Empty;
        초안.WarehouseOperatorConsentConfirmed = false;
        초안.WarehouseSiteVerified = false;
        초안.WarehouseBulkReceivingSupported = false;
        초안.WarehouseStorageSupported = false;
        초안.WarehouseOutboundSupported = false;
        초안.RequiresWarehouseOutbound = false;
        초안.RequiresFinalDestinationDelivery = false;
    }
}
