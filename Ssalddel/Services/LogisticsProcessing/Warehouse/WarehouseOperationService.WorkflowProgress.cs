using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Runtime.ExceptionServices;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.Services.Notifications;
using 살뜰.도메인.설정;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

/// <summary>창고 원장이 확인한 단계의 관계자와 복귀 화면만 결정한다. 저장·발송은 하지 않는다.</summary>
internal static class 창고업무진행알림
{
    /// <summary>관계형 재시도는 상태 조회부터 다시 시작한다. 호출자가 연 transaction은 소유하지 않는다.</summary>
    public static async Task<T> 저장단위Async<T>(SsalddelContext db, Func<Action, Task<T>> operation)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null) return await operation(() => { });
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                var committed = false;
                var before = db.ChangeTracker.Entries().ToDictionary(entry => entry.Entity,
                    entry => new 추적사본(entry.State, entry.CurrentValues.Clone(), entry.OriginalValues.Clone()),
                    ReferenceEqualityComparer.Instance);
                try { return await operation(() => committed = true); }
                catch (Exception error) when (committed)
                {
                    // 전략에는 재시도할 수 없는 봉투만 보낸다. 감사/Event 오류는 전략 밖에서 원래 예외로 반환한다.
                    throw new 저장후후속실패(ExceptionDispatchInfo.Capture(error));
                }
                catch
                {
                    // 롤백 전 값을 정본으로 재사용하지 않는다. 호출 전부터 대기 중이던 별도 변경만 보존한다.
                    foreach (var entry in db.ChangeTracker.Entries().ToArray())
                    {
                        if (!before.TryGetValue(entry.Entity, out var snapshot) || snapshot.State == EntityState.Unchanged)
                            entry.State = EntityState.Detached;
                        else
                        {
                            entry.CurrentValues.SetValues(snapshot.Current);
                            entry.OriginalValues.SetValues(snapshot.Original);
                            entry.State = snapshot.State;
                        }
                    }
                    throw;
                }
            });
        }
        catch (저장후후속실패 error)
        {
            error.Original.Throw();
            throw;
        }
    }

    private sealed record 추적사본(EntityState State, PropertyValues Current, PropertyValues Original);
    // 원래 transient 오류를 InnerException으로 노출하지 않아 실행 전략의 자동 재시도를 막는다.
    private sealed class 저장후후속실패(ExceptionDispatchInfo original) : Exception("완료 후 후속 처리를 확인해 주세요.")
    {
        public ExceptionDispatchInfo Original { get; } = original;
    }

    public static async Task<IReadOnlyList<Command알림Outbox>> 입고신청Async(
        SsalddelContext db, 입고요청 inbound, string actor, CancellationToken ct)
    {
        var clientId = inbound.일반입고클라이언트요청Id ?? inbound.현장입고클라이언트요청Id;
        // DB 생성 ID가 아직 없으므로 이미 접수한 원요청 ID를 쓴다. 구형 무식별 요청은 매번 새 신청이다.
        var source = clientId is { } id ? $"inbound-request:{id:N}" : $"warehouse:{inbound.창고Id}";
        var authorHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(inbound.주문자UserId)));
        var occurrence = clientId is { } known ? $"requested:{known:N}:{authorHash}" : $"requested:{Guid.NewGuid():N}";
        var owner = await 창고소유자Async(db, inbound.창고Id, ct);
        return 예약(db, "warehouse.requested", source, occurrence, actor, inbound.CreatedAt,
            [(owner, InboundRequestPageRoutes.Root)]);
    }

    public static async Task<IReadOnlyList<Command알림Outbox>> 입고수령Async(
        SsalddelContext db, 입고요청 inbound, string actor, DateTime occurredAt, CancellationToken ct)
    {
        var owner = await 창고소유자Async(db, inbound.창고Id, ct);
        return 예약(db, "warehouse.received", $"inbound:{inbound.Id}", "received", actor, occurredAt,
            [(owner, InboundRequestPageRoutes.DetailFor(inbound.Id)),
             (inbound.주문자UserId, InboundRequestPageRoutes.DetailFor(inbound.Id)),
             (inbound.판매자UserId, "/shipper/warehouse/inventory")]);
    }

    public static async Task<IReadOnlyList<Command알림Outbox>> 재고Async(
        SsalddelContext db, 입고상품 item, string stage, string occurrence,
        string actor, DateTime occurredAt, CancellationToken ct)
    {
        var owner = await 창고소유자Async(db, item.창고Id, ct);
        var managerPath = stage switch
        {
            "warehouse.inspected" => InboundInspectionPageRoutes.DetailFor(item.Id),
            "warehouse.stored" => "/work/inbound/put-away",
            "warehouse.packed" => "/work/outbound/packing",
            _ => "/work/inbound/put-away"
        };
        return 예약(db, stage, $"inventory:{item.Id}", occurrence, actor, occurredAt,
            [(owner, managerPath), (item.소유자UserId, "/shipper/warehouse/inventory"),
             (item.판매자UserId, "/shipper/warehouse/inventory")]);
    }

    public static async Task<IReadOnlyList<Command알림Outbox>> 피킹Async(
        SsalddelContext db, 피킹포장작업 task, string stage, string actor,
        DateTime occurredAt, CancellationToken ct)
    {
        var owner = await 창고소유자Async(db, task.창고Id, ct);
        var recipients = new List<(string? UserId, string Path)>
        {
            (owner, PickingTaskPageRoutes.DetailFor(task.작업Key)),
            (task.작업자UserId, PickingTaskPageRoutes.DetailFor(task.작업Key))
        };
        if (stage == "warehouse.picked")
        {
            if (task.입고상품Id is > 0)
            {
                var item = await db.입고상품.AsNoTracking().SingleOrDefaultAsync(
                    item => item.Id == task.입고상품Id && item.창고Id == task.창고Id, ct);
                if (item is not null)
                {
                    recipients.Add((item.소유자UserId, "/shipper/warehouse/inventory"));
                    recipients.Add((item.판매자UserId, "/shipper/warehouse/inventory"));
                }
            }
            if (!string.IsNullOrWhiteSpace(task.다음작업Key))
            {
                var next = await db.피킹포장작업.AsNoTracking().SingleOrDefaultAsync(
                    next => next.작업Key == task.다음작업Key && next.창고Id == task.창고Id
                        && next.작업유형 == 피킹포장작업유형.포장 && next.상태 != 피킹포장작업상태.취소, ct);
                if (next is not null && string.Equals(next.이전작업Key, task.작업Key, StringComparison.Ordinal)
                    && await 창고작업권한Async(db, task.창고Id, next.작업자UserId, owner, ct))
                    recipients.Add((next.작업자UserId, "/work/outbound/packing"));
            }
        }
        return 예약(db, stage, $"picking:{task.작업Key}", stage == "warehouse.picking" ? "started" : "completed",
            actor, occurredAt, recipients);
    }

    public static async Task<IReadOnlyList<Command알림Outbox>> 출고Async(
        SsalddelContext db, 출고예정 plan, 입고상품 item, string stage,
        string actor, DateTime occurredAt, CancellationToken ct)
    {
        var owner = await 창고소유자Async(db, plan.출고창고Id, ct);
        // 준비 단계의 출고 ID는 아직 DB가 부여하지 않았다. 해당 재고의 준비 사실과 이후 실제 출고 ID를 구분한다.
        var source = plan.Id > 0 ? $"outbound:{plan.Id}" : $"inventory:{item.Id}";
        return 예약(db, stage, source, stage == "warehouse.ready" ? $"ready:{plan.CreatedAt.Ticks}" : "handed-over", actor, occurredAt,
            [(owner, "/work/outbound/handoff"), (item.소유자UserId, "/shipper/warehouse/inventory"),
             (item.판매자UserId, "/shipper/warehouse/inventory")]);
    }

    public static void 분리(SsalddelContext db, IEnumerable<Command알림Outbox> reservations)
    {
        foreach (var row in reservations) db.Entry(row).State = EntityState.Detached;
    }

    private static async Task<string?> 창고소유자Async(SsalddelContext db, long warehouseId, CancellationToken ct)
        => await db.창고.AsNoTracking().Where(warehouse => warehouse.Id == warehouseId)
            .Select(warehouse => warehouse.소유자UserId).SingleOrDefaultAsync(ct);

    private static async Task<bool> 창고작업권한Async(
        SsalddelContext db, long warehouseId, string userId, string? owner, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        if (string.Equals(userId, owner, StringComparison.Ordinal)) return true;
        var memberships = await db.창고사용자.AsNoTracking()
            .Where(member => member.창고Id == warehouseId && member.UserId == userId)
            .Select(member => member.UserId).ToArrayAsync(ct);
        return memberships.Contains(userId, StringComparer.Ordinal);
    }

    private static IReadOnlyList<Command알림Outbox> 예약(
        SsalddelContext db, string stage, string source, string occurrence, string actor,
        DateTime occurredAt, IEnumerable<(string? UserId, string Path)> recipients)
    {
        var result = new List<Command알림Outbox>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var (userId, path) in recipients)
            {
                if (string.IsNullOrWhiteSpace(userId) || string.Equals(userId, actor, StringComparison.Ordinal) || !seen.Add(userId)) continue;
                var reserved = 업무진행알림Recorder.Reserve(db, stage, source, occurrence, userId, path,
                    occurredAt.Kind == DateTimeKind.Utc ? occurredAt : DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc));
                if (reserved is not null) result.Add(reserved);
            }
            return result;
        }
        catch
        {
            // 두 번째 수신자 처리에서 실패해도 이 예약이 추가한 행만 정리한다. 다른 요청의 대기 행은 보존한다.
            foreach (var row in result)
                if (db.Entry(row).State == EntityState.Added) db.Entry(row).State = EntityState.Detached;
            throw;
        }
    }
}
