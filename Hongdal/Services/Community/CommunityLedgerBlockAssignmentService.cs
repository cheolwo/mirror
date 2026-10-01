using FluentResults;
using Hongdal.Contracts.Common.Community;
using Microsoft.AspNetCore.Http;

namespace Hongdal.Services.Community;

public interface ICommunityLedgerBlockAssignmentService
{
    Task<Result<CommunityLedgerBlockAssignmentSettingsResponse>> GetAsync(
        string ledgerId,
        string blockId,
        string? userId,
        CancellationToken cancellationToken);

    Task<Result<CommunityLedgerBlockAssignmentSettingsResponse>> UpdateAsync(
        string ledgerId,
        string blockId,
        CommunityLedgerBlockAssignmentUpdateRequest request,
        string? userId,
        CancellationToken cancellationToken);
}

public sealed class CommunityLedgerBlockAssignmentService : ICommunityLedgerBlockAssignmentService
{
    private readonly I커뮤니티원장저장소 _ledgerStore;

    public CommunityLedgerBlockAssignmentService(I커뮤니티원장저장소 ledgerStore)
    {
        _ledgerStore = ledgerStore;
    }

    public async Task<Result<CommunityLedgerBlockAssignmentSettingsResponse>> GetAsync(
        string ledgerId,
        string blockId,
        string? userId,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerStore.원장조회Async(ledgerId, cancellationToken);
        var validation = ValidateReadAccess(ledger, blockId, userId);
        if (validation is not null)
        {
            return validation;
        }

        var block = ledger!.블록목록.First(item =>
            string.Equals(item.BlockId, blockId.Trim(), StringComparison.OrdinalIgnoreCase));
        return Result.Ok(BuildResponse(ledger, block, userId));
    }

