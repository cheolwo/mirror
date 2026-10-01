using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Hr;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 개별주문관점항목ViewModel(
    string 주문원장Id,
    string 역할코드,
    string 제목,
    string 요약,
    string 상태,
    IReadOnlyList<역할관점표시값> 핵심정보,
    개별주문관점항목응답 원본);

public static class 개별주문역할관점카탈로그
{
    public const string 기능코드 = "individual-order-list";

    public static 역할관점업무정의 주문자 { get; } = 정의(
        BaguaActorRoleCodes.Orderer,
        "내 개별 주문",
        "내가 확정한 주문의 계약·결제·수령 이행은 어디까지 진행됐는가?",
        "주문자가 자신의 개별 주문과 연결된 판매·입출고·배송 원장을 확인하는 관점입니다.",
        ["주문 상태", "현재 단계", "수령 이행", "계약·서명", "연결 원장"],
        [
            행동("individual-order-detail", "주문 상세", "개별 주문과 연결된 이행 원장을 확인합니다."),
            행동("individual-order-signature", "계약·서명", "주문 계약과 전자서명 상태를 확인합니다."),
            행동("individual-order-receipt", "수령 확인", "배송 또는 입고 완료 후 수령을 확인합니다.")
        ],
        ["Orderer", "Buyer", "구매자", "일반 구매자"]);

    public static 역할관점업무정의 판매자 { get; } = 정의(
        BaguaActorRoleCodes.Seller,
        "내 판매 개별 주문",
        "내 판매 원장과 연결된 주문을 무엇부터 준비해야 하는가?",
        "판매자가 실제 판매 관계가 있는 개별 주문의 확정·준비·인계 상태를 확인하는 관점입니다.",
        ["판매 관계", "주문 상태", "준비 단계", "입출고 연결", "공개 요청"],
        [
            행동("sales-order-detail", "판매 주문 확인", "판매 원장과 주문 약속을 확인합니다."),
            행동("sales-order-accept", "주문 수락·준비", "상품 공급과 납기 준비 상태를 처리합니다."),
            행동("sales-order-handoff", "창고·운송 인계", "입고와 출고 또는 운송 원장으로 인계합니다.")
        ],
        ["Seller", "Producer", "생산자", "공급자"]);

    public static 역할관점업무정의 창고관리자 { get; } = 정의(
        BaguaActorRoleCodes.WarehouseManager,
        "창고 관련 개별 주문",
        "내 창고의 입고·출고 작업을 요구하는 주문은 무엇인가?",
        "창고 관리자가 직접 담당하는 입고·출고 원장을 기준으로 개별 주문을 확인하는 관점입니다.",
        ["창고 관계", "입고·출고", "주문 상태", "필수 작업", "예외"],
        [
            행동("warehouse-order-inbound", "입고 준비", "주문과 연결된 입고 예정·검수 업무를 확인합니다."),
            행동("warehouse-order-outbound", "출고 준비", "피킹·포장·운송 인계 업무를 확인합니다."),
            행동("warehouse-order-exception", "작업 예외", "수량·상품·일정 차이를 주문 관계자에게 알립니다.")
        ],
        ["WarehouseManager", HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseDispatchOperator]);

    public static 역할관점업무정의 운송담당자 { get; } = 정의(
        BaguaActorRoleCodes.TransportOperator,
        "운송 관련 개별 주문",
        "내가 운송하거나 배송할 화물이 어느 주문에서 발생했는가?",
        "운송 담당자가 직접 참여한 배송·운송 원장을 기준으로 원주문과 약속을 확인하는 관점입니다.",
        ["운송 관계", "주문 상태", "상하차 약속", "수령 조건", "예외"],
        [
            행동("transport-order-detail", "원주문 확인", "운송 의뢰가 발생한 개별 주문을 확인합니다."),
            행동("transport-order-route", "상하차 일정", "픽업·도착지와 약속 시간을 확인합니다."),
            행동("transport-order-proof", "전달 증빙", "운송 완료와 수령 증빙을 주문에 연결합니다.")
        ],
        ["TransportOperator", "Driver", "기사", HrDetailedRoleCodes.ShippingAgencyOperator]);

