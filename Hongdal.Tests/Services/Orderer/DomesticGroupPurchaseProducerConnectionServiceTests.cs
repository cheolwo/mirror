using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.Orderer;

namespace Hongdal.Tests.Services.Orderer;

public sealed class DomesticGroupPurchaseProducerConnectionServiceTests
{
    [Fact]
    public async Task SearchCandidatesAsync_UnconnectedDirectory_ReturnsNoMembersOrContactDetails()
    {
        var service = CreateService();

        var result = await service.SearchCandidatesAsync(null, null, null);

        Assert.Equal(DomesticProducerDirectoryIntegrationStatuses.NotConnected, result.IntegrationStatusCode);
        Assert.Empty(result.Items);
        Assert.False(result.ContactDetailsDisclosed);
    }

    [Fact]
    public async Task CreateDraftAsync_KeepsContactPrivate_AndLimitsReadToOwner()
    {
        var service = CreateService();
        var campaignId = Guid.NewGuid();

        var created = await service.CreateDraftAsync(
            "buyer-1",
            new DomesticProducerContactRequestDraftRequest
            {
                GroupPurchaseCampaignId = campaignId,
                CampaignTitle = "고구마 공동구매",
                ProducerCandidateKey = "producer-1",
                ProducerMaskedDisplayName = "김○○",
                ProductSummary = "고구마",
                RequestedQuantitySummary = "100kg",
                RequiredPackagingFormCode = DomesticProducePackagingFormCodes.CorrugatedBox,
                PackagingUnitSummary = "10kg 골판지 상자",
                QualityGradeSummary = "혼합 크기 허용, 파손 제외",
                RequestedQuantity = 100,
                MaximumAbsorptionQuantity = 150,
                QuantityUnit = "kg",
                CanReceiveSplitShipments = true,
                Message = "공급 가능 여부를 협의하고 싶습니다."
            });

        Assert.Equal(DomesticProducerContactRequestStatuses.Draft, created.StatusCode);
        Assert.False(created.ContactDetailsDisclosed);
        Assert.False(created.IsDurablyPersisted);
        Assert.Equal("고구마", created.ProductSummary);
        Assert.Equal("공급 가능 여부를 협의하고 싶습니다.", created.Message);
        Assert.NotNull(await service.GetDraftAsync("buyer-1", created.DraftId));
        Assert.Null(await service.GetDraftAsync("buyer-2", created.DraftId));
    }

