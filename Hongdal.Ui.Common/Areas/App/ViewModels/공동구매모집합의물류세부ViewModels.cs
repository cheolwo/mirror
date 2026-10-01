using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 공동구매목록조회조각ViewModel(
    공동구매목록ViewModel 원본,
    공동구매화면상태ViewModel 화면상태)
    : 위임업무조각ViewModelBase<공동구매목록ViewModel>(
        원본,
        "group-purchase-list",
        "공동구매 목록 조회",
        업무조각유형.목록조회), I목록조회ViewModel<CommunityVoteResponse>
{
    public IReadOnlyList<CommunityVoteResponse> 항목목록 => 화면상태.공동구매목록;
    public string HS코드 { get => 원본.조회HS코드; set => 원본.조회HS코드 = value; }
    public string 거래경로필터 { get => 원본.거래경로필터; set => 원본.거래경로필터 = value; }
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.목록조회Async(cancellationToken: cancellationToken);
}

public sealed class 공동구매상세조회ViewModel(
    공동구매목록ViewModel 원본,
    공동구매화면상태ViewModel 화면상태)
    : 위임업무조각ViewModelBase<공동구매목록ViewModel>(
        원본,
        "group-purchase-detail",
        "공동구매 상세 조회",
        업무조각유형.상세조회), I상세조회ViewModel<CommunityVoteResponse>
{
    private Guid _공동구매Id;

    public Guid 공동구매Id
    {
        get => _공동구매Id;
        set => SetProperty(ref _공동구매Id, value);
    }

    public CommunityVoteResponse? 항목 => 화면상태.선택된공동구매;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.선택Async(공동구매Id, cancellationToken);
}

public sealed class 공동구매제안등록조각ViewModel(공동구매제안ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매제안ViewModel>(
        원본,
        "group-purchase-proposal-create",
        "공동구매 제안 등록",
        업무조각유형.등록), I등록ViewModel<공동구매제안ViewModel>
{
    public 공동구매제안ViewModel 초안 => 원본;
    public CommunityVoteResponse? 결과 => 원본.생성된공동구매;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.등록Async(cancellationToken);
}

public sealed class 공동구매수요참여등록ViewModel(공동구매수요참여ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매수요참여ViewModel>(
        원본,
        "group-purchase-demand-participation-create",
        "공동구매 수요 참여",
        업무조각유형.등록), I등록ViewModel<공동구매수요참여ViewModel>
{
    public 공동구매수요참여ViewModel 초안 => 원본;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.참여Async(cancellationToken);
}

public sealed class 공동구매이의등록ViewModel(공동구매이의검토ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매이의검토ViewModel>(
        원본,
        "group-purchase-objection-create",
        "공동구매 이의 등록",
        업무조각유형.등록), I등록ViewModel<공동구매이의검토ViewModel>
{
    public 공동구매이의검토ViewModel 초안 => 원본;
    public IReadOnlyList<PlatformCommunityPostCommentResponse> 이의목록 => 원본.전체이의;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.등록Async(cancellationToken);
}

public sealed class 공동구매모집마감처리ViewModel(공동구매모집마감ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매모집마감ViewModel>(
        원본,
        "group-purchase-recruitment-close",
        "공동구매 모집 마감",
        업무조각유형.처리), I명령ViewModel<공동구매모집마감ViewModel>
{
    public 공동구매모집마감ViewModel 초안 => 원본;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.마감Async(cancellationToken);
}

public sealed class 공동구매결의문등록ViewModel(공동구매결의ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매결의ViewModel>(
        원본,
        "group-purchase-resolution-create",
        "공동구매 결의문 등록",
        업무조각유형.등록), I등록ViewModel<공동구매결의ViewModel>
{
    public 공동구매결의ViewModel 초안 => 원본;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.결의문작성Async(cancellationToken);
}

public sealed class 공동구매결의서명준비ViewModel(공동구매결의ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매결의ViewModel>(
        원본,
        "group-purchase-resolution-signature-prepare",
        "공동구매 결의 서명 준비",
        업무조각유형.처리), I명령ViewModel<공동구매결의ViewModel>
{
    public 공동구매결의ViewModel 초안 => 원본;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.서명준비Async(cancellationToken);
}

public sealed class 공동구매전자서명초안
{
    public string 서명자이름 { get; set; } = string.Empty;
    public string 서명증빙Payload { get; set; } = string.Empty;
    public string? 접속IpHash { get; set; }
}

public sealed class 공동구매전자서명등록ViewModel(공동구매전자서명ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매전자서명ViewModel>(
        원본,
        "group-purchase-resolution-signature-create",
        "공동구매 전자서명 등록",
        업무조각유형.등록), I등록ViewModel<공동구매전자서명초안>
{
    public 공동구매전자서명초안 초안 { get; } = new();
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.서명제출Async(
            new 공동구매전자서명입력(
                초안.서명자이름,
                초안.서명증빙Payload,
                초안.접속IpHash),
            cancellationToken);

    public bool 서명자선택(string partyId) => 원본.서명자선택(partyId);
    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매이행계획미리보기ViewModel(공동구매이행계획ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매이행계획ViewModel>(
        원본,
        "group-purchase-fulfillment-preview",
        "공동구매 이행계획 미리보기",
        업무조각유형.처리), I명령ViewModel<DomesticGroupPurchaseFulfillmentPlanRequest>
{
    public DomesticGroupPurchaseFulfillmentPlanRequest 초안 => 원본.초안;
    public DomesticGroupPurchaseFulfillmentPlanResponse? 결과 => 원본.계획;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.서버미리보기Async(cancellationToken);

    public DomesticGroupPurchaseFulfillmentPlanResponse 로컬미리보기() => 원본.로컬미리보기();
    public bool 경로선택(string routeCode) => 원본.경로선택(routeCode);
}

public sealed class 공동구매발주초안등록ViewModel(공동구매이행계획ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매이행계획ViewModel>(
        원본,
        "group-purchase-order-draft-create",
        "공동구매 발주 초안 등록",
        업무조각유형.등록), I등록ViewModel<DomesticGroupPurchaseFulfillmentPlanRequest>
{
    public DomesticGroupPurchaseFulfillmentPlanRequest 초안 => 원본.초안;
    public DomesticGroupPurchaseFulfillmentOrderDraftResponse? 결과 => 원본.저장된발주초안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.발주초안저장Async(cancellationToken);
}
