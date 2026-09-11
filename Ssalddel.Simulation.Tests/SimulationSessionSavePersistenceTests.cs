using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Persistence;
using Ssalddel.Simulation.Hosting;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationSessionSavePersistenceTests
{
    private const string Session =
        "simulation-session:11111111111111111111111111111111";

    [Fact]
    public void 저장자료는_새Service에서도역할카드활동까지복원한다()
    {
        var factory = CreateFactory();
        EnsureCreated(factory);
        var saveStore = new SimulationSessionSaveStore(factory);
        var sourceSessions = new InMemory경영SimulationSessionStore();
        var sourceService = new 경영SimulationSessionService(
            sourceSessions, saveStore);
        sourceService.Create(CreateRequest());
        var source = Assert.IsType<경영SimulationSessionAggregate>(
            sourceSessions.Find(Session));
        source.EquipTeamRoleCard(new SimulationTeamRoleCardEquipRequest
        {
            ClientRequestId = Guid.Parse(
                "22222222-2222-2222-2222-222222222222"),
            ExpectedRevision = 0,
            ExpectedTeamPolicyRevision = 3,
            RequestingActorStableId = "actor:farmer",
            TargetActorStableId = "actor:explorer",
            CardCopyStableId = "card-copy:exploration",
            SlotCode = SimulationTeamRoleCardCodes.Primary,
        });
        source.StartTeamActivity(new SimulationTeamActivityStartRequest
        {
            ClientRequestId = Guid.Parse(
                "33333333-3333-3333-3333-333333333333"),
            ExpectedRevision = 1,
            ExpectedTeamPolicyRevision = 3,
            ActorStableId = "actor:explorer",
            CardCopyStableId = "card-copy:exploration",
            ActivityRoleCode = SimulationTeamRoleCardCodes.Exploration,
            ActivityStableId = "activity:exploration:persistence",
            LocationStableId = "region:jinbu-myeon",
        });
        var saved = sourceService.Save(Session, new SimulationSessionSaveRequest
        {
            SaveStableId = "save:session-persistence:role-card",
            ExpectedRevision = 2,
        });

        var restoredService = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(),
            new SimulationSessionSaveStore(factory));
        var restored = restoredService.Restore(new SimulationSessionRestoreRequest
        {
            SaveStableId = saved.SaveStableId,
        });

        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
        Assert.Equal(2, restored.Session.Revision);
        Assert.NotNull(restored.Session.TeamRoleCards);
        Assert.Single(restored.Session.TeamRoleCards!.ActiveActivities);
        Assert.True(Assert.Single(restored.Session.TeamRoleCards.Cards).IsLocked);
        using var db = factory.CreateDbContext();
        Assert.Single(db.SessionSaves);
    }

    [Fact]
    public void 같은저장식별자는_같은Hash면멱등이고_다르면충돌한다()
    {
        var factory = CreateFactory();
        EnsureCreated(factory);
        var store = new SimulationSessionSaveStore(factory);
        var sessions = new InMemory경영SimulationSessionStore();
        var service = new 경영SimulationSessionService(sessions, store);
        service.Create(CreateRequest());
        var first = service.Save(Session, new SimulationSessionSaveRequest
        {
            SaveStableId = "save:session-persistence:idempotent",
            ExpectedRevision = 0,
        });
        var retried = service.Save(Session, new SimulationSessionSaveRequest
        {
            SaveStableId = first.SaveStableId,
            ExpectedRevision = 0,
        });
        service.Advance(Session, new 경영SimulationTick진행Request
        {
            CommandId = "command:session-persistence:tick",
            ExpectedRevision = 0,
            TickCount = 1,
        });

        var conflict = Assert.Throws<SimulationConflictException>(() =>
            service.Save(Session, new SimulationSessionSaveRequest
            {
                SaveStableId = first.SaveStableId,
                ExpectedRevision = 1,
            }));

        Assert.Equal(first.ReplayHash, retried.ReplayHash);
        Assert.Equal("SimulationSaveStableIdConflict", conflict.ErrorCode);
        using var db = factory.CreateDbContext();
        Assert.Single(db.SessionSaves);
    }

    [Fact]
    public void 저장JSON이나Metadata가변조되면_조회단계에서거부한다()
    {
        var factory = CreateFactory();
        EnsureCreated(factory);
        var store = new SimulationSessionSaveStore(factory);
        var sessions = new InMemory경영SimulationSessionStore();
        var service = new 경영SimulationSessionService(sessions, store);
        service.Create(CreateRequest());
        service.Save(Session, new SimulationSessionSaveRequest
        {
            SaveStableId = "save:session-persistence:corrupted",
            ExpectedRevision = 0,
        });
        using (var db = factory.CreateDbContext())
        {
            var entity = db.SessionSaves.Single();
            entity.PackageJson = "{}";
            db.SaveChanges();
        }

        var error = Assert.Throws<InvalidOperationException>(() =>
            store.Find("save:session-persistence:corrupted"));

        Assert.Equal(SimulationSessionSaveStore.CorruptedCode, error.Message);
    }

    [Fact]
    public void 전투저장기록은_JSON저장소를거쳐도상태와무결성Hash를보존한다()
    {
        var factory = CreateFactory();
        EnsureCreated(factory);
        var store = new SimulationSessionSaveStore(factory);
        var sessions = new InMemory경영SimulationSessionStore();
        var service = new 경영SimulationSessionService(sessions, store);
        service.Create(CreateRequest());
        var aggregate = Assert.IsType<경영SimulationSessionAggregate>(
            sessions.Find(Session));
        var package = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:session-persistence:battle",
            ExpectedRevision = 0,
        });
        var battle = new SimulationBattleInstanceState(new SimulationBattleCreationContext
        {
            BattleStableId = "battle:persistence:one",
            SessionStableId = Session,
            EncounterStableId = "encounter:persistence:one",
            AreaStableId = "area:persistence:farm",
            CommanderActorStableId = "actor:farmer",
            StartedWorldTick = 0,
            StartedWorldRevision = 0,
            ScenarioSeed = 20260815,
            AlliedStrength = 12,
            HostileStrength = 9,
            InitialResourceStableIds = ["building:persistence:farm"],
            ReinforcementCandidateStableIds = ["npc:persistence:reserve"],
            CreateCommandId = "command:battle:persistence:create",
        });
        battle.ConfirmDeployment(new SimulationBattleDeploymentConfirmRequest
        {
            CommandId = "command:battle:persistence:deploy",
            ExpectedBattleRevision = 0,
            ActorStableId = "actor:farmer",
            DeploymentCode = SimulationBattleInstanceCodes.Defensive,
        });
        package = SimulationSaveReplayCloner.WithBattles(package,
            [battle.CreateSaveRecord()]);
        var saved = store.SaveOrGet(package);
        var loaded = new SimulationSessionSaveStore(factory).Find(saved.SaveStableId)!;
        var restoredBattle = SimulationBattleInstanceState.Restore(
            Assert.Single(loaded.Battles));

        Assert.Equal(saved.ReplayHash, loaded.ReplayHash);
        Assert.Equal(battle.Snapshot().ReplayHashSha256,
            restoredBattle.Snapshot().ReplayHashSha256);
        Assert.Equal(SimulationBattleInstanceCodes.Active,
            restoredBattle.Snapshot().PhaseCode);
    }

    [Fact]
    public void 저장자료물리표와열은_한국어로정의한다()
    {
        var factory = CreateFactory();
        using var db = factory.CreateDbContext();
        var entity = db.Model.FindEntityType(
            typeof(SimulationSession저장자료Entity));
        Assert.NotNull(entity);
        Assert.Equal("시뮬레이션세션_저장자료", entity.GetTableName());
        var table = StoreObjectIdentifier.Table(
            entity.GetTableName()!, entity.GetSchema());
        var columns = entity.GetProperties()
            .Select(value => value.GetColumnName(table))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("저장자료고유식별자", columns);
        Assert.Contains("세션고유식별자", columns);
        Assert.Contains("재생SHA256", columns);
        Assert.Contains("저장자료JSON", columns);
    }

    [Fact]
    public void SessionDb활성화시연결문자열을검사하고_영속Store를등록한다()
    {
        var missing = Configuration(new Dictionary<string, string?>
        {
            ["SimulationSharedPublicData:Enabled"] = "false",
            ["SimulationSessionDatabase:Enabled"] = "true",
            ["SimulationSessionDatabase:ConnectionStringName"] =
                "SimulationSession",
        });
        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSsalddelSimulationModule(missing));

        var configured = Configuration(new Dictionary<string, string?>
        {
            ["SimulationSharedPublicData:Enabled"] = "false",
            ["SimulationSessionDatabase:Enabled"] = "true",
            ["SimulationSessionDatabase:ConnectionStringName"] =
                "SimulationSession",
            ["ConnectionStrings:SimulationSession"] =
                "Server=localhost;Database=simulation_session;User=test;Password=test;",
        });
        var services = new ServiceCollection();
        services.AddSsalddelSimulationModule(configured);

        Assert.Equal(
            SsalddelSimulationHostingServiceCollectionExtensions
                .SessionConnectionStringMissingErrorCode,
            error.Message);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISimulationSessionSaveStore)
            && descriptor.ImplementationType
                == typeof(SimulationSessionSaveStore));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(ISimulationSessionSaveStore)
            && descriptor.ImplementationType
                == typeof(InMemorySimulationSessionSaveStore));
    }

    private static SimulationSessionDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<SimulationSessionDbContext>()
            .UseInMemoryDatabase(
                "simulation-session-save-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new SimulationSessionDbContextFactory(options);
    }

    private static void EnsureCreated(
        IDbContextFactory<SimulationSessionDbContext> factory)
    {
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    private static 경영SimulationSession생성Request CreateRequest() => new()
    {
        ClientRequestId = Guid.Parse(
            "11111111-1111-1111-1111-111111111111"),
        ScenarioStableId = "scenario:session-persistence",
        ScenarioDataRevision = "scenario-data:session-persistence.r1",
        ScenarioSeed = 815,
        RuleRevision = "simulation-session-persistence.r1",
        DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:pyeongchang-survivors",
            TerritoryStableId = "territory:pyeongchang",
            SettlementStableId = "settlement:daegwallyeong-farm",
            GameDateStartsOn = DateTimeOffset.Parse(
                "2026-04-01T00:00:00Z"),
        },
        TeamRoleCards = new SimulationTeamRoleCardInitialState
        {
            SessionStableId = Session,
            TeamStableId = "team:pyeongchang-survivors",
            TeamPolicyRevision = 3,
            MemberActorStableIds = ["actor:farmer", "actor:explorer"],
            Cards =
            [
                new SimulationTeamRoleCardInitialCard
                {
                    CardCopyStableId = "card-copy:exploration",
                    CardDefinitionStableId = "team-card:exploration",
                    Title = "탐험 기술",
                    ActivityRoleCodes =
                        [SimulationTeamRoleCardCodes.Exploration],
                    EquippedActorStableId = "actor:farmer",
                    SlotCode = SimulationTeamRoleCardCodes.Primary,
                },
            ],
        },
    };

    private static IConfiguration Configuration(
        IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class SimulationSessionDbContextFactory(
        DbContextOptions<SimulationSessionDbContext> options)
        : IDbContextFactory<SimulationSessionDbContext>
    {
        public SimulationSessionDbContext CreateDbContext() => new(options);
    }
}
