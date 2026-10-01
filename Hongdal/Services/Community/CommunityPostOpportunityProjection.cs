using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

internal static class CommunityPostOpportunityProjection
{
    public static async Task<CommunityVoteResponse?> FindParticipationVoteAsync(
        ICommunityVoteService voteService,
        long postId,
        CancellationToken cancellationToken)
    {
        var votes = await voteService.ListBySourcePostAsync(postId, cancellationToken);
        var interestVotes = votes.Items
            .Where(vote => vote.VoteKind == CommunityVoteKindCodes.CollectiveActionInterest)
            .ToArray();
        return interestVotes.FirstOrDefault(vote => vote.Status == CommunityVoteStatusCodes.Open)
               ?? interestVotes.FirstOrDefault();
    }

    public static CommunityPostOpportunityResponse BuildOpportunity(
        CommunityPostOpportunitySource source,
        CommunityPostOpportunityAnalysis analysis,
        string language)
    {
        var expectedLedgerId = MeatImportReadinessCaseIds.FromCommunityPost(source.PostId);
        var active = string.Equals(source.LinkedLedgerId, expectedLedgerId, StringComparison.OrdinalIgnoreCase);
        var blocked = source.LinkedLedgerId is not null && !active;
        var english = string.Equals(language, CommunityDisplayLanguageCodes.English, StringComparison.OrdinalIgnoreCase);

        return new CommunityPostOpportunityResponse
        {
            Code = CommunityPostOpportunityCodes.MeatImportReadiness,
            StateCode = active
                ? CommunityPostOpportunityStateCodes.Active
                : blocked
                    ? CommunityPostOpportunityStateCodes.BlockedByAnotherLedger
                    : CommunityPostOpportunityStateCodes.Suggested,
            Title = english ? "Review meat import readiness" : "육류 수입 준비 정보 확인",
            Summary = english
                ? "Review the same information-only checklist together before either party begins an import transaction."
                : "어느 한쪽이 수입 업무를 실행하기 전에 국내외 당사자가 같은 정보 제공용 절차표를 함께 확인합니다.",
            WhySuggested = english
                ? "The post contains both meat-product and cross-border trade signals. Nothing starts automatically."
                : "게시글에서 육류 제품과 국경 간 거래 신호가 함께 확인되었습니다. 어떤 업무도 자동으로 시작하지 않습니다.",
            LedgerTemplateKey = CommunityLedgerTemplateKeys.MeatImportReadiness,
            CanStart = !active && !blocked,
            AutoStartsWorkflow = false,
            RequiresExplicitConsent = true,
            InformationOnly = true,
            IsBrokerageEnabled = false,
            PreviewEndpoint = $"/api/v1/agricultural-fisheries/import-readiness/diagram?displayLanguage={language}",
            StartEndpoint = $"/api/v1/community/posts/{source.PostId}/opportunities/meat-import-readiness/start",
            MatchedSignals = analysis.MatchedSignals,
            MissingInformationPrompts = english
                ? ["Which beef or pork product is involved?", "What is the origin country and HS code?", "Who will participate on the Korean and overseas sides?"]
                : ["소고기·돼지고기 중 어떤 제품인가요?", "원산지 국가와 HS 코드는 무엇인가요?", "한국 측과 해외 측에서 누가 함께 확인하나요?"]
        };
    }

    public static StartCommunityPostParticipationResponse BuildParticipationStartResponse(
        CommunityPostOpportunitySource source,
        string language,
        CommunityVoteResponse vote,
        bool reused)
        => new()
        {
            PostId = source.PostId,
            DisplayLanguageCode = language,
            ReusedExistingInterestVote = reused,
            Participation = BuildParticipationEntry(source, language, vote),
            InterestVote = vote
        };

