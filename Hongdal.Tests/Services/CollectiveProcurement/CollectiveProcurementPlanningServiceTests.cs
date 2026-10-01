using Hongdal.Contracts.Common.CollectiveProcurement;
using Hongdal.Services.CollectiveProcurement;

namespace Hongdal.Tests.Services.CollectiveProcurement;

public sealed class CollectiveProcurementPlanningServiceTests
{
    [Fact]
    public void 계산엔진은_최소경제물량과_단가가가장낮은권장물량을_구분한다()
    {
        var engine = new CollectiveProcurementEconomicsEngine();

        var result = engine.Evaluate(CreateAssessment(currentQuantity: 600m, potentialQuantity: 1_000m), UtcNow());

        Assert.Equal(1_000m, result.MinimumViableQuantity);
        Assert.Equal(2_000m, result.RecommendedQuantity);
        Assert.Equal(400m, result.AdditionalQuantityToMinimumViable);
        Assert.Equal(1_400m, result.AdditionalQuantityToRecommended);
        Assert.False(result.CurrentQuantityEconomicallyViable);
        Assert.True(result.CurrentPotentialScenario!.EconomicallyViable);
        Assert.Equal(9_000m, result.BenefitPoolAmount);
        Assert.Contains(result.Warnings, warning => warning.Contains("최소 1000 kg", StringComparison.Ordinal));
    }

    [Fact]
    public void 합의한집중상한을_한참여자가넘으면_편익합의를준비완료로보지않는다()
    {
        var engine = new CollectiveProcurementEconomicsEngine();
        var request = CreateAssessment();
        request.MaximumSingleParticipantBenefitSharePercent = 80m;
        request.BenefitPositions =
        [
            Position("buyer-group", 8_000m, 1_000m, CollectiveProcurementBenefitKindCodes.BuyerSavings),
            Position("supplier", 500m, 300m, CollectiveProcurementBenefitKindCodes.SupplierBenefit)
        ];

        var result = engine.Evaluate(request, UtcNow());

        Assert.True(result.AllocationWithinBenefitPool);
        Assert.True(result.AllParticipantsMeetPrivateMinimums);
        Assert.False(result.BenefitConcentrationWithinAgreedPolicy);
        Assert.False(result.BenefitAgreementReady);
        Assert.Contains(result.Warnings, warning => warning.Contains("집중 상한", StringComparison.Ordinal));
    }

    [Fact]
    public void 계산구간이_허용범위를넘으면_정수변환전에_명확히거부한다()
    {
        var engine = new CollectiveProcurementEconomicsEngine();
        var request = CreateAssessment();
        request.MaximumSafeQuantity = decimal.MaxValue;
        request.QuantityIncrement = 0.0001m;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => engine.Evaluate(request, UtcNow()));