    public static 역할관점업무정의 협동조합운영자 { get; } = 정의(
        BaguaActorRoleCodes.CooperativeCoordinator,
        "공동 원장별 개별 주문",
        "공동 주문을 구성하는 개별 주문들이 합의와 이행 조건을 충족하는가?",
        "공동 원장의 생성자·참여자가 해당 묶음에 연결된 개별 주문을 집계·감사하는 관점입니다.",
        ["공동 원장", "개별 주문 수", "주문 상태", "연결 원장", "집계 근거"],
        [
            행동("community-individual-orders", "개별 주문 집계", "공동 주문을 구성하는 개별 주문 목록을 확인합니다."),
            행동("community-order-readiness", "확정 조건 확인", "결제·서명·수령 조건의 충족 여부를 확인합니다."),
            행동("community-order-audit", "집계 감사", "공동 발주 수량과 개별 주문 근거를 대조합니다.")
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
            new 업무역할관점좌표(
                BaguaBusinessCodes.Order,
                BaguaBusinessCodes.Order,
                기능코드,
                role.RoleCode),
            role,
            title,
            question,
            description,
            information,
            actions,
            aliases,
            역할관점데이터연결상태.역할별조회연결됨,
            "현재 사용자와 주문·하위 업무 원장 또는 공동 원장의 실제 관계를 검사하는 목록 API와 연결됩니다.");
    }

    private static 역할관점행동후보 행동(string key, string name, string description)
        => new(key, name, description);
}

public interface I개별주문역할관점ViewModel : I서버목록조회ViewModel<개별주문관점항목ViewModel>
{
    역할관점업무정의 관점정의 { get; }
    IReadOnlyList<개별주문관점항목ViewModel> 항목목록 { get; }
    bool 현재사용자관점 { get; }
}

public abstract class 개별주문역할관점ViewModelBase
    : 업무조각ViewModelBase, I개별주문역할관점ViewModel
{
    private readonly I개별주문관점Service _service;
    private 목록조회결과<개별주문관점항목ViewModel> _결과
        = 목록조회결과<개별주문관점항목ViewModel>.비어있음;
    private 목록조회요청? _최근요청;

    protected 개별주문역할관점ViewModelBase(
        역할관점업무정의 definition,
        I개별주문관점Service service,
        IHongdal현재사용자Context context)
        : base($"individual-orders-{definition.역할.RoleCode}", definition.화면제목, 업무조각유형.목록조회)
    {
        관점정의 = definition;
        _service = service;
        현재사용자Context연결(context);
    }

    public 역할관점업무정의 관점정의 { get; }
    public IReadOnlyList<개별주문관점항목ViewModel> 항목목록 => 결과.항목;
    public bool 현재사용자관점 => 관점정의.현재사용자관점(현재사용자);

    public 목록조회결과<개별주문관점항목ViewModel> 결과
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
        var normalized = 요청.정규화(100);
        최근요청 = normalized;
        return 작업실행Async(
            async token =>
            {
                var ledgerId = 필터값(normalized, nameof(개별주문관점항목응답.공동원장Id));
                var response = await _service.목록조회Async(
                    서버관점코드(),
                    ledgerId,
                    서버요청(normalized),
                    token);
                var items = response.Items
                    .Where(item => string.Equals(item.관계코드, 서버관점코드(), StringComparison.OrdinalIgnoreCase))
                    .Select(투영)
                    .ToArray();
                결과 = new 목록조회결과<개별주문관점항목ViewModel>(items, response.TotalCount);
                OnPropertyChanged(nameof(항목목록));
            },
            $"{관점정의.역할.RoleName} 관점의 개별 주문 목록을 조회했습니다.",
            cancellationToken);
    }

    protected abstract 개별주문관점항목ViewModel 투영(개별주문관점항목응답 item);

    protected 개별주문관점항목ViewModel 항목(
        개별주문관점항목응답 item,
        string title,
        string summary,
        params 역할관점표시값[] information)
        => new(item.주문원장Id, 관점정의.역할.RoleCode, title, summary, item.상태, information, item);

    protected static 역할관점표시값 정보(string key, string name, string value, bool emphasis = false)
        => new(key, name, value, emphasis);

    protected static string 값(string? value, string fallback = "미지정")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    protected static string 역할목록(개별주문관점항목응답 item)
        => item.관련원장역할목록.Count == 0 ? "연결 원장 없음" : string.Join(" · ", item.관련원장역할목록);

    private string 서버관점코드()
        => 관점정의.역할.RoleCode switch
        {
            BaguaActorRoleCodes.Orderer => 개별주문관점코드.주문자,
            BaguaActorRoleCodes.Seller => 개별주문관점코드.판매자,
            BaguaActorRoleCodes.WarehouseManager => 개별주문관점코드.창고관리자,
            BaguaActorRoleCodes.TransportOperator => 개별주문관점코드.운송담당자,
            BaguaActorRoleCodes.CooperativeCoordinator => 개별주문관점코드.공동원장,
            _ => throw new InvalidOperationException($"지원하지 않는 개별 주문 역할입니다: {관점정의.역할.RoleCode}")
        };

    private static 개별주문관점목록조회요청 서버요청(목록조회요청 request)
    {
        var sort = request.정렬조건.FirstOrDefault();
        return new 개별주문관점목록조회요청
        {
            Page = request.페이지,
            PageSize = request.페이지크기,
            Search = request.검색어,
            Status = 필터값(request, nameof(개별주문관점항목응답.상태)),
            SortBy = sort?.필드,
            SortDescending = sort?.방향 != 목록정렬방향.오름차순
        };
    }

    private static string? 필터값(목록조회요청 request, string field)
        => request.필터조건.FirstOrDefault(filter => string.Equals(
            filter.필드,
            field,
            StringComparison.OrdinalIgnoreCase))?.값;
}

