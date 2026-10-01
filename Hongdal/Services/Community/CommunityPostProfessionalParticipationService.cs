using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Community;

public sealed class CommunityPostProfessionalParticipationService : ICommunityPostProfessionalParticipationService
{
    private readonly ICommunityPostOpportunityStore _postStore;
    private readonly I커뮤니티원장저장소 _ledgerStore;
    private readonly ICommunityProfessionalEligibilityService _eligibilityService;

    public CommunityPostProfessionalParticipationService(
        ICommunityPostOpportunityStore postStore,
        I커뮤니티원장저장소 ledgerStore,
        ICommunityProfessionalEligibilityService eligibilityService)
    {
        _postStore = postStore;
        _ledgerStore = ledgerStore;
        _eligibilityService = eligibilityService;
    }

    public async Task<JoinCommunityPostProfessionalResponse> JoinAsync(
        long postId,
        JoinCommunityPostProfessionalRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);
        if (!request.ConfirmProfessionalCapacity
            || !request.ConfirmVoluntaryNonBindingParticipation
            || !request.ConfirmParticipantNotification)
        {
            throw new InvalidOperationException(
                "전문 역할 확인, 자발적 비구속 참여, 기존 참여자 알림을 모두 명시적으로 확인해야 합니다.");
        }

        var roleCode = NormalizeProfessionalRoleCode(request.ProfessionalRoleCode);
        var verifiedRoles = await _eligibilityService.GetVerifiedRoleCodesAsync(actor, cancellationToken);
        if (!verifiedRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "현재 계정의 플랫폼 프로필에서 해당 역할을 확인할 수 없습니다. 관할 면허·등록이 필요한 역할은 외부 자격 확인도 별도로 완료해야 합니다.");
        }

        var result = await JoinRoleCoreAsync(
            postId,
            request.ProvisionalLedgerId,
            roleCode,
            actor,
            actorDisplayName,
            CommunityPartyRoleConfirmationScopeCodes.PlatformProfileOnly,
            cancellationToken);
        return BuildProfessionalResponse(
            postId,
            request.DisplayLanguageCode,
            result.Ledger,
            roleCode,
            result.Reused);
    }

    public async Task<JoinCommunityPostPartyRoleResponse> JoinPartyRoleAsync(
        long postId,
        JoinCommunityPostPartyRoleRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequireActor(actorUserId);
        if (!request.ConfirmRoleCapacity
            || !request.ConfirmVoluntaryNonBindingParticipation
            || !request.ConfirmParticipantNotification)
        {
            throw new InvalidOperationException(
                "거래 당사자 역할, 자발적 비구속 참여, 기존 참여자 알림을 모두 명시적으로 확인해야 합니다.");
        }

        var roleCode = NormalizeCommercialPartyRoleCode(request.PartyRoleCode);
        var result = await JoinRoleCoreAsync(
            postId,
            request.ProvisionalLedgerId,
            roleCode,
            actor,
            actorDisplayName,
            CommunityPartyRoleConfirmationScopeCodes.ExplicitSelfAcceptance,
            cancellationToken);
        return BuildPartyRoleResponse(
            postId,
            request.DisplayLanguageCode,
            result.Ledger,
            roleCode,
            result.Reused);
    }

    private async Task<RoleJoinResult> JoinRoleCoreAsync(
        long postId,
        string? provisionalLedgerId,
        string roleCode,
        string actorUserId,
        string actorDisplayName,
        string verificationScopeCode,
        CancellationToken cancellationToken)
    {
        var source = await _postStore.GetAsync(postId, cancellationToken)
                     ?? throw new KeyNotFoundException("커뮤니티 게시글을 찾을 수 없습니다.");
        if (source.IsReportBoardPost)
        {
            throw new InvalidOperationException("신고·분쟁 게시글에서는 거래 역할에 참여할 수 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(source.LinkedLedgerId)
            || !string.Equals(source.LinkedLedgerId, provisionalLedgerId?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new CommunityPostOpportunityConflictException("게시글에 연결된 가원장이 요청과 일치하지 않습니다.");
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var ledger = await _ledgerStore.원장조회Async(source.LinkedLedgerId, cancellationToken)
                         ?? throw new KeyNotFoundException("연결된 가원장을 찾을 수 없습니다.");
            CommunityPostProfessionalParticipationProjection.EnsureProvisionalLedger(ledger);
            var availableRoles = CommunityPostPartyRoleCodes.ForPlan(
                CommunityPostProfessionalParticipationProjection.ReadTradeDirectionCode(ledger),
                CommunityPostProfessionalParticipationProjection.ReadTransportModeCodes(ledger),
                ledger.확장속성.GetValueOrDefault(
                    CommunityPostProvisionalLedgerPolicy.DestinationCountryAttributeKey));
            if (!availableRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("이 가원장의 거래 방향과 운송수단에서 모집 중인 역할이 아닙니다.");
            }

            var assignments = CommunityPostProfessionalParticipationProjection.ReadAssignments(ledger).ToList();
            var existingAssignment = assignments.FirstOrDefault(assignment =>
                string.Equals(assignment.UserId, actorUserId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(assignment.RoleCode, roleCode, StringComparison.OrdinalIgnoreCase));
            if (existingAssignment is not null)
            {
                await PromotePostMomentumAsync(postId, ledger, assignments, cancellationToken);
                return new RoleJoinResult(ledger, Reused: true);
            }

            assignments.Add(new CommunityPartyRoleAssignment
            {
                UserId = actorUserId,
                DisplayName = NormalizeDisplayName(actorDisplayName),
                RoleCode = roleCode,
                SourceCode = CommunityPartyRoleAssignmentSourceCodes.Joined,
                VerificationScopeCode = verificationScopeCode
            });
            var nextRevision = ledger.Revision + 1;
            var updatedRequest = BuildLedgerUpdateRequest(
                ledger,
                assignments,
                actorUserId,
                NormalizeDisplayName(actorDisplayName),
                roleCode,
                nextRevision);

            try
            {
                var saved = await _ledgerStore.원장저장Async(updatedRequest, actorUserId, cancellationToken);
                await PromotePostMomentumAsync(postId, saved, assignments, cancellationToken);
                return new RoleJoinResult(saved, Reused: false);
            }
            catch (InvalidOperationException) when (attempt < 4)
            {
                // The ledger store uses optimistic revisions. Reload and retry a concurrent role join.
            }
        }

        throw new InvalidOperationException("다른 참여자가 가원장을 먼저 변경했습니다. 최신 상태를 확인한 뒤 다시 시도해 주세요.");
    }

    private async Task PromotePostMomentumAsync(
        long postId,
        커뮤니티원장Dto ledger,
        IReadOnlyList<CommunityPartyRoleAssignment> assignments,
        CancellationToken cancellationToken)
    {
        var count = assignments
            .Select(assignment => assignment.UserId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var result = await _postStore.SetMomentumPromotionAsync(
            postId,
            ledger.원장Id,
            CommunityPostProfessionalParticipationProjection.ResolveMomentumCode(ledger, assignments),
            CommunityPostProfessionalParticipationProjection.ReadinessMessage(ledger, CommunityDisplayLanguageCodes.Korean),
            count,
            cancellationToken);
        if (result == CommunityPostMomentumUpdateResult.NotFound)
        {
            throw new KeyNotFoundException("커뮤니티 게시글을 찾을 수 없습니다.");
        }

        if (result == CommunityPostMomentumUpdateResult.ConflictingLedger)
        {
            throw new CommunityPostOpportunityConflictException("게시글의 가원장 연결이 변경되었습니다.");
        }
    }

    private static 커뮤니티원장저장요청 BuildLedgerUpdateRequest(
        커뮤니티원장Dto ledger,
        IReadOnlyList<CommunityPartyRoleAssignment> assignments,
        string actorUserId,
        string actorDisplayName,
        string roleCode,
        long nextRevision)
    {
        var attributes = new Dictionary<string, string>(ledger.확장속성, StringComparer.OrdinalIgnoreCase)
        {
            [CommunityPostProvisionalLedgerPolicy.ConfirmedPartyRoleAssignmentsAttributeKey] =
                CommunityPostProfessionalParticipationProjection.SerializeAssignments(assignments),
            [CommunityPostProvisionalLedgerPolicy.ConfirmedPartyRoleParticipantCountAttributeKey] = assignments
                .Select(assignment => assignment.UserId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count()
                .ToString(),
            [CommunityPostProvisionalLedgerPolicy.CommunityMomentumCodeAttributeKey] =
                CommunityPostProfessionalParticipationProjection.ResolveMomentumCode(ledger, assignments),
            [CommunityPostProvisionalLedgerPolicy.CommunityPromotionRequestedAttributeKey] = bool.TrueString,
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinedUserIdAttributeKey] = actorUserId,
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinedDisplayNameAttributeKey] = actorDisplayName,
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinedRoleCodeAttributeKey] = roleCode,
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinRevisionAttributeKey] = nextRevision.ToString()
        };
        var participants = MergeRoleParticipant(
            ledger.참여자목록,
            assignments,
            actorUserId,
            actorDisplayName);
        var blocks = ledger.블록목록
            .Select(block => string.Equals(
                    block.BlockId,
                    CommunityPostProvisionalLedgerPolicy.ProfessionalParticipationBlockId,
                    StringComparison.OrdinalIgnoreCase)
                ? CommunityPostProfessionalParticipationProjection.BuildProfessionalBlock(
                    CommunityPostProfessionalParticipationProjection.ResolveRequiredRoles(ledger),
                    assignments)
                : block)
            .ToList();
        if (blocks.All(block => !string.Equals(
                block.BlockId,
                CommunityPostProvisionalLedgerPolicy.ProfessionalParticipationBlockId,
                StringComparison.OrdinalIgnoreCase)))
        {
            blocks.Add(CommunityPostProfessionalParticipationProjection.BuildProfessionalBlock(
                CommunityPostProfessionalParticipationProjection.ResolveRequiredRoles(ledger),
                assignments));
        }

        return new 커뮤니티원장저장요청
        {
            원장Id = ledger.원장Id,
            기대Revision = ledger.Revision,
            커뮤니티Id = ledger.커뮤니티Id,
            원장템플릿Key = ledger.원장템플릿Key,
            제목 = ledger.제목,
            원함 = ledger.원함,
            상태 = ledger.상태,
            현재단계Key = ledger.현재단계Key,
            대상OsCode = ledger.대상OsCode,
            대상OsName = ledger.대상OsName,
            생성자UserId = ledger.생성자UserId,
            생성자표시명 = ledger.생성자표시명,
            블록목록 = blocks,
            참여자목록 = participants,
            포함원장목록 = ledger.포함원장목록,
            다이어그램스냅샷 = ledger.다이어그램스냅샷,
            외부참조 = ledger.외부참조,
            확장속성 = attributes
        };
    }

    private static IReadOnlyList<커뮤니티원장참여자Dto> MergeRoleParticipant(
        IReadOnlyList<커뮤니티원장참여자Dto> participants,
        IReadOnlyList<CommunityPartyRoleAssignment> assignments,
        string actorUserId,
        string actorDisplayName)
    {
        var result = participants.ToList();
        var actorIndex = result.FindIndex(participant => string.Equals(
            participant.UserId,
            actorUserId,
            StringComparison.OrdinalIgnoreCase));
        var actorAssignments = assignments
            .Where(assignment => string.Equals(assignment.UserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var labelSegments = new List<string>();
        var explicitlyAcceptedLabels = actorAssignments
            .Where(assignment => string.Equals(
                assignment.VerificationScopeCode,
                CommunityPartyRoleConfirmationScopeCodes.ExplicitSelfAcceptance,
                StringComparison.OrdinalIgnoreCase))
            .Select(assignment => CommunityPostProfessionalParticipationProjection.RoleLabel(assignment.RoleCode, false))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (explicitlyAcceptedLabels.Length > 0)
        {
            labelSegments.Add($"명시적 역할 수락 · {string.Join(", ", explicitlyAcceptedLabels)}");
        }

        var platformConfirmedLabels = actorAssignments
            .Where(assignment => !string.Equals(
                assignment.VerificationScopeCode,
                CommunityPartyRoleConfirmationScopeCodes.ExplicitSelfAcceptance,
                StringComparison.OrdinalIgnoreCase))
            .Select(assignment => CommunityPostProfessionalParticipationProjection.RoleLabel(assignment.RoleCode, false))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (platformConfirmedLabels.Length > 0)
        {
            labelSegments.Add($"플랫폼 역할 확인 · {string.Join(", ", platformConfirmedLabels)}");
        }

        var roleParticipationLabel = string.Join(" | ", labelSegments);
        if (actorIndex < 0)
        {
            result.Add(new 커뮤니티원장참여자Dto
            {
                UserId = actorUserId,
                DisplayName = actorDisplayName,
                RoleLabel = roleParticipationLabel,
                ParticipationState = "가원장 역할 참여"
            });
            return result;
        }

        var existing = result[actorIndex];
        var baseRoleLabel = string.Join(
            " | ",
            existing.RoleLabel
                .Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(segment => !segment.StartsWith("플랫폼 역할 확인", StringComparison.Ordinal)
                                  && !segment.StartsWith("명시적 역할 수락", StringComparison.Ordinal)
                                  && !segment.StartsWith("검증 전문가", StringComparison.Ordinal)));
        result[actorIndex] = new 커뮤니티원장참여자Dto
        {
            UserId = existing.UserId,
            DisplayName = string.IsNullOrWhiteSpace(existing.DisplayName) ? actorDisplayName : existing.DisplayName,
            RoleLabel = string.IsNullOrWhiteSpace(baseRoleLabel)
                ? roleParticipationLabel
                : $"{baseRoleLabel} | {roleParticipationLabel}",
            ParticipationState = "가원장 역할 참여"
        };
        return result;
    }

    private static JoinCommunityPostProfessionalResponse BuildProfessionalResponse(
        long postId,
        string? displayLanguageCode,
        커뮤니티원장Dto ledger,
        string roleCode,
        bool reused)
    {
        var language = CommunityDisplayLanguageCodes.Normalize(displayLanguageCode);
        return new JoinCommunityPostProfessionalResponse
        {
            PostId = postId,
            DisplayLanguageCode = language,
            ReusedExistingParticipation = reused,
            JoinedProfessionalRoleCode = roleCode,
            ProvisionalLedger = BuildLedgerResponse(ledger),
            Participation = BuildParticipationResponse(postId, ledger, language)
        };
    }

    private static JoinCommunityPostPartyRoleResponse BuildPartyRoleResponse(
        long postId,
        string? displayLanguageCode,
        커뮤니티원장Dto ledger,
        string roleCode,
        bool reused)
    {
        var language = CommunityDisplayLanguageCodes.Normalize(displayLanguageCode);
        return new JoinCommunityPostPartyRoleResponse
        {
            PostId = postId,
            DisplayLanguageCode = language,
            ReusedExistingParticipation = reused,
            JoinedPartyRoleCode = roleCode,
            ProvisionalLedger = BuildLedgerResponse(ledger),
            Participation = BuildParticipationResponse(postId, ledger, language)
        };
    }

    private static CommunityPostProvisionalLedgerResponse BuildLedgerResponse(커뮤니티원장Dto ledger)
        => new()
        {
            LedgerId = ledger.원장Id,
            Revision = ledger.Revision,
            LedgerTemplateKey = ledger.원장템플릿Key,
            State = ledger.상태,
            CurrentStageCode = ledger.현재단계Key ?? string.Empty,
            ParticipantCount = ledger.참여자목록.Count,
            EvidenceSnapshotHash = ledger.확장속성.GetValueOrDefault(
                CommunityPostProvisionalLedgerPolicy.EvidenceSnapshotHashAttributeKey,
                string.Empty),
            NonBinding = true,
            ParticipantNotificationsRequested = true,
            TradeDirectionCode = CommunityPostProfessionalParticipationProjection.ReadTradeDirectionCode(ledger),
            OriginCountryCode = ledger.확장속성.GetValueOrDefault(
                CommunityPostProvisionalLedgerPolicy.OriginCountryAttributeKey,
                string.Empty),
            DestinationCountryCode = ledger.확장속성.GetValueOrDefault(
                CommunityPostProvisionalLedgerPolicy.DestinationCountryAttributeKey,
                string.Empty),
            TransportModeCodes = CommunityPostProfessionalParticipationProjection.ReadTransportModeCodes(ledger)
        };

    private static CommunityPostParticipationEntryResponse BuildParticipationResponse(
        long postId,
        커뮤니티원장Dto ledger,
        string language)
        => new()
        {
            StateCode = CommunityPostParticipationStateCodes.ProvisionalLedgerCreated,
            Title = language == CommunityDisplayLanguageCodes.English ? "Maybe we can do this together" : "같이 해볼까요?",
            Summary = language == CommunityDisplayLanguageCodes.English
                ? "Transaction parties and platform-confirmed role participants may voluntarily join the non-binding provisional ledger."
                : "거래 당사자와 플랫폼에서 역할이 확인된 참여자가 비구속적 가원장에 자발적으로 참여할 수 있습니다.",
            CanStart = false,
            CanJoin = false,
            NonBinding = true,
            ProvisionalLedgerId = ledger.원장Id,
            ParticipantCount = ledger.참여자목록.Count,
            ProfessionalParticipation = CommunityPostProfessionalParticipationProjection.BuildResponse(
                ledger,
                postId,
                language),
            PartyFormation = CommunityPostProfessionalParticipationProjection.BuildPartyFormationResponse(
                ledger,
                language)
        };

    private static string NormalizeProfessionalRoleCode(string? value)
    {
        if (!CommunityPostPartyRoleCodes.IsSpecialist(value))
        {
            throw new InvalidOperationException("지원하지 않는 전문 참여 역할입니다.");
        }

        return CommunityPostPartyRoleCodes.SpecialistRoles.First(role => string.Equals(
            role,
            value!.Trim(),
            StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCommercialPartyRoleCode(string? value)
    {
        if (!CommunityPostPartyRoleCodes.IsCommercialParty(value))
        {
            throw new InvalidOperationException("지원하지 않는 거래 당사자 역할입니다.");
        }

        return CommunityPostPartyRoleCodes.CommercialPartyRoles.First(role => string.Equals(
            role,
            value!.Trim(),
            StringComparison.OrdinalIgnoreCase));
    }

    private static string RequireActor(string? actorUserId)
        => string.IsNullOrWhiteSpace(actorUserId)
            ? throw new UnauthorizedAccessException("거래 참여팀 역할을 수락하려면 로그인이 필요합니다.")
            : actorUserId.Trim();

    private static string NormalizeDisplayName(string? displayName)
    {
        var normalized = string.IsNullOrWhiteSpace(displayName) ? "역할 참여자" : displayName.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private sealed record RoleJoinResult(커뮤니티원장Dto Ledger, bool Reused);
}
