using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Hr;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record 공동주문관점항목ViewModel(
    string 공동주문원장Id,
    string 역할코드,
    string 제목,
    string 요약,
    string 상태,
    IReadOnlyList<역할관점표시값> 핵심정보,
    공동주문관점항목응답 원본);

public static class 공동주문역할관점카탈로그
{
    public const string 기능코드 = "group-order-list";

    public static 역할관점업무정의 주문자 { get; } = 정의(
        BaguaActorRoleCodes.Orderer,
        "내가 참여한 공동주문",
        "내 개별 주문이 어느 공동주문에 묶였고 전체 진행은 어디까지 왔는가?",
        "주문자가 자신의 개별 주문을 포함하는 공동주문의 집계·서명·이행 상태를 확인하는 관점입니다.",
        ["상품", "개별 주문 수", "내 주문 연결", "서명 진행", "공동 이행"],
        [
            행동("group-order-detail", "공동주문 상세", "공동주문의 집계와 전체 진행 상태를 확인합니다."),
            행동("group-order-my-order", "내 개별 주문", "공동주문에 포함된 자신의 주문을 확인합니다."),
            행동("group-order-signature", "서명 진행", "참여 주문들의 계약·서명 완료 현황을 확인합니다.")
        ],
        ["Orderer", "Buyer", "구매자", "일반 구매자"]);

    public static 역할관점업무정의 판매자 { get; } = 정의(
        BaguaActorRoleCodes.Seller,
        "판매 관련 공동주문",
        "내가 공급할 공동 주문량과 준비 상태는 어떠한가?",
        "판매자가 판매 원장으로 연결된 개별 주문들의 공동 집계와 공급 준비를 확인하는 관점입니다.",
        ["상품", "주문 집계", "공급 대상", "완료 현황", "서명 현황"],
        [
            행동("group-order-supply", "공급 집계 확인", "확정된 개별 주문 수와 공동 공급 대상을 확인합니다."),
            행동("group-order-stock", "재고·납기 확인", "공동 수량의 공급 가능성과 납기를 확인합니다."),
            행동("group-order-handoff", "물류 인계", "창고 입출고 또는 운송 업무로 인계합니다.")
        ],
        ["Seller", "Producer", "생산자", "공급자"]);

    public static 역할관점업무정의 창고관리자 { get; } = 정의(
        BaguaActorRoleCodes.WarehouseManager,
        "창고 관련 공동주문",
        "공동주문의 몇 개 개별 주문을 입고·보관·출고해야 하는가?",
        "창고 관리자가 입출고 원장과 연결된 개별 주문을 공동 작업 단위로 확인하는 관점입니다.",
        ["개별 주문 수", "완료 수", "입출고 단계", "공동 배분", "예외"],
        [
            행동("group-order-inbound", "공동 입고", "공동주문에 필요한 입고와 검수 진행을 확인합니다."),
            행동("group-order-outbound", "공동 출고", "개별 주문별 피킹·포장·출고 배분을 확인합니다."),
            행동("group-order-warehouse-exception", "창고 예외", "수량·보관·배분 차이를 공동 원장에 알립니다.")
        ],
        ["WarehouseManager", HrDetailedRoleCodes.WarehouseManager, HrDetailedRoleCodes.WarehouseDispatchOperator]);

    public static 역할관점업무정의 운송담당자 { get; } = 정의(
        BaguaActorRoleCodes.TransportOperator,
        "운송 관련 공동주문",
        "공동주문 화물을 어떤 집하·분배 단위로 운송해야 하는가?",
        "운송 담당자가 배송·운송 원장에 참여한 개별 주문의 공동 집하와 분배 맥락을 확인하는 관점입니다.",
        ["공동 화물", "개별 주문 수", "완료 수", "집하·분배", "수령 확인"],
        [
            행동("group-order-transport", "공동 운송 확인", "공동주문 운송과 연결된 원주문 집계를 확인합니다."),
            행동("group-order-consolidation", "집하·분배", "집하 창고와 개별 도착지 분배 단위를 확인합니다."),
            행동("group-order-delivery-proof", "전달 증빙", "공동 운송과 개별 수령 증빙을 연결합니다.")
        ],
        ["TransportOperator", "Driver", "기사", HrDetailedRoleCodes.ShippingAgencyOperator]);

