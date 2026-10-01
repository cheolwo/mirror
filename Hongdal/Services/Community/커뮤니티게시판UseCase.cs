using FluentResults;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Domain.Community;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Community;

public interface I커뮤니티게시판UseCase
{
    Task<Result<PlatformCommunityBoardListResponse>> 목록Async(
        string? appKey,
        string? status,
        bool includeReviewDetails,
        CancellationToken cancellationToken);

    Task<Result<PlatformCommunityBoardResponse>> 신청Async(
        PlatformCommunityBoardCreateRequest? request,
        string requesterUserId,
        string requesterDisplayName,
        CancellationToken cancellationToken);

    Task<Result<PlatformCommunityBoardResponse>> 승인Async(
        long id,
        PlatformCommunityBoardReviewRequest? request,
        string reviewerUserId,
        CancellationToken cancellationToken);

    Task<Result<PlatformCommunityBoardResponse>> 반려Async(
        long id,
        PlatformCommunityBoardReviewRequest? request,
        string reviewerUserId,
        CancellationToken cancellationToken);
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Content,
    HongdalModuleKind.Application,
    "게시판 목록과 참여자 개설 신청·운영자 승인·반려 상태를 영속 처리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "게시판 승인은 공개 대화 공간을 여는 운영 행위이며 거래 상대나 계약을 승인하는 행위가 아닙니다.")]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalUseCase("커뮤니티 게시판 개설", Summary = "커뮤니티 참여자가 게시판 개설을 신청하고 운영자가 승인 또는 반려합니다.")]
[HongdalUseCaseActor(HongdalActor.CommunityMember)]
[HongdalUseCaseActor(HongdalActor.PlatformOperator, HongdalUseCaseActorRole.Supporting)]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "커뮤니티게시글UseCase",
    Condition = "게시판 개설 신청이 승인되어 실제 글 작성 공간이 열리는 경우",
    Summary = "게시판 개설 흐름을 게시글 작성과 댓글 운영 흐름으로 확장합니다.")]
public sealed class 커뮤니티게시판UseCase : I커뮤니티게시판UseCase
{
    private readonly HongdalContext _db;

    public 커뮤니티게시판UseCase(HongdalContext db)
    {
        _db = db;
    }

