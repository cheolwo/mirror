using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Services.LogisticsProcessing.SalesOrders;

namespace Hongdal.Tests.Services.LogisticsProcessing.SalesOrders;

public sealed class SalesChannelOrderSyncServiceTests
{
    [Fact]
    public void CanPersistPlan_rejects_partial_allocations()
    {
        var plan = new OutboundBatchPlanResult
        {
            IsComplete = false,
            Allocations =
            [
                new OutboundBatchAllocation
                {
                    LineKey = "line-1",
                    InboundProductId = 101,
                    Quantity = 2
                }
            ],
            UnallocatedLines =
            [
                new OutboundBatchUnallocatedLine
                {
                    LineKey = "line-1",
                    RequestedQuantity = 3,
                    PlannedQuantity = 2
                }
            ]
        };

        var canPersist = SalesChannelOrderSyncService.CanPersistPlan(plan);

        Assert.False(canPersist);
    }

    [Fact]
    public void CanPersistPlan_accepts_only_complete_nonempty_plan()
    {
        var plan = new OutboundBatchPlanResult
        {
            IsComplete = true,
            Allocations =
            [
                new OutboundBatchAllocation
                {
                    LineKey = "line-1",
                    InboundProductId = 101,
                    Quantity = 3
                }
            ]
        };

        var canPersist = SalesChannelOrderSyncService.CanPersistPlan(plan);

        Assert.True(canPersist);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(101, 0)]
    [InlineData(101, -1)]
    public void CanPersistPlan_rejects_invalid_inventory_allocations(long inboundProductId, int quantity)
    {
        var plan = new OutboundBatchPlanResult
        {
            IsComplete = true,
            Allocations =
            [
                new OutboundBatchAllocation
                {
                    LineKey = "line-1",
                    InboundProductId = inboundProductId,
                    Quantity = quantity
                }
            ]
        };

        Assert.False(SalesChannelOrderSyncService.CanPersistPlan(plan));
    }

    [Fact]
    public void BuildInventoryReservationQuantities_aggregates_allocations_by_inbound_product()
    {
        var allocations = new[]
        {
            new OutboundBatchAllocation { InboundProductId = 101, Quantity = 2 },
            new OutboundBatchAllocation { InboundProductId = 101, Quantity = 3 },
            new OutboundBatchAllocation { InboundProductId = 202, Quantity = 4 }
        };

        var reservations = SalesChannelOrderSyncService.BuildInventoryReservationQuantities(allocations);

        Assert.Equal(5, reservations[101]);
        Assert.Equal(4, reservations[202]);
    }
}
