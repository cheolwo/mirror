using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Hosting.Controllers;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "메이저 아르카나 활성화 방향과 Town NPC 생활복구 폐루프를 검증한다.",
    Boundary = "자동 시험은 실제 Unity Game View와 운영 상품 연동 증거를 대신하지 않는다.")]
public sealed class SimulationArcanaTownNpcLifeTests
{
    [Theory]
    [InlineData("51", "49", Simulation타로카드방향Codes.Upright, 510000L)]
    [InlineData("50.9999", "49.0001", Simulation타로카드방향Codes.Reversed, 509999L)]
    public void 동일카드도활성화시점회복비중에따라방향이달라진다(
        string recovery, string threat, string expectedDirection,
        long expectedRecoveryShareMicro)
    {
        var service = Service(out _);
        var session = service.Create(CreateRequest(
            Guid.NewGuid(), decimal.Parse(recovery), decimal.Parse(threat)));
        var (previewRequest, preview) = PreviewFirstArcana(service, session);

        Assert.Equal(expectedDirection,
            preview.MajorArcanaDirectionDecision!.DirectionCode);
        Assert.Equal(expectedRecoveryShareMicro,
            preview.MajorArcanaDirectionDecision.RecoveryShareMicro);

        var confirmed = service.ConfirmTurnClosing(session.SessionStableId,
            Confirm("command:arcana:orientation", session.Revision, previewRequest));
        var activation = Assert.Single(
            confirmed.TarotContext.MajorArcanaActivations);

        Assert.Equal(preview.SelectedCards[0].CardStableId,
            activation.Selection.CardStableId);
        Assert.Equal(expectedDirection,
            activation.OrientationDecision.DirectionCode);
        Assert.Equal(Simulation메이저아르카나활성상태Codes.Active,
            activation.StateCode);
    }

    [Fact]
    public void 활성화방향은Tick중재판정되지않고하위카드에한번만FanOut된다()
    {
        var service = Service(out _);
        var session = service.Create(CreateRequest(Guid.NewGuid(), 57m, 43m));
        var (previewRequest, _) = PreviewFirstArcana(service, session);
        var activated = service.ConfirmTurnClosing(session.SessionStableId,
            Confirm("command:arcana:activate", session.Revision, previewRequest));
        var activation = Assert.Single(
            activated.TarotContext.MajorArcanaActivations);

        var advanced = service.Advance(session.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:arcana:advance",
                ExpectedRevision = activated.Revision,
                TickCount = 3,
            });
        var activeAfterTicks = Assert.Single(
            advanced.TarotContext.MajorArcanaActivations);
        var inheritances = advanced.TarotContext.OrientationInheritances;

