using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public abstract class 주문업무조각ViewModelBase(
    주문업무상태ViewModel 상태,
    string 업무코드,
    string 업무명,
    업무조각유형 업무유형) : 주문업무ViewModelBase(상태), I업무조각ViewModel
{
    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
    public 업무조각유형 업무유형 { get; } = 업무유형;
}

public sealed class 주문하위원장조회ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태)
    : 주문업무조각ViewModelBase(
        상태,
        "order-child-ledger-query",
        "주문 하위 원장 조회",
        업무조각유형.목록조회), I목록조회ViewModel<주문원장포함원장참조Dto>
{
    public IReadOnlyList<주문원장포함원장참조Dto> 항목목록 => 주문상태.하위원장목록;

    public async Task<bool> 조회Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var orderLedgerId))
        {
            return false;
        }

        return await 작업실행Async(
            async token => 주문상태.역할별결과적용(
                await service.주문원장보호조회Async(orderLedgerId, token)
                ?? throw new InvalidOperationException("주문 하위 원장 조회 응답이 비어 있습니다.")),
            "주문 하위 원장을 조회했습니다.",
            cancellationToken);
    }

    public void 선택(주문원장포함원장참조Dto? item)
        => 주문상태.하위원장선택(item);
}

public sealed class 주문하위원장연결ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태)
    : 주문업무조각ViewModelBase(
        상태,
        "order-child-ledger-connect",
        "주문 하위 원장 연결",
        업무조각유형.등록), I등록ViewModel<주문하위원장연결ClientRequest>
{
    private 주문하위원장연결ClientRequest _초안 = new()
    {
        역할 = 주문원장포함역할.판매,
        필수여부 = true
    };

    public 주문하위원장연결ClientRequest 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public IReadOnlySet<string> 연결가능역할 => 주문원장포함역할.All;

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var rootId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(초안.하위원장Id))
        {
            return 유효성실패("연결할 하위 원장 ID를 입력해 주세요.");
        }

        if (string.Equals(rootId, 초안.하위원장Id.Trim(), StringComparison.Ordinal))
        {
            return 유효성실패("주문 원장을 자기 자신의 하위 원장으로 연결할 수 없습니다.");
        }

        if (!주문원장포함역할.All.Contains(초안.역할))
        {
            return 유효성실패("하위 원장의 업무 역할을 선택해 주세요.");
        }

        초안.기대Revision ??= 주문상태.현재원장Revision;
        return await 작업실행Async(
            async token =>
            {
                var result = await service.하위원장연결Async(rootId, 초안, token)
                    ?? throw new InvalidOperationException("하위 원장 연결 응답이 비어 있습니다.");
                주문상태.통합결과적용(result);
                주문상태.하위원장선택(result.주문원장.포함원장목록.FirstOrDefault(x =>
                    string.Equals(x.원장Id, 초안.하위원장Id, StringComparison.OrdinalIgnoreCase)));
                초안 = new 주문하위원장연결ClientRequest
                {
                    역할 = 초안.역할,
                    필수여부 = true,
                    기대Revision = result.주문원장.Revision
                };
            },
            "주문 하위 원장을 연결했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 주문하위원장수정ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태)
    : 주문업무조각ViewModelBase(
        상태,
        "order-child-ledger-update",
        "주문 하위 원장 관계 수정",
        업무조각유형.수정), I수정ViewModel<주문하위원장연결ClientRequest>
{
    private 주문하위원장연결ClientRequest _초안 = new();

    public 주문하위원장연결ClientRequest 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public IReadOnlySet<string> 연결가능역할 => 주문원장포함역할.All;

    public bool 선택항목적용()
    {
        var selected = 주문상태.선택된하위원장;
        if (selected is null)
        {
            return 유효성실패("수정할 하위 원장 관계를 먼저 선택해 주세요.");
        }

        초안 = new 주문하위원장연결ClientRequest
        {
            하위원장Id = selected.원장Id,
            역할 = selected.역할,
            필수여부 = selected.필수여부,
            표시순서 = selected.표시순서,
            기대Revision = 주문상태.현재원장Revision
        };
        return true;
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var rootId))
        {
            return false;
        }

        var selected = 주문상태.선택된하위원장;
        if (selected is null)
        {
            return 유효성실패("수정할 하위 원장 관계를 먼저 선택해 주세요.");
        }

        if (!string.Equals(selected.원장Id, 초안.하위원장Id?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return 유효성실패("수정 중에는 하위 원장 ID를 변경할 수 없습니다.");
        }

        if (!주문원장포함역할.All.Contains(초안.역할))
        {
            return 유효성실패("하위 원장의 업무 역할을 선택해 주세요.");
        }

        초안.기대Revision ??= 주문상태.현재원장Revision;
        return await 작업실행Async(
            async token =>
            {
                var result = await service.하위원장연결Async(rootId, 초안, token)
                    ?? throw new InvalidOperationException("하위 원장 관계 수정 응답이 비어 있습니다.");
                주문상태.통합결과적용(result);
                주문상태.하위원장선택(result.주문원장.포함원장목록.FirstOrDefault(x =>
                    string.Equals(x.원장Id, selected.원장Id, StringComparison.OrdinalIgnoreCase)));
                선택항목적용();
            },
            "주문 하위 원장 관계를 수정했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 주문하위원장분리초안
{
    public string 하위원장Id { get; set; } = string.Empty;
    public long? 기대Revision { get; set; }
}

public sealed class 주문하위원장분리ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태)
    : 주문업무조각ViewModelBase(
        상태,
        "order-child-ledger-detach",
        "주문 하위 원장 분리",
        업무조각유형.삭제), I삭제ViewModel<주문하위원장분리초안>
{
    private 주문하위원장분리초안 _초안 = new();

    public 주문하위원장분리초안 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var rootId))
        {
            return false;
        }

        var childLedgerId = string.IsNullOrWhiteSpace(초안.하위원장Id)
            ? 주문상태.선택된하위원장?.원장Id
            : 초안.하위원장Id.Trim();
        if (string.IsNullOrWhiteSpace(childLedgerId))
        {
            return 유효성실패("분리할 하위 원장을 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await service.하위원장분리Async(
                        rootId,
                        childLedgerId,
                        초안.기대Revision ?? 주문상태.현재원장Revision,
                        token)
                    ?? throw new InvalidOperationException("하위 원장 분리 응답이 비어 있습니다.");
                주문상태.통합결과적용(result);
                주문상태.하위원장선택(null);
                초안 = new 주문하위원장분리초안 { 기대Revision = result.주문원장.Revision };
            },
            "주문 하위 원장을 분리했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));

    public bool 선택항목적용()
    {
        var selected = 주문상태.선택된하위원장;
        if (selected is null)
        {
            return 유효성실패("분리할 하위 원장을 먼저 선택해 주세요.");
        }

        초안 = new 주문하위원장분리초안
        {
            하위원장Id = selected.원장Id,
            기대Revision = 주문상태.현재원장Revision
        };
        return true;
    }
}

