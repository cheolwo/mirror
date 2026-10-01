using Hongdal.Contracts.Common.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using 홍달.Data;
using 홍달.도메인.창고;

namespace Hongdal.Services.Community;

public sealed class 입출고원장업무투영Handler : I원장업무투영동기화Handler
{
    private readonly HongdalContext _db;
    private readonly ILogger<입출고원장업무투영Handler> _logger;

    public 입출고원장업무투영Handler(
        HongdalContext db,
        ILogger<입출고원장업무투영Handler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public bool 처리대상인가(커뮤니티원장Dto 원장)
        => 입출고원장업무투영Snapshot.처리대상인가(원장);

    public async Task 동기화Async(커뮤니티원장Dto 원장, CancellationToken cancellationToken = default)
    {
        var snapshot = 입출고원장업무투영Snapshot.생성(원장);
        if (snapshot is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var changed = false;
        var touchedInboundIds = new HashSet<long>();
        var touchedInboundItemIds = new HashSet<long>();
        var touchedOutboundIds = new HashSet<long>();
        var touchedBundleIds = new HashSet<long>();

        if (snapshot.입고요청Id is long inboundId)
        {
            changed |= await 입고요청반영Async(inboundId, snapshot, now, touchedInboundIds, touchedInboundItemIds, cancellationToken);
        }

        if (snapshot.입고상품Id is long inboundItemId)
        {
            changed |= await 입고상품반영Async(inboundItemId, snapshot, now, touchedInboundItemIds, cancellationToken);
        }

        if (snapshot.출고예정Id is long outboundId)
        {
            changed |= await 출고예정반영Async(outboundId, snapshot, now, touchedOutboundIds, touchedInboundIds, touchedInboundItemIds, cancellationToken);
        }

        if (snapshot.출고묶음Id is long outboundBundleId)
        {
            changed |= await 출고묶음반영Async(outboundBundleId, snapshot, now, touchedBundleIds, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.출고묶음번호))
        {
            var bundle = await _db.출고묶음
                .FirstOrDefaultAsync(x => x.출고묶음번호 == snapshot.출고묶음번호, cancellationToken);
            if (bundle is not null && touchedBundleIds.Add(bundle.Id))
            {
                changed |= ApplyOutboundBundle(bundle, snapshot, now);
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshot.주문참조번호))
        {
            var outboundLines = await _db.출고예정
                .Where(x => x.주문참조번호 == snapshot.주문참조번호)
                .ToListAsync(cancellationToken);
            foreach (var outbound in outboundLines)
            {
                if (touchedOutboundIds.Add(outbound.Id))
                {
                    changed |= ApplyOutbound(outbound, snapshot, now);
                    changed |= await 연결입고반영Async(outbound, snapshot, now, touchedInboundIds, touchedInboundItemIds, cancellationToken);
                }
            }

            var bundles = await _db.출고묶음
                .Where(x => x.주문참조번호 == snapshot.주문참조번호)
                .ToListAsync(cancellationToken);
            foreach (var bundle in bundles)
            {
                if (touchedBundleIds.Add(bundle.Id))
                {
                    changed |= ApplyOutboundBundle(bundle, snapshot, now);
                }
            }

            var inbounds = await _db.입고요청
                .Where(x => x.주문참조번호 == snapshot.주문참조번호 || x.원주문참조번호 == snapshot.주문참조번호)
                .ToListAsync(cancellationToken);
            foreach (var inbound in inbounds)
            {
                if (touchedInboundIds.Add(inbound.Id))
                {
                    changed |= ApplyInbound(inbound, snapshot, now);
                    changed |= await 입고요청상품반영Async(inbound.Id, snapshot, now, touchedInboundItemIds, cancellationToken);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshot.운송의뢰Id))
        {
            var outboundLines = await _db.출고예정
                .Where(x => x.운송의뢰Id == snapshot.운송의뢰Id)
                .ToListAsync(cancellationToken);
            foreach (var outbound in outboundLines)
            {
                if (touchedOutboundIds.Add(outbound.Id))
                {
                    changed |= ApplyOutbound(outbound, snapshot, now);
                    changed |= await 연결입고반영Async(outbound, snapshot, now, touchedInboundIds, touchedInboundItemIds, cancellationToken);
                }
            }

            var bundles = await _db.출고묶음
                .Where(x => x.운송의뢰Id == snapshot.운송의뢰Id)
                .ToListAsync(cancellationToken);
            foreach (var bundle in bundles)
            {
                if (touchedBundleIds.Add(bundle.Id))
                {
                    changed |= ApplyOutboundBundle(bundle, snapshot, now);
                }
            }

            var inbounds = await _db.입고요청
                .Where(x => x.운송의뢰Id == snapshot.운송의뢰Id)
                .ToListAsync(cancellationToken);
            foreach (var inbound in inbounds)
            {
                if (touchedInboundIds.Add(inbound.Id))
                {
                    changed |= ApplyInbound(inbound, snapshot, now);
                    changed |= await 입고요청상품반영Async(inbound.Id, snapshot, now, touchedInboundItemIds, cancellationToken);
                }
            }
        }

        if (!changed)
        {
            _logger.LogDebug(
                "창고/마트 커뮤니티 원장 투영 대상 RDB 행을 찾지 못했습니다. 원장Id={원장Id}, 템플릿={템플릿Key}",
                snapshot.LedgerId,
                snapshot.LedgerTemplateKey);
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> 입고요청반영Async(
        long inboundId,
        입출고원장업무투영Snapshot snapshot,
        DateTime now,
        HashSet<long> touchedInboundIds,
        HashSet<long> touchedInboundItemIds,
        CancellationToken cancellationToken)
    {
        var inbound = await _db.입고요청.FirstOrDefaultAsync(x => x.Id == inboundId, cancellationToken);
        if (inbound is null || !touchedInboundIds.Add(inbound.Id))
        {
            return false;
        }

        var changed = ApplyInbound(inbound, snapshot, now);
        changed |= await 입고요청상품반영Async(inbound.Id, snapshot, now, touchedInboundItemIds, cancellationToken);
        return changed;
    }

    private async Task<bool> 입고상품반영Async(
        long inboundItemId,
        입출고원장업무투영Snapshot snapshot,
        DateTime now,
        HashSet<long> touchedInboundItemIds,
        CancellationToken cancellationToken)
    {
        var item = await _db.입고상품.FirstOrDefaultAsync(x => x.Id == inboundItemId, cancellationToken);
        if (item is null || !touchedInboundItemIds.Add(item.Id))
        {
            return false;
        }

        return ApplyInboundItem(item, snapshot, now);
    }

    private async Task<bool> 출고예정반영Async(
        long outboundId,
        입출고원장업무투영Snapshot snapshot,
        DateTime now,
        HashSet<long> touchedOutboundIds,
        HashSet<long> touchedInboundIds,
        HashSet<long> touchedInboundItemIds,
        CancellationToken cancellationToken)
    {
        var outbound = await _db.출고예정.FirstOrDefaultAsync(x => x.Id == outboundId, cancellationToken);
        if (outbound is null || !touchedOutboundIds.Add(outbound.Id))
        {
            return false;
        }

        var changed = ApplyOutbound(outbound, snapshot, now);
        changed |= await 연결입고반영Async(outbound, snapshot, now, touchedInboundIds, touchedInboundItemIds, cancellationToken);
        return changed;
    }

    private async Task<bool> 출고묶음반영Async(
        long bundleId,
        입출고원장업무투영Snapshot snapshot,
        DateTime now,
        HashSet<long> touchedBundleIds,
        CancellationToken cancellationToken)
    {
        var bundle = await _db.출고묶음.FirstOrDefaultAsync(x => x.Id == bundleId, cancellationToken);
        if (bundle is null || !touchedBundleIds.Add(bundle.Id))
        {
            return false;
        }

        return ApplyOutboundBundle(bundle, snapshot, now);
    }

    private async Task<bool> 연결입고반영Async(
        출고예정 outbound,
        입출고원장업무투영Snapshot snapshot,
        DateTime now,
        HashSet<long> touchedInboundIds,
        HashSet<long> touchedInboundItemIds,
        CancellationToken cancellationToken)
    {
        var changed = false;
        if (outbound.입고요청Id is long inboundId)
        {
            var inbound = await _db.입고요청.FirstOrDefaultAsync(x => x.Id == inboundId, cancellationToken);
            if (inbound is not null && touchedInboundIds.Add(inbound.Id))
            {
                changed |= ApplyInbound(inbound, snapshot, now);
            }
        }

        if (outbound.입고상품Id is long inboundItemId)
        {
            var item = await _db.입고상품.FirstOrDefaultAsync(x => x.Id == inboundItemId, cancellationToken);
            if (item is not null && touchedInboundItemIds.Add(item.Id))
            {
                changed |= ApplyInboundItem(item, snapshot, now);
            }
        }

        return changed;
    }

    private async Task<bool> 입고요청상품반영Async(
        long inboundId,
        입출고원장업무투영Snapshot snapshot,
        DateTime now,
        HashSet<long> touchedInboundItemIds,
        CancellationToken cancellationToken)
    {
        var changed = false;
        var items = await _db.입고상품
            .Where(x => x.입고요청Id == inboundId)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            if (touchedInboundItemIds.Add(item.Id))
            {
                changed |= ApplyInboundItem(item, snapshot, now);
            }
        }

        return changed;
    }

    private static bool ApplyInbound(입고요청 entity, 입출고원장업무투영Snapshot snapshot, DateTime now)
    {
        var changed = ApplyLedgerLink(entity.커뮤니티원장Id, snapshot.LedgerId, value => entity.커뮤니티원장Id = value);
        changed |= ApplyLedgerLink(entity.커뮤니티원장템플릿Key, snapshot.LedgerTemplateKey, value => entity.커뮤니티원장템플릿Key = value);
        changed |= ApplyLedgerLink(entity.커뮤니티원장상태, snapshot.LedgerState, value => entity.커뮤니티원장상태 = value);

        var nextState = snapshot.ResolveInboundState();
        changed |= ApplyLedgerLink(entity.상태, nextState, value => entity.상태 = value);
        if (string.Equals(nextState, 입고상태.입고완료, StringComparison.Ordinal) && entity.입고완료일시 is null)
        {
            entity.입고완료일시 = now;
            changed = true;
        }

        entity.커뮤니티원장동기화시각Utc = now;
        entity.UpdatedAt = now;
        return true;
    }

    private static bool ApplyInboundItem(입고상품 entity, 입출고원장업무투영Snapshot snapshot, DateTime now)
    {
        var changed = ApplyLedgerLink(entity.커뮤니티원장Id, snapshot.LedgerId, value => entity.커뮤니티원장Id = value);
        changed |= ApplyLedgerLink(entity.커뮤니티원장템플릿Key, snapshot.LedgerTemplateKey, value => entity.커뮤니티원장템플릿Key = value);
        changed |= ApplyLedgerLink(entity.커뮤니티원장상태, snapshot.LedgerState, value => entity.커뮤니티원장상태 = value);

        if (string.Equals(snapshot.ResolveInboundState(), 입고상태.입고완료, StringComparison.Ordinal))
        {
            if (!string.Equals(entity.상태, "보관중", StringComparison.Ordinal))
            {
                entity.상태 = "보관중";
                changed = true;
            }

            if (entity.입고완료일시 is null)
            {
                entity.입고완료일시 = now;
                changed = true;
            }
        }

        entity.커뮤니티원장동기화시각Utc = now;
        entity.UpdatedAt = now;
        return true;
    }

    private static bool ApplyOutbound(출고예정 entity, 입출고원장업무투영Snapshot snapshot, DateTime now)
    {
        var changed = ApplyLedgerLink(entity.커뮤니티원장Id, snapshot.LedgerId, value => entity.커뮤니티원장Id = value);
        changed |= ApplyLedgerLink(entity.커뮤니티원장템플릿Key, snapshot.LedgerTemplateKey, value => entity.커뮤니티원장템플릿Key = value);
        changed |= ApplyLedgerLink(entity.커뮤니티원장상태, snapshot.LedgerState, value => entity.커뮤니티원장상태 = value);

        var nextState = snapshot.ResolveOutboundState();
        changed |= ApplyLedgerLink(entity.상태, nextState, value => entity.상태 = value);
        if (string.Equals(nextState, 출고상태.출고완료, StringComparison.Ordinal) && entity.출고처리일시 is null)
        {
            entity.출고처리일시 = now;
            changed = true;
        }

        entity.커뮤니티원장동기화시각Utc = now;
        entity.UpdatedAt = now;
        return true;
    }

    private static bool ApplyOutboundBundle(출고묶음 entity, 입출고원장업무투영Snapshot snapshot, DateTime now)
    {
        var changed = ApplyLedgerLink(entity.커뮤니티원장Id, snapshot.LedgerId, value => entity.커뮤니티원장Id = value);
        changed |= ApplyLedgerLink(entity.커뮤니티원장템플릿Key, snapshot.LedgerTemplateKey, value => entity.커뮤니티원장템플릿Key = value);
        changed |= ApplyLedgerLink(entity.커뮤니티원장상태, snapshot.LedgerState, value => entity.커뮤니티원장상태 = value);

        var nextState = snapshot.ResolveOutboundState();
        changed |= ApplyLedgerLink(entity.상태, nextState, value => entity.상태 = value);

        var stage = snapshot.StageText;
        if (ContainsAny(stage, "피킹 시작", "피킹시작") && entity.피킹시작일시 is null)
        {
            entity.피킹시작일시 = now;
            changed = true;
        }

        if (ContainsAny(stage, "피킹 완료", "피킹완료") && entity.피킹완료일시 is null)
        {
            entity.피킹완료일시 = now;
            changed = true;
        }

        if (ContainsAny(stage, "포장 완료", "포장완료") && entity.포장완료일시 is null)
        {
            entity.포장완료일시 = now;
            changed = true;
        }

        if (string.Equals(nextState, 출고상태.출고완료, StringComparison.Ordinal) && entity.출고완료일시 is null)
        {
            entity.출고완료일시 = now;
            changed = true;
        }

        entity.커뮤니티원장동기화시각Utc = now;
        entity.UpdatedAt = now;
        return true;
    }

    private static bool ApplyLedgerLink(string? current, string? value, Action<string> setter)
    {
        var cleaned = 입출고원장업무투영Snapshot.Clean(value);
        if (cleaned is null || string.Equals(current, cleaned, StringComparison.Ordinal))
        {
            return false;
        }

        setter(cleaned);
        return true;
    }

    private static bool ContainsAny(string? source, params string[] candidates)
    {
        var text = 입출고원장업무투영Snapshot.Clean(source);
        return text is not null && candidates.Any(candidate => text.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class 입출고원장업무투영Snapshot
{
    public string LedgerId { get; init; } = string.Empty;
    public string LedgerTemplateKey { get; init; } = string.Empty;
    public string LedgerState { get; init; } = string.Empty;
    public string StageText { get; init; } = string.Empty;
    public long? 입고요청Id { get; init; }
    public long? 입고상품Id { get; init; }
    public long? 출고예정Id { get; init; }
    public long? 출고묶음Id { get; init; }
    public string? 출고묶음번호 { get; init; }
    public string? 주문참조번호 { get; init; }
    public string? 운송의뢰Id { get; init; }
    public string? SourceId { get; init; }
    public string? SourceType { get; init; }

    public static bool 처리대상인가(커뮤니티원장Dto 원장)
    {
        if (string.Equals(원장.원장템플릿Key, CommunityLedgerTemplateKeys.WarehouseInbound, StringComparison.OrdinalIgnoreCase)
            || string.Equals(원장.원장템플릿Key, CommunityLedgerTemplateKeys.WarehouseOutbound, StringComparison.OrdinalIgnoreCase)
            || string.Equals(원장.원장템플릿Key, CommunityLedgerTemplateKeys.HongdalMart, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGet(
            원장.외부참조,
            "입고요청Id",
            "입고상품Id",
            "출고예정Id",
            "출고묶음Id",
            "출고묶음번호",
            "WarehouseInboundId",
            "WarehouseOutboundId",
            "RdbWarehouseProjectionType") is not null)
        {
            return true;
        }

        return 원장.블록목록.Any(block =>
        {
            var entityHint = TryGet(block.Data, "업무엔티티", "Entity", "Projection");
            return ContainsAny(entityHint, "입고요청", "입고상품", "출고예정", "출고묶음", "Warehouse", "HongdalMart")
                   || ContainsAny(block.BlockId, "warehouse", "inbound", "outbound", "picking", "packing", "mart")
                   || ContainsAny(block.Title, "창고", "입고", "출고", "피킹", "포장", "마트");
        });
    }

    public static 입출고원장업무투영Snapshot? 생성(커뮤니티원장Dto 원장)
    {
        if (!처리대상인가(원장))
        {
            return null;
        }

        var inboundBlock = FindBlock(원장, block =>
            ContainsAny(block.BlockId, "inbound")
            || ContainsAny(block.Title, "입고")
            || ContainsAny(TryGet(block.Data, "업무엔티티", "Entity"), "입고요청"));
        var inboundItemBlock = FindBlock(원장, block =>
            ContainsAny(block.BlockId, "inventory", "stock", "inbound-item")
            || ContainsAny(block.Title, "재고", "입고 상품", "입고상품")
            || ContainsAny(TryGet(block.Data, "업무엔티티", "Entity"), "입고상품"));
        var outboundBlock = FindBlock(원장, block =>
            ContainsAny(block.BlockId, "outbound")
            || ContainsAny(block.Title, "출고", "출고 품목")
            || ContainsAny(TryGet(block.Data, "업무엔티티", "Entity"), "출고예정"));
        var bundleBlock = FindBlock(원장, block =>
            ContainsAny(block.BlockId, "bundle", "picking", "packing")
            || ContainsAny(block.Title, "출고 묶음", "출고묶음", "피킹", "포장")
            || ContainsAny(TryGet(block.Data, "업무엔티티", "Entity"), "출고묶음"));

        var sourceId = FirstNonEmpty(
            TryGet(원장.외부참조, "원천Id", "SourceId", "sourceId"),
            TryGet(outboundBlock?.Data, "원천Id", "SourceId", "sourceId"),
            TryGet(inboundBlock?.Data, "원천Id", "SourceId", "sourceId"));

        return new 입출고원장업무투영Snapshot
        {
            LedgerId = Clean(원장.원장Id) ?? string.Empty,
            LedgerTemplateKey = Clean(원장.원장템플릿Key) ?? string.Empty,
            LedgerState = Clean(원장.상태) ?? 커뮤니티원장상태.초안,
            StageText = FirstNonEmpty(원장.현재단계Key, 원장.상태) ?? string.Empty,
            SourceId = sourceId,
            SourceType = FirstNonEmpty(
                TryGet(원장.외부참조, "원천유형", "SourceType", "sourceType"),
                TryGet(outboundBlock?.Data, "원천유형", "SourceType", "sourceType"),
                TryGet(inboundBlock?.Data, "원천유형", "SourceType", "sourceType")),
            입고요청Id = FirstLong(
                TryGet(원장.외부참조, "입고요청Id", "InboundId", "WarehouseInboundId", "warehouseInboundId"),
                TryGet(inboundBlock?.Data, "입고요청Id", "InboundId", "WarehouseInboundId", "warehouseInboundId"),
                ExtractLongAfterPrefix(원장.원장Id, "warehouse-inbound:", "inbound:"),
                string.Equals(원장.원장템플릿Key, CommunityLedgerTemplateKeys.WarehouseInbound, StringComparison.OrdinalIgnoreCase) ? sourceId : null),
            입고상품Id = FirstLong(
                TryGet(원장.외부참조, "입고상품Id", "InboundItemId", "InventoryItemId"),
                TryGet(inboundItemBlock?.Data, "입고상품Id", "InboundItemId", "InventoryItemId")),
            출고예정Id = FirstLong(
                TryGet(원장.외부참조, "출고예정Id", "OutboundId", "WarehouseOutboundId", "outboundLineId"),
                TryGet(outboundBlock?.Data, "출고예정Id", "OutboundId", "WarehouseOutboundId", "outboundLineId"),
                ExtractLongAfterPrefix(원장.원장Id, "warehouse-outbound:", "outbound:"),
                string.Equals(원장.원장템플릿Key, CommunityLedgerTemplateKeys.WarehouseOutbound, StringComparison.OrdinalIgnoreCase) ? sourceId : null),
            출고묶음Id = FirstLong(
                TryGet(원장.외부참조, "출고묶음Id", "OutboundBundleId", "outboundBundleId"),
                TryGet(bundleBlock?.Data, "출고묶음Id", "OutboundBundleId", "outboundBundleId")),
            출고묶음번호 = FirstNonEmpty(
                TryGet(원장.외부참조, "출고묶음번호", "OutboundBundleNo", "outboundBundleNo"),
                TryGet(bundleBlock?.Data, "출고묶음번호", "OutboundBundleNo", "outboundBundleNo")),
            주문참조번호 = FirstNonEmpty(
                TryGet(원장.외부참조, "주문참조번호", "주문번호", "OrderNo", "orderNo", "OrderReferenceNo"),
                TryGet(outboundBlock?.Data, "주문참조번호", "주문번호", "OrderNo", "orderNo", "OrderReferenceNo"),
                TryGet(inboundBlock?.Data, "주문참조번호", "주문번호", "OrderNo", "orderNo", "OrderReferenceNo"),
                IsLikelyReference(sourceId) ? sourceId : null),
            운송의뢰Id = FirstNonEmpty(
                TryGet(원장.외부참조, "운송의뢰Id", "화주운송의뢰Id", "RequestId", "requestId"),
                TryGet(outboundBlock?.Data, "운송의뢰Id", "화주운송의뢰Id", "RequestId", "requestId"),
                TryGet(bundleBlock?.Data, "운송의뢰Id", "화주운송의뢰Id", "RequestId", "requestId"))
        };
    }

    public string? ResolveInboundState()
    {
        if (ContainsAny(StageText, "취소"))
        {
            return 입고상태.취소;
        }

        if (string.Equals(LedgerState, 커뮤니티원장상태.완료, StringComparison.OrdinalIgnoreCase)
            || ContainsAny(StageText, "입고 완료", "입고완료", "검수 완료", "검수완료", "입고 마감"))
        {
            return 입고상태.입고완료;
        }

        if (ContainsAny(StageText, "운송중", "납품중", "입고 시작", "입고시작"))
        {
            return 입고상태.운송중;
        }

        return null;
    }

    public string? ResolveOutboundState()
    {
        if (ContainsAny(StageText, "취소"))
        {
            return 출고상태.취소;
        }

        if (string.Equals(LedgerState, 커뮤니티원장상태.완료, StringComparison.OrdinalIgnoreCase)
            || ContainsAny(StageText, "출고 완료", "출고완료", "운송 인계", "운송인계", "기사 인계", "기사인계", "전달 완료", "전달완료"))
        {
            return 출고상태.출고완료;
        }

        if (string.Equals(LedgerState, 커뮤니티원장상태.진행중, StringComparison.OrdinalIgnoreCase)
            || ContainsAny(StageText, "피킹", "포장", "검수", "출고 준비", "출고준비"))
        {
            return 출고상태.준비중;
        }

        return null;
    }

    public static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static 커뮤니티원장블록Dto? FindBlock(커뮤니티원장Dto 원장, Func<커뮤니티원장블록Dto, bool> predicate)
        => 원장.블록목록.FirstOrDefault(predicate);

    private static string? TryGet(IReadOnlyDictionary<string, string>? data, params string[] keys)
    {
        if (data is null || data.Count == 0)
        {
            return null;
        }

        foreach (var key in keys)
        {
            foreach (var pair in data)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return Clean(pair.Value);
                }
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.Select(Clean).FirstOrDefault(value => value is not null);

    private static long? FirstLong(params string?[] values)
        => values.Select(ParseLong).FirstOrDefault(value => value.HasValue);

    private static long? ParseLong(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null)
        {
            return null;
        }

        return long.TryParse(cleaned, out var parsed) ? parsed : null;
    }

    private static string? ExtractLongAfterPrefix(string? value, params string[] prefixes)
    {
        var cleaned = Clean(value);
        if (cleaned is null)
        {
            return null;
        }

        foreach (var prefix in prefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return cleaned[prefix.Length..];
            }
        }

        return null;
    }

    private static bool IsLikelyReference(string? value)
    {
        var cleaned = Clean(value);
        return cleaned is not null && !long.TryParse(cleaned, out _);
    }

    private static bool ContainsAny(string? source, params string[] candidates)
    {
        var text = Clean(source);
        return text is not null && candidates.Any(candidate => text.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }
}
