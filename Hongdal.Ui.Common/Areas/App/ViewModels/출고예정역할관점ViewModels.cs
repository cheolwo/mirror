using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Hr;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 출고예정관점항목ViewModel(
    long 출고예정Id,
    string 역할코드,
    string 제목,
    string 요약,
    DateTime? 예정출고일,
    string 상태,
    IReadOnlyList<역할관점표시값> 핵심정보,
    출고예정항목응답 원본);

public static class 출고예정역할관점카탈로그
{
    public const string 기능코드 = "expected-outbound";

    public static 역할관점업무정의 주문자 { get; } = 정의(
        BaguaActorRoleCodes.Orderer,
        "내 주문 출고 예정",
        "내 주문은 어느 창고에서 언제 출고되고 어디로 전달되는가?",
        "주문자가 주문 상품의 출고 준비, 운송 연결과 도착 약속을 확인하는 관점입니다.",
        ["내 주문", "출고 상품", "출고 창고", "운송 의뢰", "예정 출고·도착"],
        [
            행동("order-detail", "내 주문 확인", "출고 예정과 연결된 개별 주문·공동 주문을 확인합니다."),
            행동("outbound-tracking", "출고·배송 추적", "포장, 운송 인계와 도착 진행을 확인합니다."),
            행동("outbound-issue", "출고 이의·문의", "수량·상품·일정 차이에 관한 문의를 시작합니다.")
        ],
        ["Orderer", "Buyer", "구매자", "일반 구매자"],
        역할관점데이터연결상태.역할별조회연결됨,
        "주문자 ID 관계로 필터링하는 출고 예정 읽기 API와 연결됩니다.");

    public static 역할관점업무정의 판매자 { get; } = 정의(
        BaguaActorRoleCodes.Seller,
        "내 판매 출고 예정",
        "판매가 확정된 상품을 어느 창고에서 얼마나 출고해야 하는가?",
        "판매자가 주문별 출고 수량, 창고, 포장과 운송 인계를 확인하는 관점입니다.",
        ["판매 주문", "상품·SKU", "출고 수량", "출고 창고", "운송 인계"],
        [
            행동("sales-order-detail", "판매 주문 확인", "출고를 만든 판매 주문과 약속을 확인합니다."),
            행동("outbound-release", "출고 승인", "창고가 피킹·포장을 시작할 수 있도록 출고 조건을 확인합니다."),
            행동("outbound-delay", "지연 알림", "재고·포장·운송 지연을 주문 관계자에게 알립니다.")
        ],
        ["Seller", "Producer", "생산자", "공급자"],
        역할관점데이터연결상태.역할별조회연결됨,
        "판매자 ID 관계로 필터링하는 출고 예정 읽기 API와 연결됩니다.");

    public static 역할관점업무정의 창고관리자 { get; } = 정의(
        BaguaActorRoleCodes.WarehouseManager,
        "창고 출고 예정 작업",
        "무엇을 얼마나 피킹·포장하고 어느 운송 담당자에게 인계해야 하는가?",
        "창고 관리자와 출고 담당자가 피킹, 포장, 상차와 운송 인계를 준비하는 관점입니다.",
        ["출고 창고", "상품·SKU", "출고 수량", "출고 묶음", "운송 의뢰", "상차 일정"],
        [
            행동("outbound-pick", "피킹", "주문 수량을 재고에서 피킹합니다."),
            행동("outbound-pack", "포장", "피킹 상품을 출고 단위로 포장합니다."),
            행동("outbound-transport-handoff", "운송 인계", "포장 화물을 기사 또는 운송사에 인계합니다.")
        ],
        [
            "WarehouseManager",
            HrDetailedRoleCodes.WarehouseManager,
            HrDetailedRoleCodes.WarehouseDispatchOperator,
            "창고 출고 담당자"
        ],
        역할관점데이터연결상태.공통조회연결됨,
        "창고 소유자·창고 사용자 관계로 필터링하는 출고 예정 읽기 API와 연결됩니다.");

