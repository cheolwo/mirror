using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using 살뜰.Data;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.Operations;

public interface I마트라스트마일원천Adapter
{
    Task<마트라스트마일원천상태사본> 조회Async(string 주문참조번호, CancellationToken cancellationToken = default);
}

/// <summary>
/// 서버 내부 준비 판정용 사본입니다. 사용자 ID·상품명·주소·위치·원문 오류를 포함하지 않습니다.
/// 원천 결속과 픽업 준비는 서로 다르며 어느 값도 기사 배정·픽업 권한을 부여하지 않습니다.
/// 여러 읽기의 결과이므로 원자적인 권한 증빙이 아닙니다. 최종 소비자는 업무 트랜잭션 안에서 현재 상태를 재검증해야 합니다.
/// </summary>
public sealed record 마트라스트마일원천상태사본(
    bool 원천결속됨,
    bool 픽업준비완료,
    string 결과Code,
    int 출고건수 = 0,
    long 총수량 = 0,
    DateTime? 포장완료시각Utc = null);

public static class 마트라스트마일원천결과Codes
{
    public const string 원천미결속 = MartLastMileDispatchReadinessCodes.SourceNotBound;
    public const string 준비대기 = "MartLastMilePreparationNotReady";
    public const string 준비완료 = "MartLastMilePrepared";
}

/// <summary>
/// 실제 마트 주문·출고·피킹·포장 근거만 읽습니다. SaveChanges, 배차·외부 조회·샘플 fallback은 없습니다.
/// 인증된 업무 UseCase와 readiness의 내부 의존성으로만 사용하며 공개 조회 권한을 새로 만들지 않습니다.
/// 읽기 상태 사본은 원자적인 권한 증빙이 아니며 최종 소비자의 트랜잭션 안 현재 상태 재검증을 대체하지 않습니다.
/// </summary>
public sealed class 마트라스트마일원천Adapter(SsalddelContext db) : I마트라스트마일원천Adapter
{
    public async Task<마트라스트마일원천상태사본> 조회Async(
        string 주문참조번호,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(주문참조번호)) return 미결속();
        var reference = 주문참조번호.Trim();
        var orders = await db.마트주문.AsNoTracking()
            .Where(order => order.주문참조번호 == reference).Take(2).ToListAsync(cancellationToken);
        if (orders.Count != 1 || string.IsNullOrWhiteSpace(orders[0].판매자UserId)
            || string.IsNullOrWhiteSpace(orders[0].주문자UserId)) return 미결속();

        var order = orders[0];
        var outbounds = await db.출고예정.AsNoTracking()
            .Where(plan => plan.주문참조번호 == reference).ToListAsync(cancellationToken);
        if (outbounds.Count == 0 || outbounds.Any(plan => plan.판매자UserId != order.판매자UserId
                || plan.주문자UserId != order.주문자UserId)) return 미결속();

        // 기존 readiness의 기본 원천 결속 의미를 보존합니다. 상품/피킹/포장 결손은 준비 미완료로 별도 판독합니다.
        var quantity = outbounds.Sum(plan => (long)plan.수량);
        // 첫 안전 조각에서는 하나의 포장 이력을 여러 출고의 수량 근거로 중복 인정하지 않습니다.
        // 같은 입고 상품을 나누어 출고하는 업무의 집계 근거는 별도 계약이 마련될 때까지 보류합니다.
        if (outbounds.GroupBy(plan => plan.입고상품Id).Any(group => group.Key is > 0 && group.Count() > 1))
            return 준비대기(outbounds.Count, quantity);
        if (await 마트주문피킹완료Guard.주문차단사유Async(db, reference, cancellationToken) is not null)
            return 준비대기(outbounds.Count, quantity);

        var packingTasks = await db.피킹포장작업.AsNoTracking()
            .Where(task => task.주문참조번호 == reference && task.작업유형 == 피킹포장작업유형.포장
                && task.상태 != 피킹포장작업상태.취소).ToListAsync(cancellationToken);
        if (packingTasks.Count == 0 || packingTasks.Any(task => task.출고예정Id is null
                || !outbounds.Any(plan => plan.Id == task.출고예정Id)
                || string.IsNullOrWhiteSpace(task.작업Key))
            || packingTasks.Select(task => task.작업Key).Distinct(StringComparer.Ordinal).Count() != packingTasks.Count)
            return 준비대기(outbounds.Count, quantity);

        DateTime? latestPackedAt = null;
        foreach (var plan in outbounds)
        {
            if (plan.입고상품Id is null or <= 0 || plan.상태 == 출고상태.취소 || plan.수량 <= 0)
                return 준비대기(outbounds.Count, quantity);
            var inventory = await db.입고상품.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == plan.입고상품Id, cancellationToken);
            if (inventory is null || !inventory.상태.StartsWith("포장완료", StringComparison.Ordinal)
                || inventory.창고Id != plan.출고창고Id || inventory.SKU != plan.SKU || inventory.상품명 != plan.상품명)
                return 준비대기(outbounds.Count, quantity);

            // 상태 문자열만으로 완료하지 않고 최신 실제 포장 이력의 수량과 피킹 이후 시각을 확인합니다.
            var history = await db.재고이력.AsNoTracking()
                .Where(item => item.입고상품Id == inventory.Id && item.이력유형 == "포장")
                .OrderByDescending(item => item.처리일시).ThenByDescending(item => item.Id)
                .Select(item => new { item.변경후수량, item.처리일시 }).FirstOrDefaultAsync(cancellationToken);
            var pickedAt = await db.피킹포장작업.AsNoTracking()
                .Where(task => task.주문참조번호 == reference && task.출고예정Id == plan.Id
                    && task.작업유형 == 피킹포장작업유형.피킹 && task.상태 == 피킹포장작업상태.완료)
                .Select(task => task.완료일시Utc).MaxAsync(cancellationToken);
            var matching = packingTasks.Where(task => task.출고예정Id == plan.Id).ToArray();
            if (history is null || history.변경후수량 != plan.수량 || !pickedAt.HasValue
                || AsUtc(history.처리일시) < AsUtc(pickedAt.Value)
                || matching.Length == 0 || matching.Any(task => task.입고상품Id != inventory.Id
                    || task.창고Id != plan.출고창고Id || task.SKU != plan.SKU || task.상품명 != plan.상품명
                    || task.수량 <= 0 || task.상태 != 피킹포장작업상태.완료 || !task.완료일시Utc.HasValue
                    || AsUtc(task.완료일시Utc.Value) < AsUtc(history.처리일시))
                || matching.Sum(task => (long)task.수량) != plan.수량)
                return 준비대기(outbounds.Count, quantity);

            var packedAt = AsUtc(history.처리일시);
            latestPackedAt = !latestPackedAt.HasValue || packedAt > latestPackedAt.Value ? packedAt : latestPackedAt;
        }

        return new(true, true, 마트라스트마일원천결과Codes.준비완료, outbounds.Count, quantity, latestPackedAt);
    }

    private static 마트라스트마일원천상태사본 미결속() => new(false, false, 마트라스트마일원천결과Codes.원천미결속);
    private static 마트라스트마일원천상태사본 준비대기(int count, long quantity)
        => new(true, false, 마트라스트마일원천결과Codes.준비대기, count, quantity);
    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
