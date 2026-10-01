using Hongdal.Contracts.Common.Community;

namespace Hongdal.Tests.Contracts.Common.Community;

public sealed class CommunityGroupImportLedgerPlanBuilderTests
{
    [Fact]
    public void Preview_DirectDestination_CreatesNoWarehouseLedgers()
    {
        var result = CommunityGroupImportLedgerPlanBuilder.Preview(
            CreateRequest(CommunityGroupImportLogisticsRouteCodes.DirectDestination));

        Assert.True(result.Ready);
        Assert.Contains(result.Nodes, x => x.RelationRole == 공동수입원장관계역할.원천공동구매);
        Assert.Contains(result.Nodes, x => x.RelationRole == 공동수입원장관계역할.국제운송);
        Assert.Contains(result.Nodes, x => x.RelationRole == 공동수입원장관계역할.국내운송);
        Assert.DoesNotContain(result.Nodes, x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseInbound);
        Assert.DoesNotContain(result.Nodes, x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseOutbound);
    }

    [Fact]
    public void Preview_ThreePl_CreatesInboundOutboundAndFinalTransport()
    {
        var request = CreateRequest(CommunityGroupImportLogisticsRouteCodes.ThreePlWarehouse);
        SetVerifiedWarehouse(request);
        request.RequiresWarehouseOutbound = true;
        request.RequiresFinalDestinationDelivery = true;

        var result = CommunityGroupImportLedgerPlanBuilder.Preview(request);

        Assert.True(result.Ready);
        Assert.Contains(result.Nodes, x => x.NodeId == "three-pl-inbound");
        Assert.Contains(result.Nodes, x => x.NodeId == "three-pl-outbound");
        Assert.Contains(result.Nodes, x => x.NodeId == "domestic-transport-from-three-pl");
        Assert.Equal(3, result.Nodes.Count(x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.CargoTransport));
    }

    [Fact]
    public void Preview_DedicatedWarehouseStorageOnly_CreatesInboundWithoutOutbound()
    {
        var request = CreateRequest(CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse);
        SetVerifiedWarehouse(request);
        request.RequiresWarehouseOutbound = false;
        request.RequiresFinalDestinationDelivery = false;

        var result = CommunityGroupImportLedgerPlanBuilder.Preview(request);

        Assert.True(result.Ready);
        Assert.Equal("전용 창고 입고·보관", result.LogisticsRouteLabel);
        Assert.Contains(result.Nodes, x => x.NodeId == "dedicated-warehouse-inbound");
        Assert.DoesNotContain(result.Nodes, x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseOutbound);
        Assert.DoesNotContain(result.Nodes, x => x.NodeId == "domestic-transport-from-dedicated-warehouse");
    }

    [Fact]
    public void Preview_FinalDeliveryWithoutWarehouseOutbound_IsNotReady()
    {
        var request = CreateRequest(CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse);
        SetVerifiedWarehouse(request);
        request.RequiresFinalDestinationDelivery = true;

        var result = CommunityGroupImportLedgerPlanBuilder.Preview(request);

        Assert.False(result.Ready);
        Assert.Contains(result.Warnings, x => x.Contains("출고·분배 단계", StringComparison.Ordinal));
    }

    private static CommunityGroupImportLedgerConversionRequest CreateRequest(string routeCode)
        => new()
        {
            GroupPurchaseCampaignId = Guid.NewGuid(),
            LogisticsRouteCode = routeCode,
            ProductSummary = "태국산 망고",
            PlannedQuantity = 1_000,
            QuantityUnit = "kg",
            InternationalTransportMode = "LCL",
            FinalDestinationLabel = "서울 공동 수령지"
        };

    private static void SetVerifiedWarehouse(CommunityGroupImportLedgerConversionRequest request)
    {
        request.WarehouseReferenceKey = "warehouse:verified";
        request.WarehouseDisplayName = "검증된 물류센터";
        request.WarehouseOperatorConsentConfirmed = true;
        request.WarehouseSiteVerified = true;
        request.WarehouseBulkReceivingSupported = true;
        request.WarehouseStorageSupported = true;
        request.WarehouseOutboundSupported = true;
    }
}
