using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 공동구매자동집단조회ViewModel(공동구매자동집단ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매자동집단ViewModel>(
        원본,
        "group-purchase-automatic-group-list",
        "공동구매 자동집단 조회",
        업무조각유형.목록조회), I목록조회ViewModel<공동구매자동집단응답>
{
    public 공동구매자동집단조회조건 조회조건 => 원본.조회조건;
    public IReadOnlyList<공동구매자동집단응답> 항목목록 => 원본.자동집단목록;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.목록조회Async(cancellationToken);

    public bool 선택(string automaticGroupId) => 원본.자동집단선택(automaticGroupId);
}

public sealed class 공동구매자동수요등록ViewModel(공동구매자동집단ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매자동집단ViewModel>(
        원본,
        "group-purchase-automatic-demand-create",
        "공동구매 자동수요 등록",
        업무조각유형.등록), I등록ViewModel<공동구매자동수요등록Command>
{
    public 공동구매자동수요등록Command 초안 => 원본.수요초안;
    public 공동구매자동집단응답? 결과 => 원본.선택된자동집단;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.수요등록Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매주문원장상세조회ViewModel(공동구매주문원장조회ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매주문원장조회ViewModel>(
        원본,
        "group-purchase-order-ledger-detail",
        "공동구매 주문원장 조회",
        업무조각유형.상세조회), I상세조회ViewModel<주문원장통합공개Dto>
{
    public 주문원장통합공개Dto? 항목 => 원본.통합결과;
    public 주문원장역할별조회공개Dto? 역할별항목 => 원본.역할별결과;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.조회Async(cancellationToken);

    public bool 보기선택(string viewCode) => 원본.보기선택(viewCode);
}

public sealed class 공동구매주문하위원장연결ViewModel(공동구매하위원장ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매하위원장ViewModel>(
        원본,
        "group-purchase-order-child-ledger-connect",
        "공동구매 주문 하위원장 연결",
        업무조각유형.등록), I등록ViewModel<주문하위원장연결ClientRequest>
{
    public 주문하위원장연결ClientRequest 초안 => 원본.연결초안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.연결Async(cancellationToken);

    public void 입력변경알림() => 원본.입력변경알림();
}

public sealed class 공동구매주문하위원장분리초안
{
    public string 하위원장Id { get; set; } = string.Empty;
}

public sealed class 공동구매주문하위원장분리ViewModel(공동구매하위원장ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매하위원장ViewModel>(
        원본,
        "group-purchase-order-child-ledger-disconnect",
        "공동구매 주문 하위원장 분리",
        업무조각유형.삭제), I삭제ViewModel<공동구매주문하위원장분리초안>
{
    public 공동구매주문하위원장분리초안 초안 { get; } = new();
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.분리Async(초안.하위원장Id, cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매주문서명상태조회ViewModel(공동구매주문원장서명ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매주문원장서명ViewModel>(
        원본,
        "group-purchase-order-signature-detail",
        "공동구매 주문 서명 상태 조회",
        업무조각유형.상세조회), I상세조회ViewModel<주문원장서명상태공개Dto>
{
    public 주문원장서명상태공개Dto? 항목 => 원본.서명상태;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.상태조회Async(cancellationToken);
}

public sealed class 공동구매주문서명준비ViewModel(공동구매주문원장서명ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매주문원장서명ViewModel>(
        원본,
        "group-purchase-order-signature-prepare",
        "공동구매 주문 서명 준비",
        업무조각유형.처리), I명령ViewModel<주문원장서명준비ClientRequest>
{
    public 주문원장서명준비ClientRequest 초안 => 원본.서명준비초안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.서명준비Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매주문서명등록ViewModel(공동구매주문원장서명ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매주문원장서명ViewModel>(
        원본,
        "group-purchase-order-signature-create",
        "공동구매 주문 서명 등록",
        업무조각유형.등록), I등록ViewModel<주문원장서명등록ClientRequest>
{
    public 주문원장서명등록ClientRequest 초안 => 원본.서명등록초안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.서명등록Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 공동구매커머스이행조회ViewModel(공동구매커머스이행ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매커머스이행ViewModel>(
        원본,
        "group-purchase-commerce-fulfillment-list",
        "공동구매 커머스 이행 조회",
        업무조각유형.목록조회), I목록조회ViewModel<공동구매커머스이행계획공개Dto>
{
    public string 공동구매Id { get => 원본.공동구매Id; set => 원본.공동구매Id = value; }
    public IReadOnlyList<공동구매커머스이행계획공개Dto> 항목목록 => 원본.이행계획목록;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.공동구매별조회Async(cancellationToken);

    public Task<bool> 선택Async(string documentManagementNumber, string? deliveryScopeKey = null, CancellationToken cancellationToken = default)
        => 원본.이행계획선택Async(documentManagementNumber, deliveryScopeKey, cancellationToken);
}

public sealed class 공동구매커머스문서조회ViewModel(공동구매커머스이행ViewModel 원본)
    : 위임업무조각ViewModelBase<공동구매커머스이행ViewModel>(
        원본,
        "group-purchase-commerce-document-list",
        "공동구매 커머스 문서 조회",
        업무조각유형.목록조회), I목록조회ViewModel<공동구매커머스이행계획공개Dto>
{
    public string 문서관리번호 { get => 원본.문서관리번호; set => 원본.문서관리번호 = value; }
    public IReadOnlyList<공동구매커머스이행계획공개Dto> 항목목록 => 원본.이행계획목록;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.문서번호조회Async(cancellationToken);
}
