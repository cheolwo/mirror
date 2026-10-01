using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Hr;
using Hongdal.Contracts.Common.VehicleLoading;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 하차관점항목ViewModel(
    string 하차작업Id,
    string 역할코드,
    string 제목,
    string 요약,
    string 상태,
    IReadOnlyList<역할관점표시값> 핵심정보,
    하차관점항목응답 원본);

public static class 하차역할관점카탈로그
{
    public const string 기능코드 = "vehicle-unloading-list";

    public static 역할관점업무정의 주문자 { get; } = 정의(
        BaguaActorRoleCodes.Orderer,
        "내 주문 하차·수령",
        "내 상품이 도착지에 도착했고 누구에게 인계되었는가?",
        "주문자가 최종 도착지 하차와 수령 완료를 확인하는 관점입니다.",
        ["내 주문", "상품·수량", "하차 장소", "하차 상태", "수령 방식"],
        [
            행동("unloading-track", "하차 추적", "내 주문의 하차지 도착과 인수 완료를 확인합니다."),
            행동("unloading-receipt", "수령 확인", "하차 화물과 주문 수량을 대조합니다."),
            행동("unloading-issue", "수령 문제", "누락·파손·오배송 문제를 기록합니다.")
        ],
        ["Orderer", "Buyer", "구매자", "일반 구매자"]);

    public static 역할관점업무정의 판매자 { get; } = 정의(
        BaguaActorRoleCodes.Seller,
        "판매 상품 하차",
        "판매한 상품이 약속한 도착지에 정상 인계되었는가?",
        "판매자가 배송 결과와 수령 완료를 확인하는 관점입니다.",
        ["판매 주문", "상품·수량", "도착지", "인수 완료", "배송 결과"],
        [
            행동("unloading-delivery-check", "배송 결과 확인", "판매 주문의 하차와 인수 상태를 확인합니다."),
            행동("unloading-quantity-check", "수량 대조", "판매 출고 수량과 하차 수량을 대조합니다."),
            행동("unloading-aftercare", "배송 후속 처리", "파손·반품·재배송 필요 여부를 확인합니다.")
        ],
        ["Seller", "Producer", "생산자", "공급자"]);

    public static 역할관점업무정의 창고관리자 { get; } = 정의(
        BaguaActorRoleCodes.WarehouseManager,
        "도착 창고 하차·입고",
        "어떤 차량의 어떤 화물을 내려서 입고 처리해야 하는가?",
        "도착 창고 관리자가 하차 화물을 후속 입고 요청과 연결하는 관점입니다.",
        ["도착 창고", "운송", "화물·수량", "하차 상태", "후속 입고"],
        [
            행동("unloading-prepare", "하차 준비", "도착 차량과 하차 공간을 준비합니다."),
            행동("unloading-inspect", "하차 검수", "수량·포장·파손 상태를 확인합니다."),
            행동("unloading-inbound-handoff", "입고 인계", "하차 완료 화물을 연결된 입고 요청으로 넘깁니다.")
        ],
        ["WarehouseManager", HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseInboundOperator]);

    public static 역할관점업무정의 운송담당자 { get; } = 정의(
        BaguaActorRoleCodes.TransportOperator,
        "내 운송 하차 작업",
        "어디에서 누구에게 무엇을 인계하고 운송을 완료해야 하는가?",
        "운송 담당자가 하차지 도착과 최종 인수 완료를 실행하는 현장 관점입니다.",
        ["운송 번호", "하차지", "화물·수량", "하차 상태", "인수 완료"],
        [
            행동("transport-dropoff-arrival", "하차지 도착", "기존 기사 운송 API로 하차지 도착을 기록합니다."),
            행동("transport-delivery-complete", "인수 완료", "하차 사진과 인수 증빙을 남기고 운송을 완료합니다."),
            행동("transport-unloading-exception", "하차 예외", "수령자 부재·파손·수량 차이·하차 불가를 보고합니다.")
        ],
        ["TransportOperator", "Driver", "기사", HrDetailedRoleCodes.ShippingAgencyOperator]);

