using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class 주문원장보기코드
{
    public const string 주문자보호 = "protected-orderer";
    public const string 주문자 = "orderer";
    public const string 판매자 = "seller";
    public const string 창고담당자 = "warehouse";
    public const string 운송담당자 = "transport";

    public static IReadOnlyList<string> 전체 { get; } =
        [주문자보호, 주문자, 판매자, 창고담당자, 운송담당자];
}

/// <summary>주문 조회, 하위 원장 구성과 계약 서명이 공유하는 기본 주문 상태입니다.</summary>
public sealed class 주문업무상태ViewModel : ObservableObject
{
    public 주문업무상태ViewModel()
    {
    }

    public 주문업무상태ViewModel(IHongdal현재사용자Context 현재사용자Context)
    {
        this.현재사용자Context = 현재사용자Context;
    }

    public IHongdal현재사용자Context? 현재사용자Context { get; }
    public 현재사용자Snapshot 현재사용자
        => 현재사용자Context?.현재사용자 ?? 현재사용자Snapshot.익명;
    private string? _선택된주문원장Id;
    private 주문원장통합공개Dto? _통합결과;
    private 주문원장역할별조회공개Dto? _역할별결과;
    private 주문원장서명상태공개Dto? _서명상태;
    private 주문원장포함원장참조Dto? _선택된하위원장;

    public string? 선택된주문원장Id
    {
        get => _선택된주문원장Id;
        private set => SetProperty(ref _선택된주문원장Id, value);
    }

    public 주문원장통합공개Dto? 통합결과
    {
        get => _통합결과;
        private set
        {
            if (SetProperty(ref _통합결과, value))
            {
                OnPropertyChanged(nameof(현재원장Revision));
            }
        }
    }

    public 주문원장역할별조회공개Dto? 역할별결과
    {
        get => _역할별결과;
        private set
        {
            if (SetProperty(ref _역할별결과, value))
            {
                OnPropertyChanged(nameof(현재원장Revision));
            }
        }
    }

    public 주문원장서명상태공개Dto? 서명상태
    {
        get => _서명상태;
        private set
        {
            if (SetProperty(ref _서명상태, value))
            {
                OnPropertyChanged(nameof(현재서명Revision));
                OnPropertyChanged(nameof(전체서명완료));
            }
        }
    }

    public 주문원장포함원장참조Dto? 선택된하위원장
    {
        get => _선택된하위원장;
        private set => SetProperty(ref _선택된하위원장, value);
    }

    public IReadOnlyList<주문원장포함원장참조Dto> 하위원장목록
    {
        get
        {
            var integrated = 통합결과?.주문원장;
            var roleView = 역할별결과?.주문원장상세;
            if (integrated is null)
            {
                return roleView?.포함원장목록 ?? [];
            }

            return roleView is null || integrated.Revision >= roleView.Revision
                ? integrated.포함원장목록
                : roleView.포함원장목록;
        }
    }

    public long? 현재원장Revision => new long?[]
    {
        통합결과?.주문원장.Revision,
        역할별결과?.주문원장상세?.Revision
    }.Max();
    public long? 현재서명Revision => 서명상태?.Revision;
    public bool 전체서명완료 => 서명상태?.전체서명완료여부 == true;

    public void 주문원장선택(string? orderLedgerId)
    {
        var normalized = string.IsNullOrWhiteSpace(orderLedgerId) ? null : orderLedgerId.Trim();
        if (string.Equals(선택된주문원장Id, normalized, StringComparison.Ordinal))
        {
            return;
        }

        선택된주문원장Id = normalized;
        통합결과 = null;
        역할별결과 = null;
        서명상태 = null;
        선택된하위원장 = null;
        OnPropertyChanged(nameof(하위원장목록));
    }

    public void 역할별결과적용(주문원장역할별조회공개Dto result)
    {
        ArgumentNullException.ThrowIfNull(result);
        역할별결과 = result;
        하위원장결과동기화();
    }

    public void 통합결과적용(주문원장통합공개Dto result)
    {
        ArgumentNullException.ThrowIfNull(result);
        통합결과 = result;
        하위원장결과동기화();
        if (result.주문자서명상태 is not null)
        {
            서명상태 = result.주문자서명상태;
        }
    }