    public static CommunityPostParticipationEntryResponse BuildParticipationEntry(
        CommunityPostOpportunitySource source,
        string language,
        CommunityVoteResponse? vote,
        커뮤니티원장Dto? provisionalLedger = null)
    {
        var english = string.Equals(language, CommunityDisplayLanguageCodes.English, StringComparison.OrdinalIgnoreCase);
        if (source.IsReportBoardPost)
        {
            return new CommunityPostParticipationEntryResponse
            {
                StateCode = CommunityPostParticipationStateCodes.Closed,
                Title = english ? "Collective action is unavailable" : "공동 행동을 시작할 수 없습니다",
                Summary = english
                    ? "Report and dispute posts remain separate from transaction formation."
                    : "신고·분쟁 게시글은 거래 참여와 가원장 구성에서 분리됩니다.",
                CanStart = false,
                CanJoin = false,
                NonBinding = true,
                RoleOptions = []
            };
        }

        var open = vote?.Status == CommunityVoteStatusCodes.Open;
        var provisionalLedgerId = vote?.CommunityLedgerId;
        var promoted = !string.IsNullOrWhiteSpace(provisionalLedgerId);
        return new CommunityPostParticipationEntryResponse
        {
            StateCode = promoted
                ? CommunityPostParticipationStateCodes.ProvisionalLedgerCreated
                : vote is null
                    ? CommunityPostParticipationStateCodes.Available
                    : open
                        ? CommunityPostParticipationStateCodes.Gathering
                        : CommunityPostParticipationStateCodes.Closed,
            Title = english ? "Maybe we can do this together" : "같이 해볼까요?",
            Summary = english
                ? "Express interest as a buyer, supplier, logistics professional, or observer without committing to a transaction."
                : "구매자·공급자·물류 전문가·관심 참여자 중 가능한 역할을 부담 없이 표시합니다.",
            CanStart = !promoted && !open,
            CanJoin = !promoted && open,
            AutoStartsWorkflow = false,
            NonBinding = true,
            RequiresExplicitStart = true,
            RequiresExplicitPromotionToPlanning = true,
            CanPromoteToProvisionalLedger = !promoted
                                            && open
                                            && vote!.TotalVoteCount >= CommunityPostProvisionalLedgerPolicy.MinimumParticipantCount
                                            && source.LinkedLedgerId is null,
            InterestVoteId = vote?.Id,
            ProvisionalLedgerId = provisionalLedgerId,
            ParticipantCount = vote?.TotalVoteCount ?? 0,
            StartEndpoint = $"/api/v1/community/posts/{source.PostId}/opportunities/participation/start",
            JoinEndpoint = vote is null ? string.Empty : $"/api/v1/community/votes/{vote.Id:D}/votes",
            ProvisionalLedgerEndpoint = vote is null
                ? string.Empty
                : $"/api/v1/community/posts/{source.PostId}/opportunities/participation/provisional-ledger",
            PlanningSourceReferenceId = vote?.Id.ToString("D") ?? string.Empty,
            RoleOptions = BuildRoleDefinitions(language).Select(role =>
            {
                var option = vote?.Options.FirstOrDefault(candidate => string.Equals(
                    candidate.ProductKey,
                    RoleProductKey(role.RoleCode),
                    StringComparison.OrdinalIgnoreCase));
                return new CommunityPostParticipationRoleOptionResponse
                {
                    RoleCode = role.RoleCode,
                    OptionId = option?.OptionId ?? string.Empty,
                    Label = role.Label,
                    Summary = role.Summary,
                    InterestCount = option?.VoteCount ?? 0
                };
            }).ToArray(),
            ProfessionalParticipation = CommunityPostProfessionalParticipationProjection.BuildResponse(
                provisionalLedger,
                source.PostId,
                language),
            PartyFormation = CommunityPostProfessionalParticipationProjection.BuildPartyFormationResponse(
                provisionalLedger,
                language)
        };
    }

    public static IReadOnlyList<CommunityVoteOptionCreateRequest> BuildInterestVoteOptions(string language)
        => BuildRoleDefinitions(language)
            .Select(role => new CommunityVoteOptionCreateRequest
            {
                Text = role.Label,
                ProductKey = RoleProductKey(role.RoleCode)
            })
            .ToArray();

    private static IReadOnlyList<ParticipationRoleDefinition> BuildRoleDefinitions(string language)
    {
        var english = string.Equals(language, CommunityDisplayLanguageCodes.English, StringComparison.OrdinalIgnoreCase);
        return english
            ?
            [
                new(CommunityPostParticipationRoleCodes.Buyer, "Interested buyer", "I may join the purchase or import."),
                new(CommunityPostParticipationRoleCodes.Supplier, "Potential supplier", "I may be able to supply the product."),
                new(CommunityPostParticipationRoleCodes.FreightBroker, "Broker or forwarder interest", "I may join subject to separate authority and license verification."),
                new(CommunityPostParticipationRoleCodes.Carrier, "Carrier", "I may provide transportation."),
                new(CommunityPostParticipationRoleCodes.CustomsBroker, "Customs professional interest", "I may help review customs questions before separate credential verification and engagement."),
                new(CommunityPostParticipationRoleCodes.WarehouseOperator, "Warehouse operator", "I may provide storage or handling."),
                new(CommunityPostParticipationRoleCodes.Facilitator, "Conversation facilitator", "I may help participants organize the discussion."),
                new(CommunityPostParticipationRoleCodes.FollowOnly, "Follow this", "I only want to follow the conversation for now.")
            ]
            :
            [
                new(CommunityPostParticipationRoleCodes.Buyer, "구매에 관심 있어요", "공동구매나 공동수입에 참여할 수 있어요."),
                new(CommunityPostParticipationRoleCodes.Supplier, "공급할 수 있어요", "상품 공급 가능성을 함께 검토할 수 있어요."),
                new(CommunityPostParticipationRoleCodes.FreightBroker, "운송 주선 검토로 도울 수 있어요", "관할 면허·등록을 별도로 확인한 뒤 가능한 범위에서 참여할 수 있어요."),
                new(CommunityPostParticipationRoleCodes.Carrier, "운송할 수 있어요", "운송 업무 제공 가능성을 검토할 수 있어요."),
                new(CommunityPostParticipationRoleCodes.CustomsBroker, "통관 검토로 도울 수 있어요", "자격과 수임을 별도로 확인하기 전 관세·통관 쟁점 검토에 관심을 표시해요."),
                new(CommunityPostParticipationRoleCodes.WarehouseOperator, "보관·하역으로 도울 수 있어요", "창고 보관이나 현장 작업을 제공할 수 있어요."),
                new(CommunityPostParticipationRoleCodes.Facilitator, "대화를 정리할 수 있어요", "참여자들의 의견과 다음 단계를 정리할 수 있어요."),
                new(CommunityPostParticipationRoleCodes.FollowOnly, "일단 지켜볼게요", "아직 약속하지 않고 대화만 이어서 볼게요.")
            ];
    }

    private static string RoleProductKey(string roleCode)
        => $"community-role:{roleCode}";

    private sealed record ParticipationRoleDefinition(
        string RoleCode,
        string Label,
        string Summary);
}
