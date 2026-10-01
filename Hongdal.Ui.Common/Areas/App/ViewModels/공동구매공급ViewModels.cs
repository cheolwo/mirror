using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed partial class 공동구매생산자연결ViewModel : 공동구매공급업무ViewModelBase, IDisposable
{
    private readonly I공동구매공급Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private Guid? _대상공동구매Id;

    public 공동구매생산자연결ViewModel(
        I공동구매공급Service service,
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
    public partial string 검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 지역코드 { get; set; } = "all";

    [ObservableProperty]
    public partial string 상품검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<DomesticProducerCandidateResponse> 생산자후보 { get; private set; } = [];

    [ObservableProperty]
    public partial string 연동안내 { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial DomesticProducerCandidateResponse? 선택된생산자 { get; private set; }

    [ObservableProperty]
    public partial DomesticProducerContactRequestDraftRequest 연락요청초안 { get; private set; } = new();

    [ObservableProperty]
    public partial DomesticProducerContactRequestDraftResponse? 저장된연락요청 { get; private set; }

    public async Task<bool> 후보조회Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("생산자 후보 조회는 국내 공동구매 분기에서만 사용할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("생산자를 찾을 공동구매를 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var response = await _service.생산자후보조회Async(
                    campaignId.Value,
                    검색어,
                    지역코드 == "all" ? null : 지역코드,
                    string.IsNullOrWhiteSpace(상품검색어) ? 검색어 : 상품검색어,
                    token);
                생산자후보 = response.Items;
                연동안내 = response.IntegrationMessage;

                if (선택된생산자 is not null
                    && 생산자후보.All(candidate => candidate.CandidateKey != 선택된생산자.CandidateKey))
                {
                    선택해제();
                }
            },
            "회원 생산자 후보를 불러왔습니다.",
            cancellationToken,
            ex => $"생산자 후보를 불러오지 못했습니다. {ex.Message}");
    }

    public bool 생산자선택(string candidateKey)
    {
        var candidate = 생산자후보.FirstOrDefault(item =>
            string.Equals(item.CandidateKey, candidateKey, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
        {
            return 유효성실패("연락을 요청할 생산자를 선택해 주세요.");
        }

        var campaign = _화면상태.선택된공동구매;
        선택된생산자 = candidate;
        저장된연락요청 = null;
        연락요청초안 = new DomesticProducerContactRequestDraftRequest
        {
            GroupPurchaseCampaignId = campaign?.Id ?? Guid.Empty,
            CampaignTitle = campaign?.Title ?? string.Empty,
            ProducerCandidateKey = candidate.CandidateKey,
            ProducerMaskedDisplayName = candidate.MaskedDisplayName,
            ProductSummary = string.Join(", ", candidate.ProductTags),
            RequestedQuantitySummary = "희망 수량과 구매 시기를 입력하세요.",
            RequiredPackagingFormCode = DomesticProducePackagingFormCodes.CorrugatedBox,
            PackagingUnitSummary = "10kg 규격 골판지 상자",
            QualityGradeSummary = "혼합 크기 허용, 파손·부패 제외",
            RequestedQuantity = 500,
            MaximumAbsorptionQuantity = 800,
            QuantityUnit = "kg",
            CanReceiveSplitShipments = true,
            Message = $"안녕하세요. '{campaign?.Title}' 국내 공동구매의 공급 가능 품목과 수량을 협의하고 싶습니다."
        };
        return true;
    }

    public async Task<bool> 연락요청저장Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("생산자 연락 요청은 국내 공동구매 분기에서만 저장할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null || 선택된생산자 is null)
        {
            return 유효성실패("연락 요청을 저장할 공동구매와 생산자를 선택해 주세요.");
        }

        if (!선택된생산자.ContactRequestConsentConfirmed
            || !선택된생산자.ThirdPartySharingConsentConfirmed)
        {
            return 유효성실패("연락 요청 수신과 제3자 정보 공유에 동의한 생산자만 연결할 수 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(연락요청초안.ProductSummary)
            || string.IsNullOrWhiteSpace(연락요청초안.RequestedQuantitySummary)
            || 연락요청초안.RequestedQuantity <= 0
            || 연락요청초안.MaximumAbsorptionQuantity < 연락요청초안.RequestedQuantity
            || string.IsNullOrWhiteSpace(연락요청초안.QuantityUnit)
            || string.IsNullOrWhiteSpace(연락요청초안.Message))
        {
            return 유효성실패("요청 품목, 수량·단위, 최대 인수량과 연락 메시지를 확인해 주세요.");
        }

        연락요청초안.GroupPurchaseCampaignId = campaignId.Value;
        연락요청초안.CampaignTitle = _화면상태.선택된공동구매?.Title ?? string.Empty;
        연락요청초안.ProducerCandidateKey = 선택된생산자.CandidateKey;
        연락요청초안.ProducerMaskedDisplayName = 선택된생산자.MaskedDisplayName;

        return await 작업실행Async(
            async token =>
            {
                저장된연락요청 = await _service.연락요청초안생성Async(
                    campaignId.Value,
                    연락요청초안,
                    token)
                    ?? throw new InvalidOperationException("생산자 연락 요청 초안 응답이 비어 있습니다.");
                await _화면상태.단계도달Async(
                    공동구매절차코드.공급조건협상,
                    "생산자 연락 요청을 저장하고 공급 조건 협상 단계로 진행했습니다.",
                    token);
            },
            "생산자 연락 요청 초안을 저장했습니다.",
            cancellationToken);
    }

    public void 선택해제()
    {
        선택된생산자 = null;
        저장된연락요청 = null;
        연락요청초안 = new();
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
        var campaignId = _화면상태.선택된공동구매Id;
        if (_대상공동구매Id == campaignId)
        {
            return;
        }

        _대상공동구매Id = campaignId;
        생산자후보 = [];
        연동안내 = string.Empty;
        선택해제();
    }
}

public sealed partial class 공동구매공급제안ViewModel : 공동구매공급업무ViewModelBase, IDisposable
{
    private readonly I공동구매공급Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private Guid? _대상공동구매Id;

    public 공동구매공급제안ViewModel(
        I공동구매공급Service service,
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
    public partial string 대표검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 운영지역코드 { get; set; } = "all";

    [ObservableProperty]
    public partial string 상품검색어 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<DomesticGroupPurchaseRepresentativeCandidateResponse> 공동구매대표후보 { get; private set; } = [];

    [ObservableProperty]
    public partial string 연동안내 { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial DomesticGroupPurchaseRepresentativeCandidateResponse? 선택된대표 { get; private set; }

    [ObservableProperty]
    public partial DomesticProducerSupplyOfferDraftRequest 공급제안초안 { get; private set; } = new();

    [ObservableProperty]
    public partial DomesticProducerSupplyOfferDraftResponse? 저장된공급제안 { get; private set; }

    public async Task<bool> 대표조회Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("공동구매 대표 후보 조회는 국내 공동구매 분기에서만 사용할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("공급을 제안할 공동구매를 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var response = await _service.대표후보조회Async(
                    campaignId.Value,
                    대표검색어,
                    운영지역코드 == "all" ? null : 운영지역코드,
                    string.IsNullOrWhiteSpace(상품검색어) ? 대표검색어 : 상품검색어,
                    token);
                공동구매대표후보 = response.Items;
                연동안내 = response.IntegrationMessage;

                if (선택된대표 is not null
                    && 공동구매대표후보.All(candidate => candidate.CandidateKey != 선택된대표.CandidateKey))
                {
                    선택해제();
                }
            },
            "공동구매 대표 후보를 불러왔습니다.",
            cancellationToken,
            ex => $"공동구매 대표 후보를 불러오지 못했습니다. {ex.Message}");
    }

    public bool 대표선택(string candidateKey)
    {
        var candidate = 공동구매대표후보.FirstOrDefault(item =>
            string.Equals(item.CandidateKey, candidateKey, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
        {
            return 유효성실패("공급을 제안할 공동구매 대표를 선택해 주세요.");
        }

        var campaign = _화면상태.선택된공동구매;
        선택된대표 = candidate;
        저장된공급제안 = null;
        공급제안초안 = new DomesticProducerSupplyOfferDraftRequest
        {
            GroupPurchaseCampaignId = campaign?.Id ?? Guid.Empty,
            CampaignTitle = campaign?.Title ?? string.Empty,
            RepresentativeCandidateKey = candidate.CandidateKey,
            RepresentativeMaskedDisplayName = candidate.MaskedDisplayName,
            ProducerMaskedDisplayName = "회원 생산자",
            ProductSummary = candidate.InterestedProductTags.FirstOrDefault() ?? string.Empty,
            AvailableQuantitySummary = "공급 가능한 수량과 포장 단위를 입력하세요.",
            SupportedPackagingFormCodes = [DomesticProducePackagingFormCodes.CorrugatedBox],
            AvailableQuantity = 1000,
            MinimumTakeQuantity = 300,
            QuantityUnit = "kg",
            CanSplitShipments = true,
            ExpectedPriceSummary = "희망 단가 또는 총액과 협의 가능 여부를 입력하세요.",
            SupplyDeadlineSummary = "출하 가능한 마지막 시기를 입력하세요.",
            OfferReasonCode = DomesticProducerSupplyOfferReasonCodes.Overproduction,
            QualityDisclosure = "크기, 외관, 선별 상태와 규격 외 사유를 구체적으로 입력하세요.",
            Message = $"안녕하세요. '{campaign?.Title}' 공동구매에 공급을 제안하고 싶습니다."
        };
        return true;
    }

    public async Task<bool> 공급제안저장Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 생산자 공급 제안은 국내 공동구매 분기에서만 저장할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null || 선택된대표 is null)
        {
            return 유효성실패("공급을 제안할 공동구매와 대표를 선택해 주세요.");
        }

        if (!선택된대표.RepresentativeRoleConfirmed
            || !선택된대표.ContactRequestConsentConfirmed)
        {
            return 유효성실패("대표 역할이 확인되고 연락 요청 수신에 동의한 공동구매 대표만 선택할 수 있습니다.");
        }

        if (!공급제안초안.FoodSafetyConfirmed)
        {
            return 유효성실패("식품 안전과 품질 공개 내용을 확인해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(공급제안초안.ProducerMaskedDisplayName)
            || string.IsNullOrWhiteSpace(공급제안초안.ProductSummary)
            || 공급제안초안.AvailableQuantity <= 0
            || 공급제안초안.MinimumTakeQuantity <= 0
            || 공급제안초안.MinimumTakeQuantity > 공급제안초안.AvailableQuantity
            || string.IsNullOrWhiteSpace(공급제안초안.QuantityUnit)
            || string.IsNullOrWhiteSpace(공급제안초안.QualityDisclosure))
        {
            return 유효성실패("생산자, 품목, 공급·최소 인수 수량, 단위와 품질 공개 내용을 확인해 주세요.");
        }

        공급제안초안.GroupPurchaseCampaignId = campaignId.Value;
        공급제안초안.CampaignTitle = _화면상태.선택된공동구매?.Title ?? string.Empty;
        공급제안초안.RepresentativeCandidateKey = 선택된대표.CandidateKey;
        공급제안초안.RepresentativeMaskedDisplayName = 선택된대표.MaskedDisplayName;

        return await 작업실행Async(
            async token =>
            {
                저장된공급제안 = await _service.공급제안초안생성Async(
                    campaignId.Value,
                    공급제안초안,
                    token)
                    ?? throw new InvalidOperationException("생산자 공급 제안 초안 응답이 비어 있습니다.");
                await _화면상태.단계도달Async(
                    공동구매절차코드.공급조건협상,
                    "생산자 공급 제안을 저장하고 공급 조건 협상 단계로 진행했습니다.",
                    token);
            },
            "생산자 공급 제안 초안을 저장했습니다.",
            cancellationToken);
    }

    public void 선택해제()
    {
        선택된대표 = null;
        저장된공급제안 = null;
        공급제안초안 = new();
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
        var campaignId = _화면상태.선택된공동구매Id;
        if (_대상공동구매Id == campaignId)
        {
            return;
        }

        _대상공동구매Id = campaignId;
        공동구매대표후보 = [];
        연동안내 = string.Empty;
        선택해제();
    }
}

public sealed partial class 공동구매공급적합성ViewModel(
    I공동구매공급Service service,
    공동구매화면상태ViewModel 화면상태,
    공동구매거래경로분기ViewModel 분기) : 공동구매공급업무ViewModelBase(화면상태)
{
    [ObservableProperty]
    public partial DomesticGroupPurchaseSupplyCompatibilityPreviewRequest 조건 { get; set; } = new()
    {
        BuyerRequiredPackagingFormCode = DomesticProducePackagingFormCodes.CorrugatedBox,
        BuyerRequestedQuantity = 500,
        BuyerMaximumAbsorptionQuantity = 800,
        BuyerCanReceiveSplitShipments = true,
        ProducerSupportedPackagingFormCodes = [DomesticProducePackagingFormCodes.CorrugatedBox],
        ProducerAvailableQuantity = 1000,
        ProducerMinimumTakeQuantity = 300,
        ProducerCanSplitShipments = true,
        QuantityUnit = "kg"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(상호공급가능))]
    public partial DomesticGroupPurchaseSupplyCompatibilityPreviewResponse? 판정결과 { get; private set; }

    public bool 상호공급가능 => 판정결과?.IsMutuallyFeasible == true;

    public async Task<bool> 미리보기Async(CancellationToken cancellationToken = default)
    {
        if (!분기.국내공동구매활성)
        {
            return 유효성실패("국내 공급 적합성 판정은 국내 공동구매 분기에서만 사용할 수 있습니다.");
        }

        var campaignId = 화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("공급 조건을 판정할 공동구매를 선택해 주세요.");
        }

        if (조건.BuyerRequestedQuantity <= 0
            || 조건.BuyerMaximumAbsorptionQuantity <= 0
            || 조건.ProducerAvailableQuantity <= 0
            || 조건.ProducerMinimumTakeQuantity <= 0
            || string.IsNullOrWhiteSpace(조건.QuantityUnit))
        {
            return 유효성실패("구매·공급 수량과 수량 단위를 올바르게 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                판정결과 = await service.공급적합성미리보기Async(
                    campaignId.Value,
                    조건,
                    token)
                    ?? throw new InvalidOperationException("공급 조건 적합성 판정 응답이 비어 있습니다.");
            },
            "구매자와 생산자의 공급 조건을 판정했습니다.",
            cancellationToken);
    }
}

public sealed partial class 공동구매협상ViewModel : 공동구매공급업무ViewModelBase, IDisposable
{
    private readonly I공동구매공급Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private Guid? _대상공동구매Id;

    public 공동구매협상ViewModel(
        I공동구매공급Service service,
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
    [NotifyPropertyChangedFor(nameof(미해결쟁점수))]
    public partial DomesticGroupPurchaseNegotiationTimelineResponse 협상이력 { get; private set; } = new();

    [ObservableProperty]
    public partial Guid? 선택된쟁점Id { get; private set; }

    [ObservableProperty]
    public partial DomesticGroupPurchaseNegotiationEventRequest 이벤트초안 { get; private set; } = 새이벤트초안();

    [ObservableProperty]
    public partial DomesticGroupPurchaseNegotiationIssueRequest 쟁점초안 { get; private set; } = 새쟁점초안();

    [ObservableProperty]
    public partial DomesticGroupPurchaseDeliberationPositionRequest 숙고의견초안 { get; private set; } = 새숙고의견초안();

    [ObservableProperty]
    public partial DomesticGroupPurchaseNegotiationResolutionRequest 합의초안 { get; private set; } = new();

    public int 미해결쟁점수 => 협상이력.Issues.Count(issue =>
        issue.StatusCode == DomesticGroupPurchaseNegotiationIssueStatusCodes.Deliberating);

    public async Task<bool> 이력조회Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 공급 협상은 국내 공동구매 분기에서만 조회할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null)
        {
            return 유효성실패("협상 이력을 조회할 공동구매를 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                협상이력 = await _service.협상이력조회Async(campaignId.Value, token);
                선택된쟁점Id = 협상이력.Issues.FirstOrDefault(issue =>
                    issue.StatusCode == DomesticGroupPurchaseNegotiationIssueStatusCodes.Deliberating)?.IssueId;
            },
            "공동구매 협상 이력을 불러왔습니다.",
            cancellationToken,
            ex => $"공개 협상 이력을 불러오지 못했습니다. {ex.Message}");
    }

    public async Task<bool> 이벤트등록Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 공급 협상 기록은 국내 공동구매 분기에서만 등록할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null
            || string.IsNullOrWhiteSpace(이벤트초안.MaskedActorDisplayName)
            || string.IsNullOrWhiteSpace(이벤트초안.ActorRoleLabel)
            || string.IsNullOrWhiteSpace(이벤트초안.PublicSummary))
        {
            return 유효성실패("협상 당사자 표시명, 역할과 공개 협의 내용을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await _service.협상이벤트등록Async(
                    campaignId.Value,
                    이벤트초안,
                    token)
                    ?? throw new InvalidOperationException("협상 이벤트 등록 응답이 비어 있습니다.");
                협상이력.Events.Add(created);
                OnPropertyChanged(nameof(협상이력));
                이벤트초안 = 새이벤트초안();
            },
            "협의 기록을 커뮤니티에 공개했습니다.",
            cancellationToken);
    }

    public async Task<bool> 쟁점등록Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 공급 협상 쟁점은 국내 공동구매 분기에서만 등록할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null
            || string.IsNullOrWhiteSpace(쟁점초안.Title)
            || string.IsNullOrWhiteSpace(쟁점초안.PublicSummary)
            || string.IsNullOrWhiteSpace(쟁점초안.MaskedReporterDisplayName)
            || 쟁점초안.DeliberationHours <= 0)
        {
            return 유효성실패("쟁점 제목, 공개 설명, 제기자 표시명과 숙고 시간을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await _service.협상쟁점등록Async(
                    campaignId.Value,
                    쟁점초안,
                    token)
                    ?? throw new InvalidOperationException("협상 쟁점 등록 응답이 비어 있습니다.");
                협상이력.Issues.Add(created);
                선택된쟁점Id = created.IssueId;
                OnPropertyChanged(nameof(협상이력));
                OnPropertyChanged(nameof(미해결쟁점수));
                쟁점초안 = 새쟁점초안();
            },
            "쟁점을 열고 숙고 시간을 시작했습니다.",
            cancellationToken);
    }

    public bool 쟁점선택(Guid issueId)
    {
        if (협상이력.Issues.All(issue => issue.IssueId != issueId))
        {
            return 유효성실패("협상 이력에 존재하는 쟁점을 선택해 주세요.");
        }

        선택된쟁점Id = issueId;
        return true;
    }

    public async Task<bool> 숙고의견등록Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 공급 협상 의견은 국내 공동구매 분기에서만 등록할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null || 선택된쟁점Id is null)
        {
            return 유효성실패("의견을 남길 공동구매 쟁점을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(숙고의견초안.MaskedParticipantDisplayName)
            || string.IsNullOrWhiteSpace(숙고의견초안.ParticipantRoleLabel)
            || string.IsNullOrWhiteSpace(숙고의견초안.PublicRationale))
        {
            return 유효성실패("참여자 표시명, 역할과 공개 의견을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var updated = await _service.숙고의견등록Async(
                    campaignId.Value,
                    선택된쟁점Id.Value,
                    숙고의견초안,
                    token)
                    ?? throw new InvalidOperationException("숙고 의견 등록 응답이 비어 있습니다.");
                쟁점갱신(updated);
                숙고의견초안 = 새숙고의견초안();
            },
            "숙고 의견을 공개했습니다.",
            cancellationToken);
    }

    public async Task<bool> 쟁점합의Async(CancellationToken cancellationToken = default)
    {
        if (!_분기.국내공동구매활성)
        {
            return 유효성실패("국내 공급 협상 합의는 국내 공동구매 분기에서만 등록할 수 있습니다.");
        }

        var campaignId = _화면상태.선택된공동구매Id;
        if (campaignId is null || 선택된쟁점Id is null)
        {
            return 유효성실패("합의할 공동구매 쟁점을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(합의초안.MaskedResolverDisplayName)
            || string.IsNullOrWhiteSpace(합의초안.ResolverRoleLabel)
            || string.IsNullOrWhiteSpace(합의초안.ResolutionSummary)
            || string.IsNullOrWhiteSpace(합의초안.DecisionRationale))
        {
            return 유효성실패("합의자 표시명, 역할, 합의 내용과 판단 근거를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var updated = await _service.협상쟁점합의Async(
                    campaignId.Value,
                    선택된쟁점Id.Value,
                    합의초안,
                    token)
                    ?? throw new InvalidOperationException("협상 쟁점 합의 응답이 비어 있습니다.");
                쟁점갱신(updated);
                선택된쟁점Id = 협상이력.Issues.FirstOrDefault(issue =>
                    issue.StatusCode == DomesticGroupPurchaseNegotiationIssueStatusCodes.Deliberating)?.IssueId;
                합의초안 = new();
            },
            "쟁점의 합의 결과를 공개했습니다.",
            cancellationToken);
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        GC.SuppressFinalize(this);
    }

    private void 쟁점갱신(DomesticGroupPurchaseNegotiationIssueResponse updated)
    {
        var index = 협상이력.Issues.FindIndex(issue => issue.IssueId == updated.IssueId);
        if (index >= 0)
        {
            협상이력.Issues[index] = updated;
        }
        else
        {
            협상이력.Issues.Add(updated);
        }

        OnPropertyChanged(nameof(협상이력));
        OnPropertyChanged(nameof(미해결쟁점수));
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
        => 공동구매변경동기화();

    private void 공동구매변경동기화()
    {
        var campaignId = _화면상태.선택된공동구매Id;
        if (_대상공동구매Id == campaignId)
        {
            return;
        }

        _대상공동구매Id = campaignId;
        협상이력 = new DomesticGroupPurchaseNegotiationTimelineResponse
        {
            GroupPurchaseCampaignId = campaignId ?? Guid.Empty
        };
        선택된쟁점Id = null;
        이벤트초안 = 새이벤트초안();
        쟁점초안 = 새쟁점초안();
        숙고의견초안 = 새숙고의견초안();
        합의초안 = new();
    }

    private static DomesticGroupPurchaseNegotiationEventRequest 새이벤트초안()
        => new() { EventTypeCode = DomesticGroupPurchaseNegotiationEventTypeCodes.Proposal };

    private static DomesticGroupPurchaseNegotiationIssueRequest 새쟁점초안()
        => new() { DeliberationHours = 24 };

    private static DomesticGroupPurchaseDeliberationPositionRequest 새숙고의견초안()
        => new() { PositionCode = DomesticGroupPurchaseDeliberationPositionCodes.Concern };
}

