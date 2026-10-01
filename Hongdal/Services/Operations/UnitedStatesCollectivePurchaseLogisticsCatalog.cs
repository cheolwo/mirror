using Hongdal.Contracts.Common.Operations;
using CommercialConditionCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseCommercialConditionCodes;
using CommercialConditionScopeCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseCommercialConditionScopeCodes;
using CommercialConditionValueCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseCommercialConditionValueCodes;
using EngagementSignalCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseEngagementSignalCodes;
using ProductHandlingCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseProductHandlingCodes;
using QuoteInputCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseLogisticsQuoteInputCodes;
using ResponsibilityCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseLogisticsResponsibilityCodes;
using RestrictionCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseLogisticsRestrictionCodes;
using StageCodes = Hongdal.Contracts.Common.Operations.CollectivePurchaseLogisticsStageCodes;

namespace Hongdal.Services.Operations;

public static class UnitedStatesCollectivePurchaseLogisticsCatalog
{
    public const string CatalogVersion =
        "US-COLLECTIVE-PURCHASE-3PL-2026-07-18";

    public static DateOnly SnapshotReviewedOn { get; } = new(2026, 7, 18);

    public static IReadOnlyList<string> RequiredQuoteInputCodes { get; } =
        Array.AsReadOnly(new[]
        {
            QuoteInputCodes.ProductCategoryAndRegulatoryClass,
            QuoteInputCodes.CountryOfOriginAndSupplier,
            QuoteInputCodes.ImportPortOrInboundOrigin,
            QuoteInputCodes.UnitsCasesPalletsAndSkuCount,
            QuoteInputCodes.WeightAndDimensions,
            QuoteInputCodes.TemperatureLotAndExpiration,
            QuoteInputCodes.ParticipantDestinationDistribution,
            QuoteInputCodes.OneTimeOrRecurringSchedule,
            QuoteInputCodes.KittingPackagingAndLabeling,
            QuoteInputCodes.ReturnsAndUndeliverablePolicy,
            QuoteInputCodes.ImporterAndCustomsResponsibility
        });

