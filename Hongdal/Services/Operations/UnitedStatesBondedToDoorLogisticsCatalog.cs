using Hongdal.Contracts.Common.Operations;
using BoundaryCodes = Hongdal.Contracts.Common.Operations.BondedToDoorDirectoryBoundaryCodes;
using FacilityRelationshipCodes = Hongdal.Contracts.Common.Operations.CustomsControlledFacilityOperatorRelationshipCodes;
using RoleCodes = Hongdal.Contracts.Common.Operations.BondedToDoorRoleRequirementCodes;
using StageCodes = Hongdal.Contracts.Common.Operations.BondedToDoorLogisticsStageCodes;
using StorageModelCodes = Hongdal.Contracts.Common.Operations.CustomsControlledStorageModelCodes;

namespace Hongdal.Services.Operations;

public static class UnitedStatesBondedToDoorLogisticsCatalog
{
    public const string CatalogVersion = "US-BONDED-TO-DOOR-2026-07-18";

    public static DateOnly SnapshotReviewedOn { get; } = new(2026, 7, 18);

    public static IReadOnlyList<string> UniversalRoleRequirementCodes { get; } =
        Array.AsReadOnly(new[]
        {
            RoleCodes.ImporterOfRecordRequired,
            RoleCodes.LicensedCustomsBrokerOrSelfFilerRequired,
            RoleCodes.CustomsBondRequiredWhenApplicable,
            RoleCodes.BondedCarrierRequiredBeforeCustomsRelease,
            RoleCodes.DutiesTaxesAndAdmissibilityResolvedBeforeDomesticFulfillment,
            RoleCodes.ExactFacilityAuthorizationAndFirmsCodeVerificationRequired,
            RoleCodes.ProductSpecificAgencyApprovalRequired,
            RoleCodes.ParticipantAddressConsentRequired,
            RoleCodes.ParcelCarrierAccountOrContractRequired
        });