    public static 역할관점업무정의 운송담당자 { get; } = 정의(
        BaguaActorRoleCodes.TransportOperator,
        "상차할 출고 예정",
        "어느 창고에서 어떤 화물을 언제 인수해 운송해야 하는가?",
        "운송 담당자가 상차 창고, 화물, 픽업 시각과 인계 상태를 확인하는 관점입니다.",
        ["운송 의뢰", "상차 창고", "화물·수량", "예정 출고일", "예정 도착일"],
        [
            행동("transport-detail", "운송 의뢰 확인", "배차, 경로와 상하차 약속을 확인합니다."),
            행동("outbound-pickup", "상차 인수", "창고에서 화물을 인수하고 증빙을 남깁니다."),
            행동("transport-exception", "운송 예외 보고", "상차 지연·파손·수량 차이를 기록합니다.")
        ],
        ["TransportOperator", "Driver", "기사", HrDetailedRoleCodes.ShippingAgencyOperator],
        역할관점데이터연결상태.역할별조회연결됨,
        "운송 원장의 화주·추천 기사·확정 기사 관계로 필터링하는 읽기 API와 연결됩니다.");

    public static 역할관점업무정의 협동조합운영자 { get; } = 정의(
        BaguaActorRoleCodes.CooperativeCoordinator,
        "공동 원장 출고 예정",
        "공동 주문의 배분·출고·운송 인계가 원장 합의대로 진행되는가?",
        "협동조합 운영자가 공동 원장별 출고 배분과 이행 상태를 감사하는 관점입니다.",
        ["공동 원장", "주문·판매 연결", "배분 수량", "출고 창고", "운송 이행"],
        [
            행동("community-ledger-detail", "공동 원장 확인", "출고 배분의 근거가 된 합의와 개별 주문을 확인합니다."),
            행동("community-outbound-issue", "출고 쟁점 등록", "배분·포장·상차 차이를 원장 쟁점으로 기록합니다."),
            행동("community-outbound-audit", "출고 이행 감사", "원장 합의와 실제 출고·운송 기록을 대조합니다.")
        ],
        [
            "CooperativeCoordinator",
            "협동조합 관리자",
            HrDetailedRoleCodes.OrdererGroupRepresentative,
            HrDetailedRoleCodes.OrdererGroupDistributionWorker
        ],
        역할관점데이터연결상태.역할별조회연결됨,
        "선택한 공동 원장의 생성자·참여자 관계를 검사하는 원장별 읽기 API와 연결됩니다.");

    public static IReadOnlyList<역할관점업무정의> 전체 { get; } =
        [주문자, 판매자, 창고관리자, 운송담당자, 협동조합운영자];

    private static 역할관점업무정의 정의(
        string 역할코드,
        string 화면제목,
        string 핵심질문,
        string 설명,
        IReadOnlyList<string> 핵심정보,
        IReadOnlyList<역할관점행동후보> 행동후보,
        IReadOnlyList<string> 역할별칭,
        역할관점데이터연결상태 데이터연결상태,
        string 데이터연결안내)
    {
        var 역할 = BaguaTransitionCatalog.FindRole(역할코드);
        return new 역할관점업무정의(
            new 업무역할관점좌표(
                BaguaBusinessCodes.Warehouse,
                BaguaBusinessCodes.Warehouse,
                기능코드,
                역할.RoleCode),
            역할,
            화면제목,
            핵심질문,
            설명,
            핵심정보,
            행동후보,
            역할별칭,
            데이터연결상태,
            데이터연결안내);
    }

    private static 역할관점행동후보 행동(string 기능Key, string 이름, string 설명)
        => new(기능Key, 이름, 설명);
}

