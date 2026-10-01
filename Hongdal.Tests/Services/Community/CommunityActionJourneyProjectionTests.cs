using System.Text.Json;
using Hongdal.Contracts.Common.CollectiveProcurement;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.CollectiveProcurement;
using Hongdal.Services.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityActionJourneyProjectionTests
{
    [Fact]
    public void 가격계획과_가원장을_조건조율여정으로_집계하고_개인최소값은노출하지않는다()
    {
        var source = CreateSource();
        var vote = CreateInterestVote();
        var ledger = CreateLedger("provisional-1", 커뮤니티원장상태.초안, provisional: true);
        var participation = CreateParticipation(vote.Id, ledger.원장Id);
        var plan = CreatePlan(executionReady: false);

        var journey = CommunityActionJourneyProjection.Build(
            source,
            participation,
            vote,
            ledger,
            [],
            plan,
            CommunityDisplayLanguageCodes.Korean);

        Assert.Equal(CommunityActionJourneyStageCodes.Conditions, journey.CurrentStageCode);
        Assert.True(journey.Sales.HasSalesOffer);
        Assert.Equal("올리브오일", journey.Sales.ProductTitle);
        Assert.True(journey.Economics.HasPlan);
        Assert.Equal(100m, journey.Economics.MinimumViableQuantity);
        Assert.Equal(8_500m, journey.Economics.EstimatedUnitLandedCost);
        Assert.Equal("aggregate-only", journey.Economics.DisclosureLevelCode);
        Assert.False(journey.Economics.ContainsParticipantPrivateMinimums);
    }

    [Fact]
    public void 하위실행원장이_완료되면_게시글여정도_완료로읽는다()
    {
        var source = CreateSource();
        var vote = CreateInterestVote();
        var root = CreateLedger("provisional-1", 커뮤니티원장상태.초안, provisional: true);
        root.포함원장목록 =
        [
            new 커뮤니티포함원장참조Dto
            {
                원장Id = "execution-1",
                원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
                관계유형 = CommunityLedgerRelationTypes.Contains
            }
        ];
        var execution = CreateLedger("execution-1", 커뮤니티원장상태.완료, provisional: false);

        var journey = CommunityActionJourneyProjection.Build(
            source,
            CreateParticipation(vote.Id, root.원장Id),
            vote,
            root,
            [execution],
            CreatePlan(executionReady: true),
            CommunityDisplayLanguageCodes.Korean);

        Assert.Equal(CommunityActionJourneyStageCodes.Completed, journey.CurrentStageCode);
        Assert.True(journey.HasExecutionLedger);
        Assert.Contains(journey.Ledgers, ledger => ledger.LedgerId == execution.원장Id && !ledger.IsProvisional);
        Assert.Contains(journey.Timeline, item => item.LedgerId == execution.원장Id && item.IsCompleted);
    }

    [Fact]
    public void 다른목적의연결원장은_공동행동가원장으로_오인하지않는다()
    {
        var unrelatedLedger = CreateLedger("meat-readiness-71", 커뮤니티원장상태.초안, provisional: false);
        var journey = CommunityActionJourneyProjection.Build(
            CreateSource(),
            new CommunityPostParticipationEntryResponse(),
            null,
            unrelatedLedger,
            [],
            null,
            CommunityDisplayLanguageCodes.Korean);

        Assert.Equal(CommunityActionJourneyStageCodes.Conversation, journey.CurrentStageCode);
        Assert.Empty(journey.Ledgers);
        Assert.False(journey.HasExecutionLedger);
    }

    [Fact]
    public void 실제액션화면스냅샷은_서버여정의_역할과단계를우선한다()
    {
        var campaign = new CommunityVoteResponse
        {
            Id = Guid.NewGuid(),
            Title = "함께 주문",
            CreatedAtUtc = DateTime.UtcNow,
            Status = CommunityVoteStatusCodes.Open,
            TotalVoteCount = 3,
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                TotalRequestedQuantity = 30,
                MinimumTotalQuantity = 50,
                QuantityUnit = "병"
            }
        };
        var journey = new CommunityActionJourneyResponse
        {
            CurrentStageCode = CommunityActionJourneyStageCodes.Party,
            CurrentStageLabel = "함께할 사람 구성 중",
            ParticipantCount = 4,
            RequiredRoleCount = 2,
            FilledRequiredRoleCount = 1,
            RoleSlots =
            [
                new CommunityActionJourneyRoleSlotResponse
                {
                    RoleCode = CommunityPostPartyRoleCodes.Seller,
                    CategoryCode = CommunityPartyRoleCategoryCodes.CommercialParty,
                    Label = "판매자",
                    Summary = "공급 조건을 직접 제안합니다.",
                    IsRequired = true,
                    ConfirmedParticipantCount = 1
                }
            ]
        };

        var snapshot = CommunityCollectiveActionSnapshotFactory.FromCampaign(campaign, journey);

        Assert.Equal(CommunityCollectiveActionPageKeys.Party, snapshot.CurrentPageKey);
        Assert.Equal(4, snapshot.ParticipantCount);
        Assert.Single(snapshot.RoleSlots);
        Assert.True(snapshot.RoleSlots[0].Accepted);
        Assert.Equal("판매자", snapshot.RoleSlots[0].RoleLabel);
    }

    [Fact]
    public async Task 메모리계획저장소는_원천식별자로_최신순조회한다()
    {
        var store = new InMemoryCollectiveProcurementPlanningStore();
        var older = CreatePlan(executionReady: false);
        older.PlanId = Guid.NewGuid();
        older.UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        var latest = CreatePlan(executionReady: false);
        latest.PlanId = Guid.NewGuid();
        latest.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await store.CreateAsync(older);
        await store.CreateAsync(latest);

        var plans = await store.ListBySourceAsync("community-interest-vote", "vote-1");

        Assert.Equal([latest.PlanId, older.PlanId], plans.Select(plan => plan.PlanId));
    }

    private static CommunityPostOpportunitySource CreateSource()
        => new(
            71,
            "platform",
            "올리브오일을 같이 주문해요",
            "수량과 가격을 함께 확인합니다.",
            "author-1",
            "provisional-1",
            SalesOfferJson: JsonSerializer.Serialize(new PlatformCommunityPostSalesOfferResponse
            {
                ProductTitle = "올리브오일",
                AvailableQuantity = 200,
                QuantityUnit = "병",
                UnitPrice = 10_000,
                CurrencyCode = "KRW",
                AllowsGroupPurchase = true,
                Status = PlatformCommunitySalesOfferStatuses.Open
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAtUtc: DateTime.UtcNow.AddHours(-2));

    private static CommunityVoteResponse CreateInterestVote()
        => new()
        {
            Id = Guid.NewGuid(),
            VoteKind = CommunityVoteKindCodes.CollectiveActionInterest,
            Status = CommunityVoteStatusCodes.Open,
            SourcePostId = 71,
            CommunityLedgerId = "provisional-1",
            TotalVoteCount = 3,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

    private static CommunityPostParticipationEntryResponse CreateParticipation(Guid voteId, string ledgerId)
        => new()
        {
            InterestVoteId = voteId,
            ProvisionalLedgerId = ledgerId,
            ParticipantCount = 3,
            PlanningSourceTypeCode = "community-interest-vote",
            PlanningSourceReferenceId = "vote-1",
            PartyFormation = new CommunityPostPartyFormationResponse
            {
                IsAvailable = true,
                RequiredRoleSlotCount = 2,
                RepresentedRequiredRoleSlotCount = 0
            }
        };

    private static 커뮤니티원장Dto CreateLedger(string id, string state, bool provisional)
        => new()
        {
            원장Id = id,
            원장템플릿Key = provisional
                ? CommunityLedgerTemplateKeys.GroupPurchase
                : CommunityLedgerTemplateKeys.CargoTransport,
            제목 = id,
            상태 = state,
            생성시각Utc = DateTime.UtcNow.AddMinutes(-30),
            수정시각Utc = DateTime.UtcNow,
            확장속성 = provisional
                ? new Dictionary<string, string>
                {
                    [CommunityPostProvisionalLedgerPolicy.LedgerMaturityAttributeKey] =
                        CommunityPostProvisionalLedgerPolicy.LedgerMaturityCode
                }
                : new Dictionary<string, string>()
        };

    private static CollectiveProcurementPlanState CreatePlan(bool executionReady)
    {
        var calculationRevision = 2;
        return new CollectiveProcurementPlanState
        {
            PlanId = Guid.NewGuid(),
            PlanRevision = 3,
            SourceTypeCode = "community-interest-vote",
            SourceReferenceId = "vote-1",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Participants =
            [
                new CollectiveProcurementPlanParticipantState
                {
                    UserId = "buyer-1",
                    AcceptedCalculationRevision = executionReady ? calculationRevision : null
                }
            ],
            CalculationRevisions =
            [
                new CollectiveProcurementCalculationRevisionState
                {
                    CalculationRevision = calculationRevision,
                    Assessment = new CollectiveProcurementAssessmentResponse
                    {
                        CurrencyCode = "KRW",
                        QuantityUnit = "병",
                        CurrentCommittedQuantity = 80,
                        MinimumOrderQuantity = 100,
                        MinimumViableQuantity = 100,
                        RecommendedQuantity = 150,
                        CurrentQuantityEconomicallyViable = executionReady,
                        BenefitAgreementReady = executionReady,
                        RecommendedScenario = new CollectiveProcurementQuantityScenarioResponse
                        {
                            Quantity = 150,
                            EstimatedUnitLandedCost = 8_500
                        }
                    }
                }
            ]
        };
    }
}