    public static IReadOnlyList<CollectivePurchaseLogisticsProfile> Profiles { get; } =
        Array.AsReadOnly(new[]
        {
            Profile(
                "americold",
                "https://www.americold.com/",
                new[]
                {
                    StageCodes.InternationalInboundCoordination,
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.RegionalHubOrRetailDistribution
                },
                new[]
                {
                    ProductHandlingCodes.RefrigeratedFoodByFacilityReview,
                    ProductHandlingCodes.FrozenFoodByFacilityReview
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                true,
                Array.Empty<CollectivePurchasePublishedCommercialCondition>(),
                Evidence(
                    "Americold cold chain solutions",
                    "https://www.americold.com/",
                    stages: new[]
                    {
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.RegionalHubOrRetailDistribution
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.RefrigeratedFoodByFacilityReview,
                        ProductHandlingCodes.FrozenFoodByFacilityReview
                    }),
                Evidence(
                    "Americold import and export solutions",
                    "https://www.americold.com/import-export/",
                    stages: new[]
                    {
                        StageCodes.InternationalInboundCoordination
                    })),
            Profile(
                "barrett-distribution-centers",
                "https://www.barrettdistribution.com/",
                new[]
                {
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.RegionalHubOrRetailDistribution
                },
                new[]
                {
                    ProductHandlingCodes.ShelfStablePackagedFoodByReview,
                    ProductHandlingCodes.RefrigeratedFoodByFacilityReview,
                    ProductHandlingCodes.LotOrExpirationTrackedGoods
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                true,
                Array.Empty<CollectivePurchasePublishedCommercialCondition>(),
                Evidence(
                    "Barrett shared, dedicated and food-grade warehousing",
                    "https://www.barrettdistribution.com/shared-and-dedicated-warehousing",
                    stages: new[]
                    {
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.RegionalHubOrRetailDistribution
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.ShelfStablePackagedFoodByReview,
                        ProductHandlingCodes.RefrigeratedFoodByFacilityReview,
                        ProductHandlingCodes.LotOrExpirationTrackedGoods
                    })),
            Profile(
                "cj-logistics-america",
                "https://america.cjlogistics.com/",
                new[]
                {
                    StageCodes.InternationalInboundCoordination,
                    StageCodes.CustomsBrokerageCoordination,
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                Array.Empty<CollectivePurchasePublishedCommercialCondition>(),
                Evidence(
                    "CJ Logistics United States network",
                    "https://america.cjlogistics.com/about/network/united-states/",
                    stages: new[]
                    {
                        StageCodes.InternationalInboundCoordination,
                        StageCodes.CustomsBrokerageCoordination,
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise
                    })),
            Profile(
                "efulfillment-service",
                "https://www.efulfillmentservice.com/kickstarter-fulfillment/",
                new[]
                {
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution,
                    StageCodes.ReturnsProcessing
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise,
                    ProductHandlingCodes.SmallLightParcel,
                    ProductHandlingCodes.ShelfStablePackagedFoodByReview
                },
                new[]
                {
                    EngagementSignalCodes.CampaignFulfillmentAdvertised,
                    EngagementSignalCodes.StartupOrEmergingBrandAdvertised,
                    EngagementSignalCodes.NoOngoingOrderMinimumAdvertised,
                    EngagementSignalCodes.NoLongTermContractAdvertised,
                    EngagementSignalCodes.CampaignBackerOrderGuidelinePublished,
                    EngagementSignalCodes.CustomQuoteRequired
                },
                new[]
                {
                    RestrictionCodes.FrozenFoodNotAccepted,
                    RestrictionCodes.HeavyOrOversizedGoodsGenerallyNotAccepted
                },
                true,
                new[]
                {
                    CommercialCondition(
                        CommercialConditionCodes.OrderMinimum,
                        "https://www.efulfillmentservice.com/",
                        scopeCode: CommercialConditionScopeCodes.OngoingFulfillment,
                        valueCode: CommercialConditionValueCodes.NoneAdvertised),
                    CommercialCondition(
                        CommercialConditionCodes.CampaignBackerOrderGuideline,
                        "https://www.efulfillmentservice.com/kickstarter-fulfillment/",
                        scopeCode: CommercialConditionScopeCodes.CampaignFulfillment,
                        approximateOrderCount: 200),
                    CommercialCondition(
                        CommercialConditionCodes.LongTermContract,
                        "https://www.efulfillmentservice.com/",
                        scopeCode: CommercialConditionScopeCodes.Account,
                        valueCode: CommercialConditionValueCodes.NoneAdvertised),
                    CommercialCondition(
                        CommercialConditionCodes.SetupFee,
                        "https://www.efulfillmentservice.com/",
                        scopeCode: CommercialConditionScopeCodes.Account,
                        valueCode: CommercialConditionValueCodes.NoneAdvertised)
                },
                Evidence(
                    "eFulfillment Service ecommerce fulfillment",
                    "https://www.efulfillmentservice.com/",
                    stages: new[]
                    {
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution,
                        StageCodes.ReturnsProcessing
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise,
                        ProductHandlingCodes.SmallLightParcel,
                        ProductHandlingCodes.ShelfStablePackagedFoodByReview
                    },
                    signals: new[]
                    {
                        EngagementSignalCodes.StartupOrEmergingBrandAdvertised,
                        EngagementSignalCodes.NoOngoingOrderMinimumAdvertised,
                        EngagementSignalCodes.NoLongTermContractAdvertised,
                        EngagementSignalCodes.CustomQuoteRequired
                    }),
                Evidence(
                    "eFulfillment Service crowdfunding fulfillment",
                    "https://www.efulfillmentservice.com/kickstarter-fulfillment/",
                    signals: new[]
                    {
                        EngagementSignalCodes.CampaignFulfillmentAdvertised,
                        EngagementSignalCodes.CampaignBackerOrderGuidelinePublished
                    }),
                Evidence(
                    "eFulfillment Service product restrictions",
                    "https://www.efulfillmentservice.com/faq/restrictions/",
                    restrictions: new[]
                    {
                        RestrictionCodes.FrozenFoodNotAccepted,
                        RestrictionCodes.HeavyOrOversizedGoodsGenerallyNotAccepted
                    })),
            Profile(
                "fulfillrite",
                "https://www.fulfillrite.com/services/crowdfunding-campaigns/",
                new[]
                {
                    StageCodes.InternationalInboundCoordination,
                    StageCodes.CustomsBrokerageCoordination,
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.ReturnsProcessing
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise,
                    ProductHandlingCodes.SmallLightParcel,
                    ProductHandlingCodes.ShelfStablePackagedFoodByReview
                },
                new[]
                {
                    EngagementSignalCodes.CampaignFulfillmentAdvertised,
                    EngagementSignalCodes.StartupOrEmergingBrandAdvertised,
                    EngagementSignalCodes.NoLongTermContractAdvertised,
                    EngagementSignalCodes.PublishedMonthlyMinimum,
                    EngagementSignalCodes.CustomQuoteRequired
                },
                new[]
                {
                    RestrictionCodes.PerishableFoodNotAccepted,
                    RestrictionCodes.FrozenFoodNotAccepted,
                    RestrictionCodes.ClimateControlledGoodsNotAccepted,
                    RestrictionCodes.HeavyOrOversizedGoodsGenerallyNotAccepted,
                    RestrictionCodes.WholesalePalletDistributionNotOffered
                },
                true,
                new[]
                {
                    CommercialCondition(
                        CommercialConditionCodes.MonthlyPickPackMinimum,
                        "https://www.fulfillrite.com/faqs/",
                        scopeCode: CommercialConditionScopeCodes.OngoingFulfillment,
                        amount: 399m,
                        currencyCode: "USD"),
                    CommercialCondition(
                        CommercialConditionCodes.ApproximateOrdersAtMonthlyMinimum,
                        "https://www.fulfillrite.com/faqs/",
                        scopeCode: CommercialConditionScopeCodes.OngoingFulfillment,
                        approximateOrderCount: 140),
                    CommercialCondition(
                        CommercialConditionCodes.MonthlyAccountFee,
                        "https://www.fulfillrite.com/faqs/",
                        scopeCode: CommercialConditionScopeCodes.Account,
                        amount: 59.99m,
                        currencyCode: "USD"),
                    CommercialCondition(
                        CommercialConditionCodes.LongTermContract,
                        "https://www.fulfillrite.com/faqs/",
                        scopeCode: CommercialConditionScopeCodes.Account,
                        valueCode:
                            CommercialConditionValueCodes.MonthToMonthWithThirtyDayMinimum),
                    CommercialCondition(
                        CommercialConditionCodes.SetupFee,
                        "https://www.fulfillrite.com/faqs/",
                        scopeCode: CommercialConditionScopeCodes.Account,
                        valueCode: CommercialConditionValueCodes.NoneAdvertised)
                },
                Evidence(
                    "Fulfillrite crowdfunding fulfillment",
                    "https://www.fulfillrite.com/services/crowdfunding-campaigns/",
                    signals: new[]
                    {
                        EngagementSignalCodes.CampaignFulfillmentAdvertised,
                        EngagementSignalCodes.NoLongTermContractAdvertised,
                        EngagementSignalCodes.CustomQuoteRequired
                    }),
                Evidence(
                    "Fulfillrite service and commercial FAQs",
                    "https://www.fulfillrite.com/faqs/",
                    stages: new[]
                    {
                        StageCodes.InternationalInboundCoordination,
                        StageCodes.CustomsBrokerageCoordination,
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.ReturnsProcessing
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise,
                        ProductHandlingCodes.SmallLightParcel,
                        ProductHandlingCodes.ShelfStablePackagedFoodByReview
                    },
                    signals: new[]
                    {
                        EngagementSignalCodes.StartupOrEmergingBrandAdvertised,
                        EngagementSignalCodes.PublishedMonthlyMinimum
                    },
                    restrictions: new[]
                    {
                        RestrictionCodes.PerishableFoodNotAccepted,
                        RestrictionCodes.FrozenFoodNotAccepted,
                        RestrictionCodes.ClimateControlledGoodsNotAccepted,
                        RestrictionCodes.HeavyOrOversizedGoodsGenerallyNotAccepted,
                        RestrictionCodes.WholesalePalletDistributionNotOffered
                    })),
            Profile(
                "nfi-industries",
                "https://www.nfiindustries.com/solutions/",
                new[]
                {
                    StageCodes.InternationalInboundCoordination,
                    StageCodes.PortDrayageAndTransload,
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution,
                    StageCodes.ReturnsProcessing
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                Array.Empty<CollectivePurchasePublishedCommercialCondition>(),
                Evidence(
                    "NFI supply chain solutions",
                    "https://www.nfiindustries.com/solutions/",
                    stages: new[]
                    {
                        StageCodes.InternationalInboundCoordination,
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution,
                        StageCodes.ReturnsProcessing
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise
                    }),
                Evidence(
                    "NFI port services",
                    "https://www.nfiindustries.com/solutions/port-services/",
                    stages: new[]
                    {
                        StageCodes.PortDrayageAndTransload
                    })),
            Profile(
                "odw-logistics",
                "https://www.odwlogistics.com/industries/food-beverage",
                new[]
                {
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution
                },
                new[]
                {
                    ProductHandlingCodes.ShelfStablePackagedFoodByReview,
                    ProductHandlingCodes.RefrigeratedFoodByFacilityReview,
                    ProductHandlingCodes.FrozenFoodByFacilityReview
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                true,
                Array.Empty<CollectivePurchasePublishedCommercialCondition>(),
                Evidence(
                    "ODW food and beverage logistics",
                    "https://www.odwlogistics.com/industries/food-beverage",
                    stages: new[]
                    {
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.ShelfStablePackagedFoodByReview,
                        ProductHandlingCodes.RefrigeratedFoodByFacilityReview,
                        ProductHandlingCodes.FrozenFoodByFacilityReview
                    })),
            Profile(
                "red-stag-fulfillment",
                "https://redstagfulfillment.com/",
                new[]
                {
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution,
                    StageCodes.ReturnsProcessing
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise,
                    ProductHandlingCodes.HeavyOrBulkyGoods
                },
                new[]
                {
                    EngagementSignalCodes.NoLongTermContractAdvertised,
                    EngagementSignalCodes.CustomQuoteRequired,
                    EngagementSignalCodes.EnterpriseConsultation
                },
                Array.Empty<string>(),
                false,
                new[]
                {
                    CommercialCondition(
                        CommercialConditionCodes.LongTermContract,
                        "https://redstagfulfillment.com/",
                        scopeCode: CommercialConditionScopeCodes.Account,
                        valueCode:
                            CommercialConditionValueCodes.MonthToMonthOrLongTermChoice)
                },
                Evidence(
                    "Red Stag fulfillment services",
                    "https://redstagfulfillment.com/",
                    stages: new[]
                    {
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution,
                        StageCodes.ReturnsProcessing
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise,
                        ProductHandlingCodes.HeavyOrBulkyGoods
                    },
                    signals: new[]
                    {
                        EngagementSignalCodes.NoLongTermContractAdvertised,
                        EngagementSignalCodes.CustomQuoteRequired,
                        EngagementSignalCodes.EnterpriseConsultation
                    })),
            Profile(
                "ryder-supply-chain-solutions",
                "https://www.ryder.com/en-us/e-commerce/e-commerce-fulfillment",
                new[]
                {
                    StageCodes.PortDrayageAndTransload,
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution,
                    StageCodes.ReturnsProcessing
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise
                },
                new[]
                {
                    EngagementSignalCodes.StartupOrEmergingBrandAdvertised,
                    EngagementSignalCodes.CustomQuoteRequired,
                    EngagementSignalCodes.EnterpriseConsultation
                },
                Array.Empty<string>(),
                false,
                Array.Empty<CollectivePurchasePublishedCommercialCondition>(),
                Evidence(
                    "Ryder ecommerce and Port2Door fulfillment",
                    "https://www.ryder.com/en-us/e-commerce/e-commerce-fulfillment",
                    stages: new[]
                    {
                        StageCodes.PortDrayageAndTransload,
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution,
                        StageCodes.ReturnsProcessing
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise
                    },
                    signals: new[]
                    {
                        EngagementSignalCodes.StartupOrEmergingBrandAdvertised,
                        EngagementSignalCodes.CustomQuoteRequired,
                        EngagementSignalCodes.EnterpriseConsultation
                    })),
            Profile(
                "shipbob",
                "https://www.shipbob.com/pricing/?locale=en-US",
                new[]
                {
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution,
                    StageCodes.ReturnsProcessing
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise,
                    ProductHandlingCodes.SmallLightParcel,
                    ProductHandlingCodes.ShelfStablePackagedFoodByReview,
                    ProductHandlingCodes.LotOrExpirationTrackedGoods
                },
                new[]
                {
                    EngagementSignalCodes.CampaignFulfillmentAdvertised,
                    EngagementSignalCodes.CustomQuoteRequired
                },
                Array.Empty<string>(),
                true,
                Array.Empty<CollectivePurchasePublishedCommercialCondition>(),
                Evidence(
                    "ShipBob food and beverage fulfillment",
                    "https://www.shipbob.com/categories/food-and-beverage/",
                    stages: new[]
                    {
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution,
                        StageCodes.ReturnsProcessing
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise,
                        ProductHandlingCodes.SmallLightParcel,
                        ProductHandlingCodes.ShelfStablePackagedFoodByReview,
                        ProductHandlingCodes.LotOrExpirationTrackedGoods
                    },
                    signals: new[]
                    {
                        EngagementSignalCodes.CustomQuoteRequired
                    }),
                Evidence(
                    "ShipBob one-time and crowdfunding order setup",
                    "https://www.shipbob.com/wp-content/uploads/2020/06/2.-Initial-Account-Setup-ShipBob-Onboarding-Guides.pdf",
                    signals: new[]
                    {
                        EngagementSignalCodes.CampaignFulfillmentAdvertised
                    })),
            Profile(
                "shipmonk",
                "https://www.shipmonk.com/fulfillment-solutions/crowd-funding-fulfillment",
                new[]
                {
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution,
                    StageCodes.ReturnsProcessing
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise
                },
                new[]
                {
                    EngagementSignalCodes.CampaignFulfillmentAdvertised,
                    EngagementSignalCodes.MonthlyMinimumFormulaApplies,
                    EngagementSignalCodes.CustomQuoteRequired
                },
                Array.Empty<string>(),
                false,
                new[]
                {
                    CommercialCondition(
                        CommercialConditionCodes.MonthlyMinimumFormula,
                        "https://www.shipmonk.com/pricing",
                        scopeCode: CommercialConditionScopeCodes.OngoingFulfillment,
                        valueCode:
                            CommercialConditionValueCodes.ProjectedOrdersBasedFormula)
                },
                Evidence(
                    "ShipMonk crowdfunding fulfillment",
                    "https://www.shipmonk.com/fulfillment-solutions/crowd-funding-fulfillment",
                    stages: new[]
                    {
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution,
                        StageCodes.ReturnsProcessing
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise
                    },
                    signals: new[]
                    {
                        EngagementSignalCodes.CampaignFulfillmentAdvertised,
                        EngagementSignalCodes.CustomQuoteRequired
                    }),
                Evidence(
                    "ShipMonk pricing and monthly minimum formula",
                    "https://www.shipmonk.com/pricing",
                    signals: new[]
                    {
                        EngagementSignalCodes.MonthlyMinimumFormulaApplies
                    })),
            Profile(
                "ups-supply-chain-solutions",
                "https://www.ups.com/us/en/ups-supplychain",
                new[]
                {
                    StageCodes.InternationalInboundCoordination,
                    StageCodes.CustomsBrokerageCoordination,
                    StageCodes.BulkInboundReceiving,
                    StageCodes.SharedInventoryStorage,
                    StageCodes.BreakPackKittingAndRelabeling,
                    StageCodes.ParticipantParcelFulfillment,
                    StageCodes.RegionalHubOrRetailDistribution,
                    StageCodes.ReturnsProcessing
                },
                new[]
                {
                    ProductHandlingCodes.GeneralMerchandise
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                Array.Empty<CollectivePurchasePublishedCommercialCondition>(),
                Evidence(
                    "UPS supply chain services",
                    "https://www.ups.com/us/en/ups-supplychain",
                    stages: new[]
                    {
                        StageCodes.InternationalInboundCoordination,
                        StageCodes.CustomsBrokerageCoordination
                    },
                    products: new[]
                    {
                        ProductHandlingCodes.GeneralMerchandise
                    }),
                Evidence(
                    "UPS warehousing and distribution services",
                    "https://www.ups.com/us/en/supplychain/logistics-solutions/distribution",
                    stages: new[]
                    {
                        StageCodes.BulkInboundReceiving,
                        StageCodes.SharedInventoryStorage,
                        StageCodes.BreakPackKittingAndRelabeling,
                        StageCodes.ParticipantParcelFulfillment,
                        StageCodes.RegionalHubOrRetailDistribution,
                        StageCodes.ReturnsProcessing
                    }))
        });

    private static CollectivePurchaseLogisticsProfile Profile(
        string providerKey,
        string officialInquiryUrl,
        string[] stageCodes,
        string[] productHandlingCodes,
        string[] engagementSignalCodes,
        string[] explicitRestrictionCodes,
        bool requiresFoodFacilityValidation,
        CollectivePurchasePublishedCommercialCondition[] publishedCommercialConditions,
        params CollectivePurchaseLogisticsEvidence[] evidence)
        => new()
        {
            ProviderKey = providerKey,
            OfficialInquiryUrl = officialInquiryUrl,
            StageCodes = Array.AsReadOnly(stageCodes),
            ProductHandlingCodes = Array.AsReadOnly(productHandlingCodes),
            EngagementSignalCodes = Array.AsReadOnly(engagementSignalCodes),
            ExplicitRestrictionCodes = Array.AsReadOnly(explicitRestrictionCodes),
            ExternalResponsibilityCodes = Responsibilities(requiresFoodFacilityValidation),
            PublishedCommercialConditions =
                Array.AsReadOnly(publishedCommercialConditions),
            Evidence = Array.AsReadOnly(evidence)
        };

    private static CollectivePurchaseLogisticsEvidence Evidence(
        string title,
        string url,
        string[]? stages = null,
        string[]? products = null,
        string[]? signals = null,
        string[]? restrictions = null)
        => new()
        {
            SourceTitle = title,
            SourceUrl = url,
            ReviewedOn = SnapshotReviewedOn,
            SupportedStageCodes = Array.AsReadOnly(stages ?? Array.Empty<string>()),
            SupportedProductHandlingCodes =
                Array.AsReadOnly(products ?? Array.Empty<string>()),
            SupportedEngagementSignalCodes =
                Array.AsReadOnly(signals ?? Array.Empty<string>()),
            SupportedRestrictionCodes =
                Array.AsReadOnly(restrictions ?? Array.Empty<string>())
        };

    private static CollectivePurchasePublishedCommercialCondition CommercialCondition(
        string conditionCode,
        string sourceUrl,
        string? scopeCode = null,
        string? valueCode = null,
        decimal? amount = null,
        string? currencyCode = null,
        int? approximateOrderCount = null)
        => new()
        {
            ConditionCode = conditionCode,
            ScopeCode = scopeCode,
            ValueCode = valueCode,
            Amount = amount,
            CurrencyCode = currencyCode,
            ApproximateOrderCount = approximateOrderCount,
            SourceUrl = sourceUrl,
            ReviewedOn = SnapshotReviewedOn
        };

    private static IReadOnlyList<string> Responsibilities(
        bool requiresFoodFacilityValidation)
    {
        var codes = new List<string>
        {
            ResponsibilityCodes.ImporterOfRecord,
            ResponsibilityCodes.ProductRegulatoryCompliance,
            ResponsibilityCodes.CustomsDutiesAndTaxes,
            ResponsibilityCodes.InventoryTitleAndOwnership,
            ResponsibilityCodes.ParticipantOrderAndAddressConsent
        };

        if (requiresFoodFacilityValidation)
        {
            codes.Add(ResponsibilityCodes.FoodFacilityAndColdChainValidation);
        }

        return codes.AsReadOnly();
    }
}
