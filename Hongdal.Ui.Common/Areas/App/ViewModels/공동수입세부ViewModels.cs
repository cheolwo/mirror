using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 공동수입원장조회ViewModel(공동수입원장물류ViewModel 원본)
    : 위임업무조각ViewModelBase<공동수입원장물류ViewModel>(
        원본,
        "group-import-ledger-detail",
        "공동수입 원장 조회",
        업무조각유형.상세조회), I상세조회ViewModel<CommunityGroupImportLedgerPlanResponse>
{
    public CommunityGroupImportLedgerPlanResponse? 항목 => 원본.저장된원장;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.기존원장조회Async(cancellationToken);
}

public sealed class 공동수입원장미리보기ViewModel(공동수입원장물류ViewModel 원본)
    : 위임업무조각ViewModelBase<공동수입원장물류ViewModel>(
        원본,
        "group-import-ledger-preview",
        "공동수입 원장 미리보기",
        업무조각유형.처리), I명령ViewModel<CommunityGroupImportLedgerConversionRequest>
{
    public CommunityGroupImportLedgerConversionRequest 초안 => 원본.초안;
    public CommunityGroupImportLedgerPlanResponse? 결과 => 원본.계획;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.서버미리보기Async(cancellationToken);

    public CommunityGroupImportLedgerPlanResponse 로컬미리보기() => 원본.로컬미리보기();
    public bool 물류경로선택(string routeCode) => 원본.물류경로선택(routeCode);
    public void 입력변경알림() => 원본.입력변경알림();
}

public sealed class 공동수입원장전환ViewModel(공동수입원장물류ViewModel 원본)
    : 위임업무조각ViewModelBase<공동수입원장물류ViewModel>(
        원본,
        "group-import-ledger-convert",
        "공동수입 원장 전환",
        업무조각유형.처리), I명령ViewModel<CommunityGroupImportLedgerConversionRequest>
{
    public CommunityGroupImportLedgerConversionRequest 초안 => 원본.초안;
    public CommunityGroupImportLedgerPlanResponse? 결과 => 원본.저장된원장;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.공동수입원장전환Async(cancellationToken);
}

public sealed class 공동수입선적공개조회ViewModel(공동수입선적통관ViewModel 원본)
    : 위임업무조각ViewModelBase<공동수입선적통관ViewModel>(
        원본,
        "group-import-shipment-public-detail",
        "공동수입 선적 공개 조회",
        업무조각유형.상세조회), I상세조회ViewModel<공동구매해외선적공개Dto>
{
    private string _문서관리번호 = string.Empty;

    public string 문서관리번호
    {
        get => _문서관리번호;
        set => SetProperty(ref _문서관리번호, value);
    }

    public 공동구매해외선적공개Dto? 항목 => 원본.공개선적;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.공개조회Async(문서관리번호, cancellationToken);
}

public sealed class 공동수입선적관리목록조회ViewModel(공동수입선적통관ViewModel 원본)
    : 위임업무조각ViewModelBase<공동수입선적통관ViewModel>(
        원본,
        "group-import-shipment-admin-list",
        "공동수입 선적 관리 목록 조회",
        업무조각유형.목록조회), I목록조회ViewModel<공동구매해외선적추적Dto>
{
    public IReadOnlyList<공동구매해외선적추적Dto> 항목목록 => 원본.관리목록;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.관리자목록조회Async(cancellationToken);

    public void 선택(공동구매해외선적추적Dto shipment) => 원본.현재선적선택(shipment);
}

public sealed class 공동수입선적등록ViewModel(공동수입선적통관ViewModel 원본)
    : 위임업무조각ViewModelBase<공동수입선적통관ViewModel>(
        원본,
        "group-import-shipment-save",
        "공동수입 선적 등록",
        업무조각유형.등록), I등록ViewModel<공동수입선적초안ViewModel>
{
    public 공동수입선적초안ViewModel 초안 => 원본.선적초안;
    public 공동구매해외선적추적Dto? 결과 => 원본.현재선적;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.관리자선적저장Async(cancellationToken);
}

public sealed class 공동수입선적이벤트등록ViewModel(공동수입선적통관ViewModel 원본)
    : 위임업무조각ViewModelBase<공동수입선적통관ViewModel>(
        원본,
        "group-import-shipment-event-create",
        "공동수입 선적 이벤트 등록",
        업무조각유형.등록), I등록ViewModel<공동수입선적이벤트초안ViewModel>
{
    public 공동수입선적이벤트초안ViewModel 초안 => 원본.이벤트초안;
    public 공동구매해외선적추적Dto? 결과 => 원본.현재선적;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.관리자이벤트추가Async(cancellationToken);
}

public sealed class 공동수입통관동기화ViewModel(공동수입선적통관ViewModel 원본)
    : 위임업무조각ViewModelBase<공동수입선적통관ViewModel>(
        원본,
        "group-import-customs-sync",
        "공동수입 통관 동기화",
        업무조각유형.처리), I명령ViewModel<공동수입통관동기화초안ViewModel>
{
    public 공동수입통관동기화초안ViewModel 초안 => 원본.통관초안;
    public 공동구매해외선적통관동기화결과? 결과 => 원본.통관결과;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.관리자통관동기화Async(cancellationToken);
}
