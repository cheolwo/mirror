using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Hr;
using Hongdal.Contracts.Common.VehicleLoading;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 상차관점항목ViewModel(
    string 상차작업Id,
    string 역할코드,
    string 제목,
    string 요약,
    string 상태,
    IReadOnlyList<역할관점표시값> 핵심정보,
    상차관점항목응답 원본);

public static class 상차역할관점카탈로그
{
    public const string 기능코드 = "vehicle-loading-list";

    public static 역할관점업무정의 주문자 { get; } = 정의(
        BaguaActorRoleCodes.Orderer,
        "내 주문 상차 진행",
        "내 상품이 어느 창고에서 어떤 차량에 실렸는가?",
        "주문자가 출고 화물의 상차 장소와 운송 진행을 확인하는 관점입니다.",
        ["내 주문", "상품·수량", "상차 장소", "상차 상태", "도착지"],
        [
            행동("loading-track", "상차 추적", "내 주문의 상차와 후속 운송 상태를 확인합니다."),
            행동("loading-cargo-check", "화물 확인", "실린 상품과 수량을 주문 내용과 대조합니다."),
            행동("loading-delivery-track", "배송 추적", "상차 완료 후 도착지까지의 운송을 확인합니다.")
        ],
        ["Orderer", "Buyer", "구매자", "일반 구매자"]);

    public static 역할관점업무정의 판매자 { get; } = 정의(
        BaguaActorRoleCodes.Seller,
        "판매 상품 상차",
        "내가 공급한 상품이 출고 창고에서 운송사에 제대로 인계되었는가?",
        "판매자가 출고 수량과 상차 인계 진행을 확인하는 관점입니다.",
        ["판매 주문", "상품·수량", "출고 창고", "기사 배정", "인계 상태"],
        [
            행동("loading-supply-check", "공급 화물 확인", "판매 주문의 상차 대상 수량을 확인합니다."),
            행동("loading-handoff", "운송 인계 확인", "창고에서 기사에게 화물이 인계되었는지 확인합니다."),
            행동("loading-discrepancy", "수량 차이 확인", "상차 수량과 판매 출고 수량의 차이를 확인합니다.")
        ],
        ["Seller", "Producer", "생산자", "공급자"]);

    public static 역할관점업무정의 창고관리자 { get; } = 정의(
        BaguaActorRoleCodes.WarehouseManager,
        "창고 상차 작업",
        "어떤 화물을 어느 기사에게 얼마나 인계해야 하는가?",
        "창고 관리자가 출고 화물, 차량 도착과 상차 인계를 관리하는 관점입니다.",
        ["출고 창고", "화물·수량", "운송 의뢰", "기사", "상차 상태"],
        [
            행동("loading-prepare", "상차 준비", "피킹·포장이 끝난 화물을 운송별로 준비합니다."),
            행동("loading-driver-check", "기사 확인", "도착한 기사와 운송 의뢰를 대조합니다."),
            행동("loading-handoff-proof", "인계 증빙", "수량과 상태를 확인하고 상차 인계 증빙을 남깁니다.")
        ],
        ["WarehouseManager", HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseDispatchOperator]);

    public static 역할관점업무정의 운송담당자 { get; } = 정의(
        BaguaActorRoleCodes.TransportOperator,
        "내 운송 상차 작업",
        "어디에서 무엇을 싣고 어떤 순서로 출발해야 하는가?",
        "운송 담당자가 상차지 도착과 상차 완료를 실행하는 핵심 현장 관점입니다.",
        ["운송 번호", "상차지", "화물·수량", "상차 상태", "하차지"],
        [
            행동("transport-pickup-arrival", "상차지 도착", "기존 기사 운송 API로 상차지 도착을 기록합니다."),
            행동("transport-pickup-complete", "상차 완료", "상차 인수증과 증빙을 남기고 운송을 시작합니다."),
            행동("transport-loading-exception", "상차 예외", "물건 없음·수량 차이·파손·담당자 부재를 보고합니다.")
        ],
        ["TransportOperator", "Driver", "기사", HrDetailedRoleCodes.ShippingAgencyOperator]);