        Assert.Contains("10,000", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 전원이현재리비전에동의하기전에는_다른참여자의정확한편익을숨긴다()
    {
        var service = CreateService();
        var created = await service.CreateAsync(CreatePlanRequest(), "buyer-user");

        var buyer = Assert.Single(created.Participants, participant => participant.IsCurrentViewer);
        var supplierFromBuyer = Assert.Single(created.Participants, participant => participant.ReferenceCode == "supplier");
        Assert.Equal(2_500m, buyer.ProposedBenefitAmount);
        Assert.Equal(1_000m, buyer.MinimumAcceptableBenefitAmount);
        Assert.Null(supplierFromBuyer.ProposedBenefitAmount);
        Assert.Null(supplierFromBuyer.MinimumAcceptableBenefitAmount);

        var supplierView = await service.GetAsync(created.PlanId, "supplier-user");
        var supplier = Assert.Single(supplierView!.Participants, participant => participant.IsCurrentViewer);
        var buyerFromSupplier = Assert.Single(supplierView.Participants, participant => participant.ReferenceCode == "buyer-group");
        Assert.Equal(1_000m, supplier.ProposedBenefitAmount);
        Assert.Equal(500m, supplier.MinimumAcceptableBenefitAmount);
        Assert.Null(buyerFromSupplier.ProposedBenefitAmount);

        var buyerConsent = await service.UpdateDisclosureConsentAsync(
            created.PlanId,
            Consent(created.PlanRevision, created.CurrentCalculationRevision),
            "buyer-user");
        Assert.False(buyerConsent.ExactBenefitDisclosureActive);

        var mutualConsent = await service.UpdateDisclosureConsentAsync(
            created.PlanId,
            Consent(buyerConsent.PlanRevision, buyerConsent.CurrentCalculationRevision),
            "supplier-user");

        Assert.True(mutualConsent.ExactBenefitDisclosureActive);
        var disclosedBuyer = Assert.Single(mutualConsent.Participants, participant => participant.ReferenceCode == "buyer-group");
        Assert.Equal(2_500m, disclosedBuyer.ProposedBenefitAmount);
        Assert.Null(disclosedBuyer.MinimumAcceptableBenefitAmount);
    }

    [Fact]
    public async Task 모든참여자가현재리비전을수락하면_실행준비상태가된다()
    {
        var service = CreateService();
        var created = await service.CreateAsync(CreatePlanRequest(), "buyer-user");

        var buyerAccepted = await service.AcceptRevisionAsync(
            created.PlanId,
            Acceptance(created.PlanRevision, created.CurrentCalculationRevision),
            "buyer-user");
        Assert.False(buyerAccepted.AllParticipantsAcceptedCurrentRevision);

        var supplierAccepted = await service.AcceptRevisionAsync(
            created.PlanId,
            Acceptance(buyerAccepted.PlanRevision, buyerAccepted.CurrentCalculationRevision),
            "supplier-user");

        Assert.True(supplierAccepted.AllParticipantsAcceptedCurrentRevision);
        Assert.True(supplierAccepted.ExecutionReady);
        Assert.Equal(CollectiveProcurementPlanStatusCodes.ReadyForExecution, supplierAccepted.StatusCode);
    }

    [Fact]
    public async Task 새계산리비전은_이전의공개의동의와수락을승계하지않는다()
    {
        var service = CreateService();
        var state = await service.CreateAsync(CreatePlanRequest(), "buyer-user");
        state = await service.UpdateDisclosureConsentAsync(
            state.PlanId,
            Consent(state.PlanRevision, state.CurrentCalculationRevision),
            "buyer-user");
        state = await service.UpdateDisclosureConsentAsync(
            state.PlanId,
            Consent(state.PlanRevision, state.CurrentCalculationRevision),
            "supplier-user");
        state = await service.AcceptRevisionAsync(
            state.PlanId,
            Acceptance(state.PlanRevision, state.CurrentCalculationRevision),
            "buyer-user");
        state = await service.AcceptRevisionAsync(
            state.PlanId,
            Acceptance(state.PlanRevision, state.CurrentCalculationRevision),
            "supplier-user");
        Assert.True(state.ExactBenefitDisclosureActive);
        Assert.True(state.AllParticipantsAcceptedCurrentRevision);

        var recalculated = await service.RecalculateAsync(
            state.PlanId,
            new RecalculateCollectiveProcurementPlanRequest
            {
                ExpectedPlanRevision = state.PlanRevision,
                Assessment = CreateAssessment(currentQuantity: 1_200m, potentialQuantity: 1_500m)
            },
            "buyer-user");

        Assert.Equal(2, recalculated.CurrentCalculationRevision);
        Assert.False(recalculated.ExactBenefitDisclosureActive);
        Assert.False(recalculated.AllParticipantsAcceptedCurrentRevision);
        Assert.All(recalculated.Participants, participant =>
        {
            Assert.False(participant.HasExactDisclosureConsentForCurrentRevision);
            Assert.False(participant.HasAcceptedCurrentRevision);
        });
    }

    [Fact]
    public async Task 최신계획리비전이아니면_동의요청을거부한다()
    {
        var service = CreateService();
        var created = await service.CreateAsync(CreatePlanRequest(), "buyer-user");
        await service.UpdateDisclosureConsentAsync(
            created.PlanId,
            Consent(created.PlanRevision, created.CurrentCalculationRevision),
            "buyer-user");

        await Assert.ThrowsAsync<CollectiveProcurementPlanConcurrencyException>(() =>
            service.UpdateDisclosureConsentAsync(
                created.PlanId,
                Consent(created.PlanRevision, created.CurrentCalculationRevision),
                "supplier-user"));
    }

    private static CollectiveProcurementPlanningService CreateService()
        => new(
            new InMemoryCollectiveProcurementPlanningStore(),
            new CollectiveProcurementEconomicsEngine(),
            new FakeClock(UtcNow()));

    private static CreateCollectiveProcurementPlanRequest CreatePlanRequest()
        => new()
        {
            Title = "미국산 식품 공동조달 경제성 검토",
            SourceTypeCode = "community-post",
            SourceReferenceId = "71",
            Participants =
            [
                new CollectiveProcurementParticipantRequest
                {
                    ReferenceCode = "buyer-group",
                    UserId = "buyer-user",
                    DisplayName = "공동구매 참여자",
                    RoleCode = "buyer-group"
                },
                new CollectiveProcurementParticipantRequest
                {
                    ReferenceCode = "supplier",
                    UserId = "supplier-user",
                    DisplayName = "해외 공급자",
                    RoleCode = "supplier"
                }
            ],
            Assessment = CreateAssessment()
        };

    private static CollectiveProcurementAssessmentRequest CreateAssessment(
        decimal currentQuantity = 1_000m,
        decimal potentialQuantity = 1_000m)
        => new()
        {
            CurrencyCode = "USD",
            QuantityUnit = "kg",
            CurrentCommittedQuantity = currentQuantity,
            CurrentPotentialQuantity = potentialQuantity,
            MinimumOrderQuantity = 500m,
            MaximumSafeQuantity = 2_000m,
            QuantityIncrement = 100m,
            ComparisonUnitPrice = 15m,
            TargetSavingsPercent = 10m,
            RiskReservePercent = 0m,
            MaximumSingleParticipantBenefitSharePercent = 80m,
            CandidateQuantities = [500m, 1_000m, 1_500m, 2_000m],
            SupplierPriceTiers =
            [
                new CollectiveProcurementSupplierPriceTierRequest
                {
                    Label = "기본 공급가",
                    MinimumQuantity = 0m,
                    UnitPrice = 10m,
                    SourceReference = "supplier-quote-1"
                },
                new CollectiveProcurementSupplierPriceTierRequest
                {
                    Label = "대량 공급가",
                    MinimumQuantity = 1_000m,
                    UnitPrice = 8m,
                    SourceReference = "supplier-quote-1"
                }
            ],
            CostComponents =
            [
                new CollectiveProcurementCostComponentRequest
                {
                    Code = "clearance",
                    Label = "통관·고정 처리비",
                    CategoryCode = "customs",
                    ModelCode = CollectiveProcurementCostModelCodes.Fixed,
                    Amount = 2_000m
                },
                new CollectiveProcurementCostComponentRequest
                {
                    Code = "freight-block",
                    Label = "운송 적재 구간비",
                    CategoryCode = "international-freight",
                    ModelCode = CollectiveProcurementCostModelCodes.CapacityStep,
                    Amount = 1_500m,
                    CapacityQuantity = 1_000m
                }
            ],
            BenefitPositions =
            [
                Position("buyer-group", 2_500m, 1_000m, CollectiveProcurementBenefitKindCodes.BuyerSavings),
                Position("supplier", 1_000m, 500m, CollectiveProcurementBenefitKindCodes.SupplierBenefit)
            ]
        };

    private static CollectiveProcurementBenefitPositionRequest Position(
        string participantReferenceCode,
        decimal proposedBenefit,
        decimal minimumBenefit,
        string benefitKindCode)
        => new()
        {
            ParticipantReferenceCode = participantReferenceCode,
            BenefitKindCode = benefitKindCode,
            ProposedBenefitAmount = proposedBenefit,
            MinimumAcceptableBenefitAmount = minimumBenefit
        };

    private static UpdateCollectiveProcurementDisclosureConsentRequest Consent(
        long planRevision,
        int calculationRevision)
        => new()
        {
            ExpectedPlanRevision = planRevision,
            CalculationRevision = calculationRevision,
            AllowExactBenefitDisclosure = true,
            ConfirmCommercialInformationHandling = true,
            ConfirmIndependentPricingDecision = true
        };

    private static AcceptCollectiveProcurementRevisionRequest Acceptance(
        long planRevision,
        int calculationRevision)
        => new()
        {
            ExpectedPlanRevision = planRevision,
            CalculationRevision = calculationRevision,
            ConfirmValuesAreEstimates = true,
            ConfirmIndependentDecision = true
        };

    private static DateTimeOffset UtcNow()
        => new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock : ICollectiveProcurementPlanningClock
    {
        public FakeClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
