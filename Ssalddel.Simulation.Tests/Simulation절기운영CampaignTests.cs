using System;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "절기 운영 캠페인의 Preview 불변성, Confirm 멱등성과 전이 차단을 검증한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3저장재생검증,
    Boundary = "계약·Domain·TestServer 자동 시험이며 실제 외부 서버 접속·Unity 실행 증거가 아니다.")]
public sealed class Simulation절기운영CampaignTests
{
    [Fact]
    public void Preview는_상태를바꾸지않고_정규화된입력에결정적이다()
    {
        var session = CreateSession();
        var started = Begin(session, TwoPhaseDefinition());
        var revision = session.Revision;

        var first = session.PreviewSeasonalOperationsCampaignAdvance(
            Preview(started, new[] { "DemandRiseObserved", "CashFloorSafe" },
                new[]
                {
                    Source("OperationsScene", "scene.r4"),
                    Source("Diorama", "diorama.r12"),
                }));
        var second = session.PreviewSeasonalOperationsCampaignAdvance(
            Preview(started, new[] { "CashFloorSafe", "DemandRiseObserved" },
                new[]
                {
                    Source("Diorama", "diorama.r12"),
                    Source("OperationsScene", "scene.r4"),
                }));

        Assert.True(first.CanConfirm);
        Assert.Equal("StableOperations", first.NextPhaseCode);
        Assert.Equal(first.PreviewHash, second.PreviewHash);
        Assert.Equal(revision, session.Revision);
        Assert.Equal("LunchPeak", session.GetSeasonalOperationsCampaignState()
            .CurrentPhaseCode);
    }

