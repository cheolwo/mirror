using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "세계 대기 구간 결정성, simulation-save.v25 복원과 이전 저장 hash 호환을 검증한다.",
    Boundary = "자동 시험은 Unity Play Mode, Game View, 음향 또는 현실 기상 검증을 대신하지 않는다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3결정성검증,
    WorldInteractionIds = new[] { "WI-NATURE-14" },
    WorkOrderIds = new[] { "E9-WO-NATURE-SKY-ENGINE" })]
public sealed class SimulationWorldAtmosphereTests
{
    [Theory]
    [InlineData(0, WorldWeatherCodes.Clear)]
    [InlineData(449, WorldWeatherCodes.Clear)]
    [InlineData(450, WorldWeatherCodes.Cloudy)]
    [InlineData(600, WorldWeatherCodes.Rain)]
    [InlineData(820, WorldWeatherCodes.Thunderstorm)]
    [InlineData(990, WorldWeatherCodes.Rain)]
    [InlineData(1110, WorldWeatherCodes.Clear)]
    public void 같은Nature시각은_결정적세계대기상태로투영된다(
        int elapsedSeconds, string expectedWeatherCode)
    {
        var first = WorldAtmosphereRules.Evaluate(
            WorldAtmosphereProfileCodes.NatureNightDay2FixtureR1,
            1701, 2, elapsedSeconds);
        var second = WorldAtmosphereRules.Evaluate(
            WorldAtmosphereProfileCodes.NatureNightDay2FixtureR1,
            1701, 2, elapsedSeconds);

        Assert.Equal(expectedWeatherCode, first.WeatherCode);
        Assert.Equal(first.NextWeatherCode, second.NextWeatherCode);
        Assert.Equal(first.TransitionProgressPermille,
            second.TransitionProgressPermille);
        Assert.Equal(first.CloudCoverPermille, second.CloudCoverPermille);
        Assert.Equal(first.PrecipitationPermille,
            second.PrecipitationPermille);
        Assert.Equal(first.WindIntensityPermille,
            second.WindIntensityPermille);
        Assert.Equal(first.LightningSequenceIndex,
            second.LightningSequenceIndex);
        Assert.InRange(first.TransitionProgressPermille, 0, 1000);
    }

    [Fact]
    public void Nature시간진행은_세계대기상태와_v25저장hash를같이갱신한다()
    {
        var aggregate = new 경영SimulationSessionAggregate(CreateRequest());
        var initial = aggregate.Snapshot();

        Assert.Equal(WorldWeatherCodes.Clear,
            initial.Atmosphere!.WeatherCode);

        for (var index = 0; index < 10; index++)
        {
            aggregate.AdvanceNatureSurvivalClock(new()
            {
                CommandId = "command:atmosphere:advance-to-rain:" + index,
                ExpectedRevision = aggregate.Revision,
                ElapsedRealtimeSeconds = 60,
            });
        }
        var raining = aggregate.Snapshot();
        var package = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-world-atmosphere",
            ExpectedRevision = aggregate.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new()
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(WorldWeatherCodes.Rain,
            raining.Atmosphere!.WeatherCode);
        Assert.Equal(SimulationSaveSchemaVersions.V25, package.SchemaVersion);
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(raining.Atmosphere.WeatherCode,
            restored.Snapshot().Atmosphere.WeatherCode);
        Assert.Equal(raining.Atmosphere.LightningSequenceIndex,
            restored.Snapshot().Atmosphere.LightningSequenceIndex);
    }

