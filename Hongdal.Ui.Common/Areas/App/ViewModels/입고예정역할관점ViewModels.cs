using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Hr;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 입고예정관점항목ViewModel(
    long 입고Id,
    string 역할코드,
    string 제목,
    string 요약,
    DateTime? 예정도착일,
    string 상태,
    IReadOnlyList<역할관점표시값> 핵심정보,
    입고요청항목응답 원본);

/// <summary>
/// 창고 홈의 입고 예정 기능을 다섯 역할 관점으로 정의한다.
/// 역할 관점은 표시와 행동 후보만 결정하며 서버 권한을 부여하지 않는다.
/// </summary>
public static class 입고예정역할관점카탈로그
{
    public const string 기능코드 = "expected-inbound";

    public static 역할관점업무정의 주문자 { get; } = 정의(
        BaguaActorRoleCodes.Orderer,
        "내가 받을 입고 예정",
        "내 주문은 언제, 어느 수령지 또는 가상 창고로 들어오는가?",
        "주문자가 주문 수량, 수령 위치와 도착 약속을 확인하는 관점입니다.",
        ["내 주문", "수령·가상 창고", "예정 수량", "예정 도착일"],
        [
            행동("order-detail", "내 주문 확인", "입고를 만든 개별 주문과 공동 주문 관계를 확인합니다."),
            행동("receiving-destination", "수령 위치 확인", "자택·사업장·전용 창고 등 입고 목적지를 확인합니다."),
            행동("inbound-issue", "일정 이의·문의", "도착 일정이나 수량 차이에 관한 문의를 시작합니다.")
        ],
        ["Orderer", "Buyer", "구매자", "일반 구매자"],
        역할관점데이터연결상태.역할별조회연결됨,
        "주문자 ID 관계로 필터링하는 입고 예정 읽기 API와 연결됩니다.");

    public static 역할관점업무정의 판매자 { get; } = 정의(
        BaguaActorRoleCodes.Seller,
        "내가 공급할 입고 예정",
        "내가 약속한 상품을 언제, 어느 창고에 공급해야 하는가?",
        "판매자 또는 생산자가 공급 수량, 창고와 납기 약속을 확인하는 관점입니다.",
        ["공급 상품", "공급 수량", "도착 창고", "납기 약속"],
        [
            행동("supply-order-detail", "공급 주문 확인", "입고와 연결된 판매·공급 주문을 확인합니다."),
            행동("inbound-schedule-change", "납기 변경 요청", "도착 예정일 변경을 요청합니다."),
            행동("inbound-document", "입고 서류 준비", "거래명세·검수·원산지 서류를 준비합니다.")
        ],
        ["Seller", "Producer", "생산자", "공급자"],
        역할관점데이터연결상태.역할별조회연결됨,
        "판매자 ID 관계로 필터링하는 입고 예정 읽기 API와 연결됩니다.");

    public static 역할관점업무정의 창고관리자 { get; } = 정의(
        BaguaActorRoleCodes.WarehouseManager,
        "창고 입고 예정 작업",
        "어떤 화물이 언제 도착하며 검수와 적재를 어떻게 준비해야 하는가?",
        "창고 관리자와 입고 담당자가 도크, 검수, 적재와 예외 처리를 준비하는 관점입니다.",
        ["도착 창고", "공급처", "상품·SKU", "예정 수량", "입고 흐름", "운송 의뢰"],
        [
            행동("inbound-receive", "입고 완료", "실제 도착 수량을 확인하고 입고를 완료합니다."),
            행동("inbound-inspection", "검수", "정상·불량 수량과 검수 결과를 기록합니다."),
            행동("inbound-put-away", "적재 위치 지정", "입고 상품의 보관 위치를 지정합니다.")
        ],
        [
            "WarehouseManager",
            HrDetailedRoleCodes.WarehouseManager,
            HrDetailedRoleCodes.WarehouseInboundOperator,
            "창고 입고 담당자"
        ],
        역할관점데이터연결상태.공통조회연결됨,
        "현재 창고 입고 목록 API와 연결됩니다. 실행 명령은 각 API의 서버 권한을 다시 확인합니다.");