public sealed class 주문하위원장관계CrudViewModel(
    주문하위원장조회ViewModel 조회,
    주문하위원장연결ViewModel 등록,
    주문하위원장수정ViewModel 수정,
    주문하위원장분리ViewModel 삭제)
    : 업무단위CrudViewModelBase<주문하위원장조회ViewModel, 주문하위원장연결ViewModel, 주문하위원장수정ViewModel, 주문하위원장분리ViewModel>(
        "order-child-ledger-relation",
        "주문 하위 원장 관계",
        조회,
        등록,
        수정,
        삭제);

public sealed class 주문서명상태조회ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태)
    : 주문업무조각ViewModelBase(
        상태,
        "order-signature-query",
        "주문 서명 상태 조회",
        업무조각유형.상세조회)
{
    public 주문원장서명상태공개Dto? 결과 => 주문상태.서명상태;

    public async Task<bool> 조회Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var orderLedgerId))
        {
            return false;
        }

        return await 작업실행Async(
            async token => 주문상태.서명상태적용(
                await service.주문원장서명상태조회Async(orderLedgerId, token)
                ?? throw new InvalidOperationException("주문 서명 상태 응답이 비어 있습니다.")),
            "주문 서명 상태를 조회했습니다.",
            cancellationToken);
    }
}

public sealed class 주문서명준비ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태)
    : 주문업무조각ViewModelBase(
        상태,
        "order-signature-prepare",
        "주문 서명 준비",
        업무조각유형.처리), I명령ViewModel<주문원장서명준비ClientRequest>
{
    private 주문원장서명준비ClientRequest _초안 = new();

    public 주문원장서명준비ClientRequest 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var orderLedgerId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(초안.계약문서번호) || string.IsNullOrWhiteSpace(초안.문서Hash))
        {
            return 유효성실패("계약 문서번호와 문서 해시를 입력해 주세요.");
        }

        초안.기대Revision ??= 주문상태.현재서명Revision;
        return await 작업실행Async(
            async token => 주문상태.서명상태적용(
                await service.주문원장서명준비Async(orderLedgerId, 초안, token)
                ?? throw new InvalidOperationException("주문 서명 준비 응답이 비어 있습니다.")),
            "주문 서명을 준비했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 주문서명등록ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태)
    : 주문업무조각ViewModelBase(
        상태,
        "order-signature-create",
        "주문 서명 등록",
        업무조각유형.등록), I등록ViewModel<주문원장서명등록ClientRequest>
{
    private 주문원장서명등록ClientRequest _초안 = new()
    {
        서명방법Code = ContractSignatureMethodCode.PlatformClickSign
    };

    public 주문원장서명등록ClientRequest 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var orderLedgerId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(초안.문서Hash)
            || string.IsNullOrWhiteSpace(초안.동의문Hash)
            || string.IsNullOrWhiteSpace(초안.서명증적Hash))
        {
            return 유효성실패("문서, 동의문과 서명 증적 해시를 모두 입력해 주세요.");
        }

        초안.기대Revision ??= 주문상태.현재서명Revision;
        return await 작업실행Async(
            async token => 주문상태.서명상태적용(
                await service.주문원장서명등록Async(orderLedgerId, 초안, token)
                ?? throw new InvalidOperationException("주문 서명 등록 응답이 비어 있습니다.")),
            "주문 서명을 등록했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}
