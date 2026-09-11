using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "보편 물품 획득·장착의 권위 상태, 결정성, 저장·재생과 Local·Remote 동등성을 검증한다.",
    Boundary = "자동 시험은 canonical Scene의 실제 입력·Play Mode·Game View E7 증거를 대신하지 않는다.")]
public sealed class SimulationActorEquipmentTests
{
    [Fact]
    public void 같은_세션생성키의_다른_초기장착상태를_거부한다()
    {
        var request = CreateRequest();
        var session = new 경영SimulationSessionAggregate(request);
        session.EnsureSameCreationRequest(request);

        var conflicting = CreateRequest(request.ClientRequestId);
        conflicting.ActorEquipment!.ItemInstances[0].LocationCode =
            SimulationActorEquipmentCodes.Inventory;

        var error = Assert.Throws<SimulationConflictException>(() =>
            session.EnsureSameCreationRequest(conflicting));
        Assert.Equal("SimulationCreateRequestPayloadConflict",
            error.ErrorCode);
    }

    [Fact]
    public void MainHand_도끼를_삽으로_교체하면_장착능력도_교체된다()
    {
        const string shovelInstanceId = "item:tool:shovel:fixture";
        var request = CreateRequest();
        request.ActorEquipment!.ItemInstances = new[]
        {
            new SimulationOwnedItemInstanceInitialState
            {
                ItemInstanceStableId =
                    SimulationNatureSurvivalCodes.AxePickupStableId,
                ItemDefinitionStableId =
                    SimulationActorEquipmentCodes.AxeDefinitionStableId,
                LocationCode = SimulationActorEquipmentCodes.Inventory,
            },
            new SimulationOwnedItemInstanceInitialState
            {
                ItemInstanceStableId = shovelInstanceId,
                ItemDefinitionStableId =
                    SimulationActorEquipmentCodes.ShovelDefinitionStableId,
                LocationCode = SimulationActorEquipmentCodes.Inventory,
            },
        };
        var session = new 경영SimulationSessionAggregate(request);
        var axeEquipped = session.ConfirmActorEquipmentChange(new()
        {
            CommandId = "command:equip-axe-before-swap",
            ExpectedEquipmentRevision = 0,
            ActorStableId = "player:solo",
            OperationCode = SimulationActorEquipmentCodes.Equip,
            ItemInstanceStableId =
                SimulationNatureSurvivalCodes.AxePickupStableId,
            SlotCode = SimulationActorEquipmentCodes.MainHand,
        });

        var shovelEquipped = session.ConfirmActorEquipmentChange(new()
        {
            CommandId = "command:swap-axe-for-shovel",
            ExpectedEquipmentRevision = axeEquipped.EquipmentRevision,
            ActorStableId = "player:solo",
            OperationCode = SimulationActorEquipmentCodes.Swap,
            ItemInstanceStableId = shovelInstanceId,
            SlotCode = SimulationActorEquipmentCodes.MainHand,
            SwapItemInstanceStableId =
                SimulationNatureSurvivalCodes.AxePickupStableId,
        });

        Assert.DoesNotContain(SimulationActorEquipmentCodes.Woodcutting,
            shovelEquipped.CapabilityCodes);
        Assert.Contains(SimulationActorEquipmentCodes.TerrainGrading,
            shovelEquipped.CapabilityCodes);
        Assert.Equal(SimulationActorEquipmentCodes.Inventory,
            shovelEquipped.ItemInstances.Single(value =>
                value.ItemInstanceStableId ==
                SimulationNatureSurvivalCodes.AxePickupStableId).LocationCode);
        Assert.Equal(shovelInstanceId,
            shovelEquipped.Slots.Single(value => value.SlotCode ==
                SimulationActorEquipmentCodes.MainHand)
                .EquippedItemInstanceStableId);
    }