    public static 역할관점업무정의 운송담당자 { get; } = 정의(
        BaguaActorRoleCodes.TransportOperator,
        "운송 중인 입고 예정",
        "어떤 화물을 어느 창고에 언제 도착시켜야 하는가?",
        "운송 담당자가 배차·이동·도착 약속과 운송 예외를 확인하는 관점입니다.",
        ["운송 의뢰", "목적 창고", "화물", "예정 도착일", "인계 상태"],
        [
            행동("transport-detail", "운송 의뢰 확인", "연결된 운송 의뢰와 배차 정보를 확인합니다."),
            행동("transport-arrival", "도착 보고", "하차와 창고 인계 시각을 보고합니다."),
            행동("transport-exception", "운송 예외 보고", "지연·파손·경로 변경 같은 예외를 기록합니다.")
        ],
        ["TransportOperator", "Driver", "기사", HrDetailedRoleCodes.ShippingAgencyOperator],
        역할관점데이터연결상태.역할별조회연결됨,
        "운송 원장의 화주·추천 기사·확정 기사 관계로 필터링하는 읽기 API와 연결됩니다.");

    public static 역할관점업무정의 협동조합운영자 { get; } = 정의(
        BaguaActorRoleCodes.CooperativeCoordinator,
        "공동 원장 입고 예정",
        "공동 주문의 입고 약속과 참여자 이행 상태가 원장 합의와 일치하는가?",
        "협동조합 운영자가 공동 원장, 주문, 공급, 운송과 입고 이행을 감사하는 관점입니다.",
        ["공동 원장", "원장 상태", "주문·공급 연결", "예정 도착일", "이행 예외"],
        [
            행동("community-ledger-detail", "공동 원장 확인", "입고를 만든 공동 주문 원장과 합의를 확인합니다."),
            행동("community-inbound-issue", "이행 쟁점 등록", "수량·납기·수령지 차이를 원장 쟁점으로 기록합니다."),
            행동("community-inbound-audit", "입고 이행 감사", "합의 조건과 실제 입고 이력을 대조합니다.")
        ],
        [
            "CooperativeCoordinator",
            "협동조합 관리자",
            HrDetailedRoleCodes.OrdererGroupRepresentative,
            HrDetailedRoleCodes.OrdererGroupImportCoordinator
        ],
        역할관점데이터연결상태.역할별조회연결됨,
        "선택한 공동 원장의 생성자·참여자 관계를 검사하는 원장별 읽기 API와 연결됩니다.");

    public static IReadOnlyList<역할관점업무정의> 전체 { get; } =
        [주문자, 판매자, 창고관리자, 운송담당자, 협동조합운영자];

