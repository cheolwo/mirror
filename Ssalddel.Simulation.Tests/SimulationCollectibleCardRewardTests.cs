using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationCollectibleCardRewardTests
{
    private const string Session =
        "simulation-session:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Team = "team:sim:pyeongchang-card-collectors";
    private const string Farmer = "actor:sim:farmer";
    private const string Explorer = "actor:sim:explorer";
    private const string Npc = "actor:sim:farm-npc";
    private const string StartTile = "kr5186:l2:700:1145";

    [Fact]
    public void 시작타일은보상없이밝혀지고_인접L2이동만새발견으로기록된다()
    {
        var session = CreateSession(815);
        var initial = session.GetWorldExplorationState();

        var moved = session.ConfirmTileTraversal(Traversal(0, "move-1",
            StartTile, "kr5186:l2:701:1145"));

        Assert.Contains(StartTile, initial.RevealedL2TileKeys);
        Assert.Empty(initial.DiscoveryEvents);
        Assert.True(moved.WasNewL2Tile);
        Assert.False(moved.WasNewL1Area);
        Assert.Single(moved.Exploration.DiscoveryEvents);
        Assert.Equal("kr5186:l2:701:1145", Assert.Single(
            moved.Exploration.ActorTilePositions,
            value => value.ActorStableId == Explorer).CurrentL2TileKey);

        var rejected = Assert.Throws<SimulationContractException>(() =>
            session.ConfirmTileTraversal(Traversal(1, "move-too-far",
                "kr5186:l2:701:1145", "kr5186:l2:703:1145")));
        Assert.Equal("SimulationTraversalNotAdjacent", rejected.ErrorCode);
    }

    [Fact]
    public void 산수몽삼단계_알렉스설명은윤곽을열고_실제호송통과가장소와경로를확정한다()
    {
        var session = CreateSession(815);
        session.BeginHexagramCampaign(new SimulationHexagramCampaignEnterRequest
        {
            CommandId = "meng:enter",
            ExpectedRevision = session.Revision,
            HexagramStableId = SimulationHexagramCampaignCodes.MengStableId,
            StoryStageCount = 6,
        }, "save:meng-entry");
        for (var stage = 1; stage < 3; stage++)
            session.CompleteHexagramLine(new SimulationHexagramCampaignLineCompleteRequest
            {
                CommandId = "meng:stage:" + stage,
                ExpectedRevision = session.Revision,
                ExpectedLineOrdinal = stage,
            });

        var briefing = session.GetWorldExplorationState();
        Assert.Equal(5, briefing.MapKnowledgeEntries.Length);
        Assert.Equal(SimulationMapKnowledgeLevelCodes.Confirmed,
            Assert.Single(briefing.MapKnowledgeEntries, value =>
                value.SpatialStableId == "map-place:hans-farm").KnowledgeLevelCode);
        Assert.All(briefing.MapKnowledgeEntries.Where(value =>
                value.SpatialStableId != "map-place:hans-farm"),
            value => Assert.Equal(SimulationMapKnowledgeLevelCodes.KnownOutline,
                value.KnowledgeLevelCode));

        var from = StartTile;
        foreach (var x in new[] { 701, 702, 703 })
        {
            var to = $"kr5186:l2:{x}:1145";
            session.ConfirmTileTraversal(Traversal(session.Revision,
                "meng:escort:" + x, from, to));
            from = to;
        }

        var confirmed = session.GetWorldExplorationState();
        Assert.All(confirmed.MapKnowledgeEntries,
            value => Assert.Equal(SimulationMapKnowledgeLevelCodes.Confirmed,
                value.KnowledgeLevelCode));
        Assert.Equal(SimulationMapKnowledgeSourceCodes.EscortTraversal,
            Assert.Single(confirmed.MapKnowledgeEntries, value =>
                value.SpatialStableId == "route:meng:hans-farm-to-hub").SourceCode);
    }

    [Fact]
    public void 산수몽지도지식은_같은명령재시도와저장재생에서결정적이다()
    {
        var session = CreateSession(815);
        session.BeginHexagramCampaign(new SimulationHexagramCampaignEnterRequest
        {
            CommandId = "meng:save:enter",
            ExpectedRevision = session.Revision,
            HexagramStableId = SimulationHexagramCampaignCodes.MengStableId,
            StoryStageCount = 6,
        }, "save:meng-map-entry");
        for (var stage = 1; stage < 3; stage++)
            session.CompleteHexagramLine(new SimulationHexagramCampaignLineCompleteRequest
            {
                CommandId = "meng:save:stage:" + stage,
                ExpectedRevision = session.Revision,
                ExpectedLineOrdinal = stage,
            });
        var request = Traversal(session.Revision, "meng:save:checkpoint",
            StartTile, "kr5186:l2:701:1145");
        var first = session.ConfirmTileTraversal(request);
        var retry = session.ConfirmTileTraversal(request);
        Assert.Equal(
            경영SimulationSessionAggregate.BuildWorldExplorationStatePayloadKey(first.Exploration),
            경영SimulationSessionAggregate.BuildWorldExplorationStatePayloadKey(retry.Exploration));

        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:meng-map",
            ExpectedRevision = session.Revision,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        Assert.Equal(
            경영SimulationSessionAggregate.BuildWorldExplorationStatePayloadKey(
                session.GetWorldExplorationState()),
            경영SimulationSessionAggregate.BuildWorldExplorationStatePayloadKey(
                restored.GetWorldExplorationState()));
    }

    [Fact]
    public void 활성탐험역할은_새L2보상확률에정확히10퍼센트포인트를더한다()
    {
        var session = CreateSession(815);
        session.StartTeamActivity(new SimulationTeamActivityStartRequest
        {
            ClientRequestId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ExpectedRevision = 0,
            ExpectedTeamPolicyRevision = 3,
            ActorStableId = Explorer,
            CardCopyStableId = "team-card-copy:exploration",
            ActivityRoleCode = SimulationTeamRoleCardCodes.Exploration,
            ActivityStableId = "activity:exploration:reward-test",
            LocationStableId = "area:sim:pyeongchang:exploration",
        });

        var moved = session.ConfirmTileTraversal(Traversal(1, "role-bonus-move",
            StartTile, "kr5186:l2:701:1145"));
        var evaluation = Assert.Single(moved.Rewards.Evaluations,
            value => value.TriggerCode == SimulationCollectibleCardRewardCodes.NewL2Tile);

        Assert.Equal(25m, evaluation.AppliedProbabilityPercent);
        Assert.Equal(10m, evaluation.ActiveRoleBonusPercentagePoints);
    }

    [Fact]
    public void 농사보상은_PlayerDirect밭갈기완료에만판정되고_Npc위임은제외된다()
    {
        var session = CreateSession(815);
        session.ConfirmFarmWork(FarmWork("farm-direct", 0, Farmer,
            "soil:sim:1", SimulationFarmSurvivalCodes.PlayerDirect));
        session.Advance(Tick("tick-direct", 1, 1));
        session.ConfirmFarmWork(FarmWork("farm-npc", 2, Npc,
            "soil:sim:2", SimulationFarmSurvivalCodes.NpcDelegated));
        session.Advance(Tick("tick-npc", 3, 2));

        var rewards = session.GetCollectibleCardRewards(Farmer);
        var farmEvaluations = rewards.Evaluations.Where(value =>
            value.FamilyCode == SimulationCollectibleCardRewardCodes.Farm).ToArray();

        Assert.Single(farmEvaluations);
        Assert.Equal(SimulationCollectibleCardRewardCodes.FarmTillingCompleted,
            farmEvaluations[0].TriggerCode);
        Assert.DoesNotContain(rewards.Evaluations, value =>
            value.ActorStableId == Npc);
    }

    [Fact]
    public void 농사보상난수는_클라이언트CommandId가달라도같은사건이면같다()
    {
        var first = CreateSession(815);
        var second = CreateSession(815);
        first.ConfirmFarmWork(FarmWork("random-a", 0, Farmer,
            "soil:sim:1", SimulationFarmSurvivalCodes.PlayerDirect));
        second.ConfirmFarmWork(FarmWork("random-b", 0, Farmer,
            "soil:sim:1", SimulationFarmSurvivalCodes.PlayerDirect));
        first.Advance(Tick("random-a", 1, 1));
        second.Advance(Tick("random-b", 1, 1));

        var firstEvaluation = Assert.Single(first.GetCollectibleCardRewards(Farmer)
            .Evaluations);
        var secondEvaluation = Assert.Single(second.GetCollectibleCardRewards(Farmer)
            .Evaluations);

        Assert.Equal(firstEvaluation.SourceEventStableId,
            secondEvaluation.SourceEventStableId);
        Assert.Equal(firstEvaluation.DeterministicSamplePercent,
            secondEvaluation.DeterministicSamplePercent);
        Assert.Equal(firstEvaluation.ResultCode, secondEvaluation.ResultCode);
    }

    [Fact]
    public void 탐험실패가다섯번누적되면_여섯번째적격판정은보장성공하고실패수가초기화된다()
    {
        var session = CreateSession(9);
        var current = StartTile;
        foreach (var x in new[] { 701, 702, 703, 704 })
        {
            var next = $"kr5186:l2:{x}:1145";
            session.ConfirmTileTraversal(Traversal(session.Revision,
                "pity-" + x, current, next));
            current = next;
        }
        var before = session.GetCollectibleCardRewards(Explorer);
        var pityBefore = Assert.Single(before.PityStates, value =>
            value.ActorStableId == Explorer
            && value.FamilyCode == SimulationCollectibleCardRewardCodes.Exploration);

        var guaranteed = session.ConfirmTileTraversal(Traversal(session.Revision,
            "pity-guaranteed", current, "kr5186:l2:705:1145"));
        var last = guaranteed.Rewards.Evaluations.Last();
        var pityAfter = Assert.Single(guaranteed.Rewards.PityStates, value =>
            value.ActorStableId == Explorer
            && value.FamilyCode == SimulationCollectibleCardRewardCodes.Exploration);

        Assert.Equal(5, pityBefore.ConsecutiveFailureCount);
        Assert.True(last.WasGuaranteed);
        Assert.Equal(SimulationCollectibleCardRewardCodes.Success, last.ResultCode);
        Assert.Single(guaranteed.CreatedOpportunityStableIds);
        Assert.Equal(0, pityAfter.ConsecutiveFailureCount);
    }

    [Fact]
    public void 팀이보유한같은계열카드는_정의가소진되기전까지중복되지않는다()
    {
        var session = CreateSession(815);
        var path = CreateSerpentinePath();
        var current = StartTile;
        foreach (var next in path)
        {
            var moved = session.ConfirmTileTraversal(Traversal(session.Revision,
                "catalog-" + next.Replace(':', '-'), current, next));
            current = next;
            foreach (var opportunity in moved.CreatedOpportunityStableIds)
            {
                session.DrawCollectibleCard(new SimulationCollectibleCardDrawRequest
                {
                    CommandId = "command:draw:" + opportunity,
                    ExpectedRevision = session.Revision,
                    ActorStableId = Explorer,
                    OpportunityStableId = opportunity,
                });
                if (session.GetCollectibleCardRewards(Explorer).Cards.Count(value =>
                        value.FamilyCode == SimulationCollectibleCardRewardCodes.Exploration) == 3)
                    break;
            }
            if (session.GetCollectibleCardRewards(Explorer).Cards.Count(value =>
                    value.FamilyCode == SimulationCollectibleCardRewardCodes.Exploration) == 3)
                break;
        }
        var cards = session.GetCollectibleCardRewards(Explorer).Cards.Where(value =>
            value.FamilyCode == SimulationCollectibleCardRewardCodes.Exploration).ToArray();

        Assert.Equal(3, cards.Length);
        Assert.Equal(3, cards.Select(value => value.CardDefinitionStableId).Distinct().Count());
    }

    [Fact]
    public void 성공기회는소유자만뽑고_뽑은카드는떨어진팀원에게양도할수있다()
    {
        var session = CreateSessionWithFirstL2Success();
        var traversal = session.ConfirmTileTraversal(Traversal(0, "rewarded-move",
            StartTile, "kr5186:l2:701:1145"));
        var opportunityId = Assert.Single(traversal.CreatedOpportunityStableIds);

        var wrongOwner = Assert.Throws<SimulationConflictException>(() =>
            session.DrawCollectibleCard(new SimulationCollectibleCardDrawRequest
            {
                CommandId = "wrong-owner-draw",
                ExpectedRevision = 1,
                ActorStableId = Farmer,
                OpportunityStableId = opportunityId,
            }));
        Assert.Equal("SimulationCollectibleCardOpportunityOwnerMismatch",
            wrongOwner.ErrorCode);

        var draw = session.DrawCollectibleCard(new SimulationCollectibleCardDrawRequest
        {
            CommandId = "owner-draw",
            ExpectedRevision = 1,
            ActorStableId = Explorer,
            OpportunityStableId = opportunityId,
        });
        var transfer = session.TransferCollectibleCard(
            new SimulationCollectibleCardTransferRequest
            {
                CommandId = "remote-transfer",
                ExpectedRevision = 2,
                OwnerActorStableId = Explorer,
                TargetActorStableId = Farmer,
                CardCopyStableId = draw.DrawnCard.CardCopyStableId,
            });

        Assert.Equal(Farmer, transfer.TransferredCard.OwnerActorStableId);
        Assert.True(transfer.Rewards.SupportsRemoteTransfer);
        Assert.False(transfer.Rewards.HasExpiry);
        Assert.True(transfer.TransferredCard.PresentationOnly);
    }

    [Fact]
    public void 타일발견_뽑기_양도는SaveReplay뒤에도같은Hash와소유자를복원한다()
    {
        var session = CreateSessionWithFirstL2Success();
        var moved = session.ConfirmTileTraversal(Traversal(0, "save-move",
            StartTile, "kr5186:l2:701:1145"));
        var drawn = session.DrawCollectibleCard(new SimulationCollectibleCardDrawRequest
        {
            CommandId = "save-draw",
            ExpectedRevision = 1,
            ActorStableId = Explorer,
            OpportunityStableId = Assert.Single(moved.CreatedOpportunityStableIds),
        });
        session.TransferCollectibleCard(new SimulationCollectibleCardTransferRequest
        {
            CommandId = "save-transfer",
            ExpectedRevision = 2,
            OwnerActorStableId = Explorer,
            TargetActorStableId = Farmer,
            CardCopyStableId = drawn.DrawnCard.CardCopyStableId,
        });
        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "simulation-save:collectible-cards",
            ExpectedRevision = 3,
        });

        var restored = SimulationSessionReplay.Restore(package);
        var savedAgain = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "simulation-save:collectible-cards-restored",
            ExpectedRevision = 3,
        });
        var tampered = SimulationSaveReplayCloner.ClonePackage(package);
        tampered.Snapshot.CollectibleCardRewards!.Evaluations[0]
            .AppliedProbabilityPercent += 1m;
        var tamperError = Assert.Throws<SimulationConflictException>(() =>
            SimulationSessionReplay.Restore(tampered));

        Assert.Equal(package.ReplayHash, savedAgain.ReplayHash);
        Assert.Equal(Farmer, Assert.Single(restored.Snapshot()
            .CollectibleCardRewards!.Cards).OwnerActorStableId);
        Assert.Equal(3, package.CommandLog.Length);
        Assert.Equal("SimulationReplayHashMismatch", tamperError.ErrorCode);
    }

    [Fact]
    public async Task Api는발견조회와서버확정이동을제공한다()
    {
        using var factory = CreateFactory();
        var policies = factory.Services.GetRequiredService<
            InMemorySimulationTeamObservationPolicyStore>();
        policies.Replace(Policy());
        factory.Services.GetRequiredService<경영SimulationSessionService>()
            .Create(CreateRequest(815));
        using var client = factory.CreateClient();
        var root = "/api/simulation/v1/sessions/" + Uri.EscapeDataString(Session);

        var initial = await client.GetFromJsonAsync<
            SimulationWorldExplorationStateSnapshot>(root + "/exploration-state?actorStableId="
                + Uri.EscapeDataString(Explorer));
        using var response = await client.PostAsJsonAsync(root + "/tile-traversals/confirm",
            Traversal(0, "http-move", StartTile, "kr5186:l2:701:1145"));
        var moved = await response.Content.ReadFromJsonAsync<
            SimulationTileTraversalConfirmResponse>();

        Assert.NotNull(initial);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(moved);
        Assert.True(moved.WasNewL2Tile);
    }

    private static 경영SimulationSessionAggregate CreateSession(int seed)
        => new 경영SimulationSessionAggregate(CreateRequest(seed));

    private static 경영SimulationSessionAggregate CreateSessionWithFirstL2Success()
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            var candidate = CreateSession(seed);
            var moved = candidate.ConfirmTileTraversal(Traversal(0,
                "seed-probe", StartTile, "kr5186:l2:701:1145"));
            if (moved.CreatedOpportunityStableIds.Length > 0)
                return CreateSession(seed);
        }
        throw new InvalidOperationException("테스트용 성공 seed를 찾지 못했습니다.");
    }

    private static IEnumerable<string> CreateSerpentinePath()
    {
        var path = new List<string>();
        var x = 700;
        var y = 1145;
        while (x > 695 || y > 1140)
        {
            if (x > 695) x--;
            if (y > 1140) y--;
            path.Add($"kr5186:l2:{x}:{y}");
        }
        for (var row = 1140; row <= 1150; row++)
        {
            var xs = row % 2 == 0
                ? Enumerable.Range(695, 11)
                : Enumerable.Range(695, 11).Reverse();
            foreach (var tileX in xs)
            {
                var key = $"kr5186:l2:{tileX}:{row}";
                if (key != path.Last()) path.Add(key);
            }
            if (row < 1150)
                path.Add($"kr5186:l2:{(row % 2 == 0 ? 705 : 695)}:{row + 1}");
        }
        return path;
    }

    private static SimulationTileTraversalConfirmRequest Traversal(long revision,
        string command, string from, string to) => new()
    {
        CommandId = "command:traversal:" + command,
        ExpectedRevision = revision,
        ActorStableId = Explorer,
        FromL2TileKey = from,
        ToL2TileKey = to,
    };

    private static SimulationFarmWorkConfirmRequest FarmWork(string command,
        long revision, string actor, string soil, string assignment) => new()
    {
        CommandId = "command:farm:" + command,
        ExpectedRevision = revision,
        ActorStableId = actor,
        TargetStableId = soil,
        ActionCode = SimulationFarmSurvivalCodes.Tilling,
        AssignmentKindCode = assignment,
    };

    private static 경영SimulationTick진행Request Tick(string command,
        long revision, int count) => new()
    {
        CommandId = "command:tick:" + command,
        ExpectedRevision = revision,
        TickCount = count,
    };

    private static 경영SimulationSession생성Request CreateRequest(int seed) => new()
    {
        ClientRequestId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        ScenarioStableId = "scenario:pyeongchang-collectible-card",
        ScenarioDataRevision = "scenario-data:collectible-card.r1",
        ScenarioSeed = seed,
        RuleRevision = "simulation-world:collectible-card.r1",
        DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:sim:survivors",
            TerritoryStableId = "territory:sim:pyeongchang",
            SettlementStableId = "settlement:sim:daegwallyeong-farm",
            GameDateStartsOn = DateTimeOffset.Parse("2026-04-01T00:00:00Z"),
        },
        Settlement = new SimulationSettlementInitialStateRequest
        {
            TreasuryBalance = 100m,
            CurrencyCode = "SIM",
            LaborCapacityTotal = 10m,
            StorageCapacity = 20m,
            StorageUnitCode = "unit",
            PopulationCount = 3,
            PopulationFoodDemandPerTick = 1m,
            FoodEquivalentUnitCode = "person-day",
            FoodEquivalentRuleRevision = "food-equivalent:sim-r1",
            SourceStableIds = ["source:scenario:collectible-card-test"],
            Districts =
            [
                new SimulationSettlementDistrictRequest
                {
                    DistrictStableId = "district:sim:collectible-card-farm",
                    DistrictTypeCode = "FarmDistrict",
                    SourceStableIds = ["source:scenario:collectible-card-test"],
                },
            ],
            Facilities =
            [
                new SimulationSettlementFacilityRequest
                {
                    FacilityStableId = "facility:sim:collectible-card-storage",
                    FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Storage,
                    DistrictStableId = "district:sim:collectible-card-farm",
                    SourceStableIds = ["source:scenario:collectible-card-test"],
                },
                new SimulationSettlementFacilityRequest
                {
                    FacilityStableId = "facility:sim:collectible-card-market",
                    FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Market,
                    DistrictStableId = "district:sim:collectible-card-farm",
                    SourceStableIds = ["source:scenario:collectible-card-test"],
                },
            ],
        },
        FarmSurvival = new SimulationFarmSurvivalInitialStateRequest
        {
            RegionStableId = "region:legal-dong:5176031000",
            AreaStableId = "area:sim:pyeongchang:daegwallyeong-farm",
            TileKey = StartTile,
            FarmBuildingStableId = "building:sim:daegwallyeong-farmhouse",
            SupplyUnits = 8m,
            RepairMaterialUnits = 4m,
            Actors =
            [
                new SimulationFarmActorInitialStateRequest
                {
                    ActorStableId = Farmer,
                    ActorKindCode = SimulationFarmSurvivalCodes.Player,
                    KoreanName = "농사 담당 생존자",
                },
                new SimulationFarmActorInitialStateRequest
                {
                    ActorStableId = Explorer,
                    ActorKindCode = SimulationFarmSurvivalCodes.Player,
                    KoreanName = "탐험 담당 생존자",
                },
                new SimulationFarmActorInitialStateRequest
                {
                    ActorStableId = Npc,
                    ActorKindCode = SimulationFarmSurvivalCodes.Npc,
                    KoreanName = "농장 일꾼",
                },
            ],
            SoilTiles = Enumerable.Range(1, 8).Select(index =>
                new SimulationFarmSoilTileInitialStateRequest
                {
                    SoilTileStableId = "soil:sim:" + index,
                    GridX = index - 1,
                    GridY = 0,
                }).ToArray(),
        },
        TeamRoleCards = new SimulationTeamRoleCardInitialState
        {
            SessionStableId = Session,
            TeamStableId = Team,
            TeamPolicyRevision = 3,
            MemberActorStableIds = [Farmer, Explorer, Npc],
            Cards =
            [
                new SimulationTeamRoleCardInitialCard
                {
                    CardCopyStableId = "team-card-copy:exploration",
                    CardDefinitionStableId = "team-card:exploration",
                    Title = "탐험 역할",
                    ActivityRoleCodes = [SimulationTeamRoleCardCodes.Exploration],
                    EquippedActorStableId = Explorer,
                    SlotCode = SimulationTeamRoleCardCodes.Primary,
                },
                new SimulationTeamRoleCardInitialCard
                {
                    CardCopyStableId = "team-card-copy:farm",
                    CardDefinitionStableId = "team-card:farm",
                    Title = "농사 역할",
                    ActivityRoleCodes = [SimulationTeamRoleCardCodes.FarmWork],
                    EquippedActorStableId = Farmer,
                    SlotCode = SimulationTeamRoleCardCodes.Primary,
                },
            ],
        },
    };

    private static SimulationTeamObservationPolicySnapshot Policy() => new()
    {
        SessionStableId = Session,
        TeamStableId = Team,
        Revision = 3,
        MembersCanObserve = true,
        MemberActorStableIds = [Farmer, Explorer, Npc],
        AllowedViewModeCodes = [SimulationTeamObservationViewModeCodes.FirstPerson],
        ShowObserverIndicator = true,
        SimulationOnly = true,
        IsOperationalState = false,
    };

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SsalddelExecution:Mode"] = "Operational",
                    ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                    ["SimulationSharedPublicData:Enabled"] = "false",
                }));
        });
}
