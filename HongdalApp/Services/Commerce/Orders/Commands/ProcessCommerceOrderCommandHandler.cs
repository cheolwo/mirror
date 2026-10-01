using Hongdal.Contracts.Common.Sales;
using HongdalApp.Services.Application;
using HongdalApp.Services.Commerce.Orders.Events;
using HongdalApp.Services.Warehouse.Fulfillment;

namespace HongdalApp.Services.Commerce.Orders.Commands;

public sealed class ProcessCommerceOrderCommandHandler : IAppCommandHandler<ProcessCommerceOrderCommand, CommerceOrderFulfillmentResult>
{
    private static readonly HashSet<string> DomesticChannels =
    [
        CommerceChannelKeys.SmartStore,
        CommerceChannelKeys.Coupang,
        CommerceChannelKeys.ElevenStreet
    ];

    private readonly InMemoryShipperStore _store;
    private readonly IWarehousePickingPlanner _pickingPlanner;
    private readonly IAppEventPublisher _eventPublisher;

    public ProcessCommerceOrderCommandHandler(
        InMemoryShipperStore store,
        IWarehousePickingPlanner pickingPlanner,
        IAppEventPublisher eventPublisher)
    {
        _store = store;
        _pickingPlanner = pickingPlanner;
        _eventPublisher = eventPublisher;
    }

    public async Task<CommerceOrderFulfillmentResult> HandleAsync(ProcessCommerceOrderCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var order = command.Order;
        var orderScope = ResolveOrderScope(order.ChannelType);
        if (_store.HasWarehouseOutboundNotification(order.ChannelType, order.ChannelOrderNo))
        {
            return new CommerceOrderFulfillmentResult
            {
                OrderScope = orderScope,
                ChannelType = order.ChannelType,
                ChannelOrderNo = order.ChannelOrderNo,
                Notifications = []
            };
        }

        var plannedLines = order.Items.Select(item =>
        {
            var product = _store.FindProductBySku(item.Sku);
            var inventory = product is null ? null : _store.FindInventoryByInboundProductId(product.입고상품Id);
            var warehouse = inventory is null ? null : _store.FindWarehouse(inventory.창고Id);
            var pickPlan = warehouse is null ? null : _pickingPlanner.Plan(warehouse.Id, item.Sku, item.Quantity);

            return new
            {
                Item = item,
                Product = product,
                Inventory = inventory,
                Warehouse = warehouse,
                PickPlan = pickPlan,
                CanSellToMarket = inventory?.계약정보.마켓판매가능여부 == true
            };
        }).ToList();

        var reservationRequests = plannedLines
            .Where(x => x.Inventory is not null && x.CanSellToMarket && x.PickPlan?.IsComplete == true)
            .Select(x => new WarehouseFulfillmentReservationRequest
            {
                InboundProductId = x.Inventory!.입고상품Id,
                Quantity = x.Item.Quantity,
                PickPlan = x.PickPlan!
            })
            .ToList();

        var orderReserved = reservationRequests.Count == plannedLines.Count
            && _store.TryReserveFulfillmentOrder(reservationRequests);

        var notifications = new List<WarehouseOutboundNotification>();
        var restockNotifications = new List<InboundRestockNotification>();
        foreach (var line in plannedLines)
        {
            var reserved = false;
            if (orderReserved && line.Inventory is not null && line.PickPlan?.IsComplete == true)
            {
                reserved = true;
            }

            notifications.Add(_store.CreateWarehouseOutboundNotification(new WarehouseOutboundNotification
            {
                OrderScope = orderScope,
                ChannelType = order.ChannelType,
                ChannelOrderNo = order.ChannelOrderNo,
                WarehouseId = line.Warehouse?.Id,
                WarehouseName = line.Warehouse?.창고명 ?? "미매칭",
                WarehouseManagerName = line.Warehouse?.담당자명 ?? string.Empty,
                ProductName = line.Product?.대표상품명 ?? line.Item.ProductName,
                Sku = line.Item.Sku,
                RequestedQuantity = line.Item.Quantity,
                RecipientName = order.RecipientName,
                RecipientAddress = order.RecipientAddress,
                Status = reserved && line.PickPlan?.IsComplete == true ? WarehouseOutboundNotificationStatusCodes.Ready : WarehouseOutboundNotificationStatusCodes.Blocked,
                Message = CreateMessage(orderScope, order, line.Item, reserved, line.CanSellToMarket, line.Warehouse?.담당자명, line.PickPlan),
                PickPlan = line.PickPlan
            }));

            if (reserved && line.Product is not null && line.Inventory is not null)
            {
                var restockNotification = _store.CreateInboundRestockNotificationIfNeeded(
                    order.ChannelType,
                    order.ChannelOrderNo,
                    line.Product,
                    line.Inventory);
                if (restockNotification is not null)
                {
                    restockNotifications.Add(restockNotification);
                }
            }
        }

        _store.CreateOrUpdateOrderPickingTask(order.ChannelType, order.ChannelOrderNo);

        var result = new CommerceOrderFulfillmentResult
        {
            OrderScope = orderScope,
            ChannelType = order.ChannelType,
            ChannelOrderNo = order.ChannelOrderNo,
            Notifications = notifications,
            RestockNotifications = restockNotifications
        };

        await _eventPublisher.PublishAsync(
            new CommerceOrderProcessedEvent(order.ChannelType, order.ChannelOrderNo, orderScope, notifications.Count, DateTime.UtcNow),
            cancellationToken);

        return result;
    }

    private static string ResolveOrderScope(string channelType)
        => DomesticChannels.Contains(channelType) ? CommerceOrderScopeCodes.Domestic : CommerceOrderScopeCodes.International;

    private static string CreateMessage(
        string orderScope,
        ExternalCommerceOrder order,
        ExternalCommerceOrderItem item,
        bool reserved,
        bool canSellToMarket,
        string? managerName,
        WarehousePickPlan? pickPlan)
    {
        if (!canSellToMarket)
        {
            return $"{orderScope} 주문 {order.ChannelOrderNo}의 {item.Sku}는 입고 계약상 마켓 판매 대상이 아닙니다.";
        }

        if (!reserved || pickPlan is null || !pickPlan.IsComplete)
        {
            return $"{orderScope} 주문 {order.ChannelOrderNo}의 {item.Sku} 재고를 확인해야 합니다.";
        }

        var assignee = string.IsNullOrWhiteSpace(managerName) ? "창고 담당자" : managerName;
        var firstBin = pickPlan.Instructions.FirstOrDefault()?.BinCode;
        return string.IsNullOrWhiteSpace(firstBin)
            ? $"{assignee}에게 {orderScope} 주문 {order.ChannelOrderNo} 출고 준비 알림을 생성했습니다."
            : $"{assignee}에게 {orderScope} 주문 {order.ChannelOrderNo} 출고 준비 알림을 생성했습니다. 우선 피킹 적재함: {firstBin}.";
    }
}