public sealed class 출고예정조회ViewModel : 업무조각ViewModelBase,
    I서버목록조회ViewModel<출고예정항목응답>
{
    private readonly I입출고작업Service _service;
    private readonly 입출고화면상태ViewModel _화면상태;
    private 목록조회결과<출고예정항목응답> _결과 = 목록조회결과<출고예정항목응답>.비어있음;
    private 목록조회요청? _최근요청;

    public 출고예정조회ViewModel(
        I입출고작업Service service,
        입출고화면상태ViewModel 화면상태)
        : base("expected-outbound-query", "출고 예정 조회", 업무조각유형.목록조회)
    {
        _service = service;
        _화면상태 = 화면상태;
        현재사용자Context연결(화면상태.현재사용자Context);
    }

    public 목록조회결과<출고예정항목응답> 결과
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
        var normalized = 요청.정규화();
        최근요청 = normalized;
        return 작업실행Async(
            async token =>
            {
                var response = await _service.출고예정관점목록조회Async(
                    창고업무관점코드.창고관리자,
                    null,
                    서버요청(normalized, _화면상태.선택된창고?.Id),
                    token);
                결과 = new 목록조회결과<출고예정항목응답>(response.Items, response.TotalCount);
            },
            "창고 출고 예정 목록을 조회했습니다.",
            cancellationToken);
    }

    internal static 출고예정목록조회요청 서버요청(목록조회요청 request, long? defaultWarehouseId = null)
    {
        var firstSort = request.정렬조건.FirstOrDefault();
        return new 출고예정목록조회요청
        {
            Page = request.페이지,
            PageSize = request.페이지크기,
            Search = request.검색어,
            SortBy = firstSort?.필드,
            SortDescending = firstSort?.방향 != 목록정렬방향.오름차순,
            WarehouseId = long.TryParse(
                필터값(request, nameof(출고예정항목응답.출고창고Id)),
                out var warehouseId)
                ? warehouseId
                : defaultWarehouseId
        };
    }

    internal static string? 필터값(목록조회요청 request, string field)
        => request.필터조건.FirstOrDefault(item => string.Equals(
            item.필드,
            field,
            StringComparison.OrdinalIgnoreCase))?.값;
}

public interface I출고예정역할관점ViewModel : I서버목록조회ViewModel<출고예정관점항목ViewModel>
{
    역할관점업무정의 관점정의 { get; }
    출고예정조회ViewModel 공통조회 { get; }
    IReadOnlyList<출고예정관점항목ViewModel> 항목목록 { get; }
    int 원본전체건수 { get; }
    bool 현재사용자관점 { get; }
    void 공통결과투영(목록조회요청? 요청 = null);
}