    public static 역할관점업무정의 찾기(string 역할코드)
        => 전체.FirstOrDefault(item => string.Equals(
               item.역할.RoleCode,
               역할코드,
               StringComparison.OrdinalIgnoreCase))
           ?? throw new KeyNotFoundException($"등록되지 않은 입고 예정 역할 관점입니다: {역할코드}");

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

public interface I입고예정역할관점ViewModel : I서버목록조회ViewModel<입고예정관점항목ViewModel>
{
    역할관점업무정의 관점정의 { get; }
    입고예정조회ViewModel 공통조회 { get; }
    IReadOnlyList<입고예정관점항목ViewModel> 항목목록 { get; }
    int 원본전체건수 { get; }
    bool 관점별서버전체건수확정됨 { get; }
    bool 현재사용자관점 { get; }
    void 공통결과투영(목록조회요청? 요청 = null);
}

/// <summary>
/// 하나의 정규화된 입고 예정 조회 결과를 역할별 표시 항목으로 투영하는 기반 ViewModel이다.
/// 역할별 하위 ViewModel은 필터와 표시 정보만 재정의한다.
/// </summary>
public abstract class 입고예정역할관점ViewModelBase
    : 업무조각ViewModelBase, I입고예정역할관점ViewModel
{
    private 목록조회결과<입고예정관점항목ViewModel> _결과
        = 목록조회결과<입고예정관점항목ViewModel>.비어있음;
    private 목록조회요청? _최근요청;
    private int _원본전체건수;
    private readonly I입출고작업Service _service;

    protected 입고예정역할관점ViewModelBase(
        역할관점업무정의 관점정의,
        입고예정조회ViewModel 공통조회,
        I입출고작업Service service,
        IHongdal현재사용자Context 현재사용자Context)
        : base(
            $"expected-inbound-{관점정의.역할.RoleCode}",
            관점정의.화면제목,
            업무조각유형.목록조회)
    {
        ArgumentNullException.ThrowIfNull(관점정의);
        ArgumentNullException.ThrowIfNull(공통조회);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(현재사용자Context);

        this.관점정의 = 관점정의;
        this.공통조회 = 공통조회;
        _service = service;
        현재사용자Context연결(현재사용자Context);
    }

    public 역할관점업무정의 관점정의 { get; }
    public 입고예정조회ViewModel 공통조회 { get; }
    public IReadOnlyList<입고예정관점항목ViewModel> 항목목록 => 결과.항목;

    public 목록조회결과<입고예정관점항목ViewModel> 결과
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

    public bool 관점별서버전체건수확정됨 => 관점정의.서버조회준비됨;
    public bool 현재사용자관점 => 관점정의.현재사용자관점(현재사용자);

    public Task<bool> 조회Async(
        목록조회요청 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var normalized = 요청.정규화();
        최근요청 = normalized;

        if (!관점정의.서버조회준비됨)
        {
            return Task.FromResult(유효성실패(관점정의.데이터연결안내));
        }

        return 작업실행Async(
            async token =>
            {
                if (관점정의.데이터연결상태 == 역할관점데이터연결상태.공통조회연결됨)
                {
                    if (!await 공통조회.조회Async(normalized, token))
                    {
                        throw new InvalidOperationException(
                            공통조회.오류메시지 ?? "입고 예정 목록을 조회하지 못했습니다.");
                    }

                    공통결과투영(공통조회.최근요청 ?? normalized);
                    return;
                }

                var ledgerId = 필터값(normalized, nameof(입고요청항목응답.커뮤니티원장Id));
                var response = await _service.입고예정관점목록조회Async(
                    서버관점코드(),
                    ledgerId,
                    서버요청(normalized),
                    token);
                결과투영(response.Items, response.TotalCount, normalized, 서버전체건수확정: true);
            },
            $"{관점정의.역할.RoleName} 관점의 입고 예정 목록을 조회했습니다.",
            cancellationToken);
    }

    public void 공통결과투영(목록조회요청? 요청 = null)
        => 결과투영(
            공통조회.결과.항목,
            공통조회.결과.전체건수,
            요청 ?? 공통조회.최근요청,
            서버전체건수확정: 관점정의.데이터연결상태 == 역할관점데이터연결상태.공통조회연결됨);

    private void 결과투영(
        IReadOnlyList<입고요청항목응답> source,
        int sourceTotalCount,
        목록조회요청? 요청,
        bool 서버전체건수확정)
    {
        var items = source
            .Where(item => 포함(item, 현재사용자))
            .Select(item => 투영(item, 현재사용자))
            .ToArray();

        최근요청 = 요청;
        원본전체건수 = sourceTotalCount;
        결과 = new 목록조회결과<입고예정관점항목ViewModel>(
            items,
            서버전체건수확정 ? sourceTotalCount : items.Length);
        OnPropertyChanged(nameof(항목목록));
    }

    private string 서버관점코드()
        => 관점정의.역할.RoleCode switch
        {
            BaguaActorRoleCodes.Orderer => 창고업무관점코드.주문자,
            BaguaActorRoleCodes.Seller => 창고업무관점코드.판매자,
            BaguaActorRoleCodes.WarehouseManager => 창고업무관점코드.창고관리자,
            BaguaActorRoleCodes.TransportOperator => 창고업무관점코드.운송담당자,
            BaguaActorRoleCodes.CooperativeCoordinator => 창고업무관점코드.공동원장,
            _ => throw new InvalidOperationException($"지원하지 않는 입고 예정 역할입니다: {관점정의.역할.RoleCode}")
        };

    private static 입고요청목록조회요청 서버요청(목록조회요청 request)
    {
        var firstSort = request.정렬조건.FirstOrDefault();
        return new 입고요청목록조회요청
        {
            Page = request.페이지,
            PageSize = request.페이지크기,
            Search = request.검색어,
            SortBy = firstSort?.필드,
            SortDescending = firstSort?.방향 != 목록정렬방향.오름차순,
            WarehouseId = long.TryParse(
                필터값(request, nameof(입고요청항목응답.창고Id)),
                out var warehouseId)
                ? warehouseId
                : null,
            Status = 입고상태코드.예정,
            FlowType = 필터값(request, nameof(입고요청항목응답.입고흐름유형))
        };
    }

    private static string? 필터값(목록조회요청 request, string field)
        => request.필터조건.FirstOrDefault(item => string.Equals(
            item.필드,
            field,
            StringComparison.OrdinalIgnoreCase))?.값;

    protected abstract bool 포함(입고요청항목응답 item, 현재사용자Snapshot 현재사용자);

    protected abstract 입고예정관점항목ViewModel 투영(
        입고요청항목응답 item,
        현재사용자Snapshot 현재사용자);

    protected 입고예정관점항목ViewModel 항목(
        입고요청항목응답 item,
        string 제목,
        string 요약,
        params 역할관점표시값[] 핵심정보)
        => new(
            item.Id,
            관점정의.역할.RoleCode,
            제목,
            요약,
            item.예정도착일,
            item.상태,
            핵심정보,
            item);

    protected static string 상품명(입고요청항목응답 item)
        => 값(item.예정상품명, 값(item.예정SKU, 값(item.공급처명, $"입고 #{item.Id}")));

    protected static string 주문번호(입고요청항목응답 item)
        => 값(item.주문참조번호, 값(item.원주문참조번호, item.주문Id is null ? "연결 주문 없음" : $"주문 #{item.주문Id}"));

    protected static string 수량(입고요청항목응답 item)
        => item.예정수량 is null ? "미정" : $"{item.예정수량:N0}";

    protected static string 도착일(입고요청항목응답 item)
        => item.예정도착일?.ToString("yyyy-MM-dd") ?? "일정 미정";

    protected static string 값(string? value, string fallback = "미지정")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    protected static 역할관점표시값 정보(
        string key,
        string 이름,
        string 값,
        bool 강조 = false)
        => new(key, 이름, 값, 강조);
}

public sealed class 주문자입고예정ViewModel(
    입고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context 현재사용자Context)
    : 입고예정역할관점ViewModelBase(
        입고예정역할관점카탈로그.주문자,
        공통조회,
        service,
        현재사용자Context)
{
    protected override bool 포함(입고요청항목응답 item, 현재사용자Snapshot 현재사용자)
        => 현재사용자.UserId is { Length: > 0 } userId
           && string.Equals(item.주문자UserId, userId, StringComparison.OrdinalIgnoreCase);

    protected override 입고예정관점항목ViewModel 투영(
        입고요청항목응답 item,
        현재사용자Snapshot 현재사용자)
        => 항목(
            item,
            $"{상품명(item)} 입고 예정",
            $"{주문번호(item)} · {도착일(item)} 도착 예정",
            정보("order", "내 주문", 주문번호(item)),
            정보("quantity", "내 주문 수량", 수량(item)),
            정보("destination", "수령·가상 창고", $"창고 #{item.창고Id}"),
            정보("seller", "판매자", 값(item.판매자UserId)),
            정보("arrival", "예정 도착", 도착일(item), 강조: true));
}

public sealed class 판매자입고예정ViewModel(
    입고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context 현재사용자Context)
    : 입고예정역할관점ViewModelBase(
        입고예정역할관점카탈로그.판매자,
        공통조회,
        service,
        현재사용자Context)
{
    protected override bool 포함(입고요청항목응답 item, 현재사용자Snapshot 현재사용자)
        => 현재사용자.UserId is { Length: > 0 } userId
           && string.Equals(item.판매자UserId, userId, StringComparison.OrdinalIgnoreCase);

    protected override 입고예정관점항목ViewModel 투영(
        입고요청항목응답 item,
        현재사용자Snapshot 현재사용자)
        => 항목(
            item,
            $"{상품명(item)} 공급 예정",
            $"창고 #{item.창고Id} · {도착일(item)} 납기",
            정보("product", "공급 상품", 상품명(item)),
            정보("quantity", "공급 수량", 수량(item)),
            정보("warehouse", "도착 창고", $"창고 #{item.창고Id}"),
            정보("order", "연결 주문", 주문번호(item)),
            정보("arrival", "납기 약속", 도착일(item), 강조: true));
}

public sealed class 창고관리자입고예정ViewModel(
    입고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context 현재사용자Context)
    : 입고예정역할관점ViewModelBase(
        입고예정역할관점카탈로그.창고관리자,
        공통조회,
        service,
        현재사용자Context)
{
    protected override bool 포함(입고요청항목응답 item, 현재사용자Snapshot 현재사용자)
        => true;

    protected override 입고예정관점항목ViewModel 투영(
        입고요청항목응답 item,
        현재사용자Snapshot 현재사용자)
        => 항목(
            item,
            $"{상품명(item)} · 창고 #{item.창고Id}",
            $"{값(item.공급처명)}에서 {수량(item)} 입고 예정",
            정보("supplier", "공급처", 값(item.공급처명)),
            정보("sku", "상품·SKU", $"{상품명(item)} · {값(item.예정SKU)}"),
            정보("quantity", "예정 수량", 수량(item), 강조: true),
            정보("flow", "입고 흐름", 입고흐름유형코드.GetDisplayName(item.입고흐름유형)),
            정보("transport", "운송 의뢰", 값(item.운송의뢰Id, "미연결")),
            정보("arrival", "예정 도착", 도착일(item), 강조: true));
}

public sealed class 운송담당자입고예정ViewModel(
    입고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context 현재사용자Context)
    : 입고예정역할관점ViewModelBase(
        입고예정역할관점카탈로그.운송담당자,
        공통조회,
        service,
        현재사용자Context)
{
    protected override bool 포함(입고요청항목응답 item, 현재사용자Snapshot 현재사용자)
        => !string.IsNullOrWhiteSpace(item.운송의뢰Id);

    protected override 입고예정관점항목ViewModel 투영(
        입고요청항목응답 item,
        현재사용자Snapshot 현재사용자)
        => 항목(
            item,
            $"{상품명(item)} 운송 · 창고 #{item.창고Id}",
            $"운송 {값(item.운송의뢰Id)} · {도착일(item)} 도착 약속",
            정보("transport", "운송 의뢰", 값(item.운송의뢰Id), 강조: true),
            정보("destination", "목적 창고", $"창고 #{item.창고Id}"),
            정보("cargo", "화물", $"{상품명(item)} · {수량(item)}"),
            정보("supplier", "출발 공급처", 값(item.공급처명)),
            정보("arrival", "예정 도착", 도착일(item), 강조: true));
}

public sealed class 협동조합운영자입고예정ViewModel(
    입고예정조회ViewModel 공통조회,
    I입출고작업Service service,
    IHongdal현재사용자Context 현재사용자Context)
    : 입고예정역할관점ViewModelBase(
        입고예정역할관점카탈로그.협동조합운영자,
        공통조회,
        service,
        현재사용자Context)
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
                    nameof(입고요청항목응답.커뮤니티원장Id),
                    StringComparison.OrdinalIgnoreCase))
                .Append(new 목록필터조건(
                    nameof(입고요청항목응답.커뮤니티원장Id),
                    "Equal",
                    ledgerId.Trim()))
                .ToArray()
        }, cancellationToken);
    }

    protected override bool 포함(입고요청항목응답 item, 현재사용자Snapshot 현재사용자)
        => !string.IsNullOrWhiteSpace(item.커뮤니티원장Id);

    protected override 입고예정관점항목ViewModel 투영(
        입고요청항목응답 item,
        현재사용자Snapshot 현재사용자)
        => 항목(
            item,
            $"공동 원장 {값(item.커뮤니티원장Id)} 입고",
            $"{주문번호(item)} · {상품명(item)} 이행",
            정보("ledger", "공동 원장", 값(item.커뮤니티원장Id), 강조: true),
            정보("ledger-status", "원장 상태", 값(item.커뮤니티원장상태)),
            정보("order", "연결 주문", 주문번호(item)),
            정보("parties", "주문자·판매자", $"{값(item.주문자UserId)} · {값(item.판매자UserId)}"),
            정보("arrival", "예정 도착", 도착일(item), 강조: true));
}

