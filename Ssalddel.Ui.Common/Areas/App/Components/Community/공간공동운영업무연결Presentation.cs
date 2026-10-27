using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Community;

public sealed record 공간공동운영업무단계(string Key, string Title, string Roles, string Input, string Result,
    string Boundary, IReadOnlyList<string> PreviousKeys, IReadOnlyList<string> NextKeys);

public sealed record 공간공동운영업무노드(공간공동운영업무단계 Stage,
    IReadOnlyList<공간공동운영통합원천Dto> Sources, IReadOnlyList<공간공동운영통합주문Dto> Orders);

public sealed record 공간공동운영업무연결Model
{
    public bool Available { get; init; }
    public string Message { get; init; } = "현재 업무 연결 자료를 확인해 주세요.";
    public 공간공동운영통합회차Dto? Cycle { get; init; }
    public IReadOnlyList<공간공동운영업무노드> Nodes { get; init; } = [];
    public IReadOnlyList<공간공동운영통합원천Dto> UnmappedSources { get; init; } = [];
    public IReadOnlyList<공간공동운영통합관계Dto> Relations { get; init; } = [];
    public long SnapshotRevision { get; init; }
}

/// <summary>현재 허용된 조회 자료만 절차 안내에 나란히 놓습니다. 실행 순서·완료·새 업무를 만들지 않습니다.</summary>
public static class 공간공동운영업무연결Presentation
{
    public static IReadOnlyList<공간공동운영업무단계> Stages { get; } = Array.AsReadOnly<공간공동운영업무단계>(
    [
        new("production", "생산·공급", "생산자·공급자", "공급할 상품과 참여 조건", "공급 조건과 입고 약속 확인",
            "공급 의향이나 약속은 실제 생산·입고 완료가 아니에요.", [], ["inbound"]),
        new("inbound", "입고·검수", "공급자·입고 검수 담당자", "입고 계획과 도착한 물품", "수령 수량과 물품 근거 확인",
            "입고 자료 확인과 개별 검수 절차의 완료는 구분해요.", ["production"], ["storage"]),
        new("storage", "보관·재고", "공간 제공자·보관 담당자", "공간 동의와 보관 조건", "보관 조건과 재고 업무의 참고 근거 확인",
            "공간·보관 합의가 실제 적재나 가용 재고를 보장하지 않아요.", ["inbound"], ["outbound"]),
        new("order", "주문", "주문자·판매자", "주문자가 선택한 상품과 수량·수령 조건", "주문 조건과 전문 원장 연결 확인",
            "주문 협의나 원장 연결은 배송·수령·지급 완료가 아니에요.", [], ["outbound"]),
        new("outbound", "피킹·출고", "피킹 담당자·출고 담당자", "주문과 출고 묶음", "전문 업무의 피킹·출고 근거 확인",
            "출고 묶음 자료만으로 개별 피킹·포장이 완료됐다고 판단하지 않아요.", ["storage", "order"], ["delivery"]),
        new("delivery", "배송·수령", "기사·주문자", "인계할 물품과 수령 조건", "기사 전달과 고객 수령 근거 확인",
            "예약·기사 전달·고객 수령은 각각의 전문 확인을 따라요.", ["outbound"], ["settlement"]),
        new("settlement", "정산", "업무 당사자·정산 담당자", "수행·수령과 약정 근거", "정산·지급 준비와 다음 운영 검토",
            "지급 준비 확인은 실입금이 아니며 다음 회차를 자동 시작하지 않아요.", ["delivery"], [])
    ]);

