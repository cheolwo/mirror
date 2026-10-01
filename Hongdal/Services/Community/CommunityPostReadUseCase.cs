using FluentResults;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Domain.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Content,
    HongdalModuleKind.Application,
    "게시글 목록·게시판 요약·상세와 공개 가능한 원장 문맥을 읽기 전용으로 구성",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "게시글과 원장 문맥을 읽기만 하며 추천, 댓글, 발행 상태 또는 원장 상태를 변경하지 않습니다.")]
public sealed class 커뮤니티게시글조회UseCase : I커뮤니티게시글조회UseCase
{
    private readonly HongdalContext _db;
    private readonly I게시글원장표시ContextService _ledgerContextService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public 커뮤니티게시글조회UseCase(
        HongdalContext db,
        I게시글원장표시ContextService ledgerContextService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _ledgerContextService = ledgerContextService;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<Result<PlatformCommunityPostListResponse>> 목록Async(
        string? appKey,
        string? category,
        string? boardKey,
        string? workflowTag,
        string? roleTag,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var protectedCategoryNames = CommunityBoardCatalog
            .CategoryNamesFor(CommunityBoardKeys.SafetyReport);
        var query = _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => !post.IsDeleted
                           && !post.IsReportBoardPost
                           && !protectedCategoryNames.Contains(post.Category));

        if (!string.IsNullOrWhiteSpace(appKey))
        {
            var normalizedAppKey = Normalize(appKey, "platform", 80);
            query = query.Where(post => post.AppKey == normalizedAppKey || post.AppKey == "platform");
        }

        if (!string.IsNullOrWhiteSpace(boardKey))
        {
            var boardCategoryNames = await ResolveBoardCategoryNamesAsync(
                appKey,
                boardKey,
                cancellationToken);
            query = query.Where(post => boardCategoryNames.Contains(post.Category));
        }
        else if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryNames = CommunityBoardCatalog.CategoryNamesFor(category);
            query = query.Where(post => categoryNames.Contains(post.Category));
        }

        if (!string.IsNullOrWhiteSpace(workflowTag))
        {
            var normalizedWorkflowTag = Normalize(workflowTag, string.Empty, 60);
            query = query.Where(post => post.WorkflowTag == normalizedWorkflowTag);
        }