public abstract class 출고예정역할관점ViewModelBase
    : 업무조각ViewModelBase, I출고예정역할관점ViewModel
{
    private readonly I입출고작업Service _service;
    private 목록조회결과<출고예정관점항목ViewModel> _결과
        = 목록조회결과<출고예정관점항목ViewModel>.비어있음;
    private 목록조회요청? _최근요청;
    private int _원본전체건수;

    protected 출고예정역할관점ViewModelBase(
        역할관점업무정의 관점정의,
        출고예정조회ViewModel 공통조회,
        I입출고작업Service service,
        IHongdal현재사용자Context 현재사용자Context)
        : base($"expected-outbound-{관점정의.역할.RoleCode}", 관점정의.화면제목, 업무조각유형.목록조회)
    {
        this.관점정의 = 관점정의;
        this.공통조회 = 공통조회;
        _service = service;
        현재사용자Context연결(현재사용자Context);
    }

    public 역할관점업무정의 관점정의 { get; }
    public 출고예정조회ViewModel 공통조회 { get; }
    public IReadOnlyList<출고예정관점항목ViewModel> 항목목록 => 결과.항목;

    public 목록조회결과<출고예정관점항목ViewModel> 결과
    {
        get => _결과;
        private set => SetProperty(ref _결과, value);
    }

    public 목록조회요청? 최근요청
    {
        get => _최근요청;
        private set => SetProperty(ref _최근요청, value);
    }

    public int 원본전체건수
    {
        get => _원본전체건수;
        private set => SetProperty(ref _원본전체건수, value);
    }

    public bool 현재사용자관점 => 관점정의.현재사용자관점(현재사용자);

    public Task<bool> 조회Async(목록조회요청 요청, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var normalized = 요청.정규화();
        최근요청 = normalized;
        return 작업실행Async(
            async token =>
            {
                if (관점정의.데이터연결상태 == 역할관점데이터연결상태.공통조회연결됨)
                {
                    if (!await 공통조회.조회Async(normalized, token))
                    {
                        throw new InvalidOperationException(공통조회.오류메시지 ?? "출고 예정 목록을 조회하지 못했습니다.");
                    }

                    공통결과투영(공통조회.최근요청 ?? normalized);
                    return;
                }

                var ledgerId = 출고예정조회ViewModel.필터값(
                    normalized,
                    nameof(출고예정항목응답.커뮤니티원장Id));
                var response = await _service.출고예정관점목록조회Async(
                    서버관점코드(),
                    ledgerId,
                    출고예정조회ViewModel.서버요청(normalized),
                    token);
                결과투영(response.Items, response.TotalCount, normalized, 서버전체건수확정: true);
            },
            $"{관점정의.역할.RoleName} 관점의 출고 예정 목록을 조회했습니다.",
            cancellationToken);
    }

    public void 공통결과투영(목록조회요청? 요청 = null)
        => 결과투영(
            공통조회.결과.항목,
            공통조회.결과.전체건수,
            요청 ?? 공통조회.최근요청,
            서버전체건수확정: 관점정의.데이터연결상태 == 역할관점데이터연결상태.공통조회연결됨);

    private void 결과투영(
        IReadOnlyList<출고예정항목응답> source,
        int sourceTotalCount,
        목록조회요청? request,
        bool 서버전체건수확정)
    {
        var items = source
            .Where(item => 포함(item, 현재사용자))
            .Select(item => 투영(item, 현재사용자))
            .ToArray();
        최근요청 = request;
        원본전체건수 = sourceTotalCount;
        결과 = new 목록조회결과<출고예정관점항목ViewModel>(
            items,
            서버전체건수확정 ? sourceTotalCount : items.Length);
        OnPropertyChanged(nameof(항목목록));
    }

    protected abstract bool 포함(출고예정항목응답 item, 현재사용자Snapshot 현재사용자);
    protected abstract 출고예정관점항목ViewModel 투영(출고예정항목응답 item, 현재사용자Snapshot 현재사용자);

    protected 출고예정관점항목ViewModel 항목(
        출고예정항목응답 item,
        string 제목,
        string 요약,
        params 역할관점표시값[] 핵심정보)
        => new(item.Id, 관점정의.역할.RoleCode, 제목, 요약, item.예정출고일, item.상태, 핵심정보, item);

    protected static string 값(string? value, string fallback = "미지정")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    protected static string 주문번호(출고예정항목응답 item)
        => 값(item.주문참조번호, item.주문Id is null ? "연결 주문 없음" : $"주문 #{item.주문Id}");

    protected static string 상품(출고예정항목응답 item)
        => 값(item.상품명, 값(item.SKU, $"출고 #{item.Id}"));

    protected static string 일자(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm") ?? "일정 미정";
    protected static 역할관점표시값 정보(string key, string name, string value, bool emphasis = false)
        => new(key, name, value, emphasis);

    private string 서버관점코드()
        => 관점정의.역할.RoleCode switch
        {
            BaguaActorRoleCodes.Orderer => 창고업무관점코드.주문자,
            BaguaActorRoleCodes.Seller => 창고업무관점코드.판매자,
            BaguaActorRoleCodes.WarehouseManager => 창고업무관점코드.창고관리자,
            BaguaActorRoleCodes.TransportOperator => 창고업무관점코드.운송담당자,
            BaguaActorRoleCodes.CooperativeCoordinator => 창고업무관점코드.공동원장,
            _ => throw new InvalidOperationException($"지원하지 않는 출고 예정 역할입니다: {관점정의.역할.RoleCode}")
        };
}

public sealed class 주문자출고예정ViewModel(
    출고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context context)
    : 출고예정역할관점ViewModelBase(출고예정역할관점카탈로그.주문자, 공통조회, service, context)
{
    protected override bool 포함(출고예정항목응답 item, 현재사용자Snapshot user)
        => user.UserId is { Length: > 0 } userId
           && string.Equals(item.주문자UserId, userId, StringComparison.OrdinalIgnoreCase);

    protected override 출고예정관점항목ViewModel 투영(출고예정항목응답 item, 현재사용자Snapshot user)
        => 항목(
            item,
            $"{상품(item)} 출고 예정",
            $"{주문번호(item)} · {값(item.출고창고명, $"창고 #{item.출고창고Id}")}",
            정보("order", "내 주문", 주문번호(item)),
            정보("product", "출고 상품", $"{상품(item)} · {item.수량:N0}"),
            정보("warehouse", "출고 창고", 값(item.출고창고명, $"창고 #{item.출고창고Id}")),
            정보("transport", "운송 의뢰", 값(item.운송의뢰Id, "미연결")),
            정보("schedule", "예정 출고·도착", $"{일자(item.예정출고일)} → {일자(item.예정도착일)}", true));
}

public sealed class 판매자출고예정ViewModel(
    출고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context context)
    : 출고예정역할관점ViewModelBase(출고예정역할관점카탈로그.판매자, 공통조회, service, context)
{
    protected override bool 포함(출고예정항목응답 item, 현재사용자Snapshot user)
        => user.UserId is { Length: > 0 } userId
           && string.Equals(item.판매자UserId, userId, StringComparison.OrdinalIgnoreCase);

    protected override 출고예정관점항목ViewModel 투영(출고예정항목응답 item, 현재사용자Snapshot user)
        => 항목(
            item,
            $"{상품(item)} 판매 출고",
            $"{주문번호(item)} · {item.수량:N0}개",
            정보("order", "판매 주문", 주문번호(item)),
            정보("product", "상품·SKU", $"{상품(item)} · {값(item.SKU)}"),
            정보("quantity", "출고 수량", $"{item.수량:N0}", true),
            정보("warehouse", "출고 창고", 값(item.출고창고명, $"창고 #{item.출고창고Id}")),
            정보("transport", "운송 인계", 값(item.운송의뢰Id, "인계 전")));
}

public sealed class 창고관리자출고예정ViewModel(
    출고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context context)
    : 출고예정역할관점ViewModelBase(출고예정역할관점카탈로그.창고관리자, 공통조회, service, context)
{
    protected override bool 포함(출고예정항목응답 item, 현재사용자Snapshot user) => true;

    protected override 출고예정관점항목ViewModel 투영(출고예정항목응답 item, 현재사용자Snapshot user)
        => 항목(
            item,
            $"{상품(item)} · {item.수량:N0}개 출고",
            $"{값(item.출고창고명, $"창고 #{item.출고창고Id}")} · {주문번호(item)}",
            정보("warehouse", "출고 창고", 값(item.출고창고명, $"창고 #{item.출고창고Id}")),
            정보("product", "상품·SKU", $"{상품(item)} · {값(item.SKU)}"),
            정보("quantity", "출고 수량", $"{item.수량:N0}", true),
            정보("batch", "출고 묶음", item.출고묶음Id is null ? "미편성" : $"묶음 #{item.출고묶음Id}"),
            정보("transport", "운송 의뢰", 값(item.운송의뢰Id, "미연결")),
            정보("pickup", "상차 일정", 일자(item.예정출고일), true));
}

public sealed class 운송담당자출고예정ViewModel(
    출고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context context)
    : 출고예정역할관점ViewModelBase(출고예정역할관점카탈로그.운송담당자, 공통조회, service, context)
{
    protected override bool 포함(출고예정항목응답 item, 현재사용자Snapshot user)
        => !string.IsNullOrWhiteSpace(item.운송의뢰Id);

    protected override 출고예정관점항목ViewModel 투영(출고예정항목응답 item, 현재사용자Snapshot user)
        => 항목(
            item,
            $"{상품(item)} 상차 예정",
            $"{값(item.출고창고명, $"창고 #{item.출고창고Id}")} · {일자(item.예정출고일)}",
            정보("transport", "운송 의뢰", 값(item.운송의뢰Id), true),
            정보("warehouse", "상차 창고", $"{값(item.출고창고명)} · {값(item.출고창고주소)}"),
            정보("cargo", "화물·수량", $"{상품(item)} · {item.수량:N0}"),
            정보("pickup", "예정 출고", 일자(item.예정출고일), true),
            정보("arrival", "예정 도착", 일자(item.예정도착일)));
}

public sealed class 협동조합운영자출고예정ViewModel(
    출고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context context)
    : 출고예정역할관점ViewModelBase(출고예정역할관점카탈로그.협동조합운영자, 공통조회, service, context)
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

        var source = (request ?? new 목록조회요청()).정규화();
        return 조회Async(source with
        {
            필터조건 = source.필터조건
                .Where(filter => !string.Equals(
                    filter.필드,
                    nameof(출고예정항목응답.커뮤니티원장Id),
                    StringComparison.OrdinalIgnoreCase))
                .Append(new 목록필터조건(
                    nameof(출고예정항목응답.커뮤니티원장Id),
                    "Equal",
                    ledgerId.Trim()))
                .ToArray()
        }, cancellationToken);
    }

    protected override bool 포함(출고예정항목응답 item, 현재사용자Snapshot user)
        => !string.IsNullOrWhiteSpace(item.커뮤니티원장Id);

    protected override 출고예정관점항목ViewModel 투영(출고예정항목응답 item, 현재사용자Snapshot user)
        => 항목(
            item,
            $"공동 원장 {값(item.커뮤니티원장Id)} 출고",
            $"{주문번호(item)} · {상품(item)} {item.수량:N0}",
            정보("ledger", "공동 원장", 값(item.커뮤니티원장Id), true),
            정보("ledger-status", "원장 상태", 값(item.커뮤니티원장상태)),
            정보("parties", "주문자·판매자", $"{값(item.주문자UserId)} · {값(item.판매자UserId)}"),
            정보("warehouse", "출고 창고", 값(item.출고창고명, $"창고 #{item.출고창고Id}")),
            정보("transport", "운송 이행", 값(item.운송의뢰Id, "인계 전")));
}