    public async Task<Result<CommunityLedgerBlockAssignmentSettingsResponse>> UpdateAsync(
        string ledgerId,
        string blockId,
        CommunityLedgerBlockAssignmentUpdateRequest request,
        string? userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ledger = await _ledgerStore.원장조회Async(ledgerId, cancellationToken);
        var validation = ValidateReadAccess(ledger, blockId, userId);
        if (validation is not null)
        {
            return validation;
        }

        if (!CommunityLedgerBlockAssignmentPolicy.CanManage(ledger!, userId))
        {
            return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(
                "원장 생성자 또는 대표·담당·결정 역할의 참여자만 블록 담당자를 변경할 수 있습니다.",
                StatusCodes.Status403Forbidden);
        }

        if (request.Assignments.Count > 20)
        {
            return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(
                "한 블록에는 담당자를 최대 20명까지 지정할 수 있습니다.",
                StatusCodes.Status400BadRequest);
        }

        var candidates = CommunityLedgerBlockAssignmentPolicy.ResolveCandidates(ledger!);
        var candidateById = candidates.ToDictionary(candidate => candidate.UserId, StringComparer.OrdinalIgnoreCase);
        var assignments = new List<커뮤니티원장블록담당자Dto>();
        foreach (var assignment in request.Assignments
                     .GroupBy(item => item.UserId?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.Last()))
        {
            if (string.IsNullOrWhiteSpace(assignment.UserId)
                || !candidateById.TryGetValue(assignment.UserId.Trim(), out var candidate))
            {
                return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(
                    "현재 원장에 등록된 참여자만 블록 담당자로 지정할 수 있습니다.",
                    StatusCodes.Status400BadRequest);
            }

            if (!CommunityLedgerBlockResponsibilityTypes.IsSupported(assignment.ResponsibilityType))
            {
                return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(
                    "담당 유형은 주담당, 협업, 검토 중 하나여야 합니다.",
                    StatusCodes.Status400BadRequest);
            }

            var responsibilityType = assignment.ResponsibilityType;
            assignments.Add(new 커뮤니티원장블록담당자Dto
            {
                UserId = candidate.UserId,
                DisplayName = candidate.DisplayName,
                RoleLabel = candidate.RoleLabel,
                ResponsibilityType = responsibilityType
            });
        }

        var primaryCount = assignments.Count(item =>
            item.ResponsibilityType == CommunityLedgerBlockResponsibilityTypes.Primary);
        if (assignments.Count > 0 && primaryCount != 1)
        {
            return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(
                "담당자를 지정할 때는 주담당자를 정확히 한 명 선택해야 합니다.",
                StatusCodes.Status400BadRequest);
        }

        var normalizedBlockId = blockId.Trim();
        var blocks = ledger!.블록목록.Select(block =>
            string.Equals(block.BlockId, normalizedBlockId, StringComparison.OrdinalIgnoreCase)
                ? CloneBlock(block, assignments)
                : CloneBlock(block, block.담당자목록)).ToArray();

        커뮤니티원장Dto saved;
        try
        {
            saved = await _ledgerStore.원장저장Async(
                new 커뮤니티원장저장요청
                {
                    원장Id = ledger.원장Id,
                    기대Revision = request.ExpectedRevision ?? ledger.Revision,
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
                    블록담당자명시적갱신여부 = true,
                    참여자목록 = ledger.참여자목록,
                    포함원장목록 = ledger.포함원장목록,
                    다이어그램스냅샷 = ledger.다이어그램스냅샷,
                    외부참조 = ledger.외부참조,
                    확장속성 = ledger.확장속성
                },
                userId!.Trim(),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(ex.Message, StatusCodes.Status409Conflict);
        }

        var savedBlock = saved.블록목록.First(block =>
            string.Equals(block.BlockId, normalizedBlockId, StringComparison.OrdinalIgnoreCase));
        return Result.Ok(BuildResponse(saved, savedBlock, userId));
    }

    private static Result<CommunityLedgerBlockAssignmentSettingsResponse>? ValidateReadAccess(
        커뮤니티원장Dto? ledger,
        string blockId,
        string? userId)
    {
        if (ledger is null)
        {
            return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(
                "원장을 찾을 수 없습니다.",
                StatusCodes.Status404NotFound);
        }

        if (!CommunityLedgerBlockAssignmentPolicy.HasDirectAccess(ledger, userId))
        {
            return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(
                "원장 생성자 또는 참여자만 블록 담당자를 확인할 수 있습니다.",
                StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(blockId)
            || !ledger.블록목록.Any(block =>
                string.Equals(block.BlockId, blockId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Fail<CommunityLedgerBlockAssignmentSettingsResponse>(
                "담당자를 지정할 원장 블록을 찾을 수 없습니다.",
                StatusCodes.Status404NotFound);
        }

        return null;
    }

    private static CommunityLedgerBlockAssignmentSettingsResponse BuildResponse(
        커뮤니티원장Dto ledger,
        커뮤니티원장블록Dto block,
        string? userId)
        => new()
        {
            LedgerId = ledger.원장Id,
            Revision = ledger.Revision,
            BlockId = block.BlockId,
            BlockTitle = block.Title,
            CanManage = CommunityLedgerBlockAssignmentPolicy.CanManage(ledger, userId),
            Candidates = CommunityLedgerBlockAssignmentPolicy.ResolveCandidates(ledger),
            Assignments = block.담당자목록.Select(ToResponse).ToArray()
        };

    private static PlatformCommunityLedgerBlockAssigneeResponse ToResponse(커뮤니티원장블록담당자Dto assignee)
        => new()
        {
            UserId = assignee.UserId,
            DisplayName = assignee.DisplayName,
            RoleLabel = assignee.RoleLabel,
            ResponsibilityType = assignee.ResponsibilityType,
            ResponsibilityName = CommunityLedgerBlockResponsibilityTypes.DisplayName(assignee.ResponsibilityType)
        };

    private static 커뮤니티원장블록Dto CloneBlock(
        커뮤니티원장블록Dto block,
        IReadOnlyList<커뮤니티원장블록담당자Dto> assignments)
        => new()
        {
            BlockId = block.BlockId,
            BlockType = block.BlockType,
            Title = block.Title,
            State = block.State,
            담당자목록 = assignments,
            Data = block.Data
        };

    private static Result<T> Fail<T>(string message, int statusCode)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", statusCode));
}

public static class CommunityLedgerBlockAssignmentPolicy
{
    private static readonly string[] ManagerRoleHints =
    [
        "대표", "담당", "결정", "구성자", "관리자"
    ];

    private static readonly string[] InactiveParticipationStates =
    [
        "탈퇴", "취소", "거절", "종료"
    ];

    public static bool HasDirectAccess(커뮤니티원장Dto ledger, string? userId)
        => !string.IsNullOrWhiteSpace(userId)
           && (string.Equals(ledger.생성자UserId, userId.Trim(), StringComparison.OrdinalIgnoreCase)
               || ledger.참여자목록.Any(participant =>
                   string.Equals(participant.UserId, userId.Trim(), StringComparison.OrdinalIgnoreCase)));

    public static bool CanManage(커뮤니티원장Dto ledger, string? userId)
        => !string.IsNullOrWhiteSpace(userId)
           && (string.Equals(ledger.생성자UserId, userId.Trim(), StringComparison.OrdinalIgnoreCase)
               || ledger.참여자목록.Any(participant =>
                   string.Equals(participant.UserId, userId.Trim(), StringComparison.OrdinalIgnoreCase)
                   && !IsInactive(participant.ParticipationState)
                   && ManagerRoleHints.Any(hint =>
                       participant.RoleLabel.Contains(hint, StringComparison.OrdinalIgnoreCase))));

    public static IReadOnlyList<CommunityLedgerBlockAssigneeCandidateResponse> ResolveCandidates(
        커뮤니티원장Dto ledger)
    {
        var candidates = ledger.참여자목록
            .Where(participant => !string.IsNullOrWhiteSpace(participant.UserId)
                                  && !IsInactive(participant.ParticipationState))
            .Select(participant => new CommunityLedgerBlockAssigneeCandidateResponse
            {
                UserId = participant.UserId!.Trim(),
                DisplayName = participant.DisplayName,
                RoleLabel = participant.RoleLabel,
                ParticipationState = participant.ParticipationState
            })
            .ToList();
        if (!string.IsNullOrWhiteSpace(ledger.생성자UserId)
            && candidates.All(candidate =>
                !string.Equals(candidate.UserId, ledger.생성자UserId, StringComparison.OrdinalIgnoreCase)))
        {
            candidates.Insert(0, new CommunityLedgerBlockAssigneeCandidateResponse
            {
                UserId = ledger.생성자UserId.Trim(),
                DisplayName = ledger.생성자표시명,
                RoleLabel = "원장 생성자",
                ParticipationState = "참여중"
            });
        }

        return candidates
            .GroupBy(candidate => candidate.UserId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.RoleLabel)
            .ThenBy(candidate => candidate.DisplayName)
            .ToArray();
    }

    private static bool IsInactive(string participationState)
        => InactiveParticipationStates.Any(state =>
            participationState.Contains(state, StringComparison.OrdinalIgnoreCase));
}