    public async Task<Result<PlatformCommunityBoardListResponse>> 목록Async(
        string? appKey,
        string? status,
        bool includeReviewDetails,
        CancellationToken cancellationToken)
    {
        var normalizedAppKey = Normalize(appKey, "platform", 80);
        var normalizedStatus = Normalize(status, PlatformCommunityBoardRequestStatuses.Approved, 20);

        var query = _db.PlatformCommunityBoardRequests
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Where(x => x.AppKey == normalizedAppKey || x.AppKey == "platform");

        if (!string.Equals(normalizedStatus, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var entities = await query
            .OrderBy(x => x.Status == PlatformCommunityBoardRequestStatuses.Pending ? 0 : 1)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        var items = entities
            .Select(entity => ToResponse(entity, includeReviewDetails))
            .ToArray();

        return new PlatformCommunityBoardListResponse { Items = items };
    }

    public async Task<Result<PlatformCommunityBoardResponse>> 신청Async(
        PlatformCommunityBoardCreateRequest? request,
        string requesterUserId,
        string requesterDisplayName,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Fail<PlatformCommunityBoardResponse>("request body is required");
        }

        var normalizedRequesterUserId = Normalize(requesterUserId, string.Empty, 450);
        if (string.IsNullOrWhiteSpace(normalizedRequesterUserId))
        {
            return Result.Fail<PlatformCommunityBoardResponse>("로그인한 사용자만 게시판 개설을 신청할 수 있습니다.");
        }

        var title = Normalize(request.Title, string.Empty, 60);
        var requestedBy = Normalize(request.RequestedBy, requesterDisplayName, 40);
        var reason = Normalize(request.RequestReason, string.Empty, 1000);
        if (string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(requestedBy) ||
            string.IsNullOrWhiteSpace(reason))
        {
            return Result.Fail<PlatformCommunityBoardResponse>("게시판 이름, 신청자, 개설 이유를 입력해야 합니다.");
        }

        var appKey = Normalize(request.AppKey, "platform", 80);
        if (CommunityBoardCatalog.Find(title) is not null)
        {
            return Result.Fail<PlatformCommunityBoardResponse>("같은 이름의 기본 게시판이 이미 존재합니다.");
        }

        var pendingRequestCount = await _db.PlatformCommunityBoardRequests
            .CountAsync(x => x.RequestedByUserId == normalizedRequesterUserId
                             && x.Status == PlatformCommunityBoardRequestStatuses.Pending
                             && !x.IsDeleted,
                cancellationToken);
        if (pendingRequestCount >= 3)
        {
            return Result.Fail<PlatformCommunityBoardResponse>("승인 대기 중인 게시판 신청은 사용자당 최대 3개까지 가능합니다.");
        }

        var boardKey = CreateBoardKey(title);
        var exists = await _db.PlatformCommunityBoardRequests
            .AnyAsync(x => x.AppKey == appKey &&
                           x.BoardKey == boardKey &&
                           !x.IsDeleted &&
                           x.Status != PlatformCommunityBoardRequestStatuses.Rejected,
                cancellationToken);
        if (exists)
        {
            return Result.Fail<PlatformCommunityBoardResponse>("이미 신청되었거나 승인된 게시판 이름입니다.");
        }

        var now = DateTime.UtcNow;
        var entity = new PlatformCommunityBoardRequest
        {
            AppKey = appKey,
            BoardKey = boardKey,
            Title = title,
            Description = Normalize(request.Description, string.Empty, 500),
            RequestedByUserId = normalizedRequesterUserId,
            RequestedBy = requestedBy,
            RequestReason = reason,
            Status = PlatformCommunityBoardRequestStatuses.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.PlatformCommunityBoardRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(entity, includeReviewDetails: true);
    }

    public Task<Result<PlatformCommunityBoardResponse>> 승인Async(
        long id,
        PlatformCommunityBoardReviewRequest? request,
        string reviewerUserId,
        CancellationToken cancellationToken)
        => 검토Async(
            id,
            request,
            reviewerUserId,
            PlatformCommunityBoardRequestStatuses.Approved,
            cancellationToken);

    public Task<Result<PlatformCommunityBoardResponse>> 반려Async(
        long id,
        PlatformCommunityBoardReviewRequest? request,
        string reviewerUserId,
        CancellationToken cancellationToken)
        => 검토Async(
            id,
            request,
            reviewerUserId,
            PlatformCommunityBoardRequestStatuses.Rejected,
            cancellationToken);

    private async Task<Result<PlatformCommunityBoardResponse>> 검토Async(
        long id,
        PlatformCommunityBoardReviewRequest? request,
        string reviewerUserId,
        string status,
        CancellationToken cancellationToken)
    {
        var normalizedReviewerUserId = Normalize(reviewerUserId, string.Empty, 450);
        if (string.IsNullOrWhiteSpace(normalizedReviewerUserId))
        {
            return Result.Fail<PlatformCommunityBoardResponse>("게시판 신청을 검토한 관리자 식별자가 필요합니다.");
        }

        var entity = await _db.PlatformCommunityBoardRequests
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return Result.Fail<PlatformCommunityBoardResponse>("게시판 개설 신청을 찾을 수 없습니다.");
        }

        if (!string.Equals(
                entity.Status,
                PlatformCommunityBoardRequestStatuses.Pending,
                StringComparison.Ordinal))
        {
            return Result.Fail<PlatformCommunityBoardResponse>("이미 검토가 끝난 게시판 신청입니다.");
        }

        var now = DateTime.UtcNow;
        entity.Status = status;
        entity.OperatorMemo = Normalize(request?.OperatorMemo, string.Empty, 1000);
        entity.ReviewedByUserId = normalizedReviewerUserId;
        entity.ApprovedAtUtc = status == PlatformCommunityBoardRequestStatuses.Approved ? now : null;
        entity.RejectedAtUtc = status == PlatformCommunityBoardRequestStatuses.Rejected ? now : null;
        entity.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(entity, includeReviewDetails: true);
    }

    private static PlatformCommunityBoardResponse ToResponse(
        PlatformCommunityBoardRequest entity,
        bool includeReviewDetails)
        => new()
        {
            Id = entity.Id,
            AppKey = entity.AppKey,
            BoardKey = entity.BoardKey,
            Title = entity.Title,
            Description = entity.Description,
            RequestedBy = entity.RequestedBy,
            RequestReason = includeReviewDetails ? entity.RequestReason : string.Empty,
            Status = entity.Status,
            StatusName = ToStatusName(entity.Status),
            OperatorMemo = includeReviewDetails ? entity.OperatorMemo : null,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            ApprovedAtUtc = entity.ApprovedAtUtc,
            RejectedAtUtc = includeReviewDetails ? entity.RejectedAtUtc : null
        };

    private static string ToStatusName(string status)
        => status switch
        {
            PlatformCommunityBoardRequestStatuses.Pending => "승인 대기",
            PlatformCommunityBoardRequestStatuses.Approved => "개설 승인",
            PlatformCommunityBoardRequestStatuses.Rejected => "반려",
            _ => status
        };

    private static string CreateBoardKey(string title)
    {
        var normalized = new string(title.Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return Normalize(normalized.Trim('-'), Guid.NewGuid().ToString("N")[..12], 80);
    }

    private static string Normalize(string? value, string fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
