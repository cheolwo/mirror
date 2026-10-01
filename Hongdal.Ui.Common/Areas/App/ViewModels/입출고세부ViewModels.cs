using System.ComponentModel;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public abstract class 입출고업무조각ViewModelBase : 업무조각ViewModelBase, IDisposable
{
    protected 입출고업무조각ViewModelBase(
        I입출고작업Service service,
        입출고화면상태ViewModel 상태,
        string 업무코드,
        string 업무명,
        업무조각유형 업무유형)
        : base(업무코드, 업무명, 업무유형)
    {
        Service = service;
        화면상태 = 상태;
        현재사용자Context연결(상태.현재사용자Context);
        화면상태.PropertyChanged += 화면상태변경;
    }

    protected I입출고작업Service Service { get; }
    protected 입출고화면상태ViewModel 화면상태 { get; }

    public virtual void Dispose()
    {
        화면상태.PropertyChanged -= 화면상태변경;
        GC.SuppressFinalize(this);
    }

    protected virtual void 상태변경(PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
        => 상태변경(e);
}

/// <summary>입고 요청 목록만 조회·선택하여 표 또는 카드에 제공하는 화면 조각입니다.</summary>
public sealed class 입고조회ViewModel : 입출고업무조각ViewModelBase,
    I목록조회ViewModel<입고요청항목응답>,
    I서버목록조회ViewModel<입고요청항목응답>
{
    private readonly 입고원장ViewModel _원장;
    private 목록조회결과<입고요청항목응답> _결과 = 목록조회결과<입고요청항목응답>.비어있음;
    private 목록조회요청? _최근요청;

    public 입고조회ViewModel(
        I입출고작업Service service,
        입출고화면상태ViewModel 상태,
        입고원장ViewModel 원장)
        : base(service, 상태, "inbound-query", "입고 조회", 업무조각유형.목록조회)
    {
        _원장 = 원장;
        _원장.PropertyChanged += 원장변경;
    }

    public IReadOnlyList<입고요청항목응답> 항목목록 => 화면상태.선택창고입고요청목록;
    public 목록조회결과<입고요청항목응답> 결과
    {
        get => _결과;
        private set => SetProperty(ref _결과, value);
    }

    public 목록조회요청? 최근요청
    {
        get => _최근요청;
        private set => SetProperty(ref _최근요청, value);
    }

    public IReadOnlyList<입고요청항목응답> 원장연결항목목록
        => _원장.원장Id is { Length: > 0 } ledgerId
            ? 항목목록.Where(item => string.Equals(
                item.커뮤니티원장Id,
                ledgerId,
                StringComparison.OrdinalIgnoreCase)).ToArray()
            : [];
    public 입고요청항목응답? 선택된항목 => 화면상태.선택된입고요청;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token =>
            {
                var items = await Service.입고목록조회Async(token);
                화면상태.입고목록적용(items);
                결과 = new 목록조회결과<입고요청항목응답>(items, items.Count);
            },
            "입고 목록을 조회했습니다.",
            cancellationToken);

    public Task<bool> 조회Async(
        목록조회요청 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var normalized = 요청.정규화();
        최근요청 = normalized;
        var firstSort = normalized.정렬조건.FirstOrDefault();

        return 작업실행Async(
            async token =>
            {
                var response = await Service.입고목록조회Async(new 입고요청목록조회요청
                {
                    Page = normalized.페이지,
                    PageSize = normalized.페이지크기,
                    Search = normalized.검색어,
                    SortBy = firstSort?.필드,
                    SortDescending = firstSort?.방향 != 목록정렬방향.오름차순,
                    WarehouseId = 필터값<long?>(normalized, nameof(입고요청항목응답.창고Id))
                                  ?? 화면상태.선택된창고?.Id,
                    Status = 필터값<string>(normalized, nameof(입고요청항목응답.상태)),
                    FlowType = 필터값<string>(normalized, nameof(입고요청항목응답.입고흐름유형))
                }, token);
                var result = new 목록조회결과<입고요청항목응답>(response.Items, response.TotalCount);
                화면상태.입고목록적용(result.항목);
                결과 = result;
            },
            "입고 목록을 조회했습니다.",
            cancellationToken);
    }

    public bool 선택(long inboundId)
        => 화면상태.입고요청선택(inboundId)
           || 유효성실패("목록에 있는 입고 요청을 선택해 주세요.");

    public override void Dispose()
    {
        _원장.PropertyChanged -= 원장변경;
        base.Dispose();
    }

    private void 원장변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(원장연결항목목록));

    private static TValue? 필터값<TValue>(목록조회요청 request, string field)
    {
        var value = request.필터조건.FirstOrDefault(item =>
            string.Equals(item.필드, field, StringComparison.OrdinalIgnoreCase))?.값;
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        if (typeof(TValue) == typeof(string))
        {
            return (TValue)(object)value;
        }

        if (typeof(TValue) == typeof(long?) && long.TryParse(value, out var longValue))
        {
            return (TValue)(object)(long?)longValue;
        }

        return default;
    }
}