/// <summary>
/// 공통 입고 예정 조회 한 번과 다섯 역할 투영을 조립하는 페이지 수준 ViewModel이다.
/// Razor는 현재관점 또는 역할관점목록 중 필요한 조각만 주입해 테이블·카드로 표현할 수 있다.
/// </summary>
public sealed class 입고예정PageViewModel : PageViewModelBase
{
    private static readonly 목록조회요청 기본조회요청 = new()
    {
        페이지 = 0,
        페이지크기 = 25,
        정렬조건 =
        [
            new 목록정렬조건(
                nameof(입고요청항목응답.예정도착일),
                목록정렬방향.오름차순)
        ]
    };

    private readonly 입고예정조회ViewModel _공통조회;
    private I입고예정역할관점ViewModel _현재관점;

    public 입고예정PageViewModel(
        입고예정조회ViewModel 공통조회,
        주문자입고예정ViewModel 주문자,
        판매자입고예정ViewModel 판매자,
        창고관리자입고예정ViewModel 창고관리자,
        운송담당자입고예정ViewModel 운송담당자,
        협동조합운영자입고예정ViewModel 협동조합운영자,
        IHongdal현재사용자Context 현재사용자Context)
    {
        ArgumentNullException.ThrowIfNull(공통조회);
        ArgumentNullException.ThrowIfNull(현재사용자Context);

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
            throw new InvalidOperationException("입고 예정 역할 관점은 같은 공통 조회 ViewModel을 공유해야 합니다.");
        }