    public void 서명상태적용(주문원장서명상태공개Dto result)
    {
        ArgumentNullException.ThrowIfNull(result);
        서명상태 = result;
    }

    public void 하위원장선택(주문원장포함원장참조Dto? item)
        => 선택된하위원장 = item;

    private void 하위원장결과동기화()
    {
        OnPropertyChanged(nameof(하위원장목록));
        if (선택된하위원장 is null)
        {
            return;
        }

        선택된하위원장 = 하위원장목록.FirstOrDefault(x =>
            string.Equals(x.원장Id, 선택된하위원장.원장Id, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>주문 세부 업무가 주문 문맥과 결과를 공유하도록 하는 상위 계층입니다.</summary>
public abstract class 주문업무ViewModelBase : 업무작업ViewModelBase, IDisposable
{
    protected 주문업무ViewModelBase(주문업무상태ViewModel 상태)
    {
        주문상태 = 상태 ?? throw new ArgumentNullException(nameof(상태));
        현재사용자Context연결(주문상태.현재사용자Context);
        주문상태.PropertyChanged += 주문상태변경;
    }

    protected 주문업무상태ViewModel 주문상태 { get; }
    public 주문업무상태ViewModel 상태공유 => 주문상태;
    public string? 주문원장Id => 주문상태.선택된주문원장Id;

    protected bool 주문원장선택확인(out string orderLedgerId)
    {
        orderLedgerId = 주문원장Id ?? string.Empty;
        return !string.IsNullOrWhiteSpace(orderLedgerId)
               || 유효성실패("처리할 주문 원장을 먼저 선택해 주세요.");
    }

    public void Dispose()
    {
        주문상태.PropertyChanged -= 주문상태변경;
        GC.SuppressFinalize(this);
    }

    private void 주문상태변경(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);
}

/// <summary>주문 원장을 주문자 보호형 또는 역할별 범위로 조회하는 기본 업무입니다.</summary>
public sealed class 주문조회ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태) : 주문업무ViewModelBase(상태), I상세조회ViewModel<주문원장역할별조회공개Dto>
{
    private string _보기코드 = 주문원장보기코드.주문자보호;

    public string 업무코드 => "order-ledger-query";
    public string 업무명 => "주문 원장 조회";
    public 업무조각유형 업무유형 => 업무조각유형.상세조회;
    public 주문원장역할별조회공개Dto? 항목 => 주문상태.역할별결과;

    public string 보기코드
    {
        get => _보기코드;
        private set => SetProperty(ref _보기코드, value);
    }

    public void 주문원장선택(string? orderLedgerId)
        => 주문상태.주문원장선택(orderLedgerId);

    public bool 보기선택(string viewCode)
    {
        if (!주문원장보기코드.전체.Contains(viewCode, StringComparer.OrdinalIgnoreCase))
        {
            return 유효성실패("지원되는 주문원장 보기를 선택해 주세요.");
        }

        보기코드 = viewCode;
        return true;
    }

    public async Task<bool> 조회Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var orderLedgerId))
        {
            return false;
        }

        return await 작업실행Async(
            async token =>
            {
                var result = string.Equals(보기코드, 주문원장보기코드.주문자보호, StringComparison.OrdinalIgnoreCase)
                    ? await service.주문원장보호조회Async(orderLedgerId, token)
                    : await service.주문원장역할조회Async(orderLedgerId, 보기코드, token);
                주문상태.역할별결과적용(result
                    ?? throw new InvalidOperationException("주문원장 조회 응답이 비어 있습니다."));
            },
            "주문원장을 조회했습니다.",
            cancellationToken);
    }
}

/// <summary>판매·입고·출고·배송·운송 원장을 주문 원장에 연결하는 기본 업무입니다.</summary>
public sealed class 주문하위원장ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태) : 주문업무ViewModelBase(상태)
{
    private 주문하위원장연결ClientRequest _연결초안 = 새연결초안();

    public IReadOnlySet<string> 연결가능역할 => 주문원장포함역할.All;

