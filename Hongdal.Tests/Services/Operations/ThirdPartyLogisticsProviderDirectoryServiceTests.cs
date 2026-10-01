using Hongdal.Contracts.Common.Operations;
using Hongdal.Controllers.Common;
using Hongdal.Services.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Tests.Services.Operations;

public sealed class ThirdPartyLogisticsProviderDirectoryServiceTests
{
    [Fact]
    public void Catalog_ContainsSourceBackedNonOperationalCandidates()
    {
        var providers = UnitedStatesThirdPartyLogisticsProviderCatalog.Providers;

        Assert.Equal(23, providers.Count);
        Assert.Equal(
            providers.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            providers);
        Assert.Equal(
            providers.Count,
            providers.Select(item => item.ProviderKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        foreach (var provider in providers)
        {
            Assert.Equal(OperatingMarketCodes.UnitedStates, provider.MarketCode);
            Assert.Equal(
                ThirdPartyLogisticsProviderDirectoryStatusCodes.ResearchCandidate,
                provider.DirectoryStatusCode);
            Assert.Equal(
                ThirdPartyLogisticsProviderRelationshipStatusCodes.NoPlatformRelationship,
                provider.PlatformRelationshipStatusCode);
            Assert.Equal(
                ThirdPartyLogisticsProviderVerificationStatusCodes
                    .RegulatoryStatusNotVerified,
                provider.RegulatoryVerificationStatusCode);
            Assert.False(provider.IsPlatformPartner);
            Assert.False(provider.CanBeSelectedForOperations);
            Assert.True(provider.RequiresDirectQuote);
            Assert.True(provider.RequiresFacilityCapabilityConfirmation);
            Assert.NotEmpty(provider.CapabilityCodes);
            Assert.NotEmpty(provider.SegmentCodes);
            Assert.NotEmpty(provider.Evidence);
            AssertHttps(provider.OfficialWebsiteUrl);

            var evidencedCapabilities = provider.Evidence
                .SelectMany(item => item.SupportedCapabilityCodes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Assert.Empty(provider.CapabilityCodes.Except(
                evidencedCapabilities,
                StringComparer.OrdinalIgnoreCase));

            foreach (var evidence in provider.Evidence)
            {
                Assert.Equal(
                    UnitedStatesThirdPartyLogisticsProviderCatalog.SnapshotReviewedOn,
                    evidence.ReviewedOn);
                AssertHttps(evidence.SourceUrl);
                Assert.NotEmpty(evidence.SupportedCapabilityCodes);
            }
        }
    }

    [Fact]
    public void Search_FiltersByCapabilityAndKeepsNeutralAlphabeticalOrder()
    {
        var sut = new UnitedStatesThirdPartyLogisticsProviderDirectoryService();

        var response = sut.Search(new ThirdPartyLogisticsProviderDirectoryQuery
        {
            CapabilityCode = ThirdPartyLogisticsProviderCapabilityCodes.ColdChain
        });

        Assert.True(response.Success);
        Assert.False(response.IsRecommendation);
        Assert.Equal(
            ThirdPartyLogisticsProviderSelectionPolicyCodes.NeutralCandidateDirectory,
            response.SelectionPolicyCode);
        Assert.Equal(
            new[] { "Americold", "ODW Logistics" },
            response.Items.Select(item => item.DisplayName));
        Assert.Equal(9, response.RegulatoryVerificationResources.Count);
        Assert.Contains(
            ThirdPartyLogisticsProviderCapabilityCodes.ColdChain,
            response.AvailableCapabilityCodes);
    }

    [Fact]
    public void Search_AppliesTextSegmentAndPaginationWithoutRanking()
    {
        var sut = new UnitedStatesThirdPartyLogisticsProviderDirectoryService();

        var searchResponse = sut.Search(new ThirdPartyLogisticsProviderDirectoryQuery
        {
            SearchText = "CJ",
            SegmentCode = ThirdPartyLogisticsProviderSegmentCodes.PortAndImportDistribution
        });
        var pagedResponse = sut.Search(new ThirdPartyLogisticsProviderDirectoryQuery
        {
            Page = 2,
            PageSize = 3
        });

        Assert.Equal("cj-logistics-america", Assert.Single(searchResponse.Items).ProviderKey);
        Assert.Equal(23, pagedResponse.TotalCount);
        Assert.Equal(2, pagedResponse.Page);
        Assert.Equal(3, pagedResponse.PageSize);
        Assert.Equal(new[] { "DHL Express United States", "DHL Supply Chain", "DSV" },
            pagedResponse.Items.Select(item => item.DisplayName));
    }

    [Fact]
    public void CollectivePurchaseCatalog_MapsEvidenceBackedNonExecutableProfiles()
    {
        var providersByKey = UnitedStatesThirdPartyLogisticsProviderCatalog.Providers
            .ToDictionary(item => item.ProviderKey, StringComparer.OrdinalIgnoreCase);
        var profiles = UnitedStatesCollectivePurchaseLogisticsCatalog.Profiles;

        Assert.Equal(12, profiles.Count);
        Assert.Equal(
            profiles.OrderBy(
                item => providersByKey[item.ProviderKey].DisplayName,
                StringComparer.OrdinalIgnoreCase),
            profiles);
        Assert.Equal(
            profiles.Count,
            profiles.Select(item => item.ProviderKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        foreach (var profile in profiles)
        {
            Assert.True(providersByKey.ContainsKey(profile.ProviderKey));
            Assert.True(profile.RequiresRoleComposition);
            Assert.False(profile.CanAutoAssign);
            Assert.False(profile.CanExecuteWithoutContract);
            Assert.NotEmpty(profile.StageCodes);
            Assert.NotEmpty(profile.ProductHandlingCodes);
            Assert.NotEmpty(profile.ExternalResponsibilityCodes);
            Assert.NotEmpty(profile.Evidence);
            AssertHttps(profile.OfficialInquiryUrl);

            AssertCodesAreEvidenced(
                profile.StageCodes,
                profile.Evidence.SelectMany(item => item.SupportedStageCodes));
            AssertCodesAreEvidenced(
                profile.ProductHandlingCodes,
                profile.Evidence.SelectMany(
                    item => item.SupportedProductHandlingCodes));
            AssertCodesAreEvidenced(
                profile.EngagementSignalCodes,
                profile.Evidence.SelectMany(
                    item => item.SupportedEngagementSignalCodes));
            AssertCodesAreEvidenced(
                profile.ExplicitRestrictionCodes,
                profile.Evidence.SelectMany(item => item.SupportedRestrictionCodes));

            foreach (var evidence in profile.Evidence)
            {
                Assert.Equal(
                    UnitedStatesCollectivePurchaseLogisticsCatalog.SnapshotReviewedOn,
                    evidence.ReviewedOn);
                AssertHttps(evidence.SourceUrl);
                Assert.True(
                    evidence.SupportedStageCodes.Count > 0
                    || evidence.SupportedProductHandlingCodes.Count > 0
                    || evidence.SupportedEngagementSignalCodes.Count > 0
                    || evidence.SupportedRestrictionCodes.Count > 0);
            }

            foreach (var condition in profile.PublishedCommercialConditions)
            {
                Assert.True(condition.RequiresReconfirmationBeforeContract);
                Assert.Equal(
                    UnitedStatesCollectivePurchaseLogisticsCatalog.SnapshotReviewedOn,
                    condition.ReviewedOn);
                AssertHttps(condition.SourceUrl);
            }
        }
    }

    [Fact]
    public void CollectivePurchaseCatalog_RecordsPublishedSmallCampaignConditions()
    {
        var eFulfillment = UnitedStatesCollectivePurchaseLogisticsCatalog.Profiles
            .Single(item => item.ProviderKey == "efulfillment-service");
        var fulfillrite = UnitedStatesCollectivePurchaseLogisticsCatalog.Profiles
            .Single(item => item.ProviderKey == "fulfillrite");

        var ongoingMinimum = eFulfillment.PublishedCommercialConditions.Single(
            item => item.ConditionCode ==
                    CollectivePurchaseCommercialConditionCodes.OrderMinimum);
        var campaignGuideline = eFulfillment.PublishedCommercialConditions.Single(
            item => item.ConditionCode ==
                    CollectivePurchaseCommercialConditionCodes
                        .CampaignBackerOrderGuideline);
        var monthlyMinimum = fulfillrite.PublishedCommercialConditions.Single(
            item => item.ConditionCode ==
                    CollectivePurchaseCommercialConditionCodes
                        .MonthlyPickPackMinimum);
        var approximateOrders = fulfillrite.PublishedCommercialConditions.Single(
            item => item.ConditionCode ==
                    CollectivePurchaseCommercialConditionCodes
                        .ApproximateOrdersAtMonthlyMinimum);

        Assert.Equal(
            CollectivePurchaseCommercialConditionScopeCodes.OngoingFulfillment,
            ongoingMinimum.ScopeCode);
        Assert.Equal(
            CollectivePurchaseCommercialConditionValueCodes.NoneAdvertised,
            ongoingMinimum.ValueCode);
        Assert.Equal(200, campaignGuideline.ApproximateOrderCount);
        Assert.Equal(399m, monthlyMinimum.Amount);
        Assert.Equal("USD", monthlyMinimum.CurrencyCode);
        Assert.Equal(140, approximateOrders.ApproximateOrderCount);
    }

    [Fact]
    public void CollectivePurchaseSearch_FiltersByCampaignProductAndStageWithoutRanking()
    {
        var sut = new UnitedStatesThirdPartyLogisticsProviderDirectoryService();

        var campaign = sut.SearchForCollectivePurchase(
            new CollectivePurchaseLogisticsDirectoryQuery
            {
                EngagementSignalCode =
                    CollectivePurchaseEngagementSignalCodes
                        .CampaignFulfillmentAdvertised
            });
        var frozen = sut.SearchForCollectivePurchase(
            new CollectivePurchaseLogisticsDirectoryQuery
            {
                ProductHandlingCode =
                    CollectivePurchaseProductHandlingCodes
                        .FrozenFoodByFacilityReview
            });
        var heavy = sut.SearchForCollectivePurchase(
            new CollectivePurchaseLogisticsDirectoryQuery
            {
                ProductHandlingCode =
                    CollectivePurchaseProductHandlingCodes.HeavyOrBulkyGoods
            });
        var port = sut.SearchForCollectivePurchase(
            new CollectivePurchaseLogisticsDirectoryQuery
            {
                StageCode =
                    CollectivePurchaseLogisticsStageCodes.PortDrayageAndTransload
            });

        Assert.True(campaign.Success);
        Assert.False(campaign.IsRecommendation);
        Assert.Equal(
            new[]
            {
                "eFulfillment Service",
                "Fulfillrite",
                "ShipBob",
                "ShipMonk"
            },
            campaign.Items.Select(item => item.Provider.DisplayName));
        Assert.Equal(
            new[] { "Americold", "ODW Logistics" },
            frozen.Items.Select(item => item.Provider.DisplayName));
        Assert.Equal(
            "Red Stag Fulfillment",
            Assert.Single(heavy.Items).Provider.DisplayName);
        Assert.Equal(
            new[] { "NFI Industries", "Ryder Supply Chain Solutions" },
            port.Items.Select(item => item.Provider.DisplayName));
        Assert.All(
            campaign.Items,
            item =>
            {
                Assert.False(item.Provider.IsPlatformPartner);
                Assert.False(item.Provider.CanBeSelectedForOperations);
                Assert.False(item.CollectivePurchaseProfile.CanAutoAssign);
                Assert.False(item.CollectivePurchaseProfile.CanExecuteWithoutContract);
            });
        Assert.Equal(11, campaign.RequiredQuoteInputCodes.Count);
        Assert.Equal(9, campaign.RegulatoryVerificationResources.Count);
    }

    [Fact]
    public void CollectivePurchaseSearch_SeparatesOngoingMinimumFromCampaignGuideline()
    {
        var sut = new UnitedStatesThirdPartyLogisticsProviderDirectoryService();

        var response = sut.SearchForCollectivePurchase(
            new CollectivePurchaseLogisticsDirectoryQuery
            {
                EngagementSignalCode =
                    CollectivePurchaseEngagementSignalCodes
                        .NoOngoingOrderMinimumAdvertised
            });

        Assert.Equal(
            "efulfillment-service",
            Assert.Single(response.Items).Provider.ProviderKey);
        Assert.Contains(
            response.Items[0].CollectivePurchaseProfile
                .PublishedCommercialConditions,
            item => item.ConditionCode ==
                    CollectivePurchaseCommercialConditionCodes
                        .CampaignBackerOrderGuideline
                    && item.ApproximateOrderCount == 200);
    }

    [Fact]
    public void BondedToDoorCatalog_SeparatesClaimsFromCurrentAuthorization()
    {
        var providersByKey = UnitedStatesThirdPartyLogisticsProviderCatalog.Providers
            .ToDictionary(item => item.ProviderKey, StringComparer.OrdinalIgnoreCase);
        var profiles = UnitedStatesBondedToDoorLogisticsCatalog.Profiles;

        Assert.Equal(10, profiles.Count);
        Assert.Equal(
            profiles.OrderBy(
                item => providersByKey[item.ProviderKey].DisplayName,
                StringComparer.OrdinalIgnoreCase),
            profiles);
        Assert.Equal(
            profiles.Count,
            profiles.Select(item => item.ProviderKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        foreach (var profile in profiles)
        {
            Assert.True(providersByKey.ContainsKey(profile.ProviderKey));
            Assert.True(profile.RequiresRoleComposition);
            Assert.False(profile.CanAutoAssign);
            Assert.False(profile.CanExecuteWithoutContract);
            Assert.False(profile.IsEndToEndContractConfirmed);
            Assert.False(profile.IsCustomsBrokerPermitIndependentlyVerified);
            Assert.False(profile.IsBondedCarrierAuthorityIndependentlyVerified);
            Assert.NotEmpty(profile.StageCodes);
            Assert.NotEmpty(profile.StorageModelCodes);
            Assert.Equal(
                UnitedStatesBondedToDoorLogisticsCatalog
                    .UniversalRoleRequirementCodes,
                profile.RequiredRoleCodes);
            Assert.Contains(
                BondedToDoorDirectoryBoundaryCodes
                    .EndToEndSingleContractNotVerified,
                profile.DirectoryBoundaryCodes);
            Assert.NotEmpty(profile.Evidence);
            AssertHttps(profile.OfficialInquiryUrl);

            AssertCodesAreEvidenced(
                profile.StageCodes,
                profile.Evidence.SelectMany(item => item.SupportedStageCodes));
            AssertCodesAreEvidenced(
                profile.StorageModelCodes,
                profile.Evidence.SelectMany(item => item.SupportedStorageModelCodes));

            foreach (var evidence in profile.Evidence)
            {
                Assert.Equal(
                    UnitedStatesBondedToDoorLogisticsCatalog.SnapshotReviewedOn,
                    evidence.ReviewedOn);
                AssertHttps(evidence.SourceUrl);
            }
        }

        var facilities = profiles.SelectMany(item => item.FacilityClaims).ToArray();
        Assert.Equal(9, facilities.Length);
        Assert.Equal(
            facilities.Length,
            facilities.Select(item => item.FacilityKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        foreach (var facility in facilities)
        {
            Assert.Equal(
                CustomsControlledFacilityVerificationStatusCodes
                    .OfficialProviderClaimReviewed,
                facility.ClaimVerificationStatusCode);
            Assert.Equal(
                CustomsControlledFacilityVerificationStatusCodes
                    .CurrentAuthorizationNotIndependentlyVerified,
                facility.AuthorizationVerificationStatusCode);
            Assert.True(facility.RequiresCurrentAuthorizationConfirmation);
            Assert.True(facility.RequiresCurrentFirmsCodeConfirmation);
            AssertHttps(facility.ClaimSourceUrl);
        }

        var stgNorfolk = Assert.Single(facilities, item => item.FirmsCode == "LDF9");
        Assert.Equal(
            CustomsControlledFacilityOperatorRelationshipCodes
                .PartnerOrAgentOperated,
            stgNorfolk.OperatorRelationshipCode);

        var geodis = profiles.Single(item => item.ProviderKey == "geodis");
        Assert.Contains(
            BondedToDoorDirectoryBoundaryCodes
                .UnitedStatesBondedWarehouseNotOfferedByProvider,
            geodis.DirectoryBoundaryCodes);
        Assert.DoesNotContain(
            CustomsControlledStorageModelCodes.CustomsBondedWarehouse,
            geodis.StorageModelCodes);
    }

    [Fact]
    public void BondedToDoorSearch_FiltersStorageStageStateAndFirmsCodeWithoutRanking()
    {
        var sut = new UnitedStatesThirdPartyLogisticsProviderDirectoryService();

        var bonded = sut.SearchBondedToDoor(
            new BondedToDoorLogisticsDirectoryQuery
            {
                StorageModelCode =
                    CustomsControlledStorageModelCodes.CustomsBondedWarehouse
            });
        var foreignTradeZone = sut.SearchBondedToDoor(
            new BondedToDoorLogisticsDirectoryQuery
            {
                StorageModelCode =
                    CustomsControlledStorageModelCodes.ForeignTradeZone
            });
        var inBond = sut.SearchBondedToDoor(
            new BondedToDoorLogisticsDirectoryQuery
            {
                StageCode = BondedToDoorLogisticsStageCodes.InBondTransportation
            });
        var virginia = sut.SearchBondedToDoor(
            new BondedToDoorLogisticsDirectoryQuery { StateCode = "va" });
        var firmsCode = sut.SearchBondedToDoor(
            new BondedToDoorLogisticsDirectoryQuery { SearchText = "LDF9" });

        Assert.True(bonded.Success);
        Assert.False(bonded.IsRecommendation);
        Assert.Equal(
            new[]
            {
                "Phoenix Warehouse",
                "STG Logistics",
                "World Distribution Services"
            },
            bonded.Items.Select(item => item.Provider.DisplayName));
        Assert.Equal(
            new[] { "FedEx Supply Chain", "GEODIS", "UPS Supply Chain Solutions" },
            foreignTradeZone.Items.Select(item => item.Provider.DisplayName));
        Assert.Equal(
            new[]
            {
                "DHL Express United States",
                "GEODIS",
                "UPS Supply Chain Solutions"
            },
            inBond.Items.Select(item => item.Provider.DisplayName));
        Assert.Equal(
            new[] { "STG Logistics", "World Distribution Services" },
            virginia.Items.Select(item => item.Provider.DisplayName));
        Assert.Equal("stg-logistics", Assert.Single(firmsCode.Items).Provider.ProviderKey);
        Assert.Equal(
            new[] { "CA", "IL", "NJ", "NY", "OH", "TX", "VA" },
            bonded.AvailableStateCodes);
        Assert.Equal(9, bonded.RegulatoryVerificationResources.Count);
        Assert.Contains(
            BondedToDoorRoleRequirementCodes
                .ExactFacilityAuthorizationAndFirmsCodeVerificationRequired,
            bonded.UniversalRoleRequirementCodes);
    }

    [Fact]
    public void UnavailableService_RejectsNonUnitedStatesDeployment()
    {
        var sut = new UnavailableThirdPartyLogisticsProviderDirectoryService(
            new OperatingMarketDeployment(OperatingMarketCodes.Korea));

        var response = sut.Search(new ThirdPartyLogisticsProviderDirectoryQuery());
        var collectivePurchaseResponse = sut.SearchForCollectivePurchase(
            new CollectivePurchaseLogisticsDirectoryQuery());
        var bondedToDoorResponse = sut.SearchBondedToDoor(
            new BondedToDoorLogisticsDirectoryQuery());

        Assert.False(response.Success);
        Assert.Equal(OperatingMarketCodes.Korea, response.MarketCode);
        Assert.Equal(
            ThirdPartyLogisticsProviderDirectoryErrorCodes
                .MarketNotAvailableInDeployment,
            response.ErrorCode);
        Assert.Empty(response.Items);
        Assert.False(collectivePurchaseResponse.Success);
        Assert.Equal(OperatingMarketCodes.Korea, collectivePurchaseResponse.MarketCode);
        Assert.Empty(collectivePurchaseResponse.Items);
        Assert.False(bondedToDoorResponse.Success);
        Assert.Equal(OperatingMarketCodes.Korea, bondedToDoorResponse.MarketCode);
        Assert.Empty(bondedToDoorResponse.Items);
    }

    [Fact]
    public void Controller_ReturnsOkForUnitedStatesAndNotFoundForOtherMarkets()
    {
        var unitedStatesController = new ThirdPartyLogisticsProvidersController(
            new UnitedStatesThirdPartyLogisticsProviderDirectoryService());
        var koreaController = new ThirdPartyLogisticsProvidersController(
            new UnavailableThirdPartyLogisticsProviderDirectoryService(
                new OperatingMarketDeployment(OperatingMarketCodes.Korea)));

        var unitedStatesResult = unitedStatesController.Get(pageSize: 5);
        var koreaResult = koreaController.Get();
        var unitedStatesCollectivePurchaseResult =
            unitedStatesController.GetForCollectivePurchase(pageSize: 5);
        var koreaCollectivePurchaseResult =
            koreaController.GetForCollectivePurchase();
        var unitedStatesBondedToDoorResult =
            unitedStatesController.GetBondedToDoor(pageSize: 5);
        var koreaBondedToDoorResult = koreaController.GetBondedToDoor();

        var ok = Assert.IsType<OkObjectResult>(unitedStatesResult.Result);
        var okResponse = Assert.IsType<ThirdPartyLogisticsProviderDirectoryResponse>(ok.Value);
        Assert.Equal(5, okResponse.Items.Count);

        var notFound = Assert.IsType<NotFoundObjectResult>(koreaResult.Result);
        var unavailableResponse =
            Assert.IsType<ThirdPartyLogisticsProviderDirectoryResponse>(notFound.Value);
        Assert.Equal(
            ThirdPartyLogisticsProviderDirectoryErrorCodes
                .MarketNotAvailableInDeployment,
            unavailableResponse.ErrorCode);

        var collectivePurchaseOk = Assert.IsType<OkObjectResult>(
            unitedStatesCollectivePurchaseResult.Result);
        var collectivePurchaseOkResponse =
            Assert.IsType<CollectivePurchaseLogisticsDirectoryResponse>(
                collectivePurchaseOk.Value);
        Assert.Equal(5, collectivePurchaseOkResponse.Items.Count);

        var collectivePurchaseNotFound = Assert.IsType<NotFoundObjectResult>(
            koreaCollectivePurchaseResult.Result);
        var collectivePurchaseUnavailableResponse =
            Assert.IsType<CollectivePurchaseLogisticsDirectoryResponse>(
                collectivePurchaseNotFound.Value);
        Assert.Equal(
            ThirdPartyLogisticsProviderDirectoryErrorCodes
                .MarketNotAvailableInDeployment,
            collectivePurchaseUnavailableResponse.ErrorCode);

        var bondedToDoorOk = Assert.IsType<OkObjectResult>(
            unitedStatesBondedToDoorResult.Result);
        var bondedToDoorOkResponse =
            Assert.IsType<BondedToDoorLogisticsDirectoryResponse>(
                bondedToDoorOk.Value);
        Assert.Equal(5, bondedToDoorOkResponse.Items.Count);

        var bondedToDoorNotFound = Assert.IsType<NotFoundObjectResult>(
            koreaBondedToDoorResult.Result);
        var bondedToDoorUnavailableResponse =
            Assert.IsType<BondedToDoorLogisticsDirectoryResponse>(
                bondedToDoorNotFound.Value);
        Assert.Equal(
            ThirdPartyLogisticsProviderDirectoryErrorCodes
                .MarketNotAvailableInDeployment,
            bondedToDoorUnavailableResponse.ErrorCode);
    }

    private static void AssertCodesAreEvidenced(
        IEnumerable<string> expectedCodes,
        IEnumerable<string> evidencedCodes)
    {
        var evidence = evidencedCodes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Empty(expectedCodes.Except(evidence, StringComparer.OrdinalIgnoreCase));
    }

    private static void AssertHttps(string value)
    {
        Assert.True(Uri.TryCreate(value, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    }
}
