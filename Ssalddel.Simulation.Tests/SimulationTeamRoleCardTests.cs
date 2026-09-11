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
public sealed class SimulationTeamRoleCardTests
{
    private const string Session =
        "simulation-session:99999999999999999999999999999999";
    private const string Team = "team:sim:pyeongchang-survivors";
    private const string Farmer = "actor:sim:farmer-1";
    private const string Explorer = "actor:sim:explorer-1";
    private const string ExploreCard = "team-card-copy:exploration-1";
    private const string FarmCard = "team-card-copy:farm-1";

    [Fact]
    public void 멀리있는팀원에게카드를옮기고_활동에따라역할을바꾼다()
    {
        var context = CreateContext();

        var equipped = context.Service.Equip(Session,
            Equip(0, Farmer, Explorer, ExploreCard));
        var exploration = context.Service.StartActivity(Session,
            Start(1, Explorer, ExploreCard,
                SimulationTeamRoleCardCodes.Exploration,
                "activity:explore:jinbu"));

        var explorerRole = Assert.Single(exploration.MemberRoles,
            value => value.ActorStableId == Explorer);
        var card = Assert.Single(exploration.Cards,
            value => value.CardCopyStableId == ExploreCard);
        Assert.True(equipped.SupportsRemoteEquip);
        Assert.False(card.IsPhysicalItem);
        Assert.False(card.RequiresPhysicalProximityForEquip);
        Assert.True(card.IsLocked);
        Assert.Equal(SimulationTeamRoleCardCodes.Exploration,
            explorerRole.CurrentRoleCode);
        Assert.False(explorerRole.IsPermanentProfession);
    }

    [Fact]
    public void 활동중카드는교체할수없고_종료뒤농사역할로전환할수있다()
    {
        var context = CreateContext();
        context.Service.Equip(Session,
            Equip(0, Farmer, Explorer, ExploreCard));
        context.Service.StartActivity(Session,
            Start(1, Explorer, ExploreCard,
                SimulationTeamRoleCardCodes.Exploration,
                "activity:explore:jinbu"));

        var locked = Assert.Throws<SimulationConflictException>(() =>
            context.Service.Equip(Session,
                Equip(2, Explorer, Farmer, ExploreCard)));
        Assert.Equal("SimulationTeamRoleCardActiveLock", locked.ErrorCode);

        var ended = context.Service.EndActivity(Session,
            new SimulationTeamActivityEndRequest
            {
                ClientRequestId = Guid.Parse(
                    "44444444-4444-4444-4444-444444444444"),
                ExpectedRevision = 2,
                ActorStableId = Explorer,
                ActivityStableId = "activity:explore:jinbu",
            });
        var farmEquipped = context.Service.Equip(Session,
            Equip(ended.Revision, Explorer, Explorer, FarmCard));
        var farming = context.Service.StartActivity(Session,
            Start(farmEquipped.Revision, Explorer, FarmCard,
                SimulationTeamRoleCardCodes.FarmWork,
                "activity:farm:daegwallyeong"));

        Assert.Equal(SimulationTeamRoleCardCodes.FarmWork,
            Assert.Single(farming.MemberRoles,
                value => value.ActorStableId == Explorer).CurrentRoleCode);
    }

    [Fact]
    public void 같은요청은멱등이고_팀개정변경뒤에는교체를차단한다()
    {
        var context = CreateContext();
        var request = Equip(0, Farmer, Explorer, ExploreCard);
        var first = context.Service.Equip(Session, request);
        var retried = context.Service.Equip(Session, request);
        var changed = Policy();
        changed.Revision = 8;
        context.Policies.Replace(changed);

        Assert.Equal(first.Revision, retried.Revision);
        var blocked = Assert.Throws<SimulationConflictException>(() =>
            context.Service.Get(Session, Farmer));
        Assert.Equal("SimulationTeamRoleCardPolicyMismatch", blocked.ErrorCode);
    }

    [Fact]
    public void 직접전투와전술지휘편성은_서버에서독립적으로저장된다()
    {
        var context = CreateContext();
        var initial = context.Service.Get(Session, Farmer);
        Assert.Equal(4, initial.CombatLoadouts.Length);

        var direct = context.Service.SetCombatLoadout(Session,
            Loadout(initial.Revision, Farmer,
                SimulationTeamRoleCardCodes.DirectAction,
                (SimulationTeamRoleCardCodes.Primary, ExploreCard)));
        var tactical = context.Service.SetCombatLoadout(Session,
            Loadout(direct.Revision, Farmer,
                SimulationTeamRoleCardCodes.TacticalCommand,
                (SimulationTeamRoleCardCodes.Primary, FarmCard)));

        Assert.Equal(ExploreCard, Assert.Single(tactical.CombatLoadouts,
            value => value.ActorStableId == Farmer
                && value.CombatControlModeCode ==
                SimulationTeamRoleCardCodes.DirectAction).Slots.Single()
            .CardCopyStableId);
        Assert.Equal(FarmCard, Assert.Single(tactical.CombatLoadouts,
            value => value.ActorStableId == Farmer
                && value.CombatControlModeCode ==
                SimulationTeamRoleCardCodes.TacticalCommand).Slots.Single()
            .CardCopyStableId);
    }