    public static 역할관점업무정의 협동조합운영자 { get; } = 정의(
        BaguaActorRoleCodes.CooperativeCoordinator,
        "공동 원장별 하차",
        "공동 주문 화물이 합의한 도착지와 입고 경로로 모두 인계되었는가?",
        "공동 원장 참여자가 하차·수령·후속 입고 이행을 감사하는 관점입니다.",
        ["공동 원장", "주문", "도착지", "후속 입고", "하차 완료"],
        [
            행동("community-unloading-audit", "하차 이행 감사", "공동 출고 수량과 하차·수령 기록을 대조합니다."),
            행동("community-destination-check", "도착 경로 확인", "직송 또는 창고 입고 경로가 합의와 일치하는지 확인합니다."),
            행동("community-unloading-issue", "하차 쟁점 등록", "누락·지연·수령 거부를 공동 원장 쟁점으로 연결합니다.")
        ],
        ["CooperativeCoordinator", "협동조합 관리자", HrDetailedRoleCodes.OrdererGroupRepresentative]);

    public static IReadOnlyList<역할관점업무정의> 전체 { get; } =
        [주문자, 판매자, 창고관리자, 운송담당자, 협동조합운영자];

    private static 역할관점업무정의 정의(
        string roleCode,
        string title,
        string question,
        string description,
        IReadOnlyList<string> information,
        IReadOnlyList<역할관점행동후보> actions,
        IReadOnlyList<string> aliases)
    {
        var role = BaguaTransitionCatalog.FindRole(roleCode);
        return new 역할관점업무정의(
            new 업무역할관점좌표(BaguaBusinessCodes.Transport, BaguaBusinessCodes.Warehouse, 기능코드, role.RoleCode),
            role,
            title,
            question,
            description,
            information,
            actions,
            aliases,
            역할관점데이터연결상태.역할별조회연결됨,
            "출고 화물·운송 실행·후속 입고 요청을 결합하고 실제 수령·도착 창고 관계로 필터링합니다.");
    }

    private static 역할관점행동후보 행동(string key, string name, string description)
        => new(key, name, description);
}

/// <summary>선택한 하차의 출고·운송·후속 입고 식별자를 상세 및 증빙 ViewModel과 공유합니다.</summary>
public sealed class 하차업무상태ViewModel : ObservableObject
{
    private 하차관점항목ViewModel? _선택된하차;

    public 하차관점항목ViewModel? 선택된하차
    {
        get => _선택된하차;
        private set
        {
            if (!SetProperty(ref _선택된하차, value))
            {
                return;
            }

            OnPropertyChanged(nameof(선택된하차작업Id));
            OnPropertyChanged(nameof(선택된출고예정Id));
            OnPropertyChanged(nameof(선택된운송원장Id));
            OnPropertyChanged(nameof(선택된운송의뢰Id));
            OnPropertyChanged(nameof(선택된입고요청Id));
        }
    }

    public string? 선택된하차작업Id => 선택된하차?.하차작업Id;
    public long? 선택된출고예정Id => 선택된하차?.원본.출고예정Id;
    public long? 선택된운송원장Id => 선택된하차?.원본.운송원장Id;
    public string? 선택된운송의뢰Id => 선택된하차?.원본.운송의뢰Id;
    public long? 선택된입고요청Id => 선택된하차?.원본.입고요청Id;

    public bool 하차선택(하차관점항목ViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.하차작업Id))
        {
            return false;
        }

        선택된하차 = item;
        return true;
    }

    public void 선택해제() => 선택된하차 = null;
}

public interface I하차역할관점ViewModel : I서버목록조회ViewModel<하차관점항목ViewModel>
{
    역할관점업무정의 관점정의 { get; }
    IReadOnlyList<하차관점항목ViewModel> 항목목록 { get; }
    bool 현재사용자관점 { get; }
}

