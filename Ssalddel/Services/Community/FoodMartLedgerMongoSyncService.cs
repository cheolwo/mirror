using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Food;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using 살뜰.Data;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;
using 살뜰.도메인.공통;
using 살뜰.Services.Dispatch.Engine;
using Ssalddel.Services.PrivacyRetention;

namespace Ssalddel.Services.Community;

public interface I음식마트원장Mongo동기화Service
{
    Task<커뮤니티원장Dto?> 음식주문동기화Async(
        음식주문응답 주문,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task<커뮤니티원장Dto?> 출고원장동기화Async(
        IReadOnlyList<출고예정> 출고목록,
        IReadOnlyList<입고요청> 입고목록,
        string updatedBy,
        string? 현재단계Key = null,
        string? 원장템플릿Key = null,
        CancellationToken cancellationToken = default);
}

public sealed class 음식마트원장Mongo동기화Service : I음식마트원장Mongo동기화Service
{
    private readonly I커뮤니티원장저장소 _원장저장소;
    private readonly SsalddelContext _db;
    private readonly ILogger<음식마트원장Mongo동기화Service> _logger;

    public 음식마트원장Mongo동기화Service(
        I커뮤니티원장저장소 원장저장소,
        SsalddelContext db,
        ILogger<음식마트원장Mongo동기화Service> logger)
    {
        _원장저장소 = 원장저장소;
        _db = db;
        _logger = logger;
    }

    public async Task<커뮤니티원장Dto?> 음식주문동기화Async(
        음식주문응답 주문,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var request = 음식마트원장Mongo동기화Builder.음식주문저장요청생성(주문);
                var existing = await _원장저장소.원장조회Async(request.원장Id!, cancellationToken);
                request.기대Revision = existing?.Revision ?? 0;
                await 음식배송원장연결Async(request, existing, 주문, cancellationToken);
                try
                {
                    return await _원장저장소.원장저장Async(request, updatedBy, cancellationToken);
                }
                catch (InvalidOperationException ex) when (attempt < 2
                    && ex.Message.StartsWith("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.", StringComparison.Ordinal))
                {
                    // 관계 편집과 겹친 경우 최신 포함 관계를 다시 읽어 합칩니다.
                }
            }

            throw new InvalidOperationException("음식 주문 원장 동기화 중 판본이 변경되었습니다.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "음식 주문 원장 Mongo 동기화에 실패했습니다. 주문번호={주문번호}", 주문.주문번호);
            return null;
        }
    }

