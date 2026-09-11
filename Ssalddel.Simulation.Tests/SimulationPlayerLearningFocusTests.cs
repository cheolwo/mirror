using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Hosting.Controllers;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "NPC 학습중점의 절기 구간·멱등성·행위 기여·Save/Replay·Adapter 동등성을 검증한다.",
    Boundary = "자동 시험은 Unity UI·Scene·Game View나 E4 이상 증거를 대신하지 않는다.")]
public sealed class SimulationPlayerLearningFocusTests
{
    [Fact]
    public void 한스Catalog는_농사와도끼카드를_기존WI분야결속에맞춘다()
    {
        var initial = CreateInitial("session:learning", "player:one");

        Simulation학습중점State.ValidateInitial(initial);

        Assert.Equal(2, initial.Cards.Length);
        Assert.Contains(initial.Cards, value =>
            value.CardStableId == Simulation학습중점Codes.HansFarmingCardStableId
            && value.Bindings.Length == 6
            && value.PrimaryFiveElementCode == Simulation학습중점Codes.Wood);
        Assert.Contains(initial.Cards, value =>
            value.CardStableId == Simulation학습중점Codes.HansAxeCardStableId
            && value.Bindings.Length == 2
            && value.PrimaryFiveElementCode == Simulation학습중점Codes.Metal);
        Assert.All(initial.Cards, value => Assert.Equal(
            value.DefinitionHashSha256,
            Simulation학습중점State.CalculateCardDefinitionHash(value)));
    }

    [Fact]
    public void 카드hash변경과_불연속절기구간은_초기화에서거부된다()
    {
        var cardChanged = CreateInitial("session:learning", "player:one");
        cardChanged.Cards[0].Title = "조용히 바뀐 제목";
        var scheduleChanged = CreateInitial("session:learning", "player:one");
        scheduleChanged.Segments[1].StartWorldTickInclusive = 4;

        Assert.Equal("SimulationLearningFocusCardDefinitionInvalid",
            Assert.Throws<SimulationContractException>(() =>
                new Simulation학습중점State(cardChanged)).ErrorCode);
        Assert.Equal("SimulationLearningFocusSegmentScheduleInvalid",
            Assert.Throws<SimulationContractException>(() =>
                new Simulation학습중점State(scheduleChanged)).ErrorCode);
    }

