using Microsoft.EntityFrameworkCore;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    // 수령·거절·취소·합의 수량 조정은 같은 최종 사실에서 같은 계획 상태를 얻습니다.
    // 이 판정은 실재고, 사진 완료 근거와 원천 예정 수량을 변경하지 않습니다.
    private async Task RefreshInboundCompletion(분산입고계획Record plan, CancellationToken ct)
    {
        var lots = await Query<분산재고LotRecord>().Where(x => x.PlanId == plan.PlanId).ToArrayAsync(ct);
        plan.StatusCode = InboundCompletionStatus(lots);
    }

    private static string InboundCompletionStatus(IReadOnlyList<분산재고LotRecord> lots)
    {
        var open = lots.Where(x => !ClosedInbound(x)).ToArray();
        if (lots.Count > 0 && open.Length == 0) return "Cancelled";
        if (open.Length > 0 && open.All(x => x.ReceivedQuantity == x.ExpectedQuantity)) return "Received";
        return lots.Any(x => x.ReceivedQuantity > 0) ? "PartiallyReceived" : "Planned";
    }
}