public sealed class 출고예정PageViewModel : PageViewModelBase
{
    private static readonly 목록조회요청 기본조회요청 = new()
    {
        페이지 = 0,
        페이지크기 = 25,
        정렬조건 = [new 목록정렬조건(nameof(출고예정항목응답.생성일시), 목록정렬방향.내림차순)]
    };

    private readonly 출고예정조회ViewModel _공통조회;
    private I출고예정역할관점ViewModel _현재관점;

    public 출고예정PageViewModel(
        출고예정조회ViewModel 공통조회,
        주문자출고예정ViewModel 주문자,
        판매자출고예정ViewModel 판매자,
        창고관리자출고예정ViewModel 창고관리자,
        운송담당자출고예정ViewModel 운송담당자,
        협동조합운영자출고예정ViewModel 협동조합운영자,
        IHongdal현재사용자Context context)
    {
        _공통조회 = 하위ViewModel등록(공통조회, 수명소유: false);
        역할관점목록 =
        [
            하위ViewModel등록(주문자, 수명소유: false),
            하위ViewModel등록(판매자, 수명소유: false),
            하위ViewModel등록(창고관리자, 수명소유: false),
            하위ViewModel등록(운송담당자, 수명소유: false),
            하위ViewModel등록(협동조합운영자, 수명소유: false)
        ];
        if (역할관점목록.Any(item => !ReferenceEquals(item.공통조회, 공통조회)))
        {
            throw new InvalidOperationException("출고 예정 역할 관점은 같은 공통 조회 ViewModel을 공유해야 합니다.");
        }

        _현재관점 = 역할관점목록.FirstOrDefault(item => item.관점정의.현재사용자관점(context.현재사용자))
                   ?? 주문자;
    }

    public IReadOnlyList<I출고예정역할관점ViewModel> 역할관점목록 { get; }
    public I출고예정역할관점ViewModel 현재관점
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

    public void 공통조회결과투영()
    {
        foreach (var perspective in 역할관점목록)
        {
            perspective.공통결과투영(_공통조회.최근요청 ?? 기본조회요청);
        }
    }

    protected override async Task 불러오기Async(bool 새로고침, CancellationToken cancellationToken)
    {
        var request = 현재관점.최근요청 ?? 기본조회요청;
        if (현재관점.관점정의.역할.RoleCode == BaguaActorRoleCodes.CooperativeCoordinator
            && request.필터조건.All(filter => !string.Equals(
                filter.필드,
                nameof(출고예정항목응답.커뮤니티원장Id),
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!await 현재관점.조회Async(request, cancellationToken))
        {
            throw new InvalidOperationException(현재관점.오류메시지 ?? "출고 예정 목록을 조회하지 못했습니다.");
        }

        if (현재관점.관점정의.데이터연결상태 == 역할관점데이터연결상태.공통조회연결됨)
        {
            공통조회결과투영();
        }
    }
}
