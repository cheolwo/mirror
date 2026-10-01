using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Sales;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 판매채널 계정, 판매상품과 출품이 공유하는 화면 상태입니다.
/// 판매가 공동구매·일반판매·수출 중 어느 문맥에서 시작되더라도 같은 상태 계약을 사용합니다.
/// </summary>
public sealed class 판매업무상태ViewModel : ObservableObject
{
    public 판매업무상태ViewModel()
    {
    }

    public 판매업무상태ViewModel(IHongdal현재사용자Context 현재사용자Context)
    {
        this.현재사용자Context = 현재사용자Context;
    }

    public IHongdal현재사용자Context? 현재사용자Context { get; }
    public 현재사용자Snapshot 현재사용자
        => 현재사용자Context?.현재사용자 ?? 현재사용자Snapshot.익명;
    private IReadOnlyList<판매채널계정항목응답> _계정목록 = [];
    private IReadOnlyList<판매상품항목응답> _상품목록 = [];
    private IReadOnlyList<채널출품항목응답> _출품목록 = [];
    private 판매채널계정항목응답? _선택된계정;
    private 판매상품항목응답? _선택된상품;
    private 채널출품항목응답? _선택된출품;

    public IReadOnlyList<판매채널계정항목응답> 계정목록
    {
        get => _계정목록;
        private set => SetProperty(ref _계정목록, value);
    }

    public IReadOnlyList<판매상품항목응답> 상품목록
    {
        get => _상품목록;
        private set => SetProperty(ref _상품목록, value);
    }

    public IReadOnlyList<채널출품항목응답> 출품목록
    {
        get => _출품목록;
        private set => SetProperty(ref _출품목록, value);
    }

    public 판매채널계정항목응답? 선택된계정
    {
        get => _선택된계정;
        private set => SetProperty(ref _선택된계정, value);
    }

    public 판매상품항목응답? 선택된상품
    {
        get => _선택된상품;
        private set => SetProperty(ref _선택된상품, value);
    }

    public 채널출품항목응답? 선택된출품
    {
        get => _선택된출품;
        private set => SetProperty(ref _선택된출품, value);
    }

    public void 목록적용(
        IReadOnlyList<판매채널계정항목응답> accounts,
        IReadOnlyList<판매상품항목응답> products,
        IReadOnlyList<채널출품항목응답> listings)
    {
        계정목록 = accounts ?? [];
        상품목록 = products ?? [];
        출품목록 = listings ?? [];
        선택유효성동기화();
    }

    public void 계정목록적용(IReadOnlyList<판매채널계정항목응답> items)
    {
        계정목록 = items ?? [];
        if (선택된계정 is not null && 계정목록.All(item => item.Id != 선택된계정.Id))
        {
            선택된계정 = null;
        }
    }

    public void 상품목록적용(IReadOnlyList<판매상품항목응답> items)
    {
        상품목록 = items ?? [];
        if (선택된상품 is not null && 상품목록.All(item => item.Id != 선택된상품.Id))
        {
            선택된상품 = null;
        }
    }

    public void 출품목록적용(IReadOnlyList<채널출품항목응답> items)
    {
        출품목록 = items ?? [];
        if (선택된출품 is not null && 출품목록.All(item => item.Id != 선택된출품.Id))
        {
            선택된출품 = null;
        }
    }

    public void 계정저장적용(판매채널계정항목응답 item)
    {
        ArgumentNullException.ThrowIfNull(item);
        계정목록 = 교체또는추가(계정목록, item, value => value.Id);
        선택된계정 = item;
    }

    public void 상품저장적용(판매상품항목응답 item)
    {
        ArgumentNullException.ThrowIfNull(item);
        상품목록 = 교체또는추가(상품목록, item, value => value.Id);
        선택된상품 = item;
    }

    public void 출품저장적용(채널출품항목응답 item)
    {
        ArgumentNullException.ThrowIfNull(item);
        출품목록 = 교체또는추가(출품목록, item, value => value.Id);
        선택된출품 = item;
    }

    public void 계정삭제적용(long accountId)
    {
        계정목록 = 계정목록.Where(item => item.Id != accountId).ToArray();
        if (선택된계정?.Id == accountId)
        {
            선택된계정 = null;
        }
    }