    public static IReadOnlyList<BondedToDoorLogisticsProfile> Profiles { get; } =
        Array.AsReadOnly(new[]
        {
            Profile(
                "dhl-express-united-states",
                "https://www.dhl.com/us-en/home/express.html",
                false,
                new[]
                {
                    StageCodes.CustomsWithdrawalAndRelease,
                    StageCodes.InBondTransportation,
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.ParticipantAddressFinalMileDelivery
                },
                new[] { StorageModelCodes.ExternalControlledFacilityHandoff },
                new[]
                {
                    BoundaryCodes.ProviderControlledStorageNotRecorded,
                    BoundaryCodes.ExactFacilityClaimNotRecorded
                },
                Array.Empty<CustomsControlledFacilityClaim>(),
                Evidence(
                    "DHL Express United States customs services",
                    "https://www.dhl.com/us-en/home/express/products-and-solutions/products-and-services-overview/customs-services.html",
                    new[]
                    {
                        StageCodes.CustomsWithdrawalAndRelease,
                        StageCodes.InBondTransportation,
                        StageCodes.ReleasedDomesticTransfer
                    },
                    new[] { StorageModelCodes.ExternalControlledFacilityHandoff }),
                Evidence(
                    "DHL Express United States",
                    "https://www.dhl.com/us-en/home/express.html",
                    new[] { StageCodes.ParticipantAddressFinalMileDelivery })),
            Profile(
                "fedex-supply-chain",
                "https://www.fedex.com/en-us/logistics.html",
                true,
                new[]
                {
                    StageCodes.CustomsControlledStorage,
                    StageCodes.CustomsWithdrawalAndRelease,
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ParticipantAddressFinalMileDelivery,
                    StageCodes.ReturnsProcessing
                },
                new[] { StorageModelCodes.ForeignTradeZone },
                new[] { BoundaryCodes.ExactFacilityClaimNotRecorded },
                Array.Empty<CustomsControlledFacilityClaim>(),
                Evidence(
                    "FedEx Trade Solutions",
                    "https://www.fedex.com/en-us/logistics/trade-solutions.html",
                    new[]
                    {
                        StageCodes.CustomsControlledStorage,
                        StageCodes.CustomsWithdrawalAndRelease,
                        StageCodes.ReleasedDomesticTransfer
                    },
                    new[] { StorageModelCodes.ForeignTradeZone }),
                Evidence(
                    "FedEx Supply Chain services",
                    "https://www.fedex.com/en-us/logistics/supply-chain.html",
                    new[]
                    {
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery,
                        StageCodes.ReturnsProcessing
                    })),
            Profile(
                "geodis",
                "https://geodis.com/us-en/contact-us",
                true,
                new[]
                {
                    StageCodes.CustomsControlledStorage,
                    StageCodes.CustomsWithdrawalAndRelease,
                    StageCodes.InBondTransportation,
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ParticipantAddressFinalMileDelivery,
                    StageCodes.ReturnsProcessing
                },
                new[] { StorageModelCodes.ForeignTradeZone },
                new[]
                {
                    BoundaryCodes.ExactFacilityClaimNotRecorded,
                    BoundaryCodes.UnitedStatesBondedWarehouseNotOfferedByProvider
                },
                Array.Empty<CustomsControlledFacilityClaim>(),
                Evidence(
                    "GEODIS Foreign-Trade Zone solutions",
                    "https://geodis.com/us-en/warehousing-and-value-added-logistics/warehousing/foreign-trade-zone",
                    new[]
                    {
                        StageCodes.CustomsControlledStorage,
                        StageCodes.CustomsWithdrawalAndRelease,
                        StageCodes.InBondTransportation,
                        StageCodes.ReleasedDomesticTransfer
                    },
                    new[] { StorageModelCodes.ForeignTradeZone }),
                Evidence(
                    "GEODIS eFulfillment",
                    "https://geodis.com/us-en/warehousing-and-value-added-logistics/fulfillment-omnichannel-logistics/efulfillment",
                    new[]
                    {
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery,
                        StageCodes.ReturnsProcessing
                    }),
                Evidence(
                    "GEODIS United States bonded warehouse boundary",
                    "https://geodis.com/us-en/blog/bonded-warehouses-manage-us-tariffs-import-costs")),
            Profile(
                "nfi-industries",
                "https://www.nfiindustries.com/contact/",
                false,
                new[]
                {
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ReturnsProcessing
                },
                new[] { StorageModelCodes.ExternalControlledFacilityHandoff },
                new[]
                {
                    BoundaryCodes.ProviderControlledStorageNotRecorded,
                    BoundaryCodes.ExactFacilityClaimNotRecorded
                },
                Array.Empty<CustomsControlledFacilityClaim>(),
                Evidence(
                    "NFI port services",
                    "https://www.nfiindustries.com/solutions/port-services/",
                    new[] { StageCodes.ReleasedDomesticTransfer },
                    new[] { StorageModelCodes.ExternalControlledFacilityHandoff }),
                Evidence(
                    "NFI supply chain solutions",
                    "https://www.nfiindustries.com/solutions/",
                    new[]
                    {
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ReturnsProcessing
                    })),
            Profile(
                "phoenix-warehouse",
                "https://phoenix-warehouse.com/contact-us/",
                true,
                new[]
                {
                    StageCodes.CustomsControlledStorage,
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ParticipantAddressFinalMileDelivery,
                    StageCodes.ReturnsProcessing
                },
                new[] { StorageModelCodes.CustomsBondedWarehouse },
                Array.Empty<string>(),
                new[]
                {
                    Facility(
                        "phoenix-warehouse-jersey-city",
                        "Phoenix Warehouse Jersey City bonded operation",
                        "Jersey City",
                        "NJ",
                        StorageModelCodes.CustomsBondedWarehouse,
                        "https://phoenix-warehouse.com/about-us/")
                },
                Evidence(
                    "Phoenix Warehouse bonded warehousing and distribution",
                    "https://phoenix-warehouse.com/about-us/",
                    new[]
                    {
                        StageCodes.CustomsControlledStorage,
                        StageCodes.ReleasedDomesticTransfer,
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery,
                        StageCodes.ReturnsProcessing
                    },
                    new[] { StorageModelCodes.CustomsBondedWarehouse },
                    BondedToDoorEvidenceTypeCodes.OfficialProviderFacilityPage)),
            Profile(
                "ryder-supply-chain-solutions",
                "https://www.ryder.com/en-us/contact-us",
                false,
                new[]
                {
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ParticipantAddressFinalMileDelivery,
                    StageCodes.ReturnsProcessing
                },
                new[] { StorageModelCodes.ExternalControlledFacilityHandoff },
                new[]
                {
                    BoundaryCodes.ProviderControlledStorageNotRecorded,
                    BoundaryCodes.ExactFacilityClaimNotRecorded
                },
                Array.Empty<CustomsControlledFacilityClaim>(),
                Evidence(
                    "Ryder supply chain solutions",
                    "https://www.ryder.com/en-us/logistics/third-party-logistics",
                    new[]
                    {
                        StageCodes.ReleasedDomesticTransfer,
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery,
                        StageCodes.ReturnsProcessing
                    },
                    new[] { StorageModelCodes.ExternalControlledFacilityHandoff })),
            Profile(
                "seko-logistics",
                "https://www.sekologistics.com/en/contact/",
                true,
                new[]
                {
                    StageCodes.CustomsWithdrawalAndRelease,
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ParticipantAddressFinalMileDelivery,
                    StageCodes.ReturnsProcessing
                },
                new[] { StorageModelCodes.ExternalControlledFacilityHandoff },
                new[]
                {
                    BoundaryCodes.ProviderControlledStorageNotRecorded,
                    BoundaryCodes.ExactFacilityClaimNotRecorded
                },
                Array.Empty<CustomsControlledFacilityClaim>(),
                Evidence(
                    "SEKO United States customs clearance",
                    "https://www.sekologistics.com/emea-en/services/freight-forwarding/customs-clearance/",
                    new[] { StageCodes.CustomsWithdrawalAndRelease },
                    new[] { StorageModelCodes.ExternalControlledFacilityHandoff }),
                Evidence(
                    "SEKO retail and ecommerce logistics",
                    "https://www.sekologistics.com/en/industries/retailers/",
                    new[]
                    {
                        StageCodes.ReleasedDomesticTransfer,
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery,
                        StageCodes.ReturnsProcessing
                    })),
            Profile(
                "stg-logistics",
                "https://www.stgusa.com/contact-us/",
                true,
                new[]
                {
                    StageCodes.CustomsControlledStorage,
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ParticipantAddressFinalMileDelivery
                },
                new[] { StorageModelCodes.CustomsBondedWarehouse },
                Array.Empty<string>(),
                new[]
                {
                    Facility(
                        "stg-norfolk-agent-ldf9",
                        "Norfolk Bonded Warehouse serving STG Norfolk CFS",
                        "Virginia Beach",
                        "VA",
                        StorageModelCodes.CustomsBondedWarehouse,
                        "https://www.stgusa.com/alerts/notice-stg-norfolk-cfs-transition-effective-april-1-2026/",
                        FacilityRelationshipCodes.PartnerOrAgentOperated,
                        "LDF9")
                },
                Evidence(
                    "STG CFS and transloading",
                    "https://www.stgusa.com/services/cfs-transload/",
                    new[]
                    {
                        StageCodes.CustomsControlledStorage,
                        StageCodes.ReleasedDomesticTransfer
                    },
                    new[] { StorageModelCodes.CustomsBondedWarehouse }),
                Evidence(
                    "STG managed logistics solutions",
                    "https://www.stgusa.com/news-notices/stg-logistics-managed-logistics-solutions-built-for-todays-supply-chain/",
                    new[]
                    {
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery
                    }),
                Evidence(
                    "STG Norfolk CFS transition notice",
                    "https://www.stgusa.com/alerts/notice-stg-norfolk-cfs-transition-effective-april-1-2026/",
                    new[] { StageCodes.CustomsControlledStorage },
                    new[] { StorageModelCodes.CustomsBondedWarehouse },
                    BondedToDoorEvidenceTypeCodes.OfficialProviderOperationalNotice)),
            Profile(
                "ups-supply-chain-solutions",
                "https://www.ups.com/us/en/supplychain/home",
                true,
                new[]
                {
                    StageCodes.CustomsControlledStorage,
                    StageCodes.CustomsWithdrawalAndRelease,
                    StageCodes.InBondTransportation,
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ParticipantAddressFinalMileDelivery,
                    StageCodes.ReturnsProcessing
                },
                new[] { StorageModelCodes.ForeignTradeZone },
                Array.Empty<string>(),
                new[]
                {
                    Facility(
                        "ups-gateway-ftz-dallas",
                        "UPS Gateway FTZ Dallas",
                        "Dallas",
                        "TX",
                        StorageModelCodes.ForeignTradeZone,
                        "https://www.ups.com/us/en/supplychain/logistics-solutions/customs-brokerage/foreign-trade-zones"),
                    Facility(
                        "ups-gateway-ftz-los-angeles",
                        "UPS Gateway FTZ Los Angeles",
                        "Los Angeles",
                        "CA",
                        StorageModelCodes.ForeignTradeZone,
                        "https://www.ups.com/us/en/supplychain/logistics-solutions/customs-brokerage/foreign-trade-zones"),
                    Facility(
                        "ups-gateway-ftz-new-york-jfk",
                        "UPS Gateway FTZ New York JFK",
                        "New York",
                        "NY",
                        StorageModelCodes.ForeignTradeZone,
                        "https://www.ups.com/us/en/supplychain/logistics-solutions/customs-brokerage/foreign-trade-zones"),
                    Facility(
                        "ups-gateway-ftz-chicago",
                        "UPS Gateway FTZ Chicago",
                        "Chicago",
                        "IL",
                        StorageModelCodes.ForeignTradeZone,
                        "https://www.ups.com/us/en/supplychain/logistics-solutions/customs-brokerage/foreign-trade-zones")
                },
                Evidence(
                    "UPS Foreign Trade Zone solutions",
                    "https://www.ups.com/us/en/supplychain/logistics-solutions/customs-brokerage/foreign-trade-zones",
                    new[]
                    {
                        StageCodes.CustomsControlledStorage,
                        StageCodes.CustomsWithdrawalAndRelease,
                        StageCodes.InBondTransportation
                    },
                    new[] { StorageModelCodes.ForeignTradeZone },
                    BondedToDoorEvidenceTypeCodes.OfficialProviderFacilityPage),
                Evidence(
                    "UPS warehousing and distribution services",
                    "https://www.ups.com/us/en/supplychain/logistics-solutions/distribution",
                    new[]
                    {
                        StageCodes.ReleasedDomesticTransfer,
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery,
                        StageCodes.ReturnsProcessing
                    })),
            Profile(
                "world-distribution-services",
                "https://www.worldds.net/contact-us/",
                true,
                new[]
                {
                    StageCodes.CustomsControlledStorage,
                    StageCodes.ReleasedDomesticTransfer,
                    StageCodes.FulfillmentWarehouseInbound,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantOrderPickPackAndParcelTender,
                    StageCodes.ParticipantAddressFinalMileDelivery
                },
                new[] { StorageModelCodes.CustomsBondedWarehouse },
                Array.Empty<string>(),
                new[]
                {
                    Facility(
                        "wds-los-angeles",
                        "WDS Los Angeles warehouse",
                        "Compton",
                        "CA",
                        StorageModelCodes.CustomsBondedWarehouse,
                        "https://www.worldds.net/los-angeles-california-warehouse/"),
                    Facility(
                        "wds-columbus",
                        "WDS Columbus warehouse",
                        "Columbus",
                        "OH",
                        StorageModelCodes.CustomsBondedWarehouse,
                        "https://www.worldds.net/columbus-ohio-warehouse/319/"),
                    Facility(
                        "wds-norfolk",
                        "WDS Norfolk warehouse",
                        "Virginia Beach",
                        "VA",
                        StorageModelCodes.CustomsBondedWarehouse,
                        "https://content.worldds.net/hubfs/WorldDS-Assets/Sell-Sheets-and-Brochures/WDS-NOR-Spec-Sheet_Final.pdf")
                },
                Evidence(
                    "WDS Los Angeles bonded warehouse",
                    "https://www.worldds.net/los-angeles-california-warehouse/",
                    new[]
                    {
                        StageCodes.CustomsControlledStorage,
                        StageCodes.ReleasedDomesticTransfer,
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery
                    },
                    new[] { StorageModelCodes.CustomsBondedWarehouse },
                    BondedToDoorEvidenceTypeCodes.OfficialProviderFacilityPage),
                Evidence(
                    "WDS Columbus bonded CFS warehouse",
                    "https://www.worldds.net/columbus-ohio-warehouse/319/",
                    new[]
                    {
                        StageCodes.CustomsControlledStorage,
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantOrderPickPackAndParcelTender
                    },
                    new[] { StorageModelCodes.CustomsBondedWarehouse },
                    BondedToDoorEvidenceTypeCodes.OfficialProviderFacilityPage),
                Evidence(
                    "WDS Norfolk facility specification",
                    "https://content.worldds.net/hubfs/WorldDS-Assets/Sell-Sheets-and-Brochures/WDS-NOR-Spec-Sheet_Final.pdf",
                    new[]
                    {
                        StageCodes.CustomsControlledStorage,
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.ParticipantOrderPickPackAndParcelTender
                    },
                    new[] { StorageModelCodes.CustomsBondedWarehouse },
                    BondedToDoorEvidenceTypeCodes.OfficialProviderFacilityPage),
                Evidence(
                    "WDS retail fulfillment and direct-to-consumer distribution",
                    "https://content.worldds.net/meet-wds-old",
                    new[]
                    {
                        StageCodes.FulfillmentWarehouseInbound,
                        StageCodes.ParticipantOrderPickPackAndParcelTender,
                        StageCodes.ParticipantAddressFinalMileDelivery
                    }))
        });