public abstract class 하차역할관점ViewModelBase
    : 업무조각ViewModelBase, I하차역할관점ViewModel
{
    private readonly I하차관점Service _service;
    private 목록조회결과<하차관점항목ViewModel> _결과
        = 목록조회결과<하차관점항목ViewModel>.비어있음;
    private 목록조회요청? _최근요청;

    protected 하차역할관점ViewModelBase(
        역할관점업무정의 definition,
        I하차관점Service service,
        IHongdal현재사용자Context context)
        : base($"vehicle-unloading-{definition.역할.RoleCode}", definition.화면제목, 업무조각유형.목록조회)
    {
        관점정의 = definition;
        _service = service;
        현재사용자Context연결(context);
    }

    public 역할관점업무정의 관점정의 { get; }
    public IReadOnlyList<하차관점항목ViewModel> 항목목록 => 결과.항목;
    public bool 현재사용자관점 => 관점정의.현재사용자관점(현재사용자);

    public 목록조회결과<하차관점항목ViewModel> 결과
    {
        get => _결과;
        private set => SetProperty(ref _결과, value);
    }

    public 목록조회요청? 최근요청
    {
        get => _최근요청;
        private set => SetProperty(ref _최근요청, value);
    }

    public Task<bool> 조회Async(목록조회요청 요청, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var normalized = 요청.정규화(200);
        최근요청 = normalized;
        return 작업실행Async(
            async token =>
            {
                var ledgerId = 필터값(normalized, nameof(하차관점항목응답.공동원장Id));
                var response = await _service.목록조회Async(
                    서버관점코드(),
                    ledgerId,
                    서버요청(normalized),
                    token);
                var items = response.Items
                    .Where(item => string.Equals(item.관계코드, 서버관점코드(), StringComparison.OrdinalIgnoreCase))
                    .Select(투영)
                    .ToArray();
                결과 = new 목록조회결과<하차관점항목ViewModel>(items, response.TotalCount);
                OnPropertyChanged(nameof(항목목록));
            },
            $"{관점정의.역할.RoleName} 관점의 하차 목록을 조회했습니다.",
            cancellationToken);
    }

    protected abstract 하차관점항목ViewModel 투영(하차관점항목응답 item);

    protected 하차관점항목ViewModel 항목(
        하차관점항목응답 item,
        string title,
        string summary,
        params 역할관점표시값[] information)
        => new(item.하차작업Id, 관점정의.역할.RoleCode, title, summary, item.하차상태, information, item);

    protected static 역할관점표시값 정보(string key, string name, string value, bool emphasis = false)
        => new(key, name, value, emphasis);

    protected static string 값(string? value, string fallback = "미지정")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    protected static string 상품(하차관점항목응답 item)
        => 값(item.상품명, 값(item.SKU, $"출고 #{item.출고예정Id}"));

    protected static string 장소(string address, string detail)
        => string.Join(" ", new[] { address, detail }.Where(value => !string.IsNullOrWhiteSpace(value)));

    protected static string 하차시각(하차관점항목응답 item)
        => item.하차완료일시?.ToString("yyyy-MM-dd HH:mm") ?? "완료 전";

    protected static string 수령경로(하차관점항목응답 item)
        => item.창고입고연결여부
            ? $"{값(item.도착창고명, $"창고 #{item.도착창고Id}")} 입고"
            : "최종 도착지 직접 수령";

    private string 서버관점코드()
        => 관점정의.역할.RoleCode switch
        {
            BaguaActorRoleCodes.Orderer => 하차업무관점코드.주문자,
            BaguaActorRoleCodes.Seller => 하차업무관점코드.판매자,
            BaguaActorRoleCodes.WarehouseManager => 하차업무관점코드.창고관리자,
            BaguaActorRoleCodes.TransportOperator => 하차업무관점코드.운송담당자,
            BaguaActorRoleCodes.CooperativeCoordinator => 하차업무관점코드.공동원장,
            _ => throw new InvalidOperationException($"지원하지 않는 하차 역할입니다: {관점정의.역할.RoleCode}")
        };

    private static 하차관점목록조회요청 서버요청(목록조회요청 request)
    {
        var sort = request.정렬조건.FirstOrDefault();
        return new 하차관점목록조회요청
        {
            Page = request.페이지,
            PageSize = request.페이지크기,
            Search = request.검색어,
            Status = 필터값(request, nameof(하차관점항목응답.하차상태)),
            SortBy = sort?.필드,
            SortDescending = sort?.방향 != 목록정렬방향.오름차순,
            WarehouseId = long.TryParse(
                필터값(request, nameof(하차관점항목응답.도착창고Id)),
                out var warehouseId)
                ? warehouseId
                : null
        };
    }

    private static string? 필터값(목록조회요청 request, string field)
        => request.필터조건.FirstOrDefault(filter => string.Equals(
            filter.필드,
            field,
            StringComparison.OrdinalIgnoreCase))?.값;
}