    public void 상품삭제적용(long productId)
    {
        상품목록 = 상품목록.Where(item => item.Id != productId).ToArray();
        if (선택된상품?.Id == productId)
        {
            선택된상품 = null;
        }
    }

    public void 출품삭제적용(long listingId)
    {
        출품목록 = 출품목록.Where(item => item.Id != listingId).ToArray();
        if (선택된출품?.Id == listingId)
        {
            선택된출품 = null;
        }
    }

    public bool 계정선택(long id)
    {
        var item = 계정목록.FirstOrDefault(value => value.Id == id);
        if (item is null)
        {
            return false;
        }

        선택된계정 = item;
        return true;
    }

    public bool 상품선택(long id)
    {
        var item = 상품목록.FirstOrDefault(value => value.Id == id);
        if (item is null)
        {
            return false;
        }

        선택된상품 = item;
        return true;
    }

    public bool 출품선택(long id)
    {
        var item = 출품목록.FirstOrDefault(value => value.Id == id);
        if (item is null)
        {
            return false;
        }

        선택된출품 = item;
        return true;
    }

    private void 선택유효성동기화()
    {
        if (선택된계정 is not null)
        {
            선택된계정 = 계정목록.FirstOrDefault(item => item.Id == 선택된계정.Id);
        }

        if (선택된상품 is not null)
        {
            선택된상품 = 상품목록.FirstOrDefault(item => item.Id == 선택된상품.Id);
        }

        if (선택된출품 is not null)
        {
            선택된출품 = 출품목록.FirstOrDefault(item => item.Id == 선택된출품.Id);
        }
    }

    private static IReadOnlyList<T> 교체또는추가<T, TKey>(
        IReadOnlyList<T> source,
        T item,
        Func<T, TKey> keySelector)
        where TKey : notnull
    {
        var key = keySelector(item);
        var replaced = false;
        var result = source.Select(value =>
        {
            if (!EqualityComparer<TKey>.Default.Equals(keySelector(value), key))
            {
                return value;
            }

            replaced = true;
            return item;
        }).ToList();

        if (!replaced)
        {
            result.Add(item);
        }

        return result;
    }
}

/// <summary>판매의 세부 업무가 공유 상태를 사용하도록 하는 상위 계층입니다.</summary>
public abstract class 판매업무ViewModelBase : 업무작업ViewModelBase, IDisposable
{
    protected 판매업무ViewModelBase(판매업무상태ViewModel 상태)
    {
        판매상태 = 상태 ?? throw new ArgumentNullException(nameof(상태));
        현재사용자Context연결(판매상태.현재사용자Context);
        판매상태.PropertyChanged += 판매상태변경;
    }

    protected 판매업무상태ViewModel 판매상태 { get; }
    public 판매업무상태ViewModel 상태공유 => 판매상태;

    public void Dispose()
    {
        판매상태.PropertyChanged -= 판매상태변경;
        GC.SuppressFinalize(this);
    }

    private void 판매상태변경(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);
}

/// <summary>판매채널 계정 연결이라는 독립 업무 단위입니다.</summary>
public sealed class 판매채널계정ViewModel(
    I판매채널계정Service service,
    판매업무상태ViewModel 상태) : 판매업무ViewModelBase(상태)
{
    private IReadOnlyList<string> _지원채널목록 = [];
    private 판매채널계정저장요청 _초안 = new();

    public IReadOnlyList<string> 지원채널목록
    {
        get => _지원채널목록;
        private set
        {
            if (SetProperty(ref _지원채널목록, value))
            {
                OnPropertyChanged(nameof(계정목록));
            }
        }
    }

    public IReadOnlyList<판매채널계정항목응답> 계정목록
        => 지원채널목록.Count == 0
            ? 판매상태.계정목록
            : 판매상태.계정목록.Where(item => 지원채널목록.Contains(
                item.채널종류,
                StringComparer.OrdinalIgnoreCase)).ToArray();

    public 판매채널계정저장요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public void 지원채널설정(IEnumerable<string>? channelTypes)
    {
        지원채널목록 = channelTypes?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    public void 초안교체(판매채널계정저장요청 draft)
        => 초안 = draft ?? throw new ArgumentNullException(nameof(draft));

    public bool 선택(long accountId)
        => 판매상태.계정선택(accountId) || 유효성실패("판매채널 계정을 목록에서 선택해 주세요.");

    public Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => 판매상태.계정목록적용(await service.계정목록조회Async(token)),
            "판매채널 계정 목록을 조회했습니다.",
            cancellationToken);

    public async Task<bool> 생성Async(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(초안.채널종류) || string.IsNullOrWhiteSpace(초안.상점명))
        {
            return 유효성실패("판매채널 종류와 상점명을 입력해 주세요.");
        }

        if (지원채널목록.Count > 0
            && !지원채널목록.Contains(초안.채널종류, StringComparer.OrdinalIgnoreCase))
        {
            return 유효성실패("현재 판매 업무에서 지원하는 채널을 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await service.계정생성Async(초안, token)
                    ?? throw new InvalidOperationException("판매채널 계정 생성 응답이 비어 있습니다.");
                판매상태.계정저장적용(created);
                초안 = new 판매채널계정저장요청 { 채널종류 = created.채널종류 };
            },
            "판매채널 계정을 연결했습니다.",
            cancellationToken);
    }
}