public sealed class 주문자개별주문ViewModel(
    I개별주문관점Service service,
    IHongdal현재사용자Context context)
    : 개별주문역할관점ViewModelBase(개별주문역할관점카탈로그.주문자, service, context)
{
    protected override 개별주문관점항목ViewModel 투영(개별주문관점항목응답 item)
        => 항목(
            item,
            item.제목,
            $"{값(item.현재단계Key, item.상태)} · 하위 원장 {item.관련하위원장수:N0}개",
            정보("status", "주문 상태", item.상태, true),
            정보("step", "현재 단계", 값(item.현재단계Key)),
            정보("orderer", "주문자", 값(item.주문자표시명, "본인")),
            정보("fulfillment", "연결 이행", 역할목록(item)),
            정보("disclosure", "상세 공개 필요", $"{item.상세공개요청필요수:N0}건"));
}

public sealed class 판매자개별주문ViewModel(
    I개별주문관점Service service,
    IHongdal현재사용자Context context)
    : 개별주문역할관점ViewModelBase(개별주문역할관점카탈로그.판매자, service, context)
{
    protected override 개별주문관점항목ViewModel 투영(개별주문관점항목응답 item)
        => 항목(
            item,
            $"판매 연결 주문 {item.주문원장Id}",
            $"{item.상태} · {역할목록(item)}",
            정보("relation", "판매 관계", item.조회근거, true),
            정보("status", "주문 상태", item.상태),
            정보("fulfillment", "연결 업무", 역할목록(item)),
            정보("children", "관련 원장", $"{item.관련하위원장수:N0}개"),
            정보("disclosure", "공개 요청 필요", $"{item.상세공개요청필요수:N0}건"));
}

public sealed class 창고관리자개별주문ViewModel(
    I개별주문관점Service service,
    IHongdal현재사용자Context context)
    : 개별주문역할관점ViewModelBase(개별주문역할관점카탈로그.창고관리자, service, context)
{
    protected override 개별주문관점항목ViewModel 투영(개별주문관점항목응답 item)
        => 항목(
            item,
            $"창고 작업 주문 {item.주문원장Id}",
            $"{item.상태} · {역할목록(item)}",
            정보("relation", "창고 관계", item.조회근거, true),
            정보("warehouse-flow", "입고·출고", 역할목록(item)),
            정보("status", "주문 상태", item.상태),
            정보("children", "관련 작업 원장", $"{item.관련하위원장수:N0}개"),
            정보("disclosure", "상세 공개 필요", $"{item.상세공개요청필요수:N0}건"));
}