    [Fact]
    public void 기존Nature요청은_대기계약없이_v24hash경계를유지한다()
    {
        var request = CreateRequest();
        request.Atmosphere = null;
        var aggregate = new 경영SimulationSessionAggregate(request);
        var package = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-world-atmosphere-compat",
            ExpectedRevision = aggregate.Revision,
        });

        Assert.False(aggregate.Snapshot().Atmosphere.IsEnabled);
        Assert.Equal(SimulationSaveSchemaVersions.V24, package.SchemaVersion);
        Assert.Equal(package.ReplayHash, SimulationReplayHasher.Calculate(package));
    }

    [Fact]
    public void 세계대기와_장착상태가함께있는_v28저장은_행위원장과재생hash를유지한다()
    {
        var request = CreateRequest();
        request.ClientRequestId = Guid.NewGuid();
        request.ScenarioStableId = "scenario:nature-solo-local";
        request.ScenarioDataRevision =
            SimulationNatureSurvivalCodes.ProfileRevisionR5;
        request.NatureSurvival!.StartsWithAxe = false;
        request.NatureSurvival.ResourceNodes = Enumerable.Range(1, 6)
            .Select(index => new SimulationNatureResourceNodeInitialStateRequest
            {
                ResourceNodeStableId = $"resource:nature-tree:{index:00}",
                H2StableId = "h2-candidate:nature-encounter-route",
                H1StableId = "h1-stock:nature-exploration-buffer",
                LocalX = -9f + index * 3f,
                LocalZ = 8f + index % 2 * 2.5f,
            }).ToArray();
        request.SpatialWorld = new Simulation공간세계InitialStateRequest
        {
            Definitions = PyeongchangSimulation공간상호작용Fixture
                .CreateNatureTwilightActualE5Observation().Definitions
                .Concat(PyeongchangSimulation공간상호작용Fixture
                    .CreateNatureDroppedTimberActualE5().Definitions)
                .ToArray(),
        };
        request.ActorEquipment = new SimulationActorEquipmentInitialStateRequest
        {
            ActorStableId = "player:solo",
            ItemInstances = new[]
            {
                new SimulationOwnedItemInstanceInitialState
                {
                    ItemInstanceStableId =
                        SimulationNatureSurvivalCodes.AxePickupStableId,
                    ItemDefinitionStableId =
                        SimulationActorEquipmentCodes.AxeDefinitionStableId,
                    LocationCode = SimulationActorEquipmentCodes.WorldPickup,
                    SourceSpatialStableId =
                        SimulationNatureSurvivalCodes.AxePickupStableId,
                },
            },
        };
        var aggregate = new 경영SimulationSessionAggregate(request);
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.Restore(aggregate);
        var nature = new SimulationNatureSurvivalService(sessions);
        var acquirePreview = aggregate.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        });
        Assert.True(acquirePreview.CanConfirm,
            acquirePreview.SpatialEvidenceStateCode + ":"
            + string.Join(",", acquirePreview.BlockReasonCodes) + ":"
            + string.Join(",", acquirePreview.SpatialEvidenceReferenceIds));
        nature.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "unity-local:AcquireAxe:revision-0:1",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        });
        var acquired = aggregate.GetActorEquipmentState();
        aggregate.ConfirmActorEquipmentChange(new()
        {
            CommandId = "unity-local:actor-equipment-Equip:revision-1:2",
            ExpectedEquipmentRevision = acquired.EquipmentRevision,
            ActorStableId = "player:solo",
            OperationCode = SimulationActorEquipmentCodes.Equip,
            ItemInstanceStableId =
                SimulationNatureSurvivalCodes.AxePickupStableId,
            SlotCode = SimulationActorEquipmentCodes.MainHand,
        });
        var package = aggregate.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-world-atmosphere-equipment",
            ExpectedRevision = aggregate.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var replayed = restored.CreateSavePackage(new()
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V28, package.SchemaVersion);
        Assert.Equal(SimulationSaveSchemaVersions.V25,
            package.ActorEquipmentBaseSchemaVersion);
        Assert.Equal(package.ReplayHash, replayed.ReplayHash);
    }

    [Fact]
    public async Task 같은대기명령열은_LocalCore와_RemoteHost에서_v25ReplayHash가같다()
    {
        var local = new 경영SimulationSessionAggregate(CreateRequest());
        for (var index = 0; index < 10; index++)
        {
            local.AdvanceNatureSurvivalClock(new()
            {
                CommandId = "command:atmosphere:local:" + index,
                ExpectedRevision = local.Revision,
                ElapsedRealtimeSeconds = 60,
            });
        }
        var localSnapshot = local.Snapshot();
        var localPackage = local.CreateSavePackage(new()
        {
            SaveStableId = "save:nature-world-atmosphere-parity",
            ExpectedRevision = local.Revision,
        });

        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var remote = (await createResponse.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>())!;
        for (var index = 0; index < 10; index++)
        {
            var advanceResponse = await client.PostAsJsonAsync(
                $"/api/simulation/v1/sessions/{remote.SessionStableId}" +
                "/nature-survival/clock/advance",
                new SimulationNatureSurvivalClockAdvanceRequest
                {
                    CommandId = "command:atmosphere:local:" + index,
                    ExpectedRevision = remote.Revision,
                    ElapsedRealtimeSeconds = 60,
                });
            Assert.Equal(HttpStatusCode.OK, advanceResponse.StatusCode);
            remote = (await advanceResponse.Content.ReadFromJsonAsync<
                경영SimulationSessionSnapshot>())!;
        }

        var saveResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{remote.SessionStableId}/saves",
            new SimulationSessionSaveRequest
            {
                SaveStableId = localPackage.SaveStableId,
                ExpectedRevision = remote.Revision,
            });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var remotePackage = (await saveResponse.Content.ReadFromJsonAsync<
            SimulationSessionSavePackage>())!;

        Assert.Equal(localSnapshot.Revision, remote.Revision);
        Assert.Equal(localSnapshot.Atmosphere.WeatherCode,
            remote.Atmosphere.WeatherCode);
        Assert.Equal(localSnapshot.Atmosphere.LightningSequenceIndex,
            remote.Atmosphere.LightningSequenceIndex);
        Assert.Equal(SimulationSaveSchemaVersions.V25,
            remotePackage.SchemaVersion);
        Assert.Equal(localPackage.ReplayHash, remotePackage.ReplayHash);
    }

    private static 경영SimulationSession생성Request CreateRequest() => new()
    {
        ClientRequestId = Guid.Parse("31711d42-6124-42db-8865-c02bf1aaee70"),
        ScenarioStableId = "scenario:nature-night-day2-atmosphere-fixture",
        ScenarioDataRevision = "nature-night-day2.atmosphere.r1",
        ScenarioSeed = 1701,
        RuleRevision = "simulation.rule.r1",
        DurationTicks = 365,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:solo",
            TerritoryStableId = "territory:nature",
            SettlementStableId = "settlement:nature-home",
            GameDateStartsOn = new DateTimeOffset(2026, 8, 23, 0, 0, 0,
                TimeSpan.Zero),
        },
        NatureSurvival = new SimulationNatureSurvivalInitialStateRequest
        {
            ProfileRevision = SimulationNatureSurvivalCodes.ProfileRevisionR5,
            PlayerStableId = "player:solo",
            BuildingProgressionCatalog =
                Simulation영역건물발전Catalog.CreateDefault(),
            ResourceNodes = new[]
            {
                new SimulationNatureResourceNodeInitialStateRequest
                {
                    ResourceNodeStableId = "resource:nature-tree:01",
                    H2StableId = SimulationNatureSurvivalCodes.HarvestH2StableId,
                    H1StableId = "h1-stock:nature-exploration-buffer",
                    LocalX = -6,
                    LocalZ = 8,
                },
            },
        },
        Atmosphere = new SimulationAtmosphereInitialStateRequest
        {
            ProfileStableId =
                WorldAtmosphereProfileCodes.NatureNightDay2FixtureR1,
            RuleRevision = WorldAtmosphereRuleRevisions.R1,
            ClockSourceCode = WorldAtmosphereClockSourceCodes.NatureCycleClock,
        },
    };
}