    [Fact]
    public void 필수조건이나원천이없으면_Confirm을차단하고_상태를보존한다()
    {
        var session = CreateSession();
        var started = Begin(session, TwoPhaseDefinition());
        var revision = session.Revision;
        var request = Preview(started, Array.Empty<string>(),
            Array.Empty<Simulation절기운영CampaignSourceRevision>());
        var preview = session.PreviewSeasonalOperationsCampaignAdvance(request);

        Assert.False(preview.CanConfirm);
        Assert.Contains("SeasonalOperationsCampaignConditionPending",
            preview.BlockReasonCodes);
        Assert.Contains("SeasonalOperationsCampaignSourceRevisionMissing",
            preview.BlockReasonCodes);
        var error = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmSeasonalOperationsCampaignAdvance(new()
            {
                CommandId = "campaign:confirm:blocked",
                ExpectedPreviewHash = preview.PreviewHash,
                PreviewRequest = request,
            }));
        Assert.Equal("SeasonalOperationsCampaignAdvanceBlocked", error.ErrorCode);
        Assert.Equal(revision, session.Revision);
        Assert.Equal("LunchPeak", session.GetSeasonalOperationsCampaignState()
            .CurrentPhaseCode);
    }

    [Fact]
    public void Confirm은_다음구간으로한번만전이하고_같은명령은멱등이다()
    {
        var session = CreateSession();
        var started = Begin(session, TwoPhaseDefinition());
        var request = ValidPreview(started);
        var preview = session.PreviewSeasonalOperationsCampaignAdvance(request);
        var confirm = new Simulation절기운영CampaignAdvanceConfirmRequest
        {
            CommandId = "campaign:confirm:lunch-peak",
            ExpectedPreviewHash = preview.PreviewHash,
            PreviewRequest = request,
        };

        var advanced = session.ConfirmSeasonalOperationsCampaignAdvance(confirm);
        var repeated = session.ConfirmSeasonalOperationsCampaignAdvance(confirm);

        Assert.Equal("StableOperations", advanced.CurrentPhaseCode);
        Assert.Equal(2, advanced.CurrentPhaseOrdinal);
        Assert.Equal(advanced.CampaignRevision, repeated.CampaignRevision);
        Assert.Equal(advanced.Events.Length, repeated.Events.Length);
        Assert.Single(advanced.Events, value => value.EventCode ==
            Simulation절기운영CampaignCodes.PhaseAdvanced);

        var conflict = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmSeasonalOperationsCampaignAdvance(new()
            {
                CommandId = confirm.CommandId,
                ExpectedPreviewHash = new string('0', 64),
                PreviewRequest = request,
            }));
        Assert.Equal("SeasonalOperationsCampaignCommandPayloadConflict",
            conflict.ErrorCode);
    }

    [Fact]
    public void 다른PreviewHash는_상태변경전에거부한다()
    {
        var session = CreateSession();
        var started = Begin(session, TwoPhaseDefinition());
        var request = ValidPreview(started);
        var revision = session.Revision;

        var error = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmSeasonalOperationsCampaignAdvance(new()
            {
                CommandId = "campaign:confirm:wrong-preview",
                ExpectedPreviewHash = new string('f', 64),
                PreviewRequest = request,
            }));

        Assert.Equal("SeasonalOperationsCampaignPreviewMismatch", error.ErrorCode);
        Assert.Equal(revision, session.Revision);
    }

    [Fact]
    public void 마지막구간Confirm은_캠페인만완료하고_운영상태가되지않는다()
    {
        var session = CreateSession();
        var started = Begin(session, TwoPhaseDefinition());
        var firstRequest = ValidPreview(started);
        var firstPreview = session.PreviewSeasonalOperationsCampaignAdvance(
            firstRequest);
        var finalPhase = session.ConfirmSeasonalOperationsCampaignAdvance(new()
        {
            CommandId = "campaign:confirm:stable-operations",
            ExpectedPreviewHash = firstPreview.PreviewHash,
            PreviewRequest = firstRequest,
        });
        var request = Preview(finalPhase, Array.Empty<string>(),
            Array.Empty<Simulation절기운영CampaignSourceRevision>());
        var preview = session.PreviewSeasonalOperationsCampaignAdvance(request);

        var completed = session.ConfirmSeasonalOperationsCampaignAdvance(new()
        {
            CommandId = "campaign:confirm:complete",
            ExpectedPreviewHash = preview.PreviewHash,
            PreviewRequest = request,
        });

        Assert.Equal(Simulation절기운영CampaignCodes.Completed,
            completed.StateCode);
        Assert.Empty(completed.AvailableActions);
        Assert.True(completed.SimulationOnly);
        Assert.False(completed.IsOperationalState);
        Assert.Equal("StableOperations", completed.CurrentPhaseCode);
    }

    [Fact]
    public void 반환된사본을수정해도_Aggregate상태는변하지않는다()
    {
        var session = CreateSession();
        var returned = Begin(session, TwoPhaseDefinition());

        returned.CurrentPhaseCode = "Tampered";
        returned.Phases[0].PhaseCode = "TamperedPhase";
        returned.SourceRevisions[0].Revision = "tampered";

        var current = session.GetSeasonalOperationsCampaignState();
        Assert.Equal("LunchPeak", current.CurrentPhaseCode);
        Assert.Equal("LunchPeak", current.Phases[0].PhaseCode);
        Assert.Equal("scene.r4", current.SourceRevisions[0].Revision);
    }

    [Fact]
    public async Task LocalRuntime은_같은계약으로_시작PreviewConfirm을수행한다()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "seasonal-operations-campaign-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var runtime = new LocalSimulationRuntime(
                new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(),
                new FileSimulationLocalSaveSlotStore(root));
            var session = await runtime.Sessions.CreateAsync(CreateRequest());
            var started = await runtime.SeasonalOperationsCampaigns
                .BeginSeasonalOperationsCampaignAsync(session.SessionStableId,
                    new Simulation절기운영CampaignStartRequest
                    {
                        CommandId = "runtime:campaign:start",
                        ExpectedRevision = session.Revision,
                        AreaStableId = "region:kr:hjd:1126057500",
                        Definition = TwoPhaseDefinition(),
                        SourceRevisions = new[]
                        {
                            Source("OperationsScene", "scene.r4"),
                        },
                    });
            var request = ValidPreview(started);
            var preview = await runtime.SeasonalOperationsCampaigns
                .PreviewSeasonalOperationsCampaignAdvanceAsync(
                    session.SessionStableId, request);
            var advanced = await runtime.SeasonalOperationsCampaigns
                .ConfirmSeasonalOperationsCampaignAdvanceAsync(
                    session.SessionStableId,
                    new Simulation절기운영CampaignAdvanceConfirmRequest
                    {
                        CommandId = "runtime:campaign:confirm",
                        ExpectedPreviewHash = preview.PreviewHash,
                        PreviewRequest = request,
                    });

            Assert.Equal(SimulationAuthorityLocation.LocalProcess,
                runtime.Descriptor.AuthorityLocation);
            Assert.Equal("StableOperations", advanced.CurrentPhaseCode);
            Assert.Equal(advanced.CampaignRevision,
                (await runtime.SeasonalOperationsCampaigns
                    .GetSeasonalOperationsCampaignAsync(
                        session.SessionStableId)).CampaignRevision);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RemoteHost는_LocalProcess와_같은Preview와Confirm결과를반환한다()
    {
        var sessionRequest = CreateRequest();
        var localSession = new 경영SimulationSessionAggregate(sessionRequest);
        var localStarted = Begin(localSession, TwoPhaseDefinition());
        var localRequest = ValidPreview(localStarted);
        var localPreview = localSession
            .PreviewSeasonalOperationsCampaignAdvance(localRequest);
        var localAdvanced = localSession
            .ConfirmSeasonalOperationsCampaignAdvance(new()
            {
                CommandId = "campaign:remote-parity:confirm",
                ExpectedPreviewHash = localPreview.PreviewHash,
                PreviewRequest = localRequest,
            });

        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        using var createdResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", sessionRequest);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<
            경영SimulationSessionSnapshot>();
        Assert.NotNull(created);
        var remoteService = factory.Services.GetRequiredService<
            Simulation절기운영CampaignService>();
        remoteService.Begin(created!.SessionStableId, new()
        {
            CommandId = "campaign:start",
            ExpectedRevision = created.Revision,
            AreaStableId = "region:kr:hjd:1126057500",
            Definition = TwoPhaseDefinition(),
            SourceRevisions = new[] { Source("OperationsScene", "scene.r4") },
        });

        var remoteState = await client.GetFromJsonAsync<
            Simulation절기운영CampaignStateSnapshot>(
            $"/api/simulation/v1/sessions/{created.SessionStableId}"
            + "/seasonal-operations-campaign");
        Assert.NotNull(remoteState);
        var remoteRequest = ValidPreview(remoteState!);
        using var previewResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created.SessionStableId}"
            + "/seasonal-operations-campaign/advance-previews",
            remoteRequest);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var remotePreview = await previewResponse.Content.ReadFromJsonAsync<
            Simulation절기운영CampaignAdvancePreviewSnapshot>();
        Assert.NotNull(remotePreview);
        using var confirmResponse = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created.SessionStableId}"
            + "/seasonal-operations-campaign/advance-commands",
            new Simulation절기운영CampaignAdvanceConfirmRequest
            {
                CommandId = "campaign:remote-parity:confirm",
                ExpectedPreviewHash = remotePreview!.PreviewHash,
                PreviewRequest = remoteRequest,
            });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var remoteAdvanced = await confirmResponse.Content.ReadFromJsonAsync<
            Simulation절기운영CampaignStateSnapshot>();

        Assert.NotNull(remoteAdvanced);
        Assert.Equal(localPreview.PreviewHash, remotePreview.PreviewHash);
        Assert.Equal(localAdvanced.CurrentPhaseCode,
            remoteAdvanced!.CurrentPhaseCode);
        Assert.Equal(localAdvanced.CampaignRevision,
            remoteAdvanced.CampaignRevision);
        Assert.Equal(localAdvanced.AvailableActions,
            remoteAdvanced.AvailableActions);
    }

    [Fact]
    public void 저장복원은_현재구간과멱등명령을_같은hash로재현한다()
    {
        var session = CreateSession();
        var started = Begin(session, TwoPhaseDefinition());
        var request = ValidPreview(started);
        var preview = session.PreviewSeasonalOperationsCampaignAdvance(request);
        var confirm = new Simulation절기운영CampaignAdvanceConfirmRequest
        {
            CommandId = "campaign:confirm:save-replay",
            ExpectedPreviewHash = preview.PreviewHash,
            PreviewRequest = request,
        };
        var advanced = session.ConfirmSeasonalOperationsCampaignAdvance(confirm);
        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:seasonal-operations:active",
            ExpectedRevision = advanced.CampaignRevision,
        });

        var restored = SimulationSessionReplay.Restore(saved);
        var restoredState = restored.GetSeasonalOperationsCampaignState();
        var retried = restored.ConfirmSeasonalOperationsCampaignAdvance(confirm);
        var resaved = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:seasonal-operations:active-copy",
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V32, saved.SchemaVersion);
        Assert.NotNull(saved.SeasonalOperationsCampaign);
        Assert.Equal("StableOperations", restoredState.CurrentPhaseCode);
        Assert.Equal(advanced.CampaignRevision, restoredState.CampaignRevision);
        Assert.Equal(restoredState.CampaignRevision, retried.CampaignRevision);
        Assert.Equal(saved.ReplayHash, resaved.ReplayHash);
    }

    [Fact]
    public void 저장된절기Campaign상태가변조되면_복원전에거부한다()
    {
        var session = CreateSession();
        var started = Begin(session, TwoPhaseDefinition());
        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:seasonal-operations:tampered",
            ExpectedRevision = started.CampaignRevision,
        });
        saved.SeasonalOperationsCampaign!.CurrentPhaseCode = "Tampered";

        var error = Assert.Throws<SimulationContractException>(() =>
            SimulationSessionReplay.Restore(saved));

        Assert.Equal("SeasonalOperationsCampaignSaveStateInvalid",
            error.ErrorCode);
    }

    private static 경영SimulationSessionAggregate CreateSession()
        => new(CreateRequest());

    private static 경영SimulationSession생성Request CreateRequest()
        => new()
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:seasonal-operations-campaign-test",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 6720,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:regional-operator",
                TerritoryStableId = "region:kr:hjd:1126057500",
                SettlementStableId = "settlement:sagajeong",
                GameDateStartsOn = new DateTimeOffset(2026, 9, 17, 0, 0, 0,
                    TimeSpan.Zero),
            },
        };

    private static Simulation절기운영CampaignStateSnapshot Begin(
        경영SimulationSessionAggregate session,
        Simulation절기운영CampaignDefinitionSnapshot definition)
        => session.BeginSeasonalOperationsCampaign(new()
        {
            CommandId = "campaign:start",
            ExpectedRevision = session.Revision,
            AreaStableId = "region:kr:hjd:1126057500",
            Definition = definition,
            SourceRevisions = new[] { Source("OperationsScene", "scene.r4") },
        });

    private static Simulation절기운영CampaignDefinitionSnapshot
        TwoPhaseDefinition()
        => new()
        {
            CampaignStableId = "campaign:seasonal:lunch-peak",
            DefinitionRevision = "campaign.definition.r1",
            Phases = new[]
            {
                new Simulation절기운영CampaignPhaseDefinition
                {
                    PhaseCode = "LunchPeak",
                    RequiredConditionCodes = new[]
                    {
                        "DemandRiseObserved",
                        "CashFloorSafe",
                    },
                    RequiredSourceCodes = new[]
                    {
                        "OperationsScene",
                        "Diorama",
                    },
                },
                new Simulation절기운영CampaignPhaseDefinition
                {
                    PhaseCode = "StableOperations",
                },
            },
        };

    private static Simulation절기운영CampaignAdvancePreviewRequest ValidPreview(
        Simulation절기운영CampaignStateSnapshot state)
        => Preview(state, new[] { "DemandRiseObserved", "CashFloorSafe" },
            new[]
            {
                Source("OperationsScene", "scene.r4"),
                Source("Diorama", "diorama.r12"),
            });

    private static Simulation절기운영CampaignAdvancePreviewRequest Preview(
        Simulation절기운영CampaignStateSnapshot state, string[] conditions,
        Simulation절기운영CampaignSourceRevision[] sources)
        => new()
        {
            ExpectedRevision = state.CampaignRevision,
            ExpectedPhaseCode = state.CurrentPhaseCode,
            SatisfiedConditionCodes = conditions,
            SourceRevisions = sources,
        };

    private static Simulation절기운영CampaignSourceRevision Source(
        string sourceCode, string revision)
        => new() { SourceCode = sourceCode, Revision = revision };
}