public sealed class 공동구매공급기능ViewModel : 조립ViewModelBase
{
    public 공동구매공급기능ViewModel(
        공동구매생산자연결ViewModel 생산자연결,
        공동구매공급제안ViewModel 공급제안,
        공동구매공급적합성ViewModel 공급적합성,
        공동구매협상ViewModel 협상,
        공동구매생산자후보조회ViewModel 생산자후보조회,
        공동구매생산자연락요청ViewModel 생산자연락요청,
        공동구매대표후보조회ViewModel 대표후보조회,
        공동구매공급제안등록ViewModel 공급제안등록,
        공동구매공급적합성미리보기ViewModel 공급적합성미리보기,
        공동구매협상이력조회ViewModel 협상이력조회,
        공동구매협상이벤트등록ViewModel 협상이벤트등록,
        공동구매협상쟁점등록ViewModel 협상쟁점등록,
        공동구매숙고의견등록ViewModel 숙고의견등록,
        공동구매협상쟁점합의ViewModel 협상쟁점합의)
    {
        this.생산자연결 = 하위ViewModel등록(생산자연결, 수명소유: false);
        this.공급제안 = 하위ViewModel등록(공급제안, 수명소유: false);
        this.공급적합성 = 하위ViewModel등록(공급적합성, 수명소유: false);
        this.협상 = 하위ViewModel등록(협상, 수명소유: false);
        세부업무목록 =
        [
            하위ViewModel등록(생산자후보조회, 수명소유: false),
            하위ViewModel등록(생산자연락요청, 수명소유: false),
            하위ViewModel등록(대표후보조회, 수명소유: false),
            하위ViewModel등록(공급제안등록, 수명소유: false),
            하위ViewModel등록(공급적합성미리보기, 수명소유: false),
            하위ViewModel등록(협상이력조회, 수명소유: false),
            하위ViewModel등록(협상이벤트등록, 수명소유: false),
            하위ViewModel등록(협상쟁점등록, 수명소유: false),
            하위ViewModel등록(숙고의견등록, 수명소유: false),
            하위ViewModel등록(협상쟁점합의, 수명소유: false)
        ];
    }

    public 공동구매생산자연결ViewModel 생산자연결 { get; }
    public 공동구매공급제안ViewModel 공급제안 { get; }
    public 공동구매공급적합성ViewModel 공급적합성 { get; }
    public 공동구매협상ViewModel 협상 { get; }
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }

    public bool 처리중
        => 생산자연결.처리중 || 공급제안.처리중 || 공급적합성.처리중 || 협상.처리중;
}