        Assert.Equal(activation.MajorArcanaActivationStableId,
            activeAfterTicks.MajorArcanaActivationStableId);
        Assert.Equal(activation.OrientationDecision.DirectionCode,
            activeAfterTicks.OrientationDecision.DirectionCode);
        Assert.Equal(activation.OrientationDecision.EvidenceHashSha256,
            activeAfterTicks.OrientationDecision.EvidenceHashSha256);
        Assert.Equal(inheritances.Length,
            inheritances.Select(value => value.InheritanceStableId).Distinct().Count());
        var resident = Assert.Single(inheritances, value =>
            value.TargetCardStableId
                == SimulationTown생활복구Codes.ResidentLifeCardStableId);
        Assert.Equal(Simulation상위아르카나영향방식Codes.Numeric,
            resident.InfluenceModeCode);
        Assert.Equal(1.15m, resident.NumericMultiplier);
        Assert.Contains(SimulationTown생활복구Codes.EffectBindingCode,
            resident.AllowedEffectBindingCodes);
    }

    [Fact]
    public void 카드교체와해제는과거활성화를수정하지않고새계보와종료기록을남긴다()
    {
        var service = Service(out _);
        var session = service.Create(CreateRequest(Guid.NewGuid(), 57m, 43m));
        var (firstRequest, _) = PreviewFirstArcana(service, session);
        var first = service.ConfirmTurnClosing(session.SessionStableId,
            Confirm("command:arcana:first", session.Revision, firstRequest));
        var firstActivation = Assert.Single(
            first.TarotContext.MajorArcanaActivations);

        var (secondRequest, _) = PreviewFirstArcana(service, first);
        var second = service.ConfirmTurnClosing(session.SessionStableId,
            Confirm("command:arcana:replace", first.Revision, secondRequest));
        var ended = Assert.Single(second.TarotContext.MajorArcanaActivations,
            value => value.StateCode
                == Simulation메이저아르카나활성상태Codes.Ended);
        var active = Assert.Single(second.TarotContext.MajorArcanaActivations,
            value => value.StateCode
                == Simulation메이저아르카나활성상태Codes.Active);

        Assert.Equal(firstActivation.MajorArcanaActivationStableId,
            ended.MajorArcanaActivationStableId);
        Assert.Equal(Simulation메이저아르카나종료이유Codes.Replaced,
            ended.EndReasonCode);
        Assert.Equal(active.MajorArcanaActivationStableId,
            ended.SupersededByActivationStableId);
        Assert.NotEqual(ended.MajorArcanaActivationStableId,
            active.MajorArcanaActivationStableId);

        var deactivate = new SimulationTurnClosingPreviewRequest
        {
            ExpectedRevision = second.Revision,
            DeactivateActiveMajorArcana = true,
        };
        var deactivationPreview = service.PreviewTurnClosing(
            session.SessionStableId, deactivate);
        Assert.True(deactivationPreview.DeactivatesActiveMajorArcana);
        var deactivated = service.ConfirmTurnClosing(session.SessionStableId,
            Confirm("command:arcana:deactivate", second.Revision, deactivate));

        Assert.Empty(deactivated.TarotContext.OrientationInheritances);
        Assert.Empty(deactivated.TarotContext.FrameSet.ActiveFrames);
        Assert.Equal(2, deactivated.TarotContext.MajorArcanaActivations.Length);
        Assert.All(deactivated.TarotContext.MajorArcanaActivations, value =>
            Assert.Equal(Simulation메이저아르카나활성상태Codes.Ended,
                value.StateCode));
        Assert.Equal(Simulation메이저아르카나종료이유Codes.Deactivated,
            deactivated.TarotContext.MajorArcanaActivations
                .Single(value => value.MajorArcanaActivationStableId
                    == active.MajorArcanaActivationStableId).EndReasonCode);
    }

    [Fact]
    public void Town주민두명은배터리경쟁후대체물품을선택하고생활복구폐루프를완주한다()
    {
        var service = Service(out _);
        var session = service.Create(CreateRequest(Guid.NewGuid(), 57m, 43m));
        var (previewRequest, _) = PreviewFirstArcana(service, session);
        var activated = service.ConfirmTurnClosing(session.SessionStableId,
            Confirm("command:town:activate", session.Revision, previewRequest));

        var advanced = service.Advance(session.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:town:complete-loop",
                ExpectedRevision = activated.Revision,
                TickCount = 11,
            });
        var town = service.GetTownNpcLifeState(session.SessionStableId);

        Assert.Equal(advanced.CurrentTick, town.WorldTick);
        Assert.Equal(3, town.Orders.Length);
        Assert.All(town.Orders, order =>
        {
            Assert.Equal(SimulationTown주문단계Codes.Consumed, order.StageCode);
            Assert.Equal(new[]
            {
                "WI-ORDER-01", "WI-ORDER-02", "WI-ORDER-03", "WI-ORDER-04",
                "WI-ORDER-05", "WI-ORDER-06", "WI-ORDER-07",
            }, order.WorldInteractionHistoryIds);
            Assert.NotNull(order.ConsumptionBreakdown);
            Assert.Equal(1.2075m, order.ConsumptionBreakdown!.RawMultiplier);
            Assert.Equal(36.23m, order.ConsumptionBreakdown.FinalValue);
            Assert.Equal(SimulationWorldInteractionTriggerSourceCodes.NpcDriven,
                order.TriggerSourceCode);
        });
        Assert.All(town.Goals, goal => Assert.Equal(
            SimulationWorldInteractionTriggerSourceCodes.WorldDerived,
            goal.TriggerSourceCode));
        Assert.Contains(town.Orders, value =>
            value.NpcStableId == SimulationTown생활복구Codes.ResidentAStableId
            && value.ItemStableId
                == SimulationTown생활복구Codes.PortableBatteryItemStableId);
        Assert.Contains(town.Orders, value =>
            value.NpcStableId == SimulationTown생활복구Codes.ResidentBStableId
            && value.ItemStableId
                == SimulationTown생활복구Codes.WeatherproofTarpItemStableId);
        Assert.Contains(town.Orders, value =>
            value.NpcStableId == SimulationTown생활복구Codes.ResidentAStableId
            && value.ItemStableId
                == SimulationTown생활복구Codes.EmergencyFoodItemStableId);
        Assert.All(town.Npcs, npc => Assert.Equal(
            SimulationTown목표상태Codes.NoEligibleGoal,
            npc.CurrentGoalStateCode));
        Assert.False(town.IsOperationalState);
        Assert.True(town.SimulationOnly);
        Assert.NotEmpty(town.StateHashSha256);
    }

    [Fact]
    public void 활성아르카나와Town생활상태는V16저장재생에서동일한Hash로복원된다()
    {
        var source = Service(out var saveStore);
        var session = source.Create(CreateRequest(Guid.NewGuid(), 43m, 57m));
        var (previewRequest, _) = PreviewFirstArcana(source, session);
        var activated = source.ConfirmTurnClosing(session.SessionStableId,
            Confirm("command:save:activate", session.Revision, previewRequest));
        var advanced = source.Advance(session.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:save:advance",
                ExpectedRevision = activated.Revision,
                TickCount = 6,
            });
        var saved = source.Save(session.SessionStableId,
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:town-arcana:v16",
                ExpectedRevision = advanced.Revision,
            });
        var target = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(), saveStore);

        var restored = target.Restore(new SimulationSessionRestoreRequest
        {
            SaveStableId = saved.SaveStableId,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V16, saved.SchemaVersion);
        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
        Assert.Equal(saved.Snapshot.TarotContext.ContextStateHashSha256,
            restored.Session.TarotContext.ContextStateHashSha256);
        Assert.Equal(saved.Snapshot.TownNpcLife.StateHashSha256,
            restored.Session.TownNpcLife.StateHashSha256);
        Assert.Equal(Simulation타로카드방향Codes.Reversed,
            Assert.Single(restored.Session.TarotContext.MajorArcanaActivations)
                .OrientationDecision.DirectionCode);
    }

    [Fact]
    public void Town생활조회는전용서버경계에서Simulation사본만반환한다()
    {
        var store = new InMemory경영SimulationSessionStore();
        var facade = new 경영SimulationSessionService(store,
            new InMemorySimulationSessionSaveStore());
        var session = facade.Create(CreateRequest(Guid.NewGuid(), 57m, 43m));
        var controller = new 경영SimulationWorldGameplayController(
            new 경영SimulationWorldGameplayService(
                new 경영SimulationSessionAccessor(store)));

        var response = Assert.IsType<OkObjectResult>(
            controller.GetTownNpcLife(session.SessionStableId).Result);
        var state = Assert.IsType<SimulationTownNpcLifeStateSnapshot>(response.Value);

        Assert.Equal(SimulationTown생활복구Codes.ApprovedFixtureProfile,
            state.ProfileStableId);
        Assert.False(state.IsOperationalState);
        Assert.Equal(2, state.Npcs.Length);
    }

    [Fact]
    public async Task SoloLocalProcess도서버와같은Town읽기계약을사용한다()
    {
        using var runtime = new LocalSimulationRuntime(
            new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(),
            new 사용하지않는LocalSaveSlotStore());
        var session = await runtime.Sessions.CreateAsync(
            CreateRequest(Guid.NewGuid(), 57m, 43m));

        var town = await runtime.Turns.GetTownNpcLifeStateAsync(
            session.SessionStableId);

        Assert.Equal(SimulationAuthorityLocation.LocalProcess,
            runtime.Descriptor.AuthorityLocation);
        Assert.Equal(SimulationTown생활복구Codes.ApprovedFixtureProfile,
            town.ProfileStableId);
        Assert.Equal(session.TownNpcLife.StateHashSha256,
            town.StateHashSha256);
    }

    private static 경영SimulationSessionService Service(
        out InMemorySimulationSessionSaveStore saveStore)
    {
        saveStore = new InMemorySimulationSessionSaveStore();
        return new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(), saveStore);
    }

    private static (SimulationTurnClosingPreviewRequest Request,
        SimulationTurnClosingPreviewSnapshot Preview) PreviewFirstArcana(
        경영SimulationSessionService service, 경영SimulationSessionSnapshot session)
    {
        var offer = service.GetTurnClosingContext(session.SessionStableId)
            .TarotDraw.Offers.First();
        Assert.Empty(offer.OrientationCode);
        var request = new SimulationTurnClosingPreviewRequest
        {
            ExpectedRevision = session.Revision,
            SelectedTarotCard = new Simulation타로CardSelectionRequest
            {
                OfferStableId = offer.OfferStableId,
                CardStableId = offer.Card.CardStableId,
                OrientationCode = string.Empty,
            },
        };
        return (request, service.PreviewTurnClosing(session.SessionStableId, request));
    }

    private static SimulationTurnClosingConfirmRequest Confirm(string commandId,
        long revision, SimulationTurnClosingPreviewRequest preview)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            Preview = preview,
        };

    private static 경영SimulationSession생성Request CreateRequest(Guid clientRequestId,
        decimal recovery, decimal threat)
        => new()
        {
            ClientRequestId = clientRequestId,
            ScenarioStableId = "scenario:town-arcana-life-recovery",
            ScenarioDataRevision = "simulation-data:town-arcana-life-recovery:r1",
            ScenarioSeed = 240824,
            RuleRevision = "town-arcana-life-recovery:r1",
            TarotOrientationPolicyCode =
                Simulation타로방향결정정책Codes.RecoveryShare51,
            TownNpcLifeProfileStableId =
                SimulationTown생활복구Codes.ApprovedFixtureProfile,
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim:town-residents",
                TerritoryStableId = "territory:sim:town",
                SettlementStableId = "settlement:sim:town-common-market",
                GameDateStartsOn = new DateTimeOffset(
                    2026, 8, 24, 0, 0, 0, TimeSpan.Zero),
            },
            NatureMind = new SimulationNatureMindInitialStateRequest
            {
                Players = new[]
                {
                    new SimulationNatureMindPlayerInitialStateRequest
                    {
                        PlayerStableId = SimulationNatureMindCodes.DefaultPlayerStableId,
                        RecoveryBaseOutput = recovery,
                        ThreatBaseOutput = threat,
                    },
                },
            },
        };

    private sealed class 사용하지않는LocalSaveSlotStore : ISimulationLocalSaveSlotStore
    {
        public void Write(string slotStableId, SimulationSessionSavePackage package)
            => throw new NotSupportedException();

        public SimulationLocalSaveSlotPackage Read(string slotStableId)
            => throw new NotSupportedException();
    }
}