public sealed class 운송담당자개별주문ViewModel(
    I개별주문관점Service service,
    IHongdal현재사용자Context context)
    : 개별주문역할관점ViewModelBase(개별주문역할관점카탈로그.운송담당자, service, context)
{
    protected override 개별주문관점항목ViewModel 투영(개별주문관점항목응답 item)
        => 항목(
            item,
            $"운송 원주문 {item.주문원장Id}",
            $"{item.상태} · {역할목록(item)}",
            정보("relation", "운송 관계", item.조회근거, true),
            정보("transport-flow", "배송·운송", 역할목록(item)),
            정보("status", "주문 상태", item.상태),
            정보("children", "관련 운송 원장", $"{item.관련하위원장수:N0}개"),
            정보("disclosure", "상세 공개 필요", $"{item.상세공개요청필요수:N0}건"));
}

public sealed class 협동조합운영자개별주문ViewModel(
    I개별주문관점Service service,
    IHongdal현재사용자Context context)
    : 개별주문역할관점ViewModelBase(개별주문역할관점카탈로그.협동조합운영자, service, context)
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

        var source = (request ?? new 목록조회요청()).정규화(100);
        return 조회Async(source with
        {
            필터조건 = source.필터조건
                .Where(filter => !string.Equals(
                    filter.필드,
                    nameof(개별주문관점항목응답.공동원장Id),
                    StringComparison.OrdinalIgnoreCase))
                .Append(new 목록필터조건(
                    nameof(개별주문관점항목응답.공동원장Id),
                    "Equal",
                    ledgerId.Trim()))
                .ToArray()
        }, cancellationToken);
    }

    protected override 개별주문관점항목ViewModel 투영(개별주문관점항목응답 item)
        => 항목(
            item,
            item.제목,
            $"공동 원장 {값(item.공동원장Id)} · {item.상태}",
            정보("ledger", "공동 원장", 값(item.공동원장Id), true),
            정보("status", "주문 상태", item.상태),
            정보("step", "현재 단계", 값(item.현재단계Key)),
            정보("fulfillment", "연결 이행", 역할목록(item)),
            정보("children", "관련 원장", $"{item.관련하위원장수:N0}개"));
}

public sealed class 개별주문PageViewModel : PageViewModelBase
{
    private static readonly 목록조회요청 DefaultRequest = new()
    {
        페이지 = 0,
        페이지크기 = 25,
        정렬조건 = [new 목록정렬조건(nameof(개별주문관점항목응답.수정시각Utc), 목록정렬방향.내림차순)]
    };

    private readonly 주문업무상태ViewModel _주문상태;
    private I개별주문역할관점ViewModel _현재관점;
    private 개별주문관점항목ViewModel? _선택된주문;

    public 개별주문PageViewModel(
        주문자개별주문ViewModel 주문자,
        판매자개별주문ViewModel 판매자,
        창고관리자개별주문ViewModel 창고관리자,
        운송담당자개별주문ViewModel 운송담당자,
        협동조합운영자개별주문ViewModel 협동조합운영자,
        주문업무상태ViewModel 주문상태,
        IHongdal현재사용자Context context)
    {
        _주문상태 = 주문상태;
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

    public IReadOnlyList<I개별주문역할관점ViewModel> 역할관점목록 { get; }

    public 개별주문관점항목ViewModel? 선택된주문
    {
        get => _선택된주문;
        private set => SetProperty(ref _선택된주문, value);
    }

    public I개별주문역할관점ViewModel 현재관점
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

    public bool 주문선택(개별주문관점항목ViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.주문원장Id))
        {
            return false;
        }

        선택된주문 = item;
        _주문상태.주문원장선택(item.주문원장Id);
        return true;
    }

    protected override async Task 불러오기Async(bool 새로고침, CancellationToken cancellationToken)
    {
        var request = 현재관점.최근요청 ?? DefaultRequest;
        if (현재관점.관점정의.역할.RoleCode == BaguaActorRoleCodes.CooperativeCoordinator
            && request.필터조건.All(filter => !string.Equals(
                filter.필드,
                nameof(개별주문관점항목응답.공동원장Id),
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!await 현재관점.조회Async(request, cancellationToken))
        {
            throw new InvalidOperationException(현재관점.오류메시지 ?? "개별 주문 목록을 조회하지 못했습니다.");
        }
    }
}