        if (!string.IsNullOrWhiteSpace(roleTag))
        {
            var normalizedRoleTag = Normalize(roleTag, string.Empty, 40);
            query = query.Where(post => post.RoleTag == normalizedRoleTag);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .Include(post => post.Attachments)
                .ThenInclude(attachment => attachment.Comments)
            .Include(post => post.Comments.Where(comment => !comment.IsDeleted && !comment.IsOperatorHidden))
            .AsSplitQuery()
            .OrderByDescending(post => post.IsOperatorPinned)
            .ThenByDescending(post => post.OperatorPinnedAtUtc)
            .ThenByDescending(post => post.IsCommunityMomentumPromoted)
            .ThenByDescending(post => post.CommunityMomentumUpdatedAtUtc)
            .ThenByDescending(post => post.RecommendationCount)
            .ThenByDescending(post => post.LastEngagedAtUtc)
            .ThenByDescending(post => post.PublishedAtUtc ?? post.CreatedAtUtc)
            .ThenByDescending(post => post.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result.Ok(new PlatformCommunityPostListResponse
        {
            Items = entities
                .Select(entity => CommunityPostResponseMapper.ToResponse(entity))
                .ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<IReadOnlyList<CommunityBoardSummaryResponse>>> 게시판요약목록Async(
        string? appKey,
        CancellationToken cancellationToken)
    {
        var protectedCategoryNames = CommunityBoardCatalog
            .CategoryNamesFor(CommunityBoardKeys.SafetyReport);
        var query = _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => !post.IsDeleted
                           && !post.IsReportBoardPost
                           && !protectedCategoryNames.Contains(post.Category));

        var normalizedAppKey = string.IsNullOrWhiteSpace(appKey)
            ? null
            : Normalize(appKey, "platform", 80);
        if (normalizedAppKey is not null)
        {
            query = query.Where(post => post.AppKey == normalizedAppKey || post.AppKey == "platform");
        }

        var categoryCounts = await query
            .GroupBy(post => post.Category)
            .Select(group => new CommunityBoardCategoryCount(
                group.Key,
                group.Count(),
                group.Max(post => post.PublishedAtUtc ?? post.CreatedAtUtc)))
            .ToListAsync(cancellationToken);
        var summaries = CommunityBoardCatalog.PublicBoards
            .Select(board => BuildBoardSummary(board, categoryCounts))
            .ToList();

        var customBoardsQuery = _db.PlatformCommunityBoardRequests
            .AsNoTracking()
            .Where(board => !board.IsDeleted
                            && board.Status == PlatformCommunityBoardRequestStatuses.Approved);
        if (normalizedAppKey is not null)
        {
            customBoardsQuery = customBoardsQuery.Where(board =>
                board.AppKey == normalizedAppKey || board.AppKey == "platform");
        }

        var customBoards = await customBoardsQuery
            .OrderBy(board => board.Title)
            .ToListAsync(cancellationToken);
        foreach (var board in customBoards)
        {
            if (CommunityBoardCatalog.Find(board.BoardKey) is not null
                || CommunityBoardCatalog.Find(board.Title) is not null)
            {
                continue;
            }

            var count = categoryCounts.FirstOrDefault(item =>
                string.Equals(item.Category, board.Title, StringComparison.OrdinalIgnoreCase));
            summaries.Add(new CommunityBoardSummaryResponse
            {
                BoardKey = board.BoardKey,
                DisplayName = board.Title,
                Description = board.Description,
                GroupCode = CommunityBoardGroupCodes.PeopleAndInformation,
                GroupDisplayName = "구성원 게시판",
                IsUserCreatable = true,
                IsCustom = true,
                PostingAccessCode = CommunityBoardPostingAccessCodes.Authenticated,
                PostingAccessDisplayName = CommunityBoardPostingAccessCodes.DisplayName(
                    CommunityBoardPostingAccessCodes.Authenticated),
                AllowsAnonymousPosting = false,
                PostCount = count?.Count ?? 0,
                LatestPostAtUtc = count?.LatestPostAtUtc
            });
        }

        return Result.Ok<IReadOnlyList<CommunityBoardSummaryResponse>>(summaries);
    }

    public async Task<Result<PlatformCommunityPostResponse>> 상세Async(
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _db.PlatformCommunityPosts
            .AsNoTracking()
            .Include(post => post.Attachments)
                .ThenInclude(attachment => attachment.Comments)
            .Include(post => post.Comments.Where(comment => !comment.IsDeleted && !comment.IsOperatorHidden))
            .AsSplitQuery()
            .FirstOrDefaultAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound("게시글을 찾을 수 없습니다.");
        }

        var isProtectedReport = entity.IsReportBoardPost || IsReportCategory(entity.Category);
        var ledgerContext = isProtectedReport
            ? null
            : CommunityLedgerCompletionPublication.IsSystemPost(entity)
                ? await _ledgerContextService.비식별성립사례조회Async(
                    entity.커뮤니티원장Id,
                    cancellationToken)
                : await _ledgerContextService.조회Async(
                    entity.커뮤니티원장Id,
                    _currentUserAccessor.UserId,
                    cancellationToken);
        return Result.Ok(CommunityPostResponseMapper.ToResponse(entity, ledgerContext));
    }

    private async Task<IReadOnlyList<string>> ResolveBoardCategoryNamesAsync(
        string? appKey,
        string boardKey,
        CancellationToken cancellationToken)
    {
        var catalogBoard = CommunityBoardCatalog.Find(boardKey);
        if (catalogBoard is not null)
        {
            return CommunityBoardCatalog.CategoryNamesFor(catalogBoard.Key);
        }

        var normalizedBoardKey = Normalize(boardKey, string.Empty, 80);
        var normalizedAppKey = string.IsNullOrWhiteSpace(appKey)
            ? null
            : Normalize(appKey, "platform", 80);
        var query = _db.PlatformCommunityBoardRequests
            .AsNoTracking()
            .Where(board => !board.IsDeleted
                            && board.Status == PlatformCommunityBoardRequestStatuses.Approved
                            && board.BoardKey == normalizedBoardKey);
        if (normalizedAppKey is not null)
        {
            query = query.Where(board => board.AppKey == normalizedAppKey);
        }

        var customBoardTitle = await query
            .Select(board => board.Title)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(customBoardTitle)
            ? [normalizedBoardKey]
            : [customBoardTitle];
    }

    private static CommunityBoardSummaryResponse BuildBoardSummary(
        CommunityBoardDefinition board,
        IReadOnlyList<CommunityBoardCategoryCount> categoryCounts)
    {
        var matchingCounts = categoryCounts
            .Where(item => CommunityBoardCatalog.MatchesCategory(board.Key, item.Category))
            .ToArray();
        return new CommunityBoardSummaryResponse
        {
            BoardKey = board.Key,
            DisplayName = board.DisplayName,
            Description = board.Description,
            GroupCode = board.GroupCode,
            GroupDisplayName = board.GroupDisplayName,
            IsUserCreatable = board.IsUserCreatable,
            PostingAccessCode = board.PostingAccessCode,
            PostingAccessDisplayName = board.PostingAccessDisplayName,
            AllowsAnonymousPosting = board.AllowsAnonymousPosting,
            PostCount = matchingCounts.Sum(item => item.Count),
            LatestPostAtUtc = matchingCounts
                .Select(item => (DateTime?)item.LatestPostAtUtc)
                .Max()
        };
    }

    private static bool IsReportCategory(string? category)
        => !string.IsNullOrWhiteSpace(category)
           && (category.Contains("신고", StringComparison.OrdinalIgnoreCase)
               || category.Contains("분쟁", StringComparison.OrdinalIgnoreCase)
               || category.Contains("report", StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string? value, string fallback, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static Result<PlatformCommunityPostResponse> NotFound(string message)
        => Result.Fail<PlatformCommunityPostResponse>(
            new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private sealed record CommunityBoardCategoryCount(
        string Category,
        int Count,
        DateTime LatestPostAtUtc);
}