public sealed class 주문자하차ViewModel(I하차관점Service service, IHongdal현재사용자Context context)
    : 하차역할관점ViewModelBase(하차역할관점카탈로그.주문자, service, context)
{
    protected override 하차관점항목ViewModel 투영(하차관점항목응답 item)
        => 항목(
            item,
            $"{상품(item)} 하차·수령",
            $"{값(item.주문참조번호, "내 주문")} · {item.하차상태}",
            정보("order", "내 주문", 값(item.주문참조번호), true),
            정보("cargo", "상품·수량", $"{상품(item)} · {item.수량:N0}"),
            정보("dropoff", "하차 장소", 장소(item.하차주소, item.하차상세주소)),
            정보("status", "하차 상태", item.하차상태, true),
            정보("receipt", "수령 방식", 수령경로(item)));
}

public sealed class 판매자하차ViewModel(I하차관점Service service, IHongdal현재사용자Context context)
    : 하차역할관점ViewModelBase(하차역할관점카탈로그.판매자, service, context)
{
    protected override 하차관점항목ViewModel 투영(하차관점항목응답 item)
        => 항목(
            item,
            $"{상품(item)} 배송 결과",
            $"{item.수량:N0}개 · {item.하차상태}",
            정보("order", "판매 주문", 값(item.주문참조번호)),
            정보("cargo", "상품·수량", $"{상품(item)} · {item.수량:N0}", true),
            정보("destination", "도착지", 장소(item.하차주소, item.하차상세주소)),
            정보("receipt", "인수 완료", item.하차완료여부 ? 하차시각(item) : "완료 전"),
            정보("result", "배송 결과", item.하차상태));
}

public sealed class 창고관리자하차ViewModel(I하차관점Service service, IHongdal현재사용자Context context)
    : 하차역할관점ViewModelBase(하차역할관점카탈로그.창고관리자, service, context)
{
    protected override 하차관점항목ViewModel 투영(하차관점항목응답 item)
        => 항목(
            item,
            $"{상품(item)} · {item.수량:N0}개 하차",
            $"{수령경로(item)} · {item.하차상태}",
            정보("warehouse", "도착 창고", 값(item.도착창고명, item.도착창고Id is null ? "직송 도착지" : $"창고 #{item.도착창고Id}"), true),
            정보("transport", "운송", 값(item.운송번호, item.운송의뢰Id)),
            정보("cargo", "화물·수량", $"{상품(item)} · {item.수량:N0}"),
            정보("status", "하차 상태", item.하차상태, true),
            정보("inbound", "후속 입고", item.입고요청Id is long inboundId ? $"입고 요청 #{inboundId}" : "입고 연결 없음"));
}

public sealed class 운송담당자하차ViewModel(I하차관점Service service, IHongdal현재사용자Context context)
    : 하차역할관점ViewModelBase(하차역할관점카탈로그.운송담당자, service, context)
{
    protected override 하차관점항목ViewModel 투영(하차관점항목응답 item)
        => 항목(
            item,
            $"{상품(item)} 하차",
            $"{장소(item.하차주소, item.하차상세주소)} · {item.하차상태}",
            정보("transport", "운송 번호", 값(item.운송번호, item.운송의뢰Id), true),
            정보("dropoff", "하차지", 장소(item.하차주소, item.하차상세주소)),
            정보("cargo", "화물·수량", $"{상품(item)} · {item.수량:N0}"),
            정보("status", "하차 상태", $"{item.하차상태} · {item.운송상태}", true),
            정보("receipt", "인수 완료", item.하차완료여부 ? 하차시각(item) : "완료 전"));
}