    private async Task 음식배송원장연결Async(
        커뮤니티원장저장요청 request,
        커뮤니티원장Dto? existing,
        음식주문응답 주문,
        CancellationToken cancellationToken)
    {
        // 배차가 없는 주문도 같은 원장으로 조회할 수 있습니다. null은 저장소가 기존 관계를 보존한다는 뜻입니다.
        if (주문.배차대기Id is not { } dispatchWaitId) return;
        var queue = await _db.운송원장.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == dispatchWaitId, cancellationToken)
            ?? throw new InvalidOperationException("FoodOrderDispatchSourceUnavailable");
        if (queue.배차업무유형 != 상태값.배차업무유형.음식배달
            || !운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형)
            || !string.Equals(queue.원본의뢰Id, 주문.주문번호, StringComparison.Ordinal))
            throw new InvalidOperationException("FoodOrderDispatchSourceMismatch");

        var requestId = Clean(queue.의뢰Id) ?? Clean(queue.운송번호)
            ?? throw new InvalidOperationException("FoodOrderDispatchIdentityUnavailable");
        var childId = Clean(queue.커뮤니티원장Id) ?? 운송원장Mongo동기화Builder.원장Id생성(requestId);
        var child = await _원장저장소.원장조회Async(childId, cancellationToken)
            ?? throw new InvalidOperationException("FoodOrderDeliveryLedgerUnavailable");
        if (child.확장속성.ContainsKey(배송원장개인정보파기Service.Marker))
            throw new InvalidOperationException("PrivacyDeletionRestoreBlocked");
        if (!string.Equals(child.원장템플릿Key, CommunityLedgerTemplateKeys.FoodDelivery, StringComparison.OrdinalIgnoreCase)
            || !운송의뢰배차원천유형.Is음식점주문(TryGet(child.외부참조, "원천유형"))
            || !string.Equals(TryGet(child.외부참조, "원천Id"), 주문.주문번호, StringComparison.Ordinal))
            throw new InvalidOperationException("FoodOrderDeliveryLedgerSourceMismatch");

        주문원장구성정책.연결검증(new 커뮤니티원장Dto
        {
            원장Id = request.원장Id!,
            원장템플릿Key = request.원장템플릿Key,
            커뮤니티Id = request.커뮤니티Id
        }, child, 주문원장포함역할.배송);
        var references = existing?.포함원장목록.ToList() ?? [];
        if (references.Any(x => string.Equals(x.원장Id, child.원장Id, StringComparison.OrdinalIgnoreCase)))
            return; // 이미 있는 관계의 역할·필수 여부·순서는 후속 동기화가 바꾸지 않습니다.

        references.Add(new 커뮤니티포함원장참조Dto
        {
            원장Id = child.원장Id,
            원장템플릿Key = child.원장템플릿Key,
            역할 = 주문원장포함역할.배송,
            필수여부 = true,
            표시순서 = references.Select(x => x.표시순서).DefaultIfEmpty(-1).Max() + 1
        });
        request.포함원장목록 = references;
    }

    public async Task<커뮤니티원장Dto?> 출고원장동기화Async(
        IReadOnlyList<출고예정> 출고목록,
        IReadOnlyList<입고요청> 입고목록,
        string updatedBy,
        string? 현재단계Key = null,
        string? 원장템플릿Key = null,
        CancellationToken cancellationToken = default)
    {
        if (출고목록.Count == 0)
        {
            return null;
        }

        try
        {
            var references = 출고목록.Select(x => Clean(x.주문참조번호)).Where(x => x is not null).Distinct().ToArray();
            if (await _db.Set<마트배송시도>().AsNoTracking()
                    .AnyAsync(x => Enumerable.Contains(references, x.주문참조번호), cancellationToken))
            {
                if (references.Length != 1 || 출고목록.Any(x => Clean(x.주문참조번호) != references[0]))
                    throw new InvalidOperationException("MartDriverLedgerSourceMismatch");
                return await 마트기사상태사본동기화Async(references[0]!, updatedBy, cancellationToken);
            }

            var request = 음식마트원장Mongo동기화Builder.출고원장저장요청생성(
                출고목록,
                입고목록,
                현재단계Key,
                원장템플릿Key);
            if (string.Equals(request.원장템플릿Key, CommunityLedgerTemplateKeys.SsalddelMart, StringComparison.OrdinalIgnoreCase))
            {
                var attributes = request.확장속성.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                attributes["MartDriverSnapshotState"] = request.상태;
                attributes["MartDriverSnapshotStage"] = request.현재단계Key ?? string.Empty;
                request.확장속성 = attributes;
                운송원장RdbSnapshotMarker.Attach(request, TryGet(request.외부참조, "원천Id")!);
            }
            var ledger = await _원장저장소.원장저장Async(request, updatedBy, cancellationToken);
            // 최초 기사 수락이 외부 저장 중 시작됐어도 예전 사본을 성공 처리하거나 SQL에 역투영하지 않습니다.
            if (ledger is not null && await _db.Set<마트배송시도>().AsNoTracking()
                    .AnyAsync(x => Enumerable.Contains(references, x.주문참조번호), cancellationToken)) return null;
            if (ledger is not null
                && string.Equals(request.원장템플릿Key, CommunityLedgerTemplateKeys.SsalddelMart, StringComparison.OrdinalIgnoreCase))
            {
                if (!await 마트주문투영Async(request, ledger, 출고목록, cancellationToken)) return null;
            }

            return ledger;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            var orderRef = 출고목록.Select(x => x.주문참조번호).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            _logger.LogWarning(ex, "마트/창고 출고 원장 Mongo 동기화에 실패했습니다. 주문참조번호={주문참조번호}", orderRef);
            return null;
        }
    }

    // 기사 업무가 시작된 마트 주문은 이미 확정된 SQL 업무 사실을 읽는 사본입니다.
    // 옛 Outbox payload의 단계나 출고완료만으로 수령·정산까지 완료됐다고 추론하지 않습니다.
    private async Task<커뮤니티원장Dto?> 마트기사상태사본동기화Async(
        string reference, string updatedBy, CancellationToken ct)
    {
        if (_db.Database.CurrentTransaction is not null) return null;
        var ledgerId = 음식마트원장Mongo동기화Builder.출고원장Id생성(reference, CommunityLedgerTemplateKeys.SsalddelMart);
        for (var retry = 0; retry < 3; retry++)
        {
            // SQL 사본보다 먼저 Mongo 판본을 읽어 과거 SQL 사본이 최신 발행의 판본을 차지하지 않게 합니다.
            var existing = await _원장저장소.원장조회Async(ledgerId, ct);
            var snapshot = await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = _db.Database.IsRelational()
                    ? await _db.Database.BeginTransactionAsync(_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
                        ? IsolationLevel.Serializable : IsolationLevel.RepeatableRead, ct)
                    : null;
                var read = await 마트기사상태사본조회Async(reference, ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return read;
            });
            if (snapshot is null) return null;
            var request = 음식마트원장Mongo동기화Builder.출고원장저장요청생성(
                snapshot.Outbounds, snapshot.Inbounds, snapshot.Stage, CommunityLedgerTemplateKeys.SsalddelMart);
            request.기대Revision = existing?.Revision ?? 0;
            // 기사 전달 사실까지만 존재합니다. 수령 확인·정산·지급 완료는 별도 업무입니다.
            request.상태 = 커뮤니티원장상태.진행중;
            foreach (var block in request.블록목록)
            {
                if (block.BlockId is "mart-order" or "mart-delivery") block.State = snapshot.Stage;
                if (block.BlockId == "picking-packing") block.State = "포장 완료";
            }
            var references = request.외부참조.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            references["마트배송시도Id"] = snapshot.Attempt.시도StableId;
            references["마트배송Revision"] = snapshot.Attempt.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
            request.외부참조 = references;
            var attributes = request.확장속성.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            attributes["동기화정책"] = "확정된 마트 기사 SQL 업무의 읽기 사본이며 수령 확인·정산·지급 완료가 아닙니다.";
            attributes["MartDriverSnapshotState"] = request.상태;
            attributes["MartDriverSnapshotStage"] = request.현재단계Key!;
            request.확장속성 = attributes;
            운송원장RdbSnapshotMarker.Attach(request, reference);
            커뮤니티원장Dto? ledger;
            try
            {
                ledger = await _원장저장소.원장저장Async(request, updatedBy, ct);
            }
            catch (InvalidOperationException ex) when (retry < 2
                && ex.Message.StartsWith("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.", StringComparison.Ordinal))
            {
                continue;
            }
            if (ledger is null) return null;
            // 두 저장소의 원자적 CAS가 아닙니다. 외부 저장 중 SQL이 전진하면 성공 처리하지 않고
            // 기존 Outbox를 Pending으로 남겨 최신 사본 재처리로 수렴시킵니다.
            var stableId = snapshot.Attempt.시도StableId;
            var revision = snapshot.Attempt.Revision;
            var state = snapshot.Attempt.상태Code;
            if (!await _db.Set<마트배송시도>().AsNoTracking().AnyAsync(x => x.시도StableId == stableId
                && x.Revision == revision && x.상태Code == state, ct)) return null;
            // 기존 tracked 주문/상품을 SaveChanges로 덮지 않습니다. 업무 열과 상품 열은 기사 Command 소유입니다.
            var now = DateTime.UtcNow;
            if (_db.Database.IsRelational())
            {
                var changed = await _db.마트주문.Where(x => x.주문참조번호 == reference
                    && _db.Set<마트배송시도>().Any(a => a.시도StableId == stableId && a.Revision == revision && a.상태Code == state))
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.커뮤니티원장Id, ledger.원장Id)
                        .SetProperty(y => y.커뮤니티원장템플릿Key, CommunityLedgerTemplateKeys.SsalddelMart)
                        .SetProperty(y => y.커뮤니티원장상태, ledger.상태)
                        .SetProperty(y => y.커뮤니티원장동기화시각Utc, now), ct);
                if (changed != 1) return null;
            }
            return ledger;
        }
        return null;
    }

    private async Task<마트기사상태사본?> 마트기사상태사본조회Async(string reference, CancellationToken ct)
    {
        var attempt = await _db.Set<마트배송시도>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.주문참조번호 == reference, ct);
        var order = await _db.마트주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문참조번호 == reference, ct);
        if (attempt is null || order is null || order.상태 == "취소") return null;
        var queue = await _db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == attempt.배차의뢰Id, ct);
        if (queue is null || queue.원본의뢰유형 != "SsalddelMartPackedOrder" || queue.원본의뢰Id != reference
            || queue.의뢰Id != reference || queue.일반회원배송전용
            || queue.배차업무유형 != 상태값.배차업무유형.음식배달
            || queue.확정기사Id != attempt.기사Id || queue.화주Id != order.판매자UserId
            || string.IsNullOrWhiteSpace(order.주문자UserId)) return null;
        var stage = attempt.상태Code switch
        {
            "Accepted" when queue.상태 == 상태값.배차대기상태.확정 && queue.배차큐단계 == 상태값.배차큐단계.확정 => "픽업대기",
            "PickedUp" when queue.상태 == 상태값.배차상태.상차완료 && queue.배차큐단계 == 상태값.배차큐단계.확정 => "배송중",
            "Delivered" when queue.상태 == 상태값.배차상태.하차완료 && queue.배차큐단계 == 상태값.배차큐단계.종료 => "전달완료",
            _ => null
        };
        if (stage is null) return null;
        var outbounds = await _db.출고예정.AsNoTracking().Where(x => x.주문참조번호 == reference).OrderBy(x => x.Id).ToListAsync(ct);
        if (outbounds.Count == 0 || outbounds.Any(x => x.운송의뢰Id != queue.의뢰Id
            || x.판매자UserId != order.판매자UserId || x.주문자UserId != order.주문자UserId
            || (attempt.상태Code == "Accepted" ? x.상태 != 출고상태.준비중 : x.상태 != 출고상태.출고완료))) return null;
        var inboundIds = outbounds.Where(x => x.입고요청Id.HasValue).Select(x => x.입고요청Id!.Value).Distinct().ToArray();
        var inbounds = await _db.입고요청.AsNoTracking().Where(x => Enumerable.Contains(inboundIds, x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        return new(attempt, stage, outbounds, inbounds);
    }

    private sealed record 마트기사상태사본(마트배송시도 Attempt, string Stage, IReadOnlyList<출고예정> Outbounds, IReadOnlyList<입고요청> Inbounds);

    private async Task<bool> 마트주문투영Async(
        커뮤니티원장저장요청 request,
        커뮤니티원장Dto ledger,
        IReadOnlyList<출고예정> 출고목록,
        CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
            return await 마트주문투영저장Async(request, ledger, 출고목록, cancellationToken);
        if (_db.Database.CurrentTransaction is { } callerTransaction)
        {
            // 중첩 transaction을 열지 않습니다. 약한 상위 격리 수준에서는 absence→Save 경합을 보장할 수 없습니다.
            return callerTransaction.GetDbTransaction().IsolationLevel == IsolationLevel.Serializable
                && await 마트주문투영저장Async(request, ledger, 출고목록, cancellationToken);
        }
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var applied = await 마트주문투영저장Async(request, ledger, 출고목록, cancellationToken);
                if (applied) await transaction.CommitAsync(cancellationToken);
                else await transaction.RollbackAsync(cancellationToken);
                return applied;
            }
            catch
            {
                // transaction Dispose가 SQL을 rollback합니다. 실패한 projection의 변경만 제거하여
                // 상위 Outbox의 Pending 저장이 주문/상품 변경을 뜻하지 않게 합니다.
                var reference = TryGet(request.외부참조, "주문참조번호", "주문번호", "원천Id");
                var orders = _db.ChangeTracker.Entries<마트주문>().Where(x => x.Entity.주문참조번호 == reference).ToArray();
                var orderIds = orders.Select(x => x.Entity.Id).ToHashSet();
                var outboundIds = 출고목록.Select(x => x.Id).ToHashSet();
                foreach (var line in _db.ChangeTracker.Entries<마트주문상품>().Where(x => orderIds.Contains(x.Entity.마트주문Id)
                    || (x.Entity.출고예정Id.HasValue && outboundIds.Contains(x.Entity.출고예정Id.Value))).ToArray())
                    line.State = EntityState.Detached;
                foreach (var order in orders) order.State = EntityState.Detached;
                throw;
            }
        });
    }

    private async Task<bool> 마트주문투영저장Async(
        커뮤니티원장저장요청 request,
        커뮤니티원장Dto ledger,
        IReadOnlyList<출고예정> 출고목록,
        CancellationToken cancellationToken)
    {
        var 주문참조번호 = TryGet(request.외부참조, "주문참조번호", "주문번호", "원천Id")
                         ?? 출고목록.Select(x => Clean(x.주문참조번호)).FirstOrDefault(x => x is not null)
                         ?? Clean(ledger.원장Id)
                         ?? throw new InvalidOperationException("마트 주문 projection을 만들려면 주문참조번호가 필요합니다.");
        // 관계형 저장소에서는 호출자가 연 직렬화 transaction의 범위 잠금으로 최초 수락 삽입과 함께 직렬화합니다.
        if (await _db.Set<마트배송시도>().AsNoTracking()
                .AnyAsync(x => x.주문참조번호 == 주문참조번호, cancellationToken)) return false;
        var first = 출고목록[0];
        var now = DateTime.UtcNow;
        var 상태 = Clean(request.현재단계Key) ?? Clean(ledger.상태) ?? "출고 예정";
        var order = await _db.마트주문
            .Include(x => x.상품목록)
            .FirstOrDefaultAsync(x => x.주문참조번호 == 주문참조번호, cancellationToken);

        if (order is null)
        {
            order = new 마트주문
            {
                주문참조번호 = 주문참조번호,
                CreatedAt = now
            };
            _db.마트주문.Add(order);
        }

        order.주문Id = 출고목록.Select(x => x.주문Id).FirstOrDefault(x => x.HasValue);
        order.주문자UserId = Clean(first.주문자UserId) ?? order.주문자UserId;
        order.판매자UserId = Clean(first.판매자UserId) ?? order.판매자UserId;
        order.상태 = 상태;
        order.현재단계 = Clean(request.현재단계Key);
        order.커뮤니티원장Id = Clean(ledger.원장Id);
        order.커뮤니티원장템플릿Key = Clean(ledger.원장템플릿Key);
        order.커뮤니티원장상태 = Clean(ledger.상태);
        order.커뮤니티원장동기화시각Utc = now;
        order.UpdatedAt = now;

        foreach (var outbound in 출고목록)
        {
            var item = order.상품목록.FirstOrDefault(x => x.출고예정Id == outbound.Id);
            if (item is null)
            {
                item = new 마트주문상품
                {
                    출고예정Id = outbound.Id,
                    CreatedAt = now
                };
                order.상품목록.Add(item);
            }

            item.상품명 = Clean(outbound.상품명) ?? item.상품명;
            item.SKU = Clean(outbound.SKU) ?? item.SKU;
            item.수량 = outbound.수량;
            item.상태 = Clean(outbound.상태) ?? 상태;
            item.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? TryGet(IReadOnlyDictionary<string, string>? data, params string[] keys)
    {
        if (data is null)
        {
            return null;
        }

        foreach (var key in keys)
        {
            foreach (var item in data)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return Clean(item.Value);
                }
            }
        }

        return null;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class 음식마트원장Mongo동기화Builder
{
    public static string 음식주문원장Id생성(string 주문번호)
        => $"food-order:{주문번호.Trim()}";

    public static string 출고원장Id생성(string 주문참조번호, string 원장템플릿Key)
        => string.Equals(원장템플릿Key, CommunityLedgerTemplateKeys.SsalddelMart, StringComparison.OrdinalIgnoreCase)
            ? $"ssalddel-mart:{주문참조번호.Trim()}"
            : $"warehouse-outbound:{주문참조번호.Trim()}";

    public static 커뮤니티원장저장요청 음식주문저장요청생성(음식주문응답 주문)
    {
        if (string.IsNullOrWhiteSpace(주문.주문번호))
        {
            throw new InvalidOperationException("음식 주문 원장을 만들려면 주문번호가 필요합니다.");
        }

        var 원장Id = 음식주문원장Id생성(주문.주문번호);
        var 현재단계 = ResolveFoodOrderStage(주문);

        return new 커뮤니티원장저장요청
        {
            원장Id = 원장Id,
            커뮤니티Id = "platform",
            원장템플릿Key = CommunityLedgerTemplateKeys.FoodOrder,
            제목 = $"음식 주문 원장 {주문.주문번호}",
            원함 = BuildFoodWish(주문),
            상태 = ResolveFoodLedgerState(주문),
            현재단계Key = 현재단계,
            대상OsCode = CommunityLedgerOperatingSystemCodes.FoodDelivery,
            대상OsName = "음식 배달 OS",
            생성자UserId = 주문.주문자UserId,
            생성자표시명 = "음식 주문자",
            블록목록 = BuildFoodBlocks(주문),
            참여자목록 = BuildFoodParticipants(주문),
            다이어그램스냅샷 = BuildFoodDiagram(원장Id),
            외부참조 = Data(
                ("음식주문번호", 주문.주문번호),
                ("주문번호", 주문.주문번호),
                ("음식점Id", Format(주문.음식점Id)),
                ("원천유형", "FoodOrder"),
                ("원천Id", 주문.주문번호),
                ("RdbFoodProjectionType", "음식주문")),
            확장속성 = Data(
                ("원장원본저장소", "MongoDB"),
                ("동기화정책", "Mongo 음식 주문 원장을 원본으로 보고 음식 주문 저장소는 조회 투영으로 갱신합니다."),
                ("입력Api", "POST api/v1/food-orders"),
                ("수락Api", "POST api/v1/food-orders/{orderNo}/restaurant-accept"))
        };
    }

    public static 커뮤니티원장저장요청 출고원장저장요청생성(
        IReadOnlyList<출고예정> 출고목록,
        IReadOnlyList<입고요청> 입고목록,
        string? 현재단계Key = null,
        string? 원장템플릿Key = null)
    {
        if (출고목록.Count == 0)
        {
            throw new InvalidOperationException("출고 원장을 만들려면 출고예정 목록이 필요합니다.");
        }

        var 주문참조번호 = FirstNonEmpty(출고목록.Select(x => x.주문참조번호).ToArray())
                         ?? 출고목록[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var templateKey = ResolveOutboundTemplateKey(주문참조번호, 원장템플릿Key);
        var 원장Id = 출고원장Id생성(주문참조번호, templateKey);
        var stage = FirstNonEmpty(현재단계Key, ResolveOutboundStage(출고목록), ResolveInboundStage(입고목록));
        var isMart = string.Equals(templateKey, CommunityLedgerTemplateKeys.SsalddelMart, StringComparison.OrdinalIgnoreCase);

        return new 커뮤니티원장저장요청
        {
            원장Id = 원장Id,
            커뮤니티Id = "platform",
            원장템플릿Key = templateKey,
            제목 = isMart ? $"살뜰 마트 주문 원장 {주문참조번호}" : $"창고 출고 원장 {주문참조번호}",
            원함 = isMart
                ? "도심 재고를 피킹/포장해 주문자에게 전달하고 싶습니다."
                : "창고 출고 품목을 준비하고 운송 인계까지 정리하고 싶습니다.",
            상태 = ResolveOutboundLedgerState(출고목록),
            현재단계Key = stage,
            대상OsCode = isMart
                ? CommunityLedgerOperatingSystemCodes.SsalddelMartUrbanLogistics
                : CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
            대상OsName = isMart ? "알뜰살뜰 마트 도심 물류 OS" : "창고·커머스 이행 OS",
            생성자UserId = FirstNonEmpty(출고목록.Select(x => x.주문자UserId).ToArray()),
            생성자표시명 = isMart ? "마트 주문자" : "출고 요청자",
            블록목록 = BuildOutboundBlocks(출고목록, 입고목록, isMart, stage),
            참여자목록 = BuildOutboundParticipants(출고목록),
            다이어그램스냅샷 = BuildOutboundDiagram(원장Id, templateKey, isMart),
            외부참조 = Data(
                ("주문참조번호", 주문참조번호),
                ("주문번호", 주문참조번호),
                ("출고예정Id", 출고목록.Count == 1 ? Format(출고목록[0].Id) : null),
                ("출고예정Ids", JoinIds(출고목록.Select(x => x.Id))),
                ("입고요청Id", 입고목록.Count == 1 ? Format(입고목록[0].Id) : null),
                ("입고요청Ids", JoinIds(입고목록.Select(x => x.Id))),
                ("운송의뢰Id", FirstNonEmpty(출고목록.Select(x => x.운송의뢰Id).ToArray())),
                ("원천유형", isMart ? "SsalddelMartOrder" : "WarehouseOutboundPlanned"),
                ("원천Id", 주문참조번호),
                ("RdbWarehouseProjectionTable", "출고예정"),
                ("RdbWarehouseProjectionType", "출고예정")),
            확장속성 = Data(
                ("원장원본저장소", "MongoDB"),
                ("동기화정책", "Mongo 출고 원장을 원본으로 보고 RDB 창고 출고/입고 데이터는 작업 조회를 위한 투영으로 갱신합니다."),
                ("분류정책", "주문참조번호나 명시 템플릿이 마트 흐름이면 ssalddel-mart, 아니면 warehouse-outbound로 분류합니다."),
                ("입력Api", "POST api/v1/ssalddel-mart/orders 또는 내부 주문결제완료됨Event"),
                ("포장Api", "POST api/v1/warehouse-operations/inventory-items/{id}/pack"),
                ("운송인계Api", "POST api/v1/warehouse-operations/reconsignment-requests"))
        };
    }

    private static IReadOnlyList<커뮤니티원장블록Dto> BuildFoodBlocks(음식주문응답 주문)
        =>
        [
            new()
            {
                BlockId = "food-order",
                BlockType = CommunityLedgerBlockTypes.Order,
                Title = "음식 주문",
                State = 주문.상태,
                Data = Data(
                    ("업무엔티티", "음식주문"),
                    ("주문번호", 주문.주문번호),
                    ("음식점Id", Format(주문.음식점Id)),
                    ("주문자UserId", 주문.주문자UserId),
                    ("총주문금액", Format(주문.총주문금액)),
                    ("메뉴요약", FoodOrderSampleData.BuildMenuSummary(주문.상품목록)),
                    ("입력Api", "POST api/v1/food-orders"))
            },
            new()
            {
                BlockId = "restaurant",
                BlockType = CommunityLedgerBlockTypes.Place,
                Title = "음식점",
                State = 주문.음식점수락시각Utc is null ? "수락 전" : "수락 완료",
                Data = Data(
                    ("업무엔티티", "음식점"),
                    ("음식점Id", Format(주문.음식점Id)),
                    ("음식점명", 주문.음식점명),
                    ("주소", 주문.음식점주소),
                    ("상세주소", 주문.음식점상세주소),
                    ("위도", Format(주문.음식점위도)),
                    ("경도", Format(주문.음식점경도)),
                    ("조리예상완료시각Utc", Format(주문.조리예상완료시각Utc)),
                    ("수락Api", "POST api/v1/food-orders/{orderNo}/restaurant-accept"))
            },
            new()
            {
                BlockId = "food-preparation",
                BlockType = CommunityLedgerBlockTypes.State,
                Title = "조리 준비",
                State = ResolveFoodOrderStage(주문),
                Data = Data(
                    ("업무엔티티", "음식주문.조리준비"),
                    ("음식점수락시각Utc", Format(주문.음식점수락시각Utc)),
                    ("조리예상완료시각Utc", Format(주문.조리예상완료시각Utc)),
                    ("수락메모", 주문.수락메모),
                    ("후속원장템플릿Key", CommunityLedgerTemplateKeys.FoodDelivery),
                    ("후속원장관계", "선택 0..N"))
            },
            new()
            {
                BlockId = "food-settlement",
                BlockType = CommunityLedgerBlockTypes.Settlement,
                Title = "결제 표시",
                State = 주문.결제수단,
                Data = Data(
                    ("업무엔티티", "음식주문.결제표시"),
                    ("결제수단", 주문.결제수단),
                    ("총주문금액", Format(주문.총주문금액)))
            }
        ];

    private static IReadOnlyList<커뮤니티원장참여자Dto> BuildFoodParticipants(음식주문응답 주문)
    {
        var participants = new List<커뮤니티원장참여자Dto>();
        AddParticipant(participants, 주문.주문자UserId, "주문자", "주문자");
        AddParticipant(participants, $"restaurant:{주문.음식점Id}", string.IsNullOrWhiteSpace(주문.음식점명) ? "음식점" : 주문.음식점명, "음식점");
        return participants;
    }

    private static IReadOnlyList<커뮤니티원장블록Dto> BuildOutboundBlocks(
        IReadOnlyList<출고예정> 출고목록,
        IReadOnlyList<입고요청> 입고목록,
        bool isMart,
        string? stage)
    {
        var first = 출고목록[0];
        var 주문참조번호 = FirstNonEmpty(출고목록.Select(x => x.주문참조번호).ToArray()) ?? first.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lineSummary = string.Join(", ", 출고목록.Select(x => $"{x.상품명} {x.수량}"));

        return
        [
            new()
            {
                BlockId = isMart ? "mart-order" : "outbound-order",
                BlockType = CommunityLedgerBlockTypes.Order,
                Title = isMart ? "마트 주문" : "출고 요청",
                State = ResolveOutboundStage(출고목록),
                Data = Data(
                    ("업무엔티티", isMart ? "SsalddelMartOrder" : "출고예정"),
                    ("주문참조번호", 주문참조번호),
                    ("출고예정Ids", JoinIds(출고목록.Select(x => x.Id))),
                    ("판매자UserId", first.판매자UserId),
                    ("주문자UserId", first.주문자UserId),
                    ("상품요약", lineSummary),
                    ("라인수", Format(출고목록.Count)))
            },
            new()
            {
                BlockId = isMart ? "urban-inventory" : "warehouse-inventory",
                BlockType = CommunityLedgerBlockTypes.Inventory,
                Title = isMart ? "도심 재고" : "재고 근거",
                State = 입고목록.Count == 0 ? "입고 근거 미연결" : ResolveInboundStage(입고목록),
                Data = Data(
                    ("업무엔티티", "입고요청/입고상품"),
                    ("입고요청Ids", JoinIds(입고목록.Select(x => x.Id))),
                    ("출고창고Id", Format(first.출고창고Id)),
                    ("입고요청수", Format(입고목록.Count)))
            },
            new()
            {
                BlockId = "picking-packing",
                BlockType = CommunityLedgerBlockTypes.State,
                Title = "피킹/포장",
                State = stage,
                Data = Data(
                    ("업무엔티티", "출고묶음/피킹포장"),
                    ("상태", stage),
                    ("포장Api", "POST api/v1/warehouse-operations/inventory-items/{id}/pack"))
            },
            new()
            {
                BlockId = isMart ? "mart-delivery" : "transport-handoff",
                BlockType = CommunityLedgerBlockTypes.Handoff,
                Title = isMart ? "기사 픽업" : "운송 인계",
                State = FirstNonEmpty(출고목록.Select(x => x.운송의뢰Id).ToArray()) is null ? "운송 인계 전" : "운송 인계됨",
                Data = Data(
                    ("업무엔티티", "운송인계"),
                    ("운송의뢰Id", FirstNonEmpty(출고목록.Select(x => x.운송의뢰Id).ToArray())),
                    ("연결원장템플릿Key", isMart ? CommunityLedgerTemplateKeys.SsalddelMart : CommunityLedgerTemplateKeys.CargoTransport))
            }
        ];
    }

    private static IReadOnlyList<커뮤니티원장참여자Dto> BuildOutboundParticipants(IReadOnlyList<출고예정> 출고목록)
    {
        var first = 출고목록[0];
        var participants = new List<커뮤니티원장참여자Dto>();
        AddParticipant(participants, first.주문자UserId, "주문자", "주문자");
        AddParticipant(participants, first.판매자UserId, "판매자", "판매자");
        AddParticipant(participants, null, "창고 작업자", "피킹/포장 담당자");
        return participants;
    }

    private static DiagramSnapshotDto BuildFoodDiagram(string 원장Id)
        => new()
        {
            DiagramId = $"{원장Id}:diagram",
            DiagramName = "음식 주문 원장 흐름",
            LedgerId = 원장Id,
            LedgerTemplateKey = CommunityLedgerTemplateKeys.FoodOrder,
            WorkflowModeKey = "food-order",
            Nodes =
            [
                Node("food-order", "음식 주문", CommunityLedgerBlockTypes.Order, "주문", 120, 160),
                Node("restaurant", "음식점 수락", CommunityLedgerBlockTypes.Place, "수락", 360, 160),
                Node("food-preparation", "조리 준비", CommunityLedgerBlockTypes.State, "준비 완료", 600, 160),
                Node("food-settlement", "결제 표시", CommunityLedgerBlockTypes.Settlement, "결제", 600, 320)
            ],
            Edges =
            [
                Edge("edge-food-restaurant", "food-order", "restaurant", "주문 수락", CommunityLedgerRelationTypes.Flow),
                Edge("edge-restaurant-preparation", "restaurant", "food-preparation", "조리·준비", CommunityLedgerRelationTypes.Flow),
                Edge("edge-order-settlement", "food-order", "food-settlement", "결제 표시", CommunityLedgerRelationTypes.Reference)
            ],
            Metadata = Data(
                ("Source", "FoodMartLedgerMongoSync"),
                ("PrimaryStore", "MongoDB community_ledgers"))
        };

    private static DiagramSnapshotDto BuildOutboundDiagram(string 원장Id, string templateKey, bool isMart)
        => new()
        {
            DiagramId = $"{원장Id}:diagram",
            DiagramName = isMart ? "살뜰 마트 주문 원장 흐름" : "창고 출고 원장 흐름",
            LedgerId = 원장Id,
            LedgerTemplateKey = templateKey,
            WorkflowModeKey = isMart ? "ssalddel-mart" : "warehouse-outbound",
            Nodes =
            [
                Node(isMart ? "mart-order" : "outbound-order", isMart ? "마트 주문" : "출고 요청", CommunityLedgerBlockTypes.Order, "주문", 120, 160),
                Node(isMart ? "urban-inventory" : "warehouse-inventory", isMart ? "도심 재고" : "재고 근거", CommunityLedgerBlockTypes.Inventory, "재고", 360, 160),
                Node("picking-packing", "피킹/포장", CommunityLedgerBlockTypes.State, "작업", 600, 160),
                Node(isMart ? "mart-delivery" : "transport-handoff", isMart ? "기사 픽업" : "운송 인계", CommunityLedgerBlockTypes.Handoff, "인계", 840, 160)
            ],
            Edges =
            [
                Edge("edge-order-inventory", isMart ? "mart-order" : "outbound-order", isMart ? "urban-inventory" : "warehouse-inventory", "재고 확인", CommunityLedgerRelationTypes.Requires),
                Edge("edge-inventory-work", isMart ? "urban-inventory" : "warehouse-inventory", "picking-packing", "피킹/포장", CommunityLedgerRelationTypes.Flow),
                Edge("edge-work-delivery", "picking-packing", isMart ? "mart-delivery" : "transport-handoff", "포장 후 인계", CommunityLedgerRelationTypes.Handoff)
            ],
            Metadata = Data(
                ("Source", "FoodMartLedgerMongoSync"),
                ("PrimaryStore", "MongoDB community_ledgers"))
        };

    private static string ResolveFoodLedgerState(음식주문응답 주문)
    {
        var state = 음식주문상태코드.Normalize(주문.상태);
        return state switch
        {
            음식주문상태코드.거절 or 음식주문상태코드.취소 => 커뮤니티원장상태.닫힘,
            음식주문상태코드.픽업대기
                or 음식주문상태코드.기사배정
                or 음식주문상태코드.픽업완료
                or 음식주문상태코드.전달완료
                or 음식주문상태코드.수령확인 => 커뮤니티원장상태.완료,
            _ => 커뮤니티원장상태.진행중
        };
    }

    private static string ResolveFoodOrderStage(음식주문응답 주문)
    {
        var state = 음식주문상태코드.Normalize(주문.상태);
        return state is 음식주문상태코드.기사배정
            or 음식주문상태코드.픽업완료
            or 음식주문상태코드.전달완료
            or 음식주문상태코드.수령확인
            ? 음식주문상태코드.픽업대기
            : state;
    }

    private static string BuildFoodWish(음식주문응답 주문)
    {
        var menu = FoodOrderSampleData.BuildMenuSummary(주문.상품목록);
        return string.IsNullOrWhiteSpace(menu)
            ? "음식 주문을 음식점 수락, 조리, 준비 완료 흐름으로 처리하고 싶습니다."
            : $"{menu} 주문을 음식점 수락, 조리, 준비 완료 흐름으로 처리하고 싶습니다.";
    }

    private static string ResolveOutboundTemplateKey(string 주문참조번호, string? 원장템플릿Key)
    {
        if (string.Equals(원장템플릿Key, CommunityLedgerTemplateKeys.SsalddelMart, StringComparison.OrdinalIgnoreCase)
            || string.Equals(원장템플릿Key, CommunityLedgerTemplateKeys.WarehouseOutbound, StringComparison.OrdinalIgnoreCase))
        {
            return 원장템플릿Key!;
        }

        return ContainsAny(주문참조번호, "MART", "SSALDDEL-MART", "살뜰마트", "알뜰마트", "살뜰마트", "마트")
            ? CommunityLedgerTemplateKeys.SsalddelMart
            : CommunityLedgerTemplateKeys.WarehouseOutbound;
    }

    private static string ResolveOutboundLedgerState(IReadOnlyList<출고예정> 출고목록)
    {
        if (출고목록.All(x => x.상태 == 출고상태.출고완료))
        {
            return 커뮤니티원장상태.완료;
        }

        if (출고목록.Any(x => x.상태 != 출고상태.예정))
        {
            return 커뮤니티원장상태.진행중;
        }

        return 커뮤니티원장상태.초안;
    }

    private static string? ResolveOutboundStage(IReadOnlyList<출고예정> 출고목록)
    {
        if (출고목록.Count == 0)
        {
            return null;
        }

        if (출고목록.All(x => x.상태 == 출고상태.출고완료))
        {
            return "출고 완료";
        }

        if (출고목록.Any(x => x.상태 == 출고상태.준비중))
        {
            return "출고 준비중";
        }

        return "출고 예정";
    }

    private static string? ResolveInboundStage(IReadOnlyList<입고요청> 입고목록)
    {
        if (입고목록.Count == 0)
        {
            return null;
        }

        if (입고목록.All(x => x.상태 == 입고상태.입고완료))
        {
            return "입고 완료";
        }

        if (입고목록.Any(x => x.상태 == 입고상태.운송중))
        {
            return "입고 운송중";
        }

        return "입고 예정";
    }

    private static void AddParticipant(List<커뮤니티원장참여자Dto> participants, string? userId, string displayName, string roleLabel)
    {
        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        participants.Add(new 커뮤니티원장참여자Dto
        {
            UserId = Clean(userId),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "익명 참여자" : displayName.Trim(),
            RoleLabel = roleLabel,
            ParticipationState = "참여중"
        });
    }

    private static DiagramNodeDto Node(string nodeId, string title, string kind, string groupLabel, double x, double y)
        => new()
        {
            NodeId = nodeId,
            Kind = kind,
            Title = title,
            GroupLabel = groupLabel,
            X = x,
            Y = y,
            Data = Data(("원장블록Id", nodeId))
        };

    private static DiagramEdgeDto Edge(string edgeId, string from, string to, string label, string relationType)
        => new()
        {
            EdgeId = edgeId,
            FromNodeId = from,
            ToNodeId = to,
            Label = label,
            MeaningCode = relationType,
            Data = Data(("관계유형", relationType))
        };

    private static IReadOnlyDictionary<string, string> Data(params (string Key, string? Value)[] values)
        => values
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Value!.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
        => values.Select(Clean).FirstOrDefault(value => value is not null);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Format(long? value)
        => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(long value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? Format(int? value)
        => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(int value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? Format(decimal? value)
        => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? Format(DateTime? value)
        => value?.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static string? JoinIds(IEnumerable<long> values)
    {
        var ids = values.Distinct().Select(Format).ToArray();
        return ids.Length == 0 ? null : string.Join(",", ids);
    }

    private static bool ContainsAny(string? source, params string[] candidates)
    {
        var text = Clean(source);
        return text is not null && candidates.Any(candidate => text.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }
}
