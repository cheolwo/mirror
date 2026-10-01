using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum CommunityPostPrimaryActionKind
{
    None,
    StartGathering,
    ShowParticipationDetails,
    OpenJourney
}

public sealed record CommunityPostActionFlowStageViewModel(
    string Code,
    string Label,
    bool IsCurrent,
    bool IsComplete);

public sealed class CommunityPostActionFlowViewModel
{
    private static readonly IReadOnlyList<(string Code, string Label)> StageDefinitions =
    [
        (CommunityActionJourneyStageCodes.Conversation, "이야기"),
        (CommunityActionJourneyStageCodes.Gathering, "마음"),
        (CommunityActionJourneyStageCodes.ProvisionalLedger, "가원장"),
        (CommunityActionJourneyStageCodes.Conditions, "조건·역할"),
        (CommunityActionJourneyStageCodes.InProgress, "실행"),
        (CommunityActionJourneyStageCodes.Completed, "완료")
    ];

    private CommunityPostActionFlowViewModel()
    {
    }

    public long PostId { get; private init; }
    public string CurrentStageCode { get; private init; } = CommunityActionJourneyStageCodes.Conversation;
    public string CurrentStageLabel { get; private init; } = "이야기 나누는 중";
    public string Title { get; private init; } = string.Empty;
    public string Summary { get; private init; } = string.Empty;
    public string PrimaryActionLabel { get; private init; } = string.Empty;
    public string? PrimaryActionRoute { get; private init; }
    public CommunityPostPrimaryActionKind PrimaryActionKind { get; private init; }
    public int ParticipantCount { get; private init; }
    public int RequiredRoleCount { get; private init; }
    public int FilledRequiredRoleCount { get; private init; }
    public bool HasStarted { get; private init; }
    public bool HasParticipationDetails { get; private init; }
    public IReadOnlyList<CommunityPostActionFlowStageViewModel> Stages { get; private init; } = [];

    public static CommunityPostActionFlowViewModel Create(
        CommunityPostOpportunityListResponse opportunity)
    {
        ArgumentNullException.ThrowIfNull(opportunity);

        var participation = opportunity.Participation;
        var journey = opportunity.Journey;
        var stageCode = NormalizeDisplayStage(journey.CurrentStageCode);
        var stageIndex = DisplayStageIndex(journey.CurrentStageCode);
        var primary = ResolvePrimaryAction(opportunity);

        return new CommunityPostActionFlowViewModel
        {
            PostId = opportunity.PostId,
            CurrentStageCode = stageCode,
            CurrentStageLabel = journey.CurrentStageLabel,
            Title = primary.Title,
            Summary = primary.Summary,
            PrimaryActionLabel = primary.Label,
            PrimaryActionRoute = primary.Route,
            PrimaryActionKind = primary.Kind,
            ParticipantCount = journey.ParticipantCount > 0
                ? journey.ParticipantCount
                : participation.ParticipantCount,
            RequiredRoleCount = journey.RequiredRoleCount,
            FilledRequiredRoleCount = journey.FilledRequiredRoleCount,
            HasStarted = journey.HasStarted,
            HasParticipationDetails = participation.CanJoin
                                      || participation.CanPromoteToProvisionalLedger
                                      || participation.PartyFormation.IsAvailable,
            Stages = StageDefinitions
                .Select((stage, index) => new CommunityPostActionFlowStageViewModel(
                    stage.Code,
                    stage.Label,
                    IsCurrent: index == stageIndex,
                    IsComplete: index < stageIndex))
                .ToArray()
        };
    }

