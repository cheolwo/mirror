using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Campaigns;
using Ssalddel.Unity.Data.Campaigns;
using Ssalddel.Unity.WorldProjection;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "절기 운영 Campaign의 Unity 읽기 계약, 판본 거절과 마지막 정상 사본 유지를 검증한다.",
    Boundary = "순수 메모리·가짜 전송 시험이며 Unity Scene, Play Mode, Game View 증거가 아니다.")]
public sealed class Simulation절기운영CampaignInterpreterTests
{
    private const string SessionId =
        "simulation-session:11111111111111111111111111111111";
    private const string AreaId = "region:kr:hjd:1126057500";

    [Fact]
    public void 올바른상태를받고_반환사본수정은메모리를바꾸지않는다()
    {
        var interpreter = new Simulation절기운영CampaignInterpreter();
        var result = interpreter.Apply(SessionId, AreaId, State(2));

        Assert.True(result.Accepted);
        Assert.False(result.RetainedLastSuccessful);
        result.State!.CurrentPhaseCode = "Tampered";

        Assert.Equal("LunchPeak", interpreter.Current().State!
            .CurrentPhaseCode);
    }

    [Fact]
    public void 낮은Revision과같은Revision의다른내용은거절하고_마지막정상을유지한다()
    {
        var interpreter = new Simulation절기운영CampaignInterpreter();
        Assert.True(interpreter.Apply(SessionId, AreaId, State(3)).Accepted);

        var stale = interpreter.Apply(SessionId, AreaId, State(2));
        var conflictState = State(3);
        conflictState.CurrentPhaseCode = "StableOperations";
        conflictState.CurrentPhaseOrdinal = 2;
        var conflict = interpreter.Apply(SessionId, AreaId, conflictState);

        Assert.False(stale.Accepted);
        Assert.Equal("SeasonalOperationsCampaignRevisionStale",
            stale.ErrorCode);
        Assert.True(stale.RetainedLastSuccessful);
        Assert.False(conflict.Accepted);
        Assert.Equal("SeasonalOperationsCampaignRevisionConflict",
            conflict.ErrorCode);
        Assert.Equal(3, conflict.State!.CampaignRevision);
        Assert.Equal("LunchPeak", conflict.State.CurrentPhaseCode);
    }

    [Fact]
    public void 다른Session과지역및지원하지않는Schema를거절한다()
    {
        var interpreter = new Simulation절기운영CampaignInterpreter();
        Assert.True(interpreter.Apply(SessionId, AreaId, State(2)).Accepted);

        var sessionMismatch = interpreter.Apply(
            "simulation-session:22222222222222222222222222222222",
            AreaId, State(3));
        var areaMismatch = interpreter.Apply(SessionId,
            "region:kr:hjd:1126058000", State(3));
        var schema = State(3);
        schema.SchemaVersion = "unsupported";
        var schemaMismatch = interpreter.Apply(SessionId, AreaId, schema);

        Assert.Equal("SeasonalOperationsCampaignSessionMismatch",
            sessionMismatch.ErrorCode);
        Assert.Equal("SeasonalOperationsCampaignAreaMismatch",
            areaMismatch.ErrorCode);
        Assert.Equal("SeasonalOperationsCampaignSchemaVersionUnsupported",
            schemaMismatch.ErrorCode);
        Assert.All(new[] { sessionMismatch, areaMismatch, schemaMismatch },
            result => Assert.True(result.RetainedLastSuccessful));
    }

    [Fact]
    public async Task Client는인증GET만사용하고_실패때마지막정상을유지한다()
    {
        var transport = new RecordingTransport();
        var interpreter = new Simulation절기운영CampaignInterpreter();
        var client = new Simulation절기운영CampaignClient(transport,
            new StaticDecoder(), interpreter);

        var first = await client.RefreshAsync(SessionId, AreaId);
        transport.Fail = true;
        var failed = await client.RefreshAsync(SessionId, AreaId);

        Assert.True(first.Accepted);
        Assert.False(failed.Accepted);
        Assert.True(failed.RetainedLastSuccessful);
        Assert.Equal("SeasonalOperationsCampaignRefreshFailed",
            failed.ErrorCode);
        Assert.Equal(2, failed.State!.CampaignRevision);
        Assert.Equal(2, transport.Routes.Count);
        Assert.All(transport.RequiresAuthentication, Assert.True);
        Assert.All(transport.Routes, route => Assert.Equal(
            "api/simulation/v1/sessions/"
            + Uri.EscapeDataString(SessionId)
            + "/seasonal-operations-campaign", route));
    }

    [Fact]
    public void Clear뒤에만다른Session을받을수있다()
    {
        var interpreter = new Simulation절기운영CampaignInterpreter();
        interpreter.Apply(SessionId, AreaId, State(2));

        interpreter.Clear();
        var next = interpreter.Apply(
            "simulation-session:22222222222222222222222222222222",
            AreaId, State(1));

        Assert.True(next.Accepted);
        Assert.False(next.RetainedLastSuccessful);
    }

    private static Simulation절기운영CampaignStateSnapshot State(
        long revision)
        => new()
        {
            CampaignStableId = "campaign:seasonal:lunch-peak",
            DefinitionRevision = "campaign.definition.r1",
            AreaStableId = AreaId,
            StateCode = Simulation절기운영CampaignCodes.Active,
            CurrentPhaseCode = "LunchPeak",
            CurrentPhaseOrdinal = 1,
            PhaseCount = 2,
            CampaignRevision = revision,
            Phases =
            [
                new Simulation절기운영CampaignPhaseDefinition
                {
                    PhaseCode = "LunchPeak",
                    RequiredConditionCodes = ["DemandRiseObserved"],
                    RequiredSourceCodes = ["OperationsScene"],
                },
                new Simulation절기운영CampaignPhaseDefinition
                {
                    PhaseCode = "StableOperations",
                },
            ],
            SourceRevisions =
            [
                new Simulation절기운영CampaignSourceRevision
                {
                    SourceCode = "OperationsScene",
                    Revision = "scene.r4",
                },
            ],
            AvailableActions =
                [Simulation절기운영CampaignCodes.PreviewAdvance],
            SimulationOnly = true,
            IsOperationalState = false,
        };

    private sealed class StaticDecoder : ISimulation절기운영CampaignDecoder
    {
        public Simulation절기운영CampaignStateSnapshot Decode(string json)
            => State(2);
    }

    private sealed class RecordingTransport
        : IOperationalWorldProjectionTransport
    {
        public List<string> Routes { get; } = [];
        public List<bool> RequiresAuthentication { get; } = [];
        public bool Fail { get; set; }

        public Task<string?> GetAsync(string relativeRoute,
            bool allowNotFound, bool requiresAuthentication,
            CancellationToken cancellationToken = default)
        {
            Routes.Add(relativeRoute);
            RequiresAuthentication.Add(requiresAuthentication);
            if (Fail) throw new InvalidOperationException("offline");
            return Task.FromResult<string?>("{}");
        }
    }
}