    private static BondedToDoorLogisticsProfile Profile(
        string providerKey,
        string officialInquiryUrl,
        bool advertisesIntegratedFlow,
        string[] stageCodes,
        string[] storageModelCodes,
        string[] additionalBoundaryCodes,
        CustomsControlledFacilityClaim[] facilityClaims,
        params BondedToDoorLogisticsEvidence[] evidence)
    {
        var boundaryCodes = new[]
            {
                BoundaryCodes.EndToEndSingleContractNotVerified,
                BoundaryCodes.ExactFacilityAuthorizationNotIndependentlyVerified,
                BoundaryCodes.CustomsBrokerPermitNotIndependentlyVerified,
                BoundaryCodes.BondedCarrierAuthorityNotIndependentlyVerified,
                BoundaryCodes.ProductAndFinalMileEligibilityRequiresQuote
            }
            .Concat(additionalBoundaryCodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new BondedToDoorLogisticsProfile
        {
            ProviderKey = providerKey,
            OfficialInquiryUrl = officialInquiryUrl,
            AdvertisesIntegratedFlow = advertisesIntegratedFlow,
            StageCodes = Array.AsReadOnly(stageCodes),
            StorageModelCodes = Array.AsReadOnly(storageModelCodes),
            RequiredRoleCodes = UniversalRoleRequirementCodes,
            DirectoryBoundaryCodes = Array.AsReadOnly(boundaryCodes),
            FacilityClaims = Array.AsReadOnly(facilityClaims),
            Evidence = Array.AsReadOnly(evidence)
        };
    }

    private static CustomsControlledFacilityClaim Facility(
        string facilityKey,
        string displayName,
        string city,
        string stateCode,
        string storageModelCode,
        string sourceUrl,
        string operatorRelationshipCode = FacilityRelationshipCodes.ProviderOperated,
        string? firmsCode = null)
        => new()
        {
            FacilityKey = facilityKey,
            DisplayName = displayName,
            City = city,
            StateCode = stateCode,
            StorageModelCode = storageModelCode,
            OperatorRelationshipCode = operatorRelationshipCode,
            FirmsCode = firmsCode,
            ClaimSourceUrl = sourceUrl,
            ReviewedOn = SnapshotReviewedOn
        };

    private static BondedToDoorLogisticsEvidence Evidence(
        string title,
        string url,
        string[]? stages = null,
        string[]? storageModels = null,
        string evidenceTypeCode = BondedToDoorEvidenceTypeCodes
            .OfficialProviderServicePage)
        => new()
        {
            EvidenceTypeCode = evidenceTypeCode,
            SourceTitle = title,
            SourceUrl = url,
            ReviewedOn = SnapshotReviewedOn,
            SupportedStageCodes = Array.AsReadOnly(stages ?? Array.Empty<string>()),
            SupportedStorageModelCodes =
                Array.AsReadOnly(storageModels ?? Array.Empty<string>())
        };
}