    [Fact]
    public async Task CreateDraftAsync_RejectsBuyerRequestThatExceedsAbsorptionCapacity()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateDraftAsync(
            "buyer-1",
            new DomesticProducerContactRequestDraftRequest
            {
                GroupPurchaseCampaignId = Guid.NewGuid(),
                ProducerCandidateKey = "producer-1",
                RequiredPackagingFormCode = DomesticProducePackagingFormCodes.CorrugatedBox,
                PackagingUnitSummary = "10kg 골판지 상자",
                QualityGradeSummary = "혼합 크기 허용",
                RequestedQuantity = 500,
                MaximumAbsorptionQuantity = 300,
                QuantityUnit = "kg",
                Message = "500kg 출하를 요청합니다."
            }));

        Assert.Contains("최대 인수 물량", error.Message);
    }

    [Fact]
    public async Task SearchRepresentativesAsync_UnconnectedDirectory_ReturnsNoContactDetails()
    {
        var service = CreateService();

        var result = await service.SearchRepresentativesAsync(null, null, null);

        Assert.Equal(DomesticProducerDirectoryIntegrationStatuses.NotConnected, result.IntegrationStatusCode);
        Assert.Empty(result.Items);
        Assert.False(result.ContactDetailsDisclosed);
    }

    [Fact]
    public async Task CreateSupplyOfferDraftAsync_RequiresSafetyConfirmation()
    {
        var service = CreateService();
        var request = CreateSupplyOfferRequest();
        request.FoodSafetyConfirmed = false;

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateSupplyOfferDraftAsync("producer-1", request));

        Assert.Contains("안전 확인", error.Message);
    }

    [Fact]
    public async Task CreateSupplyOfferDraftAsync_KeepsQualityDisclosure_AndLimitsReadToOwner()
    {
        var service = CreateService();
        var request = CreateSupplyOfferRequest();

        var created = await service.CreateSupplyOfferDraftAsync("producer-1", request);

        Assert.Equal(DomesticProducerContactRequestStatuses.Draft, created.StatusCode);
        Assert.True(created.FoodSafetyConfirmed);
        Assert.Equal("크기가 고르지 않지만 파손과 부패는 없습니다.", created.QualityDisclosure);
        Assert.False(created.ContactDetailsDisclosed);
        Assert.False(created.IsDurablyPersisted);
        Assert.NotNull(await service.GetSupplyOfferDraftAsync("producer-1", created.DraftId));
        Assert.Null(await service.GetSupplyOfferDraftAsync("producer-2", created.DraftId));
    }

    [Fact]
    public void PreviewCompatibility_WhenPackagingAndSplitVolumeFit_IsMutuallyFeasible()
    {
        var service = CreateService();

        var result = service.PreviewCompatibility(new DomesticGroupPurchaseSupplyCompatibilityPreviewRequest
        {
            BuyerRequiredPackagingFormCode = DomesticProducePackagingFormCodes.CorrugatedBox,
            BuyerRequestedQuantity = 500,
            BuyerMaximumAbsorptionQuantity = 800,
            BuyerCanReceiveSplitShipments = true,
            ProducerSupportedPackagingFormCodes = [DomesticProducePackagingFormCodes.CorrugatedBox],
            ProducerAvailableQuantity = 1_000,
            ProducerMinimumTakeQuantity = 300,
            ProducerCanSplitShipments = true,
            QuantityUnit = "kg"
        });

        Assert.True(result.ProducerCanMeetPackaging);
        Assert.True(result.ProducerCanMeetRequestedQuantity);
        Assert.True(result.BuyerMeetsMinimumTakeQuantity);
        Assert.False(result.BuyerCanAbsorbFullOffer);
        Assert.True(result.SplitShipmentCanResolveVolumeGap);
        Assert.True(result.IsMutuallyFeasible);
        Assert.Empty(result.UnresolvedConditions);
    }

    [Fact]
    public void PreviewCompatibility_WhenPackagingAndBuyerCapacityDoNotFit_ListsBothConditions()
    {
        var service = CreateService();

        var result = service.PreviewCompatibility(new DomesticGroupPurchaseSupplyCompatibilityPreviewRequest
        {
            BuyerRequiredPackagingFormCode = DomesticProducePackagingFormCodes.CorrugatedBox,
            BuyerRequestedQuantity = 500,
            BuyerMaximumAbsorptionQuantity = 100,
            ProducerSupportedPackagingFormCodes = [DomesticProducePackagingFormCodes.Bulk],
            ProducerAvailableQuantity = 1_000,
            ProducerMinimumTakeQuantity = 300,
            ProducerCanSplitShipments = false,
            QuantityUnit = "kg"
        });

        Assert.False(result.ProducerCanMeetPackaging);
        Assert.False(result.BuyerMeetsMinimumTakeQuantity);
        Assert.False(result.IsMutuallyFeasible);
        Assert.Contains(result.UnresolvedConditions, x => x.Contains("포장 형태", StringComparison.Ordinal));
        Assert.Contains(result.UnresolvedConditions, x => x.Contains("최소 인수", StringComparison.Ordinal));
    }

    private static DomesticProducerSupplyOfferDraftRequest CreateSupplyOfferRequest()
        => new()
        {
            GroupPurchaseCampaignId = Guid.NewGuid(),
            CampaignTitle = "못난이 고구마 공동구매",
            RepresentativeCandidateKey = "representative-1",
            RepresentativeMaskedDisplayName = "대표 최○○",
            ProducerMaskedDisplayName = "농가 김○○",
            ProducerRegionSummary = "전남 해남",
            ProductSummary = "규격 외 고구마",
            AvailableQuantitySummary = "10kg 상자 50개",
            SupportedPackagingFormCodes = [DomesticProducePackagingFormCodes.CorrugatedBox],
            AvailableQuantity = 500,
            MinimumTakeQuantity = 200,
            QuantityUnit = "kg",
            CanSplitShipments = true,
            ExpectedPriceSummary = "상자당 18,000원, 협의 가능",
            SupplyDeadlineSummary = "이번 주 금요일까지 출하",
            OfferReasonCode = DomesticProducerSupplyOfferReasonCodes.OffGrade,
            QualityDisclosure = "크기가 고르지 않지만 파손과 부패는 없습니다.",
            FoodSafetyConfirmed = true,
            Message = "공동구매 가능 여부를 검토해 주세요."
        };

    private static DomesticGroupPurchaseProducerConnectionService CreateService()
        => new(
            new UnconnectedCommunityProducerMemberDirectory(),
            new UnconnectedCommunityGroupPurchaseRepresentativeDirectory(),
            new InMemoryDomesticProducerContactRequestDraftStore(),
            new InMemoryDomesticProducerSupplyOfferDraftStore());
}