    private static CommunityPostPrimaryAction ResolvePrimaryAction(
        CommunityPostOpportunityListResponse opportunity)
    {
        var participation = opportunity.Participation;
        var journey = opportunity.Journey;

        if (!journey.IsAvailable)
        {
            return new(
                CommunityPostPrimaryActionKind.None,
                "대화를 이어가 주세요",
                "이 글은 공동행동으로 전환하지 않고 게시글과 댓글 안에서 다룹니다.",
                string.Empty,
                null);
        }

        if (journey.CurrentStageCode == CommunityActionJourneyStageCodes.Completed)
        {
            return OpenJourney(
                opportunity,
                CommunityCollectiveActionPageKeys.Completed,
                "함께 만든 결과가 남았어요",
                "완료된 과정과 개인정보를 줄인 공동 기록을 확인할 수 있습니다.",
                "완료 기록 보기");
        }

        if (journey.CurrentStageCode == CommunityActionJourneyStageCodes.InProgress)
        {
            return OpenJourney(
                opportunity,
                CommunityCollectiveActionPageKeys.InProgress,
                "지금 함께 진행하고 있어요",
                "현재 진행 상태와 추가로 참여할 수 있는 여력을 확인해 보세요.",
                "진행 상황 보기");
        }

        if (journey.CurrentStageCode == CommunityActionJourneyStageCodes.Readiness)
        {
            return OpenJourney(
                opportunity,
                CommunityCollectiveActionPageKeys.Readiness,
                "실행 전 확인이 남았어요",
                "합의한 조건과 역할 수락 상태를 당사자가 직접 확인합니다.",
                "실행 전 확인하기");
        }

        if (journey.CurrentStageCode == CommunityActionJourneyStageCodes.Party)
        {
            return OpenJourney(
                opportunity,
                CommunityCollectiveActionPageKeys.Party,
                "함께할 역할을 채우는 중이에요",
                "비어 있는 거래 당사자와 전문 역할을 확인하고 자발적으로 참여할 수 있습니다.",
                "함께할 역할 보기");
        }

        if (journey.CurrentStageCode == CommunityActionJourneyStageCodes.Conditions)
        {
            return OpenJourney(
                opportunity,
                CommunityCollectiveActionPageKeys.Conditions,
                "우리 조건을 맞추는 중이에요",
                "수량, 가격, 일정과 수령 조건을 함께 확인합니다.",
                "조건 함께 맞추기");
        }

        if (journey.CurrentStageCode == CommunityActionJourneyStageCodes.ProvisionalLedger)
        {
            return OpenJourney(
                opportunity,
                CommunityCollectiveActionPageKeys.Gathering,
                "모인 마음이 가원장으로 이어졌어요",
                "아직 계약은 아니며, 필요한 조건과 역할을 하나씩 확인하는 단계입니다.",
                "가원장 이어서 보기");
        }

        if (participation.CanPromoteToProvisionalLedger)
        {
            return new(
                CommunityPostPrimaryActionKind.ShowParticipationDetails,
                "가원장으로 이어갈 만큼 마음이 모였어요",
                "글 작성자가 공동구매·공동수입·공동수출 중 어떤 방향으로 조건을 모을지 정할 수 있습니다.",
                "가원장 방향 정하기",
                null);
        }

        if (participation.CanJoin)
        {
            return new(
                CommunityPostPrimaryActionKind.ShowParticipationDetails,
                "내가 할 수 있는 만큼 가볍게 참여해요",
                "가능한 역할을 선택해 마음을 보탤 수 있습니다. 이 선택만으로 주문이나 계약은 생기지 않습니다.",
                "가능한 역할 고르기",
                null);
        }

        if (participation.CanStart)
        {
            return new(
                CommunityPostPrimaryActionKind.StartGathering,
                "이야기에서 마음을 모아볼 수 있어요",
                "관심 있는 사람이 있는지 먼저 확인하고, 충분히 모였을 때만 다음 단계를 검토합니다.",
                "이 글에서 마음 모으기",
                null);
        }

        if (journey.HasStarted)
        {
            return OpenJourney(
                opportunity,
                CommunityCollectiveActionPageKeys.Gathering,
                "다른 참여를 기다리고 있어요",
                "현재 모인 마음과 다음 단계에 필요한 조건을 확인할 수 있습니다.",
                "모인 마음 이어서 보기");
        }

        return new(
            CommunityPostPrimaryActionKind.None,
            "이야기를 나누는 중이에요",
            "댓글로 질문과 경험을 나누며 함께할 가능성을 살펴봅니다.",
            string.Empty,
            null);
    }

    private static CommunityPostPrimaryAction OpenJourney(
        CommunityPostOpportunityListResponse opportunity,
        string pageKey,
        string title,
        string summary,
        string label)
        => new(
            CommunityPostPrimaryActionKind.OpenJourney,
            title,
            summary,
            label,
            CommunityCollectiveActionRoutes.Build(
                pageKey,
                opportunity.Participation.InterestVoteId));

    private static string NormalizeDisplayStage(string? stageCode)
        => DisplayStageIndex(stageCode) switch
        {
            1 => CommunityActionJourneyStageCodes.Gathering,
            2 => CommunityActionJourneyStageCodes.ProvisionalLedger,
            3 => CommunityActionJourneyStageCodes.Conditions,
            4 => CommunityActionJourneyStageCodes.InProgress,
            5 => CommunityActionJourneyStageCodes.Completed,
            _ => CommunityActionJourneyStageCodes.Conversation
        };

    private static int DisplayStageIndex(string? stageCode)
        => stageCode switch
        {
            CommunityActionJourneyStageCodes.Gathering => 1,
            CommunityActionJourneyStageCodes.ProvisionalLedger => 2,
            CommunityActionJourneyStageCodes.Conditions => 3,
            CommunityActionJourneyStageCodes.Party => 3,
            CommunityActionJourneyStageCodes.Readiness => 3,
            CommunityActionJourneyStageCodes.InProgress => 4,
            CommunityActionJourneyStageCodes.Completed => 5,
            _ => 0
        };

    private sealed record CommunityPostPrimaryAction(
        CommunityPostPrimaryActionKind Kind,
        string Title,
        string Summary,
        string Label,
        string? Route);
}