    [Fact]
    public async Task Api는_공동카드조회_원격장착_활동시작종료를제공한다()
    {
        using var factory = CreateFactory();
        var policies = factory.Services.GetRequiredService<
            InMemorySimulationTeamObservationPolicyStore>();
        policies.Replace(Policy());
        factory.Services.GetRequiredService<경영SimulationSessionService>()
            .Create(SessionRequest());
        using var client = factory.CreateClient();
        var path = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(Session) + "/team-role-cards";

        var initial = await client.GetFromJsonAsync<
            SimulationTeamRoleCardStateSnapshot>(path + "?actorStableId="
                + Uri.EscapeDataString(Farmer));
        using var equipResponse = await client.PostAsJsonAsync(path + "/equip",
            Equip(0, Farmer, Explorer, ExploreCard));
        var equipped = await equipResponse.Content.ReadFromJsonAsync<
            SimulationTeamRoleCardStateSnapshot>();
        using var startResponse = await client.PostAsJsonAsync(
            path + "/activities/start",
            Start(1, Explorer, ExploreCard,
                SimulationTeamRoleCardCodes.Exploration,
                "activity:explore:http"));
        var active = await startResponse.Content.ReadFromJsonAsync<
            SimulationTeamRoleCardStateSnapshot>();
        using var lockedResponse = await client.PostAsJsonAsync(path + "/equip",
            Equip(2, Explorer, Farmer, ExploreCard));
        using var endResponse = await client.PostAsJsonAsync(
            path + "/activities/end", new SimulationTeamActivityEndRequest
            {
                ClientRequestId = Guid.Parse(
                    "88888888-8888-8888-8888-888888888888"),
                ExpectedRevision = 2,
                ActorStableId = Explorer,
                ActivityStableId = "activity:explore:http",
            });
        using var loadoutResponse = await client.PostAsJsonAsync(
            path + "/combat-loadouts/set",
            Loadout(3, Farmer, SimulationTeamRoleCardCodes.DirectAction,
                (SimulationTeamRoleCardCodes.Primary, FarmCard)));

        Assert.NotNull(initial);
        Assert.Equal(HttpStatusCode.OK, equipResponse.StatusCode);
        Assert.NotNull(equipped);
        Assert.Equal(1, equipped.Revision);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.NotNull(active);
        Assert.Single(active.ActiveActivities);
        Assert.Equal(HttpStatusCode.Conflict, lockedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loadoutResponse.StatusCode);
    }