    public static 역할관점업무정의 협동조합운영자 { get; } = 정의(
        BaguaActorRoleCodes.CooperativeCoordinator,
        "공동 원장별 공동주문",
        "공동구매 합의에서 만들어진 주문집계가 개별 주문의 합과 일치하는가?",
        "공동 원장의 생성자·참여자가 공동주문 집계와 개별 주문·서명·이행 상태를 감사하는 관점입니다.",
        ["공동 원장", "자동집단", "상품", "개별 주문 집계", "서명·완료"],
        [
            행동("community-group-orders", "주문집계 확인", "공동 원장에 연결된 공동주문을 확인합니다."),
            행동("community-group-order-audit", "개별 주문 합계 감사", "공동 수량의 근거인 개별 주문 집합을 대조합니다."),
            행동("community-group-order-readiness", "발주 준비 확인", "서명과 필수 주문 상태가 발주 조건을 충족하는지 확인합니다.")
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
            new 업무역할관점좌표(BaguaBusinessCodes.Order, BaguaBusinessCodes.Order, 기능코드, role.RoleCode),
            role,
            title,
            question,
            description,
            information,
            actions,
            aliases,
            역할관점데이터연결상태.역할별조회연결됨,
            "개별 주문과 하위 판매·창고·운송 원장 또는 공동 원장의 실제 관계를 검사하는 API와 연결됩니다.");
    }

    private static 역할관점행동후보 행동(string key, string name, string description)
        => new(key, name, description);
}

public interface I공동주문역할관점ViewModel : I서버목록조회ViewModel<공동주문관점항목ViewModel>
{
    역할관점업무정의 관점정의 { get; }
    IReadOnlyList<공동주문관점항목ViewModel> 항목목록 { get; }
    bool 현재사용자관점 { get; }
}

