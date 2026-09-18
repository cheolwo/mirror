using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Application;
using Ssalddel.Unity.Campaigns;
using Ssalddel.Unity.Cards;
using Ssalddel.Unity.Data.Campaigns;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.InterpretationContracts;
using Ssalddel.Unity.Observation;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E4,
    "절기 운영 화면의 원천별 마지막 정상 판본·지연·문맥 전환을 검증한다.",
    Boundary = "순수 화면 모델 시험이며 Presenter·Scene·Game View·실제 조작 증거가 아니다.")]
public sealed class SeasonalCampaignObservationCoordinatorTests
{
    private const string SessionId =
        "simulation-session:11111111111111111111111111111111";
    private const string AreaId = "region:kr:hjd:1126057500";
    private static readonly DateTime ObservedAt = new(
        2026, 9, 17, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 정상원천판본을따로보존하고_Campaign허용조작만화면에낸다()
    {
        var coordinator = new SeasonalCampaignObservationCoordinator();

        var result = coordinator.Compose(SessionId, AreaId, Campaign(),
        [
            Fresh(SeasonalCampaignObservationSourceCodes.Diorama,
                "diorama.r12", 12),
            Fresh(SeasonalCampaignObservationSourceCodes.Operations,
                "scene.r4", 4),
        ], ObservedAt);

        Assert.True(result.Accepted);
        Assert.True(result.ScreenModel.HasCampaign);
        Assert.False(result.ScreenModel.ActionsDisabled);
        Assert.Equal([Simulation절기운영CampaignCodes.PreviewAdvance],
            result.ScreenModel.AvailableActions);
        Assert.Equal(3, result.CompositionRevisionSet.Length);
        Assert.DoesNotContain(result.CompositionRevisionSet,
            value => value.SourceCode == "Global");
    }

    [Fact]
    public void 카드원천실패는_다른원천과Campaign조작을지우지않는다()
    {
        var coordinator = new SeasonalCampaignObservationCoordinator();
        coordinator.Compose(SessionId, AreaId, Campaign(),
        [
            Fresh(SeasonalCampaignObservationSourceCodes.Diorama,
                "diorama.r12", 12),
            Fresh(SeasonalCampaignObservationSourceCodes.Cards,
                "cards.r3", 3),
        ], ObservedAt);

        var result = coordinator.Compose(SessionId, AreaId, Campaign(),
        [
            Failed(SeasonalCampaignObservationSourceCodes.Cards,
                "CardRefreshFailed"),
        ], ObservedAt);

        var cards = Assert.Single(result.SourceDiagnostics,
            value => value.SourceCode ==
                SeasonalCampaignObservationSourceCodes.Cards);
        Assert.Equal(SeasonalCampaignSourceFreshnessCodes.RefreshDelayed,
            cards.FreshnessCode);
        Assert.Equal("cards.r3", cards.LastSuccessfulRevision);
        Assert.False(result.ScreenModel.ActionsDisabled);
        Assert.Single(result.ScreenModel.AvailableActions);
        Assert.Contains(result.SourceDiagnostics, value => value.SourceCode ==
            SeasonalCampaignObservationSourceCodes.Diorama);
    }

    [Fact]
    public void 현재국면필수운영원천이없으면_관찰은유지하고Campaign조작만막는다()
    {
        var coordinator = new SeasonalCampaignObservationCoordinator();
        var campaign = Campaign();
        campaign.State!.Phases =
        [
            new Simulation절기운영CampaignPhaseDefinition
            {
                PhaseCode = "LunchPeak",
                RequiredSourceCodes =
                [Simulation절기운영CampaignSourceCodes.OperationsScene],
            },
            new Simulation절기운영CampaignPhaseDefinition
            {
                PhaseCode = "StableOperations",
            },
        ];

        var blocked = coordinator.Compose(SessionId, AreaId, campaign,
            Array.Empty<SeasonalCampaignObservationSourceUpdate>(),
            ObservedAt);
        var recovered = coordinator.Compose(SessionId, AreaId, campaign,
        [
            Fresh(SeasonalCampaignObservationSourceCodes.Operations,
                "scene.r4", 4),
        ], ObservedAt.AddSeconds(30));

        Assert.True(blocked.Accepted);
        Assert.True(blocked.ScreenModel.HasCampaign);
        Assert.True(blocked.ScreenModel.ActionsDisabled);
        Assert.Empty(blocked.ScreenModel.AvailableActions);
        Assert.Equal([SeasonalCampaignObservationSourceCodes.Operations],
            blocked.ScreenModel.RequiredSourceCodes);
        Assert.Equal([SeasonalCampaignObservationSourceCodes.Operations],
            blocked.ScreenModel.DelayedRequiredSourceCodes);
        Assert.Equal(["RequiredSourceUnavailable:Operations"],
            blocked.ScreenModel.DisabledActionReasons);

        Assert.False(recovered.ScreenModel.ActionsDisabled);
        Assert.Empty(recovered.ScreenModel.DelayedRequiredSourceCodes);
        Assert.Equal([Simulation절기운영CampaignCodes.PreviewAdvance],
            recovered.ScreenModel.AvailableActions);
    }

    [Fact]
    public void 알수없는필수원천은_최신인것처럼추정하지않고조작을막는다()
    {
        var coordinator = new SeasonalCampaignObservationCoordinator();
        var campaign = Campaign();
        campaign.State!.Phases =
        [
            new Simulation절기운영CampaignPhaseDefinition
            {
                PhaseCode = "LunchPeak",
                RequiredSourceCodes = ["FutureSource"],
            },
            new Simulation절기운영CampaignPhaseDefinition
            {
                PhaseCode = "StableOperations",
            },
        ];

        var result = coordinator.Compose(SessionId, AreaId, campaign,
            Array.Empty<SeasonalCampaignObservationSourceUpdate>(),
            ObservedAt);

        Assert.True(result.ScreenModel.ActionsDisabled);
        Assert.Equal(["Unmapped:FutureSource"],
            result.ScreenModel.DelayedRequiredSourceCodes);
    }

    [Fact]
    public void Campaign실패는_마지막Phase를보이되_조작만비활성화한다()
    {
        var coordinator = new SeasonalCampaignObservationCoordinator();
        coordinator.Compose(SessionId, AreaId, Campaign(), Array.Empty<
            SeasonalCampaignObservationSourceUpdate>(), ObservedAt);
        var staleCampaign = Campaign();
        staleCampaign.Accepted = false;
        staleCampaign.RetainedLastSuccessful = true;
        staleCampaign.ErrorCode = "SeasonalOperationsCampaignRefreshFailed";

        var result = coordinator.Compose(SessionId, AreaId, staleCampaign,
            Array.Empty<SeasonalCampaignObservationSourceUpdate>(),
            ObservedAt.AddMinutes(1));

        Assert.Equal("LunchPeak", result.ScreenModel.CurrentPhaseCode);
        Assert.True(result.ScreenModel.ActionsDisabled);
        Assert.Empty(result.ScreenModel.AvailableActions);
        Assert.Equal(["CampaignRefreshDelayed"],
            result.ScreenModel.DisabledActionReasons);
        var diagnostic = Assert.Single(result.SourceDiagnostics);
        Assert.Equal(SeasonalCampaignSourceFreshnessCodes.RefreshDelayed,
            diagnostic.FreshnessCode);
    }

    [Fact]
    public void 낮은운영판본은_마지막정상을유지하고지연으로표시한다()
    {
        var coordinator = new SeasonalCampaignObservationCoordinator();
        coordinator.Compose(SessionId, AreaId, Campaign(),
        [Fresh(SeasonalCampaignObservationSourceCodes.Operations,
            "scene.r4", 4)], ObservedAt);

        var result = coordinator.Compose(SessionId, AreaId, Campaign(),
        [Fresh(SeasonalCampaignObservationSourceCodes.Operations,
            "scene.r3", 3)], ObservedAt.AddMinutes(1));

        var operations = Assert.Single(result.SourceDiagnostics,
            value => value.SourceCode ==
                SeasonalCampaignObservationSourceCodes.Operations);
        Assert.Equal("scene.r4", operations.LastSuccessfulRevision);
        Assert.Equal(SeasonalCampaignSourceFreshnessCodes.RefreshDelayed,
            operations.FreshnessCode);
        Assert.Equal("SeasonalCampaignObservationRevisionStale",
            operations.ErrorCode);
    }

    [Fact]
    public void Session이나지역전환은_이전진단과선택을비운다()
    {
        var selection = new SelectionStateStore();
        selection.SetAuthorizationScope("scope:campaign");
        selection.Select(new WorldStableId("order:sample-1"));
        var coordinator = new SeasonalCampaignObservationCoordinator(selection);
        coordinator.Compose(SessionId, AreaId, Campaign(),
        [Fresh(SeasonalCampaignObservationSourceCodes.Cards,
            "cards.r3", 3)], ObservedAt);
        var other = Campaign(
            "simulation-session:22222222222222222222222222222222",
            "region:kr:hjd:1126058000");

        var result = coordinator.Compose(other.SessionStableId,
            other.AreaStableId, other,
            Array.Empty<SeasonalCampaignObservationSourceUpdate>(),
            ObservedAt.AddMinutes(1));

        Assert.Null(selection.SelectedWorldId);
        Assert.DoesNotContain(result.SourceDiagnostics,
            value => value.SourceCode ==
                SeasonalCampaignObservationSourceCodes.Cards);
        Assert.Equal(other.SessionStableId,
            result.ScreenModel.SessionStableId);
        Assert.Equal(other.AreaStableId, result.ScreenModel.AreaStableId);
    }

    [Fact]
    public void 기존Diorama와운영Scene결과를_새Payload없이판본입력으로바꾼다()
    {
        var diorama = SeasonalCampaignObservationSourceAdapters.FromDiorama(
            new AdministrativeDongDioramaApplyResult
            {
                Accepted = true,
                Manifest = new AdministrativeDongDioramaManifest
                {
                    ProjectionHashSha256 = "diorama-hash-r12",
                },
            }, 12, ObservedAt);
        var operations = SeasonalCampaignObservationSourceAdapters
            .FromOperations(new OperationalWorldSceneApplyResult
            {
                Accepted = true,
                Cursor = 4,
            }, ObservedAt);

        Assert.Equal(SeasonalCampaignObservationSourceCodes.Diorama,
            diorama.SourceCode);
        Assert.Equal("diorama-hash-r12", diorama.Revision);
        Assert.Equal(12, diorama.RevisionOrdinal);
        Assert.Equal(SeasonalCampaignObservationSourceCodes.Operations,
            operations.SourceCode);
        Assert.Equal("4", operations.Revision);
        Assert.Equal(4, operations.RevisionOrdinal);
    }

    [Fact]
    public void 카드와Npc화면은_각원천판본만Campaign조합입력으로바꾼다()
    {
        var cards = SeasonalCampaignObservationSourceAdapters.FromCards(
            new CardWorkspaceSnapshot
            {
                PresentationOnly = true,
                SourceRevisions =
                [
                    new CardWorkspaceFamilyRevision
                    {
                        FamilyCode = CardFamilyCodes.TeamRole,
                        SourceRevision = 7,
                    },
                    new CardWorkspaceFamilyRevision
                    {
                        FamilyCode = CardFamilyCodes.Tarot,
                        SourceRevision = 3,
                    },
                ],
            }, ObservedAt);
        var npc = SeasonalCampaignObservationSourceAdapters.FromNpc(
            new 동네관찰ScreenModel { Revision = 11 }, ObservedAt);

        Assert.True(cards.Succeeded);
        Assert.Equal(SeasonalCampaignObservationSourceCodes.Cards,
            cards.SourceCode);
        Assert.Equal("Tarot:3|TeamRole:7", cards.Revision);
        Assert.Equal(7, cards.RevisionOrdinal);
        Assert.True(npc.Succeeded);
        Assert.Equal(SeasonalCampaignObservationSourceCodes.Npc,
            npc.SourceCode);
        Assert.Equal("11", npc.Revision);
        Assert.Equal(11, npc.RevisionOrdinal);
    }

    [Fact]
    public void 판본없는카드화면은_최신으로추정하지않는다()
    {
        var result = SeasonalCampaignObservationSourceAdapters.FromCards(
            new CardWorkspaceSnapshot { PresentationOnly = true }, ObservedAt);

        Assert.False(result.Succeeded);
        Assert.Equal("CardWorkspaceRevisionUnavailable", result.ErrorCode);
    }

    [Fact]
    public void Presenter는읽기전용화면과원천판본을_ViewSocket에그대로전달한다()
    {
        var coordinator = new SeasonalCampaignObservationCoordinator();
        var result = coordinator.Compose(SessionId, AreaId, Campaign(),
        [
            Fresh(SeasonalCampaignObservationSourceCodes.Diorama,
                "diorama.r12", 12),
        ], ObservedAt);
        var target = new RecordingTarget();

        new SeasonalCampaignPresenter().Present(result, target);

        Assert.NotNull(target.ScreenModel);
        Assert.Equal("LunchPeak", target.ScreenModel!.CurrentPhaseCode);
        Assert.Equal(2, target.Diagnostics.Length);
        Assert.Equal(2, target.Revisions.Length);
    }

    private static Simulation절기운영CampaignApplyResult Campaign(
        string session = SessionId, string area = AreaId)
        => new()
        {
            Accepted = true,
            SessionStableId = session,
            AreaStableId = area,
            State = new Simulation절기운영CampaignStateSnapshot
            {
                CampaignStableId = "campaign:seasonal:lunch-peak",
                DefinitionRevision = "campaign.definition.r1",
                AreaStableId = area,
                StateCode = Simulation절기운영CampaignCodes.Active,
                CurrentPhaseCode = "LunchPeak",
                CurrentPhaseOrdinal = 1,
                PhaseCount = 2,
                CampaignRevision = 2,
                AvailableActions =
                    [Simulation절기운영CampaignCodes.PreviewAdvance],
                SimulationOnly = true,
            },
        };

    private static SeasonalCampaignObservationSourceUpdate Fresh(
        string source, string revision, long ordinal)
        => new()
        {
            SourceCode = source,
            Revision = revision,
            RevisionOrdinal = ordinal,
            SuccessfulAtUtc = ObservedAt,
            Succeeded = true,
        };

    private static SeasonalCampaignObservationSourceUpdate Failed(
        string source, string error)
        => new()
        {
            SourceCode = source,
            ErrorCode = error,
        };

    private sealed class RecordingTarget
        : ISeasonalCampaignPresentationTarget
    {
        public SeasonalCampaignScreenModel? ScreenModel { get; private set; }
        public SeasonalCampaignSourceDiagnostic[] Diagnostics
            { get; private set; } = [];
        public SeasonalCampaignCompositionRevision[] Revisions
            { get; private set; } = [];

        public void ApplySeasonalCampaign(
            SeasonalCampaignScreenModel screenModel,
            SeasonalCampaignSourceDiagnostic[] sourceDiagnostics,
            SeasonalCampaignCompositionRevision[] compositionRevisionSet)
        {
            ScreenModel = screenModel;
            Diagnostics = sourceDiagnostics;
            Revisions = compositionRevisionSet;
        }
    }
}