    public static 역할관점업무정의 협동조합운영자 { get; } = 정의(
        BaguaActorRoleCodes.CooperativeCoordinator,
        "공동 원장별 상차",
        "공동 주문의 출고 물량이 약속한 운송에 빠짐없이 상차되었는가?",
        "공동 원장 참여자가 주문·출고·운송 사이의 상차 이행을 감사하는 관점입니다.",
        ["공동 원장", "주문", "출고 창고", "운송", "상차 완료"],
        [
            행동("community-loading-audit", "상차 이행 감사", "공동 원장의 출고 수량과 상차 기록을 대조합니다."),
            행동("community-loading-progress", "상차 진행 확인", "창고와 운송별 상차 대기·도착·완료를 확인합니다."),
            행동("community-loading-issue", "상차 쟁점 등록", "누락·지연·수량 차이를 공동 원장 쟁점으로 연결합니다.")
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
            new 업무역할관점좌표(BaguaBusinessCodes.Warehouse, BaguaBusinessCodes.Transport, 기능코드, role.RoleCode),
            role,
            title,
            question,
            description,
            information,
            actions,
            aliases,
            역할관점데이터연결상태.역할별조회연결됨,
            "출고예정과 운송 실행 투영을 결합하고 실제 주문·판매·창고·운송·공동원장 관계로 필터링합니다.");
    }

    private static 역할관점행동후보 행동(string key, string name, string description)
        => new(key, name, description);
}

/// <summary>상차 목록 선택을 후속 상차 상세·증빙 ViewModel과 공유합니다.</summary>
public sealed class 상차업무상태ViewModel : ObservableObject
{
    private 상차관점항목ViewModel? _선택된상차;

    public 상차관점항목ViewModel? 선택된상차
    {
        get => _선택된상차;
        private set
        {
            if (!SetProperty(ref _선택된상차, value))
            {
                return;
            }

            OnPropertyChanged(nameof(선택된상차작업Id));
            OnPropertyChanged(nameof(선택된출고예정Id));
            OnPropertyChanged(nameof(선택된운송원장Id));
            OnPropertyChanged(nameof(선택된운송의뢰Id));
        }
    }

    public string? 선택된상차작업Id => 선택된상차?.상차작업Id;
    public long? 선택된출고예정Id => 선택된상차?.원본.출고예정Id;
    public long? 선택된운송원장Id => 선택된상차?.원본.운송원장Id;
    public string? 선택된운송의뢰Id => 선택된상차?.원본.운송의뢰Id;

    public bool 상차선택(상차관점항목ViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.상차작업Id))
        {
            return false;
        }

        선택된상차 = item;
        return true;
    }

    public void 선택해제() => 선택된상차 = null;
}

public interface I상차역할관점ViewModel : I서버목록조회ViewModel<상차관점항목ViewModel>
{
    역할관점업무정의 관점정의 { get; }
    IReadOnlyList<상차관점항목ViewModel> 항목목록 { get; }
    bool 현재사용자관점 { get; }
}

