using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationTeamObservationTests
{
    private const string Session = "simulation-session:team-observation-1";
    private const string Team = "team:sim:pyeongchang-survivors";
    private const string Observer = "actor:sim:farmer-1";
    private const string Target = "actor:sim:explorer-1";

    [Fact]
    public void 같은팀은_개별요청없이_관찰만허용한다()
    {
        var result = new SimulationTeamObservationPolicy().Evaluate(
            Policy(), Request(Target));

        Assert.True(result.Allowed);
        Assert.Equal(SimulationTeamObservationAccessReasonCodes.SameTeam,
            result.ReasonCode);
        Assert.False(result.RequiresPerViewConsent);
        Assert.False(result.CanControlTarget);
        Assert.False(result.MoveObserverActor);
        Assert.False(result.ChangesWorldState);
        Assert.True(result.ShowObserverIndicator);
        Assert.True(result.PresentationOnly);
        Assert.False(result.IsOperationalState);
    }

    [Fact]
    public void 다른팀대상과_낡은팀개정은_관찰을허용하지않는다()
    {
        var policy = new SimulationTeamObservationPolicy();
        var differentTeam = policy.Evaluate(Policy(), Request("actor:sim:outsider"));
        var stale = Request(Target);
        stale.ExpectedTeamRevision = 6;
        var staleRevision = policy.Evaluate(Policy(), stale);

        Assert.False(differentTeam.Allowed);
        Assert.Equal(SimulationTeamObservationAccessReasonCodes.TargetNotInTeam,
            differentTeam.ReasonCode);
        Assert.False(staleRevision.Allowed);
        Assert.Equal(SimulationTeamObservationAccessReasonCodes.RevisionMismatch,
            staleRevision.ReasonCode);
        Assert.False(differentTeam.CanControlTarget);
        Assert.False(staleRevision.CanControlTarget);
    }

    [Fact]
    public async Task 팀정책저장소가확인한같은팀만_Api에서관찰가능하다()
    {
        using var factory = CreateFactory();
        factory.Services.GetRequiredService<
            InMemorySimulationTeamObservationPolicyStore>().Replace(Policy());
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/" + Uri.EscapeDataString(Session)
            + "/team-observation/access/preview", Request(Target));
        var result = await response.Content.ReadFromJsonAsync<
            SimulationTeamObservationAccessResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.Allowed);
        Assert.Equal(Team, result.TeamStableId);
        Assert.False(result.CanControlTarget);
    }

    [Fact]
    public async Task 서버팀원장이없으면_클라이언트입력만으로관찰권한을만들지않는다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/" + Uri.EscapeDataString(Session)
            + "/team-observation/access/preview", Request(Target));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void 관전Session은_공개Pose만읽고_종료뒤Frame을차단한다()
    {
        var policies = new InMemorySimulationTeamObservationPolicyStore();
        var sessions = new InMemorySimulationTeamObservationSessionStore();
        var poses = new InMemorySimulationTeamMemberPoseStore();
        policies.Replace(Policy());
        poses.Replace(Pose());
        var service = new SimulationTeamObservationService(
            policies, sessions, poses);

        var started = service.Start(Session, StartRequest());
        var frame = service.GetFrame(Session,
            started.ObservationSessionStableId);
        var indicators = service.GetObservers(Session, Target);
        var ended = service.End(Session, started.ObservationSessionStableId,
            new SimulationTeamObservationSessionEndRequest
            {
                ClientRequestId = Guid.Parse(
                    "22222222-2222-2222-2222-222222222222"),
                ObserverActorStableId = Observer,
            });

        Assert.Equal(SimulationTeamObservationSessionStateCodes.Active,
            started.StateCode);
        Assert.Equal(12, frame.TargetPose.PoseRevision);
        Assert.False(frame.ContainsPrivateUi);
        Assert.False(frame.ContainsInventory);
        Assert.False(frame.ContainsChat);
        Assert.Equal(Observer, Assert.Single(indicators.ObserverActorStableIds));
        Assert.True(indicators.ShowIndicator);
        Assert.Equal(SimulationTeamObservationSessionStateCodes.Ended,
            ended.StateCode);
        Assert.Throws<SimulationConflictException>(() =>
            service.GetFrame(Session, started.ObservationSessionStableId));
        Assert.False(service.GetObservers(Session, Target).ShowIndicator);
    }

    [Fact]
    public void 팀개정이바뀌면_진행중관전Frame을즉시차단한다()
    {
        var policies = new InMemorySimulationTeamObservationPolicyStore();
        var sessions = new InMemorySimulationTeamObservationSessionStore();
        var poses = new InMemorySimulationTeamMemberPoseStore();
        policies.Replace(Policy());
        poses.Replace(Pose());
        var service = new SimulationTeamObservationService(
            policies, sessions, poses);
        var started = service.Start(Session, StartRequest());
        var changed = Policy();
        changed.Revision = 8;
        policies.Replace(changed);

        var error = Assert.Throws<SimulationConflictException>(() =>
            service.GetFrame(Session, started.ObservationSessionStableId));

        Assert.Equal("SimulationTeamObservationAuthorizationChanged",
            error.ErrorCode);
    }

    [Fact]
    public async Task Api는_관전시작_Frame_표시_종료를_한경계로제공한다()
    {
        using var factory = CreateFactory();
        factory.Services.GetRequiredService<
            InMemorySimulationTeamObservationPolicyStore>().Replace(Policy());
        factory.Services.GetRequiredService<
            InMemorySimulationTeamMemberPoseStore>().Replace(Pose());
        using var client = factory.CreateClient();
        var basePath = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(Session) + "/team-observation";

        var started = await (await client.PostAsJsonAsync(
                basePath + "/sessions/start", StartRequest()))
            .Content.ReadFromJsonAsync<SimulationTeamObservationSessionResponse>();
        Assert.NotNull(started);
        var frame = await client.GetFromJsonAsync<SimulationTeamObservationFrameResponse>(
            basePath + "/sessions/"
            + Uri.EscapeDataString(started.ObservationSessionStableId) + "/frame");
        var observers = await client.GetFromJsonAsync<
            SimulationTeamObserverIndicatorResponse>(basePath + "/targets/"
            + Uri.EscapeDataString(Target) + "/observers");
        var endedResponse = await client.PostAsJsonAsync(basePath + "/sessions/"
            + Uri.EscapeDataString(started.ObservationSessionStableId) + "/end",
            new SimulationTeamObservationSessionEndRequest
            {
                ClientRequestId = Guid.Parse(
                    "33333333-3333-3333-3333-333333333333"),
                ObserverActorStableId = Observer,
            });

        Assert.NotNull(frame);
        Assert.Equal(12, frame.TargetPose.PoseRevision);
        Assert.NotNull(observers);
        Assert.True(observers.ShowIndicator);
        Assert.Equal(HttpStatusCode.OK, endedResponse.StatusCode);
        using var afterEnd = await client.GetAsync(basePath + "/sessions/"
            + Uri.EscapeDataString(started.ObservationSessionStableId) + "/frame");
        Assert.Equal(HttpStatusCode.Conflict, afterEnd.StatusCode);
    }

    private static SimulationTeamObservationPolicySnapshot Policy()
        => new()
        {
            SessionStableId = Session,
            TeamStableId = Team,
            Revision = 7,
            MembersCanObserve = true,
            MemberActorStableIds = [Observer, Target],
            AllowedViewModeCodes =
            [
                SimulationTeamObservationViewModeCodes.FirstPerson,
                SimulationTeamObservationViewModeCodes.Follow,
            ],
            ShowObserverIndicator = true,
            SimulationOnly = true,
            IsOperationalState = false,
        };

    private static SimulationTeamObservationAccessRequest Request(string target)
        => new()
        {
            ObserverActorStableId = Observer,
            TargetActorStableId = target,
            RequestedViewModeCode =
                SimulationTeamObservationViewModeCodes.FirstPerson,
            ExpectedTeamRevision = 7,
            TargetTileKey = "kr5186:l2:700:1145",
        };

    private static SimulationTeamObservationSessionStartRequest StartRequest()
        => new()
        {
            ClientRequestId = Guid.Parse(
                "11111111-1111-1111-1111-111111111111"),
            ObserverActorStableId = Observer,
            TargetActorStableId = Target,
            RequestedViewModeCode =
                SimulationTeamObservationViewModeCodes.FirstPerson,
            ExpectedTeamRevision = 7,
            TargetTileKey = "kr5186:l2:700:1145",
        };

    private static SimulationTeamMemberPoseSnapshot Pose()
        => new()
        {
            SessionStableId = Session,
            ActorStableId = Target,
            PoseRevision = 12,
            CapturedAtUtc = DateTimeOffset.Parse("2026-08-15T01:00:00Z"),
            TileKey = "kr5186:l2:701:1145",
            LocalOffsetXMeters = 120d,
            LocalOffsetYMeters = -35d,
            ElevationMeters = 735d,
            CameraHeightMeters = 1.65d,
            YawDegrees = 42d,
            PitchDegrees = -6d,
            MovementIntentCode = "Walk",
            IsAvailable = true,
            SimulationOnly = true,
            IsOperationalState = false,
            PresentationOnly = true,
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
}