/// <summary>창고 유형과 무관하게 입고 예정 상태만 서버 페이징으로 조회하는 화면 조각입니다.</summary>
public sealed class 입고예정조회ViewModel : 업무조각ViewModelBase,
    I서버목록조회ViewModel<입고요청항목응답>
{
    private readonly 입고조회ViewModel _입고조회;
    private 목록조회결과<입고요청항목응답> _결과 = 목록조회결과<입고요청항목응답>.비어있음;
    private 목록조회요청? _최근요청;

    public 입고예정조회ViewModel(입고조회ViewModel 입고조회)
        : base("expected-inbound-query", "입고 예정 조회", 업무조각유형.목록조회)
    {
        _입고조회 = 입고조회;
    }

    public 목록조회결과<입고요청항목응답> 결과
    {
        get => _결과;
        private set => SetProperty(ref _결과, value);
    }

    public 목록조회요청? 최근요청
    {
        get => _최근요청;
        private set => SetProperty(ref _최근요청, value);
    }

    public bool 선택(long inboundId)
        => _입고조회.선택(inboundId);

    public Task<bool> 조회Async(
        목록조회요청 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var normalized = 요청.정규화();
        var expectedRequest = normalized with
        {
            필터조건 = normalized.필터조건
                .Where(filter => !string.Equals(
                    filter.필드,
                    nameof(입고요청항목응답.상태),
                    StringComparison.OrdinalIgnoreCase))
                .Append(new 목록필터조건(
                    nameof(입고요청항목응답.상태),
                    "Equal",
                    입고상태코드.예정))
                .ToArray()
        };
        최근요청 = expectedRequest;

        return 작업실행Async(
            async token =>
            {
                if (!await _입고조회.조회Async(expectedRequest, token))
                {
                    throw new InvalidOperationException(
                        _입고조회.오류메시지 ?? "입고 예정 목록을 조회하지 못했습니다.");
                }

                결과 = _입고조회.결과;
            },
            "입고 예정 목록을 조회했습니다.",
            cancellationToken);
    }
}

/// <summary>입고 요청 한 건을 등록하는 폼용 화면 조각입니다.</summary>
public sealed class 입고등록ViewModel : 입출고업무조각ViewModelBase, I등록ViewModel<입고요청저장요청>
{
    private 입고요청저장요청 _초안 = new();

    public 입고등록ViewModel(I입출고작업Service service, 입출고화면상태ViewModel 상태)
        : base(service, 상태, "inbound-create", "입고 등록", 업무조각유형.등록)
    {
    }