    [Fact]
    public void 도끼획득과_장착을_분리하고_벌목능력을_장착상태에서_판정한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());

        var acquisition = session.PreviewActorItemAcquire(new()
        {
            ObservedEquipmentRevision = 0,
            ActorStableId = "player:solo",
            ItemInstanceStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            SpecializationWorldInteractionId =
                SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId,
        });
        Assert.True(acquisition.CanConfirm);
        Assert.Equal(SimulationActorEquipmentCodes.AcquireWorldInteractionId,
            acquisition.ArchetypeWorldInteractionId);

        var acquiredSession = session.ConfirmNatureSurvivalAction(new()
        {
            CommandId = "command:acquire-axe",
            ExpectedRevision = 0,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        });
        var acquired = session.GetActorEquipmentState();
        Assert.Equal(SimulationActorEquipmentCodes.Inventory,
            Assert.Single(acquired.ItemInstances, value =>
                value.ItemCode == SimulationNatureSurvivalCodes.AxeItemCode)
                .LocationCode);
        Assert.DoesNotContain(SimulationActorEquipmentCodes.Woodcutting,
            acquired.CapabilityCodes);

        var blocked = HarvestPreview(session, acquiredSession.Revision);
        Assert.Contains(SimulationNatureSurvivalCodes.AxeRequired,
            blocked.BlockReasonCodes);

        var equipped = session.ConfirmActorEquipmentChange(new()
        {
            CommandId = "command:equip-axe",
            ExpectedEquipmentRevision = acquired.EquipmentRevision,
            ActorStableId = "player:solo",
            OperationCode = SimulationActorEquipmentCodes.Equip,
            ItemInstanceStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            SlotCode = SimulationActorEquipmentCodes.MainHand,
            SpecializationWorldInteractionId =
                SimulationNatureSurvivalCodes.BeginHarvestWorldInteractionId,
        });
        Assert.Contains(SimulationActorEquipmentCodes.Woodcutting,
            equipped.CapabilityCodes);
        Assert.Equal(SimulationNatureSurvivalCodes.AxePickupStableId,
            equipped.Slots.Single(value => value.SlotCode ==
                SimulationActorEquipmentCodes.MainHand)
                .EquippedItemInstanceStableId);
        Assert.DoesNotContain(SimulationNatureSurvivalCodes.AxeRequired,
            HarvestPreview(session, acquiredSession.Revision).BlockReasonCodes);

        var unequipped = session.ConfirmActorEquipmentChange(new()
        {
            CommandId = "command:unequip-axe",
            ExpectedEquipmentRevision = equipped.EquipmentRevision,
            ActorStableId = "player:solo",
            OperationCode = SimulationActorEquipmentCodes.Unequip,
            ItemInstanceStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            SlotCode = SimulationActorEquipmentCodes.MainHand,
        });
        Assert.DoesNotContain(SimulationActorEquipmentCodes.Woodcutting,
            unequipped.CapabilityCodes);
        Assert.Contains(SimulationNatureSurvivalCodes.AxeRequired,
            HarvestPreview(session, acquiredSession.Revision).BlockReasonCodes);
    }

    [Fact]
    public void 장착상태를_v27로_저장복원하고_명령을_결정적으로_재생한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var acquiredSession = session.ConfirmNatureSurvivalAction(new()
        {
            CommandId = "command:save:acquire",
            ExpectedRevision = 0,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        });
        var acquired = session.GetActorEquipmentState();
        var equipped = session.ConfirmActorEquipmentChange(new()
        {
            CommandId = "command:save:equip",
            ExpectedEquipmentRevision = acquired.EquipmentRevision,
            ActorStableId = "player:solo",
            OperationCode = SimulationActorEquipmentCodes.Equip,
            ItemInstanceStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            SlotCode = SimulationActorEquipmentCodes.MainHand,
        });

        var package = session.CreateSavePackage(new()
        {
            SaveStableId = "save:actor-equipment:v27",
            ExpectedRevision = acquiredSession.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V27, package.SchemaVersion);
        Assert.Equal(equipped.StateHashSha256,
            package.ActorEquipment!.StateHashSha256);

        var restored = SimulationSessionReplay.Restore(package);
        var restoredState = restored.GetActorEquipmentState();
        Assert.Equal(equipped.StateHashSha256, restoredState.StateHashSha256);
        Assert.Contains(SimulationActorEquipmentCodes.Woodcutting,
            restoredState.CapabilityCodes);
        var replayed = restored.CreateSavePackage(new()
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(package.ReplayHash, replayed.ReplayHash);
    }

    [Fact]
    public async Task LocalProcess와_RemoteHost는_같은_장착상태hash를_만든다()
    {
        var request = CreateRequest(Guid.NewGuid());
        var local = new 경영SimulationSessionAggregate(request);
        var localSession = local.ConfirmNatureSurvivalAction(new()
        {
            CommandId = "command:parity:acquire",
            ExpectedRevision = 0,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
            TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
        });
        var localAcquired = local.GetActorEquipmentState();
        var localEquipped = local.ConfirmActorEquipmentChange(new()
        {
            CommandId = "command:parity:equip",
            ExpectedEquipmentRevision = localAcquired.EquipmentRevision,
            ActorStableId = "player:solo",
            OperationCode = SimulationActorEquipmentCodes.Equip,
            ItemInstanceStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            SlotCode = SimulationActorEquipmentCodes.MainHand,
        });

        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var createdResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", request);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>();
        Assert.NotNull(created);

        var acquireResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created!.SessionStableId}/nature-survival/commands",
            new SimulationNatureSurvivalCommandRequest
            {
                CommandId = "command:parity:acquire",
                ExpectedRevision = created.Revision,
                PlayerStableId = "player:solo",
                ActionCode = SimulationNatureSurvivalCodes.AcquireAxe,
                TargetStableId = SimulationNatureSurvivalCodes.AxePickupStableId,
            });
        Assert.Equal(HttpStatusCode.OK, acquireResponse.StatusCode);
        var remoteAcquired = await client.GetFromJsonAsync<
            SimulationActorEquipmentStateSnapshot>(
            $"/api/simulation/v1/sessions/{created.SessionStableId}/actor-equipment");
        Assert.NotNull(remoteAcquired);

        var equipResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created.SessionStableId}/actor-equipment/changes/confirm",
            new SimulationActorEquipmentChangeConfirmRequest
            {
                CommandId = "command:parity:equip",
                ExpectedEquipmentRevision = remoteAcquired!.EquipmentRevision,
                ActorStableId = "player:solo",
                OperationCode = SimulationActorEquipmentCodes.Equip,
                ItemInstanceStableId =
                    SimulationNatureSurvivalCodes.AxePickupStableId,
                SlotCode = SimulationActorEquipmentCodes.MainHand,
            });
        Assert.Equal(HttpStatusCode.OK, equipResponse.StatusCode);
        var remoteEquipped = await equipResponse.Content.ReadFromJsonAsync<
            SimulationActorEquipmentStateSnapshot>();
        Assert.NotNull(remoteEquipped);
        Assert.Equal(localSession.Revision, created.Revision + 1);
        Assert.Equal(localEquipped.StateHashSha256,
            remoteEquipped!.StateHashSha256);
    }

    private static SimulationNatureSurvivalActionPreviewSnapshot HarvestPreview(
        경영SimulationSessionAggregate session, long worldRevision)
        => session.PreviewNatureSurvivalAction(new()
        {
            ObservedWorldRevision = worldRevision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
            TargetStableId = "resource:nature-tree:01",
        });

    internal static 경영SimulationSession생성Request CreateRequest(
        Guid? clientRequestId = null)
        => new()
        {
            ClientRequestId = clientRequestId ??
                Guid.Parse("79299446-e885-43de-902d-94c4df910cf1"),
            ScenarioStableId = "scenario:actor-equipment-fixture",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 20260827,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:solo",
                TerritoryStableId = "territory:nature",
                SettlementStableId = "settlement:nature-home",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 27, 0, 0, 0,
                    TimeSpan.Zero),
            },
            SpatialWorld = new Simulation공간세계InitialStateRequest
            {
                Definitions = new[]
                {
                    new Simulation공간정의InitialRequest
                    {
                        SpatialStableId =
                            SimulationNatureSurvivalCodes.ActualE5SpatialStableId(
                                SimulationNatureSurvivalCodes
                                    .AcquireAxeWorldInteractionId),
                        FacilityStableId = "facility:nature-tool-pickup",
                        AreaStableId = "area:nature-home",
                        AreaSetStableId = "area-set:nature",
                        LandscapeGraphStableId =
                            "landscape-graph:nature-survival-home.v1",
                        LandscapeNodeStableId = "nature-tool-pickup",
                        EvidenceKindCode =
                            Simulation공간근거종류Codes.LandscapeGraph,
                        AccessStateCode = Simulation공간접근상태Codes.Available,
                        CapabilityCodes = new[]
                        {
                            Simulation공간능력Codes.Traversable,
                        },
                        DefinitionRevision = "wi-nature-05.actual-e5.r1",
                        DefinitionHashSha256 =
                            "8f08298c84a82e52b8f977d6652b43472b79b3e755ee66c9698c65973ec95eef",
                        SourceStableIds = new[]
                        {
                            "wi-spatial-seedbed:nature-survival-home.v1",
                            "world-interaction:wi-nature-05",
                        },
                    },
                },
            },
            NatureSurvival = new SimulationNatureSurvivalInitialStateRequest
            {
                PlayerStableId = "player:solo",
                StartsWithAxe = false,
                ResourceNodes = new[]
                {
                    new SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId = "resource:nature-tree:01",
                        H2StableId = SimulationNatureSurvivalCodes.HarvestH2StableId,
                        H1StableId = "h1-stock:nature-exploration-buffer",
                    },
                },
            },
            ActorEquipment = new SimulationActorEquipmentInitialStateRequest
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
            },
        };
}