    public 주문하위원장연결ClientRequest 연결초안
    {
        get => _연결초안;
        private set => SetProperty(ref _연결초안, value);
    }

    public async Task<bool> 연결Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var rootId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(연결초안.하위원장Id))
        {
            return 유효성실패("연결할 하위 원장 ID를 입력해 주세요.");
        }

        if (string.Equals(rootId, 연결초안.하위원장Id.Trim(), StringComparison.Ordinal))
        {
            return 유효성실패("주문 원장을 자기 자신의 하위 원장으로 연결할 수 없습니다.");
        }

        if (!주문원장포함역할.All.Contains(연결초안.역할))
        {
            return 유효성실패("하위 원장의 업무 역할을 선택해 주세요.");
        }

        연결초안.기대Revision ??= 주문상태.현재원장Revision;
        return await 작업실행Async(
            async token =>
            {
                var result = await service.하위원장연결Async(rootId, 연결초안, token)
                    ?? throw new InvalidOperationException("하위 원장 연결 응답이 비어 있습니다.");
                주문상태.통합결과적용(result);
                연결초안 = 새연결초안(result.주문원장.Revision, 연결초안.역할);
            },
            "하위 원장을 주문 원장에 연결했습니다.",
            cancellationToken);
    }

    public async Task<bool> 분리Async(string childLedgerId, CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var rootId))
        {
            return false;
        }

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
                        주문상태.현재원장Revision,
                        token)
                    ?? throw new InvalidOperationException("하위 원장 분리 응답이 비어 있습니다.");
                주문상태.통합결과적용(result);
                연결초안.기대Revision = result.주문원장.Revision;
                OnPropertyChanged(nameof(연결초안));
            },
            "하위 원장을 주문 원장에서 분리했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(연결초안));

    private static 주문하위원장연결ClientRequest 새연결초안(
        long? revision = null,
        string? role = null)
        => new()
        {
            역할 = role ?? 주문원장포함역할.판매,
            필수여부 = true,
            기대Revision = revision
        };
}