public abstract class 상차역할관점ViewModelBase
    : 업무조각ViewModelBase, I상차역할관점ViewModel
{
    private readonly I상차관점Service _service;
    private 목록조회결과<상차관점항목ViewModel> _결과
        = 목록조회결과<상차관점항목ViewModel>.비어있음;
    private 목록조회요청? _최근요청;

    protected 상차역할관점ViewModelBase(
        역할관점업무정의 definition,
        I상차관점Service service,
        IHongdal현재사용자Context context)
        : base($"vehicle-loading-{definition.역할.RoleCode}", definition.화면제목, 업무조각유형.목록조회)
    {
        관점정의 = definition;
        _service = service;
        현재사용자Context연결(context);
    }

    public 역할관점업무정의 관점정의 { get; }
    public IReadOnlyList<상차관점항목ViewModel> 항목목록 => 결과.항목;
    public bool 현재사용자관점 => 관점정의.현재사용자관점(현재사용자);

    public 목록조회결과<상차관점항목ViewModel> 결과
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
                var ledgerId = 필터값(normalized, nameof(상차관점항목응답.공동원장Id));
                var response = await _service.목록조회Async(
                    서버관점코드(),
                    ledgerId,
                    서버요청(normalized),
                    token);
                var items = response.Items
                    .Where(item => string.Equals(item.관계코드, 서버관점코드(), StringComparison.OrdinalIgnoreCase))
                    .Select(투영)
                    .ToArray();
                결과 = new 목록조회결과<상차관점항목ViewModel>(items, response.TotalCount);
                OnPropertyChanged(nameof(항목목록));
            },
            $"{관점정의.역할.RoleName} 관점의 상차 목록을 조회했습니다.",
            cancellationToken);
    }

    protected abstract 상차관점항목ViewModel 투영(상차관점항목응답 item);

    protected 상차관점항목ViewModel 항목(
        상차관점항목응답 item,
        string title,
        string summary,
        params 역할관점표시값[] information)
        => new(item.상차작업Id, 관점정의.역할.RoleCode, title, summary, item.상차상태, information, item);

    protected static 역할관점표시값 정보(string key, string name, string value, bool emphasis = false)
        => new(key, name, value, emphasis);

    protected static string 값(string? value, string fallback = "미지정")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    protected static string 상품(상차관점항목응답 item)
        => 값(item.상품명, 값(item.SKU, $"출고 #{item.출고예정Id}"));

    protected static string 장소(string address, string detail)
        => string.Join(" ", new[] { address, detail }.Where(value => !string.IsNullOrWhiteSpace(value)));

    protected static string 상차시각(상차관점항목응답 item)
        => item.상차완료일시?.ToString("yyyy-MM-dd HH:mm") ?? "완료 전";

    private string 서버관점코드()
        => 관점정의.역할.RoleCode switch
        {
            BaguaActorRoleCodes.Orderer => 상차업무관점코드.주문자,
            BaguaActorRoleCodes.Seller => 상차업무관점코드.판매자,
            BaguaActorRoleCodes.WarehouseManager => 상차업무관점코드.창고관리자,
            BaguaActorRoleCodes.TransportOperator => 상차업무관점코드.운송담당자,
            BaguaActorRoleCodes.CooperativeCoordinator => 상차업무관점코드.공동원장,
            _ => throw new InvalidOperationException($"지원하지 않는 상차 역할입니다: {관점정의.역할.RoleCode}")
        };

    private static 상차관점목록조회요청 서버요청(목록조회요청 request)
    {
        var sort = request.정렬조건.FirstOrDefault();
        return new 상차관점목록조회요청
        {
            Page = request.페이지,
            PageSize = request.페이지크기,
            Search = request.검색어,
            Status = 필터값(request, nameof(상차관점항목응답.상차상태)),
            SortBy = sort?.필드,
            SortDescending = sort?.방향 != 목록정렬방향.오름차순,
            WarehouseId = long.TryParse(
                필터값(request, nameof(상차관점항목응답.출고창고Id)),
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

public sealed class 주문자상차ViewModel(I상차관점Service service, IHongdal현재사용자Context context)
    : 상차역할관점ViewModelBase(상차역할관점카탈로그.주문자, service, context)
{
    protected override 상차관점항목ViewModel 투영(상차관점항목응답 item)
        => 항목(
            item,
            $"{상품(item)} 상차 진행",
            $"{값(item.주문참조번호, "내 주문")} · {item.상차상태}",
            정보("order", "내 주문", 값(item.주문참조번호), true),
            정보("cargo", "상품·수량", $"{상품(item)} · {item.수량:N0}"),
            정보("pickup", "상차 장소", 장소(item.상차주소, item.상차상세주소)),
            정보("status", "상차 상태", item.상차상태, true),
            정보("destination", "도착지", 장소(item.하차주소, item.하차상세주소)));
}

public sealed class 판매자상차ViewModel(I상차관점Service service, IHongdal현재사용자Context context)
    : 상차역할관점ViewModelBase(상차역할관점카탈로그.판매자, service, context)
{
    protected override 상차관점항목ViewModel 투영(상차관점항목응답 item)
        => 항목(
            item,
            $"{상품(item)} 운송 인계",
            $"{item.수량:N0}개 · {item.상차상태}",
            정보("order", "판매 주문", 값(item.주문참조번호)),
            정보("cargo", "상품·수량", $"{상품(item)} · {item.수량:N0}", true),
            정보("warehouse", "출고 창고", 값(item.출고창고명, $"창고 #{item.출고창고Id}")),
            정보("driver", "배정 기사", 값(item.확정기사UserId, "배정 전")),
            정보("handoff", "인계 상태", item.상차상태));
}

public sealed class 창고관리자상차ViewModel(I상차관점Service service, IHongdal현재사용자Context context)
    : 상차역할관점ViewModelBase(상차역할관점카탈로그.창고관리자, service, context)
{
    protected override 상차관점항목ViewModel 투영(상차관점항목응답 item)
        => 항목(
            item,
            $"{상품(item)} · {item.수량:N0}개 상차",
            $"{값(item.출고창고명, $"창고 #{item.출고창고Id}")} · {item.상차상태}",
            정보("warehouse", "출고 창고", 값(item.출고창고명, $"창고 #{item.출고창고Id}"), true),
            정보("cargo", "화물·수량", $"{상품(item)} · {item.수량:N0}"),
            정보("transport", "운송 의뢰", item.운송의뢰Id),
            정보("driver", "기사", 값(item.확정기사UserId, "배정 전")),
            정보("status", "상차 상태", item.상차상태, true));
}

public sealed class 운송담당자상차ViewModel(I상차관점Service service, IHongdal현재사용자Context context)
    : 상차역할관점ViewModelBase(상차역할관점카탈로그.운송담당자, service, context)
{
    protected override 상차관점항목ViewModel 투영(상차관점항목응답 item)
        => 항목(
            item,
            $"{상품(item)} 상차",
            $"{장소(item.상차주소, item.상차상세주소)} · {item.상차상태}",
            정보("transport", "운송 번호", 값(item.운송번호, item.운송의뢰Id), true),
            정보("pickup", "상차지", 장소(item.상차주소, item.상차상세주소)),
            정보("cargo", "화물·수량", $"{상품(item)} · {item.수량:N0}"),
            정보("status", "상차 상태", $"{item.상차상태} · {item.운송상태}", true),
            정보("destination", "하차지", 장소(item.하차주소, item.하차상세주소)));
}

public sealed class 협동조합운영자상차ViewModel(I상차관점Service service, IHongdal현재사용자Context context)
    : 상차역할관점ViewModelBase(상차역할관점카탈로그.협동조합운영자, service, context)
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
                    nameof(상차관점항목응답.공동원장Id),
                    StringComparison.OrdinalIgnoreCase))
                .Append(new 목록필터조건(
                    nameof(상차관점항목응답.공동원장Id),
                    "Equal",
                    ledgerId.Trim()))
                .ToArray()
        }, cancellationToken);
    }

    protected override 상차관점항목ViewModel 투영(상차관점항목응답 item)
        => 항목(
            item,
            $"공동 원장 {값(item.공동원장Id)} 상차",
            $"{값(item.주문참조번호)} · {상품(item)} {item.수량:N0}",
            정보("ledger", "공동 원장", 값(item.공동원장Id), true),
            정보("order", "주문", 값(item.주문참조번호)),
            정보("warehouse", "출고 창고", 값(item.출고창고명, $"창고 #{item.출고창고Id}")),
            정보("transport", "운송", 값(item.운송번호, item.운송의뢰Id)),
            정보("completion", "상차 완료", item.상차완료여부 ? 상차시각(item) : item.상차상태, true));
}