        _현재관점 = 역할관점목록.FirstOrDefault(item =>
                       item.관점정의.현재사용자관점(현재사용자Context.현재사용자))
                   ?? 주문자;
    }

    public IReadOnlyList<I입고예정역할관점ViewModel> 역할관점목록 { get; }

    public I입고예정역할관점ViewModel 현재관점
    {
        get => _현재관점;
        private set => SetProperty(ref _현재관점, value);
    }

    public bool 관점선택(string 역할코드)
    {
        var 관점 = 역할관점목록.FirstOrDefault(item => string.Equals(
            item.관점정의.역할.RoleCode,
            역할코드,
            StringComparison.OrdinalIgnoreCase));
        if (관점 is null)
        {
            return false;
        }

        현재관점 = 관점;
        return true;
    }

    public void 공통조회결과투영()
    {
        foreach (var 관점 in 역할관점목록)
        {
            관점.공통결과투영(_공통조회.최근요청 ?? 기본조회요청);
        }
    }

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        var 요청 = 현재관점.최근요청 ?? 기본조회요청;

        // 공동 원장 관점은 원장을 고른 뒤 해당 원장의 참여 관계로 조회한다.
        if (현재관점.관점정의.역할.RoleCode == BaguaActorRoleCodes.CooperativeCoordinator
            && 요청.필터조건.All(filter => !string.Equals(
                filter.필드,
                nameof(입고요청항목응답.커뮤니티원장Id),
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!await 현재관점.조회Async(요청, cancellationToken))
        {
            throw new InvalidOperationException(
                현재관점.오류메시지 ?? "입고 예정 목록을 조회하지 못했습니다.");
        }

        if (현재관점.관점정의.데이터연결상태 == 역할관점데이터연결상태.공통조회연결됨)
        {
            공통조회결과투영();
        }
    }
}
