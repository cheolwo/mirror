using Hongdal.Contracts.Common.CollectiveProcurement;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class CommunityCollectiveActionPageViewModelTests
{
    [Fact]
    public void 알수없는PageKey는_마음모으기로정규화한다()
    {
        var campaignId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

        Assert.Equal(
            CommunityCollectiveActionPageKeys.Gathering,
            CommunityCollectiveActionPageKeys.Normalize("unknown"));
        Assert.Equal(
            $"/community/actions/in-progress?campaignId={campaignId:D}",
            CommunityCollectiveActionRoutes.Build(CommunityCollectiveActionPageKeys.InProgress, campaignId));
        Assert.Equal("/community/posts/31", CommunityCollectiveActionRoutes.BuildSourcePost(31));
        Assert.Equal("/community", CommunityCollectiveActionRoutes.BuildSourcePost(null));
    }

    [Fact]
    public void 추가참여ViewModel은_모든필수근거의최솟값으로_확인여력을계산한다()
    {
        var snapshot = CommunityCollectiveActionPreviewCatalog.Create()
            .Single(item => item.Id == Guid.Parse("11111111-1111-4111-8111-111111111111"));
        var viewModel = new CommunityActionExecutionViewModel();

        viewModel.Apply(snapshot);
        viewModel.AdditionalQuantity = 8m;

        Assert.True(viewModel.AllRequiredCapacityConfirmed);
        Assert.Equal(52m, viewModel.ConfirmedMaximumTotalQuantity);
        Assert.Equal(8m, viewModel.ConfirmedRemainingQuantity);
        Assert.True(viewModel.SelectedQuantityFitsConfirmedCapacity);

        viewModel.AdditionalQuantity = 9m;

        Assert.False(viewModel.SelectedQuantityFitsConfirmedCapacity);
    }

    [Fact]
    public void 확인대기근거가있으면_추가수량을확정여력으로표시하지않는다()
    {
        var viewModel = new CommunityActionExecutionViewModel();
        viewModel.Apply(new CommunityCollectiveActionSnapshot
        {
            CurrentCommittedQuantity = 10m,
            CurrentPotentialQuantity = 12m,
            QuantityUnit = "상자",
            CapacityEvidence =
            [
                new("supply", "공급", "공급자", CommunityCapacityEvidenceStatus.Confirmed, 20m, "확인"),
                new("transport", "운송", "운송사", CommunityCapacityEvidenceStatus.Pending, 18m, "확인 중")
            ]
        });

        Assert.False(viewModel.AllRequiredCapacityConfirmed);
        Assert.Null(viewModel.ConfirmedMaximumTotalQuantity);
        Assert.Equal(0m, viewModel.ConfirmedRemainingQuantity);
        Assert.Equal(6m, viewModel.EstimatedRemainingQuantity);
        Assert.Contains("확인하고", viewModel.CapacityHeadline);
    }

    [Fact]
    public async Task PageViewModel은_하위ViewModel을조립하고_빈Feed에서는둘러보기상태를연다()
    {
        using var page = CreatePage(new FakeSource([]));
        page.Configure(CommunityCollectiveActionPageKeys.InProgress, null);

        var initialized = await page.초기화Async();

        Assert.True(initialized);
        Assert.True(page.IsPreview);
        Assert.Equal(CommunityCollectiveActionPageKeys.InProgress, page.CurrentPage.Key);
        Assert.NotNull(page.SelectedAction);
        Assert.Equal(CommunityCollectiveActionPageKeys.InProgress, page.SelectedAction!.CurrentPageKey);
        Assert.Equal(8m, page.Execution.ConfirmedRemainingQuantity);
    }

    [Fact]
    public async Task 실제Campaign이있으면_예시가아닌실제Feed로표시한다()
    {
        var campaign = new CommunityVoteResponse
        {
            Id = Guid.NewGuid(),
            SourcePostId = 902,
            Title = "동네 쌀 공동구매",
            Description = "필요한 가구가 함께 수량을 모읍니다.",
            CommunityScope = "서울",
            Status = CommunityVoteStatusCodes.Open,
            TotalVoteCount = 5,
            Options =
            [
                new CommunityVoteOptionResponse
                {
                    OptionId = "rice",
                    Text = "쌀 10kg",
                    RequestedQuantity = 8,
                    QuantityUnit = "포"
                }
            ],
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                MinimumTotalQuantity = 20,
                TotalRequestedQuantity = 8,
                QuantityUnit = "포",
                ShipFromCountryCode = "KR",
                DeliveryCountryCode = "KR"
            }
        };
        using var page = CreatePage(new FakeSource([campaign]));
        page.Configure(CommunityCollectiveActionPageKeys.Gathering, campaign.Id);

        await page.초기화Async();

        Assert.False(page.IsPreview);
        Assert.Equal(campaign.Id, page.SelectedAction?.Id);
        Assert.Equal(902, page.SelectedAction?.SourcePostId);
        Assert.Equal("동네 쌀 공동구매", page.SelectedAction?.Title);
        Assert.Equal(8m, page.Execution.CurrentCommittedQuantity);
    }

    [Fact]
    public void 미국공동수입Snapshot은_배달권과보세부터주소배송까지_여정을조립한다()
    {
        var campaign = new CommunityVoteResponse
        {
            Id = Guid.NewGuid(),
            Title = "미국 구매자 공동수입",
            Status = CommunityVoteStatusCodes.Open,
            TotalVoteCount = 12,
            Options =
            [
                new CommunityVoteOptionResponse
                {
                    Text = "중국산 생활용품",
                    RequestedQuantity = 24,
                    QuantityUnit = "상자"
                }
            ],
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                OperatingMarketCountryCode = "US",
                ShipFromCountryCode = "CN",
                DeliveryCountryCode = "US",
                IsGroupImportCandidate = true,
                TotalRequestedQuantity = 24,
                MinimumParticipantCount = 10,
                MinimumTotalQuantity = 20,
                IsMinimumReached = true,
                QuantityUnit = "상자",
                ServiceAreaKey = "us-place:3651000",
                ServiceAreaLabel = "New York city"
            }
        };
        var journey = new CommunityActionJourneyResponse
        {
            ProvisionalLedgerId = "provisional-us-1",
            RoleSlots =
            [
                new CommunityActionJourneyRoleSlotResponse
                {
                    RoleCode = CommunityPostPartyRoleCodes
                        .CustomsControlledFacilityOperator,
                    CategoryCode = CommunityPartyRoleCategoryCodes.Fulfillment,
                    Label = "보세창고·FTZ 운영자",
                    ConfirmedParticipantCount = 1
                }
            ]
        };

        var snapshot = CommunityCollectiveActionSnapshotFactory.FromCampaign(
            campaign,
            journey);

        Assert.True(snapshot.Delivery.IsApplicable);
        Assert.True(snapshot.Delivery.RecruitmentScopeVerified);
        Assert.False(snapshot.Delivery.IndividualAddressesVisibleToCommunity);
        Assert.False(snapshot.Delivery.ProviderSelectionIsAutomated);
        Assert.True(snapshot.Delivery.RequiresSeparateProviderContracts);
        Assert.Equal(11, snapshot.Delivery.Stages.Count);
        Assert.Equal(2, snapshot.Delivery.CompletedStageCount);
        Assert.Equal(
            CommunityCollectiveImportDeliveryStageCodes.RoleFormation,
            snapshot.Delivery.CurrentStage?.Code);
        Assert.True(snapshot.Delivery.OriginPreparation.IsApplicable);
        Assert.True(snapshot.Delivery.OriginPreparation.IsChinaOrigin);
        Assert.False(snapshot.Delivery.OriginPreparation.SavingsConfirmed);
        Assert.False(snapshot.Delivery.OriginPreparation.UsesDeMinimisAssumption);
        Assert.False(snapshot.Delivery.OriginPreparation.ArtificialShipmentSplittingAllowed);
        Assert.Contains(
            snapshot.Delivery.OriginPreparation.CostInputs,
            input => input.CategoryCode ==
                     CollectiveProcurementCostCategoryCodes.OriginPreparation
                     && input.Kind ==
                     CommunityCollectiveImportOriginPreparationCostInputKind.CandidateCost);
        Assert.Contains(
            snapshot.Delivery.OriginPreparation.Tasks,
            task => task.Code ==
                    CommunityCollectiveImportOriginPreparationTaskCodes.ParcelLabelData
                    && task.Timing ==
                    CommunityCollectiveImportOriginPreparationTiming
                        .PrepareBeforeExportAndFinalizeAfterRouting);
        Assert.Contains(
            snapshot.Delivery.Stages,
            stage => stage.Number == 4
                     && stage.Code ==
                     CommunityCollectiveImportDeliveryStageCodes.OriginFactoryPreparation);
        Assert.Contains(
            snapshot.Delivery.Stages,
            stage => stage.Code ==
                     "ParticipantAddressFinalMileDelivery"
                     && stage.State == CommunityCollectiveImportDeliveryStageState
                         .SeparateContractRequired);
    }

    [Fact]
    public async Task 미국공동수입둘러보기는_PageViewModel의배송하위ViewModel로전달된다()
    {
        using var page = CreatePage(new FakeSource([]));
        var preview = CommunityCollectiveActionPreviewCatalog.Create()
            .Single(item => item.Delivery.IsApplicable);
        page.Configure(CommunityCollectiveActionPageKeys.Party, preview.Id);

        await page.초기화Async();

        Assert.Equal(preview.Id, page.SelectedAction?.Id);
        Assert.True(page.Delivery.IsApplicable);
        Assert.Equal("New York city", page.Delivery.RecruitmentScopeLabel);
        Assert.Equal(11, page.Delivery.TotalStageCount);
        Assert.Equal(
            "수입·물류 역할 구성",
            page.Delivery.CurrentStage?.Label);
    }

    [Fact]
    public void 한국행수입육은_통관뒤전통시장2차가공과동네냉장배송으로이어진다()
    {
        var campaign = new CommunityVoteResponse
        {
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                OperatingMarketCountryCode = "KR",
                ShipFromCountryCode = "AU",
                DeliveryCountryCode = "KR",
                IsGroupImportCandidate = true,
                CustomsClearanceStatusCode = CommunityGroupPurchaseCustomsClearanceStatusCodes.Cleared,
                HsCode = "0202.30",
                TemperatureCode = "냉동"
            }
        };

        var snapshot = CommunityTraditionalMarketImportedMeatScenarioFactory.Build(campaign);

        Assert.True(snapshot.IsApplicable);
        Assert.True(snapshot.CustomsReleased);
        Assert.False(snapshot.IncludesLiveAnimalHandling);
        Assert.True(snapshot.OverseasSlaughterRemainsOutsideMarketScope);
        Assert.True(snapshot.ProcessingStartsAfterOfficialRelease);
        Assert.False(snapshot.PlatformSelectsProcessor);
        Assert.Equal("020230", snapshot.HsCode);
        Assert.Equal(10, snapshot.Stages.Count);
        Assert.Equal(4, snapshot.CompletedStageCount);
        Assert.Equal(
            CommunityTraditionalMarketImportedMeatStageCodes.RefrigeratedTransportToMarket,
            snapshot.CurrentStage?.Code);
        Assert.Contains(
            snapshot.Requirements,
            requirement => requirement.Code ==
                           CommunityTraditionalMarketImportedMeatRequirementCodes.ProcessingBusinessScope
                           && requirement.CandidateBusinessTypes.Contains("식육포장처리업"));
        Assert.Contains(
            snapshot.CostInputs,
            input => input.CategoryCode ==
                     CollectiveProcurementCostCategoryCodes.DomesticValueAddedProcessing
                     && input.PaysLocalBusiness);
        Assert.Contains(
            snapshot.CostInputs,
            input => input.CategoryCode ==
                     CollectiveProcurementCostCategoryCodes.LocalColdChainDelivery
                     && input.PaysLocalBusiness);
    }

    [Fact]
    public void 한국행수입품이라도_HS02육류가아니면전통시장육류흐름을열지않는다()
    {
        var campaign = new CommunityVoteResponse
        {
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                ShipFromCountryCode = "ES",
                DeliveryCountryCode = "KR",
                IsGroupImportCandidate = true,
                HsCode = "1509.20"
            }
        };

        var snapshot = CommunityTraditionalMarketImportedMeatScenarioFactory.Build(campaign);

        Assert.False(snapshot.IsApplicable);
        Assert.Empty(snapshot.Stages);
    }

    [Fact]
    public void 국내반출확인전에는_전통시장가공과배송단계를열지않는다()
    {
        var campaign = new CommunityVoteResponse
        {
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                ShipFromCountryCode = "AU",
                DeliveryCountryCode = "KR",
                IsGroupImportCandidate = true,
                CustomsClearanceStatusCode = CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown,
                HsCode = "0202.30",
                TemperatureCode = "냉동"
            }
        };

        var snapshot = CommunityTraditionalMarketImportedMeatScenarioFactory.Build(campaign);

        Assert.True(snapshot.IsApplicable);
        Assert.False(snapshot.CustomsReleased);
        Assert.Equal(0, snapshot.CompletedStageCount);
        Assert.Equal(
            CommunityTraditionalMarketImportedMeatStageCodes.KoreaQuarantineAndInspection,
            snapshot.CurrentStage?.Code);
        Assert.All(
            snapshot.Stages.Where(stage => stage.Number >= 5),
            stage => Assert.Equal(
                CommunityTraditionalMarketImportedMeatStageState.Waiting,
                stage.State));
    }

    [Fact]
    public async Task 호주산수입육둘러보기는_PageViewModel배송하위ViewModel로전달된다()
    {
        using var page = CreatePage(new FakeSource([]));
        var preview = CommunityCollectiveActionPreviewCatalog.Create()
            .Single(item => item.TraditionalMarketImportedMeatFulfillment.IsApplicable);
        page.Configure(CommunityCollectiveActionPageKeys.Party, preview.Id);

        await page.초기화Async();

        Assert.Equal(preview.Id, page.SelectedAction?.Id);
        Assert.True(page.TraditionalMarketImportedMeat.IsApplicable);
        Assert.Equal("AU", page.TraditionalMarketImportedMeat.Snapshot.SourceCountryCode);
        Assert.Equal(10, page.TraditionalMarketImportedMeat.Snapshot.Stages.Count);
        Assert.Equal(
            "전통시장으로 냉장·냉동 운송",
            page.TraditionalMarketImportedMeat.Snapshot.CurrentStage?.Label);
    }

    [Fact]
    public void 전통시장생활권농산물은_상인회협의전장날제안으로조립된다()
    {
        var campaign = new CommunityVoteResponse
        {
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                ShipFromCountryCode = "KR",
                DeliveryCountryCode = "KR",
                ServiceAreaKey = "traditional-market:sample",
                ServiceAreaLabel = "성남 전통시장 생활권",
                HsCode = "0702.00",
                QuantityUnit = "상자"
            }
        };

        var snapshot = CommunityMarketDayScenarioFactory.Build(
            campaign,
            "제철 토마토",
            reservedQuantity: 80,
            potentialQuantity: 96,
            quantityUnit: "상자");

        Assert.True(snapshot.IsApplicable);
        Assert.Equal(
            CommunityMarketDayProductHandlingProfileCodes.FreshProduce,
            snapshot.ProductHandlingProfileCode);
        Assert.False(snapshot.ScheduleConfirmed);
        Assert.False(snapshot.AssociationAgreementConfirmed);
        Assert.False(snapshot.PlatformAutomaticallyAssignsMerchants);
        Assert.True(snapshot.ReservedInventoryProtected);
        Assert.True(snapshot.WalkInInventorySeparated);
        Assert.Equal("traditional-market:sample", snapshot.MarketScopeKey);
        Assert.Equal(0m, snapshot.ConfirmedWalkInSaleQuantity);
        Assert.Equal(16m, snapshot.PotentialWalkInSaleQuantity);
        Assert.Equal(16m, snapshot.UnconfirmedWalkInSaleQuantity);
        Assert.False(snapshot.CanAdvertiseWalkInSale);
        Assert.Contains(
            snapshot.MerchantRoles,
            role => role.RoleCode == CommunityMarketDayRoleCodes.FreshProduceMerchant);
        Assert.Contains(
            snapshot.MerchantRoles,
            role => role.RoleCode == CommunityMarketDayRoleCodes.MarketVisualDesigner
                    && !role.Required
                    && !role.Accepted);
        Assert.Contains(
            snapshot.MerchantRoles,
            role => role.RoleCode == CommunityMarketDayRoleCodes.MarketFoodBusinessIngredientBuyer
                    && !role.Required
                    && !role.Accepted);
        Assert.True(snapshot.DomesticSupply.IsApplicable);
        Assert.False(snapshot.DomesticSupply.RequiresCustomsClearance);
        Assert.False(snapshot.DomesticSupply.CanDispatchDirectlyToMarket);
        Assert.Equal(10, snapshot.DomesticSupply.Stages.Count);
        Assert.Contains(
            snapshot.DomesticSupply.Roles,
            role => role.RoleCode == CommunityDomesticMarketSupplyRoleCodes.ProducerOrCooperative
                    && !role.Accepted);
        Assert.Equal(
            "국내 생산자·산지 공급 주체 직접 수락",
            snapshot.DomesticSupply.CurrentStage?.Label);
        Assert.True(snapshot.DomesticSupply.MarketIngredientSupply.IsApplicable);
        Assert.Equal(80m, snapshot.DomesticSupply.MarketIngredientSupply.HouseholdReservedQuantity);
        Assert.Equal(16m, snapshot.DomesticSupply.MarketIngredientSupply.PotentialBusinessSupplyQuantity);
        Assert.Empty(snapshot.DomesticSupply.MarketIngredientSupply.Businesses);
        Assert.False(snapshot.DomesticSupply.MarketIngredientSupply.CanConfirmBusinessSupply);
        Assert.False(snapshot.DomesticSupply.MarketIngredientSupply.PlatformAutomaticallyAssignsBusinesses);
        Assert.Equal(
            "시장 조리 가게 식재료 수요 등록",
            snapshot.DomesticSupply.MarketIngredientSupply.CurrentStage?.Label);
        Assert.Equal(
            "상인회·시장관리자 공동사업 합의",
            snapshot.CurrentStage?.Label);
    }

    [Fact]
    public void 국내수산물공동구매는_콜드체인역할과시장직입고여정으로조립된다()
    {
        var campaign = new CommunityVoteResponse
        {
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                ShipFromCountryCode = "KR",
                DeliveryCountryCode = "KR",
                ServiceAreaKey = "traditional-market:busan",
                ServiceAreaLabel = "부산 전통시장 생활권",
                HsCode = "0302.89",
                TemperatureCode = "chilled",
                QuantityUnit = "상자"
            }
        };

        var snapshot = CommunityMarketDayScenarioFactory.Build(
            campaign,
            "남해안 제철 생선 꾸러미",
            reservedQuantity: 45,
            potentialQuantity: 54,
            quantityUnit: "상자");

        Assert.Equal(
            CommunityMarketDayProductHandlingProfileCodes.FisheriesProducts,
            snapshot.ProductHandlingProfileCode);
        Assert.Contains(
            snapshot.MerchantRoles,
            role => role.RoleCode == CommunityMarketDayRoleCodes.FisheriesMerchant);
        Assert.True(snapshot.DomesticSupply.IsApplicable);
        Assert.Equal(
            CommunityDomesticMarketSupplyProductCategoryCodes.FisheriesProducts,
            snapshot.DomesticSupply.ProductCategoryCode);
        Assert.True(snapshot.DomesticSupply.RequiresColdChain);
        Assert.False(snapshot.DomesticSupply.RequiresCustomsClearance);
        Assert.False(snapshot.DomesticSupply.PlatformAutomaticallySelectsSuppliers);
        Assert.False(snapshot.DomesticSupply.PlatformAutomaticallyAssignsCarriers);
        Assert.Contains(
            snapshot.DomesticSupply.Roles,
            role => role.RoleCode == CommunityDomesticMarketSupplyRoleCodes.OriginToMarketCarrier
                    && role.Label.Contains("냉장·냉동", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.DomesticSupply.Stages,
            stage => stage.Code == CommunityDomesticMarketSupplyStageCodes.MarketReceiving
                     && stage.EvidenceLabel.Contains("선도·온도", StringComparison.Ordinal));
        Assert.True(snapshot.DomesticSupply.MarketIngredientSupply.IsApplicable);
        Assert.True(snapshot.DomesticSupply.MarketIngredientSupply.RequiresStorageConditionConfirmation);
        Assert.Contains(
            snapshot.MerchantRoles,
            role => role.RoleCode == CommunityMarketDayRoleCodes.MarketFoodBusinessIngredientBuyer
                    && role.VerificationLabel.Contains("보관", StringComparison.Ordinal));
    }

    [Fact]
    public void 해외산농산물에는_국내산지직입고여정을적용하지않는다()
    {
        var campaign = new CommunityVoteResponse
        {
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                ShipFromCountryCode = "CN",
                DeliveryCountryCode = "KR",
                ServiceAreaKey = "traditional-market:sample",
                HsCode = "0702.00",
                QuantityUnit = "상자"
            }
        };

        var snapshot = CommunityMarketDayScenarioFactory.Build(
            campaign,
            "수입 토마토",
            reservedQuantity: 20,
            potentialQuantity: 24,
            quantityUnit: "상자");

        Assert.True(snapshot.IsApplicable);
        Assert.False(snapshot.DomesticSupply.IsApplicable);
        Assert.False(snapshot.DomesticSupply.MarketIngredientSupply.IsApplicable);
    }

    [Fact]
    public void 제철농산물시범장날은_예약과현장판매재고를분리한다()
    {
        var preview = CommunityCollectiveActionPreviewCatalog.Create()
            .Single(item => item.MarketDay.State == CommunityMarketDayState.PilotScheduled);

        Assert.Equal(80m, preview.MarketDay.ReservedQuantity);
        Assert.Equal(12m, preview.MarketDay.ConfirmedWalkInSaleQuantity);
        Assert.Equal(16m, preview.MarketDay.PotentialWalkInSaleQuantity);
        Assert.Equal(4m, preview.MarketDay.UnconfirmedWalkInSaleQuantity);
        Assert.True(preview.MarketDay.AssociationAgreementConfirmed);
        Assert.True(preview.MarketDay.ScheduleConfirmed);
        Assert.True(preview.MarketDay.CanAdvertiseWalkInSale);
        Assert.True(preview.MarketDay.ReservedInventoryProtected);
        Assert.True(preview.MarketDay.WalkInInventorySeparated);
        Assert.Equal("traditional-market:sample-seongnam", preview.MarketDay.MarketScopeKey);
        Assert.True(preview.MarketDay.DomesticSupply.IsApplicable);
        Assert.True(preview.MarketDay.DomesticSupply.CanDispatchDirectlyToMarket);
        Assert.True(preview.MarketDay.DomesticSupply.ReservedAllocationProtected);
        Assert.False(preview.MarketDay.DomesticSupply.RequiresCustomsClearance);
        Assert.Equal(10, preview.MarketDay.DomesticSupply.Stages.Count);
        Assert.Equal(
            "산지 선별·포장·상차",
            preview.MarketDay.DomesticSupply.CurrentStage?.Label);
        var ingredientSupply = preview.MarketDay.DomesticSupply.MarketIngredientSupply;
        Assert.True(ingredientSupply.IsApplicable);
        Assert.True(ingredientSupply.HouseholdReservationProtected);
        Assert.True(ingredientSupply.CanConfirmBusinessSupply);
        Assert.False(ingredientSupply.PlatformAutomaticallyAssignsBusinesses);
        Assert.Equal(68m, ingredientSupply.HouseholdReservedQuantity);
        Assert.Equal(12m, ingredientSupply.ConfirmedBusinessSupplyQuantity);
        Assert.Equal(80m, ingredientSupply.HouseholdReservedQuantity + ingredientSupply.ConfirmedBusinessSupplyQuantity);
        Assert.Equal(2, ingredientSupply.Businesses.Count);
        Assert.All(
            ingredientSupply.Businesses,
            business =>
            {
                Assert.True(business.DirectlyAccepted);
                Assert.True(business.BusinessScopeVerified);
                Assert.True(business.StorageConditionConfirmed);
                Assert.StartsWith("market-food-business:", business.BusinessReferenceKey, StringComparison.Ordinal);
            });
        Assert.Equal(
            "시장 입고 시 가게 공급 lot 분리",
            ingredientSupply.CurrentStage?.Label);
        Assert.Contains(
            preview.MarketDay.MerchantRoles,
            role => role.RoleCode == CommunityMarketDayRoleCodes.FreshProduceMerchant
                    && role.Accepted);
        Assert.Contains(
            preview.MarketDay.MerchantRoles,
            role => role.RoleCode == CommunityMarketDayRoleCodes.MarketVisualDesigner
                    && !role.Required
                    && role.Accepted);
    }

    [Fact]
    public async Task 제철농산물장날은_PageViewModel의장날하위ViewModel로전달된다()
    {
        using var page = CreatePage(new FakeSource([]));
        var preview = CommunityCollectiveActionPreviewCatalog.Create()
            .Single(item => item.MarketDay.State == CommunityMarketDayState.PilotScheduled);
        page.Configure(CommunityCollectiveActionPageKeys.InProgress, preview.Id);

        await page.초기화Async();

        Assert.Equal(preview.Id, page.SelectedAction?.Id);
        Assert.True(page.MarketDay.IsApplicable);
        Assert.True(page.MarketDay.CanAdvertiseWalkInSale);
        Assert.True(page.MarketDay.DomesticSupply.IsApplicable);
        Assert.True(page.MarketDay.DomesticSupply.CanDispatchDirectlyToMarket);
        Assert.True(page.MarketDay.MarketIngredientSupply.IsApplicable);
        Assert.True(page.MarketDay.MarketIngredientSupply.CanConfirmBusinessSupply);
        Assert.Equal(
            "장날 입고·검수·상점별 인계",
            page.MarketDay.CurrentStage?.Label);
    }

    private static CommunityCollectiveActionPageViewModel CreatePage(
        ICommunityCollectiveActionSource source)
        => new(
            source,
            new CommunityActionJourneyNavigationViewModel(),
            new CommunityActionCollectionViewModel(),
            new CommunityActionConditionsViewModel(),
            new CommunityActionPartyViewModel(),
            new CommunityActionDeliveryViewModel(),
            new CommunityActionTraditionalMarketImportedMeatViewModel(),
            new CommunityActionMarketDayViewModel(),
            new CommunityActionReadinessViewModel(),
            new CommunityActionExecutionViewModel(),
            new CommunityActionOutcomeViewModel());

    private sealed class FakeSource(IReadOnlyList<CommunityVoteResponse> items)
        : ICommunityCollectiveActionSource
    {
        public Task<IReadOnlyList<CommunityVoteResponse>> LoadAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(items);
    }
}