    public static 공간공동운영업무연결Model Build(공간공동운영상세Response detail,
        공간공동운영통합Response snapshot, string? cycleId)
    {
        if (detail is null || snapshot is null || string.IsNullOrWhiteSpace(detail.LedgerId)
            || detail.LedgerId != snapshot.LedgerId || detail.Revision <= 0 || detail.Revision != snapshot.Revision
            || detail.ExecutionMode != snapshot.ExecutionMode || detail.ExecutionMode is not ("Simulation" or "Operational"))
            return Unavailable("같은 공동운영의 최신 자료를 다시 불러와 주세요.");
        var selectedId = string.IsNullOrWhiteSpace(cycleId) ? detail.Cycles.OrderByDescending(cycle => cycle.Number).FirstOrDefault()?.CycleId : cycleId;
        var detailRows = detail.Cycles.Where(cycle => cycle.CycleId == selectedId).ToArray();
        var cycleRows = snapshot.Cycles.Where(cycle => cycle.CycleId == selectedId).ToArray();
        if (string.IsNullOrWhiteSpace(selectedId) || detailRows.Length != 1 || cycleRows.Length != 1)
            return Unavailable("선택한 회차의 현재 자료를 확인하지 못했어요.");
        var original = cycleRows[0]; var current = detailRows[0];
        if (original.NodeKey != "cycle:" + selectedId || original.Number != current.Number
            || original.TermsRevision != current.TermsRevision || original.StatusCode != current.StatusCode)
            return Unavailable("회차 자료가 달라졌어요. 다시 불러와 주세요.");

        var references = current.References.GroupBy(reference => SourceKey(reference.KindCode, reference.ReferenceId))
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var sourceKeys = original.SharedSourceKeys.Distinct(StringComparer.Ordinal).ToArray();
        if (sourceKeys.Any(key => !references.ContainsKey(key)) || sourceKeys.Length != original.SharedSourceKeys.Count
            || original.Orders.Any(order => order.OrderRequestId == Guid.Empty || order.NodeKey != "order:" + order.OrderRequestId.ToString("N"))
            || original.Orders.GroupBy(order => order.NodeKey).Any(group => group.Count() != 1))
            return Unavailable("이 회차의 자료 연결을 다시 확인해야 해요.");

        var sources = new Dictionary<string, 공간공동운영통합원천Dto>(StringComparer.Ordinal);
        foreach (var key in sourceKeys)
        {
            var reference = references[key][0];
            var found = snapshot.SharedSources.Where(source => source.NodeKey == key).ToArray();
            var source = found.Length == 1 && references[key].Length == 1
                && SourceKey(found[0].KindCode, found[0].ReferenceId) == key ? found[0] : null;
            var verified = source?.Verified == true && StageKey(reference.KindCode) is not null
                && source.StatusCode is not ("source-context-conflict" or "source-unverified" or "execution-mode-mismatch"
                    or "not-visible" or "unavailable" or "disabled" or "unsupported" or "lineage-mismatch");
            sources[key] = new()
            {
                NodeKey = key, KindCode = reference.KindCode, ReferenceId = reference.ReferenceId,
                Label = SourceLabel(reference.KindCode), StatusCode = source?.StatusCode ?? "source-unverified",
                Verified = verified, Completed = verified && source!.Completed,
                RequiresAttention = !verified || source!.RequiresAttention,
                Href = verified ? 공간공동운영Presentation.SafeWorkHref(source!.Href) ?? "" : ""
            };
        }
        var orders = snapshot.OrdersReadStatusCode == "current" ? original.Orders.Select(order => new 공간공동운영통합주문Dto
        {
            NodeKey = order.NodeKey, OrderRequestId = order.OrderRequestId, TermsVersion = order.TermsVersion,
            ProductName = order.ProductName, AgreementStatusCode = order.AgreementStatusCode,
            RootOrderLedgerId = order.RootOrderLedgerId, RootReadStatusCode = order.RootReadStatusCode,
            Children = order.RootReadStatusCode == "verified-link"
                ? order.Children.Select(child => child.NodeKey).Distinct(StringComparer.Ordinal)
                    .Where(key => sources.TryGetValue(key, out var source) && source.Verified)
                    .Select(key => sources[key]).ToArray() : []
        }).ToArray() : [];
        var visibleOrders = orders.ToDictionary(order => order.NodeKey, StringComparer.Ordinal);
        var rootKey = "cooperation:" + detail.LedgerId;
        var relations = snapshot.Relations.Where(relation => relation.RelationCode switch
        {
            "cooperation-cycle" => relation.FromKey == rootKey && relation.ToKey == original.NodeKey,
            "cycle-source-reference" => relation.FromKey == original.NodeKey && sources.ContainsKey(relation.ToKey),
            "cycle-order-agreement" => relation.FromKey == original.NodeKey && visibleOrders.ContainsKey(relation.ToKey),
            "source-lineage" => visibleOrders.TryGetValue(relation.FromKey, out var order)
                && order.Children.Any(source => source.NodeKey == relation.ToKey),
            _ => false
        }).DistinctBy(relation => (relation.FromKey, relation.ToKey, relation.RelationCode))
            .Select(relation => new 공간공동운영통합관계Dto
            { FromKey = relation.FromKey, ToKey = relation.ToKey, RelationCode = relation.RelationCode }).ToArray();
        return new()
        {
            Available = true, Message = snapshot.OrdersReadStatusCode == "current"
                ? "점선은 절차 안내이며, 연결 자료는 이 회차에서 확인한 참고 근거예요."
                : "절차는 안내로 볼 수 있어요. 당사자 주문 자료는 현재 다시 확인해야 해요.",
            SnapshotRevision = snapshot.Revision,
            Cycle = new() { NodeKey = original.NodeKey, CycleId = original.CycleId, Number = original.Number,
                TermsRevision = original.TermsRevision, StatusCode = original.StatusCode,
                SharedSourceKeys = sourceKeys, Orders = orders },
            Nodes = Stages.Select(stage => new 공간공동운영업무노드(stage,
                sources.Values.Where(source => StageKey(source.KindCode) == stage.Key).ToArray(),
                stage.Key == "order" ? orders : [])).ToArray(),
            UnmappedSources = sources.Values.Where(source => StageKey(source.KindCode) is null).ToArray(),
            Relations = relations
        };
    }

