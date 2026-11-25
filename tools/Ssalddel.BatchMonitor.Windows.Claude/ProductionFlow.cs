namespace Ssalddel.BatchMonitor.Windows;

// 생산·소비 두 축과 배달 연결의 병목 비교 (PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION r134).
// 이번 회차 단위로 주문(소비), 매장 준비 여력(생산), 기사 배송 여력(연결)을 지역별로 비교한다.

internal enum FlowBottleneck { Balanced, Demand, Preparation, Delivery }

internal sealed record DistrictFlowInput(string District, int Orders, int Kitchens, int ReadyRiders);

internal sealed record DistrictFlow(string District, int Orders, int Prepare, int Deliver, int Limit,
    FlowBottleneck Bottleneck, string Focus);

internal sealed record FlowSummary(IReadOnlyList<DistrictFlow> Districts, int Orders, int Prepare, int Deliver,
    int Limit, FlowBottleneck Bottleneck, string Focus);

internal static class ProductionFlow
{
    internal static DistrictFlow Evaluate(DistrictFlowInput input)
    {
        // 현재 엔진은 매장마다 회차당 한 건을 조리하고, 기사는 회차당 한 건을 맡는다.
        var prepare = Math.Max(0, input.Kitchens);
        var deliver = Math.Max(0, input.ReadyRiders);
        var orders = Math.Max(0, input.Orders);
        var limit = Math.Min(orders, Math.Min(prepare, deliver));
        FlowBottleneck bottleneck;
        string focus;
        if (limit < orders && deliver <= prepare)
        {
            bottleneck = FlowBottleneck.Delivery;
            focus = $"배송 여력 부족 · 주문 {orders}건에 출발 가능한 기사 {deliver}명. 기사 휴식·점검이나 다른 기사를 보세요.";
        }
        else if (limit < orders)
        {
            bottleneck = FlowBottleneck.Preparation;
            focus = $"준비 여력 부족 · 주문 {orders}건에 준비 가능 {prepare}건. 조리·준비 여력을 확보하세요.";
        }
        else if (prepare == orders && deliver == orders)
        {
            bottleneck = FlowBottleneck.Balanced;
            focus = "준비·주문·배송이 맞습니다. 실제 수령과 정산 결과를 보세요.";
        }
        else
        {
            bottleneck = FlowBottleneck.Demand;
            focus = $"주문보다 여력이 큽니다 · 준비 {prepare - orders}건·배송 {Math.Max(0, deliver - orders)}건 여유. 수요 연결 여지를 보세요.";
        }

        return new(input.District, orders, prepare, deliver, limit, bottleneck, focus);
    }

    internal static FlowSummary Summarize(IEnumerable<DistrictFlowInput> inputs)
    {
        var districts = inputs.Select(Evaluate).ToArray();
        var orders = districts.Sum(item => item.Orders);
        var limit = districts.Sum(item => item.Limit);
        var blocked = districts.FirstOrDefault(item => item.Bottleneck is FlowBottleneck.Delivery or FlowBottleneck.Preparation);
        var bottleneck = blocked?.Bottleneck ??
            (districts.All(item => item.Bottleneck == FlowBottleneck.Balanced) ? FlowBottleneck.Balanced : FlowBottleneck.Demand);
        var focus = blocked is not null
            ? $"먼저 볼 곳 · {blocked.District} {blocked.Focus}"
            : $"이번 회차 주문 {orders}건을 모두 완료할 여력이 있습니다.";
        return new(Array.AsReadOnly(districts), orders, districts.Sum(item => item.Prepare), districts.Sum(item => item.Deliver),
            limit, bottleneck, focus);
    }
}
