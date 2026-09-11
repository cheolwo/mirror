using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Hosting;
using Ssalddel.Simulation.Persistence;
using Ssalddel.WorkflowRules;
using Ssalddel.Security;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "공식 지속 세계·비공개 협동 방·계정 명상 상태 사본의 결정성과 인증 경계를 검증한다.",
    Boundary = "자동 다중 클라이언트 시험은 실제 Unity Play Mode·Game View와 운영 배포 증거를 대신하지 않는다.")]
public sealed class SimulationOnlineWorldTests
{
    [Fact]
    public void 공식세계는_네AreaSet각32명과_전체128명구조를제공한다()
    {
        var service = CreateService();

        var directory = service.Directory();
        var official = Assert.Single(directory.Worlds);

        Assert.Equal(SimulationOnlineWorldCodes.OfficialPersistentWorld,
            official.WorldKindCode);
        Assert.True(official.AlwaysActive);
        Assert.Equal(128, official.MaximumParticipants);
        Assert.Equal(4, official.AreaSets.Length);
        Assert.All(official.AreaSets, area =>
        {
            Assert.Equal(32, area.Capacity);
            Assert.Equal(SimulationOnlineWorldCodes.RemoteHost,
                area.AuthorityLocationCode);
            Assert.Equal(SimulationOnlineWorldCodes.AuthoritySessionReserved,
                area.SessionBindingStateCode);
            Assert.StartsWith("simulation-session:",
                area.AuthoritySessionStableId);
            Assert.Equal(SimulationOnlineWorldCoordinator
                    .CalculateAuthoritySessionStableId(
                        official.WorldStableId, area.AreaSetStableId),
                area.AuthoritySessionStableId);
        });
        Assert.Equal(4, official.AreaSets.Select(value =>
            value.AuthoritySessionStableId).Distinct(StringComparer.Ordinal)
            .Count());
        Assert.Equal(SimulationOnlineWorldCodes.NatureCooperationObjectiveStableId,
            Assert.Single(official.Objectives).ObjectiveStableId);
    }

