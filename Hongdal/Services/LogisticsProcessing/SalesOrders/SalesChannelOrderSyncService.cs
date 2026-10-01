using Hongdal.Contracts.Common.Sales;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Services.LogisticsProcessing.Warehouse;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.LogisticsProcessing.SalesOrders;

public interface ISalesChannelOrderSyncService
{
    Task<SalesChannelOrderSyncResult> SyncAsync(string syncScope, CancellationToken cancellationToken);
}

public sealed class SalesChannelOrderSyncService : ISalesChannelOrderSyncService
{
    private readonly HongdalContext _db;
    private readonly IReadOnlyList<ISalesChannelOrderFeedClient> _feedClients;
    private readonly IOutboundBatchEngine _outboundBatchEngine;
    private readonly SalesChannelOrderSyncOptions _options;
    private readonly ILogger<SalesChannelOrderSyncService> _logger;

    public SalesChannelOrderSyncService(
        HongdalContext db,
        IEnumerable<ISalesChannelOrderFeedClient> feedClients,
        IOutboundBatchEngine outboundBatchEngine,
        Microsoft.Extensions.Options.IOptions<SalesChannelOrderSyncOptions> options,
        ILogger<SalesChannelOrderSyncService> logger)
    {
        _db = db;
        _feedClients = feedClients.ToArray();
        _outboundBatchEngine = outboundBatchEngine;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SalesChannelOrderSyncResult> SyncAsync(string syncScope, CancellationToken cancellationToken)
    {
        var normalizedScope = NormalizeScope(syncScope);
        if (!_options.Enabled)
        {
            return new SalesChannelOrderSyncResult(normalizedScope, 0, 0, 0, 0);
        }

        var channelTypes = CommerceChannelOrderSyncScopes.GetChannelTypes(normalizedScope);
        var accounts = await _db.판매채널계정
            .Where(x => channelTypes.Contains(x.채널종류) && x.연결상태 != "해제")
            .OrderBy(x => x.마지막동기화일시 ?? DateTime.MinValue)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(cancellationToken);

        var fetchedOrderCount = 0;
        var createdOutboundCount = 0;
        var skippedOrderCount = 0;
        var now = DateTime.UtcNow;

        foreach (var account in accounts)
        {
            var client = _feedClients.FirstOrDefault(x => x.CanFetch(account.채널종류, normalizedScope));

            if (client is null)
            {
                _logger.LogInformation(
                    "Action={Action} ChannelType={ChannelType} SyncScope={SyncScope} AccountId={AccountId}",
                    "SalesChannelOrderFeedClientMissing",
                    account.채널종류,
                    normalizedScope,
                    account.Id);
                continue;
            }

            var sinceUtc = account.마지막동기화일시 ?? now.AddMinutes(-Math.Max(1, _options.LookbackMinutes));
            var orders = await client.FetchOrdersAsync(account, sinceUtc, cancellationToken);
            fetchedOrderCount += orders.Count;
            var retryRequired = false;

            foreach (var order in orders)
            {
                var creationResult = await CreateWarehouseOutboundRequestsAsync(account, order, cancellationToken);
                retryRequired |= creationResult.RetryRequired;

                if (creationResult.CreatedCount == 0)
                {
                    skippedOrderCount++;
                }
                else
                {
                    createdOutboundCount += creationResult.CreatedCount;
                }
            }

            if (!retryRequired)
            {
                account.마지막동기화일시 = now;
                account.UpdatedAt = now;
            }
            else
            {
                _logger.LogInformation(
                    "Action={Action} AccountId={AccountId} SyncScope={SyncScope}",
                    "SalesChannelOrderSyncCursorRetainedForRetry",
                    account.Id,
                    normalizedScope);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new SalesChannelOrderSyncResult(
            normalizedScope,
            accounts.Count,
            fetchedOrderCount,
            createdOutboundCount,
            skippedOrderCount);
    }

    private async Task<WarehouseOutboundCreationResult> CreateWarehouseOutboundRequestsAsync(
        홍달.도메인.판매.판매채널계정 account,
        SalesChannelOrderFeedEntry order,
        CancellationToken cancellationToken)
    {
        if (order.Items.Count == 0 || string.IsNullOrWhiteSpace(order.ChannelOrderNo))
        {
            return WarehouseOutboundCreationResult.Skipped;
        }

        var orderReference = CreateOrderReference(order.ChannelType, order.ChannelOrderNo);
        var now = DateTime.UtcNow;
        var lines = new List<OutboundBatchPlanLineRequest>();

        foreach (var item in order.Items.Where(x => x.Quantity > 0))
        {
            var listingQuery =
                from listing in _db.채널출품
                join product in _db.판매상품 on listing.판매상품Id equals product.Id
                join inboundItem in _db.입고상품 on product.입고상품Id equals inboundItem.Id
                where listing.판매채널계정Id == account.Id
                      && product.소유자UserId == account.UserId
                      && (product.판매SKU == item.Sku || listing.채널상품번호 == item.ChannelProductNo)
                select new { listing, product, inboundItem };

            // 예약 재고는 ExecuteUpdateAsync로 직접 갱신되므로 같은 동기화 실행에서
            // 이전 주문의 추적 스냅샷을 재사용하지 않고 최신 DB 값을 다시 읽는다.
            var mapped = await listingQuery
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
            if (mapped is null)
            {
                continue;
            }

            var alreadyExists = await _db.출고예정.AnyAsync(x =>
                x.주문참조번호 == orderReference
                && x.판매자UserId == account.UserId
                && x.SKU == mapped.product.판매SKU
                && x.상태 != 홍달.도메인.창고.출고상태.취소,
                cancellationToken);

            if (alreadyExists)
            {
                continue;
            }

            if (!Hongdal.Contracts.Common.Inbound.입고계약유형코드.CanSellToMarket(mapped.inboundItem.계약유형))
            {
                continue;
            }

            lines.Add(new OutboundBatchPlanLineRequest
            {
                LineKey = CreateLineKey(mapped.product.판매SKU, lines.Count + 1),
                SalesProductId = mapped.product.Id,
                PreferredInboundProductId = mapped.inboundItem.Id,
                Sku = mapped.product.판매SKU,
                ProductName = string.IsNullOrWhiteSpace(item.ProductName) ? mapped.product.대표상품명 : item.ProductName,
                Quantity = item.Quantity
            });
        }

        if (lines.Count == 0)
        {
            return WarehouseOutboundCreationResult.Skipped;
        }

        var plan = await _outboundBatchEngine.PlanAsync(new OutboundBatchPlanRequest
        {
            OrderReference = orderReference,
            SellerUserId = account.UserId,
            OrdererUserId = string.IsNullOrWhiteSpace(order.BuyerName) ? order.RecipientName : order.BuyerName,
            DestinationAddress = order.RecipientAddress,
            Lines = lines
        }, cancellationToken);

        if (!CanPersistPlan(plan))
        {
            _logger.LogInformation(
                "Action={Action} OrderReference={OrderReference} UnallocatedLineCount={UnallocatedLineCount} Message={Message}",
                "OutboundBatchPlanIncompleteNotPersisted",
                orderReference,
                plan.UnallocatedLines.Count,
                plan.Message);
            return WarehouseOutboundCreationResult.Retry;
        }

        var persisted = await TryPersistCompletePlanAsync(
            account,
            order,
            orderReference,
            plan,
            now,
            cancellationToken);
        if (!persisted)
        {
            _logger.LogInformation(
                "Action={Action} OrderReference={OrderReference} Message={Message}",
                "OutboundInventoryReservationConflict",
                orderReference,
                "계획 이후 재고가 변경되어 출고 예약을 원자적으로 확보하지 못했습니다.");
            return WarehouseOutboundCreationResult.Retry;
        }

        return new WarehouseOutboundCreationResult(plan.Allocations.Count, RetryRequired: false);
    }

    private async Task<bool> TryPersistCompletePlanAsync(
        홍달.도메인.판매.판매채널계정 account,
        SalesChannelOrderFeedEntry order,
        string orderReference,
        OutboundBatchPlanResult plan,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var reservationQuantities = BuildInventoryReservationQuantities(plan.Allocations);
        var executionStrategy = _db.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var reservation in reservationQuantities.OrderBy(item => item.Key))
            {
                var quantity = reservation.Value;
                var affected = await _db.입고상품
                    .Where(item => item.Id == reservation.Key
                                   && item.판매자UserId == account.UserId
                                   && item.가용수량 >= quantity)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.가용수량, item => item.가용수량 - quantity)
                            .SetProperty(item => item.예약수량, item => item.예약수량 + quantity)
                            .SetProperty(item => item.UpdatedAt, now),
                        cancellationToken);

                if (affected != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            var outboundRows = plan.Allocations
                .Select(allocation => new 홍달.도메인.창고.출고예정
                {
                    주문참조번호 = orderReference,
                    판매상품Id = allocation.SalesProductId,
                    입고상품Id = allocation.InboundProductId,
                    판매자UserId = account.UserId,
                    주문자UserId = string.IsNullOrWhiteSpace(order.BuyerName) ? order.RecipientName : order.BuyerName,
                    출고창고Id = allocation.WarehouseId,
                    상품명 = allocation.ProductName,
                    SKU = allocation.Sku,
                    수량 = allocation.Quantity,
                    상태 = 홍달.도메인.창고.출고상태.예정,
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToArray();

            _db.출고예정.AddRange(outboundRows);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                foreach (var row in outboundRows)
                {
                    _db.Entry(row).State = EntityState.Detached;
                }

                throw;
            }
        });
    }

    internal static IReadOnlyDictionary<long, int> BuildInventoryReservationQuantities(
        IReadOnlyList<OutboundBatchAllocation> allocations)
        => allocations
            .Where(allocation => allocation.Quantity > 0)
            .GroupBy(allocation => allocation.InboundProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(allocation => allocation.Quantity));

    internal static bool CanPersistPlan(OutboundBatchPlanResult plan)
        => plan.IsComplete
           && plan.Allocations.Count > 0
           && plan.Allocations.All(allocation => allocation.InboundProductId > 0 && allocation.Quantity > 0)
           && plan.UnallocatedLines.Count == 0;

    private static string NormalizeScope(string syncScope)
        => string.Equals(syncScope, CommerceChannelOrderSyncScopes.Overseas, StringComparison.OrdinalIgnoreCase)
            ? CommerceChannelOrderSyncScopes.Overseas
            : CommerceChannelOrderSyncScopes.Domestic;

    private static string CreateOrderReference(string channelType, string channelOrderNo)
        => $"{channelType.Trim()}:{channelOrderNo.Trim()}";

    private static string CreateLineKey(string sku, int sequence)
        => $"{sku.Trim()}#{sequence}";

    private readonly record struct WarehouseOutboundCreationResult(int CreatedCount, bool RetryRequired)
    {
        public static WarehouseOutboundCreationResult Skipped => new(0, RetryRequired: false);

        public static WarehouseOutboundCreationResult Retry => new(0, RetryRequired: true);
    }
}