public sealed class 협동조합운영자하차ViewModel(I하차관점Service service, IHongdal현재사용자Context context)
    : 하차역할관점ViewModelBase(하차역할관점카탈로그.협동조합운영자, service, context)
{
    public Task<bool> 원장별조회Async(
        string ledgerId,
        목록조회요청? request = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ledgerId))
        {
            return Task.FromResult(유효성실패("조회할 공동 원장을 선택해 주세요."));
        }

        var source = (request ?? new 목록조회요청()).정규화(200);
        return 조회Async(source with
        {
            필터조건 = source.필터조건
                .Where(filter => !string.Equals(
                    filter.필드,
                    nameof(하차관점항목응답.공동원장Id),
                    StringComparison.OrdinalIgnoreCase))
                .Append(new 목록필터조건(
                    nameof(하차관점항목응답.공동원장Id),
                    "Equal",
                    ledgerId.Trim()))
                .ToArray()
        }, cancellationToken);
    }

    protected override 하차관점항목ViewModel 투영(하차관점항목응답 item)
        => 항목(
            item,
            $"공동 원장 {값(item.공동원장Id)} 하차",
            $"{값(item.주문참조번호)} · {상품(item)} {item.수량:N0}",
            정보("ledger", "공동 원장", 값(item.공동원장Id), true),
            정보("order", "주문", 값(item.주문참조번호)),
            정보("destination", "도착지", $"{장소(item.하차주소, item.하차상세주소)} · {수령경로(item)}"),
            정보("inbound", "후속 입고", item.입고요청Id is long inboundId ? $"입고 요청 #{inboundId}" : "직접 수령"),
            정보("completion", "하차 완료", item.하차완료여부 ? 하차시각(item) : item.하차상태, true));
}

public sealed class 하차PageViewModel : PageViewModelBase
{
    private static readonly 목록조회요청 DefaultRequest = new()
    {
        페이지 = 0,
        페이지크기 = 25,
        정렬조건 = [new 목록정렬조건(nameof(하차관점항목응답.수정시각Utc), 목록정렬방향.내림차순)]
    };

    private readonly 하차업무상태ViewModel _상태;
    private I하차역할관점ViewModel _현재관점;

    public 하차PageViewModel(
        주문자하차ViewModel 주문자,
        판매자하차ViewModel 판매자,
        창고관리자하차ViewModel 창고관리자,
        운송담당자하차ViewModel 운송담당자,
        협동조합운영자하차ViewModel 협동조합운영자,
        하차업무상태ViewModel 상태,
        IHongdal현재사용자Context context)
    {
        _상태 = 상태;
        역할관점목록 =
        [
            하위ViewModel등록(주문자, 수명소유: false),
            하위ViewModel등록(판매자, 수명소유: false),
            하위ViewModel등록(창고관리자, 수명소유: false),
            하위ViewModel등록(운송담당자, 수명소유: false),
            하위ViewModel등록(협동조합운영자, 수명소유: false)
        ];
        _현재관점 = 역할관점목록.FirstOrDefault(item => item.관점정의.현재사용자관점(context.현재사용자))
                   ?? 주문자;
    }

    public IReadOnlyList<I하차역할관점ViewModel> 역할관점목록 { get; }
    public 하차관점항목ViewModel? 선택된하차 => _상태.선택된하차;

    public I하차역할관점ViewModel 현재관점
    {
        get => _현재관점;
        private set => SetProperty(ref _현재관점, value);
    }

    public bool 관점선택(string roleCode)
    {
        var perspective = 역할관점목록.FirstOrDefault(item => string.Equals(
            item.관점정의.역할.RoleCode,
            roleCode,
            StringComparison.OrdinalIgnoreCase));
        if (perspective is null)
        {
            return false;
        }

        현재관점 = perspective;
        return true;
    }

    public bool 하차선택(하차관점항목ViewModel? item)
    {
        if (!_상태.하차선택(item))
        {
            return false;
        }

        OnPropertyChanged(nameof(선택된하차));
        return true;
    }

    protected override async Task 불러오기Async(bool 새로고침, CancellationToken cancellationToken)
    {
        var request = 현재관점.최근요청 ?? DefaultRequest;
        if (현재관점.관점정의.역할.RoleCode == BaguaActorRoleCodes.CooperativeCoordinator
            && request.필터조건.All(filter => !string.Equals(
                filter.필드,
                nameof(하차관점항목응답.공동원장Id),
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!await 현재관점.조회Async(request, cancellationToken))
        {
            throw new InvalidOperationException(현재관점.오류메시지 ?? "하차 목록을 조회하지 못했습니다.");
        }
    }
}
