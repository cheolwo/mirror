using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

/// <summary>마트 주문의 모든 상품에 대한 피킹 근거를 확인합니다. 일반 창고 포장에는 새 조건을 부과하지 않습니다.</summary>
public static class 마트주문피킹완료Guard
{
    public static async Task<string?> 입고상품차단사유Async(
        SsalddelContext db,
        입고상품 item,
        CancellationToken cancellationToken)
    {
        var references = await db.출고예정.AsNoTracking()
            .Where(plan => plan.입고상품Id == item.Id)
            .Select(plan => plan.주문참조번호).ToListAsync(cancellationToken);
        references.AddRange(await db.피킹포장작업.AsNoTracking()
            .Where(task => task.입고상품Id == item.Id)
            .Select(task => task.주문참조번호).ToListAsync(cancellationToken));
        references.AddRange(await db.입고요청.AsNoTracking()
            .Where(inbound => inbound.Id == item.입고요청Id)
            .Select(inbound => inbound.주문참조번호).ToListAsync(cancellationToken));

        foreach (var reference in references.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal))
        {
            if (await 주문차단사유Async(db, reference, cancellationToken) is { } blocked)
                return blocked;
        }

        return null;
    }

    public static async Task<string?> 주문차단사유Async(
        SsalddelContext db,
        string 주문참조번호,
        CancellationToken cancellationToken)
    {
        var orders = await db.마트주문.AsNoTracking().Include(order => order.상품목록)
            .Where(order => order.주문참조번호 == 주문참조번호).Take(2).ToListAsync(cancellationToken);
        if (orders.Count == 0) return null;
        if (orders.Count != 1) return "살뜰마트 주문 연결을 확인한 뒤 포장해 주세요.";
        var order = orders[0];
        var lines = order.상품목록;
        if (order.상태.Contains("취소", StringComparison.Ordinal) || lines.Count == 0
            || lines.Any(line => line.출고예정Id is null or <= 0 || line.수량 <= 0
                || string.IsNullOrWhiteSpace(line.SKU) || string.IsNullOrWhiteSpace(line.상품명)
                || line.상태.Contains("취소", StringComparison.Ordinal))
            || lines.Select(line => line.출고예정Id).Distinct().Count() != lines.Count)
            return "살뜰마트 주문의 상품·출고 연결을 확인한 뒤 포장해 주세요.";

        var plans = await db.출고예정.AsNoTracking()
            .Where(plan => plan.주문참조번호 == 주문참조번호 && plan.상태 != 출고상태.취소)
            .ToListAsync(cancellationToken);
        if (plans.Count != lines.Count)
            return "살뜰마트 주문 전체의 출고 연결이 확인되지 않았습니다.";
        var tasks = await db.피킹포장작업.AsNoTracking()
            .Where(task => task.주문참조번호 == 주문참조번호 && task.작업유형 == 피킹포장작업유형.피킹
                && task.상태 != 피킹포장작업상태.취소).ToListAsync(cancellationToken);
        if (tasks.Count == 0 || tasks.Any(task => task.출고예정Id is null
                || !plans.Any(plan => plan.Id == task.출고예정Id)
                || string.IsNullOrWhiteSpace(task.작업Key) || string.IsNullOrWhiteSpace(task.라인Key))
            || tasks.Select(task => task.작업Key).Distinct(StringComparer.Ordinal).Count() != tasks.Count)
            return "살뜰마트 주문 전체의 피킹 작업 근거를 확인해 주세요.";

        foreach (var line in lines)
        {
            var plan = plans.SingleOrDefault(candidate => candidate.Id == line.출고예정Id);
            if (plan?.입고상품Id is null or <= 0 || plan.출고창고Id <= 0
                || plan.SKU != line.SKU || plan.상품명 != line.상품명 || plan.수량 != line.수량
                || plan.판매자UserId != order.판매자UserId || plan.주문자UserId != order.주문자UserId)
                return "살뜰마트 주문 상품과 출고 근거가 일치하지 않습니다.";
            var inventory = await db.입고상품.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == plan.입고상품Id, cancellationToken);
            if (inventory is null || inventory.창고Id != plan.출고창고Id
                || inventory.SKU != line.SKU || inventory.상품명 != line.상품명)
                return "살뜰마트 피킹 대상 재고를 확인해 주세요.";
            var picked = tasks.Where(task => task.출고예정Id == plan.Id).ToArray();
            if (picked.Length == 0 || picked.Any(task => task.입고상품Id != inventory.Id
                    || task.창고Id != plan.출고창고Id || task.SKU != line.SKU || task.상품명 != line.상품명
                    || task.수량 <= 0 || task.상태 != 피킹포장작업상태.완료 || !task.완료일시Utc.HasValue)
                || picked.Sum(task => (long)task.수량) != line.수량)
                return "살뜰마트 주문의 모든 상품 피킹을 완료한 뒤 포장해 주세요.";
        }

        return null;
    }
}
