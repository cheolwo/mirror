using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 공동구매생산자후보조회ViewModel(공동구매생산자연결ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매생산자연결ViewModel>(
        원본,
        "group-purchase-producer-candidate-list",
        "생산자 후보 조회",
        업무조각유형.목록조회), I목록조회ViewModel<DomesticProducerCandidateResponse>
{
    public IReadOnlyList<DomesticProducerCandidateResponse> 항목목록 => 원본.생산자후보;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.후보조회Async(cancellationToken);

    public bool 선택(string candidateKey) => 원본.생산자선택(candidateKey);
}

public sealed class 공동구매생산자연락요청ViewModel(공동구매생산자연결ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매생산자연결ViewModel>(
        원본,
        "group-purchase-producer-contact-create",
        "생산자 연락 요청",
        업무조각유형.등록), I등록ViewModel<DomesticProducerContactRequestDraftRequest>
{
    public DomesticProducerContactRequestDraftRequest 초안 => 원본.연락요청초안;
    public DomesticProducerContactRequestDraftResponse? 결과 => 원본.저장된연락요청;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.연락요청저장Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매대표후보조회ViewModel(공동구매공급제안ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매공급제안ViewModel>(
        원본,
        "group-purchase-representative-candidate-list",
        "공동구매 대표 후보 조회",
        업무조각유형.목록조회), I목록조회ViewModel<DomesticGroupPurchaseRepresentativeCandidateResponse>
{
    public IReadOnlyList<DomesticGroupPurchaseRepresentativeCandidateResponse> 항목목록 => 원본.공동구매대표후보;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.대표조회Async(cancellationToken);

    public bool 선택(string candidateKey) => 원본.대표선택(candidateKey);
}

public sealed class 공동구매공급제안등록ViewModel(공동구매공급제안ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매공급제안ViewModel>(
        원본,
        "group-purchase-supply-offer-create",
        "공동구매 공급 제안 등록",
        업무조각유형.등록), I등록ViewModel<DomesticProducerSupplyOfferDraftRequest>
{
    public DomesticProducerSupplyOfferDraftRequest 초안 => 원본.공급제안초안;
    public DomesticProducerSupplyOfferDraftResponse? 결과 => 원본.저장된공급제안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.공급제안저장Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매공급적합성미리보기ViewModel(공동구매공급적합성ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매공급적합성ViewModel>(
        원본,
        "group-purchase-supply-compatibility-preview",
        "공동구매 공급 적합성 미리보기",
        업무조각유형.처리), I명령ViewModel<DomesticGroupPurchaseSupplyCompatibilityPreviewRequest>
{
    public DomesticGroupPurchaseSupplyCompatibilityPreviewRequest 초안 => 원본.조건;
    public DomesticGroupPurchaseSupplyCompatibilityPreviewResponse? 결과 => 원본.판정결과;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.미리보기Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매협상이력조회ViewModel(공동구매협상ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매협상ViewModel>(
        원본,
        "group-purchase-negotiation-detail",
        "공동구매 협상 이력 조회",
        업무조각유형.상세조회), I상세조회ViewModel<DomesticGroupPurchaseNegotiationTimelineResponse>
{
    public DomesticGroupPurchaseNegotiationTimelineResponse 항목 => 원본.협상이력;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.이력조회Async(cancellationToken);

    public bool 쟁점선택(Guid issueId) => 원본.쟁점선택(issueId);
}

public sealed class 공동구매협상이벤트등록ViewModel(공동구매협상ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매협상ViewModel>(
        원본,
        "group-purchase-negotiation-event-create",
        "공동구매 협상 이벤트 등록",
        업무조각유형.등록), I등록ViewModel<DomesticGroupPurchaseNegotiationEventRequest>
{
    public DomesticGroupPurchaseNegotiationEventRequest 초안 => 원본.이벤트초안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.이벤트등록Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매협상쟁점등록ViewModel(공동구매협상ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매협상ViewModel>(
        원본,
        "group-purchase-negotiation-issue-create",
        "공동구매 협상 쟁점 등록",
        업무조각유형.등록), I등록ViewModel<DomesticGroupPurchaseNegotiationIssueRequest>
{
    public DomesticGroupPurchaseNegotiationIssueRequest 초안 => 원본.쟁점초안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.쟁점등록Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매숙고의견등록ViewModel(공동구매협상ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매협상ViewModel>(
        원본,
        "group-purchase-deliberation-position-create",
        "공동구매 숙고 의견 등록",
        업무조각유형.등록), I등록ViewModel<DomesticGroupPurchaseDeliberationPositionRequest>
{
    public DomesticGroupPurchaseDeliberationPositionRequest 초안 => 원본.숙고의견초안;
    public Guid? 선택된쟁점Id => 원본.선택된쟁점Id;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.숙고의견등록Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매협상쟁점합의ViewModel(공동구매협상ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매협상ViewModel>(
        원본,
        "group-purchase-negotiation-issue-resolve",
        "공동구매 협상 쟁점 합의",
        업무조각유형.처리), I명령ViewModel<DomesticGroupPurchaseNegotiationResolutionRequest>
{
    public DomesticGroupPurchaseNegotiationResolutionRequest 초안 => 원본.합의초안;
    public Guid? 선택된쟁점Id => 원본.선택된쟁점Id;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.쟁점합의Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}