    public 입고요청저장요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        초안.창고Id = 화면상태.선택된창고?.Id ?? 초안.창고Id;
        초안.입고흐름유형 = 입고흐름유형코드.Normalize(초안.입고흐름유형);
        if (초안.창고Id <= 0)
        {
            return 유효성실패("입고할 창고를 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(초안.공급처명))
        {
            return 유효성실패("입고 공급처를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await Service.입고요청생성Async(초안, token)
                    ?? throw new InvalidOperationException("입고 요청 생성 응답이 비어 있습니다.");
                화면상태.입고요청저장적용(result);
                초안 = new 입고요청저장요청 { 창고Id = result.창고Id };
            },
            "입고 요청을 등록했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

/// <summary>선택된 입고 예정 요청의 기준정보를 정정하는 화면 조각입니다.</summary>
public sealed class 입고수정ViewModel : 입출고업무조각ViewModelBase, I수정ViewModel<입고요청저장요청>
{
    private 입고요청저장요청 _초안 = new();

    public 입고수정ViewModel(I입출고작업Service service, 입출고화면상태ViewModel 상태)
        : base(service, 상태, "inbound-update", "입고 수정", 업무조각유형.수정)
    {
    }

    public 입고요청저장요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public bool 선택항목적용()
    {
        var selected = 화면상태.선택된입고요청;
        if (selected is null)
        {
            return 유효성실패("수정할 입고 요청을 먼저 선택해 주세요.");
        }

        초안 = new 입고요청저장요청
        {
            창고Id = selected.창고Id,
            입고흐름유형 = selected.입고흐름유형,
            입고생성경로 = selected.입고생성경로,
            계약선행여부 = selected.계약선행여부,
            자동생성여부 = selected.자동생성여부,
            주문Id = selected.주문Id,
            주문참조번호 = selected.주문참조번호,
            판매자UserId = selected.판매자UserId,
            출고예정Id = selected.출고예정Id,
            운송의뢰Id = selected.운송의뢰Id,
            공급처코드 = selected.공급처코드,
            공급처명 = selected.공급처명,
            원주문참조번호 = selected.원주문참조번호,
            예정도착일 = selected.예정도착일,
            계약정보 = selected.계약정보
        };
        return true;
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var selected = 화면상태.선택된입고요청;
        if (selected is null)
        {
            return 유효성실패("수정할 입고 요청을 먼저 선택해 주세요.");
        }

        if (초안.창고Id <= 0 || string.IsNullOrWhiteSpace(초안.공급처명))
        {
            return 유효성실패("입고 창고와 공급처를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await Service.입고요청수정Async(selected.Id, 초안, token)
                    ?? throw new InvalidOperationException("입고 요청 수정 응답이 비어 있습니다.");
                화면상태.입고요청저장적용(result);
                선택항목적용();
            },
            "입고 요청을 수정했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

/// <summary>선택된 입고 예정 요청을 이력을 보존한 채 취소하는 화면 조각입니다.</summary>
public sealed class 입고삭제ViewModel : 입출고업무조각ViewModelBase, I삭제ViewModel<long>
{
    public 입고삭제ViewModel(I입출고작업Service service, 입출고화면상태ViewModel 상태)
        : base(service, 상태, "inbound-delete", "입고 취소", 업무조각유형.삭제)
    {
    }

    public long 초안 => 화면상태.선택된입고요청?.Id ?? 0;

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var inboundId = 초안;
        if (inboundId <= 0)
        {
            return 유효성실패("취소할 입고 요청을 먼저 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                await Service.입고요청취소Async(inboundId, token);
                화면상태.입고요청삭제적용(inboundId);
            },
            "입고 요청을 취소했습니다.",
            cancellationToken);
    }
}

public sealed class 입고CrudViewModel : 업무단위CrudViewModelBase<입고조회ViewModel, 입고등록ViewModel, 입고수정ViewModel, 입고삭제ViewModel>
{
    public 입고CrudViewModel(
        입고조회ViewModel 조회,
        입고등록ViewModel 등록,
        입고수정ViewModel 수정,
        입고삭제ViewModel 삭제,
        bool 하위수명소유 = true)
        : base("inbound", "입고", 조회, 등록, 수정, 삭제, 하위수명소유)
    {
    }
}

/// <summary>선택된 입고 요청의 실제 입고 수량을 확정하는 화면 조각입니다.</summary>
public sealed class 입고완료ViewModel : 입출고업무조각ViewModelBase, I명령ViewModel<입고완료요청>
{
    private 입고완료요청 _초안 = new();

    public 입고완료ViewModel(I입출고작업Service service, 입출고화면상태ViewModel 상태)
        : base(service, 상태, "inbound-complete", "입고 완료", 업무조각유형.처리)
    {
    }

    public 입고완료요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var inboundId = 화면상태.선택된입고요청?.Id;
        if (inboundId is null)
        {
            return 유효성실패("완료할 입고 요청을 먼저 선택해 주세요.");
        }

        if (초안.Items.Count == 0
            || 초안.Items.Any(item => string.IsNullOrWhiteSpace(item.상품명) || item.입고수량 <= 0))
        {
            return 유효성실패("상품명과 1개 이상의 입고 수량을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                화면상태.입고완료적용(await Service.입고완료Async(inboundId.Value, 초안, token));
                화면상태.재고목록적용(await Service.재고목록조회Async(token));
                초안 = new 입고완료요청();
            },
            "입고를 완료했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

/// <summary>입고·출고 화면에서 재고를 독립적으로 조회하는 화면 조각의 공통 계층입니다.</summary>
public abstract class 재고조회ViewModelBase(
    I입출고작업Service service,
    입출고화면상태ViewModel 상태,
    string 업무코드,
    string 업무명) : 입출고업무조각ViewModelBase(
        service,
        상태,
        업무코드,
        업무명,
        업무조각유형.목록조회), I목록조회ViewModel<재고항목응답>
{
    public IReadOnlyList<재고항목응답> 항목목록 => 화면상태.선택창고재고목록;
    public 재고항목응답? 선택된항목 => 화면상태.선택된재고;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => 화면상태.재고목록적용(await Service.재고목록조회Async(token)),
            $"{업무명} 대상을 조회했습니다.",
            cancellationToken);

    public bool 선택(long inboundProductId)
        => 화면상태.재고선택(inboundProductId)
           || 유효성실패("목록에 있는 재고를 선택해 주세요.");
}

public sealed class 입고재고조회ViewModel(
    I입출고작업Service service,
    입출고화면상태ViewModel 상태)
    : 재고조회ViewModelBase(service, 상태, "inbound-inventory-query", "입고 재고 조회");

public sealed class 출고재고조회ViewModel(
    I입출고작업Service service,
    입출고화면상태ViewModel 상태)
    : 재고조회ViewModelBase(service, 상태, "outbound-inventory-query", "출고 재고 조회");

/// <summary>선택된 입고 재고를 검수하는 폼용 화면 조각입니다.</summary>
public sealed class 입고검수ViewModel : 입출고업무조각ViewModelBase, I명령ViewModel<입고검수요청>
{
    private 입고검수요청 _초안 = new();

    public 입고검수ViewModel(I입출고작업Service service, 입출고화면상태ViewModel 상태)
        : base(service, 상태, "inbound-inspection", "입고 검수", 업무조각유형.처리)
    {
    }

    public 입고검수요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var inboundProductId = 화면상태.선택된재고?.입고상품Id;
        if (inboundProductId is null)
        {
            return 유효성실패("검수할 입고 재고를 먼저 선택해 주세요.");
        }

        if (초안.검수수량 <= 0 || 초안.불량수량 < 0 || 초안.불량수량 > 초안.검수수량)
        {
            return 유효성실패("검수 수량과 그 이하의 불량 수량을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await Service.입고검수Async(inboundProductId.Value, 초안, token)
                    ?? throw new InvalidOperationException("입고 검수 응답이 비어 있습니다.");
                화면상태.입고작업결과적용(result);
                초안 = new 입고검수요청();
            },
            "입고 재고를 검수했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

/// <summary>선택된 입고 재고의 보관 위치를 지정하는 화면 조각입니다.</summary>
public sealed class 입고적재ViewModel : 입출고업무조각ViewModelBase, I명령ViewModel<적재위치배정요청>
{
    private 적재위치배정요청 _초안 = new();

    public 입고적재ViewModel(I입출고작업Service service, 입출고화면상태ViewModel 상태)
        : base(service, 상태, "inbound-put-away", "입고 적재", 업무조각유형.처리)
    {
    }

    public 적재위치배정요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var inboundProductId = 화면상태.선택된재고?.입고상품Id;
        if (inboundProductId is null)
        {
            return 유효성실패("적재할 입고 재고를 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(초안.보관위치))
        {
            return 유효성실패("적재할 보관 위치를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await Service.적재위치배정Async(inboundProductId.Value, 초안, token)
                    ?? throw new InvalidOperationException("적재 위치 배정 응답이 비어 있습니다.");
                화면상태.입고작업결과적용(result);
                초안 = new 적재위치배정요청();
            },
            "입고 재고의 적재 위치를 지정했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

/// <summary>선택된 출고 재고를 포장하는 화면 조각입니다.</summary>
public sealed class 출고포장ViewModel : 입출고업무조각ViewModelBase, I명령ViewModel<포장작업요청>
{
    private 포장작업요청 _초안 = new();

    public 출고포장ViewModel(I입출고작업Service service, 입출고화면상태ViewModel 상태)
        : base(service, 상태, "outbound-pack", "출고 포장", 업무조각유형.처리)
    {
    }

    public 포장작업요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var inventory = 화면상태.선택된재고;
        if (inventory is null)
        {
            return 유효성실패("포장할 출고 재고를 먼저 선택해 주세요.");
        }

        if (초안.포장수량 <= 0 || 초안.포장수량 > inventory.가용수량)
        {
            return 유효성실패("가용 수량 이하의 포장 수량을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await Service.포장작업Async(inventory.입고상품Id, 초안, token)
                    ?? throw new InvalidOperationException("출고 포장 응답이 비어 있습니다.");
                화면상태.출고작업결과적용(result);
                초안 = new 포장작업요청();
            },
            "출고 재고를 포장했습니다.",
            cancellationToken);
    }

    public void 초안적용(포장작업요청 request) => 초안 = request;
    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

/// <summary>선택된 출고 재고를 운송 업무에 인계하는 화면 조각입니다.</summary>
public sealed class 출고운송인계ViewModel : 입출고업무조각ViewModelBase, I명령ViewModel<재고운송의뢰생성요청>
{
    private 재고운송의뢰생성요청 _초안 = new();

    public 출고운송인계ViewModel(I입출고작업Service service, 입출고화면상태ViewModel 상태)
        : base(service, 상태, "outbound-transport-handoff", "출고 운송 인계", 업무조각유형.처리)
    {
    }

    public 재고운송의뢰생성요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var inventory = 화면상태.선택된재고;
        if (inventory is null)
        {
            return 유효성실패("운송에 인계할 출고 재고를 먼저 선택해 주세요.");
        }

        초안.입고상품Id = inventory.입고상품Id;
        if (초안.요청수량 <= 0
            || 초안.요청수량 > inventory.가용수량
            || string.IsNullOrWhiteSpace(초안.하차지주소))
        {
            return 유효성실패("가용 수량 이하의 요청 수량과 하차지 주소를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await Service.운송인계Async(초안, token)
                    ?? throw new InvalidOperationException("출고 운송 인계 응답이 비어 있습니다.");
                화면상태.운송의뢰적용(result);
                초안 = new 재고운송의뢰생성요청();
            },
            "출고 재고를 운송 업무에 인계했습니다.",
            cancellationToken);
    }

    public void 초안적용(재고운송의뢰생성요청 request) => 초안 = request;
    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}
