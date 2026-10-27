using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Sales;
using 살뜰.Data;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.LogisticsProcessing.SalesOrders;

public interface ISalesChannelOrderReadService
{
    Task<판매채널주문목록응답> QueryAsync(
        판매채널주문목록조회요청 request,
        CancellationToken cancellationToken);

    Task<판매채널주문상세응답?> GetAsync(
        long orderId,
        CancellationToken cancellationToken);
}

/// <summary>
/// 외부 주문 수집을 실행하지 않고, 이미 재고 예약과 함께 영속된 판매채널 출고 후보만 읽습니다.
/// </summary>
public sealed class SalesChannelOrderReadService(
    SsalddelContext db,
    ICurrentUserAccessor currentUserAccessor) : ISalesChannelOrderReadService
{
    public async Task<판매채널주문목록응답> QueryAsync(
        판매채널주문목록조회요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scoped = ApplyOwnerScope(ApplySalesOrderScope(db.출고예정.AsNoTracking()));
        scoped = ApplySyncScope(scoped, request.SyncScope);

        var grouped = scoped.GroupBy(item => new
        {
            item.판매자UserId,
            item.주문참조번호
        });

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var idMatched = long.TryParse(search, out var outboundId);
            grouped = grouped.Where(group => group.Any(item =>
                (idMatched && item.Id == outboundId)
                || item.주문참조번호.Contains(search)
                || item.상품명.Contains(search)
                || item.SKU.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            grouped = grouped.Where(group => group.Any(item => item.상태 == status));
        }

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalCount = await grouped.CountAsync(cancellationToken);
        var groupPage = await grouped
            .Select(group => new OrderGroupProjection
            {
                OrderId = group.Min(item => item.Id),
                SellerUserId = group.Key.판매자UserId,
                OrderReference = group.Key.주문참조번호,
                UpdatedAt = group.Max(item => item.UpdatedAt)
            })
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.OrderId)
            .Skip(Skip(page, pageSize))
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        var items = await LoadSummariesAsync(scoped, groupPage, cancellationToken);
        var accountQuery = db.판매채널계정.AsNoTracking().AsQueryable();
        var userId = RequireUserId();
        if (!IsServerAdmin()) accountQuery = accountQuery.Where(account => account.UserId == userId);
        if (!string.IsNullOrWhiteSpace(request.SyncScope))
        {
            var channels = CommerceChannelOrderSyncScopes.GetChannelTypes(request.SyncScope).ToList();
            accountQuery = accountQuery.Where(account => channels.Contains(account.채널종류));
        }
        var accounts = await accountQuery.Where(account => account.동기화진단Json != null).ToArrayAsync(cancellationToken);
        return new 판매채널주문목록응답
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            SyncIssues = accounts.SelectMany(SalesChannelSyncDiagnostics.Pending).OrderByDescending(issue => issue.LastObservedAtUtc).ToArray()
        };
    }

    public async Task<판매채널주문상세응답?> GetAsync(
        long orderId,
        CancellationToken cancellationToken)
    {
        var scoped = ApplyOwnerScope(ApplySalesOrderScope(db.출고예정.AsNoTracking()));
        var selected = await scoped.FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        if (selected is null)
        {
            return null;
        }

        var lines = await scoped
            .Where(item => item.판매자UserId == selected.판매자UserId
                           && item.주문참조번호 == selected.주문참조번호)
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var warehouses = await LoadWarehousesAsync(lines, cancellationToken);
        return new 판매채널주문상세응답
        {
            주문 = ToSummary(lines, warehouses),
            출고라인목록 = lines.Select(line => ToLine(line, warehouses.GetValueOrDefault(line.출고창고Id))).ToArray()
        };
    }

    private async Task<IReadOnlyList<판매채널주문요약응답>> LoadSummariesAsync(
        IQueryable<출고예정> scoped,
        IReadOnlyList<OrderGroupProjection> groupPage,
        CancellationToken cancellationToken)
    {
        if (groupPage.Count == 0)
        {
            return [];
        }

        // List<T>.Contains keeps this expression on the EF-translatable collection path.
        // The array extension can bind to the .NET 10 ReadOnlySpan overload while
        // EF evaluates the expression tree, which is not a valid expression result type.
        var references = groupPage.Select(item => item.OrderReference).Distinct().ToList();
        var selectedKeys = groupPage
            .Select(item => GroupKey(item.SellerUserId, item.OrderReference))
            .ToHashSet(StringComparer.Ordinal);
        var candidates = await scoped
            .Where(item => references.Contains(item.주문참조번호))
            .ToArrayAsync(cancellationToken);
        var lines = candidates
            .Where(item => selectedKeys.Contains(GroupKey(item.판매자UserId, item.주문참조번호)))
            .ToArray();
        var warehouses = await LoadWarehousesAsync(lines, cancellationToken);
        var summaries = lines
            .GroupBy(item => GroupKey(item.판매자UserId, item.주문참조번호), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => ToSummary(group.ToArray(), warehouses), StringComparer.Ordinal);

        return groupPage
            .Select(item => summaries[GroupKey(item.SellerUserId, item.OrderReference)])
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<long, 창고>> LoadWarehousesAsync(
        IReadOnlyCollection<출고예정> lines,
        CancellationToken cancellationToken)
    {
        var warehouseIds = lines.Select(item => item.출고창고Id).Distinct().ToList();
        return warehouseIds.Count == 0
            ? new Dictionary<long, 창고>()
            : await db.창고.AsNoTracking()
                .Where(item => warehouseIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
    }

    private IQueryable<출고예정> ApplyOwnerScope(IQueryable<출고예정> query)
    {
        var userId = RequireUserId();
        return IsServerAdmin()
            ? query
            : query.Where(item => item.판매자UserId == userId);
    }

    private static IQueryable<출고예정> ApplySalesOrderScope(IQueryable<출고예정> query)
        => query.Where(item =>
            item.주문참조번호.StartsWith(CommerceChannelKeys.SmartStore + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.Coupang + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.ElevenStreet + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.Shopify + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.Amazon + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.Ebay + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.Walmart + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.Etsy + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.TikTokShop + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.Shopee + ":")
            || item.주문참조번호.StartsWith(CommerceChannelKeys.Lazada + ":"));

    private static IQueryable<출고예정> ApplySyncScope(
        IQueryable<출고예정> query,
        string? syncScope)
    {
        if (string.Equals(syncScope, CommerceChannelOrderSyncScopes.Domestic, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(item =>
                item.주문참조번호.StartsWith(CommerceChannelKeys.SmartStore + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.Coupang + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.ElevenStreet + ":"));
        }

        if (string.Equals(syncScope, CommerceChannelOrderSyncScopes.Overseas, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(item =>
                item.주문참조번호.StartsWith(CommerceChannelKeys.Shopify + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.Amazon + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.Ebay + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.Walmart + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.Etsy + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.TikTokShop + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.Shopee + ":")
                || item.주문참조번호.StartsWith(CommerceChannelKeys.Lazada + ":"));
        }

        return query;
    }

    private static 판매채널주문요약응답 ToSummary(
        IReadOnlyList<출고예정> lines,
        IReadOnlyDictionary<long, 창고> warehouses)
    {
        var first = lines.OrderBy(item => item.Id).First();
        var (channelType, channelOrderNo) = ParseOrderReference(first.주문참조번호);
        var warehouseNames = lines
            .Select(item => warehouses.GetValueOrDefault(item.출고창고Id)?.창고명)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var warehouseCount = lines.Select(item => item.출고창고Id).Distinct().Count();

        return new 판매채널주문요약응답
        {
            OrderId = lines.Min(item => item.Id),
            주문참조번호 = first.주문참조번호,
            채널종류 = channelType,
            채널주문번호 = channelOrderNo,
            국내외구분 = CommerceChannelOrderSyncScopes.Resolve(channelType),
            출고상태 = ResolveStatus(lines),
            출고라인수 = lines.Count,
            총수량 = lines.Sum(item => item.수량),
            대표상품명 = first.상품명,
            출고창고수 = warehouseCount,
            출고창고표시 = warehouseCount switch
            {
                0 => "창고 확인 필요",
                1 when warehouseNames.Length == 1 => warehouseNames[0],
                1 => $"창고 #{first.출고창고Id}",
                _ => $"{warehouseCount:N0}개 창고"
            },
            운송인계여부 = lines.Any(item => !string.IsNullOrWhiteSpace(item.운송의뢰Id)),
            생성일시 = lines.Min(item => item.CreatedAt),
            수정일시 = lines.Max(item => item.UpdatedAt)
        };
    }

    private static 판매채널주문출고라인응답 ToLine(출고예정 line, 창고? warehouse)
        => new()
        {
            Id = line.Id,
            판매상품Id = line.판매상품Id,
            입고상품Id = line.입고상품Id,
            출고창고Id = line.출고창고Id,
            출고창고명 = warehouse?.창고명 ?? string.Empty,
            출고묶음Id = line.출고묶음Id,
            상품명 = line.상품명,
            SKU = line.SKU,
            수량 = line.수량,
            상태 = line.상태,
            운송의뢰Id = line.운송의뢰Id,
            출고처리일시 = line.출고처리일시,
            생성일시 = line.CreatedAt,
            수정일시 = line.UpdatedAt
        };

    private static string ResolveStatus(IReadOnlyList<출고예정> lines)
    {
        var states = lines.Select(item => item.상태).Distinct(StringComparer.Ordinal).ToArray();
        return states.Length == 1 ? states[0] : "복합 상태";
    }

    internal static (string ChannelType, string ChannelOrderNo) ParseOrderReference(string orderReference)
    {
        var separator = orderReference.IndexOf(':');
        return separator <= 0 || separator == orderReference.Length - 1
            ? (string.Empty, orderReference)
            : (orderReference[..separator], orderReference[(separator + 1)..]);
    }

    private string RequireUserId()
    {
        var userId = currentUserAccessor.UserId?.Trim();
        return !string.IsNullOrWhiteSpace(userId)
            ? userId
            : throw new InvalidOperationException("로그인 사용자를 확인할 수 없습니다.");
    }

    private bool IsServerAdmin()
        => string.Equals(currentUserAccessor.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase);

    private static string GroupKey(string sellerUserId, string orderReference)
        => $"{sellerUserId}\u001f{orderReference}";

    private static int Skip(int page, int pageSize)
        => (int)Math.Min((long)page * pageSize, int.MaxValue);

    private sealed class OrderGroupProjection
    {
        public long OrderId { get; init; }
        public string SellerUserId { get; init; } = string.Empty;
        public string OrderReference { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; }
    }
}
