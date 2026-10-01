using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.Orderer;

namespace Hongdal.Tests.Contracts.Common.Orderer;

public sealed class DomesticGroupPurchaseFulfillmentPlanBuilderTests
{
    [Fact]
    public void Preview_DirectCollectionPoint_ContainsOnlySaleAndDirectTransportLedgers()
    {
        var result = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(
            CreateRequest(DomesticGroupPurchaseFulfillmentRouteCodes.DirectCollectionPoint));

        Assert.True(result.OrderPlacementReady);
        Assert.Equal(3, result.LedgerNodes.Count);
        Assert.Contains(result.LedgerNodes, x => x.IsOrderRoot && x.LedgerTemplateKey == CommunityLedgerTemplateKeys.Order);
        Assert.Contains(result.LedgerNodes, x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.LocalSale);
        Assert.Contains(result.LedgerNodes, x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.CargoTransport);
        Assert.DoesNotContain(result.LedgerNodes, x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseInbound);
        Assert.DoesNotContain(result.LedgerNodes, x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseOutbound);
    }

    [Fact]
    public void Preview_TraditionalMarketHub_AddsHubInboundSortingOutboundAndLastMile()
    {
        var request = CreateRequest(DomesticGroupPurchaseFulfillmentRouteCodes.TraditionalMarketHub);
        request.HubReferenceKey = "traditional-market-hub:sample";
        request.HubDisplayName = "샘플 전통시장 공동물류 거점";
        request.RequiresSorting = true;
        request.RequiresLastMileDelivery = true;
        SetVerifiedHubCapabilities(request, supportsStorage: false);

        var result = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(request);

        Assert.True(result.OrderPlacementReady);
        Assert.Contains(result.LedgerNodes, x => x.NodeId == "market-hub-inbound" && x.IncludedLedgerRole == 주문원장포함역할.창고입고);
        Assert.Contains(result.LedgerNodes, x => x.NodeId == "market-hub-outbound" && x.IncludedLedgerRole == 주문원장포함역할.창고출고);
        Assert.Equal(2, result.LedgerNodes.Count(x => x.LedgerTemplateKey == CommunityLedgerTemplateKeys.CargoTransport));
    }

    [Fact]
    public void Preview_ThirdPartyLogistics_AlwaysAddsWarehouseInboundAndOutbound()
    {
        var request = CreateRequest(DomesticGroupPurchaseFulfillmentRouteCodes.ThirdPartyLogistics);
        request.HubReferenceKey = "third-party-logistics:sample";
        request.HubDisplayName = "샘플 3PL";
        request.RequiresLastMileDelivery = true;
        SetVerifiedHubCapabilities(request, supportsStorage: true);

        var result = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(request);

        Assert.True(result.OrderPlacementReady);
        Assert.Contains(result.LedgerNodes, x => x.NodeId == "third-party-inbound");
        Assert.Contains(result.LedgerNodes, x => x.NodeId == "third-party-outbound");
        Assert.All(
            result.LedgerNodes.Where(x => !x.IsOrderRoot),
            node => Assert.Contains(
                result.LedgerEdges,
                edge => edge.FromNodeId == "order-root"
                        && edge.ToNodeId == node.NodeId
                        && edge.RelationType == CommunityLedgerRelationTypes.Contains));
    }

    [Fact]
    public void Preview_DedicatedWarehouseStorageOnly_AddsInboundWithoutOutbound()
    {
        var request = CreateRequest(DomesticGroupPurchaseFulfillmentRouteCodes.DedicatedWarehouse);
        request.HubReferenceKey = "dedicated-warehouse:sample";
        request.HubDisplayName = "공동구매 전용 창고";
        request.RequiresStorage = true;
        request.RequiresSorting = false;
        request.RequiresLastMileDelivery = false;
        SetVerifiedHubCapabilities(request, supportsStorage: true);

        var result = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(request);

        Assert.True(result.OrderPlacementReady);
        Assert.Contains(result.LedgerNodes, x => x.NodeId == "dedicated-warehouse-inbound");
        Assert.DoesNotContain(result.LedgerNodes, x => x.NodeId == "dedicated-warehouse-outbound");
        Assert.DoesNotContain(result.LedgerNodes, x => x.NodeId == "transport-from-dedicated-warehouse");
    }