/// <summary>주문 계약 문서의 서명 준비, 등록과 상태 조회를 담당하는 기본 업무입니다.</summary>
public sealed class 주문서명ViewModel(
    I주문원장Service service,
    주문업무상태ViewModel 상태) : 주문업무ViewModelBase(상태)
{
    private 주문원장서명준비ClientRequest _서명준비초안 = new();
    private 주문원장서명등록ClientRequest _서명등록초안 = new()
    {
        서명방법Code = ContractSignatureMethodCode.PlatformClickSign
    };

    public 주문원장서명준비ClientRequest 서명준비초안
    {
        get => _서명준비초안;
        private set => SetProperty(ref _서명준비초안, value);
    }

    public 주문원장서명등록ClientRequest 서명등록초안
    {
        get => _서명등록초안;
        private set => SetProperty(ref _서명등록초안, value);
    }

    public bool 전체서명완료 => 주문상태.전체서명완료;

    public async Task<bool> 상태조회Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var orderLedgerId))
        {
            return false;
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await service.주문원장서명상태조회Async(orderLedgerId, token)
                    ?? throw new InvalidOperationException("주문원장 서명 상태 응답이 비어 있습니다.");
                주문상태.서명상태적용(result);
                Revision동기화(result.Revision);
            },
            "주문원장 서명 상태를 조회했습니다.",
            cancellationToken);
    }

    public async Task<bool> 서명준비Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var orderLedgerId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(서명준비초안.계약문서번호)
            || string.IsNullOrWhiteSpace(서명준비초안.문서Hash))
        {
            return 유효성실패("계약 문서번호와 문서 해시를 입력해 주세요.");
        }

        서명준비초안.기대Revision ??= 주문상태.현재서명Revision;
        return await 작업실행Async(
            async token =>
            {
                var result = await service.주문원장서명준비Async(orderLedgerId, 서명준비초안, token)
                    ?? throw new InvalidOperationException("주문원장 서명 준비 응답이 비어 있습니다.");
                주문상태.서명상태적용(result);
                서명등록초안.문서Hash = 서명준비초안.문서Hash;
                Revision동기화(result.Revision);
            },
            "주문원장 서명을 준비했습니다.",
            cancellationToken);
    }

    public async Task<bool> 서명등록Async(CancellationToken cancellationToken = default)
    {
        if (!주문원장선택확인(out var orderLedgerId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(서명등록초안.문서Hash)
            || string.IsNullOrWhiteSpace(서명등록초안.동의문Hash)
            || string.IsNullOrWhiteSpace(서명등록초안.서명증적Hash))
        {
            return 유효성실패("문서, 동의문과 서명 증적 해시를 모두 입력해 주세요.");
        }

        서명등록초안.기대Revision ??= 주문상태.현재서명Revision;
        return await 작업실행Async(
            async token =>
            {
                var result = await service.주문원장서명등록Async(orderLedgerId, 서명등록초안, token)
                    ?? throw new InvalidOperationException("주문원장 서명 등록 응답이 비어 있습니다.");
                주문상태.서명상태적용(result);
                Revision동기화(result.Revision);
            },
            "주문원장에 서명했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
    {
        OnPropertyChanged(nameof(서명준비초안));
        OnPropertyChanged(nameof(서명등록초안));
    }

    private void Revision동기화(long revision)
    {
        서명준비초안.기대Revision = revision;
        서명등록초안.기대Revision = revision;
        입력변경알림();
        OnPropertyChanged(nameof(전체서명완료));
    }
}

/// <summary>기본 주문 업무를 조회·하위 원장·서명 단위로 조립한 재사용 루트입니다.</summary>
public sealed class 주문ViewModel : 조립ViewModelBase, ICrudPageViewModel
{
    public 주문ViewModel(
        주문업무상태ViewModel 상태,
        주문조회ViewModel 조회,
        주문하위원장ViewModel 하위원장,
        주문서명ViewModel 서명,
        주문하위원장관계CrudViewModel 하위원장관계Crud,
        주문서명상태조회ViewModel 서명상태조회,
        주문서명준비ViewModel 서명준비,
        주문서명등록ViewModel 서명등록)
    {
        this.상태 = 하위ViewModel등록(상태, 수명소유: false);
        this.조회 = 하위ViewModel등록(조회, 수명소유: false);
        this.하위원장 = 하위ViewModel등록(하위원장);
        this.서명 = 하위ViewModel등록(서명);
        this.하위원장관계Crud = 하위ViewModel등록(하위원장관계Crud);
        this.서명상태조회 = 하위ViewModel등록(서명상태조회, 수명소유: false);
        this.서명준비 = 하위ViewModel등록(서명준비, 수명소유: false);
        this.서명등록 = 하위ViewModel등록(서명등록, 수명소유: false);
        Crud업무단위목록 = [하위원장관계Crud];
        세부업무목록 = [조회, .. 하위원장관계Crud.Crud업무목록, 서명상태조회, 서명준비, 서명등록];
    }

    public 주문업무상태ViewModel 상태 { get; }
    public 주문조회ViewModel 조회 { get; }
    public 주문하위원장ViewModel 하위원장 { get; }
    public 주문서명ViewModel 서명 { get; }
    public 주문하위원장관계CrudViewModel 하위원장관계Crud { get; }
    public IReadOnlyList<I업무단위CrudViewModel> Crud업무단위목록 { get; }
    public 주문하위원장조회ViewModel 하위원장조회 => 하위원장관계Crud.조회;
    public 주문하위원장연결ViewModel 하위원장연결 => 하위원장관계Crud.등록;
    public 주문하위원장수정ViewModel 하위원장수정 => 하위원장관계Crud.수정;
    public 주문하위원장분리ViewModel 하위원장분리 => 하위원장관계Crud.삭제;
    public 주문서명상태조회ViewModel 서명상태조회 { get; }
    public 주문서명준비ViewModel 서명준비 { get; }
    public 주문서명등록ViewModel 서명등록 { get; }
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }
    public bool 처리중 => 조회.처리중
                          || 하위원장.처리중
                          || 서명.처리중
                          || 세부업무목록.Any(item => item.처리중);
}