    [Fact]
    public void 카드교체와활동은_SessionSaveReplay뒤에도복원된다()
    {
        var context = CreateContext();
        context.Service.Equip(Session,
            Equip(0, Farmer, Explorer, ExploreCard));
        context.Service.StartActivity(Session,
            Start(1, Explorer, ExploreCard,
                SimulationTeamRoleCardCodes.Exploration,
                "activity:explore:save"));
        context.Service.SetCombatLoadout(Session,
            Loadout(2, Farmer, SimulationTeamRoleCardCodes.TacticalCommand,
                (SimulationTeamRoleCardCodes.Primary, FarmCard)));
        var aggregate = Assert.IsType<경영SimulationSessionAggregate>(
            context.Sessions.Find(Session));

        var package = aggregate.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = "simulation-save:team-role-card-1",
                ExpectedRevision = 3,
            });
        var restored = SimulationSessionReplay.Restore(package);
        var state = restored.GetTeamRoleCards();

        Assert.Equal(3, restored.Revision);
        Assert.Equal(3, state.Revision);
        Assert.True(Assert.Single(state.Cards,
            value => value.CardCopyStableId == ExploreCard).IsLocked);
        Assert.Equal(SimulationTeamRoleCardCodes.Exploration,
            Assert.Single(state.MemberRoles,
                value => value.ActorStableId == Explorer).CurrentRoleCode);
        Assert.Contains(package.CommandLog, value =>
            value.CommandTypeCode == SimulationCommandTypeCodes.TeamRoleCardEquip);
        Assert.Contains(package.CommandLog, value =>
            value.CommandTypeCode == SimulationCommandTypeCodes.TeamActivityStart);
        Assert.Contains(package.CommandLog, value =>
            value.CommandTypeCode == SimulationCommandTypeCodes.CombatCardLoadoutSet);
        Assert.Equal(FarmCard, Assert.Single(state.CombatLoadouts,
            value => value.ActorStableId == Farmer
                && value.CombatControlModeCode ==
                SimulationTeamRoleCardCodes.TacticalCommand).Slots.Single()
            .CardCopyStableId);
    }

    private static TestContext CreateContext()
    {
        var policies = new InMemorySimulationTeamObservationPolicyStore();
        policies.Replace(Policy());
        var sessions = new InMemory경영SimulationSessionStore();
        sessions.CreateOrGet(SessionRequest());
        var service = new SimulationTeamRoleCardService(policies,
            sessions);
        return new TestContext(service, policies, sessions);
    }

    private static SimulationTeamRoleCardInitialState Initial() => new()
    {
        SessionStableId = Session,
        TeamStableId = Team,
        TeamPolicyRevision = 7,
        MemberActorStableIds = [Farmer, Explorer],
        Cards =
        [
            new SimulationTeamRoleCardInitialCard
            {
                CardCopyStableId = ExploreCard,
                CardDefinitionStableId = "team-card:exploration",
                Title = "탐험 기술",
                ActivityRoleCodes = [SimulationTeamRoleCardCodes.Exploration],
                EquippedActorStableId = Farmer,
                SlotCode = SimulationTeamRoleCardCodes.Primary,
            },
            new SimulationTeamRoleCardInitialCard
            {
                CardCopyStableId = FarmCard,
                CardDefinitionStableId = "team-card:farm-work",
                Title = "농사 기술",
                ActivityRoleCodes = [SimulationTeamRoleCardCodes.FarmWork],
                EquippedActorStableId = Explorer,
                SlotCode = SimulationTeamRoleCardCodes.Primary,
            },
        ],
    };

    private static 경영SimulationSession생성Request SessionRequest() => new()
    {
        ClientRequestId = Guid.Parse(
            "99999999-9999-9999-9999-999999999999"),
        ScenarioStableId = "scenario:pyeongchang-team-role-card",
        ScenarioDataRevision = "scenario-data:team-role-card.r1",
        ScenarioSeed = 815,
        RuleRevision = "simulation-team-role-card.r1",
        DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:pyeongchang-survivors",
            TerritoryStableId = "territory:pyeongchang",
            SettlementStableId = "settlement:daegwallyeong-farm",
            GameDateStartsOn = DateTimeOffset.Parse("2026-04-01T00:00:00Z"),
        },
        TeamRoleCards = Initial(),
    };

    private static SimulationTeamObservationPolicySnapshot Policy() => new()
    {
        SessionStableId = Session,
        TeamStableId = Team,
        Revision = 7,
        MembersCanObserve = true,
        MemberActorStableIds = [Farmer, Explorer],
        AllowedViewModeCodes =
            [SimulationTeamObservationViewModeCodes.FirstPerson],
        ShowObserverIndicator = true,
        SimulationOnly = true,
        IsOperationalState = false,
    };

    private static SimulationTeamRoleCardEquipRequest Equip(
        long revision,
        string requester,
        string target,
        string card) => new()
    {
        ClientRequestId = Guid.NewGuid(),
        ExpectedRevision = revision,
        ExpectedTeamPolicyRevision = 7,
        RequestingActorStableId = requester,
        TargetActorStableId = target,
        CardCopyStableId = card,
        SlotCode = SimulationTeamRoleCardCodes.Primary,
    };

    private static SimulationTeamActivityStartRequest Start(
        long revision,
        string actor,
        string card,
        string role,
        string activity) => new()
    {
        ClientRequestId = Guid.NewGuid(),
        ExpectedRevision = revision,
        ExpectedTeamPolicyRevision = 7,
        ActorStableId = actor,
        CardCopyStableId = card,
        ActivityRoleCode = role,
        ActivityStableId = activity,
        LocationStableId = role == SimulationTeamRoleCardCodes.Exploration
            ? "region:jinbu-myeon" : "region:daegwallyeong-myeon",
    };

    private static SimulationCombatCardLoadoutSetRequest Loadout(
        long revision, string actor, string mode,
        params (string Slot, string Card)[] slots) => new()
    {
        ClientRequestId = Guid.NewGuid(),
        ExpectedRevision = revision,
        ExpectedTeamPolicyRevision = 7,
        RequestingActorStableId = actor,
        TargetActorStableId = actor,
        CombatControlModeCode = mode,
        Slots = slots.Select(value => new SimulationCombatCardLoadoutSlotSnapshot
        {
            SlotCode = value.Slot,
            CardCopyStableId = value.Card,
        }).ToArray(),
    };

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SsalddelExecution:Mode"] = "Operational",
                            ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                            ["SimulationSharedPublicData:Enabled"] = "false",
                        });
                });
            });

    private sealed record TestContext(
        SimulationTeamRoleCardService Service,
        InMemorySimulationTeamObservationPolicyStore Policies,
        InMemory경영SimulationSessionStore Sessions);
}