public sealed class 상차PageViewModel : PageViewModelBase
{
    private static readonly 목록조회요청 DefaultRequest = new()
    {
        페이지 = 0,
        페이지크기 = 25,
        정렬조건 = [new 목록정렬조건(nameof(상차관점항목응답.수정시각Utc), 목록정렬방향.내림차순)]
    };

    private readonly 상차업무상태ViewModel _상태;
    private I상차역할관점ViewModel _현재관점;

    public 상차PageViewModel(
        주문자상차ViewModel 주문자,
        판매자상차ViewModel 판매자,
        창고관리자상차ViewModel 창고관리자,
        운송담당자상차ViewModel 운송담당자,
        협동조합운영자상차ViewModel 협동조합운영자,
        상차업무상태ViewModel 상태,
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

    public IReadOnlyList<I상차역할관점ViewModel> 역할관점목록 { get; }
    public 상차관점항목ViewModel? 선택된상차 => _상태.선택된상차;

    public I상차역할관점ViewModel 현재관점
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

    public bool 상차선택(상차관점항목ViewModel? item)
    {
        if (!_상태.상차선택(item))
        {
            return false;
        }

        OnPropertyChanged(nameof(선택된상차));
        return true;
    }

    protected override async Task 불러오기Async(bool 새로고침, CancellationToken cancellationToken)
    {
        var request = 현재관점.최근요청 ?? DefaultRequest;
        if (현재관점.관점정의.역할.RoleCode == BaguaActorRoleCodes.CooperativeCoordinator
            && request.필터조건.All(filter => !string.Equals(
                filter.필드,
                nameof(상차관점항목응답.공동원장Id),
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!await 현재관점.조회Async(request, cancellationToken))
        {
            throw new InvalidOperationException(현재관점.오류메시지 ?? "상차 목록을 조회하지 못했습니다.");
        }
    }
}