    [Fact]
    public void 연결된참가자는_AreaSet의결정적RemoteHost세션을_준비하고공유한다()
    {
        var online = CreateService();
        var world = online.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        world = JoinOfficial(online, "player:provision:a", world, 0);
        world = JoinOfficial(online, "player:provision:b", world, 1);
        var store = new InMemory경영SimulationSessionStore();
        var provisioner = new SimulationOnlineNatureSessionProvisioningService(
            store, online);

        var first = provisioner.Ensure("player:provision:a", new()
        {
            WorldStableId = world.WorldStableId,
            ExpectedOnlineWorldRevision = world.WorldRevision,
        });
        var second = provisioner.Ensure("player:provision:b", new()
        {
            WorldStableId = world.WorldStableId,
            ExpectedOnlineWorldRevision = world.WorldRevision,
        });

        Assert.Equal(world.AreaSets[0].AuthoritySessionStableId,
            first.AuthoritySessionStableId);
        Assert.Equal(first.AuthoritySessionStableId,
            second.AuthoritySessionStableId);
        Assert.Equal(first.PrimaryActorStableId,
            second.PrimaryActorStableId);
        Assert.True(first.SupportsMultipleActors);
        Assert.Equal(2, first.ParticipantActors.Length);
        Assert.All(first.ParticipantActors, actor =>
        {
            Assert.Equal(SimulationOnlineWorldCodes
                    .ParticipantActorRegistered,
                actor.RegistrationStateCode);
            Assert.True(actor.HasAuthorityInventory);
            Assert.True(actor.CanExecuteNatureWorldInteraction);
            Assert.Equal(first.AuthoritySessionStableId,
                actor.AuthoritySessionStableId);
        });
        Assert.Equal(first.ParticipantActors.Select(value =>
                value.ActorStableId),
            second.ParticipantActors.Select(value => value.ActorStableId));
        Assert.Equal(SimulationOnlineWorldCodes
                .AuthoritySessionRuntimeReadyCooperativeLogging,
            first.RuntimeStateCode);
        Assert.Equal(SimulationOnlineWorldCodes.RemoteHost,
            first.AuthorityLocationCode);
        Assert.False(first.IsOperationalState);
        var aggregate = Assert.IsType<경영SimulationSessionAggregate>(
            store.Find(first.AuthoritySessionStableId));

        var actorA = first.ParticipantActors.Single(value =>
            value.PlayerStableId == "player:provision:a").ActorStableId;
        var actorB = first.ParticipantActors.Single(value =>
            value.PlayerStableId == "player:provision:b").ActorStableId;
        var nature = new SimulationNatureSurvivalService(store);
        var begunA = nature.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "command:coop:actor-a:harvest",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = actorA,
            ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
            TargetStableId = "resource:nature-tree:01",
        });
        Assert.Throws<SimulationConflictException>(() => nature.Confirm(
            aggregate.SessionStableId, new()
            {
                CommandId = "command:coop:actor-b:conflict",
                ExpectedRevision = begunA.Revision,
                PlayerStableId = actorB,
                ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
                TargetStableId = "resource:nature-tree:01",
            }));
        var challengeA = nature.Get(aggregate.SessionStableId)
            .ActiveFocusChallenge!;
        nature.SubmitFocusTiming(aggregate.SessionStableId, new()
        {
            CommandId = "command:coop:actor-a:focus",
            ChallengeStableId = challengeA.ChallengeStableId,
            ExpectedWorldRevision = aggregate.Revision,
            ExpectedChallengeRevision = challengeA.ChallengeRevision,
            InputOffsetMillis = 500,
        });
        nature.AdvanceClock(aggregate.SessionStableId, new()
        {
            CommandId = "command:coop:actor-a:complete",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });
        var completedA = aggregate.GetActionManifestationLedger()!
            .TailRecords.Single(value => value.CommandId ==
                "command:coop:actor-a:harvest:completed");
        Assert.Equal(actorA, completedA.ActorStableId);
        Assert.Equal(250, aggregate.GetPlayerDomainProfile()!
            .명상기여기록들.Single(value =>
                value.SourceActionRecordStableId ==
                completedA.행위기록StableId).명상경험증가Milli);

        nature.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "command:coop:actor-b:harvest",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = actorB,
            ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
            TargetStableId = "resource:nature-tree:02",
        });
        var challengeB = nature.Get(aggregate.SessionStableId)
            .ActiveFocusChallenge!;
        nature.SubmitFocusTiming(aggregate.SessionStableId, new()
        {
            CommandId = "command:coop:actor-b:focus",
            ChallengeStableId = challengeB.ChallengeStableId,
            ExpectedWorldRevision = aggregate.Revision,
            ExpectedChallengeRevision = challengeB.ChallengeRevision,
            InputOffsetMillis = 500,
        });
        nature.AdvanceClock(aggregate.SessionStableId, new()
        {
            CommandId = "command:coop:actor-b:complete",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });
        var completedB = aggregate.GetActionManifestationLedger()!
            .TailRecords.Single(value => value.CommandId ==
                "command:coop:actor-b:harvest:completed");
        Assert.Equal(actorB, completedB.ActorStableId);
        Assert.Equal(250, aggregate.GetPlayerDomainProfile(actorA)!
            .명상경험Milli);
        Assert.Equal(250, aggregate.GetPlayerDomainProfile(actorB)!
            .명상경험Milli);

        var registered = nature.Get(aggregate.SessionStableId)
            .CooperativeActors;
        Assert.Equal(2, registered.Length);
        Assert.All(registered, value => Assert.True(value.HasAxe));

        var save = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:online-coop-logging",
            ExpectedRevision = aggregate.Revision,
        });
        var replayed = SimulationSessionReplay.Restore(save);
        Assert.Equal(aggregate.GetNatureSurvivalState().CooperativeActors
                .Select(value => value.ActorStableId),
            replayed.GetNatureSurvivalState().CooperativeActors
                .Select(value => value.ActorStableId));
        var replayedCompletion = replayed.GetActionManifestationLedger()!
            .TailRecords.Single(value => value.CommandId ==
                completedA.CommandId);
        Assert.Equal(completedA.기록HashSha256,
            replayedCompletion.기록HashSha256);
        Assert.Equal(250, replayed.GetPlayerDomainProfile()!
            .명상경험Milli);
        Assert.Equal(250, replayed.GetPlayerDomainProfile(actorA)!
            .명상경험Milli);
        Assert.Equal(250, replayed.GetPlayerDomainProfile(actorB)!
            .명상경험Milli);
        var reconnectQuery = new Simulation행위기록Query
        {
            WorldStableId = aggregate.GetActionManifestationLedger()!
                .WorldStableId,
            Cursor = new Simulation행위기록Cursor(),
            WorldInteractionIds = new[]
            {
                SimulationNatureSurvivalCodes.BeginHarvestWorldInteractionId,
            },
            MaxCount = 16,
        };
        Assert.Equal(aggregate.QueryActionManifestations(reconnectQuery)
                .Records.Select(value => value.기록HashSha256),
            replayed.QueryActionManifestations(reconnectQuery)
                .Records.Select(value => value.기록HashSha256));

        Assert.Throws<SimulationConflictException>(() => provisioner.Ensure(
            "player:provision:a", new()
            {
                WorldStableId = world.WorldStableId,
                ExpectedOnlineWorldRevision = world.WorldRevision - 1,
            }));

        var restartedOnline = new SimulationOnlineWorldService(
            new SeedCheckpointStore(online.CaptureCheckpoint()));
        var restartedStore = new InMemory경영SimulationSessionStore();
        var restartedProvisioner =
            new SimulationOnlineNatureSessionProvisioningService(
                restartedStore, restartedOnline);
        var restarted = restartedProvisioner.Ensure(
            "player:provision:a", new()
            {
                WorldStableId = world.WorldStableId,
                ExpectedOnlineWorldRevision = world.WorldRevision,
            });
        Assert.Equal(first.AuthoritySessionStableId,
            restarted.AuthoritySessionStableId);
        Assert.Equal(first.PrimaryActorStableId,
            restarted.PrimaryActorStableId);
        Assert.Equal(first.ParticipantActors.Select(value =>
                value.ActorStableId),
            restarted.ParticipantActors.Select(value => value.ActorStableId));
    }

    [Fact]
    public void 온라인벌목서비스는_Jwt주체Actor를결속하고_충돌과각자명상을확정한다()
    {
        var online = CreateService();
        var world = online.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        world = JoinOfficial(online, "player:logging:a", world, 81);
        world = JoinOfficial(online, "player:logging:b", world, 82);
        var store = new InMemory경영SimulationSessionStore();
        var provisioning = new
            SimulationOnlineNatureSessionProvisioningService(store, online);
        var bridge = new SimulationOnlineMeditationBridgeService(store, online);
        var logging = new SimulationOnlineCooperativeLoggingService(store,
            online, provisioning, bridge);
        var runtime = provisioning.Ensure("player:logging:a", new()
        {
            WorldStableId = world.WorldStableId,
            ExpectedOnlineWorldRevision = world.WorldRevision,
        });

        var begunA = logging.Begin("player:logging:a", new()
        {
            WorldStableId = world.WorldStableId,
            CommandId = "command:online-logging:a:begin",
            TargetResourceStableId = "resource:nature-tree:01",
            ExpectedOnlineWorldRevision = world.WorldRevision,
            ExpectedSessionWorldRevision = runtime.SessionWorldRevision,
        });
        Assert.Throws<SimulationConflictException>(() => logging.Begin(
            "player:logging:b", new()
            {
                WorldStableId = world.WorldStableId,
                CommandId = "command:online-logging:b:conflict",
                TargetResourceStableId = "resource:nature-tree:01",
                ExpectedOnlineWorldRevision = world.WorldRevision,
                ExpectedSessionWorldRevision = begunA.SessionWorldRevision,
            }));
        var focusedA = logging.Focus("player:logging:a", new()
        {
            WorldStableId = world.WorldStableId,
            CommandId = "command:online-logging:a:focus",
            ChallengeStableId = begunA.Nature.ActiveFocusChallenge!
                .ChallengeStableId,
            ExpectedChallengeRevision = begunA.Nature.ActiveFocusChallenge
                .ChallengeRevision,
            ExpectedOnlineWorldRevision = world.WorldRevision,
            ExpectedSessionWorldRevision = begunA.SessionWorldRevision,
            InputOffsetMillis = 500,
        });
        var completedA = logging.Complete("player:logging:a", new()
        {
            WorldStableId = world.WorldStableId,
            CommandId = "command:online-logging:a:complete",
            ExpectedOnlineWorldRevision = world.WorldRevision,
            ExpectedSessionWorldRevision = focusedA.SessionWorldRevision,
        });
        Assert.Equal("player:logging:a",
            completedA.AccountMeditation!.AccountPlayerStableId);
        Assert.Equal(250,
            completedA.AccountMeditation.MeditationExperienceMilli);

        world = online.GetWorld(world.WorldStableId);
        var begunB = logging.Begin("player:logging:b", new()
        {
            WorldStableId = world.WorldStableId,
            CommandId = "command:online-logging:b:begin",
            TargetResourceStableId = "resource:nature-tree:02",
            ExpectedOnlineWorldRevision = world.WorldRevision,
            ExpectedSessionWorldRevision = completedA.SessionWorldRevision,
        });
        var focusedB = logging.Focus("player:logging:b", new()
        {
            WorldStableId = world.WorldStableId,
            CommandId = "command:online-logging:b:focus",
            ChallengeStableId = begunB.Nature.ActiveFocusChallenge!
                .ChallengeStableId,
            ExpectedChallengeRevision = begunB.Nature.ActiveFocusChallenge
                .ChallengeRevision,
            ExpectedOnlineWorldRevision = world.WorldRevision,
            ExpectedSessionWorldRevision = begunB.SessionWorldRevision,
            InputOffsetMillis = 500,
        });
        var completedB = logging.Complete("player:logging:b", new()
        {
            WorldStableId = world.WorldStableId,
            CommandId = "command:online-logging:b:complete",
            ExpectedOnlineWorldRevision = world.WorldRevision,
            ExpectedSessionWorldRevision = focusedB.SessionWorldRevision,
        });
        Assert.Equal("player:logging:b",
            completedB.AccountMeditation!.AccountPlayerStableId);
        Assert.Equal(250,
            completedB.AccountMeditation.MeditationExperienceMilli);
        Assert.NotEqual(completedA.ActorStableId, completedB.ActorStableId);
        Assert.Equal(2, store.Find(runtime.AuthoritySessionStableId)!
            .GetActionManifestationLedger()!.TailRecords.Count(value =>
                value.CommandId.EndsWith(":begin:completed",
                    StringComparison.Ordinal)));
        world = online.GetWorld(world.WorldStableId);
        var reconnectFirst = logging.Reconnect("player:logging:a", new()
        {
            WorldStableId = world.WorldStableId,
            ExpectedOnlineWorldRevision = world.WorldRevision,
            MaxCount = 1,
        });
        var reconnectSecond = logging.Reconnect("player:logging:a", new()
        {
            WorldStableId = world.WorldStableId,
            ExpectedOnlineWorldRevision = world.WorldRevision,
            Cursor = reconnectFirst.ActionRecords.NextCursor,
            MaxCount = 1,
        });
        Assert.Single(reconnectFirst.ActionRecords.Records);
        Assert.Single(reconnectSecond.ActionRecords.Records);
        Assert.NotEqual(reconnectFirst.ActionRecords.Records[0]
                .행위기록StableId,
            reconnectSecond.ActionRecords.Records[0].행위기록StableId);
    }

    [Fact]
    public void 비공개방은_초대합류_소유자이탈_임시리더_전원이탈정지를보존한다()
    {
        var service = CreateService();
        var created = service.CreatePrivateRoom("player:owner",
            new SimulationPrivateRoomCreateRequest
            {
                ClientRequestId = Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"),
                CommandId = "command:room:create",
                InvitedPlayerStableIds =
                    ["player:guest-a", "player:guest-b"],
            });
        var world = created.World;
        Assert.Equal(SimulationOnlineWorldCodes.PrivateHostedRoom,
            world.WorldKindCode);
        Assert.Equal(3, world.Participants.Length);

        world = Join(service, "player:guest-a", world);
        var privateSessionStore = new InMemory경영SimulationSessionStore();
        var privateProvisioning =
            new SimulationOnlineNatureSessionProvisioningService(
                privateSessionStore, service);
        var ownerRuntime = privateProvisioning.Ensure("player:owner", new()
        {
            WorldStableId = world.WorldStableId,
            ExpectedOnlineWorldRevision = world.WorldRevision,
        });
        var guestRuntime = privateProvisioning.Ensure("player:guest-a", new()
        {
            WorldStableId = world.WorldStableId,
            ExpectedOnlineWorldRevision = world.WorldRevision,
        });
        Assert.Equal(ownerRuntime.AuthoritySessionStableId,
            guestRuntime.AuthoritySessionStableId);
        Assert.Equal(2, ownerRuntime.ParticipantActors.Length);
        Assert.True(ownerRuntime.SupportsMultipleActors);
        world = service.Leave("player:owner",
            new SimulationOnlineWorldLeaveRequest
            {
                CommandId = "command:room:owner-leave",
                WorldStableId = world.WorldStableId,
                ExpectedWorldRevision = world.WorldRevision,
            }).World;
        Assert.Equal("player:guest-a",
            world.TemporaryLeaderPlayerStableId);
        Assert.Equal(SimulationOnlineWorldCodes.TemporaryLeader,
            world.Participants.Single(value =>
                value.PlayerStableId == "player:guest-a").AuthorityRoleCode);

        world = Join(service, "player:guest-b", world);
        world = service.Leave("player:guest-a",
            new SimulationOnlineWorldLeaveRequest
            {
                CommandId = "command:room:guest-a-leave",
                WorldStableId = world.WorldStableId,
                ExpectedWorldRevision = world.WorldRevision,
            }).World;
        Assert.Equal("player:guest-b",
            world.TemporaryLeaderPlayerStableId);

        world = service.Leave("player:guest-b",
            new SimulationOnlineWorldLeaveRequest
            {
                CommandId = "command:room:guest-b-leave",
                WorldStableId = world.WorldStableId,
                ExpectedWorldRevision = world.WorldRevision,
            }).World;
        Assert.Equal(SimulationOnlineWorldCodes.Suspended, world.StateCode);

        var checkpoint = service.CaptureCheckpoint();
        var restored = new SimulationOnlineWorldService(
            new SeedCheckpointStore(checkpoint));
        var restoredWorld = restored.GetWorld(world.WorldStableId);
        Assert.Equal(SimulationOnlineWorldCodes.Suspended,
            restoredWorld.StateCode);
        Assert.Equal(world.AreaSets[0].AuthoritySessionStableId,
            restoredWorld.AreaSets[0].AuthoritySessionStableId);
        Assert.Equal(world.AreaSets[0].StateHashSha256,
            restoredWorld.AreaSets[0].StateHashSha256);
    }

    [Fact]
    public void 공식AreaSet은_32명을수용하고_33번째를대기시킨뒤_빈자리에승격한다()
    {
        var service = CreateService();
        var world = service.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        var areaId = world.AreaSets[0].AreaSetStableId;

        for (var index = 0; index < 33; index++)
        {
            var result = service.Join($"player:official:{index:D2}",
                new SimulationOnlineWorldJoinRequest
                {
                    CommandId = $"command:official:join:{index:D2}",
                    WorldStableId = world.WorldStableId,
                    AreaSetStableId = areaId,
                    ExpectedWorldRevision = world.WorldRevision,
                    ExpectedAreaSetRevision = world.AreaSets[0].PartitionRevision,
                });
            world = result.World;
            Assert.Equal(index == 32
                    ? SimulationOnlineWorldCodes.Queued
                    : SimulationOnlineWorldCodes.Joined,
                result.ResultCode);
        }

        Assert.Equal(32, world.AreaSets[0].ConnectedParticipants);
        Assert.Equal(1, world.AreaSets[0].WaitingParticipants);

        world = service.Leave("player:official:00",
            new SimulationOnlineWorldLeaveRequest
            {
                CommandId = "command:official:leave:00",
                WorldStableId = world.WorldStableId,
                ExpectedWorldRevision = world.WorldRevision,
            }).World;
        Assert.Equal(32, world.AreaSets[0].ConnectedParticipants);
        Assert.Equal(0, world.AreaSets[0].WaitingParticipants);
        Assert.Equal(SimulationOnlineWorldCodes.Connected,
            world.Participants.Single(value =>
                value.PlayerStableId == "player:official:32")
                .ParticipantStateCode);
    }

    [Fact]
    public void 파티고정신호는_연결된구성원만남기며_명상은온라인세계사이에만계정공유된다()
    {
        var service = CreateService();
        var world = service.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        world = JoinOfficial(service, "player:a", world, 0);
        world = JoinOfficial(service, "player:b", world, 1);

        world = service.CreateParty("player:a",
            new SimulationOnlinePartyCreateRequest
            {
                CommandId = "command:party:create",
                WorldStableId = world.WorldStableId,
                PartyStableId = "party:nature:a-b",
                MemberPlayerStableIds = ["player:b"],
                ExpectedWorldRevision = world.WorldRevision,
            }).World;
        var signaled = service.SendFixedSignal("player:b",
            new SimulationFixedSignalSendRequest
            {
                CommandId = "command:signal:threat",
                WorldStableId = world.WorldStableId,
                PartyStableId = "party:nature:a-b",
                SignalCode = SimulationOnlineWorldCodes.ThreatFound,
                ExpectedWorldRevision = world.WorldRevision,
            });
        world = signaled.World;
        Assert.Equal(SimulationOnlineWorldCodes.ThreatFound,
            Assert.Single(world.RecentSignals).SignalCode);

        var first = service.ApplyVerifiedMeditation(
            new SimulationVerifiedMeditationContributionRequest
            {
                WorldStableId = world.WorldStableId,
                PlayerStableId = "player:a",
                SourceActionRecordStableId = "action:official:focus:1",
                MeditationExperienceMilli = 250,
                SourceActionWorldRevision = 1,
                ExpectedOnlineWorldRevision = world.WorldRevision,
                RuleRevision = "meditation-progress.r2",
            });
        var duplicate = service.ApplyVerifiedMeditation(
            new SimulationVerifiedMeditationContributionRequest
            {
                WorldStableId = world.WorldStableId,
                PlayerStableId = "player:a",
                SourceActionRecordStableId = "action:official:focus:1",
                MeditationExperienceMilli = 250,
                SourceActionWorldRevision = 1,
                ExpectedOnlineWorldRevision = world.WorldRevision,
                RuleRevision = "meditation-progress.r2",
            });
        Assert.True(first.Applied);
        Assert.False(duplicate.Applied);
        Assert.Equal(250,
            service.GetAccountMeditation("player:a")
                .MeditationExperienceMilli);

        var room = service.CreatePrivateRoom("player:a",
            new SimulationPrivateRoomCreateRequest
            {
                ClientRequestId = Guid.Parse(
                    "22222222-2222-2222-2222-222222222222"),
                CommandId = "command:second-room:create",
                InvitedPlayerStableIds = ["player:b"],
            }).World;
        service.ApplyVerifiedMeditation(
            new SimulationVerifiedMeditationContributionRequest
            {
                WorldStableId = room.WorldStableId,
                PlayerStableId = "player:a",
                SourceActionRecordStableId = "action:private:focus:1",
                MeditationExperienceMilli = 250,
                SourceActionWorldRevision = 2,
                ExpectedOnlineWorldRevision = room.WorldRevision,
                RuleRevision = "meditation-progress.r2",
            });
        var account = service.GetAccountMeditation("player:a");
        Assert.Equal(500, account.MeditationExperienceMilli);
        Assert.Equal(2, account.Contributions.Length);
    }

    [Fact]
    public void 실제벌목집중기록만_온라인명상으로동기화되고_재전송과세계간중복을막는다()
    {
        var online = CreateService();
        var world = online.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        var aggregate = CreateFocusedNatureAggregate(
            SessionClientRequestId(
                world.AreaSets[0].AuthoritySessionStableId),
            world.AreaSets[0].AreaSetStableId);
        var actionRecord = aggregate.GetActionManifestationLedger()!
            .TailRecords.Single(value => value.CommandId ==
                "command:online-focus:harvest:completed");
        var sessionStore = new InMemory경영SimulationSessionStore();
        sessionStore.Restore(aggregate);
        world = JoinOfficial(online, "player:solo", world, 41);
        var bridge = new SimulationOnlineMeditationBridgeService(
            sessionStore, online);
        var request = new SimulationOnlineMeditationSyncRequest
        {
            WorldStableId = world.WorldStableId,
            SessionStableId = aggregate.SessionStableId,
            SourceActionRecordStableId = actionRecord.행위기록StableId,
            ExpectedOnlineWorldRevision = world.WorldRevision,
        };

        var applied = bridge.Sync("player:solo", request);
        var duplicate = bridge.Sync("player:solo", request);

        Assert.True(applied.Applied);
        Assert.False(duplicate.Applied);
        Assert.Equal(250,
            applied.AccountMeditation!.MeditationExperienceMilli);
        var contribution = Assert.Single(
            applied.AccountMeditation.Contributions);
        Assert.Equal(actionRecord.AfterWorldRevision,
            contribution.SourceActionWorldRevision);
        Assert.Equal(world.WorldRevision,
            contribution.AppliedOnlineWorldRevision);
        Assert.Null(typeof(SimulationOnlineMeditationSyncRequest)
            .GetProperty(nameof(SimulationVerifiedMeditationContributionRequest
                .MeditationExperienceMilli)));

        var privateRoom = online.CreatePrivateRoom("player:solo",
            new SimulationPrivateRoomCreateRequest
            {
                ClientRequestId = Guid.Parse(
                    "77777777-7777-7777-7777-777777777777"),
                CommandId = "command:online-focus:private-room",
                InvitedPlayerStableIds = ["player:online-focus:guest"],
            }).World;
        Assert.Throws<SimulationConflictException>(() => bridge.Sync(
            "player:solo", new SimulationOnlineMeditationSyncRequest
            {
                WorldStableId = privateRoom.WorldStableId,
                SessionStableId = aggregate.SessionStableId,
                SourceActionRecordStableId = actionRecord.행위기록StableId,
                ExpectedOnlineWorldRevision = privateRoom.WorldRevision,
            }));
        Assert.Throws<SimulationConflictException>(() => bridge.Sync(
            "player:forged", request));
        Assert.Equal(250, online.GetAccountMeditation("player:solo")
            .MeditationExperienceMilli);
    }

    [Fact]
    public void 파티AreaSet전환은_수용인원이부족하면전원을원래영역에남긴다()
    {
        var service = CreateService();
        var world = service.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        var sourceAreaId = world.AreaSets[0].AreaSetStableId;
        var targetAreaId = world.AreaSets[1].AreaSetStableId;
        world = JoinOfficialArea(service, "player:party:a", world,
            sourceAreaId, "party-a");
        world = JoinOfficialArea(service, "player:party:b", world,
            sourceAreaId, "party-b");
        world = service.CreateParty("player:party:a",
            new SimulationOnlinePartyCreateRequest
            {
                CommandId = "command:party:handover:create",
                WorldStableId = world.WorldStableId,
                PartyStableId = "party:handover",
                MemberPlayerStableIds = ["player:party:b"],
                ExpectedWorldRevision = world.WorldRevision,
            }).World;
        for (var index = 0; index < 31; index++)
            world = JoinOfficialArea(service,
                $"player:target:{index:D2}", world, targetAreaId,
                $"target-{index:D2}");

        var blockedRevision = world.WorldRevision;
        Assert.Throws<SimulationConflictException>(() =>
            service.TransferAreaSet("player:party:a",
                Transfer(world, targetAreaId,
                    "command:party:handover:blocked")));
        world = service.GetWorld(world.WorldStableId);
        Assert.Equal(blockedRevision, world.WorldRevision);
        Assert.All(world.Participants.Where(value =>
                value.PartyStableId == "party:handover"),
            value => Assert.Equal(sourceAreaId, value.AreaSetStableId));

        world = service.Leave("player:target:00",
            new SimulationOnlineWorldLeaveRequest
            {
                CommandId = "command:target:leave",
                WorldStableId = world.WorldStableId,
                ExpectedWorldRevision = world.WorldRevision,
            }).World;
        world = service.TransferAreaSet("player:party:a",
            Transfer(world, targetAreaId,
                "command:party:handover:applied")).World;

        Assert.Equal(32, world.AreaSets.Single(value =>
            value.AreaSetStableId == targetAreaId).ConnectedParticipants);
        Assert.All(world.Participants.Where(value =>
                value.PartyStableId == "party:handover"),
            value => Assert.Equal(targetAreaId, value.AreaSetStableId));
    }

    [Fact]
    public void 퇴장한플레이어는_온라인명상기여를추가할수없다()
    {
        var service = CreateService();
        var world = service.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        world = JoinOfficial(service, "player:left", world, 50);
        world = service.Leave("player:left",
            new SimulationOnlineWorldLeaveRequest
            {
                CommandId = "command:left:leave",
                WorldStableId = world.WorldStableId,
                ExpectedWorldRevision = world.WorldRevision,
            }).World;

        Assert.Throws<SimulationConflictException>(() =>
            service.ApplyVerifiedMeditation(
                new SimulationVerifiedMeditationContributionRequest
                {
                    WorldStableId = world.WorldStableId,
                    PlayerStableId = "player:left",
                    SourceActionRecordStableId = "action:left:focus",
                    MeditationExperienceMilli = 250,
                    SourceActionWorldRevision = 3,
                    ExpectedOnlineWorldRevision = world.WorldRevision,
                    RuleRevision = "meditation-progress.r2",
                }));
    }

    [Fact]
    public void 공동목표기여는_공식세계의연결참가자와권위행위기록만받는다()
    {
        var service = CreateService();
        var world = service.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        world = JoinOfficial(service, "player:objective", world, 60);
        var request = new SimulationVerifiedObjectiveContributionRequest
        {
            WorldStableId = world.WorldStableId,
            ObjectiveStableId = SimulationOnlineWorldCodes
                .NatureCooperationObjectiveStableId,
            PlayerStableId = "player:objective",
            SourceActionRecordStableId = "action:objective:verified",
            ContributionUnits = 25,
            AppliedWorldRevision = world.WorldRevision,
        };

        var applied = service.ApplyVerifiedObjectiveContribution(request);
        var duplicate = service.ApplyVerifiedObjectiveContribution(request);
        Assert.True(applied.Applied);
        Assert.False(duplicate.Applied);
        Assert.Equal(25, Assert.Single(duplicate.World.Objectives)
            .CurrentContributionUnits);

        var room = service.CreatePrivateRoom("player:objective",
            new SimulationPrivateRoomCreateRequest
            {
                ClientRequestId = Guid.Parse(
                    "66666666-6666-6666-6666-666666666666"),
                CommandId = "command:objective:private-room",
                InvitedPlayerStableIds = ["player:objective:guest"],
            }).World;
        Assert.Throws<SimulationConflictException>(() =>
            service.ApplyVerifiedObjectiveContribution(
                new SimulationVerifiedObjectiveContributionRequest
                {
                    WorldStableId = room.WorldStableId,
                    ObjectiveStableId = SimulationOnlineWorldCodes
                        .NatureCooperationObjectiveStableId,
                    PlayerStableId = "player:objective",
                    SourceActionRecordStableId = "action:objective:private",
                    ContributionUnits = 25,
                    AppliedWorldRevision = room.WorldRevision,
                }));
    }

    [Fact]
    public void 상태사본Hash변조는복원을거부하고_명령재전송은중복적용하지않는다()
    {
        var service = CreateService();
        var request = new SimulationPrivateRoomCreateRequest
        {
            ClientRequestId = Guid.Parse(
                "33333333-3333-3333-3333-333333333333"),
            CommandId = "command:idempotent-room:create",
            InvitedPlayerStableIds = ["player:guest"],
        };
        var first = service.CreatePrivateRoom("player:owner", request);
        var duplicate = service.CreatePrivateRoom("player:owner", request);
        Assert.True(first.Applied);
        Assert.False(duplicate.Applied);
        Assert.Equal(first.World.WorldRevision,
            duplicate.World.WorldRevision);

        var checkpoint = service.CaptureCheckpoint();
        checkpoint.Worlds[0].WorldRevision++;
        Assert.Throws<SimulationConflictException>(() =>
            new SimulationOnlineWorldCoordinator(checkpoint));
    }

    [Fact]
    public void SimulationSessionDb는_온라인세계상태사본과계정명상을재기동복원한다()
    {
        var options = new DbContextOptionsBuilder<SimulationSessionDbContext>()
            .UseInMemoryDatabase("online-world-" + Guid.NewGuid().ToString("N"))
            .Options;
        var persistence = new SimulationOnlineWorldCheckpointStore(
            new TestDbContextFactory(options));
        var service = new SimulationOnlineWorldService(persistence);
        var room = service.CreatePrivateRoom("player:persistent",
            new SimulationPrivateRoomCreateRequest
            {
                ClientRequestId = Guid.Parse(
                    "55555555-5555-5555-5555-555555555555"),
                CommandId = "command:persistent-room:create",
                InvitedPlayerStableIds = ["player:persistent-guest"],
            }).World;
        service.ApplyVerifiedMeditation(
            new SimulationVerifiedMeditationContributionRequest
            {
                WorldStableId = room.WorldStableId,
                PlayerStableId = "player:persistent",
                SourceActionRecordStableId = "action:persistent:focus",
                MeditationExperienceMilli = 250,
                SourceActionWorldRevision = 4,
                ExpectedOnlineWorldRevision = room.WorldRevision,
                RuleRevision = "meditation-progress.r2",
            });

        var restarted = new SimulationOnlineWorldService(
            new SimulationOnlineWorldCheckpointStore(
                new TestDbContextFactory(options)));

        Assert.Equal(room.StateHashSha256,
            restarted.GetWorld(room.WorldStableId).StateHashSha256);
        Assert.Equal(250, restarted.GetAccountMeditation(
            "player:persistent").MeditationExperienceMilli);
    }

    [Fact]
    public async Task 온라인세계Http는_익명요청을거부하고_Jwt주체로방소유자를확정한다()
    {
        const string secret =
            "ssalddel-unified-host-test-secret-key-2026";
        using var factory = new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("TestingAuthenticated");
                builder.ConfigureAppConfiguration(
                    (_, configuration) => configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SsalddelExecution:Mode"] = "Operational",
                            ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "false",
                            ["SimulationSharedPublicData:Enabled"] = "false",
                            ["SimulationWorldDerivationDatabase:Enabled"] = "false",
                            ["SimulationSessionDatabase:Enabled"] = "false",
                        }));
            });
        using var anonymous = factory.CreateClient();
        var configuredIdentity = factory.Services.GetRequiredService<
            IOptions<JwtOptions>>().Value;
        Assert.Equal("Ssalddel.Tests", configuredIdentity.Issuer);
        var bearer = factory.Services.GetRequiredService<
            IOptionsMonitor<JwtBearerOptions>>().Get(
                JwtBearerDefaults.AuthenticationScheme);
        var bearerKey = Assert.IsType<SymmetricSecurityKey>(
            bearer.TokenValidationParameters.IssuerSigningKey);
        Assert.Equal(Encoding.UTF8.GetBytes(secret), bearerKey.Key);
        var denied = await anonymous.GetAsync(
            "/api/simulation/v1/online-worlds");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token(secret,
                "player:http-owner"));
        var directory = await client.GetAsync(
            "/api/simulation/v1/online-worlds");
        Assert.True(directory.StatusCode == HttpStatusCode.OK,
            directory.StatusCode + " " + string.Join(";", directory.Headers
                .WwwAuthenticate.Select(value => value.ToString())));
        var created = await client.PostAsJsonAsync(
            "/api/simulation/v1/online-worlds/private-rooms",
            new SimulationPrivateRoomCreateRequest
            {
                ClientRequestId = Guid.Parse(
                    "44444444-4444-4444-4444-444444444444"),
                CommandId = "command:http-room:create",
                InvitedPlayerStableIds = ["player:http-guest"],
            });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<
            SimulationOnlineWorldMutationResult>();
        Assert.Equal("player:http-owner",
            body!.World.OwnerPlayerStableId);

        var online = factory.Services.GetRequiredService<
            SimulationOnlineWorldService>();
        var official = online.GetWorld(
            SimulationOnlineWorldCodes.NatureCooperationWorldStableId);
        var focused = CreateFocusedNatureAggregate(
            SessionClientRequestId(
                official.AreaSets[0].AuthoritySessionStableId),
            official.AreaSets[0].AreaSetStableId);
        factory.Services.GetRequiredService<I경영SimulationSessionStore>()
            .Restore(focused);
        using var focusClient = factory.CreateClient();
        focusClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token(secret,
                "player:solo"));
        var joined = await focusClient.PostAsJsonAsync(
            $"/api/simulation/v1/online-worlds/{official.WorldStableId}/joins",
            new SimulationOnlineWorldJoinRequest
            {
                CommandId = "command:http-online-focus:join",
                WorldStableId = official.WorldStableId,
                AreaSetStableId = official.AreaSets[0].AreaSetStableId,
                ExpectedWorldRevision = official.WorldRevision,
                ExpectedAreaSetRevision =
                    official.AreaSets[0].PartitionRevision,
            });
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        var joinedBody = await joined.Content.ReadFromJsonAsync<
            SimulationOnlineWorldMutationResult>();
        var provisioned = await focusClient.PostAsJsonAsync(
            $"/api/simulation/v1/online-worlds/{official.WorldStableId}/authority-session-provisions",
            new SimulationOnlineAuthoritySessionProvisionRequest
            {
                WorldStableId = official.WorldStableId,
                ExpectedOnlineWorldRevision =
                    joinedBody!.World.WorldRevision,
            });
        Assert.Equal(HttpStatusCode.OK, provisioned.StatusCode);
        var provisionedBody = await provisioned.Content.ReadFromJsonAsync<
            SimulationOnlineAuthoritySessionRuntimeSnapshot>();
        Assert.Equal(focused.SessionStableId,
            provisionedBody!.AuthoritySessionStableId);
        Assert.True(provisionedBody.SupportsMultipleActors);
        var actionRecord = focused.GetActionManifestationLedger()!
            .TailRecords.Single(value => value.CommandId ==
                "command:online-focus:harvest:completed");
        var synchronized = await focusClient.PostAsJsonAsync(
            $"/api/simulation/v1/online-worlds/{official.WorldStableId}/meditation-syncs",
            new SimulationOnlineMeditationSyncRequest
            {
                WorldStableId = official.WorldStableId,
                SessionStableId = focused.SessionStableId,
                SourceActionRecordStableId = actionRecord.행위기록StableId,
                ExpectedOnlineWorldRevision =
                    joinedBody.World.WorldRevision,
            });
        Assert.True(synchronized.StatusCode == HttpStatusCode.OK,
            synchronized.StatusCode + " "
            + await synchronized.Content.ReadAsStringAsync());
        var synchronizedBody = await synchronized.Content.ReadFromJsonAsync<
            SimulationOnlineWorldMutationResult>();
        Assert.True(synchronizedBody!.Applied);
        Assert.Equal(250,
            synchronizedBody.AccountMeditation!.MeditationExperienceMilli);

        var beganLogging = await focusClient.PostAsJsonAsync(
            $"/api/simulation/v1/online-worlds/{official.WorldStableId}/logging/begins",
            new SimulationOnlineLoggingBeginRequest
            {
                WorldStableId = official.WorldStableId,
                CommandId = "command:http-coop-logging:begin",
                TargetResourceStableId = "resource:nature-tree:02",
                ExpectedOnlineWorldRevision =
                    synchronizedBody.World.WorldRevision,
                ExpectedSessionWorldRevision =
                    provisionedBody.SessionWorldRevision,
            });
        Assert.Equal(HttpStatusCode.OK, beganLogging.StatusCode);
        var beganLoggingBody = await beganLogging.Content.ReadFromJsonAsync<
            SimulationOnlineLoggingResultSnapshot>();
        var focusedLogging = await focusClient.PostAsJsonAsync(
            $"/api/simulation/v1/online-worlds/{official.WorldStableId}/logging/focus-attempts",
            new SimulationOnlineLoggingFocusRequest
            {
                WorldStableId = official.WorldStableId,
                CommandId = "command:http-coop-logging:focus",
                ChallengeStableId = beganLoggingBody!.Nature
                    .ActiveFocusChallenge!.ChallengeStableId,
                ExpectedChallengeRevision = beganLoggingBody.Nature
                    .ActiveFocusChallenge.ChallengeRevision,
                ExpectedOnlineWorldRevision =
                    synchronizedBody.World.WorldRevision,
                ExpectedSessionWorldRevision =
                    beganLoggingBody.SessionWorldRevision,
                InputOffsetMillis = 500,
            });
        Assert.Equal(HttpStatusCode.OK, focusedLogging.StatusCode);
        var focusedLoggingBody = await focusedLogging.Content.ReadFromJsonAsync<
            SimulationOnlineLoggingResultSnapshot>();
        var completedLogging = await focusClient.PostAsJsonAsync(
            $"/api/simulation/v1/online-worlds/{official.WorldStableId}/logging/completions",
            new SimulationOnlineLoggingCompleteRequest
            {
                WorldStableId = official.WorldStableId,
                CommandId = "command:http-coop-logging:complete",
                ExpectedOnlineWorldRevision =
                    synchronizedBody.World.WorldRevision,
                ExpectedSessionWorldRevision =
                    focusedLoggingBody!.SessionWorldRevision,
            });
        Assert.Equal(HttpStatusCode.OK, completedLogging.StatusCode);
        var completedLoggingBody = await completedLogging.Content
            .ReadFromJsonAsync<SimulationOnlineLoggingResultSnapshot>();
        Assert.Equal(500, completedLoggingBody!.AccountMeditation!
            .MeditationExperienceMilli);
        Assert.Equal(completedLoggingBody.ActorStableId,
            completedLoggingBody.CompletedActionRecord!.ActorStableId);
        var afterLoggingWorld = online.GetWorld(official.WorldStableId);
        var reconnectedLogging = await focusClient.PostAsJsonAsync(
            $"/api/simulation/v1/online-worlds/{official.WorldStableId}/logging/reconnects",
            new SimulationOnlineLoggingReconnectRequest
            {
                WorldStableId = official.WorldStableId,
                ExpectedOnlineWorldRevision =
                    afterLoggingWorld.WorldRevision,
                MaxCount = 16,
            });
        Assert.Equal(HttpStatusCode.OK, reconnectedLogging.StatusCode);
        var reconnectedBody = await reconnectedLogging.Content
            .ReadFromJsonAsync<SimulationOnlineLoggingReconnectSnapshot>();
        Assert.Contains(reconnectedBody!.ActionRecords.Records, value =>
            value.행위기록StableId == completedLoggingBody
                .CompletedActionRecord.행위기록StableId);
    }

    private static SimulationOnlineWorldService CreateService()
        => new(new InMemorySimulationOnlineWorldCheckpointStore());

    private static 경영SimulationSessionAggregate CreateFocusedNatureAggregate(
        Guid clientRequestId, string areaSetStableId)
    {
        var request = new 경영SimulationSession생성Request
        {
            ClientRequestId = clientRequestId,
            ScenarioStableId = "scenario:online-focus-fixture",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 1234,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:solo",
                TerritoryStableId = "territory:nature",
                SettlementStableId = "settlement:nature-home",
                GameDateStartsOn = new DateTimeOffset(
                    2026, 8, 28, 0, 0, 0, TimeSpan.Zero),
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
                        AreaSetStableId = areaSetStableId,
                        LandscapeGraphStableId =
                            "landscape-graph:nature-survival-home.v1",
                        LandscapeNodeStableId = "nature-tool-pickup",
                        EvidenceKindCode =
                            Simulation공간근거종류Codes.LandscapeGraph,
                        AccessStateCode =
                            Simulation공간접근상태Codes.Available,
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
                AreaSetStableId = areaSetStableId,
                ProfileRevision =
                    SimulationNatureSurvivalCodes.ProfileRevisionR5,
                BuildingProgressionCatalog =
                    Simulation영역건물발전Catalog.CreateDefault(),
                ResourceNodes = Enumerable.Range(1, 6).Select(index =>
                    new SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId =
                            $"resource:nature-tree:{index:00}",
                        H2StableId =
                            SimulationNatureSurvivalCodes.HarvestH2StableId,
                        H1StableId =
                            "h1-stock:nature-exploration-buffer",
                        LocalX = -8 + index * 2,
                        LocalZ = 8,
                    }).ToArray(),
            },
            NatureMind = new SimulationNatureMindInitialStateRequest
            {
                Players = new[]
                {
                    new SimulationNatureMindPlayerInitialStateRequest
                    {
                        PlayerStableId = "player:solo",
                    },
                },
            },
        };
        request.SpatialWorld.Definitions = request.SpatialWorld.Definitions
            .Concat(PyeongchangSimulation공간상호작용Fixture
                .CreateNatureDroppedTimberActualE5().Definitions)
            .ToArray();
        var aggregate = new 경영SimulationSessionAggregate(request);
        var sessionStore = new InMemory경영SimulationSessionStore();
        sessionStore.Restore(aggregate);
        var service = new SimulationNatureSurvivalService(sessionStore);
        var started = service.Confirm(aggregate.SessionStableId, new()
        {
            CommandId = "command:online-focus:harvest",
            ExpectedRevision = aggregate.Revision,
            PlayerStableId = "player:solo",
            ActionCode = SimulationNatureSurvivalCodes.BeginHarvest,
            TargetStableId = "resource:nature-tree:01",
        });
        var offered = service.Get(aggregate.SessionStableId)
            .ActiveFocusChallenge!;
        service.SubmitFocusTiming(aggregate.SessionStableId, new()
        {
            CommandId = "command:online-focus:attempt",
            ChallengeStableId = offered.ChallengeStableId,
            ExpectedWorldRevision = started.Revision,
            ExpectedChallengeRevision = offered.ChallengeRevision,
            InputOffsetMillis = 500,
        });
        service.AdvanceClock(aggregate.SessionStableId, new()
        {
            CommandId = "command:online-focus:complete",
            ExpectedRevision = aggregate.Revision,
            ElapsedRealtimeSeconds =
                NatureSurvivalRules.HarvestWorkSeconds,
            WorkInputHeld = true,
        });
        return aggregate;
    }

    private static Guid SessionClientRequestId(string sessionStableId)
        => Guid.ParseExact(sessionStableId.Substring(
            "simulation-session:".Length), "N");

    private static SimulationOnlineWorldStateSnapshot Join(
        SimulationOnlineWorldService service, string player,
        SimulationOnlineWorldStateSnapshot world)
        => service.Join(player, new SimulationOnlineWorldJoinRequest
        {
            CommandId = "command:join:" + player,
            WorldStableId = world.WorldStableId,
            AreaSetStableId = world.AreaSets[0].AreaSetStableId,
            ExpectedWorldRevision = world.WorldRevision,
            ExpectedAreaSetRevision = world.AreaSets[0].PartitionRevision,
        }).World;

    private static SimulationOnlineWorldStateSnapshot JoinOfficial(
        SimulationOnlineWorldService service, string player,
        SimulationOnlineWorldStateSnapshot world, int sequence)
        => service.Join(player, new SimulationOnlineWorldJoinRequest
        {
            CommandId = "command:official-party-join:" + sequence,
            WorldStableId = world.WorldStableId,
            AreaSetStableId = world.AreaSets[0].AreaSetStableId,
            ExpectedWorldRevision = world.WorldRevision,
            ExpectedAreaSetRevision = world.AreaSets[0].PartitionRevision,
        }).World;

    private static SimulationOnlineWorldStateSnapshot JoinOfficialArea(
        SimulationOnlineWorldService service, string player,
        SimulationOnlineWorldStateSnapshot world, string areaSetStableId,
        string commandSuffix)
    {
        var area = world.AreaSets.Single(value =>
            value.AreaSetStableId == areaSetStableId);
        return service.Join(player, new SimulationOnlineWorldJoinRequest
        {
            CommandId = "command:official-area-join:" + commandSuffix,
            WorldStableId = world.WorldStableId,
            AreaSetStableId = areaSetStableId,
            ExpectedWorldRevision = world.WorldRevision,
            ExpectedAreaSetRevision = area.PartitionRevision,
        }).World;
    }

    private static SimulationOnlineAreaSetTransferRequest Transfer(
        SimulationOnlineWorldStateSnapshot world, string targetAreaSetStableId,
        string commandId)
    {
        var actor = world.Participants.Single(value =>
            value.PlayerStableId == "player:party:a");
        return new SimulationOnlineAreaSetTransferRequest
        {
            CommandId = commandId,
            WorldStableId = world.WorldStableId,
            TargetAreaSetStableId = targetAreaSetStableId,
            ExpectedWorldRevision = world.WorldRevision,
            ExpectedSourceAreaSetRevision = world.AreaSets.Single(value =>
                value.AreaSetStableId == actor.AreaSetStableId)
                .PartitionRevision,
            ExpectedTargetAreaSetRevision = world.AreaSets.Single(value =>
                value.AreaSetStableId == targetAreaSetStableId)
                .PartitionRevision,
        };
    }

    private static string Token(string secret, string player)
    {
        var token = new JwtSecurityToken(
            issuer: "Ssalddel.Tests",
            audience: "Ssalddel.Client.Tests",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, player),
                new Claim(JwtRegisteredClaimNames.Sub, player),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class SeedCheckpointStore(
        SimulationOnlineWorldCheckpointSnapshot checkpoint)
        : ISimulationOnlineWorldCheckpointStore
    {
        private SimulationOnlineWorldCheckpointSnapshot value = checkpoint;

        public SimulationOnlineWorldCheckpointSnapshot? Find() => value;

        public void Save(SimulationOnlineWorldCheckpointSnapshot candidate)
            => value = candidate;
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<SimulationSessionDbContext> options)
        : IDbContextFactory<SimulationSessionDbContext>
    {
        public SimulationSessionDbContext CreateDbContext() => new(options);
    }
}