    [Fact]
    public async Task CreateOrderDraftAsync_DoesNotPlaceOrderOrPersistLedgers_AndLimitsReadToOwner()
    {
        var service = new DomesticGroupPurchaseFulfillmentPlanService(
            new InMemoryDomesticGroupPurchaseFulfillmentOrderDraftStore());

        var created = await service.CreateOrderDraftAsync(
            "representative-1",
            CreateRequest(DomesticGroupPurchaseFulfillmentRouteCodes.DirectCollectionPoint));

        Assert.Equal(DomesticGroupPurchaseFulfillmentDraftStatuses.Draft, created.StatusCode);
        Assert.False(created.IsDurablyPersisted);
        Assert.False(created.Plan.LedgersPersisted);
        Assert.False(created.Plan.OrderPlaced);
        Assert.NotNull(await service.GetOrderDraftAsync("representative-1", created.DraftId));
        Assert.Null(await service.GetOrderDraftAsync("representative-2", created.DraftId));
        Assert.Equal(64, created.Plan.PlanFingerprint.Length);
        Assert.Equal("고구마", created.Plan.RequestSnapshot.ProductSummary);
        Assert.Equal(
            CommunityGroupPurchaseAgreementPolicy.PolicyCode,
            created.AgreementPolicyCode);
        Assert.Equal(created.Plan.AgreementPolicyCode, created.AgreementPolicyCode);
        Assert.Contains("제안의 선후만으로", created.ProposalOriginLegalEffectNotice);
        Assert.Contains("생산자와 공동구매 대표가 합의한 최종 계약문", created.GuidanceMessage);
    }

    [Fact]
    public void Preview_HubWithoutCapacityOrAgreement_IsNotReadyToPlaceOrder()
    {
        var request = CreateRequest(DomesticGroupPurchaseFulfillmentRouteCodes.TraditionalMarketHub);
        request.HubDisplayName = "검증 전 시장 거점";
        request.ProducerTermsAccepted = false;
        request.HubCapabilities = new DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot();

        var result = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(request);

        Assert.False(result.OrderPlacementReady);
        Assert.Contains(result.PlanningWarnings, x => x.Contains("생산자의 공급", StringComparison.Ordinal));
        Assert.Contains(result.PlanningWarnings, x => x.Contains("운영자의 사용 동의", StringComparison.Ordinal));
        Assert.Contains(result.PlanningWarnings, x => x.Contains("일일 처리 가능", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_ChangedRoute_ProducesDifferentPlanFingerprint()
    {
        var direct = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(
            CreateRequest(DomesticGroupPurchaseFulfillmentRouteCodes.DirectCollectionPoint));
        var thirdPartyRequest = CreateRequest(DomesticGroupPurchaseFulfillmentRouteCodes.ThirdPartyLogistics);
        thirdPartyRequest.HubDisplayName = "검증된 3PL";
        SetVerifiedHubCapabilities(thirdPartyRequest, supportsStorage: true);

        var thirdParty = DomesticGroupPurchaseFulfillmentPlanBuilder.Preview(thirdPartyRequest);

        Assert.NotEqual(direct.PlanFingerprint, thirdParty.PlanFingerprint);
        Assert.Equal(64, direct.PlanFingerprint.Length);
        Assert.Equal("1.1", direct.PlanVersion);
        Assert.Equal(
            CommunityGroupPurchaseAgreementPolicy.PolicyCode,
            direct.AgreementPolicyCode);
    }

    private static DomesticGroupPurchaseFulfillmentPlanRequest CreateRequest(string routeCode)
        => new()
        {
            GroupPurchaseCampaignId = Guid.NewGuid(),
            CampaignTitle = "고구마 공동구매",
            RouteCode = routeCode,
            ProducerDisplayName = "해남 생산자",
            ProductSummary = "고구마",
            QuantitySummary = "500kg",
            PlannedQuantity = 500,
            QuantityUnit = "kg",
            DestinationLabel = "서울 공동 수령지",
            ProducerTermsAccepted = true,
            BuyerRepresentativeTermsAccepted = true,
            SupplyCompatibilityConfirmed = true
        };

    private static void SetVerifiedHubCapabilities(
        DomesticGroupPurchaseFulfillmentPlanRequest request,
        bool supportsStorage)
    {
        request.HubCapabilities = new DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot
        {
            HasOperatorConsent = true,
            SiteVerified = true,
            SupportsBulkReceiving = true,
            SupportsSorting = true,
            SupportsStorage = supportsStorage,
            SupportsLastMileHandoff = true,
            HandlingCapacity = 1_000,
            CapacityUnit = "kg"
        };
    }
}