    public static string SourceStatus(공간공동운영통합원천Dto source)
        => !source.Verified ? "현재 자료 확인 필요" : source.RequiresAttention ? "문제 확인 필요"
            : source.KindCode == "Settlement" ? source.Completed ? "지급 준비 확인 · 실제 지급 확인 별도" : "정산 자료 확인 · 지급 준비 확인 전"
            : source.KindCode == "RootOrderLedger" ? "주문 원장 연결 확인 · 실물 완료와 별도"
            : source.Completed ? "원 업무의 해당 근거 확인 · 절차 전체 완료와 별도" : "원 업무 자료 확인 · 진행 상태 확인 필요";

    public static string OrderStatus(string? status) => status switch
    {
        "proposed" => "주문 조건 협의 중", "handoff-pending" => "전문 업무 연결 확인 중",
        "confirmed" => "전문 주문으로 인계 · 배송·수령과 별도", _ => "현재 주문 조건 확인 필요"
    };

    public static string RootStatus(string? status) => status switch
    {
        "not-created" => "원 주문 준비 전", "not-visible" => "내 권한으로 원 주문 확인 필요",
        "lineage-unverified" => "원 주문과 자료 연결 확인 필요", "verified-link" => "원 주문 연결 확인",
        "temporarily-unavailable" => "원 주문 자료를 잠시 확인하지 못했어요.", _ => "원 주문의 현재 연결 확인 필요"
    };

    public static string RelationLabel(string? relationCode) => relationCode switch
    {
        "cooperation-cycle" => "운영 회차 연결", "cycle-order-agreement" => "당사자 주문 연결",
        "cycle-source-reference" or "source-lineage" => "참고 자료 연결", _ => "연결 확인 필요"
    };

    private static string? StageKey(string kind) => kind switch
    {
        "InboundPlan" => "inbound", "DistributedSite" or "StorageAgreement" => "storage",
        "RootOrderLedger" => "order", "DistributedWave" => "outbound",
        "DistributedOrder" or "MartOrder" => "delivery", "Settlement" => "settlement", _ => null
    };

    private static string SourceLabel(string kind) => kind switch
    {
        "InboundPlan" => "입고 수령 자료", "DistributedSite" => "공간 사용·픽업 동의",
        "StorageAgreement" => "물품·공간 보관 합의", "RootOrderLedger" => "기존 주문 원장",
        "DistributedWave" => "출고 묶음과 수령 결과", "DistributedOrder" => "배송·고객 수령 자료",
        "MartOrder" => "마트 고객 수령 근거", "Settlement" => "본인 정산·지급 준비", _ => "분류 확인이 필요한 연결 자료"
    };

    private static string SourceKey(string kind, string id) => "source:" + kind + ":" + id;
    private static 공간공동운영업무연결Model Unavailable(string message) => new() { Message = message };
}