/// <summary>입고상품을 판매상품으로 전환하는 상품 등록 업무 단위입니다.</summary>
public sealed class 상품등록ViewModel(
    I상품등록Service service,
    판매업무상태ViewModel 상태) : 판매업무ViewModelBase(상태)
{
    private 판매상품저장요청 _초안 = new();

    public IReadOnlyList<판매상품항목응답> 상품목록 => 판매상태.상품목록;

    public 판매상품저장요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public void 초안교체(판매상품저장요청 draft)
        => 초안 = draft ?? throw new ArgumentNullException(nameof(draft));

    public void 입고상품연결(long inboundProductId, string? productName = null, string? sku = null)
    {
        초안.입고상품Id = inboundProductId;
        if (!string.IsNullOrWhiteSpace(productName))
        {
            초안.대표상품명 = productName;
        }

        if (string.IsNullOrWhiteSpace(초안.판매SKU) && !string.IsNullOrWhiteSpace(sku))
        {
            초안.판매SKU = sku;
        }

        OnPropertyChanged(nameof(초안));
    }

    public bool 선택(long productId)
        => 판매상태.상품선택(productId) || 유효성실패("판매상품을 목록에서 선택해 주세요.");

    public Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => 판매상태.상품목록적용(await service.상품목록조회Async(token)),
            "판매상품 목록을 조회했습니다.",
            cancellationToken);

    public async Task<bool> 등록Async(CancellationToken cancellationToken = default)
    {
        if (초안.입고상품Id <= 0)
        {
            return 유효성실패("판매상품과 연결할 입고상품을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(초안.대표상품명)
            || string.IsNullOrWhiteSpace(초안.판매SKU)
            || 초안.판매가 <= 0)
        {
            return 유효성실패("대표상품명, 판매 SKU와 0원보다 큰 판매가를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await service.상품생성Async(초안, token)
                    ?? throw new InvalidOperationException("판매상품 생성 응답이 비어 있습니다.");
                판매상태.상품저장적용(created);
                초안 = new 판매상품저장요청
                {
                    입고상품Id = created.입고상품Id,
                    대표상품명 = created.대표상품명,
                    판매SKU = created.판매SKU,
                    판매가 = created.판매가
                };
            },
            "판매상품을 등록했습니다.",
            cancellationToken);
    }
}

/// <summary>판매상품과 판매채널 계정을 연결하는 출품 업무 단위입니다.</summary>
public sealed class 채널출품ViewModel(
    I채널출품Service service,
    판매업무상태ViewModel 상태) : 판매업무ViewModelBase(상태)
{
    private 채널출품저장요청 _초안 = new();

    public IReadOnlyList<채널출품항목응답> 출품목록 => 판매상태.출품목록;

    public 채널출품저장요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public void 초안교체(채널출품저장요청 draft)
        => 초안 = draft ?? throw new ArgumentNullException(nameof(draft));

    public bool 계정선택(long accountId)
    {
        if (!판매상태.계정선택(accountId))
        {
            return 유효성실패("출품할 판매채널 계정을 선택해 주세요.");
        }

        초안.판매채널계정Id = accountId;
        OnPropertyChanged(nameof(초안));
        return true;
    }

    public bool 상품선택(long productId)
    {
        if (!판매상태.상품선택(productId))
        {
            return 유효성실패("출품할 판매상품을 선택해 주세요.");
        }

        초안.판매상품Id = productId;
        OnPropertyChanged(nameof(초안));
        return true;
    }

    public Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => 판매상태.출품목록적용(await service.출품목록조회Async(token)),
            "채널 출품 목록을 조회했습니다.",
            cancellationToken);

    public async Task<bool> 생성Async(CancellationToken cancellationToken = default)
    {
        if (초안.판매상품Id <= 0 || 초안.판매채널계정Id <= 0)
        {
            return 유효성실패("출품할 판매상품과 판매채널 계정을 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await service.출품생성Async(초안, token)
                    ?? throw new InvalidOperationException("판매채널 출품 응답이 비어 있습니다.");
                판매상태.출품저장적용(created);
            },
            "판매채널 출품을 생성했습니다.",
            cancellationToken);
    }
}