public abstract class 공동주문역할관점ViewModelBase
    : 업무조각ViewModelBase, I공동주문역할관점ViewModel
{
    private readonly I공동주문관점Service _service;
    private 목록조회결과<공동주문관점항목ViewModel> _결과
        = 목록조회결과<공동주문관점항목ViewModel>.비어있음;
    private 목록조회요청? _최근요청;

    protected 공동주문역할관점ViewModelBase(
        역할관점업무정의 definition,
        I공동주문관점Service service,
        IHongdal현재사용자Context context)
        : base($"group-orders-{definition.역할.RoleCode}", definition.화면제목, 업무조각유형.목록조회)
    {
        관점정의 = definition;
        _service = service;
        현재사용자Context연결(context);
    }

    public 역할관점업무정의 관점정의 { get; }
    public IReadOnlyList<공동주문관점항목ViewModel> 항목목록 => 결과.항목;
    public bool 현재사용자관점 => 관점정의.현재사용자관점(현재사용자);

    public 목록조회결과<공동주문관점항목ViewModel> 결과
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
                var ledgerId = 필터값(normalized, nameof(공동주문관점항목응답.공동원장Id));
                var response = await _service.목록조회Async(
                    서버관점코드(),
                    ledgerId,
                    서버요청(normalized),
                    token);
                var items = response.Items
                    .Where(item => string.Equals(item.관계코드, 서버관점코드(), StringComparison.OrdinalIgnoreCase))
                    .Select(투영)
                    .ToArray();
                결과 = new 목록조회결과<공동주문관점항목ViewModel>(items, response.TotalCount);
                OnPropertyChanged(nameof(항목목록));
            },
            $"{관점정의.역할.RoleName} 관점의 공동주문 목록을 조회했습니다.",
            cancellationToken);
    }

    protected abstract 공동주문관점항목ViewModel 투영(공동주문관점항목응답 item);

    protected 공동주문관점항목ViewModel 항목(
        공동주문관점항목응답 item,
        string title,
        string summary,
        params 역할관점표시값[] information)
        => new(item.공동주문원장Id, 관점정의.역할.RoleCode, title, summary, item.상태, information, item);

    protected static 역할관점표시값 정보(string key, string name, string value, bool emphasis = false)
        => new(key, name, value, emphasis);

    protected static string 값(string? value, string fallback = "미지정")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    protected static string 진행(공동주문관점항목응답 item)
        => $"{item.완료개별주문수:N0}/{item.개별주문수:N0} 완료";

    protected static string 서명(공동주문관점항목응답 item)
        => $"{item.서명완료주문수:N0}/{item.서명대상주문수:N0} 완료";

    private string 서버관점코드()
        => 관점정의.역할.RoleCode switch
        {
            BaguaActorRoleCodes.Orderer => 공동주문관점코드.주문자,
            BaguaActorRoleCodes.Seller => 공동주문관점코드.판매자,
            BaguaActorRoleCodes.WarehouseManager => 공동주문관점코드.창고관리자,
            BaguaActorRoleCodes.TransportOperator => 공동주문관점코드.운송담당자,
            BaguaActorRoleCodes.CooperativeCoordinator => 공동주문관점코드.공동원장,
            _ => throw new InvalidOperationException($"지원하지 않는 공동주문 역할입니다: {관점정의.역할.RoleCode}")
        };

    private static 공동주문관점목록조회요청 서버요청(목록조회요청 request)
    {
        var sort = request.정렬조건.FirstOrDefault();
        return new 공동주문관점목록조회요청
        {
            Page = request.페이지,
            PageSize = request.페이지크기,
            Search = request.검색어,
            Status = 필터값(request, nameof(공동주문관점항목응답.상태)),
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

public sealed class 주문자공동주문ViewModel(I공동주문관점Service service, IHongdal현재사용자Context context)
    : 공동주문역할관점ViewModelBase(공동주문역할관점카탈로그.주문자, service, context)
{
    protected override 공동주문관점항목ViewModel 투영(공동주문관점항목응답 item)
        => 항목(
            item,
            item.제목,
            $"{값(item.상품명, "공동 상품")} · 개별 주문 {item.개별주문수:N0}건",
            정보("product", "상품", 값(item.상품명, item.상품키 ?? "미지정"), true),
            정보("orders", "개별 주문", $"{item.개별주문수:N0}건"),
            정보("progress", "공동 이행", 진행(item)),
            정보("signature", "서명 진행", 서명(item)),
            정보("status", "공동주문 상태", item.상태));
}

public sealed class 판매자공동주문ViewModel(I공동주문관점Service service, IHongdal현재사용자Context context)
    : 공동주문역할관점ViewModelBase(공동주문역할관점카탈로그.판매자, service, context)
{
    protected override 공동주문관점항목ViewModel 투영(공동주문관점항목응답 item)
        => 항목(
            item,
            $"{값(item.상품명, item.제목)} 공급 공동주문",
            $"개별 주문 {item.개별주문수:N0}건 · {진행(item)}",
            정보("relation", "판매 관계", item.조회근거, true),
            정보("product", "공급 상품", 값(item.상품명, item.상품키 ?? "미지정")),
            정보("orders", "주문 집계", $"{item.개별주문수:N0}건"),
            정보("progress", "완료 현황", 진행(item)),
            정보("signature", "서명 현황", 서명(item)));
}

public sealed class 창고관리자공동주문ViewModel(I공동주문관점Service service, IHongdal현재사용자Context context)
    : 공동주문역할관점ViewModelBase(공동주문역할관점카탈로그.창고관리자, service, context)
{
    protected override 공동주문관점항목ViewModel 투영(공동주문관점항목응답 item)
        => 항목(
            item,
            $"{값(item.상품명, item.제목)} 공동 입출고",
            $"개별 주문 {item.개별주문수:N0}건 · {진행(item)}",
            정보("relation", "창고 관계", item.조회근거, true),
            정보("orders", "개별 주문", $"{item.개별주문수:N0}건"),
            정보("completed", "입출고 완료", 진행(item)),
            정보("required", "필수 주문 완료", item.필수개별주문완료여부 ? "완료" : "진행 중"),
            정보("status", "공동주문 상태", item.상태));
}

public sealed class 운송담당자공동주문ViewModel(I공동주문관점Service service, IHongdal현재사용자Context context)
    : 공동주문역할관점ViewModelBase(공동주문역할관점카탈로그.운송담당자, service, context)
{
    protected override 공동주문관점항목ViewModel 투영(공동주문관점항목응답 item)
        => 항목(
            item,
            $"{값(item.상품명, item.제목)} 공동 운송",
            $"개별 주문 {item.개별주문수:N0}건 · {진행(item)}",
            정보("relation", "운송 관계", item.조회근거, true),
            정보("cargo", "공동 화물", 값(item.상품명, item.상품키 ?? "미지정")),
            정보("orders", "개별 주문", $"{item.개별주문수:N0}건"),
            정보("progress", "전달 진행", 진행(item)),
            정보("status", "공동주문 상태", item.상태));
}

public sealed class 협동조합운영자공동주문ViewModel(I공동주문관점Service service, IHongdal현재사용자Context context)
    : 공동주문역할관점ViewModelBase(공동주문역할관점카탈로그.협동조합운영자, service, context)
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
                    nameof(공동주문관점항목응답.공동원장Id),
                    StringComparison.OrdinalIgnoreCase))
                .Append(new 목록필터조건(
                    nameof(공동주문관점항목응답.공동원장Id),
                    "Equal",
                    ledgerId.Trim()))
                .ToArray()
        }, cancellationToken);
    }

    protected override 공동주문관점항목ViewModel 투영(공동주문관점항목응답 item)
        => 항목(
            item,
            item.제목,
            $"공동 원장 {값(item.공동원장Id)} · {값(item.상품명, "상품 미지정")}",
            정보("ledger", "공동 원장", 값(item.공동원장Id), true),
            정보("auto-group", "자동집단", 값(item.자동집단Id)),
            정보("product", "상품", 값(item.상품명, item.상품키 ?? "미지정")),
            정보("orders", "개별 주문 집계", $"{item.개별주문수:N0}건 · {진행(item)}"),
            정보("signature", "서명", 서명(item)));
}

