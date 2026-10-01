using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public static class UnitedStatesThirdPartyLogisticsProviderCatalog
{
    public const string CatalogVersion = "US-3PL-2026-07-18-03";

    public static DateOnly SnapshotReviewedOn { get; } = new(2026, 7, 18);

    public static IReadOnlyList<ThirdPartyLogisticsProviderDirectoryItem> Providers { get; } =
        Array.AsReadOnly(new[]
        {
            Provider(
                "americold",
                "Americold",
                "https://www.americold.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.NorthAmerica,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.ColdChain,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ImportExportSupport,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.FoodColdChain,
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution,
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics
                },
                OfficialServiceEvidence(
                    "Americold cold chain solutions",
                    "https://www.americold.com/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.ColdChain,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices),
                OfficialServiceEvidence(
                    "Americold import and export solutions",
                    "https://www.americold.com/import-export/",
                    ThirdPartyLogisticsProviderCapabilityCodes.ImportExportSupport)),
            Provider(
                "barrett-distribution-centers",
                "Barrett Distribution Centers",
                "https://www.barrettdistribution.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.TemperatureControlledStorage,
                    ThirdPartyLogisticsProviderCapabilityCodes.LotAndExpirationTracking,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.FoodColdChain,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialServiceEvidence(
                    "Barrett shared, dedicated and food-grade warehousing",
                    "https://www.barrettdistribution.com/shared-and-dedicated-warehousing",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.TemperatureControlledStorage,
                    ThirdPartyLogisticsProviderCapabilityCodes.LotAndExpirationTracking,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution)),
            Provider(
                "cj-logistics-america",
                "CJ Logistics America",
                "https://america.cjlogistics.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightForwarding,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution
                },
                OfficialLocationEvidence(
                    "CJ Logistics United States network",
                    "https://america.cjlogistics.com/about/network/united-states/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightForwarding,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices)),
            Provider(
                "dhl-express-united-states",
                "DHL Express United States",
                "https://www.dhl.com/us-en/home/express.html",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.InBondTransportation,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.ImportExportSupport
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer
                },
                OfficialServiceEvidence(
                    "DHL Express United States customs services",
                    "https://www.dhl.com/us-en/home/express/products-and-solutions/products-and-services-overview/customs-services.html",
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.InBondTransportation,
                    ThirdPartyLogisticsProviderCapabilityCodes.ImportExportSupport),
                OfficialServiceEvidence(
                    "DHL Express United States",
                    "https://www.dhl.com/us-en/home/express.html",
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery)),
            Provider(
                "dhl-supply-chain",
                "DHL Supply Chain",
                "https://www.dhl.com/us-en/home/supply-chain.html",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel
                },
                OfficialServiceEvidence(
                    "DHL Supply Chain United States",
                    "https://www.dhl.com/us-en/home/supply-chain.html",
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices),
                OfficialServiceEvidence(
                    "DHL warehousing solutions",
                    "https://www.dhl.com/us-en/home/supply-chain/solutions/warehousing.html?locale=true",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution)),
            Provider(
                "dsv",
                "DSV",
                "https://www.dsv.com/en-us/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialServiceEvidence(
                    "DSV United States contract logistics",
                    "https://www.dsv.com/en-us/our-solutions/contract-logistics",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices)),
            Provider(
                "efulfillment-service",
                "eFulfillment Service",
                "https://www.efulfillmentservice.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.CampaignFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.CampaignAndCrowdfunding,
                    ThirdPartyLogisticsProviderSegmentCodes.SmallBusinessFlexibleFulfillment,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialServiceEvidence(
                    "eFulfillment Service ecommerce fulfillment",
                    "https://www.efulfillmentservice.com/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly),
                OfficialServiceEvidence(
                    "eFulfillment Service crowdfunding fulfillment",
                    "https://www.efulfillmentservice.com/kickstarter-fulfillment/",
                    ThirdPartyLogisticsProviderCapabilityCodes.CampaignFulfillment)),
            Provider(
                "fedex-supply-chain",
                "FedEx Supply Chain",
                "https://www.fedex.com/en-us/logistics/supply-chain.html",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel
                },
                OfficialServiceEvidence(
                    "FedEx supply chain services",
                    "https://www.fedex.com/en-us/logistics/supply-chain.html",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices)),
            Provider(
                "fulfillrite",
                "Fulfillrite",
                "https://www.fulfillrite.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.CampaignFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.CampaignAndCrowdfunding,
                    ThirdPartyLogisticsProviderSegmentCodes.SmallBusinessFlexibleFulfillment
                },
                OfficialServiceEvidence(
                    "Fulfillrite crowdfunding fulfillment",
                    "https://www.fulfillrite.com/services/crowdfunding-campaigns/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.CampaignFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly),
                OfficialServiceEvidence(
                    "Fulfillrite service and commercial FAQs",
                    "https://www.fulfillrite.com/faqs/",
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics)),
            Provider(
                "geodis",
                "GEODIS",
                "https://geodis.com/us-en",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialServiceEvidence(
                    "GEODIS United States warehousing and value-added logistics",
                    "https://geodis.com/us-en/warehousing-and-value-added-logistics",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices)),
            Provider(
                "gxo-logistics",
                "GXO Logistics",
                "https://gxo.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.NorthAmerica,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialServiceEvidence(
                    "GXO logistics services",
                    "https://gxo.com/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics),
                OfficialServiceEvidence(
                    "GXO Direct solutions",
                    "https://gxo.com/supply-chain-mgmt/gxo-direct/solutions/",
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices)),
            Provider(
                "nfi-industries",
                "NFI Industries",
                "https://www.nfiindustries.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.NorthAmerica
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.DedicatedTransportation,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage,
                    ThirdPartyLogisticsProviderCapabilityCodes.Transloading
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution
                },
                OfficialServiceEvidence(
                    "NFI supply chain solutions",
                    "https://www.nfiindustries.com/solutions/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.DedicatedTransportation,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightBrokerage),
                OfficialServiceEvidence(
                    "NFI port services",
                    "https://www.nfiindustries.com/solutions/port-services/",
                    ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage,
                    ThirdPartyLogisticsProviderCapabilityCodes.Transloading)),
            Provider(
                "odw-logistics",
                "ODW Logistics",
                "https://www.odwlogistics.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.NorthAmerica
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ColdChain,
                    ThirdPartyLogisticsProviderCapabilityCodes.TemperatureControlledStorage,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.FoodColdChain,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics
                },
                OfficialServiceEvidence(
                    "ODW food and beverage logistics",
                    "https://www.odwlogistics.com/industries/food-beverage",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ColdChain,
                    ThirdPartyLogisticsProviderCapabilityCodes.TemperatureControlledStorage,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution)),
            Provider(
                "penske-logistics",
                "Penske Logistics",
                "https://www.penskelogistics.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.NorthAmerica
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialServiceEvidence(
                    "Penske warehousing and distribution",
                    "https://www.penskelogistics.com/solutions/warehousing-and-distribution/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices),
                OfficialServiceEvidence(
                    "Penske transportation management solutions",
                    "https://www.penskelogistics.com/solutions/transportation-services/transportation-management-solutions/",
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement)),
            Provider(
                "phoenix-warehouse",
                "Phoenix Warehouse",
                "https://phoenix-warehouse.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsControlledWarehousing,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialLocationEvidence(
                    "Phoenix Warehouse Jersey City bonded 3PL",
                    "https://phoenix-warehouse.com/about-us/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsControlledWarehousing,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution)),
            Provider(
                "red-stag-fulfillment",
                "Red Stag Fulfillment",
                "https://redstagfulfillment.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.HeavyAndBulkyFulfillment,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel
                },
                OfficialServiceEvidence(
                    "Red Stag fulfillment services",
                    "https://redstagfulfillment.com/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution)),
            Provider(
                "ryder-supply-chain-solutions",
                "Ryder Supply Chain Solutions",
                "https://www.ryder.com/en-us/logistics/third-party-logistics",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.NorthAmerica
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.DedicatedTransportation,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing,
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution
                },
                OfficialServiceEvidence(
                    "Ryder supply chain solutions",
                    "https://www.ryder.com/en-us/logistics/third-party-logistics",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.DedicatedTransportation,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices)),
            Provider(
                "seko-logistics",
                "SEKO Logistics",
                "https://www.sekologistics.com/en/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightForwarding,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel
                },
                OfficialServiceEvidence(
                    "SEKO United States customs clearance",
                    "https://www.sekologistics.com/emea-en/services/freight-forwarding/customs-clearance/",
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightForwarding,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage),
                OfficialServiceEvidence(
                    "SEKO retail and ecommerce logistics",
                    "https://www.sekologistics.com/en/industries/retailers/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices)),
            Provider(
                "shipbob",
                "ShipBob",
                "https://www.shipbob.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.CampaignFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly,
                    ThirdPartyLogisticsProviderCapabilityCodes.TemperatureControlledStorage,
                    ThirdPartyLogisticsProviderCapabilityCodes.LotAndExpirationTracking,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing,
                    ThirdPartyLogisticsProviderSegmentCodes.CampaignAndCrowdfunding,
                    ThirdPartyLogisticsProviderSegmentCodes.FoodColdChain
                },
                OfficialLocationEvidence(
                    "ShipBob United States fulfillment services",
                    "https://www.shipbob.com/shipbob-locations/usa/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices),
                OfficialServiceEvidence(
                    "ShipBob food and beverage fulfillment",
                    "https://www.shipbob.com/categories/food-and-beverage/",
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly,
                    ThirdPartyLogisticsProviderCapabilityCodes.TemperatureControlledStorage,
                    ThirdPartyLogisticsProviderCapabilityCodes.LotAndExpirationTracking,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution),
                OfficialServiceEvidence(
                    "ShipBob one-time and crowdfunding order setup",
                    "https://www.shipbob.com/wp-content/uploads/2020/06/2.-Initial-Account-Setup-ShipBob-Onboarding-Guides.pdf",
                    ThirdPartyLogisticsProviderCapabilityCodes.CampaignFulfillment)),
            Provider(
                "shipmonk",
                "ShipMonk",
                "https://www.shipmonk.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.CampaignFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.CampaignAndCrowdfunding
                },
                OfficialServiceEvidence(
                    "ShipMonk crowdfunding fulfillment",
                    "https://www.shipmonk.com/fulfillment-solutions/crowd-funding-fulfillment",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.CampaignFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly)),
            Provider(
                "stg-logistics",
                "STG Logistics",
                "https://www.stgusa.com/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.NorthAmerica
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage,
                    ThirdPartyLogisticsProviderCapabilityCodes.Transloading,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsControlledWarehousing,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution,
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialLocationEvidence(
                    "STG CFS and transloading",
                    "https://www.stgusa.com/services/cfs-transload/",
                    ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage,
                    ThirdPartyLogisticsProviderCapabilityCodes.Transloading,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsControlledWarehousing),
                OfficialServiceEvidence(
                    "STG managed logistics solutions",
                    "https://www.stgusa.com/news-notices/stg-logistics-managed-logistics-solutions-built-for-todays-supply-chain/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices)),
            Provider(
                "ups-supply-chain-solutions",
                "UPS Supply Chain Solutions",
                "https://www.ups.com/us/en/supplychain/home",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates,
                    ThirdPartyLogisticsProviderCoverageCodes.Global
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightForwarding,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage,
                    ThirdPartyLogisticsProviderCapabilityCodes.ForeignTradeZoneOperations,
                    ThirdPartyLogisticsProviderCapabilityCodes.InBondTransportation,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.EnterpriseContractLogistics,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution
                },
                OfficialServiceEvidence(
                    "UPS warehousing and distribution services",
                    "https://www.ups.com/us/en/supplychain/logistics-solutions/distribution",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.OmnichannelFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.ReverseLogistics,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices),
                OfficialServiceEvidence(
                    "UPS logistics services and solutions",
                    "https://www.ups.com/us/en/supplychain/logistics-solutions",
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.FreightForwarding,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage),
                OfficialLocationEvidence(
                    "UPS Foreign Trade Zone solutions",
                    "https://www.ups.com/us/en/supplychain/logistics-solutions/customs-brokerage/foreign-trade-zones",
                    ThirdPartyLogisticsProviderCapabilityCodes.ForeignTradeZoneOperations,
                    ThirdPartyLogisticsProviderCapabilityCodes.InBondTransportation)),
            Provider(
                "world-distribution-services",
                "World Distribution Services",
                "https://www.worldds.net/",
                new[]
                {
                    ThirdPartyLogisticsProviderCoverageCodes.UnitedStates
                },
                new[]
                {
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage,
                    ThirdPartyLogisticsProviderCapabilityCodes.Transloading,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsControlledWarehousing,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution
                },
                new[]
                {
                    ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution,
                    ThirdPartyLogisticsProviderSegmentCodes.EcommerceDirectToConsumer,
                    ThirdPartyLogisticsProviderSegmentCodes.RetailOmnichannel,
                    ThirdPartyLogisticsProviderSegmentCodes.SharedMultiClientWarehousing
                },
                OfficialLocationEvidence(
                    "WDS Los Angeles bonded warehouse",
                    "https://www.worldds.net/los-angeles-california-warehouse/",
                    ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution,
                    ThirdPartyLogisticsProviderCapabilityCodes.EcommerceFulfillment,
                    ThirdPartyLogisticsProviderCapabilityCodes.InventoryManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.TransportationManagement,
                    ThirdPartyLogisticsProviderCapabilityCodes.LastMileDelivery,
                    ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage,
                    ThirdPartyLogisticsProviderCapabilityCodes.Transloading,
                    ThirdPartyLogisticsProviderCapabilityCodes.CustomsControlledWarehousing,
                    ThirdPartyLogisticsProviderCapabilityCodes.ValueAddedServices,
                    ThirdPartyLogisticsProviderCapabilityCodes.KittingAndAssembly,
                    ThirdPartyLogisticsProviderCapabilityCodes.RetailDistribution))
        });

    public static IReadOnlyList<ThirdPartyLogisticsRegulatoryVerificationResource>
        RegulatoryVerificationResources { get; } = Array.AsReadOnly(new[]
        {
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes
                    .FmcsaAuthorityAndInsurance,
                AuthorityName = "Federal Motor Carrier Safety Administration",
                Purpose =
                    "Verify motor carrier, broker, freight forwarder authority and financial responsibility separately from a 3PL service claim.",
                OfficialUrl =
                    "https://li-public.fmcsa.dot.gov/LIVIEW/pkg_html.prc_lisearch"
            },
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes
                    .FmcOceanTransportationIntermediary,
                AuthorityName = "Federal Maritime Commission",
                Purpose =
                    "Check the licensed and bonded ocean transportation intermediary list when ocean forwarding or NVOCC work is required.",
                OfficialUrl = "https://www.fmc.gov/licensing-and-certification/"
            },
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes.CbpBondedWarehouse,
                AuthorityName = "U.S. Customs and Border Protection",
                Purpose =
                    "Confirm bonded warehouse status for the exact facility instead of inferring it from the provider brand.",
                OfficialUrl =
                    "https://www.help.cbp.gov/s/article/Article1853?language=en_US"
            },
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes
                    .CbpPermittedCustomsBrokers,
                AuthorityName = "U.S. Customs and Border Protection",
                Purpose =
                    "Verify the customs broker permit for the port and legal entity that will file the entry.",
                OfficialUrl = "https://www.cbp.gov/about/contact/brokers-listing"
            },
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes
                    .CbpInBondTransportation,
                AuthorityName = "U.S. Customs and Border Protection",
                Purpose =
                    "Confirm importer liability, ACE filing and bonded-carrier requirements before moving uncleared cargo between facilities.",
                OfficialUrl =
                    "https://www.help.cbp.gov/s/article/Article-1150?language=en_US"
            },
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes.CbpCustomsBond,
                AuthorityName = "U.S. Customs and Border Protection",
                Purpose =
                    "Confirm whether a single-entry or continuous customs bond is required for the importer and transaction.",
                OfficialUrl =
                    "https://www.help.cbp.gov/s/article/Article1072?language=en_US"
            },
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes.CbpFirmsCode,
                AuthorityName = "U.S. Customs and Border Protection",
                Purpose =
                    "Reconfirm the exact facility FIRMS code and active ACE acceptance instead of relying on a marketing location name.",
                OfficialUrl =
                    "https://www.cbp.gov/sites/default/files/2024-08/Trade_Information%20Notice_ACE%20Validation%20of%20FIRMS%20Codes%20for%20In-bond%20Arrivals508_0.pdf"
            },
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes
                    .ForeignTradeZonesBoard,
                AuthorityName = "U.S. Foreign-Trade Zones Board",
                Purpose =
                    "Check FTZ designation and then separately confirm CBP activation for the exact site before treating it as operational.",
                OfficialUrl = "https://www.trade.gov/about-ftzs"
            },
            new ThirdPartyLogisticsRegulatoryVerificationResource
            {
                ResourceCode = ThirdPartyLogisticsRegulatoryResourceCodes.EpaSmartWayPartner,
                AuthorityName = "U.S. Environmental Protection Agency",
                Purpose =
                    "Look up current SmartWay participation as optional sustainability evidence, not as an endorsement.",
                OfficialUrl = "https://www.epa.gov/smartway/smartway-partner-list"
            }
        });

    private static ThirdPartyLogisticsProviderDirectoryItem Provider(
        string providerKey,
        string displayName,
        string officialWebsiteUrl,
        string[] coverageCodes,
        string[] capabilityCodes,
        string[] segmentCodes,
        params ThirdPartyLogisticsProviderEvidence[] evidence)
        => new()
        {
            MarketCode = OperatingMarketCodes.UnitedStates,
            ProviderKey = providerKey,
            DisplayName = displayName,
            OfficialWebsiteUrl = officialWebsiteUrl,
            CoverageCodes = Array.AsReadOnly(coverageCodes),
            CapabilityCodes = Array.AsReadOnly(capabilityCodes),
            SegmentCodes = Array.AsReadOnly(segmentCodes),
            Evidence = Array.AsReadOnly(evidence)
        };

    private static ThirdPartyLogisticsProviderEvidence OfficialServiceEvidence(
        string title,
        string url,
        params string[] supportedCapabilityCodes)
        => OfficialEvidence(
            ThirdPartyLogisticsProviderEvidenceTypeCodes.OfficialProviderServicePage,
            title,
            url,
            supportedCapabilityCodes);

    private static ThirdPartyLogisticsProviderEvidence OfficialLocationEvidence(
        string title,
        string url,
        params string[] supportedCapabilityCodes)
        => OfficialEvidence(
            ThirdPartyLogisticsProviderEvidenceTypeCodes.OfficialProviderLocationPage,
            title,
            url,
            supportedCapabilityCodes);

    private static ThirdPartyLogisticsProviderEvidence OfficialEvidence(
        string evidenceTypeCode,
        string title,
        string url,
        string[] supportedCapabilityCodes)
        => new()
        {
            EvidenceTypeCode = evidenceTypeCode,
            SourceTitle = title,
            SourceUrl = url,
            ReviewedOn = SnapshotReviewedOn,
            SupportedCapabilityCodes = Array.AsReadOnly(supportedCapabilityCodes)
        };
}
