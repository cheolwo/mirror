using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Hosting.Controllers;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationTurnClosingTests
{
    private const string FoolCard = "learning:hongik.fool.beginner-mind";
    private const string SeoulCultureCard = "culture:kr-seoul.living-culture-question.2026";

    [Fact]
    public void TURN0_마감Context는현재경영일과검수된학당카드를조회하지만상태를바꾸지않는다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());

        var context = service.GetTurnClosingContext(session.SessionStableId);
        var unchanged = service.Get(session.SessionStableId);

        Assert.Equal(1, context.TurnNumber);
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), context.GameDate);
        Assert.True(context.CanCloseTurn);
        Assert.Equal(3, context.AvailableCards.Length);
        Assert.Equal(3, context.TarotDraw.Offers.Length);
        var journeyRoot = context.TarotContext.FrameSet.JourneyRoot;
        Assert.Equal(SimulationTarotJourneyRootCodes.FoolCardStableId,
            journeyRoot.CardStableId);
        Assert.Equal(SimulationTarotJourneyRootCodes.TraditionalArcanaNumber,
            journeyRoot.TraditionalArcanaNumber);
        Assert.Equal(SimulationTarotJourneyRootCodes.JourneySequenceOrder,
            journeyRoot.JourneySequenceOrder);
        Assert.Equal(SimulationTarotMetaLayerCodes.JourneyRoot,
            journeyRoot.MetaLayerCode);
        Assert.Equal(SimulationCardHierarchyTierCodes.Meta,
            journeyRoot.HierarchyTierCode);
        Assert.True(journeyRoot.IsAlwaysActive);
        Assert.Empty(context.TarotContext.FrameSet.ActiveFrames);
        Assert.All(context.AvailableCards.Where(card =>
            card.CardKindCode == SimulationTurnCardKindCodes.Philosophy), card =>
        {
            Assert.Equal(SimulationTurnCardKindCodes.Philosophy, card.CardKindCode);
            Assert.Equal(SimulationTurnCardEffectTimingCodes.NextTurn, card.EffectTimingCode);
            Assert.StartsWith("source:", card.SourceStableId);
        });
        Assert.Equal(0, unchanged.CurrentTick);
        Assert.Equal(0, unchanged.Revision);
    }

    [Fact]
    public void CULTURE_CARD0_문화카드는지역기간출처달력과효과규칙을모두보존한다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());

        var card = Assert.Single(
            service.GetTurnClosingContext(session.SessionStableId).AvailableCards,
            value => value.CardStableId == SeoulCultureCard);

        Assert.Equal(SimulationTurnCardKindCodes.Culture, card.CardKindCode);
        Assert.Equal("kr-seoul", card.RegionKey);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            card.AvailableFromGameDate);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            card.AvailableThroughGameDate);
        Assert.Equal("simulation-culture-calendar:kr-seoul:2026.r1", card.CalendarRevision);
        Assert.Equal("culture-local-context-awareness:r1", card.EffectRuleRevision);
        Assert.Equal("source:kr-regional-culture-promotion-agency", card.SourceStableId);
        Assert.StartsWith("https://www.mcst.go.kr/", card.SourceUrl, StringComparison.Ordinal);
        Assert.NotNull(card.EvidenceCheckedAtUtc);
    }

    [Fact]
    public void CULTURE_CARD0_근거가불완전한문화카드는catalog진입전에거부한다()
    {
        var error = Assert.Throws<SimulationContractException>(() =>
            경영SimulationSessionAggregate.ValidateCultureCard(new SimulationTurnCardSnapshot
            {
                CardKindCode = SimulationTurnCardKindCodes.Culture,
                RegionKey = "kr-seoul",
                SourceStableId = "source:incomplete",
            }));

        Assert.Equal("SimulationCultureTurnCardProvenanceInvalid", error.ErrorCode);
    }

    [Fact]
    public void CULTURE_CARD0_Confirm은문화카드근거를다음턴효과와saveReplay에보존한다()
    {
        var saveStore = new InMemorySimulationSessionSaveStore();
        var source = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(), saveStore);
        var session = source.Create(CreateRequest());
        var request = PreviewRequest(0, SeoulCultureCard);
        var closed = source.ConfirmTurnClosing(
            session.SessionStableId,
            ConfirmRequest("command:culture-card.close-1", 0, request));
        var saved = source.Save(session.SessionStableId, new SimulationSessionSaveRequest
        {
            SaveStableId = "save:culture-card.close-1",
            ExpectedRevision = closed.Revision,
        });
        var target = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(), saveStore);

        var restored = target.Restore(new SimulationSessionRestoreRequest
        {
            SaveStableId = saved.SaveStableId,
        });

        var active = Assert.Single(restored.Session.ActiveTurnCardEffects);
        Assert.Equal(SeoulCultureCard, active.CardStableId);
        Assert.Equal(SimulationTurnCardEffectCodes.LocalContextAwareness, active.EffectCode);
        Assert.Equal("CommunityInsight", active.TargetStatCode);
        Assert.Equal("kr-seoul", active.RegionKey);
        Assert.Equal("simulation-culture-calendar:kr-seoul:2026.r1", active.CalendarRevision);
        Assert.Equal("culture-local-context-awareness:r1", active.EffectRuleRevision);
        Assert.StartsWith("https://www.mcst.go.kr/", active.SourceUrl, StringComparison.Ordinal);
        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
        Assert.Equal(SimulationSaveSchemaVersions.V7, saved.SchemaVersion);
        Assert.Equal(1, restored.Session.RegionalCausality.RecoveryScore);
    }

    [Fact]
    public void TURN0_카드없이마감해도명시적Confirm에서만다음날로진행한다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());
        var previewRequest = PreviewRequest(session.Revision);

        var preview = service.PreviewTurnClosing(session.SessionStableId, previewRequest);
        var beforeConfirm = service.Get(session.SessionStableId);
        var next = service.ConfirmTurnClosing(
            session.SessionStableId,
            ConfirmRequest("command:turn.close-without-card", session.Revision, previewRequest));

        Assert.Equal(0, beforeConfirm.CurrentTick);
        Assert.Equal(1, preview.ClosingTurnNumber);
        Assert.Equal(2, preview.NextTurnNumber);
        Assert.Empty(preview.SelectedCards);
        Assert.Equal(1, next.CurrentTick);
        Assert.Equal(new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero), next.WorldContext.GameDate);
        var closing = Assert.Single(next.TurnClosings);
        Assert.Equal(1, closing.ClosedTurnNumber);
        Assert.Empty(closing.SelectedCards);
        Assert.Empty(next.ActiveTurnCardEffects);
    }

    [Fact]
    public void TURN0_철학카드는끝난날을바꾸지않고다음경영일에만활성화된다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());
        var previewRequest = PreviewRequest(session.Revision, FoolCard);

        var preview = service.PreviewTurnClosing(session.SessionStableId, previewRequest);
        var next = service.ConfirmTurnClosing(
            session.SessionStableId,
            ConfirmRequest("command:turn.close-with-fool", session.Revision, previewRequest));

        Assert.Equal(SimulationTurnCardEffectCodes.BeginnerMind,
            Assert.Single(preview.SelectedCards).EffectCode);
        var closing = Assert.Single(next.TurnClosings);
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), closing.ClosedGameDate);
        var active = Assert.Single(next.ActiveTurnCardEffects);
        Assert.Equal(FoolCard, active.CardStableId);
        Assert.Equal(2, active.ActiveTurnNumber);
        Assert.Equal("Awareness", active.TargetStatCode);
        Assert.Equal(1, active.StatDelta);
        Assert.Equal(closing.TurnClosingStableId, active.SourceTurnClosingStableId);
        Assert.Equal(1, next.RegionalCausality.RecoveryScore);
        Assert.Equal(0, next.RegionalCausality.ThreatScore);
        Assert.Equal(SimulationRegionalIncidentCodes.OpportunityOutcome,
            next.RegionalCausality.OutcomeCode);
        Assert.Contains(next.RegionalCausality.Changes, value =>
            value.SourceCode == SimulationRegionalIncidentCodes.PositiveTurnCard);
    }

    [Fact]
    public void TURN0_역방향타로카드는_지역위협점수를더한다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());
        var offer = service.GetTurnClosingContext(session.SessionStableId)
            .TarotDraw.Offers.First(value => value.OrientationCode ==
                Simulation타로카드방향Codes.Reversed);
        var preview = new SimulationTurnClosingPreviewRequest
        {
            ExpectedRevision = session.Revision,
            SelectedTarotCard = new Simulation타로CardSelectionRequest
            {
                OfferStableId = offer.OfferStableId,
                CardStableId = offer.Card.CardStableId,
                OrientationCode = offer.OrientationCode,
            },
        };

        var next = service.ConfirmTurnClosing(session.SessionStableId,
            ConfirmRequest("command:turn.reversed-regional-threat", 0, preview));

        Assert.Equal(1, next.RegionalCausality.ThreatScore);
        Assert.Equal(0, next.RegionalCausality.RecoveryScore);
        Assert.Equal(SimulationRegionalIncidentCodes.ThreatOutcome,
            next.RegionalCausality.OutcomeCode);
        Assert.Contains(next.RegionalCausality.Changes, value =>
            value.SourceCode == SimulationRegionalIncidentCodes.ReversedTurnCard
            && value.SourceStableId == offer.OfferStableId);
    }

    [Fact]
    public void TURN0_타로는_Frame과Proposal을만들지만_Incident와Effect를자동생성하지않는다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());
        var offer = service.GetTurnClosingContext(session.SessionStableId)
            .TarotDraw.Offers.First(value => value.OrientationCode ==
                Simulation타로카드방향Codes.Upright);
        var preview = new SimulationTurnClosingPreviewRequest
        {
            ExpectedRevision = 0,
            SelectedTarotCard = new Simulation타로CardSelectionRequest
            {
                OfferStableId = offer.OfferStableId,
                CardStableId = offer.Card.CardStableId,
                OrientationCode = offer.OrientationCode,
            },
        };

        var next = service.ConfirmTurnClosing(session.SessionStableId,
            ConfirmRequest("command:turn.tarot-context", 0, preview));
        var frame = Assert.Single(next.TarotContext.FrameSet.ActiveFrames);
        var journeyRoot = next.TarotContext.FrameSet.JourneyRoot;
        var proposal = Assert.Single(next.TarotContext.Proposals);
        var evaluation = Assert.Single(next.TarotContext.IncidentEvaluations);

        Assert.Equal(SimulationTarotFrameScopeCodes.Turn, frame.FrameScopeCode);
        Assert.Equal(journeyRoot.FrameStableId,
            frame.ParentJourneyFrameStableId);
        Assert.Equal(SimulationTarotMetaLayerCodes.ActiveMajorArcana,
            frame.MetaLayerCode);
        Assert.Equal(SimulationTarotJourneyRootCodes.FoolCardStableId,
            journeyRoot.CardStableId);
        Assert.Equal(frame.FrameStableId, proposal.SourceFrameStableId);
        Assert.Equal(SimulationTarotIncidentEvaluationResultCodes.NoIncident,
            evaluation.EvaluationResultCode);
        Assert.Empty(evaluation.IncidentStableId);
        Assert.Empty(evaluation.EffectStableIds);
        Assert.All(next.TarotContext.Relations,
            relation => Assert.False(relation.ChangesAvailability));
        Assert.NotEmpty(next.TarotContext.FrameSet.FrameSetHashSha256);
        Assert.NotEmpty(next.TarotContext.ContextStateHashSha256);
    }

    [Fact]
    public void TURN0_같은Command는멱등하고다른카드payload와staleRevision은거부한다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());
        var preview = PreviewRequest(session.Revision, FoolCard);
        var command = ConfirmRequest("command:turn.idempotent", session.Revision, preview);

        var first = service.ConfirmTurnClosing(session.SessionStableId, command);
        var retry = service.ConfirmTurnClosing(session.SessionStableId, command);
        var payloadConflict = Assert.Throws<SimulationConflictException>(() =>
            service.ConfirmTurnClosing(
                session.SessionStableId,
                ConfirmRequest("command:turn.idempotent", 0, PreviewRequest(0))));
        var stale = Assert.Throws<SimulationConflictException>(() =>
            service.PreviewTurnClosing(session.SessionStableId, PreviewRequest(0)));

        Assert.Equal(first.Revision, retry.Revision);
        Assert.Single(retry.TurnClosings);
        Assert.Equal("SimulationCommandPayloadConflict", payloadConflict.ErrorCode);
        Assert.Equal("SimulationExpectedRevisionMismatch", stale.ErrorCode);
    }

    [Fact]
    public void TURN0_미게시카드와두장이상선택은거부한다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());

        var unavailable = Assert.Throws<SimulationConflictException>(() =>
            service.PreviewTurnClosing(
                session.SessionStableId,
                PreviewRequest(0, "learning:invented.card")));
        var limit = Assert.Throws<SimulationContractException>(() =>
            service.PreviewTurnClosing(
                session.SessionStableId,
                PreviewRequest(0, FoolCard, "learning:hongik.chariot.integrated-progress")));

        Assert.Equal("SimulationTurnCardUnavailable", unavailable.ErrorCode);
        Assert.Equal("SimulationTurnCardSelectionLimitExceeded", limit.ErrorCode);
    }

    [Fact]
    public void TURN0_턴마감은예약된경영업무를같은WorldTick규칙으로해결한다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());
        var scheduled = service.ConfirmDecision(
            session.SessionStableId,
            new SimulationDecisionConfirmRequest
            {
                CommandId = "command:turn.schedule-work",
                ExpectedRevision = 0,
                Preview = new SimulationDecisionPreviewRequest
                {
                    DecisionStableId = "decision:turn.work-1",
                    DecisionTypeCode = "TurnWork",
                    ActorStableId = "actor:sim.manager-1",
                    TargetStableIds = new[] { "facility:sim.market-1" },
                    ExpectedEffects = new[]
                    {
                        new SimulationValueProjection
                        {
                            ValueTypeCode = "WorkCompleted",
                            TargetLedgerStableId = "ledger:sim.turn-work-1",
                            BeforeValue = 0,
                            Delta = 1,
                            AfterValue = 1,
                            UnitCode = "TASK",
                            SourceStableIds = new[] { "source:fixture.turn-work-r1" },
                        },
                    },
                    SourceStableIds = new[] { "source:fixture.turn-work-r1" },
                    Task = new SimulationTaskPlanRequest
                    {
                        TaskStableId = "task:turn.work-1",
                        TaskTypeCode = "TurnWork",
                        FacilityStableId = "facility:sim.market-1",
                        AssignedCapacity = 1,
                        AssignedCapacityUnitCode = "TASK",
                        DurationTicks = 1,
                        InputLotStableIds = new[] { "work-item:sim.turn-work-1" },
                        SourceStableIds = new[] { "source:fixture.turn-work-r1" },
                    },
                },
            });
        var context = service.GetTurnClosingContext(session.SessionStableId);

        var next = service.ConfirmTurnClosing(
            session.SessionStableId,
            ConfirmRequest(
                "command:turn.resolve-work",
                scheduled.Revision,
                PreviewRequest(scheduled.Revision)));

        Assert.Equal(1, context.PendingTaskCount);
        Assert.Equal(SimulationTaskStateCodes.Completed, Assert.Single(next.Tasks).StateCode);
        Assert.Equal(1, next.CurrentTick);
    }

    [Fact]
    public void TURN0_턴마감은saveReplay에서카드와결과를동일하게복원한다()
    {
        var saveStore = new InMemorySimulationSessionSaveStore();
        var source = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(), saveStore);
        var session = source.Create(CreateRequest());
        var preview = PreviewRequest(0, FoolCard);
        var closed = source.ConfirmTurnClosing(
            session.SessionStableId,
            ConfirmRequest("command:turn.save-replay", 0, preview));
        var saved = source.Save(session.SessionStableId, new SimulationSessionSaveRequest
        {
            SaveStableId = "save:turn.close-1",
            ExpectedRevision = closed.Revision,
        });

        var target = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(), saveStore);
        var restored = target.Restore(new SimulationSessionRestoreRequest
        {
            SaveStableId = saved.SaveStableId,
        });

        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
        Assert.Equal(SimulationCommandTypeCodes.TurnClosingConfirm,
            Assert.Single(saved.CommandLog).CommandTypeCode);
        Assert.NotNull(saved.CommandLog[0].TurnClosingConfirmRequest);
        Assert.Equal(FoolCard,
            Assert.Single(restored.Session.ActiveTurnCardEffects).CardStableId);
        Assert.Equal(SimulationTarotJourneyRootCodes.FoolCardStableId,
            restored.Session.TarotContext.FrameSet.JourneyRoot.CardStableId);
        Assert.True(restored.Session.TarotContext.FrameSet.JourneyRoot.IsAlwaysActive);
        Assert.Equal(1, restored.Session.CurrentTick);
        Assert.Single(restored.Session.TurnClosings);
    }

    [Fact]
    public void TURN0_Controller는마감ContextPreviewConfirm경계를노출한다()
    {
        var service = Service();
        var session = service.Create(CreateRequest());
        var controller = new 경영SimulationSessionsController(service);
        var previewRequest = PreviewRequest(0, FoolCard);

        var context = Assert.IsType<OkObjectResult>(
            controller.GetTurnClosingContext(session.SessionStableId).Result);
        var preview = Assert.IsType<OkObjectResult>(
            controller.PreviewTurnClosing(session.SessionStableId, previewRequest).Result);
        var confirm = Assert.IsType<OkObjectResult>(
            controller.ConfirmTurnClosing(
                session.SessionStableId,
                ConfirmRequest("command:turn.controller", 0, previewRequest)).Result);

        Assert.IsType<SimulationTurnClosingContextSnapshot>(context.Value);
        Assert.IsType<SimulationTurnClosingPreviewSnapshot>(preview.Value);
        Assert.IsType<경영SimulationSessionSnapshot>(confirm.Value);
    }

    private static 경영SimulationSessionService Service()
        => new(
            new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore());

    private static SimulationTurnClosingPreviewRequest PreviewRequest(
        long revision,
        params string[] cardStableIds)
        => new()
        {
            ExpectedRevision = revision,
            SelectedCardStableIds = cardStableIds,
        };

    private static SimulationTurnClosingConfirmRequest ConfirmRequest(
        string commandId,
        long revision,
        SimulationTurnClosingPreviewRequest preview)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            Preview = preview,
        };

    private static 경영SimulationSession생성Request CreateRequest()
        => new()
        {
            ClientRequestId = Guid.Parse("706a236b-17e5-44e2-a070-a0785ae42d19"),
            ScenarioStableId = "scenario:turn-closing-potato-4w",
            ScenarioDataRevision = "simulation-data:turn-closing:1",
            ScenarioSeed = 240811,
            RuleRevision = "turn-closing-rule:1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim.borderland-1",
                TerritoryStableId = "territory:sim.borderland-1",
                SettlementStableId = "settlement:sim.border-town-1",
                GameDateStartsOn = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            },
        };
}