public sealed class 공동주문PageViewModel : PageViewModelBase
{
    private static readonly 목록조회요청 DefaultRequest = new()
    {
        페이지 = 0,
        페이지크기 = 25,
        정렬조건 = [new 목록정렬조건(nameof(공동주문관점항목응답.수정시각Utc), 목록정렬방향.내림차순)]
    };

    private readonly 공동구매실행상태ViewModel _실행상태;
    private I공동주문역할관점ViewModel _현재관점;
    private 공동주문관점항목ViewModel? _선택된공동주문;

    public 공동주문PageViewModel(
        주문자공동주문ViewModel 주문자,
        판매자공동주문ViewModel 판매자,
        창고관리자공동주문ViewModel 창고관리자,
        운송담당자공동주문ViewModel 운송담당자,
        협동조합운영자공동주문ViewModel 협동조합운영자,
        공동구매실행상태ViewModel 실행상태,
        IHongdal현재사용자Context context)
    {
        _실행상태 = 실행상태;
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

    public IReadOnlyList<I공동주문역할관점ViewModel> 역할관점목록 { get; }

    public I공동주문역할관점ViewModel 현재관점
    {
        get => _현재관점;
        private set => SetProperty(ref _현재관점, value);
    }

    public 공동주문관점항목ViewModel? 선택된공동주문
    {
        get => _선택된공동주문;
        private set => SetProperty(ref _선택된공동주문, value);
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

    public bool 공동주문선택(공동주문관점항목ViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.공동주문원장Id))
        {
            return false;
        }

        선택된공동주문 = item;
        _실행상태.주문집계선택(item.공동주문원장Id);
        return true;
    }

    protected override async Task 불러오기Async(bool 새로고침, CancellationToken cancellationToken)
    {
        var request = 현재관점.최근요청 ?? DefaultRequest;
        if (현재관점.관점정의.역할.RoleCode == BaguaActorRoleCodes.CooperativeCoordinator
            && request.필터조건.All(filter => !string.Equals(
                filter.필드,
                nameof(공동주문관점항목응답.공동원장Id),
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!await 현재관점.조회Async(request, cancellationToken))
        {
            throw new InvalidOperationException(현재관점.오류메시지 ?? "공동주문 목록을 조회하지 못했습니다.");
        }
    }
}