    [Fact]
    public void Preview는_초반장착시점만보여주고_상태를변경하지않는다()
    {
        var state = new Simulation학습중점State(
            CreateInitial("session:learning", "player:one"));
        var before = state.Snapshot();
        var preview = state.Preview(Change(0,
            Simulation학습중점Codes.HansFarmingCardStableId), 0);
        var after = state.Snapshot();

        Assert.True(preview.AppliesAtCurrentBoundary);
        Assert.Equal("segment:spring-equinox:early",
            preview.EffectiveSegmentStableId);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.StateHashSha256, after.StateHashSha256);
        Assert.Empty(after.ChangeReceipts);
    }

    [Fact]
    public void Confirm은_한슬롯을즉시활성화하고_같은요청을멱등재사용한다()
    {
        var state = new Simulation학습중점State(
            CreateInitial("session:learning", "player:one"));
        var request = Change(0,
            Simulation학습중점Codes.HansFarmingCardStableId);

        var confirmed = state.Confirm(request, 0);
        var reused = state.Confirm(request, 0);

        Assert.Equal(1, confirmed.Revision);
        Assert.Equal(Simulation학습중점Codes.HansFarmingCardStableId,
            confirmed.ActiveCardStableId);
        Assert.Null(confirmed.PendingChange);
        Assert.Single(confirmed.ActivationHistory);
        Assert.Equal(confirmed.StateHashSha256, reused.StateHashSha256);
        Assert.Equal("SimulationLearningFocusRequestPayloadConflict",
            Assert.Throws<SimulationConflictException>(() => state.Confirm(new()
            {
                ClientRequestId = request.ClientRequestId,
                ExpectedRevision = request.ExpectedRevision,
                PlayerStableId = request.PlayerStableId,
                CardStableId = Simulation학습중점Codes.HansAxeCardStableId,
            }, 0)).ErrorCode);
    }

    [Fact]
    public void 구간중변경은_다음구간에예약되고_후속확정이같은구간예약을대체한다()
    {
        var id = Guid.Parse("c226d0d3-87ef-44b2-8d49-c84c16f11101");
        var sessionId = SessionId(id);
        var session = new 경영SimulationSessionAggregate(CreateSessionRequest(id));
        var first = session.ConfirmLearningFocusChange(Change(0,
            Simulation학습중점Codes.HansFarmingCardStableId));
        var axe = session.ConfirmLearningFocusChange(Change(first.Revision,
            Simulation학습중점Codes.HansAxeCardStableId));
        var replacement = session.ConfirmLearningFocusChange(Change(
            axe.Revision, Simulation학습중점Codes.HansFarmingCardStableId));

        Assert.Equal("segment:spring-equinox:middle",
            replacement.PendingChange!.EffectiveSegmentStableId);
        Assert.Equal(Simulation학습중점Codes.HansFarmingCardStableId,
            replacement.PendingChange.CardStableId);

        session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "tick:learning:middle",
            ExpectedRevision = session.Revision,
            TickCount = 3,
        });
        var activated = session.GetLearningFocusState();
        Assert.Equal(sessionId, activated.SessionStableId);
        Assert.Null(activated.PendingChange);
        Assert.Equal(Simulation학습중점Codes.HansFarmingCardStableId,
            activated.ActiveCardStableId);
        Assert.Equal("segment:spring-equinox:middle",
            activated.ActiveFromSegmentStableId);
        Assert.Equal(3, activated.ChangeReceipts.Length);
        Assert.Equal(2, activated.ActivationHistory.Length);
    }

    [Fact]
    public void 마지막구간에는_다음학습구간변경을예약할수없다()
    {
        var state = new Simulation학습중점State(
            CreateInitial("session:learning", "player:one"));

        Assert.Equal("SimulationLearningFocusNextSegmentUnavailable",
            Assert.Throws<SimulationConflictException>(() => state.Preview(
                Change(0, Simulation학습중점Codes.HansFarmingCardStableId),
                7)).ErrorCode);
    }

    [Theory]
    [InlineData(Simulation행위결과분류Codes.성공, 1)]
    [InlineData(Simulation행위결과분류Codes.의미있는실패, 1)]
    [InlineData(Simulation행위결과분류Codes.후퇴복구, 1)]
    [InlineData(Simulation행위결과분류Codes.취소, 0)]
    public void 활성학습카드는_취소를제외한의미있는행위에_이해도하나를준다(
        string resultCode,
        int expectedUnderstanding)
    {
        var session = CreateSessionWithActiveCard(
            Simulation학습중점Codes.HansFarmingCardStableId);

        session.AppendActionManifestationAndProgression(Action(
            "WI-FARM-05", "command:farm:" + resultCode, resultCode));
        var profile = session.GetPlayerDomainProfile("player:one")!;
        var farm = profile.분야진척들.Single(value =>
            value.분야StableId == Simulation플레이어분야Codes.농업생산);
        var learning = profile.기여기록들.Where(value =>
            value.SourceCode == Simulation분야기여SourceCodes.Npc학습중점)
            .ToArray();

        Assert.Equal(expectedUnderstanding, farm.이해도);
        Assert.Equal(expectedUnderstanding, learning.Length);
        Assert.Equal(expectedUnderstanding,
            session.GetLearningFocusState().EffectReceipts.Length);
        Assert.All(learning, value =>
            Assert.Equal(Simulation학습중점Codes.HansFarmingCardStableId,
                value.PublicationStableId));
    }

    [Fact]
    public void 활성카드와무관한WI와_같은행위재시도는_추가학습을만들지않는다()
    {
        var session = CreateSessionWithActiveCard(
            Simulation학습중점Codes.HansFarmingCardStableId);
        var unrelated = Action("WI-NATURE-06", "command:logging",
            Simulation행위결과분류Codes.성공);
        session.AppendActionManifestationAndProgression(unrelated);
        var farm = Action("WI-FARM-05", "command:farm:once",
            Simulation행위결과분류Codes.성공);
        session.AppendActionManifestationAndProgression(farm);
        session.AppendActionManifestationAndProgression(farm);

        var learning = session.GetPlayerDomainProfile("player:one")!
            .기여기록들.Where(value => value.SourceCode ==
                Simulation분야기여SourceCodes.Npc학습중점).ToArray();
        Assert.Single(learning);
        Assert.Single(session.GetLearningFocusState().EffectReceipts);
    }

    [Fact]
    public void SaveV30은_학습상태와행위기여를_같은hash로복원한다()
    {
        var session = CreateSessionWithActiveCard(
            Simulation학습중점Codes.HansFarmingCardStableId);
        session.AppendActionManifestationAndProgression(Action(
            "WI-FARM-05", "command:farm:save",
            Simulation행위결과분류Codes.의미있는실패));
        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:learning-focus:v30",
            ExpectedRevision = session.Revision,
        });

        var restored = SimulationSessionReplay.Restore(saved);
        var savedAgain = restored.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = saved.SaveStableId,
                ExpectedRevision = restored.Revision,
            });

        Assert.Equal(SimulationSaveSchemaVersions.V30, saved.SchemaVersion);
        Assert.NotNull(saved.LearningFocus);
        Assert.Equal(saved.LearningFocus!.StateHashSha256,
            restored.GetLearningFocusState().StateHashSha256);
        Assert.Equal(saved.PlayerDomainProfile!.StateHashSha256,
            restored.GetPlayerDomainProfile("player:one")!.StateHashSha256);
        Assert.Equal(saved.ReplayHash, savedAgain.ReplayHash);
    }

    [Fact]
    public async System.Threading.Tasks.Task LocalRuntime과HttpAdapter는_같은상태판본을반환한다()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "ssalddel-learning-focus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new InMemory경영SimulationSessionStore();
            using var runtime = new LocalSimulationRuntime(store,
                new InMemorySimulationSessionSaveStore(),
                new FileSimulationLocalSaveSlotStore(root));
            var request = CreateSessionRequest(Guid.Parse(
                "26e4e2ed-e4b0-4801-ad32-11dccf482201"));
            var created = await runtime.CreateAsync(request);
            var local = await runtime.GetLearningFocusAsync(
                created.SessionStableId, "player:one");
            var service = new SimulationPlayerLearningFocusService(store);
            var controller = new SimulationPlayerLearningFocusController(service);
            var http = Assert.IsType<OkObjectResult>(controller.Get(
                created.SessionStableId, "player:one").Result);
            var projection = Assert.IsType<Simulation학습중점ProjectionSnapshot>(
                http.Value);

            Assert.Equal(local.StateHashSha256, projection.StateHashSha256);
            Assert.Equal(local.Revision, projection.Revision);
            Assert.Equal(local.OwnedCards.Select(value => value.CardStableId),
                projection.OwnedCards.Select(value => value.CardStableId));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void 이데아맵은_실제행위전에는_노드와학습필요를만들지않는다()
    {
        var session = new 경영SimulationSessionAggregate(
            CreateSessionRequest(Guid.NewGuid()));

        var map = session.GetPlayerIdeaMapProjection("player:one");

        Assert.False(map.BasicViewAvailable);
        Assert.Empty(map.Nodes);
        Assert.Empty(map.Edges);
        Assert.DoesNotContain(map.Nodes, value => value.NodeKindCode ==
            Simulation플레이어이데아맵Codes.LearningNeed);
        Assert.False(map.ChangesWorldState);
    }

    [Fact]
    public void 첫실제행위는_장소와관계없이_기초이데아맵을연다()
    {
        var session = new 경영SimulationSessionAggregate(
            CreateSessionRequest(Guid.NewGuid()));
        session.AppendActionManifestationAndProgression(Action(
            "WI-NATURE-06", "command:first-action",
            Simulation행위결과분류Codes.성공));

        var map = session.GetPlayerIdeaMapProjection("player:one");

        Assert.True(map.BasicViewAvailable);
        Assert.Contains(map.Nodes, value => value.NodeKindCode ==
            Simulation플레이어이데아맵Codes.RecentExperience
            && value.Title == "WI-NATURE-06");
        Assert.Contains(map.Nodes, value => value.NodeKindCode ==
            Simulation플레이어이데아맵Codes.VerifiedKnowledgeSkill);
        Assert.Contains(map.Edges, value => value.EdgeKindCode ==
            Simulation플레이어이데아맵Codes.VerifiedBy);
    }

    [Fact]
    public void 한스학습중점은_후속실제행위에만_멘토관계를남긴다()
    {
        var session = CreateSessionWithActiveCard(
            Simulation학습중점Codes.HansFarmingCardStableId);
        session.AppendActionManifestationAndProgression(Action(
            "WI-FARM-05", "command:mentored-farm",
            Simulation행위결과분류Codes.성공));
        var unrelated = Action("WI-NATURE-06",
            "command:unrelated-logging",
            Simulation행위결과분류Codes.성공);
        unrelated.BeforeWorldRevision = 1;
        unrelated.AfterWorldRevision = 2;
        session.AppendActionManifestationAndProgression(unrelated);

        var map = session.GetPlayerIdeaMapProjection("player:one");
        var mentored = Assert.Single(map.Edges, value =>
            value.EdgeKindCode ==
                Simulation플레이어이데아맵Codes.MentoredBy);

        Assert.Equal("npc:hans", mentored.SourceMentorActorStableId);
        Assert.Contains(map.Nodes, value =>
            value.SourceMentorActorStableId == "npc:hans"
            && value.이해도 == 1);
    }

    [Fact]
    public void 이데아맵은_전역명상과_분야별숙련을_서로다른필드로보존한다()
    {
        var session = CreateSessionWithActiveCard(
            Simulation학습중점Codes.HansFarmingCardStableId);
        session.AppendActionManifestationAndProgression(Action(
            "WI-FARM-05", "command:separate-axes",
            Simulation행위결과분류Codes.성공));

        var map = session.GetPlayerIdeaMapProjection("player:one");
        var farm = Assert.Single(map.Nodes, value =>
            value.분야StableId == Simulation플레이어분야Codes.농업생산);

        Assert.Equal(0, map.MeditationProficiency);
        Assert.Equal(Simulation분야단계Codes.미경험,
            map.MeditationStageCode);
        Assert.Equal(1, farm.이해도);
    }

    [Fact]
    public void 이데아맵은_재조회와SaveReplay에서_같은파생hash를돌려준다()
    {
        var session = CreateSessionWithActiveCard(
            Simulation학습중점Codes.HansFarmingCardStableId);
        session.AppendActionManifestationAndProgression(Action(
            "WI-FARM-05", "command:idea-save",
            Simulation행위결과분류Codes.의미있는실패));
        var before = session.GetPlayerIdeaMapProjection("player:one");
        var again = session.GetPlayerIdeaMapProjection("player:one");
        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:idea-map:v30",
            ExpectedRevision = session.Revision,
        });
        var restored = SimulationSessionReplay.Restore(saved)
            .GetPlayerIdeaMapProjection("player:one");

        Assert.Equal(before.StateHashSha256, again.StateHashSha256);
        Assert.Equal(before.StateHashSha256, restored.StateHashSha256);
        Assert.Equal(before.WorldRevision, restored.WorldRevision);
    }

    [Fact]
    public async System.Threading.Tasks.Task 이데아맵LocalRuntime과HttpAdapter는_같은읽기모델을반환한다()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "ssalddel-idea-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new InMemory경영SimulationSessionStore();
            using var runtime = new LocalSimulationRuntime(store,
                new InMemorySimulationSessionSaveStore(),
                new FileSimulationLocalSaveSlotStore(root));
            var created = await runtime.CreateAsync(
                CreateSessionRequest(Guid.NewGuid()));
            var local = await runtime.GetPlayerIdeaMapAsync(
                created.SessionStableId, "player:one");
            var controller = new SimulationPlayerIdeaMapController(
                new SimulationPlayerIdeaMapService(store));
            var http = Assert.IsType<OkObjectResult>(controller.Get(
                created.SessionStableId, "player:one").Result);
            var projection = Assert.IsType<
                Simulation플레이어이데아맵ProjectionSnapshot>(http.Value);

            Assert.Equal(local.StateHashSha256, projection.StateHashSha256);
            Assert.Equal(local.WorldRevision, projection.WorldRevision);
            Assert.False(projection.ChangesWorldState);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static 경영SimulationSessionAggregate CreateSessionWithActiveCard(
        string cardStableId)
    {
        var id = Guid.NewGuid();
        var request = CreateSessionRequest(id);
        request.LearningFocus!.ActiveCardStableId = cardStableId;
        request.LearningFocus.ActiveFromSegmentStableId =
            "segment:spring-equinox:early";
        return new 경영SimulationSessionAggregate(request);
    }

    private static 경영SimulationSession생성Request CreateSessionRequest(Guid id)
    {
        var sessionId = SessionId(id);
        return new 경영SimulationSession생성Request
        {
            ClientRequestId = id,
            ScenarioStableId = "scenario:player-npc-learning-focus",
            ScenarioDataRevision = "fixture.learning-focus.r1",
            ScenarioSeed = 20260903,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 9,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:solo",
                TerritoryStableId = "territory:nature-farm",
                SettlementStableId = "settlement:hans-farm",
                GameDateStartsOn = new DateTimeOffset(
                    2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
            },
            LearningFocus = CreateInitial(sessionId, "player:one"),
        };
    }

    private static Simulation학습중점InitialState CreateInitial(
        string sessionStableId,
        string playerStableId)
        => Simulation기본Npc학습카드Catalog.CreateHansInitialState(
            sessionStableId,
            playerStableId,
            "solar-term-learning-cadence.fixture.r1",
            new[]
            {
                Segment("segment:spring-equinox:early",
                    Simulation학습중점Codes.Early, 0, 3),
                Segment("segment:spring-equinox:middle",
                    Simulation학습중점Codes.Middle, 3, 6),
                Segment("segment:spring-equinox:late",
                    Simulation학습중점Codes.Late, 6, 9),
            });

    private static Simulation학습구간Snapshot Segment(
        string id, string phase, int start, int end)
        => new()
        {
            SegmentStableId = id,
            SolarTermStableId = "solar-term:spring-equinox",
            SolarTermRevision = "solar-term.fixture.r1",
            PhaseCode = phase,
            StartWorldTickInclusive = start,
            EndWorldTickExclusive = end,
        };

    private static Simulation학습중점ChangeRequest Change(
        long revision, string cardStableId)
        => new()
        {
            ClientRequestId = Guid.NewGuid(),
            ExpectedRevision = revision,
            PlayerStableId = "player:one",
            CardStableId = cardStableId,
        };

    private static Simulation행위발현Record Action(
        string wi, string command, string result)
        => new()
        {
            WorldStableId = "world:nature-farm",
            SessionStableId = "session:learning-focus",
            PlayableLoopStableId =
                "playable-unit:player-npc-learning-focus.v1",
            WorldInteractionId = wi,
            CommandId = command,
            TriggerSourceCode =
                SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
            InitiatorStableId = "player:one",
            ActorStableId = "player:one",
            ActorKindCode = "Player",
            TargetStableIds = new[] { "target:learning" },
            OutcomeStableId = "outcome:" + command,
            PrimaryOutcomeCode = "Completed",
            결과분류Code = result,
            EffectBatchStableId = "effect-batch:" + command,
            EffectReceiptStableIds = new[] { "effect-receipt:" + command },
            영향공간StableIds = new[] { "h1:hans-farm" },
            BeforeWorldRevision = 0,
            AfterWorldRevision = 1,
            AppliedWorldTick = 0,
            RuleRevision = "fixture.learning-focus.r1",
        };

    private static Simulation행위발현Record AppendGroundedAction(
        경영SimulationSessionAggregate session,
        string wi,
        string command,
        string result,
        params string[] sourceReferences)
    {
        var action = Action(wi, command, result);
        action.SessionStableId = session.SessionStableId;
        var previousRevision = session.GetActionManifestationLedger()
            ?.TailRecords.LastOrDefault()?.AfterWorldRevision ?? 0;
        action.BeforeWorldRevision = previousRevision;
        action.AfterWorldRevision = previousRevision + 1;
        action.SourceReferenceIds = sourceReferences ?? Array.Empty<string>();
        session.AppendActionManifestationAndProgression(action);
        return session.GetActionManifestationLedger()!.TailRecords.Single(value =>
            value.CommandId == command);
    }

    private static string SessionId(Guid clientRequestId)
        => "simulation-session:" + clientRequestId.ToString("N");
}