/// <summary>
/// 기본 판매 흐름을 조립합니다. 다른 업무 영역은 이 ViewModel을 주입받고 필요한 특화 규칙만 추가합니다.
/// </summary>
public sealed class 판매ViewModel : 조립ViewModelBase, ICrudPageViewModel
{
    public 판매ViewModel(
        판매업무상태ViewModel 상태,
        판매채널계정ViewModel 계정,
        상품등록ViewModel 상품등록,
        채널출품ViewModel 출품,
        판매채널계정CrudViewModel 계정Crud,
        판매상품CrudViewModel 상품Crud,
        채널출품CrudViewModel 출품Crud)
    {
        this.상태 = 하위ViewModel등록(상태, 수명소유: false);
        this.계정 = 하위ViewModel등록(계정);
        this.상품등록 = 하위ViewModel등록(상품등록);
        this.출품 = 하위ViewModel등록(출품);
        this.계정Crud = 하위ViewModel등록(계정Crud);
        this.상품Crud = 하위ViewModel등록(상품Crud);
        this.출품Crud = 하위ViewModel등록(출품Crud);
        Crud업무단위목록 = [계정Crud, 상품Crud, 출품Crud];
        세부업무목록 = [.. 계정Crud.Crud업무목록, .. 상품Crud.Crud업무목록, .. 출품Crud.Crud업무목록];
    }

    public 판매업무상태ViewModel 상태 { get; }
    public 판매채널계정ViewModel 계정 { get; }
    public 상품등록ViewModel 상품등록 { get; }
    public 채널출품ViewModel 출품 { get; }
    public 판매채널계정CrudViewModel 계정Crud { get; }
    public 판매상품CrudViewModel 상품Crud { get; }
    public 채널출품CrudViewModel 출품Crud { get; }
    public IReadOnlyList<I업무단위CrudViewModel> Crud업무단위목록 { get; }
    public 판매채널계정조회ViewModel 계정조회 => 계정Crud.조회;
    public 판매채널계정등록ViewModel 계정등록 => 계정Crud.등록;
    public 판매채널계정수정ViewModel 계정수정 => 계정Crud.수정;
    public 판매채널계정삭제ViewModel 계정삭제 => 계정Crud.삭제;
    public 판매상품조회ViewModel 상품조회 => 상품Crud.조회;
    public 판매상품등록ViewModel 상품등록업무 => 상품Crud.등록;
    public 판매상품수정ViewModel 상품수정 => 상품Crud.수정;
    public 판매상품삭제ViewModel 상품삭제 => 상품Crud.삭제;
    public 채널출품조회ViewModel 출품조회 => 출품Crud.조회;
    public 채널출품등록ViewModel 출품등록 => 출품Crud.등록;
    public 채널출품수정ViewModel 출품수정 => 출품Crud.수정;
    public 채널출품삭제ViewModel 출품삭제 => 출품Crud.삭제;
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }
    public bool 처리중 => 계정.처리중
                          || 상품등록.처리중
                          || 출품.처리중
                          || 세부업무목록.Any(item => item.처리중);

    public void 지원채널설정(IEnumerable<string>? channelTypes)
        => 계정.지원채널설정(channelTypes);

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => await 계정조회.조회Async(cancellationToken)
           && await 상품조회.조회Async(cancellationToken)
           && await 출품조회.조회Async(cancellationToken);
}
